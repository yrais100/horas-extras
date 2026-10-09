import { cronometro, fecha, fechaHoraLocal, horaDelDia, horaLocal, horas, leerNumero, nombreCategoria, pesos } from '../formato';

describe('formato', () => {
  it('pesos colombianos sin decimales', () => {
    expect(pesos(0)).toBe('$ 0');
    expect(pesos(1234567)).toBe('$ 1.234.567');
    expect(pesos(999.6)).toBe('$ 1.000');
    expect(pesos(-2500)).toBe('-$ 2.500');
  });

  it('horas como en la web', () => {
    expect(horas(0)).toBe('0h 00m');
    expect(horas(2.5)).toBe('2h 30m');
    expect(horas(1.999)).toBe('2h 00m');
  });

  it('cronómetro', () => {
    expect(cronometro(0)).toBe('00:00:00');
    expect(cronometro(3725.9)).toBe('01:02:05');
    expect(cronometro(-3)).toBe('00:00:00');
  });

  it('fechas y horas en hora de Colombia', () => {
    expect(horaLocal('2026-10-08T23:30:00Z')).toBe('6:30 p. m.');
    expect(horaLocal('2026-10-09T05:05:00Z')).toBe('12:05 a. m.');
    expect(horaLocal(null)).toBe('');
    expect(fechaHoraLocal('2026-10-08T23:30:00.1234567Z')).toBe('jue 8 oct, 6:30 p. m.');
    expect(fecha('2026-10-11')).toBe('dom 11 oct');
    expect(horaDelDia('19:00:00')).toBe('7:00 p. m.');
    expect(horaDelDia('12:00:00')).toBe('12:00 p. m.');
  });

  it('nombres de categoría', () => {
    expect(nombreCategoria('DominicalNocturna')).toBe('Extra dominical/festiva nocturna');
  });

  it('lee números escritos al estilo colombiano', () => {
    expect(leerNumero('1.500.000')).toBe(1500000);
    expect(leerNumero('$ 2.500.000')).toBe(2500000);
    expect(leerNumero('1500000')).toBe(1500000);
    expect(leerNumero('8.500,50')).toBe(8500.5);
    expect(leerNumero('12.5')).toBe(12.5);
    expect(leerNumero('25')).toBe(25);
    expect(leerNumero('')).toBeNull();
    expect(leerNumero('abc')).toBeNull();
  });
});
