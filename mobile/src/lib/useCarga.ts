import { useFocusEffect } from 'expo-router';
import { useCallback, useState } from 'react';

export function mensajeDe(e: unknown): string {
  return e instanceof Error ? e.message : 'Ocurrió un error inesperado.';
}

/** Carga datos cada vez que la pantalla gana el foco. `cargar` debe venir de useCallback. */
export function useCarga<T>(cargar: () => Promise<T>) {
  const [datos, setDatos] = useState<T | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(false);

  const recargar = useCallback(async () => {
    setCargando(true);
    try {
      setDatos(await cargar());
      setError(null);
    } catch (e) {
      setError(mensajeDe(e));
    } finally {
      setCargando(false);
    }
  }, [cargar]);

  useFocusEffect(
    useCallback(() => {
      void recargar();
    }, [recargar]),
  );

  return { datos, setDatos, error, setError, cargando, recargar };
}
