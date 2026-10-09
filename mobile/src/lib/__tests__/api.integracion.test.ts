/**
 * Prueba de punta a punta contra una API real. Solo corre si se define HE_API_URL, por ejemplo:
 *   HE_API_URL=http://localhost:5228 npm test -- api.integracion
 * Crea un usuario nuevo en esa base de datos: úsala contra el entorno local, no contra producción.
 *
 * @jest-environment node
 */
import * as http from 'node:http';
import * as https from 'node:https';
import { ClienteApi, type AlmacenTokens, type TokensGuardados } from '../api';
import { hoy, quincenaDe } from '../tiempo';

// jest-expo reemplaza fetch por un sustituto sin red; aquí basta un fetch mínimo con node:http.
const fetchNode = ((destino: string, init: RequestInit = {}) =>
  new Promise((resolver, rechazar) => {
    const u = new URL(destino);
    const req = (u.protocol === 'https:' ? https : http).request(
      u,
      { method: init.method ?? 'GET', headers: init.headers as Record<string, string> },
      (res) => {
        let texto = '';
        res.setEncoding('utf8');
        res.on('data', (c) => (texto += c));
        res.on('end', () =>
          resolver({
            ok: (res.statusCode ?? 0) >= 200 && (res.statusCode ?? 0) < 300,
            status: res.statusCode,
            headers: { get: (h: string) => (res.headers[h.toLowerCase()] as string | undefined) ?? null },
            text: async () => texto,
            json: async () => JSON.parse(texto),
          }),
        );
      },
    );
    req.on('error', rechazar);
    if (init.body) req.write(init.body);
    req.end();
  })) as unknown as typeof fetch;

const url = process.env.HE_API_URL;
const describir = url ? describe : describe.skip;

describir('API real', () => {
  let guardado: TokensGuardados | null = null;
  const almacen: AlmacenTokens = { leer: async () => guardado, guardar: async (t) => void (guardado = t) };
  const api = new ClienteApi({ baseUrl: url ?? 'http://localhost', almacen, fetch: fetchNode });
  const email = `movil-${Date.now()}@ejemplo.co`;

  it('registra, ingresa y recorre el flujo de la app', async () => {
    await expect(api.registrar(email, 'corta')).rejects.toThrow(/clave/i);
    await api.registrar(email, 'Clave.Segura1');
    await api.ingresar(email, 'Clave.Segura1');
    expect(guardado?.accessToken).toBeTruthy();

    // Salario y recargos
    const quincena = quincenaDe(hoy());
    const s = await api.guardarSalario({ vigenteDesde: quincena.desde, salarioMensual: 2_100_000, valorHoraManual: null });
    expect((await api.salarios()).map((x) => x.id)).toEqual([s.id]);
    const r = await api.recargos();
    expect(r.modo).toBe('Ley');
    const manual = await api.guardarRecargos({ ...r, modo: 'Manual', inicioNocturno: '21:00:00' });
    expect(manual).toMatchObject({ modo: 'Manual', inicioNocturno: '21:00:00' });
    await api.guardarRecargos({ ...manual, modo: 'Ley' });
    expect((await api.ley()).horasMensuales).toBeGreaterThan(0);

    // Cronómetro
    expect(await api.estado()).toBeNull();
    expect((await api.iniciar('desde la app')).estado).toBe('EnCurso');
    await expect(api.iniciar()).rejects.toThrow('Ya tienes una sesión abierta');
    expect((await api.pausar()).estado).toBe('Pausada');
    expect((await api.continuar()).estado).toBe('EnCurso');
    const fin = await api.finalizar();
    expect(fin.estado).toBe('Finalizada');
    expect(fin.tramos).toHaveLength(2);

    // Registro manual de una hora que terminó hace 10 minutos
    const finManual = new Date(Date.now() - 10 * 60_000);
    const m = await api.registrarManual(new Date(finManual.getTime() - 3_600_000), finManual, 'olvido');
    expect(m.segundosAcumulados).toBe(3600);
    await expect(api.registrarManual(finManual, new Date(finManual.getTime() - 1), '')).rejects.toThrow(
      'El fin debe ser posterior al inicio.',
    );

    const sesiones = await api.sesiones();
    expect(sesiones.map((x) => x.id)).toEqual(expect.arrayContaining([fin.id, m.id]));

    // Liquidación
    const liq = await api.liquidacion(quincena);
    expect(liq.desde).toBe(quincena.desde);
    expect(liq.horasTotales).toBeGreaterThanOrEqual(1);
    expect(liq.valorTotal).toBeGreaterThan(0);
    const g = await api.guardarLiquidacion(quincena);
    expect((await api.liquidaciones())[0].id).toBe(g.id);
    expect((await api.liquidacionGuardada(g.id)).valorTotal).toBe(g.valorTotal);

    // Borrado
    await api.eliminarSesion(m.id);
    await expect(api.eliminarSesion(m.id)).rejects.toThrow('Sesión no encontrada.');
    await api.eliminarSalario(s.id);
    expect(await api.salarios()).toEqual([]);

    // Renovación del token con el servidor real
    guardado = { ...guardado!, venceEn: 0 };
    const antes = guardado.refreshToken;
    await api.estado();
    expect(guardado.refreshToken).not.toBe(antes);

    // Token de renovación inválido: la sesión termina
    guardado = { accessToken: 'x', refreshToken: 'x', venceEn: 0 };
    await expect(api.estado()).rejects.toThrow('Tu sesión expiró');
    expect(guardado).toBeNull();
  }, 60_000);
});
