import * as SecureStoreNativo from 'expo-secure-store';
import { Platform } from 'react-native';
import type { AlmacenTokens, TokensGuardados } from './api';

// SecureStore no existe en el navegador; para `expo start --web` (solo desarrollo) se usa localStorage.
const SecureStore =
  Platform.OS === 'web'
    ? {
        getItemAsync: async (k: string) => globalThis.localStorage?.getItem(k) ?? null,
        setItemAsync: async (k: string, v: string) => globalThis.localStorage?.setItem(k, v),
        deleteItemAsync: async (k: string) => globalThis.localStorage?.removeItem(k),
      }
    : SecureStoreNativo;

// Cada token va en su propia clave: juntos superan el tamaño recomendado por valor de SecureStore.
const CLAVES = { acceso: 'tokenAcceso', renovacion: 'tokenRenovacion', vence: 'tokenVence', servidor: 'servidor', email: 'email' };

export const almacenSeguro: AlmacenTokens = {
  async leer() {
    const [accessToken, refreshToken, vence] = await Promise.all([
      SecureStore.getItemAsync(CLAVES.acceso),
      SecureStore.getItemAsync(CLAVES.renovacion),
      SecureStore.getItemAsync(CLAVES.vence),
    ]);
    if (!accessToken || !refreshToken) return null;
    return { accessToken, refreshToken, venceEn: Number(vence) || 0 };
  },
  async guardar(t: TokensGuardados | null) {
    if (!t) {
      await Promise.all([CLAVES.acceso, CLAVES.renovacion, CLAVES.vence].map((k) => SecureStore.deleteItemAsync(k)));
      return;
    }
    await SecureStore.setItemAsync(CLAVES.acceso, t.accessToken);
    await SecureStore.setItemAsync(CLAVES.renovacion, t.refreshToken);
    await SecureStore.setItemAsync(CLAVES.vence, String(t.venceEn));
  },
};

export const leerServidor = () => SecureStore.getItemAsync(CLAVES.servidor);
export const guardarServidor = (url: string) => SecureStore.setItemAsync(CLAVES.servidor, url);
export const leerEmail = () => SecureStore.getItemAsync(CLAVES.email);
export const guardarEmail = (email: string | null) =>
  email ? SecureStore.setItemAsync(CLAVES.email, email) : SecureStore.deleteItemAsync(CLAVES.email);
