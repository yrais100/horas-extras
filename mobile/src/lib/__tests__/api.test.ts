import { ApiError, ClienteApi, normalizarBaseUrl, type AlmacenTokens, type TokensGuardados } from '../api';

function almacenEnMemoria(inicial: TokensGuardados | null = null): AlmacenTokens & { valor: TokensGuardados | null } {
  return {
    valor: inicial,
    async leer() {
      return this.valor;
    },
    async guardar(t) {
      this.valor = t;
    },
  };
}

function respuesta(status: number, cuerpo?: unknown, tipo = 'application/json'): Response {
  const texto = cuerpo === undefined ? '' : JSON.stringify(cuerpo);
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: { get: (h: string) => (h.toLowerCase() === 'content-type' && texto ? tipo : null) },
    json: async () => JSON.parse(texto),
    text: async () => texto,
  } as unknown as Response;
}

type Llamada = { url: string; init: RequestInit };

function fetchFalso(responder: (l: Llamada) => Response) {
  const llamadas: Llamada[] = [];
  const f = jest.fn(async (url: string, init: RequestInit) => {
    const l = { url, init };
    llamadas.push(l);
    return responder(l);
  });
  return { f: f as unknown as typeof fetch, llamadas };
}

const tokens = (n: number) => ({ tokenType: 'Bearer', accessToken: `acceso${n}`, expiresIn: 3600, refreshToken: `renovar${n}` });
const auth = (l: Llamada) => (l.init.headers as Record<string, string>).Authorization;

describe('normalizarBaseUrl', () => {
  it('quita la barra final y exige http(s)', () => {
    expect(normalizarBaseUrl(' https://horasextras.azurewebsites.net/ ')).toBe('https://horasextras.azurewebsites.net');
    expect(normalizarBaseUrl('http://192.168.1.20:5228')).toBe('http://192.168.1.20:5228');
    expect(normalizarBaseUrl('horasextras.azurewebsites.net')).toBeNull();
    expect(normalizarBaseUrl('ftp://x')).toBeNull();
  });
});

