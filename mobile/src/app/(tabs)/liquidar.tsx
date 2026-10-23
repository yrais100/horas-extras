import { useFocusEffect } from 'expo-router';
import { useCallback, useRef, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { ResumenLiquidacion } from '../../components/ResumenLiquidacion';
import { SelectorFecha } from '../../components/Selectores';
import { Aviso, Boton, colores, Fila, Pantalla, Suave, Tarjeta } from '../../components/ui';
import { fecha, horas, pesos } from '../../lib/formato';
import { useSesion } from '../../lib/sesion';
import { abrirPdfLiquidacion } from '../../lib/reporte';
import { hoy, quincenaAnterior, quincenaDe } from '../../lib/tiempo';
import type { Liquidacion, LiquidacionGuardada, Periodo } from '../../lib/tipos';
import { mensajeDe } from '../../lib/useCarga';

export default function Liquidar() {
  const { api } = useSesion();
  const [periodo, setPeriodo] = useState<Periodo>(() => quincenaDe(hoy()));
  const [resultado, setResultado] = useState<Liquidacion | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [cargando, setCargando] = useState(false);
  const [guardando, setGuardando] = useState(false);
  const [guardada, setGuardada] = useState<LiquidacionGuardada | null>(null);
  const [abriendoPdf, setAbriendoPdf] = useState(false);
  const ultimo = useRef(periodo);

  const calcular = useCallback(
    async (p: Periodo) => {
      ultimo.current = p;
      setPeriodo(p);
      setMensaje(null);
      setGuardada(null);
      setCargando(true);
      try {
        setResultado(await api.liquidacion(p));
        setError(null);
      } catch (e) {
        setError(mensajeDe(e));
      } finally {
        setCargando(false);
      }
    },
    [api],
  );

  useFocusEffect(
    useCallback(() => {
      // Al volver a la pestaña se recalcula el último periodo liquidado (puede haber horas nuevas).
      void calcular(ultimo.current);
    }, [calcular]),
  );

  const guardar = async () => {
    setGuardando(true);
    try {
      setGuardada(await api.guardarLiquidacion(periodo));
      setMensaje('Liquidación guardada. La encuentras en Historial, donde también puedes volver a bajar el reporte.');
      setError(null);
    } catch (e) {
      setError(mensajeDe(e));
    } finally {
      setGuardando(false);
    }
  };

  const descargarPdf = async (id: number) => {
    setAbriendoPdf(true);
    try {
      await abrirPdfLiquidacion(api, id);
    } catch (e) {
      setError(mensajeDe(e));
    } finally {
      setAbriendoPdf(false);
    }
  };

  return (
    <Pantalla cargando={cargando} alRecargar={() => calcular(periodo)}>
      <Tarjeta>
        <Fila campos>
          <SelectorFecha etiqueta="Desde" valor={periodo.desde} alCambiar={(desde) => setPeriodo({ ...periodo, desde })} />
          <SelectorFecha etiqueta="Hasta" valor={periodo.hasta} alCambiar={(hasta) => setPeriodo({ ...periodo, hasta })} />
        </Fila>
        <Fila>
          <Boton titulo="← Anterior" variante="secundario" onPress={() => calcular(quincenaAnterior(periodo))} />
          <Boton titulo="Actual" variante="secundario" onPress={() => calcular(quincenaDe(hoy()))} />
          <Boton titulo="Liquidar" onPress={() => calcular(periodo)} ocupado={cargando} />
        </Fila>
      </Tarjeta>

      {error ? <Aviso tipo="error">{error}</Aviso> : null}
      {mensaje ? <Aviso tipo="exito">{mensaje}</Aviso> : null}
      {guardada ? (
        <Boton titulo="Descargar reporte PDF" variante="exito" onPress={() => descargarPdf(guardada.id)} ocupado={abriendoPdf} />
      ) : null}

      {resultado ? (
        <>
          <Tarjeta titulo={`${fecha(resultado.desde)} – ${fecha(resultado.hasta)}`}>
            <ResumenLiquidacion resultado={resultado} />
            <Suave>Valor hora actual: {pesos(resultado.valorHoraActual)}</Suave>
            <Boton titulo="Guardar esta liquidación" variante="exito" onPress={guardar} ocupado={guardando} />
          </Tarjeta>

          <Tarjeta titulo="Detalle por día">
            {resultado.dias.length === 0 ? <Suave>Sin registros en este periodo.</Suave> : null}
            {resultado.dias.map((d) => (
              <View key={d.fecha} style={e.dia}>
                <View style={{ flex: 1, gap: 2 }}>
                  <Text style={e.fecha}>{fecha(d.fecha)}</Text>
                  <View style={{ flexDirection: 'row', gap: 6 }}>
                    {d.dominicalOFestivo ? <Text style={[e.etiqueta, { backgroundColor: '#d7f0f7' }]}>Dom/Fest</Text> : null}
                    {d.excedeLimiteDiario ? <Text style={[e.etiqueta, { backgroundColor: '#fff0c2' }]}>+2 h</Text> : null}
                  </View>
                </View>
                <View style={{ alignItems: 'flex-end' }}>
                  <Suave>
                    Diurnas {horas(d.horasDiurnas)} · Nocturnas {horas(d.horasNocturnas)}
                  </Suave>
                  <Text style={e.fecha}>{pesos(d.valor)}</Text>
                </View>
              </View>
            ))}
          </Tarjeta>
        </>
      ) : null}
    </Pantalla>
  );
}

const e = StyleSheet.create({
  dia: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: colores.borde,
    gap: 8,
  },
  fecha: { fontSize: 15, fontWeight: '600', color: colores.texto },
  etiqueta: { fontSize: 11, paddingHorizontal: 6, paddingVertical: 1, borderRadius: 4, overflow: 'hidden', color: colores.texto },
});
