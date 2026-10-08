using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HorasExtras.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAplicacion(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<RegistroHorasService>();
        services.AddScoped<LiquidacionService>();
        services.AddScoped<PerfilService>();
        return services;
    }
}
