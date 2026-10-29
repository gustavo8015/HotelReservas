using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelReservas.Api.Controllers;

public record SolicitudPrediccionCancelacion(int AnticipacionDias, int Noches, int Personas, string Tipo, string MedioPago, bool HuespedFrecuente, bool FinDeSemana);
public record SolicitudPrediccionOcupacion(DateOnly Fecha, int ReservasAnticipadas);

/// <summary>
/// RF19 y RF21: puente entre la app y el servicio inteligente TensorFlow/FastAPI (carpeta servicio_inteligente).
/// La app nunca llama al servicio Python directamente: pasa por esta API, que exige el token JWT y
/// reenvía la solicitud a la URL configurada en ServicioInteligente:Url (por defecto http://127.0.0.1:8000).
/// </summary>
[ApiController]
[Route("api/prediccion")]
[Authorize]
public class PrediccionController : ControllerBase
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    public PrediccionController(IHttpClientFactory http, IConfiguration cfg) { _http = http; _cfg = cfg; }

    private HttpClient Cliente()
    {
        var c = _http.CreateClient();
        c.BaseAddress = new Uri((_cfg["ServicioInteligente:Url"] ?? "http://127.0.0.1:8000").TrimEnd('/') + "/");
        c.Timeout = TimeSpan.FromSeconds(10);
        return c;
    }

    private async Task<ActionResult> Reenviar(string ruta, object? cuerpo)
    {
        try
        {
            using var c = Cliente();
            var r = cuerpo == null ? await c.GetAsync(ruta) : await c.PostAsJsonAsync(ruta, cuerpo);
            var texto = await r.Content.ReadAsStringAsync();
            return new ContentResult { StatusCode = (int)r.StatusCode, Content = texto, ContentType = "application/json" };
        }
        catch (Exception ex) { return StatusCode(503, new { mensaje = "El servicio inteligente no está disponible: " + ex.Message }); }
    }

    /// <summary>GET /api/prediccion/estado: estado y métricas de validación de los modelos.</summary>
    [HttpGet("estado")]
    public Task<ActionResult> Estado() => Reenviar("metricas", null);

    /// <summary>POST /api/prediccion/cancelacion: probabilidad y nivel de riesgo de cancelación (RF19).</summary>
    [HttpPost("cancelacion")]
    public Task<ActionResult> Cancelacion([FromBody] SolicitudPrediccionCancelacion s) => Reenviar("predecir/cancelacion",
        new { anticipacion_dias = s.AnticipacionDias, noches = s.Noches, personas = s.Personas, tipo = s.Tipo, medio_pago = s.MedioPago, huesped_frecuente = s.HuespedFrecuente, fin_de_semana = s.FinDeSemana });

    /// <summary>POST /api/prediccion/ocupacion: habitaciones ocupadas previstas para una fecha (RF21).</summary>
    [HttpPost("ocupacion")]
    public Task<ActionResult> Ocupacion([FromBody] SolicitudPrediccionOcupacion s) => Reenviar("predecir/ocupacion",
        new { fecha = s.Fecha.ToString("yyyy-MM-dd"), reservas_anticipadas = s.ReservasAnticipadas });
}
