using System.Globalization;
using System.Text.Json;
using HotelReservas.Api.Data;
using HotelReservas.Api.Servicios;
using Microsoft.EntityFrameworkCore;

namespace HotelReservas.Api.Asistente;

/// <summary>
/// Catálogo de funciones de SOLO LECTURA que el asistente puede invocar (sección 13.3.3).
/// El modelo de IA nunca accede a la base de datos: elige una función y la API la ejecuta
/// reutilizando los mismos servicios de negocio de CU-03 y CU-12. Ninguna función devuelve
/// documentos de identidad ni datos de contacto de los huéspedes.
/// </summary>
public class CatalogoFunciones
{
    private readonly HotelReservasContext _db;
    private readonly ServicioReservas _reservas;
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");

    public CatalogoFunciones(HotelReservasContext db, ServicioReservas reservas) { _db = db; _reservas = reservas; }

    public static readonly IReadOnlyList<DescripcionFuncion> Descripciones = new List<DescripcionFuncion>
    {
        new("consultarDisponibilidad",
            "Lista las habitaciones de un tipo que están libres, reservadas o en mantenimiento para un intervalo de fechas, con la tarifa por noche y el precio neto (IVA 19 % incluido).",
            new { type = "object", properties = new {
                tipo = new { type = "string", @enum = new[] { "Sencilla", "Doble", "Suite" }, description = "Tipo de habitación" },
                entrada = new { type = "string", description = "Fecha de entrada en formato AAAA-MM-DD" },
                salida = new { type = "string", description = "Fecha de salida en formato AAAA-MM-DD (posterior a la entrada)" } },
                required = new[] { "tipo", "entrada", "salida" } }),
        new("consultarTarifa",
            "Devuelve la tarifa vigente por noche de un tipo de habitación y el desglose subtotal, IVA 19 % y precio neto para un número de noches.",
            new { type = "object", properties = new {
                tipo = new { type = "string", @enum = new[] { "Sencilla", "Doble", "Suite" } },
                noches = new { type = "integer", minimum = 1, description = "Número de noches; 1 si no se indica" },
                fecha = new { type = "string", description = "Fecha de referencia AAAA-MM-DD; hoy si no se indica" } },
                required = new[] { "tipo" } }),
        new("consultarReserva",
            "Devuelve el estado actual y el último movimiento de bitácora de una reserva identificada por su código (RSV seguido de nueve dígitos).",
            new { type = "object", properties = new { codigo = new { type = "string", description = "Código de la reserva, por ejemplo RSV000000045" } }, required = new[] { "codigo" } }),
        new("consultarLlegadas",
            "Cuenta y lista las reservas confirmadas cuya fecha de entrada es la fecha indicada (llegadas programadas).",
            new { type = "object", properties = new { fecha = new { type = "string", description = "Fecha AAAA-MM-DD; mañana si el usuario dice 'mañana'" } }, required = new[] { "fecha" } }),
        new("consultarReservasConfirmadas",
            "Cuenta las reservas por estado (confirmadas, en casa, finalizadas, canceladas) y la ocupación actual de las habitaciones.",
            new { type = "object", properties = new { } }),
    };

