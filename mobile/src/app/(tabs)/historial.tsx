import { useCallback, useState } from 'react';
import { Alert, StyleSheet, Text, View } from 'react-native';
import { SelectorFechaHora } from '../../components/Selectores';
import { Aviso, Boton, Campo, colores, Pantalla, Suave, Tarjeta } from '../../components/ui';
import { fecha, fechaHoraLocal, horaLocal, horas, nombreEstado, pesos } from '../../lib/formato';
import { useSesion } from '../../lib/sesion';
import { mensajeDe, useCarga } from '../../lib/useCarga';

/** Hora actual redondeada hacia abajo a 5 minutos. */
function ahoraRedondeado(): Date {
  const paso = 5 * 60 * 1000;
  return new Date(Math.floor(Date.now() / paso) * paso);
}

export default function Historial() {
  const { api } = useSesion();
  const cargar = useCallback(async () => {
    const [sesiones, liquidaciones] = await Promise.all([api.sesiones(), api.liquidaciones()]);
    return { sesiones, liquidaciones };
  }, [api]);
  const { datos, error, setError, cargando, recargar } = useCarga(cargar);

  const [fin, setFin] = useState(ahoraRedondeado);
  const [inicio, setInicio] = useState(() => new Date(fin.getTime() - 60 * 60 * 1000));
  const [nota, setNota] = useState('');
  const [guardando, setGuardando] = useState(false);
  const [mensaje, setMensaje] = useState<string | null>(null);

  const agregar = async () => {
    setMensaje(null);
    setGuardando(true);
    let fallo: string | null = null;
    try {
      await api.registrarManual(inicio, fin, nota.trim());
      setNota('');
      setMensaje('Registro agregado.');
    } catch (e) {
      fallo = mensajeDe(e);
    }
    await recargar();
    if (fallo) setError(fallo);
    setGuardando(false);
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
        <Suave>Para olvidos o correcciones. Hora de Colombia.</Suave>
        <SelectorFechaHora etiqueta="Inicio" valor={inicio} alCambiar={setInicio} />
        <SelectorFechaHora etiqueta="Fin" valor={fin} alCambiar={setFin} />
        <Campo etiqueta="Nota" value={nota} onChangeText={setNota} placeholder="Opcional" />
        {mensaje ? <Aviso tipo="exito">{mensaje}</Aviso> : null}
        <Boton titulo="Agregar" onPress={agregar} ocupado={guardando} />
      </Tarjeta>

      <Tarjeta titulo="Sesiones de la quincena">
        {datos?.sesiones.length === 0 ? <Suave>Sin sesiones en esta quincena.</Suave> : null}
        {datos?.sesiones.map((s) => (
          <View key={s.id} style={e.fila}>
            <View style={{ flex: 1, gap: 2 }}>
              <Text style={e.principal}>{fechaHoraLocal(s.inicioUtc)}</Text>
              <Suave>
                {s.finUtc ? `Hasta ${horaLocal(s.finUtc)}` : nombreEstado(s.estado)} · {horas(s.segundosAcumulados / 3600)}
              </Suave>
              {s.nota ? <Suave>{s.nota}</Suave> : null}
            </View>
            <Boton titulo="Eliminar" variante="peligroBorde" onPress={() => eliminar(s.id, fechaHoraLocal(s.inicioUtc))} />
          </View>
        ))}
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
});
