namespace HorasExtras.Domain;

/// <summary>Una operación viola una regla del negocio (por ejemplo, pausar una sesión que no está en curso).</summary>
public class ReglaNegocioException(string mensaje) : Exception(mensaje);

/// <summary>Los datos recibidos no son válidos (HTTP 400).</summary>
public class DatosInvalidosException(string mensaje) : ReglaNegocioException(mensaje);
