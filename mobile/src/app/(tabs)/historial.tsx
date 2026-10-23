import { useCallback, useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { SelectorFecha, SelectorFechaHora, SelectorHora } from '../../components/Selectores';
import { Aviso, Boton, Campo, colores, Fila, Pantalla, Suave, Tarjeta } from '../../components/ui';
import type { ClienteApi } from '../../lib/api';
import { fecha, fechaHoraLocal, horaLocal, horas, leerNumero, nombreEstado, pesos } from '../../lib/formato';
import { abrirPdfLiquidacion } from '../../lib/reporte';
import { useSesion } from '../../lib/sesion';
import { aColombia, fechaDe, HORA_INICIO_POR_DEFECTO, horaDeMinutos, hoy, leerUtc, tipoDeDia } from '../../lib/tiempo';
import type { Festivo, Revision, Sesion } from '../../lib/tipos';
import { mensajeDe, useCarga } from '../../lib/useCarga';

const REVISION: Record<Revision, { texto: string; color: string }> = {
  Pendiente: { texto: 'Por aprobar', color: colores.advertencia },
  Aprobada: { texto: 'Aprobada', color: colores.exito },
  Rechazada: { texto: 'Rechazada', color: colores.peligro },
};

function Insignia({ revision }: { revision: Revision }) {
  const r = REVISION[revision];
  return <Text style={[e.insignia, { color: r.color, borderColor: r.color }]}>{r.texto}</Text>;
}

/** Hora actual redondeada hacia abajo a 5 minutos. */
function ahoraRedondeado(): Date {
  const paso = 5 * 60 * 1000;
  return new Date(Math.floor(Date.now() / paso) * paso);
}

/** Lee la cantidad de horas escrita ("2", "1,5" o "1.5"). */
function leerHoras(texto: string): number {
  const h = leerNumero(texto);
  if (h === null || h <= 0) throw new Error('Escribe cuántas horas trabajaste, por ejemplo 2 o 1,5.');
  return h;
}

function Segmentos<T extends string>({ opciones, valor, alCambiar }: { opciones: [T, string][]; valor: T; alCambiar: (v: T) => void }) {
  return (
    <View style={e.segmentos}>
      {opciones.map(([v, texto]) => (
        <Pressable
          key={v}
          accessibilityRole="button"
          accessibilityState={{ selected: valor === v }}
          onPress={() => alCambiar(v)}
          style={[e.segmento, valor === v && e.segmentoActivo]}
        >
          <Text style={[e.textoSegmento, valor === v && { color: '#fff' }]}>{texto}</Text>
        </Pressable>
      ))}
    </View>
  );
}

/** Edición de una sesión: fecha, hora de inicio, horas y observaciones (solo observaciones si sigue abierta). */
function EditorSesion({
  sesion,
  api,
  festivos,
  alTerminar,
}: {
  sesion: Sesion;
  api: ClienteApi;
  festivos: Festivo[];
  alTerminar: (error: string | null, guardado: boolean) => void;
}) {
  const inicio = sesion.inicioUtc ? leerUtc(sesion.inicioUtc) : new Date();
  const p = aColombia(inicio);
  const soloNota = sesion.estado !== 'Finalizada';
  const [dia, setDia] = useState(fechaDe(inicio));
  const [minutos, setMinutos] = useState(p.hora * 60 + p.minuto);
  const [cantidad, setCantidad] = useState(String(Math.round((sesion.segundosAcumulados / 3600) * 100) / 100).replace('.', ','));
  const [nota, setNota] = useState(sesion.nota ?? '');
  const [guardando, setGuardando] = useState(false);

  const guardar = async () => {
    setGuardando(true);
    try {
      if (soloNota) await api.cambiarNota(sesion.id, nota);
      else await api.ajustarSesion(sesion.id, { fecha: dia, horaInicio: horaDeMinutos(minutos), horas: leerHoras(cantidad), nota });
      alTerminar(null, true);
    } catch (err) {
      alTerminar(mensajeDe(err), false);
      setGuardando(false);
    }
  };

  return (
    <View style={e.editor}>
      {soloNota ? (
        <Suave>La sesión sigue abierta: puedes cambiar las observaciones. Para ajustar el tiempo, finalízala primero.</Suave>
      ) : (
        <>
          {sesion.tramos.length > 1 ? (
            <Suave>Al guardar, las pausas de esta sesión se reemplazan por un solo bloque con las horas indicadas.</Suave>
          ) : null}
          <SelectorFecha etiqueta="Fecha" valor={dia} alCambiar={setDia} />
          <Suave>{tipoDeDia(dia, festivos)}</Suave>
          <Fila campos>
            <SelectorHora etiqueta="Hora de inicio" minutos={minutos} alCambiar={setMinutos} />
            <Campo etiqueta="Horas" value={cantidad} onChangeText={setCantidad} keyboardType="decimal-pad" />
          </Fila>
        </>
      )}
      <Campo etiqueta="Observaciones" value={nota} onChangeText={setNota} multiline maxLength={2000} style={e.multilinea} />
      <Fila>
        <Boton titulo="Guardar" onPress={guardar} ocupado={guardando} />
        <Boton titulo="Cancelar" variante="secundario" onPress={() => alTerminar(null, false)} />
      </Fila>
    </View>
  );
}

export default function Historial() {
  const { api, cuenta } = useSesion();
  const esAdmin = cuenta?.esAdmin ?? false;
  const cargar = useCallback(async () => {
    const anio = Number(hoy().slice(0, 4));
    const [sesiones, liquidaciones, festivos] = await Promise.all([
      api.sesiones(),
      api.liquidaciones(),
      // Los festivos solo sirven de ayuda para indicar el tipo de día; el cálculo lo hace el servidor.
      Promise.all([api.festivos(anio - 1), api.festivos(anio)])
        .then((x) => x.flat())
        .catch(() => [] as Festivo[]),
    ]);
    return { sesiones, liquidaciones, festivos };
  }, [api]);
  const { datos, error, setError, cargando, recargar } = useCarga(cargar);
  const festivos = datos?.festivos ?? [];

  const [modo, setModo] = useState<'horas' | 'intervalo'>('horas');
  const [dia, setDia] = useState(() => hoy());
  const [minutosInicio, setMinutosInicio] = useState(HORA_INICIO_POR_DEFECTO);
  const [cantidad, setCantidad] = useState('');
  const [fin, setFin] = useState(ahoraRedondeado);
  const [inicio, setInicio] = useState(() => new Date(fin.getTime() - 60 * 60 * 1000));
  const [nota, setNota] = useState('');
  const [guardando, setGuardando] = useState(false);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [editando, setEditando] = useState<number | null>(null);
  const [abriendoPdf, setAbriendoPdf] = useState<number | null>(null);

  const descargarPdf = async (id: number) => {
    setAbriendoPdf(id);
    try {
      await abrirPdfLiquidacion(api, id);
    } catch (err) {
      setError(mensajeDe(err));
    } finally {
      setAbriendoPdf(null);
    }
  };

  const agregar = async () => {
    setMensaje(null);
    setGuardando(true);
    let fallo: string | null = null;
    try {
      if (modo === 'horas') {
        await api.registrarPorHoras({ fecha: dia, horaInicio: horaDeMinutos(minutosInicio), horas: leerHoras(cantidad), nota });
        setCantidad('');
      } else {
        await api.registrarManual(inicio, fin, nota.trim());
      }
      setNota('');
      setMensaje('Registro agregado.');
    } catch (err) {
      fallo = mensajeDe(err);
    }
    await recargar();
    if (fallo) setError(fallo);
    setGuardando(false);
  };

  const terminarEdicion = async (fallo: string | null, guardado: boolean) => {
    if (fallo) {
      setError(fallo);
      return;
    }
    setEditando(null);
    if (guardado) {
      setMensaje(null);
      await recargar();
    }
  };

  const eliminar = (id: number, descripcion: string) =>
    Alert.alert('Eliminar sesión', `¿Eliminar la sesión del ${descripcion}? No se puede deshacer.`, [
      { text: 'Cancelar', style: 'cancel' },
      {
        text: 'Eliminar',
        style: 'destructive',
        onPress: async () => {
          let fallo: string | null = null;
          try {
            await api.eliminarSesion(id);
          } catch (e) {
            fallo = mensajeDe(e);
          }
          await recargar();
          if (fallo) setError(fallo);
        },
      },
    ]);

  return (
    <Pantalla cargando={cargando} alRecargar={recargar}>
      {error ? <Aviso tipo="error">{error}</Aviso> : null}

      <Tarjeta titulo="Registro manual">
        <Suave>
          Para olvidos o correcciones. Hora de Colombia. El recargo se calcula según el día (ordinario, dominical o festivo)
          y la franja diurna o nocturna.
          {esAdmin ? '' : ' Tus registros suman en la liquidación cuando el administrador los aprueba.'}
        </Suave>
        <Segmentos
          opciones={[
            ['horas', 'Por horas'],
            ['intervalo', 'Inicio y fin'],
          ]}
          valor={modo}
          alCambiar={setModo}
        />
        {modo === 'horas' ? (
          <>
            <SelectorFecha etiqueta="Fecha" valor={dia} alCambiar={setDia} />
            <Suave>{tipoDeDia(dia, festivos)}</Suave>
            <Fila campos>
              <SelectorHora etiqueta="Hora de inicio" minutos={minutosInicio} alCambiar={setMinutosInicio} />
              <Campo etiqueta="Horas" value={cantidad} onChangeText={setCantidad} keyboardType="decimal-pad" placeholder="Ej. 2 o 1,5" />
            </Fila>
          </>
        ) : (
          <>
            <SelectorFechaHora etiqueta="Inicio" valor={inicio} alCambiar={setInicio} />
            <SelectorFechaHora etiqueta="Fin" valor={fin} alCambiar={setFin} />
          </>
        )}
        <Campo
          etiqueta="Observaciones"
          value={nota}
          onChangeText={setNota}
          placeholder="Qué hiciste durante ese tiempo (opcional)"
          multiline
          maxLength={2000}
          style={e.multilinea}
        />
        {mensaje ? <Aviso tipo="exito">{mensaje}</Aviso> : null}
        <Boton titulo="Agregar" onPress={agregar} ocupado={guardando} />
      </Tarjeta>

      <Tarjeta titulo="Sesiones de la quincena">
        {datos?.sesiones.length === 0 ? <Suave>Sin sesiones en esta quincena.</Suave> : null}
        {datos?.sesiones.map((s) =>
          editando === s.id ? (
            <EditorSesion key={s.id} sesion={s} api={api} festivos={festivos} alTerminar={terminarEdicion} />
          ) : (
            <View key={s.id} style={e.fila}>
              <View style={{ flex: 1, gap: 2 }}>
                <Text style={e.principal}>{fechaHoraLocal(s.inicioUtc)}</Text>
                <Suave>
                  {s.finUtc ? `Hasta ${horaLocal(s.finUtc)}` : nombreEstado(s.estado)} · {horas(s.segundosAcumulados / 3600)}
                </Suave>
                {s.nota ? <Suave>{s.nota}</Suave> : null}
                {s.estado === 'Finalizada' ? <Insignia revision={s.revision} /> : null}
                {s.revision === 'Rechazada' && s.comentarioRevision ? (
                  <Text style={{ color: colores.peligro }}>Motivo: {s.comentarioRevision}</Text>
                ) : null}
              </View>
              {esAdmin || s.revision !== 'Aprobada' ? (
                <View style={{ gap: 6 }}>
                  <Boton titulo="Editar" variante="secundario" onPress={() => setEditando(s.id)} />
                  <Boton titulo="Eliminar" variante="peligroBorde" onPress={() => eliminar(s.id, fechaHoraLocal(s.inicioUtc))} />
                </View>
              ) : null}
            </View>
          ),
        )}
      </Tarjeta>

      <Tarjeta titulo="Liquidaciones guardadas">
        {datos?.liquidaciones.length === 0 ? <Suave>Aún no has guardado liquidaciones.</Suave> : null}
        {datos?.liquidaciones.map((l) => (
          <View key={l.id} style={e.fila}>
            <View style={{ flex: 1, gap: 2 }}>
              <Text style={e.principal}>
                {fecha(l.desde)} – {fecha(l.hasta)}
              </Text>
              <Suave>Guardada {fechaHoraLocal(l.creadaEnUtc)}</Suave>
            </View>
            <View style={{ alignItems: 'flex-end' }}>
              <Text style={e.principal}>{pesos(l.valorTotal)}</Text>
              <Suave>{horas(l.horasTotales)}</Suave>
            </View>
            <Boton titulo="PDF" variante="secundario" onPress={() => descargarPdf(l.id)} ocupado={abriendoPdf === l.id} />
          </View>
        ))}
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
  insignia: {
    alignSelf: 'flex-start',
    fontSize: 12,
    fontWeight: '600',
    borderWidth: 1,
    borderRadius: 6,
    paddingHorizontal: 6,
    paddingVertical: 1,
    marginTop: 2,
  },
  editor: {
    gap: 8,
    paddingVertical: 10,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: colores.borde,
  },
  multilinea: { minHeight: 64, textAlignVertical: 'top' },
  segmentos: { flexDirection: 'row', borderWidth: 1, borderColor: colores.primario, borderRadius: 8, overflow: 'hidden' },
  segmento: { flex: 1, paddingVertical: 10, alignItems: 'center' },
  segmentoActivo: { backgroundColor: colores.primario },
  textoSegmento: { color: colores.primario, fontWeight: '600' },
});
