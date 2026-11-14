using System.Text.RegularExpressions;
using HotelReservas.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelReservas.Api.Seguridad;

/// <summary>
/// Política de contraseñas (RNF01; Ley 1266 de 2008 art. 4 principio de seguridad; Ley 1581 de 2012 art. 4 lit. g):
/// mínimo 8 caracteres con mayúscula, minúscula, número y símbolo, sin espacios y distinta de las claves comunes.
/// </summary>
public static class PoliticaContrasenas
{
    private static readonly HashSet<string> Comunes = new(StringComparer.OrdinalIgnoreCase) { "12345678", "password", "contraseña", "hotel2026", "qwerty123", "admin123" };
    public static (bool cumple, List<string> faltas) Evaluar(string clave)
    {
        var faltas = new List<string>();
        if (clave.Length < 8) faltas.Add("al menos 8 caracteres");
        if (!Regex.IsMatch(clave, "[A-ZÁÉÍÓÚÑ]")) faltas.Add("una mayúscula");
        if (!Regex.IsMatch(clave, "[a-záéíóúñ]")) faltas.Add("una minúscula");
        if (!Regex.IsMatch(clave, "[0-9]")) faltas.Add("un número");
        if (!Regex.IsMatch(clave, @"[^\w\s]")) faltas.Add("un símbolo");
        if (clave.Contains(' ')) faltas.Add("sin espacios");
        if (Comunes.Contains(clave)) faltas.Add("no usar una clave común");
        return (faltas.Count == 0, faltas);
    }
}

/// <summary>
/// Control de intentos de acceso (Ley 1273 de 2009 art. 269A; política numeral 8): tras 5 intentos fallidos la cuenta queda
/// bloqueada 15 minutos. El estado se guarda en USUARIO (intentos_fallidos, bloqueado_hasta) para que sobreviva a reinicios.
/// </summary>
public class ControlIntentos
{
    public const int MaxIntentos = 5;
    public static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(15);
    private readonly HotelReservasContext _db;
    public ControlIntentos(HotelReservasContext db) => _db = db;

    public bool EstaBloqueado(Usuario u) => u.BloqueadoHasta.HasValue && u.BloqueadoHasta.Value > DateTime.Now;

    /// <summary>Registra un intento fallido; devuelve true si con este intento la cuenta quedó bloqueada.</summary>
    public async Task<bool> FalloAsync(Usuario u, CancellationToken ct)
    {
        var fila = await _db.Usuarios.FirstAsync(x => x.IdUsuario == u.IdUsuario, ct);
        fila.IntentosFallidos = (byte)Math.Min(255, fila.IntentosFallidos + 1);
        var bloqueado = false;
        if (fila.IntentosFallidos >= MaxIntentos) { fila.BloqueadoHasta = DateTime.Now.Add(Bloqueo); fila.IntentosFallidos = 0; bloqueado = true; }
        await _db.SaveChangesAsync(ct);
        return bloqueado;
    }

    public async Task ExitoAsync(Usuario u, CancellationToken ct)
    {
        var fila = await _db.Usuarios.FirstAsync(x => x.IdUsuario == u.IdUsuario, ct);
        fila.IntentosFallidos = 0; fila.BloqueadoHasta = null; fila.UltimoAcceso = DateTime.Now;
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Auditoría de seguridad (tabla AUDITORIA_SEGURIDAD, script 09). Nunca guarda documentos, teléfonos ni correos de huéspedes:
/// el detalle pasa por el mismo filtro de datos personales del asistente.
/// </summary>
public class AuditoriaSeguridad
{
    private readonly HotelReservasContext _db;
    private readonly IHttpContextAccessor _http;
    public AuditoriaSeguridad(HotelReservasContext db, IHttpContextAccessor http) { _db = db; _http = http; }

    public async Task RegistrarAsync(string tipo, int? idUsuario = null, int? idHuesped = null, string? detalle = null, string? correo = null, CancellationToken ct = default)
    {
        _db.EventosSeguridad.Add(new EventoSeguridad
        {
            Fecha = DateTime.Now, Tipo = tipo, IdUsuario = idUsuario, IdHuesped = idHuesped,
            Correo = correo == null ? null : Enmascarar.Correo(correo),
            Detalle = detalle == null ? null : Asistente.FiltroDatosPersonales.Aplicar(detalle)[..Math.Min(400, Asistente.FiltroDatosPersonales.Aplicar(detalle).Length)],
            DireccionIp = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
        });
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>Enmascaramiento para las listas del personal (principio de circulación restringida).</summary>
public static class Enmascarar
{
    public static string Documento(string d) => d.Length > 4 ? new string('*', d.Length - 4) + d[^4..] : d;
    public static string? Telefono(string? t) => string.IsNullOrEmpty(t) ? t : "***" + t[^Math.Min(4, t.Length)..];
    public static string? Correo(string? c) { if (string.IsNullOrEmpty(c)) return c; var i = c.IndexOf('@'); return i < 0 ? "***" : c[..Math.Min(2, i)] + "***" + c[i..]; }
}

/// <summary>Cabeceras de seguridad HTTP para todas las respuestas (política numeral 8: controles técnicos).</summary>
public class CabecerasSeguridad
{
    private readonly RequestDelegate _next;
    public CabecerasSeguridad(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["X-Frame-Options"] = "DENY";
        ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
        ctx.Response.Headers["Cache-Control"] = "no-store";
        if (ctx.Request.IsHttps) ctx.Response.Headers["Strict-Transport-Security"] = "max-age=31536000";
        await _next(ctx);
    }
}
