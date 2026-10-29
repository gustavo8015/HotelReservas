using System.Net;
using System.Net.Mail;

namespace HotelReservas.Api.Servicios;

/// <summary>
/// Envío de la confirmación de reserva por correo electrónico (RF20) desde la cuenta del hotel
/// (hotelreservas2026@gmail.com) mediante SMTP de Gmail con STARTTLS en el puerto 587.
/// La clave de aplicación NO va en el código ni en el repositorio: se lee de la configuración
/// (dotnet user-secrets o variable de entorno Correo__ClaveAplicacion).
/// </summary>
public class ServicioCorreo
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<ServicioCorreo> _log;
    public ServicioCorreo(IConfiguration cfg, ILogger<ServicioCorreo> log) { _cfg = cfg; _log = log; }

    public string Remitente => _cfg["Correo:Usuario"] ?? "hotelreservas2026@gmail.com";
    public bool Configurado => !string.IsNullOrWhiteSpace(_cfg["Correo:ClaveAplicacion"]);

    public async Task EnviarAsync(string destino, string asunto, string cuerpo)
    {
        if (!Configurado) throw new InvalidOperationException("Falta Correo:ClaveAplicacion (clave de aplicación de Gmail).");
        using var cliente = new SmtpClient(_cfg["Correo:Smtp"] ?? "smtp.gmail.com", _cfg.GetValue<int>("Correo:Puerto", 587))
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(Remitente, _cfg["Correo:ClaveAplicacion"]),
        };
        using var msg = new MailMessage(new MailAddress(Remitente, _cfg["Correo:NombreRemitente"] ?? "Hotel Reservas"), new MailAddress(destino))
        {
            Subject = asunto,
            Body = cuerpo,
            IsBodyHtml = false,
        };
        msg.Bcc.Add(Remitente);   // copia para el hotel
        await cliente.SendMailAsync(msg);
        _log.LogInformation("Correo de confirmación enviado a {Destino} desde {Remitente}", destino, Remitente);
    }
}