describe('ClienteApi', () => {
  const ahora = 1_000_000;

  it('ingresa y guarda los tokens con su vencimiento', async () => {
    const almacen = almacenEnMemoria();
    const { f, llamadas } = fetchFalso(() => respuesta(200, tokens(1)));
    const api = new ClienteApi({ baseUrl: 'http://api/', almacen, fetch: f, ahora: () => ahora });
    await api.ingresar(' ana@ejemplo.co ', 'Clave.Segura1');
    expect(llamadas[0].url).toBe('http://api/api/auth/login');
    expect(JSON.parse(llamadas[0].init.body as string)).toEqual({ email: 'ana@ejemplo.co', password: 'Clave.Segura1' });
    expect(almacen.valor).toEqual({ accessToken: 'acceso1', refreshToken: 'renovar1', venceEn: ahora + 3_600_000 });
  });

  it('traduce el 401 del ingreso a un mensaje claro', async () => {
    const { f } = fetchFalso(() => respuesta(401, { title: 'Unauthorized', status: 401, detail: 'Failed' }, 'application/problem+json'));
    const api = new ClienteApi({ baseUrl: 'http://api', almacen: almacenEnMemoria(), fetch: f });
    await expect(api.ingresar('a@b.co', 'mala')).rejects.toEqual(new ApiError(401, 'Correo o clave incorrectos.'));
  });

  it('junta los errores de validación del registro', async () => {
    const { f } = fetchFalso(() =>
      respuesta(
        400,
        { errors: { PasswordTooShort: ['La clave debe tener al menos 6 caracteres.'], PasswordRequiresDigit: ['La clave debe tener al menos un número.'] } },
        'application/problem+json',
      ),
    );
    const api = new ClienteApi({ baseUrl: 'http://api', almacen: almacenEnMemoria(), fetch: f });
    await expect(api.registrar('a@b.co', 'x')).rejects.toThrow(
      'La clave debe tener al menos 6 caracteres.\nLa clave debe tener al menos un número.',
    );
  });

  it('muestra el detalle de las reglas de negocio (409)', async () => {
    const almacen = almacenEnMemoria({ accessToken: 'a', refreshToken: 'r', venceEn: ahora + 3_600_000 });
    const { f } = fetchFalso(() =>
      respuesta(409, { status: 409, detail: 'Ya tienes una sesión abierta. Pausa, continúa o finalízala.' }, 'application/problem+json'),
    );
    const api = new ClienteApi({ baseUrl: 'http://api', almacen, fetch: f, ahora: () => ahora });
    await expect(api.iniciar()).rejects.toThrow('Ya tienes una sesión abierta. Pausa, continúa o finalízala.');
  });

  it('envía el token y entiende null y 204', async () => {
    const almacen = almacenEnMemoria({ accessToken: 'a', refreshToken: 'r', venceEn: ahora + 3_600_000 });
    const { f, llamadas } = fetchFalso((l) => (l.init.method === 'DELETE' ? respuesta(204) : respuesta(200, null)));
    const api = new ClienteApi({ baseUrl: 'http://api', almacen, fetch: f, ahora: () => ahora });
    expect(await api.estado()).toBeNull();
    expect(await api.eliminarSesion(3)).toBeUndefined();
    expect(auth(llamadas[0])).toBe('Bearer a');
    expect(llamadas[1].url).toBe('http://api/api/sesiones/3');
  });

  it('pasa el periodo en la consulta', async () => {
    const almacen = almacenEnMemoria({ accessToken: 'a', refreshToken: 'r', venceEn: ahora + 3_600_000 });
    const { f, llamadas } = fetchFalso(() => respuesta(200, []));
    const api = new ClienteApi({ baseUrl: 'http://api', almacen, fetch: f, ahora: () => ahora });
    await api.liquidacion({ desde: '2026-10-01', hasta: '2026-10-15' });
    expect(llamadas[0].url).toBe('http://api/api/liquidacion?desde=2026-10-01&hasta=2026-10-15');
  });

  it('renueva el token antes de que venza, una sola vez para varias peticiones', async () => {
    const almacen = almacenEnMemoria({ accessToken: 'viejo', refreshToken: 'renovar0', venceEn: ahora + 30_000 });
    const { f, llamadas } = fetchFalso((l) => (l.url.endsWith('/refresh') ? respuesta(200, tokens(1)) : respuesta(200, [])));
    const api = new ClienteApi({ baseUrl: 'http://api', almacen, fetch: f, ahora: () => ahora });
    await Promise.all([api.salarios(), api.liquidaciones()]);
    const renovaciones = llamadas.filter((l) => l.url.endsWith('/api/auth/refresh'));
    expect(renovaciones).toHaveLength(1);
    expect(JSON.parse(renovaciones[0].init.body as string)).toEqual({ refreshToken: 'renovar0' });
    expect(llamadas.filter((l) => !l.url.endsWith('/refresh')).map(auth)).toEqual(['Bearer acceso1', 'Bearer acceso1']);
  });

  it('si el servidor rechaza el token, renueva y reintenta', async () => {
    const almacen = almacenEnMemoria({ accessToken: 'revocado', refreshToken: 'renovar0', venceEn: ahora + 3_600_000 });
    const { f, llamadas } = fetchFalso((l) => {
      if (l.url.endsWith('/refresh')) return respuesta(200, tokens(2));
      return auth(l) === 'Bearer acceso2' ? respuesta(200, { id: 1 }) : respuesta(401);
    });
    const api = new ClienteApi({ baseUrl: 'http://api', almacen, fetch: f, ahora: () => ahora });
    expect(await api.pausar()).toEqual({ id: 1 });
    expect(llamadas).toHaveLength(3);
  });

  it('si la renovación falla, borra los tokens y avisa que la sesión expiró', async () => {
    const almacen = almacenEnMemoria({ accessToken: 'a', refreshToken: 'vencido', venceEn: ahora - 1 });
    const alExpirar = jest.fn();
    const { f } = fetchFalso(() => respuesta(401));
    const api = new ClienteApi({ baseUrl: 'http://api', almacen, fetch: f, ahora: () => ahora, alExpirar });
    await expect(api.estado()).rejects.toEqual(new ApiError(401, 'Tu sesión expiró. Vuelve a ingresar.'));
    expect(almacen.valor).toBeNull();
    expect(alExpirar).toHaveBeenCalled();
  });

  it('sin conexión da un mensaje con la dirección del servidor', async () => {
    const f = jest.fn(async () => {
      throw new TypeError('Network request failed');
    }) as unknown as typeof fetch;
    const api = new ClienteApi({ baseUrl: 'http://192.168.1.20:5228', almacen: almacenEnMemoria(), fetch: f });
    await expect(api.ingresar('a@b.co', 'x')).rejects.toMatchObject({ status: 0, message: expect.stringContaining('192.168.1.20:5228') });
  });
});
