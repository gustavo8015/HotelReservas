using HotelReservas.Api.Data;
using HotelReservas.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelReservas.Api.Controllers;

public record SolicitudLogin(string Correo, string Clave);
public record RespuestaLogin(string Token, DateTime Expira, string Nombre, string Rol);

/// <summary>CU-01 / RF01: inicio de sesión y emisión del token JWT.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly HotelReservasContext _db;
    private readonly ServicioToken _token;
    public AuthController(HotelReservasContext db, ServicioToken token) { _db = db; _token = token; }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<RespuestaLogin>> Login([FromBody] SolicitudLogin s)
    {
        var u = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Correo == s.Correo && x.Activo);
        if (u == null || !HashClave.Verificar(s.Clave, u.ClaveHash))
            return Unauthorized(new { mensaje = "Correo o contraseña incorrectos." });
        var (token, expira) = _token.Emitir(u);
        return Ok(new RespuestaLogin(token, expira, u.Nombre, u.Rol));
    }

    /// <summary>Utilidad para el administrador: genera el hash PBKDF2 de una contraseña (para actualizar USUARIO.clave_hash).</summary>
    [HttpPost("hash")]
    [Authorize(Roles = "ADMINISTRADOR")]
    public ActionResult Hash([FromBody] string clave) => Ok(new { hash = HashClave.Generar(clave) });
}
