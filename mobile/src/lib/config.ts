import Constants from 'expo-constants';

/** Sitio de producción. Se puede cambiar en app.json (extra.apiUrl), con EXPO_PUBLIC_API_URL o desde la app. */
export const SERVIDOR_PRODUCCION = 'https://horasextras.azurewebsites.net';

export function servidorPorDefecto(): string {
  const extra = Constants.expoConfig?.extra as { apiUrl?: string } | undefined;
  return process.env.EXPO_PUBLIC_API_URL || extra?.apiUrl || SERVIDOR_PRODUCCION;
}
