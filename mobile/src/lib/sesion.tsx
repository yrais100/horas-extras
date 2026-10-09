import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { ClienteApi, normalizarBaseUrl } from './api';
import { almacenSeguro, guardarEmail, guardarServidor, leerEmail, leerServidor } from './almacen';
import { servidorPorDefecto } from './config';

type Estado = 'cargando' | 'fuera' | 'dentro';

interface ValorSesion {
  estado: Estado;
  api: ClienteApi;
  servidor: string;
  email: string | null;
  ingresar(email: string, password: string): Promise<void>;
  registrar(email: string, password: string): Promise<void>;
  salir(): Promise<void>;
  /** Cambia el servidor de la API. Cierra la sesión, porque los tokens son de cada servidor. */
  cambiarServidor(url: string): Promise<void>;
}

const Contexto = createContext<ValorSesion | null>(null);

export function ProveedorSesion({ children }: { children: ReactNode }) {
  const [estado, setEstado] = useState<Estado>('cargando');
  const [servidor, setServidor] = useState(servidorPorDefecto());
  const [email, setEmail] = useState<string | null>(null);

  const api = useMemo(
    () => new ClienteApi({ baseUrl: servidor, almacen: almacenSeguro, alExpirar: () => setEstado('fuera') }),
    [servidor],
  );

  useEffect(() => {
    (async () => {
      const guardado = await leerServidor();
      const url = (guardado && normalizarBaseUrl(guardado)) || servidorPorDefecto();
      setServidor(url);
      setEmail(await leerEmail());
      setEstado((await almacenSeguro.leer()) ? 'dentro' : 'fuera');
    })().catch(() => setEstado('fuera'));
  }, []);

  const ingresar = useCallback(
    async (correo: string, password: string) => {
      await api.ingresar(correo, password);
      await guardarEmail(correo.trim());
      setEmail(correo.trim());
      setEstado('dentro');
    },
    [api],
  );

  const registrar = useCallback(
    async (correo: string, password: string) => {
      await api.registrar(correo, password);
      await ingresar(correo, password);
    },
    [api, ingresar],
  );

  const salir = useCallback(async () => {
    await api.salir();
    await guardarEmail(null);
    setEmail(null);
    setEstado('fuera');
  }, [api]);

  const cambiarServidor = useCallback(
    async (url: string) => {
      const normal = normalizarBaseUrl(url);
      if (!normal) throw new Error('Escribe una dirección que empiece por http:// o https://');
      await api.salir();
      await guardarServidor(normal);
      setServidor(normal);
      setEstado('fuera');
    },
    [api],
  );

  const valor = useMemo(
    () => ({ estado, api, servidor, email, ingresar, registrar, salir, cambiarServidor }),
    [estado, api, servidor, email, ingresar, registrar, salir, cambiarServidor],
  );
  return <Contexto.Provider value={valor}>{children}</Contexto.Provider>;
}

export function useSesion(): ValorSesion {
  const v = useContext(Contexto);
  if (!v) throw new Error('useSesion debe usarse dentro de ProveedorSesion');
  return v;
}
