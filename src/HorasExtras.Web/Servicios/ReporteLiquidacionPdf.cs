using HorasExtras.Application;
using HorasExtras.Domain;
using HorasExtras.Domain.Calculo;
using HorasExtras.Domain.Entidades;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HorasExtras.Web.Servicios;

/// <summary>
/// Reporte en PDF de una liquidación guardada: resumen por categoría, cada registro con su valor y sus
/// observaciones, el detalle por día y los avisos. Se arma desde el detalle congelado al guardar, así que
/// siempre sale igual aunque después cambien las horas o el salario.
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

            p.Header().PaddingBottom(12).Column(c =>
            {
                c.Item().Text("Liquidación de horas extras").FontSize(17).Bold().FontColor(Azul);
                c.Item().PaddingTop(4).Text(t =>
                {
                    t.Span("Empleado: ").SemiBold();
                    t.Span(empleado);
                });
                c.Item().Text(t =>
                {
                    t.Span("Periodo: ").SemiBold();
                    t.Span($"{Largo(r.Desde)} al {Largo(r.Hasta)}");
                });
                c.Item().Text(t =>
                {
                    t.Span("Guardada: ").SemiBold();
                    t.Span($"{FechaHora(g.CreadaEnUtc)} · Liquidación n.º {g.Id}");
                });
                c.Item().PaddingTop(6).LineHorizontal(1).LineColor(Azul);
            });

            p.Content().Column(c =>
            {
                c.Spacing(14);
                c.Item().Element(x => Resumen(x, r));
                c.Item().Element(x => Registros(x, r));
                c.Item().Element(x => Dias(x, r));
                if (r.Avisos.Count > 0)
                    c.Item().Column(a =>
                    {
                        a.Item().Element(Titulo).Text("Avisos");
                        foreach (var aviso in r.Avisos)
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

    private static void Resumen(IContainer x, ResultadoLiquidacion r) => x.Column(c =>
    {
        c.Item().Element(Titulo).Text("Resumen");
        c.Item().Table(t =>
        {
            t.ColumnsDefinition(d =>
            {
                d.RelativeColumn(3);
                d.RelativeColumn(1);
                d.RelativeColumn(1.4f);
            });
            t.Header(h =>
            {
                Encabezado(h.Cell(), "Categoría");
                Encabezado(h.Cell(), "Horas", derecha: true);
                Encabezado(h.Cell(), "Valor", derecha: true);
            });
            foreach (var tc in r.Totales)
            {
                Celda(t.Cell()).Text(Formato.Nombre(tc.Categoria));
                Celda(t.Cell()).AlignRight().Text(Formato.Horas(tc.Horas));
                Celda(t.Cell()).AlignRight().Text(Formato.Pesos(tc.Valor));
            }
            Total(t.Cell()).Text("Total liquidado");
            Total(t.Cell()).AlignRight().Text(Formato.Horas(r.HorasTotales));
            Total(t.Cell()).AlignRight().Text(Formato.Pesos(r.ValorTotal));
        });
        c.Item().PaddingTop(4).Text(
            $"Días trabajados: {r.DiasTrabajados} · Valor hora: {Formato.Pesos(r.ValorHoraActual)}").FontColor(Suave);
        if (r.HorasPorAprobar > 0)
            c.Item().Text($"Además hay {Formato.Horas(r.HorasPorAprobar)} ({Formato.Pesos(r.ValorPorAprobar)}) por aprobar que no suman en este total.")
                .FontColor(Suave);
    });

    private static void Registros(IContainer x, ResultadoLiquidacion r) => x.Column(c =>
    {
        c.Item().Element(Titulo).Text("Detalle de registros y lo realizado");
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
                d.RelativeColumn(2.2f);
                d.RelativeColumn(1);
                d.RelativeColumn(3);
                d.RelativeColumn(1.3f);
                d.RelativeColumn(1.3f);
            });
            t.Header(h =>
            {
                Encabezado(h.Cell(), "#");
                Encabezado(h.Cell(), "Fecha y horario");
                Encabezado(h.Cell(), "Horas", derecha: true);
                Encabezado(h.Cell(), "Recargos");
                Encabezado(h.Cell(), "Valor", derecha: true);
                Encabezado(h.Cell(), "Estado");
            });
            var n = 0;
            foreach (var reg in r.Registros)
            {
                n++;
                var suma = reg.Revision == RevisionSesion.Aprobada;
                // Fila de datos sin borde inferior: las observaciones van justo debajo, en la misma "tarjeta".
                Celda(t.Cell(), borde: false).Text(n.ToString()).FontColor(Suave);
                Celda(t.Cell(), borde: false).Text(Horario(reg));
                Celda(t.Cell(), borde: false).AlignRight().Text(Formato.Horas(reg.Horas));
                Celda(t.Cell(), borde: false).Column(cat =>
                {
                    foreach (var tc in reg.Categorias)
                        cat.Item().Text($"{Formato.Nombre(tc.Categoria)}: {Formato.Horas(tc.Horas)}");
                });
                Celda(t.Cell(), borde: false).AlignRight().Text(Formato.Pesos(reg.Valor)).FontColor(suma ? Colors.Grey.Darken4 : Suave);
                Celda(t.Cell(), borde: false).Text(Estado(reg)).FontColor(suma ? Colors.Green.Darken2 : Colors.Orange.Darken3);

                t.Cell().ColumnSpan(6).BorderBottom(0.5f).BorderColor(Borde).PaddingBottom(6).PaddingLeft(25).PaddingRight(3)
                    .Text(tx =>
                    {
                        tx.Span("Lo realizado: ").SemiBold();
                        if (string.IsNullOrWhiteSpace(reg.Nota)) tx.Span("Sin observaciones.").Italic().FontColor(Suave);
                        else tx.Span(reg.Nota);
                    });
            }
        });
        var aprobados = r.Registros.Where(x => x.Revision == RevisionSesion.Aprobada).ToList();
        c.Item().PaddingTop(4).Text(
            $"{aprobados.Count} de {r.Registros.Count} registros aprobados. Los valores por registro están redondeados al peso; el total oficial es el del resumen.")
            .FontSize(8).FontColor(Suave);
    });

    private static void Dias(IContainer x, ResultadoLiquidacion r) => x.Column(c =>
    {
        c.Item().Element(Titulo).Text("Detalle por día (horas aprobadas)");
        if (r.Dias.Count == 0)
        {
            c.Item().Text("Sin horas aprobadas en este periodo.").FontColor(Suave);
            return;
        }
        c.Item().Table(t =>
        {
            t.ColumnsDefinition(d =>
            {
                d.RelativeColumn(2.4f);
                d.RelativeColumn(1);
                d.RelativeColumn(1);
                d.RelativeColumn(1);
                d.RelativeColumn(1.3f);
            });
            t.Header(h =>
            {
                Encabezado(h.Cell(), "Día");
                Encabezado(h.Cell(), "Diurnas", derecha: true);
                Encabezado(h.Cell(), "Nocturnas", derecha: true);
                Encabezado(h.Cell(), "Total", derecha: true);
                Encabezado(h.Cell(), "Valor", derecha: true);
            });
            foreach (var d in r.Dias)
            {
                var notas = new List<string>();
                if (d.DominicalOFestivo) notas.Add("dominical/festivo");
                if (d.ExcedeLimiteDiario) notas.Add($"más de {Liquidador.LimiteDiario:0} h");
                Celda(t.Cell()).Text(Largo(d.Fecha) + (notas.Count > 0 ? $" ({string.Join(", ", notas)})" : ""));
                Celda(t.Cell()).AlignRight().Text(Formato.Horas(d.HorasDiurnas));
                Celda(t.Cell()).AlignRight().Text(Formato.Horas(d.HorasNocturnas));
                Celda(t.Cell()).AlignRight().Text(Formato.Horas(d.Horas));
                Celda(t.Cell()).AlignRight().Text(Formato.Pesos(d.Valor));
            }
        });
    });

    private static string Horario(DetalleRegistro reg)
    {
        if (reg.InicioUtc is not { } inicio) return "";
        var desde = ZonaHoraria.ALocal(inicio);
        if (reg.EnCurso || reg.FinUtc is not { } fin) return $"{desde:ddd d MMM yyyy}, desde {desde:h:mm tt}";
        var hasta = ZonaHoraria.ALocal(fin);
        return desde.Date == hasta.Date
            ? $"{desde:ddd d MMM yyyy}, {desde:h:mm tt} a {hasta:h:mm tt}"
            : $"{desde:ddd d MMM yyyy}, {desde:h:mm tt} a {hasta:ddd d MMM}, {hasta:h:mm tt}";
    }

    private static string Estado(DetalleRegistro reg) => reg.Revision switch
    {
        RevisionSesion.Aprobada => reg.EnCurso ? "Aprobada (en curso)" : "Aprobada",
        _ => reg.EnCurso ? "En curso, por aprobar (no suma)" : "Por aprobar (no suma)",
    };

    private static string Largo(DateOnly f) => f.ToString("dddd d 'de' MMMM 'de' yyyy");

    private static string FechaHora(DateTime utc) => ZonaHoraria.ALocal(utc).ToString("d 'de' MMMM 'de' yyyy, h:mm tt");

    private static IContainer Titulo(IContainer x) => x.PaddingBottom(4).DefaultTextStyle(t => t.FontSize(12).SemiBold().FontColor(Azul));

    private static void Encabezado(IContainer x, string texto, bool derecha = false)
    {
        var celda = x.Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(3);
        (derecha ? celda.AlignRight() : celda).Text(texto).SemiBold();
    }

    private static IContainer Celda(IContainer x, bool borde = true) =>
        (borde ? x.BorderBottom(0.5f).BorderColor(Borde) : x).PaddingVertical(3).PaddingHorizontal(3);

    private static IContainer Total(IContainer x) =>
        x.Background(Colors.Blue.Lighten5).PaddingVertical(4).PaddingHorizontal(3).DefaultTextStyle(t => t.Bold());
}
