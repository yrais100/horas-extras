using HorasExtras.Domain;
using HorasExtras.Domain.Calculo;
using HorasExtras.Domain.Entidades;

namespace HorasExtras.Domain.Tests;

public class CalculoTests
{
    private const string U = "u1";

    // Salario 2.100.000 / 210 h = 10.000 por hora (jornada de 42 h desde jul-2026).
    private static ResolutorParametros Resolutor(ConfiguracionRecargos? cfg = null, params SalarioUsuario[] salarios) =>
        new(cfg ?? ConfiguracionRecargos.PorDefecto(U),
            salarios.Length > 0 ? salarios : [new SalarioUsuario { UsuarioId = U, VigenteDesde = new(2026, 1, 1), SalarioMensual = 2_100_000 }]);

    /// <summary>Hora local de Colombia a UTC.</summary>
    private static DateTime Co(string local) => ZonaHoraria.AUtc(DateTime.Parse(local));

    private static Tramo T(string ini, string fin) => new(Co(ini)) { FinUtc = Co(fin) };

    private static readonly Periodo Oct1 = new(new(2026, 10, 1), new(2026, 10, 15));

    [Fact]
    public void Festivos2026IncluyenEmilianiYSemanaSanta()
    {
        var f = Festivos.DelAnio(2026).Select(x => x.Fecha.ToString("yyyy-MM-dd")).ToList();
        Assert.Equal(18, f.Count);
        foreach (var d in new[] { "2026-01-12", "2026-03-23", "2026-04-02", "2026-04-03", "2026-05-18",
                     "2026-06-08", "2026-06-15", "2026-06-29", "2026-10-12", "2026-11-02", "2026-11-16" })
            Assert.Contains(d, f);
        Assert.False(Festivos.EsFestivo(new(2026, 10, 8)));
    }

    [Fact]
    public void ValoresDeLeyCambianConLaFecha()
    {
        Assert.Equal(new TimeOnly(21, 0), ValoresLey.Para(new(2025, 10, 1)).InicioNocturno);
        Assert.Equal(new TimeOnly(19, 0), ValoresLey.Para(new(2026, 10, 8)).InicioNocturno);
        Assert.Equal(210, ValoresLey.Para(new(2026, 10, 8)).HorasMensuales);
        Assert.Equal(220, ValoresLey.Para(new(2026, 7, 1)).HorasMensuales);
        Assert.Equal(115, ValoresLey.Para(new(2026, 10, 8)).PctExtraDominicalDiurna);
        Assert.Equal(100, ValoresLey.Para(new(2027, 7, 1)).PctDominical);
    }

    [Fact]
    public void JuevesDe18a21SonUnaDiurnaYDosNocturnas()
    {
        var t = ClasificadorHoras.Clasificar(Co("2026-10-08 18:00"), Co("2026-10-08 21:00"), Resolutor().Para).ToList();
        Assert.Equal([(Categoria.Diurna, 1.0), (Categoria.Nocturna, 2.0)], t.Select(x => (x.Categoria, x.Duracion.TotalHours)));
    }

    [Fact]
    public void SabadoAMedianocheCruzaADominicalNocturna()
    {
        var t = ClasificadorHoras.Clasificar(Co("2026-10-10 23:00"), Co("2026-10-11 01:00"), Resolutor().Para).ToList();
        Assert.Equal([(new DateOnly(2026, 10, 10), Categoria.Nocturna), (new DateOnly(2026, 10, 11), Categoria.DominicalNocturna)],
            t.Select(x => (x.Fecha, x.Categoria)));
    }

    [Fact]
    public void MadrugadaDe5a7EsNocturnaYLuegoDiurna()
    {
        var t = ClasificadorHoras.Clasificar(Co("2026-10-09 05:00"), Co("2026-10-09 07:00"), Resolutor().Para).ToList();
        Assert.Equal([Categoria.Nocturna, Categoria.Diurna], t.Select(x => x.Categoria));
    }

