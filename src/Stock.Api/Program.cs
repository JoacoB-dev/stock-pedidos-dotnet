using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Stock.Api.Infra;
using Stock.Api.Servicios;
using Stock.Datos;
using Stock.Dominio.Entidades;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Base de datos ----------
var cadena = config.GetConnectionString("Stock")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Stock.");
builder.Services.AddDbContext<StockDbContext>(o => o.UseSqlServer(cadena));

// ---------- Servicios propios ----------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<PedidoServicio>();
builder.Services.AddScoped<ProductoServicio>();
builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ---------- Autenticación JWT ----------
var jwt = config.GetSection("Jwt").Get<OpcionesJwt>() ?? new OpcionesJwt();
if (jwt.Clave.Length < 32)
    throw new InvalidOperationException("Jwt:Clave tiene que tener al menos 32 caracteres (configurala por variable de entorno Jwt__Clave).");
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<GeneradorDeTokens>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // dejamos los nombres cortos: sub, name, role
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Emisor,
            ValidAudience = jwt.Audiencia,
            IssuerSigningKey = jwt.ClaveDeFirma(),
            NameClaimType = ClaimNames.Nombre,
            RoleClaimType = ClaimNames.Rol,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

// ---------- MVC, errores y OpenAPI ----------
builder.Services.AddControllers(o => o.Filters.Add<FiltroDeValidacion>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
    c.ProblemDetails.Extensions["traceId"] = c.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ManejadorDeErrores>();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info = new OpenApiInfo { Title = "Stock y Pedidos API", Version = "v1",
        Description = "API de una distribuidora mayorista (proyecto de portfolio). Logueate en /api/auth/login y usá el token con el botón Authorize." };
    doc.Components ??= new OpenApiComponents();
    doc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    doc.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Pegá el token que devuelve /api/auth/login"
    };
    doc.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", doc)] = [] }];
    return Task.CompletedTask;
}));

var app = builder.Build();

// ---------- Migraciones y datos demo ----------
if (config.GetValue<bool>("Bd:MigrarAlIniciar"))
    await PrepararBaseAsync(app);

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/openapi/v1.json", "Stock y Pedidos v1");
    o.RoutePrefix = "swagger";
});

// La interfaz React compilada se sirve desde wwwroot (ver Dockerfile / README)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/salud", () => Results.Ok(new { estado = "ok" })).AllowAnonymous();
// Cualquier ruta que no sea de la API la resuelve React Router
app.MapFallbackToFile("index.html");

app.Run();

static async Task PrepararBaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<StockDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // En Docker la base puede tardar unos segundos en aceptar conexiones
    for (var intento = 1; ; intento++)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception ex) when (intento < 10 && ex is Microsoft.Data.SqlClient.SqlException)
        {
            log.LogWarning("SQL Server todavía no responde (intento {Intento}): {Mensaje}", intento, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }

    var cfg = app.Configuration;
    if (cfg.GetValue<bool>("Bd:CargarUsuariosDemo"))
        await Semilla.UsuariosAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>());
    if (cfg.GetValue<bool>("Bd:CargarDatosDemo"))
        await Semilla.DatosDemoAsync(db, scope.ServiceProvider.GetRequiredService<TimeProvider>());
}

/// <summary>Visible para WebApplicationFactory en las pruebas.</summary>
public partial class Program;
