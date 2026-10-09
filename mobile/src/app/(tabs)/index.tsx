import { useCallback, useEffect, useState } from 'react';
import { Alert, AppState, StyleSheet, Text, View } from 'react-native';
import { ResumenLiquidacion } from '../../components/ResumenLiquidacion';
import { Aviso, Boton, Campo, colores, Fila, Pantalla, Suave, Tarjeta } from '../../components/ui';
import { cronometro, fecha, horaLocal, nombreEstado } from '../../lib/formato';
import { useSesion } from '../../lib/sesion';
import type { Sesion } from '../../lib/tipos';
import { mensajeDe, useCarga } from '../../lib/useCarga';

export default function Hoy() {
  const { api } = useSesion();
  const cargar = useCallback(async () => {
    const [sesion, resumen] = await Promise.all([api.estado(), api.liquidacion()]);
    return { sesion, resumen, marca: Date.now() };
  }, [api]);
  const { datos, error, setError, cargando, recargar } = useCarga(cargar);
  const [ahora, setAhora] = useState(() => Date.now());
  const [nota, setNota] = useState('');
  const [ocupado, setOcupado] = useState(false);

  const sesion = datos?.sesion ?? null;
  const enCurso = sesion?.estado === 'EnCurso';

  // El cronómetro avanza en el teléfono; el servidor guarda la hora real de cada tramo.
  useEffect(() => {
    if (!enCurso) return;
    const t = setInterval(() => setAhora(Date.now()), 1000);
    return () => clearInterval(t);
  }, [enCurso]);

  // Al volver a la app (por ejemplo, tras bloquear el teléfono) se recarga el estado.
  useEffect(() => {
    const s = AppState.addEventListener('change', (e) => {
      if (e === 'active') void recargar();
    });
    return () => s.remove();
  }, [recargar]);

  const accion = async (fn: () => Promise<Sesion>) => {
    setOcupado(true);
    let fallo: string | null = null;
    try {
      await fn();
      setNota('');
    } catch (e) {
      fallo = mensajeDe(e);
    }
    await recargar();
    if (fallo) setError(fallo);
    setOcupado(false);
  };

  const finalizar = () =>
    Alert.alert('Finalizar sesión', '¿Terminaste tus horas extra de esta jornada?', [
      { text: 'Cancelar', style: 'cancel' },
      { text: 'Finalizar', style: 'destructive', onPress: () => accion(api.finalizar) },
    ]);

  const segundos = sesion
    ? sesion.segundosAcumulados + (enCurso && datos ? Math.max(0, (ahora - datos.marca) / 1000) : 0)
    : 0;
  const colorEstado = enCurso ? colores.exito : sesion ? colores.advertencia : colores.suave;

  return (
    <Pantalla cargando={cargando} alRecargar={recargar}>
      <Tarjeta>
        <View style={{ alignItems: 'center', gap: 6 }}>
          <Text style={[e.estado, { backgroundColor: colorEstado }]}>{sesion ? nombreEstado(sesion.estado) : 'Sin iniciar'}</Text>
          <Text style={e.reloj}>{cronometro(segundos)}</Text>
          <Suave>
            {sesion
              ? `Iniciada a las ${horaLocal(sesion.inicioUtc)} · ${sesion.tramos.length} tramo(s)`
              : 'Pulsa Iniciar cuando empieces tus horas extra.'}
          </Suave>
        </View>
        {error ? <Aviso tipo="error">{error}</Aviso> : null}
        {!sesion ? (
          <>
            <Campo etiqueta="Nota (opcional)" value={nota} onChangeText={setNota} placeholder="Ej.: cierre de mes" />
            <Boton titulo="▶  Iniciar" grande onPress={() => accion(() => api.iniciar(nota.trim()))} ocupado={ocupado} deshabilitado={!datos} />
          </>
        ) : (
          <Fila style={{ justifyContent: 'center' }}>
            {enCurso ? (
              <Boton titulo="⏸  Pausar" variante="advertencia" grande onPress={() => accion(api.pausar)} ocupado={ocupado} />
            ) : (
              <Boton titulo="▶  Continuar" grande onPress={() => accion(api.continuar)} ocupado={ocupado} />
            )}
            <Boton titulo="■  Finalizar" variante="peligroBorde" grande onPress={finalizar} deshabilitado={ocupado} />
          </Fila>
        )}
      </Tarjeta>

      {datos?.resumen ? (
        <Tarjeta titulo={`Quincena actual · ${fecha(datos.resumen.desde)} – ${fecha(datos.resumen.hasta)}`}>
          <ResumenLiquidacion resultado={datos.resumen} />
        </Tarjeta>
      ) : null}
    </Pantalla>
  );
}

const e = StyleSheet.create({
  estado: { color: '#fff', fontWeight: '600', paddingHorizontal: 12, paddingVertical: 4, borderRadius: 999, overflow: 'hidden' },
  reloj: { fontSize: 52, fontWeight: '700', fontVariant: ['tabular-nums'], color: colores.texto },
});
