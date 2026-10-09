// Tipos de la API REST de HorasExtras.Web (ver src/HorasExtras.Application/Dtos.cs y Domain/Calculo/Liquidador.cs).
// Las fechas sin hora llegan como "2026-10-01", las horas como "19:00:00" y los instantes en UTC ISO 8601.

export type EstadoSesion = 'EnCurso' | 'Pausada' | 'Finalizada';
export type Categoria = 'Diurna' | 'Nocturna' | 'DominicalDiurna' | 'DominicalNocturna';
export type ModoRecargos = 'Ley' | 'Manual';

/** Fecha local de Colombia, "AAAA-MM-DD". */
export type Fecha = string;

export interface Tokens {
  tokenType: string;
  accessToken: string;
  expiresIn: number;
  refreshToken: string;
}

export interface Tramo {
  inicioUtc: string;
  finUtc: string | null;
}

export interface Sesion {
  id: number;
  estado: EstadoSesion;
  nota: string | null;
  inicioUtc: string | null;
  finUtc: string | null;
  segundosAcumulados: number;
  tramos: Tramo[];
}

export interface TotalCategoria {
  categoria: Categoria;
  horas: number;
  valor: number;
}

export interface DetalleDia {
  fecha: Fecha;
  dominicalOFestivo: boolean;
  horasDiurnas: number;
  horasNocturnas: number;
  horas: number;
  valor: number;
  excedeLimiteDiario: boolean;
}

export interface Liquidacion {
  desde: Fecha;
  hasta: Fecha;
  calculadoEnUtc: string;
  totales: TotalCategoria[];
  horasTotales: number;
  valorTotal: number;
  diasTrabajados: number;
  dias: DetalleDia[];
  valorHoraActual: number;
  avisos: string[];
}

export interface LiquidacionGuardada {
  id: number;
  desde: Fecha;
  hasta: Fecha;
  creadaEnUtc: string;
  horasTotales: number;
  valorTotal: number;
}

export interface Salario {
  id: number;
  vigenteDesde: Fecha;
  salarioMensual: number;
  valorHoraManual: number | null;
}

export interface SalarioInput {
  vigenteDesde: Fecha;
  salarioMensual: number;
  valorHoraManual: number | null;
}

export interface Recargos {
  modo: ModoRecargos;
  inicioNocturno: string;
  finNocturno: string;
  horasMensuales: number;
  pctExtraDiurna: number;
  pctExtraNocturna: number;
  pctExtraDominicalDiurna: number;
  pctExtraDominicalNocturna: number;
}

export interface ValoresLey {
  inicioNocturno: string;
  finNocturno: string;
  jornadaSemanal: number;
  horasMensuales: number;
  pctDominical: number;
  pctExtraDiurna: number;
  pctExtraNocturna: number;
  pctExtraDominicalDiurna: number;
  pctExtraDominicalNocturna: number;
}

export interface Periodo {
  desde: Fecha;
  hasta: Fecha;
}
