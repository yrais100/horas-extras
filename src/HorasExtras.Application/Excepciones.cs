using HorasExtras.Domain;

namespace HorasExtras.Application;

/// <summary>El recurso no existe o no pertenece al usuario (HTTP 404).</summary>
public class NoEncontradoException(string mensaje) : Exception(mensaje);
