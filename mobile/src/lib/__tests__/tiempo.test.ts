import {
  aColombia,
  desdeColombia,
  fechaDe,
  horaDeMinutos,
  hoy,
  instanteDeFecha,
  leerUtc,
  minutosDeHora,
  quincenaAnterior,
  quincenaDe,
  sumarDias,
} from '../tiempo';

describe('tiempo (hora de Colombia, UTC-5)', () => {
  it('lee instantes de .NET con 7 decimales y sin zona', () => {
    expect(leerUtc('2026-10-09T15:34:34.4559194Z').toISOString()).toBe('2026-10-09T15:34:34.455Z');
    expect(leerUtc('2026-10-09T15:34:34').toISOString()).toBe('2026-10-09T15:34:34.000Z');
    expect(() => leerUtc('no es fecha')).toThrow();
  });

  it('convierte entre UTC y la hora de pared de Colombia', () => {
    // 8 de octubre de 2026, 11 p. m. UTC = 6 p. m. en Colombia (jueves).
    const p = aColombia(new Date('2026-10-08T23:00:00Z'));
    expect(p).toMatchObject({ anio: 2026, mes: 10, dia: 8, hora: 18, minuto: 0, diaSemana: 4 });
    expect(desdeColombia(2026, 10, 8, 18, 0).toISOString()).toBe('2026-10-08T23:00:00.000Z');
  });

  it('la fecha local cambia a la medianoche de Colombia, no a la de UTC', () => {
    expect(fechaDe(new Date('2026-10-09T04:59:00Z'))).toBe('2026-10-08');
    expect(fechaDe(new Date('2026-10-09T05:00:00Z'))).toBe('2026-10-09');
    expect(hoy(new Date('2026-10-09T03:00:00Z'))).toBe('2026-10-08');
  });

  it('el mediodía de una fecha conserva el día', () => {
    expect(fechaDe(instanteDeFecha('2026-02-28'))).toBe('2026-02-28');
  });

  it('calcula quincenas como Quincena.cs', () => {
    expect(quincenaDe('2026-10-09')).toEqual({ desde: '2026-10-01', hasta: '2026-10-15' });
    expect(quincenaDe('2026-10-16')).toEqual({ desde: '2026-10-16', hasta: '2026-10-31' });
    expect(quincenaDe('2028-02-20')).toEqual({ desde: '2028-02-16', hasta: '2028-02-29' });
    expect(quincenaDe('2026-02-20')).toEqual({ desde: '2026-02-16', hasta: '2026-02-28' });
    expect(quincenaAnterior({ desde: '2026-10-01', hasta: '2026-10-15' })).toEqual({ desde: '2026-09-16', hasta: '2026-09-30' });
    expect(quincenaAnterior({ desde: '2026-01-01', hasta: '2026-01-15' })).toEqual({ desde: '2025-12-16', hasta: '2025-12-31' });
  });

  it('suma días cruzando meses y años', () => {
    expect(sumarDias('2026-12-31', 1)).toBe('2027-01-01');
    expect(sumarDias('2026-03-01', -1)).toBe('2026-02-28');
  });

  it('convierte horas del día al formato TimeOnly', () => {
    expect(minutosDeHora('19:00:00')).toBe(1140);
    expect(minutosDeHora('6:30')).toBe(390);
    expect(horaDeMinutos(1140)).toBe('19:00:00');
    expect(horaDeMinutos(5)).toBe('00:05:00');
  });
});
