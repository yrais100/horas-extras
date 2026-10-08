namespace HorasExtras.Domain.Entidades;

public enum EstadoSesion { EnCurso, Pausada, Finalizada }

/// <summary>
/// Una jornada de horas extra. Cada "iniciar" o "continuar" abre un tramo y cada "pausar"
/// o "finalizar" lo cierra; las pausas no se cuentan.
/// </summary>
public class Sesion
{
    public int Id { get; private set; }
    public string UsuarioId { get; private set; } = "";
    public EstadoSesion Estado { get; private set; }
    public string? Nota { get; set; }
    public DateTime CreadaEnUtc { get; private set; }
    public List<Tramo> Tramos { get; private set; } = [];

    private Sesion() { }

    public static Sesion Iniciar(string usuarioId, DateTime ahoraUtc, string? nota = null)
    {
        var s = new Sesion { UsuarioId = usuarioId, Estado = EstadoSesion.EnCurso, Nota = nota, CreadaEnUtc = ahoraUtc };
        s.Tramos.Add(new Tramo(ahoraUtc));
        return s;
    }

    /// <summary>Registro manual de un intervalo ya trabajado (olvidos o correcciones).</summary>
    public static Sesion Manual(string usuarioId, DateTime inicioUtc, DateTime finUtc, DateTime ahoraUtc, string? nota)
    {
        if (finUtc <= inicioUtc) throw new DatosInvalidosException("El fin debe ser posterior al inicio.");
        if (finUtc > ahoraUtc) throw new DatosInvalidosException("No se pueden registrar horas en el futuro.");
        var s = new Sesion { UsuarioId = usuarioId, Estado = EstadoSesion.Finalizada, Nota = nota ?? "Registro manual", CreadaEnUtc = ahoraUtc };
        s.Tramos.Add(new Tramo(inicioUtc) { FinUtc = finUtc });
        return s;
    }

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
