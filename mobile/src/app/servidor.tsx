import { router } from 'expo-router';
import { useState } from 'react';
import { Aviso, Boton, Campo, Fila, Pantalla, Suave, Tarjeta } from '../components/ui';
import { normalizarBaseUrl } from '../lib/api';
import { SERVIDOR_PRODUCCION } from '../lib/config';
import { useSesion } from '../lib/sesion';
import { mensajeDe } from '../lib/useCarga';

export default function Servidor() {
  const { servidor, cambiarServidor } = useSesion();
  const [url, setUrl] = useState(servidor);
  const [mensaje, setMensaje] = useState<{ tipo: 'error' | 'exito'; texto: string } | null>(null);
  const [probando, setProbando] = useState(false);

  const probar = async () => {
    const base = normalizarBaseUrl(url);
    if (!base) return setMensaje({ tipo: 'error', texto: 'Escribe una dirección que empiece por http:// o https://' });
    setProbando(true);
    setMensaje(null);
    try {
      const r = await fetch(`${base}/salud`);
      const cuerpo = (await r.json().catch(() => null)) as { estado?: string; baseDeDatos?: string } | null;
      setMensaje(
        cuerpo?.estado === 'ok'
          ? { tipo: 'exito', texto: 'El servidor responde y su base de datos está lista.' }
          : { tipo: 'error', texto: cuerpo?.baseDeDatos ?? `El servidor respondió ${r.status}.` },
      );
    } catch {
      setMensaje({ tipo: 'error', texto: `No se pudo conectar con ${base}.` });
    } finally {
      setProbando(false);
    }
  };

  const guardar = async () => {
    try {
      await cambiarServidor(url);
      router.back();
    } catch (e) {
      setMensaje({ tipo: 'error', texto: mensajeDe(e) });
    }
  };

  return (
    <Pantalla>
      <Tarjeta titulo="Dirección de la API">
        <Suave>
          Normalmente es el sitio de producción. Para desarrollo, usa la dirección de tu computador en la red local, por
          ejemplo http://192.168.1.20:5228.
        </Suave>
        <Campo
          etiqueta="URL"
          value={url}
          onChangeText={setUrl}
          autoCapitalize="none"
          autoCorrect={false}
          keyboardType="url"
        />
        {mensaje ? <Aviso tipo={mensaje.tipo}>{mensaje.texto}</Aviso> : null}
        <Fila>
          <Boton titulo="Probar conexión" variante="secundario" onPress={probar} ocupado={probando} />
          <Boton titulo="Usar producción" variante="secundario" onPress={() => setUrl(SERVIDOR_PRODUCCION)} />
          <Boton titulo="Guardar" onPress={guardar} />
        </Fila>
      </Tarjeta>
    </Pantalla>
  );
}
