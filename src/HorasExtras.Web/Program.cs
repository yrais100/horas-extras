using System.Globalization;
using HorasExtras.Application;
using HorasExtras.Infrastructure;
using HorasExtras.Web.Api;
using HorasExtras.Web.Components;
using HorasExtras.Web.Components.Account;
using HorasExtras.Web.Servicios;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Moneda y fechas en formato colombiano ($ 1.234.567, "jue 8 oct").
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("es-CO");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// La web inicia sesión con cookie; la app móvil con token bearer (+ refresh token) por /api/auth/login.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddBearerToken(IdentityConstants.BearerScheme)
    .AddIdentityCookies();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(PoliticasApi.Usuario, p => p
        .AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme)
        .RequireAuthenticatedUser())
    .AddPolicy(PoliticasApi.Admin, p => p
        .AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme)
        .RequireRole(AppDbContext.RolAdmin));

builder.Services.AddInfraestructura(builder.Configuration);
builder.Services.AddAplicacion();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<Usuario>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false; // sin servidor de correo en el MVP
        options.User.RequireUniqueEmail = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddErrorDescriber<ErroresIdentityEs>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddUserManager<UsuarioManager>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddApiEndpoints();

builder.Services.AddSingleton<IEmailSender<Usuario>, IdentityNoOpEmailSender>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Si la base de datos no responde, la app arranca igual: el error queda en el log y en /salud.
var errorBaseDeDatos = await app.Services.InicializarBaseDeDatosAsync(app.Configuration);
if (errorBaseDeDatos is null)
{
    using var scope = app.Services.CreateScope();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roles.RoleExistsAsync(AppDbContext.RolAdmin)) await roles.CreateAsync(new IdentityRole(AppDbContext.RolAdmin));
}
else
{
    app.Logger.LogCritical("No se pudo preparar la base de datos: {Error}", errorBaseDeDatos);
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.MapOpenApi(); // /openapi/v1.json
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseCors();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();
app.MapHorasExtrasApi();

// Diagnóstico de despliegue: indica si la base de datos responde (no expone la cadena de conexión).
app.MapGet("/salud", async (IDbContextFactory<AppDbContext> fabrica) =>
{
    if (errorBaseDeDatos is not null)
        return Results.Json(new { estado = "error", baseDeDatos = errorBaseDeDatos }, statusCode: 503);
    await using var db = await fabrica.CreateDbContextAsync();
    return await db.Database.CanConnectAsync()
        ? Results.Json(new { estado = "ok" })
        : Results.Json(new { estado = "error", baseDeDatos = "La base de datos no responde." }, statusCode: 503);
});

app.Run();

public partial class Program;
