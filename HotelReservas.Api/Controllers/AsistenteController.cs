using HotelReservas.Api.Asistente;
using HotelReservas.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelReservas.Api.Controllers;

/// <summary>Recurso del servicio de IA (sección 13). Solo lectura: nunca crea, modifica ni cancela reservas.</summary>
[ApiController]
[Route("api/asistente")]
[Authorize]
public class AsistenteController : ControllerBase
{
    private readonly OrquestadorAsistente _orq;
    public AsistenteController(OrquestadorAsistente orq) => _orq = orq;

    /// <summary>POST /api/asistente  { "consulta": "¿qué dobles hay libres del 16 al 18 de octubre?" }</summary>
    [HttpPost]
    public async Task<ActionResult<RespuestaAsistente>> Consultar([FromBody] SolicitudAsistente s, CancellationToken ct)
        => Ok(await _orq.ProcesarAsync(s.Consulta, User.IdUsuario(), ct));

    /// <summary>GET /api/asistente/funciones: catálogo de funciones de lectura que el modelo puede invocar.</summary>
    [HttpGet("funciones")]
    public ActionResult<IEnumerable<DescripcionFuncion>> Funciones() => Ok(CatalogoFunciones.Descripciones);

    /// <summary>GET /api/asistente/estado: qué intérprete está activo (Azure OpenAI o reglas).</summary>
    [HttpGet("estado")]
    public ActionResult Estado([FromServices] InterpreteAzureOpenAI ia)
        => Ok(new { interprete = ia.Disponible ? ia.Nombre : "REGLAS", azureOpenAIConfigurado = ia.Disponible, funciones = CatalogoFunciones.Descripciones.Count });
}
