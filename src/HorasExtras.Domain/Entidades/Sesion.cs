namespace HorasExtras.Domain.Entidades;

public enum EstadoSesion { EnCurso, Pausada, Finalizada }

/// <summary>
/// Una jornada de horas extra. Cada "iniciar" o "continuar" abre un tramo y cada "pausar"
/// o "finalizar" lo cierra; las pausas no se cuentan.
/// </summary>
public class Sesion
{
    public const int LargoMaximoNota = 2000;
    public const decimal HorasMaximasPorRegistro = 24;

    /// <summary>Hora de inicio que se asume cuando se registran horas por cantidad sin indicar la hora.</summary>
    public static readonly TimeOnly HoraInicioPorDefecto = new(18, 0);

    public int Id { get; private set; }
    public string UsuarioId { get; private set; } = "";
    public EstadoSesion Estado { get; private set; }
    /// <summary>Observaciones: qué se hizo durante ese tiempo.</summary>
    public string? Nota { get; private set; }
    public DateTime CreadaEnUtc { get; private set; }
    public List<Tramo> Tramos { get; private set; } = [];

    private Sesion() { }

    public static Sesion Iniciar(string usuarioId, DateTime ahoraUtc, string? nota = null)
    {
        var s = new Sesion { UsuarioId = usuarioId, Estado = EstadoSesion.EnCurso, Nota = Limpiar(nota), CreadaEnUtc = ahoraUtc };
        s.Tramos.Add(new Tramo(ahoraUtc));
        return s;
    }

    /// <summary>Registro manual de un intervalo ya trabajado (olvidos o correcciones).</summary>
    public static Sesion Manual(string usuarioId, DateTime inicioUtc, DateTime finUtc, DateTime ahoraUtc, string? nota)
    {
        ValidarIntervalo(inicioUtc, finUtc, ahoraUtc);
        var s = new Sesion { UsuarioId = usuarioId, Estado = EstadoSesion.Finalizada, Nota = Limpiar(nota) ?? "Registro manual", CreadaEnUtc = ahoraUtc };
        s.Tramos.Add(new Tramo(inicioUtc) { FinUtc = finUtc });
        return s;
    }

    /// <summary>
    /// Intervalo en UTC para una cantidad de horas trabajadas en una fecha, empezando a la hora indicada
    /// (hora de Colombia). La liquidación clasifica después esas horas según el día y la franja.
    /// </summary>
    public static (DateTime inicioUtc, DateTime finUtc) IntervaloPorHoras(DateOnly fecha, TimeOnly? horaInicio, decimal horas)
    {
        if (horas <= 0) throw new DatosInvalidosException("Las horas deben ser mayores que cero.");
        if (horas > HorasMaximasPorRegistro)
            throw new DatosInvalidosException($"Un registro no puede superar {HorasMaximasPorRegistro} horas.");
        var minutos = (int)Math.Round(horas * 60);
        if (minutos == 0) throw new DatosInvalidosException("Las horas deben ser mayores que cero.");
        var inicio = ZonaHoraria.AUtc(fecha.ToDateTime(horaInicio ?? HoraInicioPorDefecto));
        return (inicio, inicio.AddMinutes(minutos));
    }

    /// <summary>Ajuste manual de una sesión finalizada: queda como un solo tramo con el nuevo intervalo.</summary>
    public void Ajustar(DateTime inicioUtc, DateTime finUtc, DateTime ahoraUtc, string? nota)
    {
        if (Estado != EstadoSesion.Finalizada)
            throw new ReglaNegocioException("Solo se pueden ajustar sesiones finalizadas. Finaliza la sesión primero.");
        ValidarIntervalo(inicioUtc, finUtc, ahoraUtc);
        Tramos.Clear();
        Tramos.Add(new Tramo(inicioUtc) { FinUtc = finUtc });
        CambiarNota(nota);
    }

    public void CambiarNota(string? nota) => Nota = Limpiar(nota);

    public void Pausar(DateTime ahoraUtc)
    {
        if (Estado != EstadoSesion.EnCurso) throw new ReglaNegocioException("Solo se puede pausar una sesión en curso.");
        CerrarTramo(ahoraUtc);
        Estado = EstadoSesion.Pausada;
    }

    public void Continuar(DateTime ahoraUtc)
    {
        if (Estado != EstadoSesion.Pausada) throw new ReglaNegocioException("Solo se puede continuar una sesión pausada.");
        Tramos.Add(new Tramo(ahoraUtc));
        Estado = EstadoSesion.EnCurso;
    }

    public void Finalizar(DateTime ahoraUtc)
    {
        if (Estado == EstadoSesion.Finalizada) throw new ReglaNegocioException("La sesión ya está finalizada.");
        if (Estado == EstadoSesion.EnCurso) CerrarTramo(ahoraUtc);
        Estado = EstadoSesion.Finalizada;
    }

    public DateTime? InicioUtc => Tramos.Count == 0 ? null : Tramos.Min(t => t.InicioUtc);

    public DateTime? FinUtc => Estado == EstadoSesion.Finalizada ? Tramos.Max(t => t.FinUtc) : null;

    public TimeSpan Duracion(DateTime ahoraUtc) =>
        Tramos.Aggregate(TimeSpan.Zero, (t, x) => t + ((x.FinUtc ?? ahoraUtc) - x.InicioUtc));

    private static void ValidarIntervalo(DateTime inicioUtc, DateTime finUtc, DateTime ahoraUtc)
    {
        if (finUtc <= inicioUtc) throw new DatosInvalidosException("El fin debe ser posterior al inicio.");
        if (finUtc > ahoraUtc) throw new DatosInvalidosException("No se pueden registrar horas en el futuro.");
    }

    private static string? Limpiar(string? nota)
    {
        nota = nota?.Trim();
        if (string.IsNullOrEmpty(nota)) return null;
        if (nota.Length > LargoMaximoNota)
            throw new DatosInvalidosException($"Las observaciones no pueden superar {LargoMaximoNota} caracteres.");
        return nota;
    }

    private void CerrarTramo(DateTime ahoraUtc)
    {
        var abierto = Tramos.Single(t => t.FinUtc is null);
        abierto.FinUtc = ahoraUtc < abierto.InicioUtc ? abierto.InicioUtc : ahoraUtc;
    }
}

public class Tramo
{
    public int Id { get; private set; }
    public int SesionId { get; private set; }
    public DateTime InicioUtc { get; private set; }
    public DateTime? FinUtc { get; set; }

    private Tramo() { }

    public Tramo(DateTime inicioUtc) => InicioUtc = inicioUtc;
}
