using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelReservas.Api.Data;
using HotelReservas.Api.Servicios;

namespace HotelReservas.Api.Asistente;

/// <summary>Filtro de datos personales: lo que sale hacia el servicio externo no lleva documentos, teléfonos ni correos (Ley 1581 de 2012).</summary>
public static class FiltroDatosPersonales
{
    public static string Aplicar(string texto)
    {
        texto = Regex.Replace(texto, @"[\w.+-]+@[\w-]+\.[\w.]+", "[correo]");
        texto = Regex.Replace(texto, @"\b(?:cc|ce|ti|pa|nit|pasaporte|cedula|cédula)\s*[:#]?\s*[a-z]{0,2}\d{5,}\b", "[documento]", RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto, @"(?<![\d-])(?:\+?57\s?)?3\d{2}[\s-]?\d{3}[\s-]?\d{4}(?!\d)", "[telefono]");   // celulares colombianos
        texto = Regex.Replace(texto, @"(?<!rsv)(?<![\d-])\d{7,12}(?![\d-])", "[numero]", RegexOptions.IgnoreCase);     // documentos sueltos (no códigos RSV ni fechas)
        return texto;
    }
}

/// <summary>
/// Lógica de integración (sección 13.3.3): recibe la consulta con el usuario autenticado, aplica el filtro de
/// datos personales, pide al intérprete (modelo de IA o reglas) la función a ejecutar, valida que pertenezca
/// al catálogo de solo lectura, la ejecuta sobre el repositorio central, registra la consulta en
/// CONSULTA_ASISTENTE y devuelve la respuesta redactada junto con los datos estructurados.
/// </summary>
public class OrquestadorAsistente
{
    private readonly CatalogoFunciones _catalogo;
    private readonly InterpreteAzureOpenAI _ia;
    private readonly InterpreteReglas _reglas;
    private readonly HotelReservasContext _db;
    private readonly ILogger<OrquestadorAsistente> _log;

    public OrquestadorAsistente(CatalogoFunciones catalogo, InterpreteAzureOpenAI ia, InterpreteReglas reglas, HotelReservasContext db, ILogger<OrquestadorAsistente> log)
    { _catalogo = catalogo; _ia = ia; _reglas = reglas; _db = db; _log = log; }

    public async Task<RespuestaAsistente> ProcesarAsync(string consulta, int idUsuario, CancellationToken ct)
    {
        var reloj = Stopwatch.StartNew();
        consulta = (consulta ?? "").Trim();
        if (consulta.Length == 0) return new RespuestaAsistente("Escriba su consulta.", null, null, "NINGUNO", false, 0);
        if (consulta.Length > 500) consulta = consulta[..500];

        var consultaFiltrada = FiltroDatosPersonales.Aplicar(consulta);
        IInterpreteConsultas interprete = _ia.Disponible ? _ia : _reglas;
        Interpretacion interp;
        try
        {
            interp = await interprete.InterpretarAsync(consultaFiltrada, CatalogoFunciones.Descripciones, ct);
        }
        catch (Exception ex) when (interprete == _ia)
        {
            _log.LogWarning(ex, "Azure OpenAI no disponible; se usa el intérprete de reglas.");
            interprete = _reglas;
            interp = await _reglas.InterpretarAsync(consultaFiltrada, CatalogoFunciones.Descripciones, ct);
        }

        string? funcion = interp.Funcion; string respuesta; bool exitosa = true; object? datos = null;
        if (funcion == null)
        {
            respuesta = interp.MensajeDirecto ?? "¿Podría precisar su consulta?";
        }
        else if (CatalogoFunciones.Descripciones.All(d => d.Nombre != funcion))
        {
            // Regla 1: solo funciones de lectura del catálogo. Cualquier otra cosa se rechaza.
            _log.LogWarning("El intérprete devolvió una función fuera del catálogo: {Funcion}", funcion);
            respuesta = "Esa operación no está disponible desde el asistente. Use la pantalla Reservas.";
            exitosa = false;
        }
        else
        {
            try
            {
                var resultado = await _catalogo.EjecutarAsync(funcion, interp.Parametros, ct);
                datos = resultado.Datos;
                try { respuesta = await interprete.RedactarAsync(consultaFiltrada, resultado, ct); }
                catch (Exception ex) { _log.LogWarning(ex, "Fallo al redactar con IA; se devuelve el resumen de la función."); respuesta = resultado.ResumenParaModelo; }
            }
            catch (ErrorNegocio ex) { respuesta = ex.Message; exitosa = false; }
        }

        reloj.Stop();
        _db.ConsultasAsistente.Add(new ConsultaAsistente
        {
            IdUsuario = idUsuario, Fecha = DateTime.Now, TextoConsulta = consultaFiltrada, FuncionInvocada = funcion,
            Parametros = interp.Parametros?.GetRawText(), Interprete = interprete.Nombre, Exitosa = exitosa, DuracionMs = (int)reloj.ElapsedMilliseconds,
        });
        await _db.SaveChangesAsync(ct);

        JsonElement? datosJson = datos == null ? null : JsonSerializer.SerializeToElement(datos);
        return new RespuestaAsistente(respuesta, funcion, datosJson, interprete.Nombre, exitosa, (int)reloj.ElapsedMilliseconds);
    }
}
