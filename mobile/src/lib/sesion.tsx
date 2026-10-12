import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { ApiError, ClienteApi, normalizarBaseUrl } from './api';
import { almacenSeguro, guardarEmail, guardarServidor, leerEmail, leerServidor } from './almacen';
import { servidorPorDefecto } from './config';
import type { Cuenta } from './tipos';

type Estado = 'cargando' | 'fuera' | 'dentro';

interface ValorSesion {
  estado: Estado;
  api: ClienteApi;
  servidor: string;
  email: string | null;
  /** Datos y rol del usuario que ingresó; null mientras carga o si no hay sesión. */
  cuenta: Cuenta | null;
  ingresar(email: string, password: string): Promise<void>;
  /** Crea la cuenta e ingresa si ya está activa; 'pendiente' si un administrador debe aprobarla. */
  registrar(email: string, password: string): Promise<'dentro' | 'pendiente'>;
  salir(): Promise<void>;
  /** Cambia el servidor de la API. Cierra la sesión, porque los tokens son de cada servidor. */
  cambiarServidor(url: string): Promise<void>;
}

const Contexto = createContext<ValorSesion | null>(null);

export function ProveedorSesion({ children }: { children: ReactNode }) {
  const [estado, setEstado] = useState<Estado>('cargando');
  const [servidor, setServidor] = useState(servidorPorDefecto());
  const [email, setEmail] = useState<string | null>(null);
  const [cuenta, setCuenta] = useState<Cuenta | null>(null);

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

  useEffect(() => {
    if (estado !== 'dentro') return;
    let vigente = true;
    api
      .cuenta()
      .then((c) => vigente && setCuenta(c))
      .catch(() => vigente && setCuenta(null));
    return () => {
      vigente = false;
    };
  }, [estado, api]);

  const ingresar = useCallback(
    async (correo: string, password: string) => {
      await api.ingresar(correo, password);
      await guardarEmail(correo.trim());
      setEmail(correo.trim());
      setCuenta(null); // la de un ingreso anterior no aplica; el efecto carga la nueva
      setEstado('dentro');
    },
    [api],
  );

  const registrar = useCallback(
    async (correo: string, password: string) => {
      await api.registrar(correo, password);
      try {
        await ingresar(correo, password);
        return 'dentro' as const;
      } catch (e) {
        // La cuenta quedó creada; solo falta que un administrador la apruebe.
        if (e instanceof ApiError && e.status === 401) return 'pendiente' as const;
        throw e;
      }
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
    () => ({ estado, api, servidor, email, cuenta: estado === 'dentro' ? cuenta : null, ingresar, registrar, salir, cambiarServidor }),
    [estado, api, servidor, email, cuenta, ingresar, registrar, salir, cambiarServidor],
  );
  return <Contexto.Provider value={valor}>{children}</Contexto.Provider>;
}

export function useSesion(): ValorSesion {
  const v = useContext(Contexto);
  if (!v) throw new Error('useSesion debe usarse dentro de ProveedorSesion');
  return v;
}
