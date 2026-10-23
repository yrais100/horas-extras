import { Linking } from 'react-native';
import type { ClienteApi } from './api';

/** Abre el reporte PDF de una liquidación guardada en el navegador del teléfono, donde se puede guardar o compartir. */
export async function abrirPdfLiquidacion(api: ClienteApi, id: number): Promise<void> {
  await Linking.openURL(await api.urlPdfLiquidacion(id));
}
