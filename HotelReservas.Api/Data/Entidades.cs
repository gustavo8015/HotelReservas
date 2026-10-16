namespace HotelReservas.Api.Data;

// Entidades del dominio: una por tabla del esquema relacional (scripts 01 y 05 del repositorio)
// más CONSULTA_ASISTENTE (script 06), la tabla auxiliar del servicio de IA (sección 13.3.1).

public class Usuario
{
    public int IdUsuario { get; set; }
    public string Nombre { get; set; } = "";
    public string Correo { get; set; } = "";
    public string ClaveHash { get; set; } = "";
    public string Rol { get; set; } = "RECEPCIONISTA";
    public bool Activo { get; set; } = true;
}

public class Huesped
{
    public int IdHuesped { get; set; }
    public string TipoDocumento { get; set; } = "CC";
    public string NumeroDocumento { get; set; } = "";
    public string Nombres { get; set; } = "";
    public string Apellidos { get; set; } = "";
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public bool ExtranjeroNoResidente { get; set; }
    public List<Reserva> Reservas { get; set; } = new();
}

public class TipoHabitacion
{
    public int IdTipo { get; set; }
    public string Nombre { get; set; } = "";
    public int Capacidad { get; set; }
    public string? Descripcion { get; set; }
    public List<Habitacion> Habitaciones { get; set; } = new();
    public List<Tarifa> Tarifas { get; set; } = new();
}

public class Habitacion
{
    public int IdHabitacion { get; set; }
    public string Numero { get; set; } = "";
    public int Piso { get; set; }
    public int IdTipo { get; set; }
    public string Estado { get; set; } = "DISPONIBLE";
    public TipoHabitacion Tipo { get; set; } = null!;
}

public class Tarifa
{
    public int IdTarifa { get; set; }
    public int IdTipo { get; set; }
    public decimal ValorNoche { get; set; }
    public DateOnly VigenciaDesde { get; set; }
    public DateOnly? VigenciaHasta { get; set; }
}

public class Reserva
{
    public int IdReserva { get; set; }
    public string Codigo { get; set; } = "";
    public int IdHuesped { get; set; }
    public int IdHabitacion { get; set; }
    public int IdUsuario { get; set; }
    public DateOnly FechaEntrada { get; set; }
    public DateOnly FechaSalida { get; set; }
    public int NumeroPersonas { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TasaIva { get; set; }
    public decimal Impuesto { get; set; }
    public bool ExentoIva { get; set; }
    public decimal ValorTotal { get; set; }          // precio neto
    public string Estado { get; set; } = "CONFIRMADA";
    public DateTime FechaCreacion { get; set; }
    public byte[] VersionFila { get; set; } = Array.Empty<byte>();   // ROWVERSION: concurrencia optimista
    public Huesped Huesped { get; set; } = null!;
    public Habitacion Habitacion { get; set; } = null!;
    public List<Pago> Pagos { get; set; } = new();
    public Comprobante? Comprobante { get; set; }
    public List<BitacoraReserva> Bitacora { get; set; } = new();
}

public class Pago
{
    public int IdPago { get; set; }
    public int IdReserva { get; set; }
    public decimal Valor { get; set; }
    public string Medio { get; set; } = "";
    public DateTime Fecha { get; set; }
    public string Estado { get; set; } = "APROBADO";
}

public class Comprobante
{
    public int IdComprobante { get; set; }
    public int IdReserva { get; set; }
    public string Numero { get; set; } = "";
    public DateTime FechaEmision { get; set; }
    public decimal Valor { get; set; }
}

public class BitacoraReserva
{
    public int IdBitacora { get; set; }
    public int IdReserva { get; set; }
    public int IdUsuario { get; set; }
    public DateTime Fecha { get; set; }
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = "";
    public string? Detalle { get; set; }
    public Usuario Usuario { get; set; } = null!;
}

/// <summary>Registro de cada consulta hecha al asistente (trazabilidad del servicio de IA, RNF05).</summary>
public class ConsultaAsistente
{
    public long IdConsulta { get; set; }
    public int IdUsuario { get; set; }
    public DateTime Fecha { get; set; }
    public string TextoConsulta { get; set; } = "";
    public string? FuncionInvocada { get; set; }
    public string? Parametros { get; set; }
    public string Interprete { get; set; } = "";        // AZURE_OPENAI o REGLAS
    public bool Exitosa { get; set; }
    public int DuracionMs { get; set; }
}