    public async Task<ResultadoFuncion> EjecutarAsync(string funcion, JsonElement? p, CancellationToken ct)
    {
        string S(string k, string? def = null) => p.HasValue && p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : def ?? "";
        int I(string k, int def) => int.TryParse(S(k), out var n) ? n : def;
        DateOnly F(string k, DateOnly def) => DateOnly.TryParse(S(k), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : def;
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        switch (funcion)
        {
            case "consultarDisponibilidad":
            {
                var tipo = NormalizarTipo(S("tipo", "Doble"));
                var entrada = F("entrada", hoy.AddDays(1)); var salida = F("salida", entrada.AddDays(1));
                var lista = await _reservas.ConsultarDisponibilidadAsync(tipo, entrada, salida);
                var libres = lista.Where(h => h.Disponible).ToList();
                var datos = new { tipo, entrada, salida, noches = lista.FirstOrDefault()?.Noches ?? 0,
                    tarifaNoche = lista.FirstOrDefault()?.TarifaNoche ?? 0, precioNeto = lista.FirstOrDefault()?.PrecioNeto ?? 0,
                    disponibles = libres.Select(h => h.Numero), noDisponibles = lista.Where(h => !h.Disponible).Select(h => new { h.Numero, h.Estado }) };
                var resumen = $"Tipo {tipo}, del {entrada:yyyy-MM-dd} al {salida:yyyy-MM-dd}: {libres.Count} disponibles ({string.Join(", ", libres.Select(h => h.Numero))}); " +
                              $"no disponibles: {string.Join(", ", lista.Where(h => !h.Disponible).Select(h => h.Numero + " (" + h.Estado.ToLower() + ")"))}. " +
                              $"Tarifa {Moneda(datos.tarifaNoche)} por noche; precio neto por {datos.noches} noche(s) {Moneda(datos.precioNeto)} (IVA 19 % incluido).";
                return new(funcion, datos, resumen);
            }
            case "consultarTarifa":
            {
                var tipo = NormalizarTipo(S("tipo", "Doble")); var noches = Math.Max(1, I("noches", 1)); var fecha = F("fecha", hoy);
                var t = await _db.TiposHabitacion.FirstOrDefaultAsync(x => x.Nombre == tipo, ct) ?? throw new ErrorNegocio(50004, "Tipo de habitación no válido.");
                var tarifa = await _reservas.TarifaVigenteAsync(t.IdTipo, fecha) ?? throw new ErrorNegocio(50003, "No hay tarifa vigente.");
                var subtotal = tarifa * noches; var iva = ServicioReservas.CalcularIva(subtotal, false);
                var datos = new { tipo, noches, tarifaNoche = tarifa, subtotal, iva, precioNeto = subtotal + iva, exentoExtranjeroNoResidente = subtotal };
                return new(funcion, datos, $"Tarifa vigente {tipo}: {Moneda(tarifa)} por noche. Por {noches} noche(s): subtotal {Moneda(subtotal)} + IVA 19 % {Moneda(iva)} = precio neto {Moneda(subtotal + iva)}. Huésped extranjero no residente: exento de IVA, pagaría {Moneda(subtotal)}.");
            }
            case "consultarReserva":
            {
                var codigo = S("codigo").ToUpperInvariant();
                var r = await _db.Reservas.Include(x => x.Habitacion).ThenInclude(h => h.Tipo).AsNoTracking().FirstOrDefaultAsync(x => x.Codigo == codigo, ct);
                if (r == null) return new(funcion, new { codigo, existe = false }, $"No existe ninguna reserva con el código {codigo}.");
                var ultimo = await _db.Bitacora.Where(b => b.IdReserva == r.IdReserva).OrderByDescending(b => b.Fecha).Select(b => new { b.Fecha, b.EstadoAnterior, b.EstadoNuevo }).FirstOrDefaultAsync(ct);
                var datos = new { codigo, existe = true, estado = r.Estado, habitacion = r.Habitacion.Numero, tipo = r.Habitacion.Tipo.Nombre, entrada = r.FechaEntrada, salida = r.FechaSalida, precioNeto = r.ValorTotal, ultimoMovimiento = ultimo };
                return new(funcion, datos, $"Reserva {codigo}: estado {r.Estado}, habitación {r.Habitacion.Numero} ({r.Habitacion.Tipo.Nombre}), del {r.FechaEntrada:yyyy-MM-dd} al {r.FechaSalida:yyyy-MM-dd}, precio neto {Moneda(r.ValorTotal)}. Último movimiento: {(ultimo == null ? "sin registros" : $"{ultimo.EstadoAnterior ?? "-"} → {ultimo.EstadoNuevo} el {ultimo.Fecha:yyyy-MM-dd HH:mm}")}.");
            }
            case "consultarLlegadas":
            {
                var fecha = F("fecha", hoy.AddDays(1));
                var llegadas = await _db.Reservas.Include(x => x.Habitacion).ThenInclude(h => h.Tipo).AsNoTracking()
                    .Where(x => x.Estado == "CONFIRMADA" && x.FechaEntrada == fecha).OrderBy(x => x.Habitacion.Numero)
                    .Select(x => new { x.Codigo, habitacion = x.Habitacion.Numero, tipo = x.Habitacion.Tipo.Nombre, x.NumeroPersonas, salida = x.FechaSalida }).ToListAsync(ct);
                return new(funcion, new { fecha, total = llegadas.Count, llegadas },
                    $"Para el {fecha:yyyy-MM-dd} hay {llegadas.Count} llegada(s) programada(s): {string.Join("; ", llegadas.Select(l => $"{l.Codigo} hab. {l.habitacion} ({l.tipo}, {l.NumeroPersonas} pers.)"))}.");
            }
            case "consultarReservasConfirmadas":
            {
                var porEstado = await _db.Reservas.GroupBy(x => x.Estado).Select(g => new { estado = g.Key, total = g.Count() }).ToListAsync(ct);
                var habs = await _db.Habitaciones.GroupBy(h => h.Estado).Select(g => new { estado = g.Key, total = g.Count() }).ToListAsync(ct);
                int N(string e) => porEstado.FirstOrDefault(x => x.estado == e)?.total ?? 0;
                return new(funcion, new { reservas = porEstado, habitaciones = habs },
                    $"Reservas: {N("CONFIRMADA")} confirmadas, {N("EN_CASA")} en casa, {N("FINALIZADA")} finalizadas, {N("CANCELADA")} canceladas. Habitaciones: {string.Join(", ", habs.Select(h => h.total + " " + h.estado.ToLower()))}.");
            }
            default:
                throw new ErrorNegocio(50020, $"La función '{funcion}' no está en el catálogo de solo lectura.");
        }
    }

    public static string NormalizarTipo(string t)
    {
        t = t.Trim().ToLowerInvariant();
        if (t.StartsWith("sen")) return "Sencilla";
        if (t.StartsWith("sui")) return "Suite";
        return "Doble";
    }

    public static string Moneda(decimal v) => "$ " + v.ToString("N0", Es);
}
