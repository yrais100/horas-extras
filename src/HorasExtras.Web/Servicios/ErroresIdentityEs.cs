using Microsoft.AspNetCore.Identity;

namespace HorasExtras.Web.Servicios;

/// <summary>Mensajes de Identity en español (web y API).</summary>
public class ErroresIdentityEs : IdentityErrorDescriber
{
    private static IdentityError E(string code, string msg) => new() { Code = code, Description = msg };

    public override IdentityError DuplicateEmail(string email) => E(nameof(DuplicateEmail), $"El correo {email} ya está registrado.");
    public override IdentityError DuplicateUserName(string userName) => E(nameof(DuplicateUserName), $"El usuario {userName} ya está registrado.");
    public override IdentityError InvalidEmail(string? email) => E(nameof(InvalidEmail), $"El correo {email} no es válido.");
    public override IdentityError InvalidUserName(string? userName) => E(nameof(InvalidUserName), $"El usuario {userName} no es válido.");
    public override IdentityError PasswordMismatch() => E(nameof(PasswordMismatch), "La clave no es correcta.");
    public override IdentityError PasswordTooShort(int length) => E(nameof(PasswordTooShort), $"La clave debe tener al menos {length} caracteres.");
    public override IdentityError PasswordRequiresDigit() => E(nameof(PasswordRequiresDigit), "La clave debe tener al menos un número.");
    public override IdentityError PasswordRequiresLower() => E(nameof(PasswordRequiresLower), "La clave debe tener al menos una minúscula.");
    public override IdentityError PasswordRequiresUpper() => E(nameof(PasswordRequiresUpper), "La clave debe tener al menos una mayúscula.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => E(nameof(PasswordRequiresNonAlphanumeric), "La clave debe tener al menos un símbolo (por ejemplo . o !).");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => E(nameof(PasswordRequiresUniqueChars), $"La clave debe tener al menos {uniqueChars} caracteres distintos.");
}
