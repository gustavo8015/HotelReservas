using System.Security.Claims;
using HotelReservas.Api.Data;
using HotelReservas.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelReservas.Api.Controllers;

public record SolicitudRectificacion(string? Nombres, string? Apellidos, string? Telefono, string? Correo);
public record SolicitudHabeas(string? Detalle);

/// <summary>
/// Derechos del titular (Ley 1581 de 2012 art. 8; Ley 1266 de 2008 art. 6; Decreto 1377 de 2013 art. 9):
/// el cliente autenticado consulta, rectifica, revoca la autorización y pide la supresión de sus datos.
/// Toda solicitud queda en SOLICITUD_HABEAS_DATA (plazos de los arts. 14 y 15) y en AUDITORIA_SEGURIDAD.
/// </summary>
[ApiController]
[Route("api/habeasdata")]
[Authorize]
public class HabeasDataController : ControllerBase
{
    private readonly HotelReservasContext _db;
    private readonly AuditoriaSeguridad _auditoria;
    public HabeasDataController(HotelReservasContext db, AuditoriaSeguridad auditoria) { _db = db; _auditoria = auditoria; }

    private async Task<Huesped?> HuespedDelTokenAsync(CancellationToken ct)
    {
        var u = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.IdUsuario == User.IdUsuario(), ct);
        return u?.IdHuesped == null ? null : await _db.Huespedes.FirstOrDefaultAsync(x => x.IdHuesped == u.IdHuesped, ct);
    }

    /// <summary>Derecho de acceso: los datos que el hotel tiene del titular, su autorización y sus reservas. Gratuito (Ley 1266 art. 6).</summary>
    [HttpGet("mis-datos")]
    public async Task<ActionResult> MisDatos(CancellationToken ct)
    {
        var h = await HuespedDelTokenAsync(ct);
        if (h == null) return NotFound(new { mensaje = "El usuario no tiene un huésped asociado." });
        var reservas = await _db.Reservas.AsNoTracking().Where(r => r.IdHuesped == h.IdHuesped)
            .Select(r => new { r.Codigo, r.Estado, r.FechaEntrada, r.FechaSalida, r.ValorTotal }).ToListAsync(ct);
        var solicitudes = await _db.SolicitudesHabeasData.AsNoTracking().Where(s => s.IdHuesped == h.IdHuesped).OrderByDescending(s => s.FechaSolicitud).ToListAsync(ct);
        await _auditoria.RegistrarAsync("HABEAS_CONSULTA", User.IdUsuario(), h.IdHuesped, "consulta de datos por el titular", ct: ct);
        return Ok(new
        {
            datos = new { h.Nombres, h.Apellidos, h.TipoDocumento, h.NumeroDocumento, h.Telefono, h.Correo, h.ExtranjeroNoResidente },
            autorizacion = new { h.AutorizacionDatos, h.FechaAutorizacion, h.FechaRevocatoria },
            finalidad = "Gestión de reservas, pagos, comprobantes y notificaciones del hotel; reportes exigidos por la ley de turismo y migración.",
            derechos = new[] { "conocer", "actualizar", "rectificar", "revocar la autorización", "solicitar la supresión", "presentar reclamos ante la SIC" },
            canal = "hotelreservas2026@gmail.com",
            reservas, solicitudes,
        });
    }

    /// <summary>Rectificación o actualización (Ley 1581 art. 8 lit. a; Ley 1266 art. 6): el titular corrige sus datos de contacto y nombre.</summary>
    [HttpPut("rectificar")]
    public async Task<ActionResult> Rectificar([FromBody] SolicitudRectificacion s, CancellationToken ct)
    {
        var h = await HuespedDelTokenAsync(ct);
        if (h == null) return NotFound();
        var cambios = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.Nombres) && s.Nombres != h.Nombres) { h.Nombres = s.Nombres.Trim(); cambios.Add("nombres"); }
        if (!string.IsNullOrWhiteSpace(s.Apellidos) && s.Apellidos != h.Apellidos) { h.Apellidos = s.Apellidos.Trim(); cambios.Add("apellidos"); }
        if (s.Telefono != null && s.Telefono != h.Telefono) { h.Telefono = s.Telefono.Trim(); cambios.Add("teléfono"); }
        if (!string.IsNullOrWhiteSpace(s.Correo) && s.Correo != h.Correo)
        {
            if (!System.Net.Mail.MailAddress.TryCreate(s.Correo, out _)) return BadRequest(new { mensaje = "Correo no válido." });
            h.Correo = s.Correo.Trim(); cambios.Add("correo");
        }
        if (cambios.Count == 0) return Ok(new { mensaje = "No hay cambios." });
        _db.SolicitudesHabeasData.Add(new SolicitudHabeasData { IdHuesped = h.IdHuesped, Tipo = "RECTIFICACION", FechaSolicitud = DateTime.Now, Detalle = "campos: " + string.Join(", ", cambios), Estado = "RESUELTA", FechaRespuesta = DateTime.Now, Respuesta = "Rectificación aplicada en línea", IdUsuarioResuelve = User.IdUsuario() });
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("HABEAS_RECTIFICACION", User.IdUsuario(), h.IdHuesped, "campos: " + string.Join(", ", cambios), ct: ct);
        return Ok(new { mensaje = "Datos actualizados.", campos = cambios });
    }

    /// <summary>Revocatoria de la autorización (Decreto 1377 art. 9). Las reservas vigentes se conservan por la relación contractual.</summary>
    [HttpPost("revocar")]
    public async Task<ActionResult> Revocar([FromBody] SolicitudHabeas s, CancellationToken ct)
    {
        var h = await HuespedDelTokenAsync(ct);
        if (h == null) return NotFound();
        h.AutorizacionDatos = false; h.FechaRevocatoria = DateTime.Now;
        _db.SolicitudesHabeasData.Add(new SolicitudHabeasData { IdHuesped = h.IdHuesped, Tipo = "REVOCATORIA", FechaSolicitud = DateTime.Now, Detalle = s.Detalle, Estado = "RESUELTA", FechaRespuesta = DateTime.Now, Respuesta = "Autorización revocada; no se enviarán comunicaciones distintas de las de sus reservas vigentes" });
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("HABEAS_REVOCATORIA", User.IdUsuario(), h.IdHuesped, ct: ct);
        return Ok(new { mensaje = "Autorización revocada. Sus reservas vigentes se mantienen por la obligación contractual y tributaria." });
    }

    /// <summary>Solicitud de supresión (Ley 1581 arts. 8 lit. e y 15). Queda en trámite; el administrador la resuelve con sp_SuprimirDatosHuesped cuando no hay reservas vigentes.</summary>
    [HttpPost("supresion")]
    public async Task<ActionResult> Supresion([FromBody] SolicitudHabeas s, CancellationToken ct)
    {
        var h = await HuespedDelTokenAsync(ct);
        if (h == null) return NotFound();
        var vigentes = await _db.Reservas.CountAsync(r => r.IdHuesped == h.IdHuesped && (r.Estado == "PENDIENTE" || r.Estado == "CONFIRMADA" || r.Estado == "EN_CASA"), ct);
        var sol = new SolicitudHabeasData { IdHuesped = h.IdHuesped, Tipo = "SUPRESION", FechaSolicitud = DateTime.Now, Detalle = s.Detalle, Estado = "EN_TRAMITE" };
        _db.SolicitudesHabeasData.Add(sol);
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("HABEAS_SUPRESION", User.IdUsuario(), h.IdHuesped, vigentes > 0 ? $"solicitud en trámite; {vigentes} reserva(s) vigente(s)" : "solicitud en trámite", ct: ct);
        return Accepted(new { idSolicitud = sol.IdSolicitud, estado = sol.Estado, plazoDias = 15, mensaje = vigentes > 0 ? $"Solicitud registrada. Tiene {vigentes} reserva(s) vigente(s); la supresión se ejecuta al terminar la estadía." : "Solicitud registrada; se resolverá en máximo quince días hábiles." });
    }

    /// <summary>Administrador: solicitudes pendientes y auditoría de seguridad reciente (política numeral 11, Oficial de Seguridad).</summary>
    [HttpGet("solicitudes")]
    [Authorize(Roles = "ADMINISTRADOR")]
    public async Task<ActionResult> Solicitudes(CancellationToken ct)
        => Ok(await _db.SolicitudesHabeasData.AsNoTracking().OrderByDescending(s => s.FechaSolicitud).Take(100).ToListAsync(ct));

    [HttpGet("auditoria")]
    [Authorize(Roles = "ADMINISTRADOR")]
    public async Task<ActionResult> Auditoria([FromQuery] int top = 100, CancellationToken ct = default)
        => Ok(await _db.EventosSeguridad.AsNoTracking().OrderByDescending(e => e.Fecha).Take(Math.Clamp(top, 1, 500)).ToListAsync(ct));

    /// <summary>Administrador: resuelve una solicitud de supresión ejecutando el procedimiento del script 09.</summary>
    [HttpPost("solicitudes/{id:int}/suprimir")]
    [Authorize(Roles = "ADMINISTRADOR")]
    public async Task<ActionResult> Suprimir(int id, CancellationToken ct)
    {
        var sol = await _db.SolicitudesHabeasData.FirstOrDefaultAsync(s => s.IdSolicitud == id && s.Tipo == "SUPRESION", ct);
        if (sol == null) return NotFound();
        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"EXEC sp_SuprimirDatosHuesped @id_huesped={sol.IdHuesped}, @id_usuario={User.IdUsuario()}", ct);
            return Ok(new { mensaje = "Datos suprimidos (anonimizados)." });
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.StartsWith("50030"))
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }
}
