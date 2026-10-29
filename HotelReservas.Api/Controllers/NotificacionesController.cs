using HotelReservas.Api.Data;
using HotelReservas.Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelReservas.Api.Controllers;

public record SolicitudNotificacion(string CodigoReserva, string Canal);   // Canal: WHATSAPP o CORREO

/// <summary>RF20: confirmación de la reserva por WhatsApp (enlace wa.me) o por correo electrónico (SMTP desde la cuenta del hotel).</summary>
[ApiController]
[Route("api/notificaciones")]
[Authorize]
public class NotificacionesController : ControllerBase
{
    private readonly HotelReservasContext _db;
    private readonly ServicioCorreo _correo;
    public NotificacionesController(HotelReservasContext db, ServicioCorreo correo) { _db = db; _correo = correo; }

    /// <summary>GET /api/notificaciones/estado: indica si el envío de correo está configurado.</summary>
    [HttpGet("estado")]
    public ActionResult Estado() => Ok(new { remitente = _correo.Remitente, correoConfigurado = _correo.Configurado, whatsapp = "enlace wa.me generado por la API" });

    /// <summary>POST /api/notificaciones { codigoReserva, canal }. Devuelve 202 con el destino y, para WhatsApp, el enlace listo.</summary>
    [HttpPost]
    public async Task<ActionResult> Enviar([FromBody] SolicitudNotificacion s)
    {
        var r = await _db.Reservas.Include(x => x.Habitacion).ThenInclude(h => h.Tipo).Include(x => x.Huesped).Include(x => x.Pagos).Include(x => x.Comprobante)
            .AsNoTracking().FirstOrDefaultAsync(x => x.Codigo == s.CodigoReserva);
        if (r == null) return NotFound(new { mensaje = "La reserva no existe." });

        var canal = (s.Canal ?? "").Trim().ToUpperInvariant();
        var texto = TextoConfirmacion(r);
        if (canal == "WHATSAPP")
        {
            var tel = new string((r.Huesped.Telefono ?? "").Where(char.IsDigit).ToArray());
            if (tel.Length == 10 && tel.StartsWith("3")) tel = "57" + tel;
            if (tel.Length == 0) return BadRequest(new { mensaje = "El huésped no tiene teléfono registrado." });
            return Accepted(new { canal, destino = "+" + tel, enlace = $"https://wa.me/{tel}?text={Uri.EscapeDataString(texto)}", texto });
        }
        if (canal == "CORREO")
        {
            if (string.IsNullOrWhiteSpace(r.Huesped.Correo)) return BadRequest(new { mensaje = "El huésped no tiene correo registrado." });
            if (!_correo.Configurado) return StatusCode(503, new { mensaje = "El envío de correo no está configurado en el servidor (Correo:ClaveAplicacion)." });
            try
            {
                await _correo.EnviarAsync(r.Huesped.Correo, $"Confirmación de reserva {r.Codigo} · Hotel Reservas", texto.Replace("*", ""));
                return Accepted(new { canal, destino = r.Huesped.Correo, remitente = _correo.Remitente });
            }
            catch (Exception ex) { return StatusCode(502, new { mensaje = "No fue posible enviar el correo: " + ex.Message }); }
        }
        return BadRequest(new { mensaje = "Canal no válido: use WHATSAPP o CORREO." });
    }

    private string TextoConfirmacion(Reserva r)
    {
        var noches = r.FechaSalida.DayNumber - r.FechaEntrada.DayNumber;
        var medio = r.Pagos.OrderByDescending(p => p.IdPago).Select(p => p.Medio).FirstOrDefault() ?? "pendiente";
        string M(decimal v) => Asistente.CatalogoFunciones.Moneda(v);
        return $"Hotel Reservas · Confirmación de reserva {r.Codigo}\n" +
               (r.Comprobante != null ? $"Comprobante: {r.Comprobante.Numero}\n" : "Estado: pendiente de pago\n") +
               $"Huésped: {r.Huesped.Nombres} {r.Huesped.Apellidos} ({r.Huesped.TipoDocumento} {r.Huesped.NumeroDocumento})\n" +
               $"Habitación: {r.Habitacion.Numero} · {r.Habitacion.Tipo.Nombre} · {r.NumeroPersonas} persona{(r.NumeroPersonas > 1 ? "s" : "")}\n" +
               $"Entrada: {r.FechaEntrada:yyyy-MM-dd} · Salida: {r.FechaSalida:yyyy-MM-dd} ({noches} noche{(noches > 1 ? "s" : "")})\n" +
               $"Subtotal: {M(r.Subtotal)}\nIVA 19 %: {(r.ExentoIva ? "exento (extranjero no residente)" : M(r.Impuesto))}\nTotal con IVA: {M(r.ValorTotal)}\n" +
               $"Pago: {medio} · Estado: {r.Estado}\n\nPresente esta confirmación en recepción. ¡Lo esperamos!\nHotel Reservas · {_correo.Remitente}";
    }
}
