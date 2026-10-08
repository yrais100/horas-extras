using HorasExtras.Domain;
using HorasExtras.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace HorasExtras.Application;

/// <summary>Salario (con historial) y configuración de recargos de cada usuario.</summary>
public class PerfilService(IAppDbContextFactory fabrica)
{
    public async Task<IReadOnlyList<SalarioDto>> SalariosAsync(string usuarioId, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        return await db.Salarios.Where(s => s.UsuarioId == usuarioId)
            .OrderByDescending(s => s.VigenteDesde)
            .Select(s => new SalarioDto(s.Id, s.VigenteDesde, s.SalarioMensual, s.ValorHoraManual))
            .ToListAsync(ct);
    }

    /// <summary>Agrega un salario vigente desde una fecha; si ya hay uno en esa fecha, lo reemplaza.</summary>
    public async Task<SalarioDto> GuardarSalarioAsync(string usuarioId, SalarioInput input, CancellationToken ct = default)
    {
        if (input.SalarioMensual <= 0 && input.ValorHoraManual is not > 0)
            throw new DatosInvalidosException("Ingresa el salario mensual o un valor hora manual.");
        if (input.SalarioMensual < 0 || input.ValorHoraManual < 0)
            throw new DatosInvalidosException("Los valores no pueden ser negativos.");

        await using var db = fabrica.Crear();
        var s = await db.Salarios.FirstOrDefaultAsync(x => x.UsuarioId == usuarioId && x.VigenteDesde == input.VigenteDesde, ct);
        if (s is null)
        {
            s = new SalarioUsuario { UsuarioId = usuarioId, VigenteDesde = input.VigenteDesde };
            db.Salarios.Add(s);
        }
        s.SalarioMensual = input.SalarioMensual;
        s.ValorHoraManual = input.ValorHoraManual is > 0 ? input.ValorHoraManual : null;
        await db.SaveChangesAsync(ct);
        return new(s.Id, s.VigenteDesde, s.SalarioMensual, s.ValorHoraManual);
    }

    public async Task EliminarSalarioAsync(string usuarioId, int id, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        var s = await db.Salarios.FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == usuarioId, ct)
            ?? throw new NoEncontradoException("Salario no encontrado.");
        db.Salarios.Remove(s);
        await db.SaveChangesAsync(ct);
    }

    public async Task<RecargosDto> RecargosAsync(string usuarioId, CancellationToken ct = default)
    {
        await using var db = fabrica.Crear();
        return RecargosDto.De(await db.Recargos.FindAsync([usuarioId], ct) ?? ConfiguracionRecargos.PorDefecto(usuarioId));
    }

    public async Task<RecargosDto> GuardarRecargosAsync(string usuarioId, RecargosDto input, CancellationToken ct = default)
    {
        if (new[] { input.HorasMensuales, input.PctExtraDiurna, input.PctExtraNocturna,
                input.PctExtraDominicalDiurna, input.PctExtraDominicalNocturna }.Any(v => v < 0) || input.HorasMensuales == 0)
            throw new DatosInvalidosException("Los porcentajes no pueden ser negativos y las horas mensuales deben ser mayores a cero.");
        if (input.InicioNocturno == input.FinNocturno)
            throw new DatosInvalidosException("El inicio y el fin de la jornada nocturna no pueden ser iguales.");

        await using var db = fabrica.Crear();
        var c = await db.Recargos.FindAsync([usuarioId], ct);
        if (c is null)
        {
            c = ConfiguracionRecargos.PorDefecto(usuarioId);
            db.Recargos.Add(c);
        }
        c.Modo = input.Modo;
        c.InicioNocturno = input.InicioNocturno;
        c.FinNocturno = input.FinNocturno;
        c.HorasMensuales = input.HorasMensuales;
        c.PctExtraDiurna = input.PctExtraDiurna;
        c.PctExtraNocturna = input.PctExtraNocturna;
        c.PctExtraDominicalDiurna = input.PctExtraDominicalDiurna;
        c.PctExtraDominicalNocturna = input.PctExtraDominicalNocturna;
        await db.SaveChangesAsync(ct);
        return RecargosDto.De(c);
    }
}
