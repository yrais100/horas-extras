import { useCallback, useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { SelectorFecha, SelectorHora } from '../../components/Selectores';
import { Aviso, Boton, Campo, colores, Fila, Pantalla, Suave, Tarjeta } from '../../components/ui';
import { fechaLarga, horaDelDia, leerNumero, pesos } from '../../lib/formato';
import { useSesion } from '../../lib/sesion';
import { horaDeMinutos, hoy, minutosDeHora, quincenaDe } from '../../lib/tiempo';
import type { ModoRecargos, Recargos, ValoresLey } from '../../lib/tipos';
import { mensajeDe, useCarga } from '../../lib/useCarga';

/** Recargos en edición: las horas como minutos y los números como texto del teclado. */
interface Edicion {
  modo: ModoRecargos;
  inicioNocturno: number;
  finNocturno: number;
  horasMensuales: string;
  pctExtraDiurna: string;
  pctExtraNocturna: string;
  pctExtraDominicalDiurna: string;
  pctExtraDominicalNocturna: string;
}

const PORCENTAJES = [
  ['pctExtraDiurna', '% extra diurna'],
  ['pctExtraNocturna', '% extra nocturna'],
  ['pctExtraDominicalDiurna', '% dom./fest. diurna'],
  ['pctExtraDominicalNocturna', '% dom./fest. nocturna'],
] as const;

function aEdicion(r: Recargos | ValoresLey, modo: ModoRecargos): Edicion {
  return {
    modo,
    inicioNocturno: minutosDeHora(r.inicioNocturno),
    finNocturno: minutosDeHora(r.finNocturno),
    horasMensuales: String(r.horasMensuales),
    pctExtraDiurna: String(r.pctExtraDiurna),
    pctExtraNocturna: String(r.pctExtraNocturna),
    pctExtraDominicalDiurna: String(r.pctExtraDominicalDiurna),
    pctExtraDominicalNocturna: String(r.pctExtraDominicalNocturna),
  };
}

function deEdicion(e: Edicion): Recargos | string {
  const numeros = [e.horasMensuales, ...PORCENTAJES.map(([k]) => e[k])].map(leerNumero);
  if (numeros.some((n) => n === null)) return 'Revisa los números: hay un campo vacío o mal escrito.';
  const [horasMensuales, d, n, dd, dn] = numeros as number[];
  return {
    modo: e.modo,
    inicioNocturno: horaDeMinutos(e.inicioNocturno),
    finNocturno: horaDeMinutos(e.finNocturno),
    horasMensuales,
    pctExtraDiurna: d,
    pctExtraNocturna: n,
    pctExtraDominicalDiurna: dd,
    pctExtraDominicalNocturna: dn,
  };
}

export default function Perfil() {
  const { api, email, servidor, salir } = useSesion();
  const cargar = useCallback(async () => {
    const [salarios, recargos, ley] = await Promise.all([api.salarios(), api.recargos(), api.ley()]);
    return { salarios, recargos, ley };
  }, [api]);
  const { datos, setDatos, error, setError, cargando, recargar } = useCarga(cargar);

  const [desde, setDesde] = useState(() => quincenaDe(hoy()).desde);
  const [salario, setSalario] = useState('');
  const [valorHora, setValorHora] = useState('');
  const [cambios, setCambios] = useState<Edicion | null>(null);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState<'salario' | 'recargos' | null>(null);

  // Lo que el usuario está editando; si no ha tocado nada, lo guardado en el servidor.
  const edicion = cambios ?? (datos?.recargos ? aEdicion(datos.recargos, datos.recargos.modo) : null);
  const editar = (c: Partial<Edicion>) => edicion && setCambios({ ...edicion, ...c });

  const guardarSalario = async () => {
    setMensaje(null);
    const mensual = leerNumero(salario) ?? 0;
    const hora = valorHora.trim() ? leerNumero(valorHora) : null;
    if (valorHora.trim() && hora === null) return setError('El valor hora manual no es un número válido.');
    setOcupado('salario');
    try {
      await api.guardarSalario({ vigenteDesde: desde, salarioMensual: mensual, valorHoraManual: hora });
      const salarios = await api.salarios();
      setDatos((d) => (d ? { ...d, salarios } : d));
      setSalario('');
      setValorHora('');
      setError(null);
      setMensaje('Salario guardado.');
    } catch (e) {
      setError(mensajeDe(e));
    } finally {
      setOcupado(null);
    }
  };

  const eliminarSalario = (id: number, vigente: string) =>
    Alert.alert('Eliminar salario', `¿Eliminar el salario vigente desde el ${fechaLarga(vigente)}?`, [
      { text: 'Cancelar', style: 'cancel' },
      {
        text: 'Eliminar',
        style: 'destructive',
        onPress: async () => {
          try {
            await api.eliminarSalario(id);
            const salarios = await api.salarios();
            setDatos((d) => (d ? { ...d, salarios } : d));
          } catch (e) {
            setError(mensajeDe(e));
          }
        },
      },
    ]);

  const guardarRecargos = async () => {
    if (!edicion) return;
    setMensaje(null);
    const r = deEdicion(edicion);
    if (typeof r === 'string') return setError(r);
    setOcupado('recargos');
    try {
      const guardado = await api.guardarRecargos(r);
      setDatos((d) => (d ? { ...d, recargos: guardado } : d));
      setCambios(null);
      setError(null);
      setMensaje('Recargos guardados.');
    } catch (e) {
      setError(mensajeDe(e));
    } finally {
      setOcupado(null);
    }
  };

  const confirmarSalida = () =>
    Alert.alert('Cerrar sesión', '¿Salir de tu cuenta en este teléfono?', [
      { text: 'Cancelar', style: 'cancel' },
      { text: 'Salir', style: 'destructive', onPress: () => void salir() },
    ]);

  const ley = datos?.ley;

  return (
    <Pantalla cargando={cargando} alRecargar={recargar}>
      {error ? <Aviso tipo="error">{error}</Aviso> : null}
      {mensaje ? <Aviso tipo="exito">{mensaje}</Aviso> : null}

      <Tarjeta titulo="Salario">
        <Suave>
          El valor de cada hora sale de tu salario mensual dividido por las horas mensuales de ley, salvo que ingreses un
          valor hora manual. Si te cambian el salario, agrégalo con la fecha desde la que rige: las horas anteriores se
          siguen liquidando con el salario anterior.
        </Suave>
        <SelectorFecha etiqueta="Vigente desde" valor={desde} alCambiar={setDesde} />
        <Fila campos>
          <Campo etiqueta="Salario mensual (COP)" value={salario} onChangeText={setSalario} keyboardType="numeric" placeholder="Ej.: 2.500.000" />
          <Campo etiqueta="Valor hora manual (opcional)" value={valorHora} onChangeText={setValorHora} keyboardType="numeric" />
        </Fila>
        <Boton titulo="Guardar salario" onPress={guardarSalario} ocupado={ocupado === 'salario'} />
        {datos?.salarios.length === 0 ? <Suave>Aún no has registrado tu salario.</Suave> : null}
        {datos?.salarios.map((s) => (
          <View key={s.id} style={e.fila}>
            <View style={{ flex: 1 }}>
              <Text style={e.principal}>{pesos(s.salarioMensual)}</Text>
              <Suave>
                Desde el {fechaLarga(s.vigenteDesde)}
                {s.valorHoraManual ? ` · valor hora manual ${pesos(s.valorHoraManual)}` : ''}
              </Suave>
            </View>
            <Boton titulo="Eliminar" variante="peligroBorde" onPress={() => eliminarSalario(s.id, s.vigenteDesde)} />
          </View>
        ))}
      </Tarjeta>

      {edicion && ley ? (
        <Tarjeta titulo="Recargos y jornada nocturna">
          <View style={e.segmentos}>
            {(['Ley', 'Manual'] as const).map((m) => (
              <Pressable
                key={m}
                accessibilityRole="button"
                accessibilityState={{ selected: edicion.modo === m }}
                onPress={() => editar({ modo: m })}
                style={[e.segmento, edicion.modo === m && e.segmentoActivo]}
              >
                <Text style={[e.textoSegmento, edicion.modo === m && { color: '#fff' }]}>
                  {m === 'Ley' ? 'Según la ley' : 'Valores manuales'}
                </Text>
              </Pressable>
            ))}
          </View>

          {edicion.modo === 'Ley' ? (
            <Suave>
              Hoy aplica: nocturno de {horaDelDia(ley.inicioNocturno)} a {horaDelDia(ley.finNocturno)}, jornada de{' '}
              {ley.jornadaSemanal} h semanales (divisor {ley.horasMensuales} h/mes), extra diurna +{ley.pctExtraDiurna} %,
              extra nocturna +{ley.pctExtraNocturna} %, dominical/festiva diurna +{ley.pctExtraDominicalDiurna} % y nocturna +
              {ley.pctExtraDominicalNocturna} %. Los cambios de ley se aplican solos según la fecha de cada hora.
            </Suave>
          ) : (
            <>
              <Fila campos>
                <SelectorHora
                  etiqueta="Inicio nocturno"
                  minutos={edicion.inicioNocturno}
                  alCambiar={(m) => editar({ inicioNocturno: m })}
                />
                <SelectorHora
                  etiqueta="Fin nocturno"
                  minutos={edicion.finNocturno}
                  alCambiar={(m) => editar({ finNocturno: m })}
                />
              </Fila>
              <Campo
                etiqueta="Horas mensuales (divisor del salario)"
                value={edicion.horasMensuales}
                onChangeText={(t) => editar({ horasMensuales: t })}
                keyboardType="numeric"
              />
              <Fila campos>
                {PORCENTAJES.map(([k, etiqueta]) => (
                  <Campo
                    key={k}
                    etiqueta={etiqueta}
                    value={edicion[k]}
                    onChangeText={(t) => editar({ [k]: t })}
                    keyboardType="numeric"
                  />
                ))}
              </Fila>
              <Boton titulo="Cargar valores de ley" variante="secundario" onPress={() => setCambios(aEdicion(ley, 'Manual'))} />
            </>
          )}
          <Boton titulo="Guardar recargos" onPress={guardarRecargos} ocupado={ocupado === 'recargos'} />
          <Suave>
            Revisar: valores de ley tomados del Código Sustantivo del Trabajo, la Ley 2101 de 2021 y la Ley 2466 de 2025.
            Confírmalos con tu área de nómina o un abogado laboral.
          </Suave>
        </Tarjeta>
      ) : null}

      <Tarjeta titulo="Cuenta">
        <Suave>{email ?? 'Sesión iniciada'}</Suave>
        <Suave>Servidor: {servidor}</Suave>
        <Boton titulo="Cerrar sesión" variante="peligroBorde" onPress={confirmarSalida} />
      </Tarjeta>
    </Pantalla>
  );
}

const e = StyleSheet.create({
  fila: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingVertical: 8,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: colores.borde,
  },
  principal: { fontSize: 15, fontWeight: '600', color: colores.texto },
  segmentos: { flexDirection: 'row', borderWidth: 1, borderColor: colores.primario, borderRadius: 8, overflow: 'hidden' },
  segmento: { flex: 1, paddingVertical: 10, alignItems: 'center' },
  segmentoActivo: { backgroundColor: colores.primario },
  textoSegmento: { color: colores.primario, fontWeight: '600' },
});
