using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Stock.Dominio.Entidades;

namespace Stock.Api.Infra;

public class OpcionesJwt
{
    public string Emisor { get; set; } = "stock-pedidos";
    public string Audiencia { get; set; } = "stock-pedidos-web";
    /// <summary>Clave simétrica de al menos 32 caracteres. En producción va por variable de entorno.</summary>
    public string Clave { get; set; } = "";
    public int MinutosDeVida { get; set; } = 480;

    public SymmetricSecurityKey ClaveDeFirma() => new(Encoding.UTF8.GetBytes(Clave));
}

/// <summary>
/// Arma el JWT. Usa siempre la hora real (no el TimeProvider de la app) porque la validación
/// del token también usa la hora real.
/// </summary>
public class GeneradorDeTokens(OpcionesJwt opciones)
{
    public (string Token, DateTime Expira) Generar(Usuario usuario)
    {
        var expira = DateTime.UtcNow.AddMinutes(opciones.MinutosDeVida);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = opciones.Emisor,
            Audience = opciones.Audiencia,
            NotBefore = DateTime.UtcNow,
            IssuedAt = DateTime.UtcNow,
            Expires = expira,
            // Nombres cortos de claims (sub, email, name, role): MapInboundClaims está apagado en Program.cs
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
                new Claim(ClaimNames.Nombre, usuario.Nombre),
                new Claim(ClaimNames.Rol, usuario.Rol.ToString())
            ]),
            SigningCredentials = new SigningCredentials(opciones.ClaveDeFirma(), SecurityAlgorithms.HmacSha256)
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expira);
    }
}

public static class UsuarioActualExtensions
{
    public static int IdUsuario(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? throw new InvalidOperationException("El token no tiene el id de usuario."));

    public static bool EsAdministrador(this ClaimsPrincipal user) =>
        user.IsInRole(nameof(Rol.Administrador));
}

public static class ClaimNames
{
    public const string Nombre = "name";
    public const string Rol = "role";
}

public static class Roles
{
    public const string Administrador = nameof(Rol.Administrador);
    public const string Vendedor = nameof(Rol.Vendedor);
}
