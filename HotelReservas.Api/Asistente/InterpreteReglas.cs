using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HotelReservas.Api.Asistente;

/// <summary>
/// Intérprete de respaldo basado en reglas (expresiones regulares). Se usa cuando no hay
/// credenciales de Azure OpenAI configuradas o el servicio no responde, de modo que la pantalla
/// Asistente funciona en la demostración sin internet. Cubre las mismas funciones del catálogo.
/// </summary>
public class InterpreteReglas : IInterpreteConsultas
{
    public string Nombre => "REGLAS";
    public bool Disponible => true;

    private static readonly string[] Meses = { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

    public Task<Interpretacion> InterpretarAsync(string consulta, IReadOnlyList<DescripcionFuncion> catalogo, CancellationToken ct)
    {
        var s = Normalizar(consulta);
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        if (Regex.IsMatch(s, @"\b(reserv(a|e|ar|ame|eme)\s+la|crea|confirma|modifica|cancela|registra|check\s*-?\s*in|check\s*-?\s*out|paga)\b") && !Regex.IsMatch(s, @"\bcuant"))
            return Task.FromResult(Interpretacion.Aclaracion("No puedo crear, modificar ni cancelar reservas desde el asistente. Para confirmarla con verificación de disponibilidad y comprobante, use la pantalla Reservas."));

        var codigo = Regex.Match(s, @"rsv\s*\d{6,9}");
        if (codigo.Success)
            return Task.FromResult(Llamar("consultarReserva", new { codigo = Regex.Replace(codigo.Value, @"\s", "").ToUpperInvariant() }));

        var tipo = s.Contains("suite") ? "Suite" : s.Contains("sencill") ? "Sencilla" : s.Contains("doble") ? "Doble" : null;

        if (Regex.IsMatch(s, @"\b(llegada|llegan|entran|check\s*-?\s*in)"))
        {
            var fecha = s.Contains("manana") ? hoy.AddDays(1) : s.Contains("hoy") ? hoy : FechaUnica(s, hoy) ?? hoy.AddDays(1);
            return Task.FromResult(Llamar("consultarLlegadas", new { fecha = fecha.ToString("yyyy-MM-dd") }));
        }

        if (Regex.IsMatch(s, @"\bcuant[oa]s?\b.*\b(reserva|confirmad|ocupad|en casa)") || Regex.IsMatch(s, @"\b(ocupacion|resumen|estado del hotel)\b"))
            return Task.FromResult(Llamar("consultarReservasConfirmadas", new { }));

        if (Regex.IsMatch(s, @"\b(cuanto|precio|cuesta|tarifa|vale|valor|costo|costaria)\b"))
        {
            if (tipo == null) return Task.FromResult(Interpretacion.Aclaracion("¿Para qué tipo de habitación (Sencilla, Doble o Suite) y cuántas noches?"));
            var noches = Regex.Match(s, @"(\d{1,2})\s*noche");
            return Task.FromResult(Llamar("consultarTarifa", new { tipo, noches = noches.Success ? int.Parse(noches.Groups[1].Value) : 1 }));
        }

        if (Regex.IsMatch(s, @"\b(libre|disponib|hay|quedan|tienen|habitacion)"))
        {
            if (tipo == null) return Task.FromResult(Interpretacion.Aclaracion("¿Qué tipo de habitación necesita (Sencilla, Doble o Suite) y para qué fechas?"));
            var rango = Rango(s, hoy);
            if (rango == null) return Task.FromResult(Interpretacion.Aclaracion($"¿Para qué fechas necesita la habitación {tipo.ToLower()}? Por ejemplo: del 16 al 18 de octubre."));
            return Task.FromResult(Llamar("consultarDisponibilidad", new { tipo, entrada = rango.Value.entrada.ToString("yyyy-MM-dd"), salida = rango.Value.salida.ToString("yyyy-MM-dd") }));
        }

        return Task.FromResult(Interpretacion.Aclaracion("Puedo consultar disponibilidad, tarifas, el estado de una reserva y las llegadas del día. ¿Podría indicarme el tipo de habitación y las fechas? Por ejemplo: \"¿qué dobles hay libres del 16 al 18?\""));
    }

    public Task<string> RedactarAsync(string consulta, ResultadoFuncion resultado, CancellationToken ct) => Task.FromResult(resultado.ResumenParaModelo);

    private static Interpretacion Llamar(string funcion, object parametros) =>
        new(funcion, JsonSerializer.SerializeToElement(parametros), null);

    private static string Normalizar(string t)
    {
        var d = t.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var ch in d) if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString();
    }

    private static int Mes(string s, int def)
    {
        for (var i = 0; i < Meses.Length; i++) if (s.Contains(Meses[i])) return i + 1;
        return def;
    }

    /// <summary>"del 16 al 18 de octubre", "16-18 oct", "2026-10-16 a 2026-10-18"</summary>
    private static (DateOnly entrada, DateOnly salida)? Rango(string s, DateOnly hoy)
    {
        var iso = Regex.Matches(s, @"\d{4}-\d{2}-\d{2}");
        if (iso.Count >= 2 && DateOnly.TryParse(iso[0].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var a) && DateOnly.TryParse(iso[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var b))
            return (a, b);
        var m = Regex.Match(s, @"\b(\d{1,2})\s*(?:al|a|-|hasta el)\s*(\d{1,2})\b");
        if (m.Success)
        {
            var mes = Mes(s, hoy.Month); var anio = hoy.Year;
            var d1 = int.Parse(m.Groups[1].Value); var d2 = int.Parse(m.Groups[2].Value);
            if (d1 < 1 || d1 > DateTime.DaysInMonth(anio, mes) || d2 < 1) return null;
            var entrada = new DateOnly(anio, mes, d1);
            DateOnly salida;
            if (d2 > d1) salida = new DateOnly(anio, mes, Math.Min(d2, DateTime.DaysInMonth(anio, mes)));
            else { var sig = entrada.AddMonths(1); salida = new DateOnly(sig.Year, sig.Month, Math.Min(d2, DateTime.DaysInMonth(sig.Year, sig.Month))); }   // "del 30 al 2": cruza de mes
            if (entrada < hoy.AddMonths(-1)) { entrada = entrada.AddYears(1); salida = salida.AddYears(1); }
            if (salida <= entrada) salida = entrada.AddDays(1);
            return (entrada, salida);
        }
        if (s.Contains("fin de semana"))
        {
            var dias = ((int)DayOfWeek.Friday - (int)hoy.DayOfWeek + 7) % 7; if (dias == 0) dias = 7;
            var v = hoy.AddDays(dias); return (v, v.AddDays(2));
        }
        if (s.Contains("manana")) return (hoy.AddDays(1), hoy.AddDays(2));
        if (s.Contains("hoy") || s.Contains("esta noche")) return (hoy, hoy.AddDays(1));
        return null;
    }

    private static DateOnly? FechaUnica(string s, DateOnly hoy)
    {
        var iso = Regex.Match(s, @"\d{4}-\d{2}-\d{2}");
        if (iso.Success && DateOnly.TryParse(iso.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        var m = Regex.Match(s, @"\b(\d{1,2})\s+de\s+([a-z]+)");
        if (m.Success) { var mes = Mes(m.Groups[2].Value, hoy.Month); var dia = int.Parse(m.Groups[1].Value); if (dia >= 1 && dia <= DateTime.DaysInMonth(hoy.Year, mes)) return new DateOnly(hoy.Year, mes, dia); }
        return null;
    }
}
