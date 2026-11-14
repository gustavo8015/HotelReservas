using HotelReservas.Api.Data;
using HotelReservas.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelReservas.Api.Controllers;

public record SolicitudLogin(string Correo, string Clave);
public record RespuestaLogin(string Token, DateTime Expira, string Nombre, string Rol);
public record SolicitudRegistro(string Nombres, string Apellidos, string TipoDocumento, string NumeroDocumento, string? Telefono, string Correo, string Clave, bool AutorizaDatos);
public record SolicitudCambioClave(string ClaveActual, string ClaveNueva);

/// <summary>
/// CU-01 / RF01: inicio de sesión y emisión del token JWT, con los controles de la política de seguridad:
/// bloqueo tras cinco intentos fallidos (Ley 1273 de 2009 art. 269A), auditoría de cada intento (Ley 1581 art. 17),
/// política de contraseñas y registro de clientes con autorización de datos (Ley 1581 art. 9; Decreto 1377 art. 5).
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly HotelReservasContext _db;
    private readonly ServicioToken _token;
    private readonly ControlIntentos _intentos;
    private readonly AuditoriaSeguridad _auditoria;
    public AuthController(HotelReservasContext db, ServicioToken token, ControlIntentos intentos, AuditoriaSeguridad auditoria)
    { _db = db; _token = token; _intentos = intentos; _auditoria = auditoria; }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<RespuestaLogin>> Login([FromBody] SolicitudLogin s, CancellationToken ct)
    {
        var u = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Correo == s.Correo && x.Activo, ct);
        if (u == null)
        {
            await _auditoria.RegistrarAsync("LOGIN_FALLIDO", correo: s.Correo, detalle: "usuario inexistente o inactivo", ct: ct);
            return Unauthorized(new { mensaje = "Correo o contraseña incorrectos." });
        }
        if (_intentos.EstaBloqueado(u))
        {
            await _auditoria.RegistrarAsync("ACCESO_DENEGADO", u.IdUsuario, detalle: $"cuenta bloqueada hasta {u.BloqueadoHasta:HH:mm}", ct: ct);
            return StatusCode(423, new { mensaje = $"Cuenta bloqueada por intentos fallidos. Intente de nuevo después de las {u.BloqueadoHasta:HH:mm}." });
        }
        if (!HashClave.Verificar(s.Clave, u.ClaveHash))
        {
            var bloqueado = await _intentos.FalloAsync(u, ct);
            await _auditoria.RegistrarAsync(bloqueado ? "BLOQUEO" : "LOGIN_FALLIDO", u.IdUsuario, detalle: bloqueado ? "cinco intentos fallidos: bloqueo de 15 minutos" : "contraseña incorrecta", ct: ct);
            return Unauthorized(new { mensaje = bloqueado ? "Cuenta bloqueada durante 15 minutos por intentos fallidos." : "Correo o contraseña incorrectos." });
        }
        await _intentos.ExitoAsync(u, ct);
        await _auditoria.RegistrarAsync("LOGIN_OK", u.IdUsuario, detalle: $"perfil {u.Rol}", ct: ct);
        var (token, expira) = _token.Emitir(u);
        return Ok(new RespuestaLogin(token, expira, u.Nombre, u.Rol));
    }

    /// <summary>CU-02 / CU-01: registro de un huésped como usuario CLIENTE. Exige la autorización de tratamiento de datos y una contraseña que cumpla la política.</summary>
    [HttpPost("registro")]
    [AllowAnonymous]
    public async Task<ActionResult> Registro([FromBody] SolicitudRegistro s, CancellationToken ct)
    {
        if (!s.AutorizaDatos) return BadRequest(new { mensaje = "Debe autorizar el tratamiento de sus datos personales (Ley 1581 de 2012, artículo 9)." });
        var (cumple, faltas) = PoliticaContrasenas.Evaluar(s.Clave);
        if (!cumple) return BadRequest(new { mensaje = "La contraseña no cumple la política: " + string.Join(", ", faltas) + "." });
        if (await _db.Usuarios.AnyAsync(x => x.Correo == s.Correo, ct)) return Conflict(new { mensaje = "Ya existe una cuenta con ese correo." });
        var h = await _db.Huespedes.FirstOrDefaultAsync(x => x.TipoDocumento == s.TipoDocumento && x.NumeroDocumento == s.NumeroDocumento, ct);
        if (h == null)
        {
            h = new Huesped { TipoDocumento = s.TipoDocumento, NumeroDocumento = s.NumeroDocumento, Nombres = s.Nombres, Apellidos = s.Apellidos, Telefono = s.Telefono, Correo = s.Correo };
            _db.Huespedes.Add(h);
        }
        h.AutorizacionDatos = true; h.FechaAutorizacion = DateTime.Now; h.FechaRevocatoria = null;
        await _db.SaveChangesAsync(ct);
        var u = new Usuario { Nombre = $"{s.Nombres} {s.Apellidos}".Trim(), Correo = s.Correo, ClaveHash = HashClave.Generar(s.Clave), Rol = "CLIENTE", Activo = true, IdHuesped = h.IdHuesped, ClaveActualizada = DateTime.Now };
        _db.Usuarios.Add(u);
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("HABEAS_CONSULTA", u.IdUsuario, h.IdHuesped, "registro de cliente con autorización de datos", ct: ct);
        return Created($"/api/huespedes/{h.IdHuesped}", new { idUsuario = u.IdUsuario, idHuesped = h.IdHuesped, autorizacionDatos = true, fechaAutorizacion = h.FechaAutorizacion });
    }

    /// <summary>Cambio de contraseña del usuario autenticado, con la política de contraseñas.</summary>
    [HttpPost("cambiar-clave")]
    [Authorize]
    public async Task<ActionResult> CambiarClave([FromBody] SolicitudCambioClave s, CancellationToken ct)
    {
        var u = await _db.Usuarios.FirstAsync(x => x.IdUsuario == User.IdUsuario(), ct);
        if (!HashClave.Verificar(s.ClaveActual, u.ClaveHash)) return Unauthorized(new { mensaje = "La contraseña actual no es correcta." });
        var (cumple, faltas) = PoliticaContrasenas.Evaluar(s.ClaveNueva);
        if (!cumple) return BadRequest(new { mensaje = "La contraseña nueva no cumple la política: " + string.Join(", ", faltas) + "." });
        u.ClaveHash = HashClave.Generar(s.ClaveNueva); u.ClaveActualizada = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("CAMBIO_CLAVE", u.IdUsuario, ct: ct);
        return Ok(new { mensaje = "Contraseña actualizada." });
    }

    /// <summary>Cierre de sesión: el token se descarta en el cliente; aquí solo queda la evidencia (SESION_EXPIRADA cuando lo origina la inactividad).</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult> Logout([FromQuery] bool inactividad, CancellationToken ct)
    {
        await _auditoria.RegistrarAsync(inactividad ? "SESION_EXPIRADA" : "LOGOUT", User.IdUsuario(), ct: ct);
        return Ok();
    }

    /// <summary>Utilidad para el administrador: genera el hash PBKDF2 de una contraseña (para actualizar USUARIO.clave_hash).</summary>
    [HttpPost("hash")]
    [Authorize(Roles = "ADMINISTRADOR")]
    public ActionResult Hash([FromBody] string clave)
    {
        var (cumple, faltas) = PoliticaContrasenas.Evaluar(clave);
        return cumple ? Ok(new { hash = HashClave.Generar(clave) }) : BadRequest(new { mensaje = "La contraseña no cumple la política: " + string.Join(", ", faltas) + "." });
    }
}
