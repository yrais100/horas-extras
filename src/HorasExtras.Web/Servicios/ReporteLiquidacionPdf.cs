using HorasExtras.Application;
using HorasExtras.Domain;
using HorasExtras.Domain.Calculo;
using HorasExtras.Domain.Entidades;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HorasExtras.Web.Servicios;

/// <summary>
/// Reporte en PDF de una liquidación guardada: datos del periodo, cada registro con su horario, recargos y
/// lo realizado, el detalle por día y los avisos. No muestra valores en pesos (ni totales, ni valor hora):
/// es un soporte de las horas, no un comprobante de pago. Se arma desde el detalle congelado al guardar,
/// así que siempre sale igual aunque después cambien las horas.
/// </summary>
public static class ReporteLiquidacionPdf
{
    private const string Azul = "#1b4f8a";
    private static readonly string Borde = Colors.Grey.Lighten2;
    private static readonly string Suave = Colors.Grey.Darken1;

    public static string NombreArchivo(LiquidacionGuardadaDto l) =>
        $"liquidacion-{l.Desde:yyyy-MM-dd}-a-{l.Hasta:yyyy-MM-dd}.pdf";

    public static byte[] Generar(LiquidacionReporte reporte, string empleado, DateTime generadoUtc)
    {
        var (g, r) = (reporte.Guardada, reporte.Detalle);
        return Document.Create(doc => doc.Page(p =>
        {
            p.Size(PageSizes.Letter);
            p.Margin(36);
            p.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Colors.Grey.Darken4));

            p.Header().PaddingBottom(14).Column(c =>
            {
                c.Item().Row(row =>
                {
                    row.RelativeItem().AlignBottom().Text("Liquidación de horas extras").FontSize(17).Bold().FontColor(Azul);
                    row.AutoItem().AlignBottom().Text($"N.º {g.Id}").FontSize(11).SemiBold().FontColor(Suave);
                });
                c.Item().PaddingTop(6).LineHorizontal(1).LineColor(Azul);
                c.Item().PaddingTop(8).Element(x => Datos(x, g, r, empleado));
            });

            p.Content().Column(c =>
            {
                c.Spacing(16);
                c.Item().Element(x => Registros(x, r));
                c.Item().Element(x => Dias(x, r));
                var avisos = Avisos(r);
                if (avisos.Count > 0)
                    c.Item().Column(a =>
                    {
                        a.Item().Element(Titulo).Text("Avisos");
                        foreach (var aviso in avisos)
                            a.Item().PaddingBottom(3).Background(Colors.Amber.Lighten5).Padding(5).Text(aviso);
                    });
            });

            p.Footer().Row(f =>
            {
                f.RelativeItem().Text($"Generado el {FechaHora(generadoUtc)} (hora de Colombia)").FontSize(8).FontColor(Suave);
                f.RelativeItem().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Suave));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    /// <summary>Ficha con los datos de la liquidación, en dos columnas para no gastar la página en renglones sueltos.</summary>
    private static void Datos(IContainer x, LiquidacionGuardadaDto g, ResultadoLiquidacion r, string empleado) =>
        x.Background(Colors.Grey.Lighten5).Border(0.5f).BorderColor(Borde).Padding(8).Row(row =>
        {
            row.Spacing(16);
            row.RelativeItem(1.4f).Column(c =>
            {
                c.Spacing(3);
                c.Item().Element(y => Dato(y, "Empleado", empleado));
                c.Item().Element(y => Dato(y, "Periodo", $"{Corto(r.Desde)} al {Corto(r.Hasta)}"));
            });
            row.RelativeItem().Column(c =>
            {
                c.Spacing(3);
                c.Item().Element(y => Dato(y, "Guardada", FechaHora(g.CreadaEnUtc)));
                if (r.Registros is { Count: > 0 } regs)
                {
                    var aprobados = regs.Count(x => x.Revision == RevisionSesion.Aprobada);
                    var pendientes = regs.Count - aprobados;
                    c.Item().Element(y => Dato(y, "Registros", pendientes == 0
                        ? $"{regs.Count}, {(regs.Count == 1 ? "aprobado" : "todos aprobados")}"
                        : $"{regs.Count} ({aprobados} aprobados, {pendientes} por aprobar)"));
                }
            });
        });

    private static void Dato(IContainer x, string etiqueta, string valor) => x.Row(row =>
    {
        row.ConstantItem(62).Text(etiqueta).FontSize(8.5f).SemiBold().FontColor(Suave);
        row.RelativeItem().Text(valor);
    });

    private static void Registros(IContainer x, ResultadoLiquidacion r) => x.Column(c =>
    {
        c.Item().Element(Titulo).Text("Registros y lo realizado");
        if (r.Registros is null)
        {
            c.Item().Text("Esta liquidación se guardó antes de que se conservara el detalle por registro. Vuelve a guardarla para incluirlo.")
                .FontColor(Suave);
            return;
        }
        if (r.Registros.Count == 0)
        {
            c.Item().Text("Sin registros en este periodo.").FontColor(Suave);
            return;
        }
        c.Item().Table(t =>
        {
            t.ColumnsDefinition(d =>
            {
                d.ConstantColumn(22);
                d.RelativeColumn(1.6f);
                d.RelativeColumn(1.7f);
                d.RelativeColumn(0.7f);
                d.RelativeColumn(2.6f);
                d.RelativeColumn(1.3f);
            });
            t.Header(h =>
            {
                Encabezado(h.Cell(), "#");
                Encabezado(h.Cell(), "Fecha");
                Encabezado(h.Cell(), "Horario");
                Encabezado(h.Cell(), "Horas", derecha: true);
                Encabezado(h.Cell(), "Recargos");
                Encabezado(h.Cell(), "Estado");
            });
            var n = 0;
            foreach (var reg in r.Registros)
            {
                n++;
                var suma = reg.Revision == RevisionSesion.Aprobada;
                var (fecha, horario) = Horario(reg);
                // Fila de datos sin borde inferior: lo realizado va justo debajo, en la misma "tarjeta".
                Celda(t.Cell(), borde: false).Text(n.ToString()).FontColor(Suave);
                Celda(t.Cell(), borde: false).Text(fecha);
                Celda(t.Cell(), borde: false).Text(horario);
                Celda(t.Cell(), borde: false).AlignRight().Text(Formato.Horas(reg.Horas)).SemiBold();
                Celda(t.Cell(), borde: false).Column(cat =>
                {
                    foreach (var tc in reg.Categorias)
                        cat.Item().Text($"{Formato.Nombre(tc.Categoria)}: {Formato.Horas(tc.Horas)}");
                });
                Celda(t.Cell(), borde: false).Text(Estado(reg)).FontColor(suma ? Colors.Green.Darken2 : Colors.Orange.Darken3);

                t.Cell().ColumnSpan(6).BorderBottom(0.5f).BorderColor(Borde).PaddingBottom(7).PaddingLeft(25).PaddingRight(3)
                    .Text(tx =>
                    {
                        tx.Span("Lo realizado: ").SemiBold();
                        if (string.IsNullOrWhiteSpace(reg.Nota)) tx.Span("Sin observaciones.").Italic().FontColor(Suave);
                        else tx.Span(reg.Nota);
                    });
            }
        });
    });

    private static void Dias(IContainer x, ResultadoLiquidacion r) => x.Column(c =>
    {
        c.Item().Element(Titulo).Text("Horas aprobadas por día");
        if (r.Dias.Count == 0)
        {
            c.Item().Text("Sin horas aprobadas en este periodo.").FontColor(Suave);
            return;
        }
        c.Item().Table(t =>
        {
            t.ColumnsDefinition(d =>
            {
                d.RelativeColumn(2.2f);
                d.RelativeColumn(1.8f);
                d.RelativeColumn(1);
                d.RelativeColumn(1);
                d.RelativeColumn(1);
            });
            t.Header(h =>
            {
                Encabezado(h.Cell(), "Día");
                Encabezado(h.Cell(), "Observación");
                Encabezado(h.Cell(), "Diurnas", derecha: true);
                Encabezado(h.Cell(), "Nocturnas", derecha: true);
                Encabezado(h.Cell(), "Total", derecha: true);
            });
            foreach (var d in r.Dias)
            {
                var notas = new List<string>();
                if (d.DominicalOFestivo) notas.Add("Dominical o festivo");
                if (d.ExcedeLimiteDiario) notas.Add($"Más de {Liquidador.LimiteDiario:0} h");
                Celda(t.Cell()).Text(Largo(d.Fecha));
                Celda(t.Cell()).Text(string.Join(" · ", notas)).FontColor(Suave);
                Celda(t.Cell()).AlignRight().Text(Formato.Horas(d.HorasDiurnas));
                Celda(t.Cell()).AlignRight().Text(Formato.Horas(d.HorasNocturnas));
                Celda(t.Cell()).AlignRight().Text(Formato.Horas(d.Horas)).SemiBold();
            }
        });
    });

    /// <summary>
    /// Los avisos guardados pueden traer pesos (lo por aprobar) o hablar del salario; en el PDF se quitan
    /// esos y se reescribe el de horas por aprobar solo con horas.
    /// </summary>
    private static List<string> Avisos(ResultadoLiquidacion r)
    {
        var avisos = r.Avisos
            .Where(a => !a.Contains("salario", StringComparison.OrdinalIgnoreCase)
                        && !a.Contains("por aprobar", StringComparison.OrdinalIgnoreCase)
                        && !a.Contains('$'))
            .ToList();
        if (r.HorasPorAprobar > 0)
            avisos.Add($"Hay {Formato.Horas(r.HorasPorAprobar)} por aprobar que no se incluyen hasta que el administrador las revise.");
        return avisos;
    }

    private static (string Fecha, string Horario) Horario(DetalleRegistro reg)
    {
        if (reg.InicioUtc is not { } inicio) return ("", "");
        var desde = ZonaHoraria.ALocal(inicio);
        var fecha = desde.ToString("ddd d MMM yyyy");
        if (reg.EnCurso || reg.FinUtc is not { } fin) return (fecha, $"Desde {desde:h:mm tt} (en curso)");
        var hasta = ZonaHoraria.ALocal(fin);
        return desde.Date == hasta.Date
            ? (fecha, $"{desde:h:mm tt} a {hasta:h:mm tt}")
            : (fecha, $"{desde:h:mm tt} a {hasta:ddd d MMM}, {hasta:h:mm tt}");
    }

    private static string Estado(DetalleRegistro reg) => reg.Revision switch
    {
        RevisionSesion.Aprobada => reg.EnCurso ? "Aprobada (en curso)" : "Aprobada",
        _ => reg.EnCurso ? "En curso, por aprobar" : "Por aprobar",
    };

    private static string Largo(DateOnly f) => f.ToString("dddd d 'de' MMMM 'de' yyyy");

    private static string Corto(DateOnly f) => f.ToString("d 'de' MMMM 'de' yyyy");

    private static string FechaHora(DateTime utc) => ZonaHoraria.ALocal(utc).ToString("d 'de' MMMM 'de' yyyy, h:mm tt");

    private static IContainer Titulo(IContainer x) => x.PaddingBottom(4).DefaultTextStyle(t => t.FontSize(12).SemiBold().FontColor(Azul));

    private static void Encabezado(IContainer x, string texto, bool derecha = false)
    {
        var celda = x.Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(3);
        (derecha ? celda.AlignRight() : celda).Text(texto).SemiBold();
    }

    private static IContainer Celda(IContainer x, bool borde = true) =>
        (borde ? x.BorderBottom(0.5f).BorderColor(Borde) : x).PaddingVertical(3).PaddingHorizontal(3);
}
