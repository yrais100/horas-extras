using HorasExtras.Domain;
using HorasExtras.Web.Servicios;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace HorasExtras.Web.Components.Shared;

/// <summary>Base de las páginas: expone el usuario autenticado y muestra errores de negocio.</summary>
public abstract class PaginaUsuario : ComponentBase
{
    [CascadingParameter] private Task<AuthenticationState> EstadoAuth { get; set; } = default!;

    protected string UsuarioId { get; private set; } = "";
    protected string? Error { get; set; }

    protected override async Task OnInitializedAsync()
    {
        UsuarioId = (await EstadoAuth).User.Id();
        await CargarAsync();
    }

    protected virtual Task CargarAsync() => Task.CompletedTask;

    /// <summary>Ejecuta una acción y muestra el mensaje si viola una regla de negocio.</summary>
    protected async Task Intentar(Func<Task> accion)
    {
        Error = null;
        try
        {
            await accion();
        }
        catch (ReglaNegocioException e)
        {
            Error = e.Message;
        }
        catch (Application.NoEncontradoException e)
        {
            Error = e.Message;
        }
    }
}
