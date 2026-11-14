namespace HotelReservas.Api.Data;

/// <summary>Fila de AUDITORIA_SEGURIDAD (script 09).</summary>
public class EventoSeguridad
{
    public long IdEvento { get; set; }
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = "";
    public int? IdUsuario { get; set; }
    public string? Correo { get; set; }
    public int? IdHuesped { get; set; }
    public string? Detalle { get; set; }
    public string? DireccionIp { get; set; }
    public string Estado { get; set; } = "REGISTRADO";
}

/// <summary>Fila de SOLICITUD_HABEAS_DATA (script 09): consulta, rectificación, revocatoria o supresión pedida por el titular.</summary>
public class SolicitudHabeasData
{
    public int IdSolicitud { get; set; }
    public int IdHuesped { get; set; }
    public string Tipo { get; set; } = "CONSULTA";
    public DateTime FechaSolicitud { get; set; }
    public string? Detalle { get; set; }
    public string Estado { get; set; } = "EN_TRAMITE";
    public DateTime? FechaRespuesta { get; set; }
    public string? Respuesta { get; set; }
    public int? IdUsuarioResuelve { get; set; }
}
