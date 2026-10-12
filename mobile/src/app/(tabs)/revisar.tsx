import { useCallback, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Aviso, Boton, Campo, colores, Fila, Pantalla, Suave, Tarjeta } from '../../components/ui';
import { fechaHoraLocal, horaLocal, horas } from '../../lib/formato';
import { useSesion } from '../../lib/sesion';
import { mensajeDe, useCarga } from '../../lib/useCarga';

/** Bandeja del administrador: cuentas nuevas y horas por aprobar de todos los usuarios. */
export default function Revisar() {
  const { api } = useSesion();
  const cargar = useCallback(async () => {
    const [usuarios, sesiones] = await Promise.all([api.usuariosAdmin(), api.sesionesPorAprobar()]);
    return { pendientes: usuarios.filter((u) => u.estado === 'Pendiente'), sesiones };
  }, [api]);
  const { datos, error, setError, cargando, recargar } = useCarga(cargar);
  const [rechazando, setRechazando] = useState<number | null>(null);
  const [motivo, setMotivo] = useState('');
  const [ocupado, setOcupado] = useState<string | null>(null);

  const ejecutar = async (clave: string, accion: () => Promise<unknown>) => {
    setOcupado(clave);
    let fallo: string | null = null;
    try {
      await accion();
      setRechazando(null);
      setMotivo('');
    } catch (e) {
      fallo = mensajeDe(e);
    }
    await recargar();
    if (fallo) setError(fallo);
    setOcupado(null);
  };

  return (
    <Pantalla cargando={cargando} alRecargar={recargar}>
      {error ? <Aviso tipo="error">{error}</Aviso> : null}

      <Tarjeta titulo="Cuentas por aprobar">
        {datos?.pendientes.length === 0 ? <Suave>No hay cuentas nuevas.</Suave> : null}
        {datos?.pendientes.map((u) => (
          <View key={u.id} style={e.fila}>
            <Text style={[e.principal, { flex: 1 }]}>{u.nombre ?? u.email}</Text>
            <Boton
              titulo="Aprobar"
              onPress={() => ejecutar(`u${u.id}`, () => api.aprobarUsuario(u.id))}
              ocupado={ocupado === `u${u.id}`}
            />
          </View>
        ))}
        <Suave>El salario de cada persona, los roles y desactivar cuentas se manejan desde la web, en Usuarios.</Suave>
      </Tarjeta>

      <Tarjeta titulo="Horas por aprobar">
        {datos?.sesiones.length === 0 ? <Suave>No hay horas por aprobar.</Suave> : null}
        {datos?.sesiones.map(({ sesion: s, email, nombre }) => (
          <View key={s.id} style={e.bloque}>
            <Text style={e.principal}>{nombre ?? email}</Text>
            <Suave>
              {fechaHoraLocal(s.inicioUtc)} a {horaLocal(s.finUtc)} · {horas(s.segundosAcumulados / 3600)}
            </Suave>
            {s.nota ? <Suave>{s.nota}</Suave> : null}
            {rechazando === s.id ? (
              <>
                <Campo
                  etiqueta="Motivo del rechazo"
                  value={motivo}
                  onChangeText={setMotivo}
                  placeholder="Lo verá la persona para corregir el registro"
                  multiline
                  maxLength={500}
                />
                <Fila>
                  <Boton
                    titulo="Rechazar"
                    variante="peligroBorde"
                    onPress={() => ejecutar(`r${s.id}`, () => api.rechazarSesion(s.id, motivo))}
                    ocupado={ocupado === `r${s.id}`}
                  />
                  <Boton titulo="Cancelar" variante="secundario" onPress={() => setRechazando(null)} />
                </Fila>
              </>
            ) : (
              <Fila>
                <Boton
                  titulo="Aprobar"
                  onPress={() => ejecutar(`a${s.id}`, () => api.aprobarSesion(s.id))}
                  ocupado={ocupado === `a${s.id}`}
                />
                <Boton
                  titulo="Rechazar"
                  variante="peligroBorde"
                  onPress={() => {
                    setMotivo('');
                    setRechazando(s.id);
                  }}
                />
              </Fila>
            )}
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
  bloque: { gap: 6, paddingVertical: 10, borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colores.borde },
  principal: { fontSize: 15, fontWeight: '600', color: colores.texto },
});
