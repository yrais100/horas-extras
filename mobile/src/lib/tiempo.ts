import type { Fecha, Periodo } from './tipos';

// Colombia no tiene horario de verano: siempre UTC-5, igual que ZonaHoraria.cs en el servidor.
export const ZONA = 'America/Bogota';
const DESFASE_MS = -5 * 60 * 60 * 1000;

export interface PartesLocales {
  anio: number;
  mes: number; // 1 a 12
  dia: number;
  hora: number;
  minuto: number;
  segundo: number;
  diaSemana: number; // 0 = domingo
}

/** Lee un instante UTC del servidor. .NET envía hasta 7 decimales; se recortan a milisegundos. */
export function leerUtc(iso: string): Date {
  let s = iso.replace(/(\.\d{3})\d+/, '$1');
  if (!/[zZ]|[+-]\d\d:?\d\d$/.test(s)) s += 'Z';
  const d = new Date(s);
  if (Number.isNaN(d.getTime())) throw new Error(`Fecha inválida: ${iso}`);
  return d;
}

export function aColombia(instante: Date): PartesLocales {
  const l = new Date(instante.getTime() + DESFASE_MS);
  return {
    anio: l.getUTCFullYear(),
    mes: l.getUTCMonth() + 1,
    dia: l.getUTCDate(),
    hora: l.getUTCHours(),
    minuto: l.getUTCMinutes(),
    segundo: l.getUTCSeconds(),
    diaSemana: l.getUTCDay(),
  };
}

/** Instante que corresponde a una hora de pared en Colombia. */
export function desdeColombia(anio: number, mes: number, dia: number, hora = 0, minuto = 0): Date {
  return new Date(Date.UTC(anio, mes - 1, dia, hora, minuto) - DESFASE_MS);
}

const dos = (n: number) => String(n).padStart(2, '0');

export function fechaDe(instante: Date): Fecha {
  const p = aColombia(instante);
  return `${p.anio}-${dos(p.mes)}-${dos(p.dia)}`;
}

export function hoy(ahora: Date = new Date()): Fecha {
  return fechaDe(ahora);
}

export function partesFecha(f: Fecha): { anio: number; mes: number; dia: number } {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(f);
  if (!m) throw new Error(`Fecha inválida: ${f}`);
  return { anio: Number(m[1]), mes: Number(m[2]), dia: Number(m[3]) };
}

/** Mediodía en Colombia de esa fecha: sirve como valor de un selector de fecha sin riesgo de cambiar de día. */
export function instanteDeFecha(f: Fecha): Date {
  const { anio, mes, dia } = partesFecha(f);
  return desdeColombia(anio, mes, dia, 12, 0);
}

export function sumarDias(f: Fecha, n: number): Fecha {
  const { anio, mes, dia } = partesFecha(f);
  const d = new Date(Date.UTC(anio, mes - 1, dia + n));
  return `${d.getUTCFullYear()}-${dos(d.getUTCMonth() + 1)}-${dos(d.getUTCDate())}`;
}

function ultimoDiaDelMes(anio: number, mes: number): number {
  return new Date(Date.UTC(anio, mes, 0)).getUTCDate();
}

/** Quincena que contiene la fecha: del 1 al 15 o del 16 al último día del mes (como Quincena.cs). */
export function quincenaDe(f: Fecha): Periodo {
  const { anio, mes, dia } = partesFecha(f);
  const p = `${anio}-${dos(mes)}`;
  return dia <= 15
    ? { desde: `${p}-01`, hasta: `${p}-15` }
    : { desde: `${p}-16`, hasta: `${p}-${dos(ultimoDiaDelMes(anio, mes))}` };
}

export function quincenaAnterior(p: Periodo): Periodo {
  return quincenaDe(sumarDias(p.desde, -1));
}

/** "19:00:00" → minutos desde la medianoche. */
export function minutosDeHora(h: string): number {
  const m = /^(\d{1,2}):(\d{2})/.exec(h);
  if (!m) throw new Error(`Hora inválida: ${h}`);
  return Number(m[1]) * 60 + Number(m[2]);
}

/** Minutos desde la medianoche → "19:00:00", el formato TimeOnly que espera la API. */
export function horaDeMinutos(min: number): string {
  return `${dos(Math.floor(min / 60) % 24)}:${dos(min % 60)}:00`;
}
