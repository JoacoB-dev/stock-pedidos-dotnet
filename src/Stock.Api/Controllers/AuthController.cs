using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Dtos;
using Stock.Api.Infra;
using Stock.Datos;
using Stock.Dominio.Entidades;

namespace Stock.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(StockDbContext db, GeneradorDeTokens tokens, IPasswordHasher<Usuario> hasher)
    : ControllerBase
{
    /// <summary>Devuelve un JWT si el email y la contraseña son correctos.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email && u.Activo, ct);

        // Mismo mensaje si no existe el usuario o si la clave está mal: no damos pistas
        if (usuario is null ||
            hasher.VerifyHashedPassword(usuario, usuario.HashClave, req.Clave) == PasswordVerificationResult.Failed)
            return Problem(statusCode: 401, title: "No autorizado", detail: "Email o contraseña incorrectos.");

        var (token, expira) = tokens.Generar(usuario);
        return Ok(new LoginResponse(token, expira, new UsuarioDto(usuario.Id, usuario.Email, usuario.Nombre, usuario.Rol)));
    }

    /// <summary>Datos del usuario del token.</summary>
    [HttpGet("yo")]
    [Authorize]
    public async Task<UsuarioDto> Yo(CancellationToken ct)
    {
        var id = User.IdUsuario();
        return await db.Usuarios.Where(u => u.Id == id)
            .Select(u => new UsuarioDto(u.Id, u.Email, u.Nombre, u.Rol)).FirstAsync(ct);
    }
}