    [Fact]
    public void LiquidacionConValores()
    {
        var r = Liquidador.Liquidar(
            [T("2026-10-08 18:00", "2026-10-08 21:00"), T("2026-10-12 08:00", "2026-10-12 09:00")], // el 12 es festivo
            Resolutor(), Oct1, Co("2026-10-13 00:00"));
        decimal Valor(Categoria c) => r.Totales.Single(t => t.Categoria == c).Valor;
        Assert.Equal(12_500, Valor(Categoria.Diurna));
        Assert.Equal(35_000, Valor(Categoria.Nocturna));
        Assert.Equal(21_500, Valor(Categoria.DominicalDiurna));
        Assert.Equal(69_000, r.ValorTotal);
        Assert.Equal(2, r.DiasTrabajados);
        Assert.True(r.Dias[0].ExcedeLimiteDiario);
        Assert.Single(r.Avisos);
    }

    [Fact]
    public void TramoAbiertoSeCuentaHastaAhora()
    {
        var r = Liquidador.Liquidar([new Tramo(Co("2026-10-08 17:00"))], Resolutor(), Oct1, Co("2026-10-08 18:30"));
        Assert.Equal(1.5m, r.HorasTotales);
    }

    [Fact]
    public void AumentoDeSalarioSoloAfectaDesdeSuVigencia()
    {
        var resolutor = Resolutor(null,
            new SalarioUsuario { UsuarioId = U, VigenteDesde = new(2026, 1, 1), SalarioMensual = 2_100_000 },
            new SalarioUsuario { UsuarioId = U, VigenteDesde = new(2026, 10, 10), SalarioMensual = 4_200_000 });
        var r = Liquidador.Liquidar(
            [T("2026-10-08 08:00", "2026-10-08 09:00"), T("2026-10-13 08:00", "2026-10-13 09:00")],
            resolutor, Oct1, Co("2026-10-14 00:00"));
        Assert.Equal(12_500 + 25_000, r.ValorTotal);
    }

    [Fact]
    public void ModoManualYValorHoraManual()
    {
        var cfg = ConfiguracionRecargos.PorDefecto(U);
        cfg.Modo = ModoRecargos.Manual;
        cfg.InicioNocturno = new(22, 0);
        cfg.PctExtraNocturna = 100;
        var r = Liquidador.Liquidar([T("2026-10-08 21:00", "2026-10-08 23:00")],
            Resolutor(cfg, new SalarioUsuario { UsuarioId = U, VigenteDesde = new(2026, 1, 1), SalarioMensual = 1, ValorHoraManual = 8_000 }),
            Oct1, Co("2026-10-09 00:00"));
        Assert.Equal(10_000 + 16_000, r.ValorTotal);
    }

    [Fact]
    public void SinSalarioAvisa()
    {
        var r = Liquidador.Liquidar([T("2026-10-08 08:00", "2026-10-08 09:00")],
            new ResolutorParametros(ConfiguracionRecargos.PorDefecto(U), []), Oct1, Co("2026-10-09 00:00"));
        Assert.Equal(0, r.ValorTotal);
        Assert.Contains(r.Avisos, a => a.Contains("salario"));
    }

    [Theory]
    [InlineData("2026-10-08", "2026-10-01", "2026-10-15")]
    [InlineData("2026-02-20", "2026-02-16", "2026-02-28")]
    [InlineData("2028-02-29", "2028-02-16", "2028-02-29")]
    public void Quincenas(string fecha, string desde, string hasta)
    {
        var q = Quincena.De(DateOnly.Parse(fecha));
        Assert.Equal((DateOnly.Parse(desde), DateOnly.Parse(hasta)), (q.Desde, q.Hasta));
    }

    [Fact]
    public void FlujoDeSesion()
    {
        var s = Sesion.Iniciar(U, Co("2026-10-08 18:00"));
        s.Pausar(Co("2026-10-08 19:00"));
        Assert.Throws<ReglaNegocioException>(() => s.Pausar(Co("2026-10-08 19:10")));
        s.Continuar(Co("2026-10-08 19:30"));
        s.Finalizar(Co("2026-10-08 20:30"));
        Assert.Equal(TimeSpan.FromHours(2), s.Duracion(Co("2026-10-09 00:00")));
        Assert.Equal(Co("2026-10-08 18:00"), s.InicioUtc);
        Assert.Throws<ReglaNegocioException>(() => s.Continuar(Co("2026-10-08 21:00")));
    }
}
