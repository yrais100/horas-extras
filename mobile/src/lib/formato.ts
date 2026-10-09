import type { Categoria, EstadoSesion, Fecha } from './tipos';
import { aColombia, leerUtc, partesFecha } from './tiempo';

const DIAS = ['dom', 'lun', 'mar', 'mié', 'jue', 'vie', 'sáb'];
const MESES = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sept', 'oct', 'nov', 'dic'];

/** Pesos colombianos sin decimales: "$ 1.234.567". Sin Intl, que varía entre motores de JavaScript. */
export function pesos(v: number): string {
  const n = Math.round(Math.abs(v));
  const miles = String(n).replace(/\B(?=(\d{3})+(?!\d))/g, '.');
  return `${v < 0 && n > 0 ? '-' : ''}$ ${miles}`;
}

/** Horas decimales → "2h 05m", como Formato.Horas en la web. */
export function horas(h: number): string {
  const min = Math.round(h * 60);
  return `${Math.floor(min / 60)}h ${String(min % 60).padStart(2, '0')}m`;
}

/** Segundos → "01:02:03". */
export function cronometro(segundos: number): string {
  const s = Math.max(0, Math.floor(segundos));
  const dos = (n: number) => String(n).padStart(2, '0');
  return `${dos(Math.floor(s / 3600))}:${dos(Math.floor((s % 3600) / 60))}:${dos(s % 60)}`;
}

function horaLocalDe(h: number, m: number): string {
  const sufijo = h < 12 ? 'a. m.' : 'p. m.';
  return `${h % 12 === 0 ? 12 : h % 12}:${String(m).padStart(2, '0')} ${sufijo}`;
}

/** Hora de Colombia de un instante UTC: "6:30 p. m.". */
export function horaLocal(utc: string | null | undefined): string {
  if (!utc) return '';
  const p = aColombia(leerUtc(utc));
  return horaLocalDe(p.hora, p.minuto);
}

/** "jue 8 oct, 6:30 p. m." */
export function fechaHoraLocal(utc: string | null | undefined): string {
  if (!utc) return '';
  const p = aColombia(leerUtc(utc));
  return `${DIAS[p.diaSemana]} ${p.dia} ${MESES[p.mes - 1]}, ${horaLocalDe(p.hora, p.minuto)}`;
}

/** "2026-10-08" → "jue 8 oct". */
export function fecha(f: Fecha): string {
  const { anio, mes, dia } = partesFecha(f);
  const diaSemana = new Date(Date.UTC(anio, mes - 1, dia)).getUTCDay();
  return `${DIAS[diaSemana]} ${dia} ${MESES[mes - 1]}`;
}

/** "2026-10-08" → "8 oct 2026". */
export function fechaLarga(f: Fecha): string {
  const { anio, mes, dia } = partesFecha(f);
  return `${dia} ${MESES[mes - 1]} ${anio}`;
}

/** "19:00:00" → "7:00 p. m." */
export function horaDelDia(h: string): string {
  const [hh, mm] = h.split(':').map(Number);
  return horaLocalDe(hh, mm);
}

export function nombreCategoria(c: Categoria): string {
  switch (c) {
    case 'Diurna':
      return 'Extra diurna';
    case 'Nocturna':
      return 'Extra nocturna';
    case 'DominicalDiurna':
      return 'Extra dominical/festiva diurna';
    case 'DominicalNocturna':
      return 'Extra dominical/festiva nocturna';
  }
}

export function nombreEstado(e: EstadoSesion): string {
  return e === 'EnCurso' ? 'En curso' : e === 'Pausada' ? 'Pausada' : 'Finalizada';
}

/**
 * Convierte lo que escribe el usuario en número; null si no es válido.
 * En Colombia el punto separa miles y la coma los decimales ("1.500.000", "8.500,50"), pero también se
 * acepta "12.5" como decimal cuando el punto no agrupa miles.
 */
export function leerNumero(texto: string): number | null {
  let t = texto.trim().replace(/[\s$]/g, '');
  if (t === '') return null;
  if (t.includes(',')) t = t.replace(/\./g, '').replace(',', '.');
  else if (/^-?\d{1,3}(\.\d{3})+$/.test(t)) t = t.replace(/\./g, '');
  if (!/^-?\d+(\.\d+)?$/.test(t)) return null;
  return Number(t);
}
