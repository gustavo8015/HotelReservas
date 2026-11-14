using System.Security.Claims;
using HotelReservas.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelReservas.Api.Controllers;

public record ConsultaSeguridad(string Pregunta);

/// <summary>
/// Puente hacia el servicio inteligente de seguridad de la información (servicio_seguridad_ia, FastAPI, puerto 8100):
/// responde preguntas de cumplimiento con la Ley 1266 de 2008, la Ley 1581 de 2012 y la política del hotel.
/// La pregunta pasa primero por el filtro de datos personales, de modo que nunca sale un documento, teléfono o correo.
/// </summary>
[ApiController]
[Route("api/seguridad")]
[Authorize]
public class SeguridadIAController : ControllerBase
{
    private readonly IHttpClientFactory _http;
    private readonly AuditoriaSeguridad _auditoria;
    public SeguridadIAController(IHttpClientFactory http, AuditoriaSeguridad auditoria) { _http = http; _auditoria = auditoria; }

    [HttpPost("consultar")]
    public async Task<ActionResult> Consultar([FromBody] ConsultaSeguridad c, CancellationToken ct)
    {
        var limpia = Asistente.FiltroDatosPersonales.Aplicar(c.Pregunta ?? "");
        if (limpia != (c.Pregunta ?? "")) await _auditoria.RegistrarAsync("DATO_FILTRADO_IA", User.IdUsuario(), detalle: "la consulta de seguridad contenía datos personales; se enmascararon", ct: ct);
        var rol = User.FindFirstValue(ClaimTypes.Role) ?? "RECEPCIONISTA";
        try
        {
            var r = await _http.CreateClient("seguridad_ia").PostAsJsonAsync("/consultar", new { pregunta = limpia, rol, k = 3 }, ct);
            var cuerpo = await r.Content.ReadAsStringAsync(ct);
            return new ContentResult { Content = cuerpo, ContentType = "application/json", StatusCode = (int)r.StatusCode };
        }
        catch (Exception ex) { return StatusCode(503, new { mensaje = "El servicio inteligente de seguridad no está disponible.", detalle = ex.Message }); }
    }

    [HttpPost("verificar-dato")]
    public async Task<ActionResult> VerificarDato([FromBody] ConsultaSeguridad c, CancellationToken ct)
    {
        try
        {
            var r = await _http.CreateClient("seguridad_ia").PostAsJsonAsync("/verificar-dato", new { texto = c.Pregunta }, ct);
            return new ContentResult { Content = await r.Content.ReadAsStringAsync(ct), ContentType = "application/json", StatusCode = (int)r.StatusCode };
        }
        catch (Exception ex) { return StatusCode(503, new { mensaje = "El servicio inteligente de seguridad no está disponible.", detalle = ex.Message }); }
    }

    [HttpGet("politica")]
    [AllowAnonymous]
    public async Task<ActionResult> Politica(CancellationToken ct)
    {
        try
        {
            var r = await _http.CreateClient("seguridad_ia").GetAsync("/politica", ct);
            return new ContentResult { Content = await r.Content.ReadAsStringAsync(ct), ContentType = "application/json", StatusCode = (int)r.StatusCode };
        }
        catch { return Ok(new { nombre = "Política de Seguridad y Privacidad de la Información de Hotel Reservas", version = "1.0", nota = "servicio inteligente no disponible; ver docs/seguridad" }); }
    }
}
