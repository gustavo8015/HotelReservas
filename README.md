# Hotel Reservas

**Sistema Transaccional Móvil (APK) para la Gestión de Reservas de Habitaciones en un Hotel**

Universidad Manuela Beltrán · Ingeniería de Sistemas · Asignatura: Sistemas Transaccionales (7.° semestre)
Docente: Jamilton Fernando Benvides
Integrantes: Gustavo Adolfo Cardona Bayona · Jonathan Soler Guevara · Hugo Andrés Forero Angel

---

## Propósito general

Diseñar y construir un sistema transaccional, implementado como aplicación móvil Android (APK), que permita al personal de un hotel registrar huéspedes y habitaciones, consultar disponibilidad en tiempo real, realizar reservas, generar comprobantes y consultar reportes de ocupación, garantizando la integridad, la consistencia y la seguridad de la información.

La transacción crítica del sistema es **confirmar reserva**: verificar disponibilidad, bloquear la habitación para el rango de fechas, registrar el huésped y el pago, y emitir el comprobante, todo como una única unidad de trabajo con propiedades ACID. Si alguna parte falla, no queda ningún cambio a medias.

## Estado actual: Entrega S8 (análisis y diseño)

| Entrega | Contenido | Estado |
|---|---|---|
| S7 | Repositorio, primera interfaz (.NET MAUI) y navegación inicial | Entregada |
| **S8** | **Fase II: información general, objetivos, arquitectura, casos de uso, diagrama de clases y modelo relacional; scripts SQL del modelo y de la transacción** | **Actual** |
| Siguiente | API REST en ASP.NET Core con transacciones (BEGIN TRAN / COMMIT / ROLLBACK) | Pendiente |
| Siguiente | Conexión de la app a la API, CRUD de huéspedes, habitaciones y reservas | Pendiente |
| Final | Demostración de concurrencia (dos usuarios, última habitación) y pruebas | Pendiente |

### Qué se agregó en esta entrega

- `docs/FaseII_Documentacion_Proyecto_S8_Hotel_Reservas.pdf` (y `.docx`): documento de análisis y diseño con las diez secciones de la guía, en formato APA 7. Incorpora la retroalimentación del docente sobre la Fase I: la contextualización del problema (sección 1) se presenta sin decisiones técnicas, se delimita el alcance de cada fase (1.6) y las decisiones de arquitectura y tecnología se justifican en la sección 4.
- `docs/diagramas/`: diagrama de arquitectura en capas, casos de uso UML, diagrama de clases UML y modelo relacional (PNG + fuente PlantUML).
- `database/`: scripts T-SQL para SQL Server: creación de las 9 tablas, procedimiento almacenado de la transacción **confirmar reserva** y datos de prueba.

## Arquitectura (en capas, cliente-servidor)

<p align="center">
  <img src="docs/diagramas/arquitectura-capas.png" width="420" alt="Arquitectura en capas">
</p>

| Capa | Responsabilidad | Tecnología |
|---|---|---|
| Presentación | Aplicación móvil Android (APK), patrón MVVM | C# · .NET MAUI |
| Lógica de negocio | Servicios REST (JSON), reglas de negocio, coordinación de la transacción, autenticación JWT | C# · ASP.NET Core Web API |
| Acceso a datos | Repositorios, mapeo objeto-relacional, transacciones T-SQL | Entity Framework Core / ADO.NET |
| Datos | Persistencia, integridad referencial, bloqueos y aislamiento | Microsoft SQL Server (Express/Developer) + SSMS |

La app nunca accede a la base de datos directamente: toda operación pasa por la API sobre HTTPS. Si dos recepcionistas intentan reservar la última habitación al mismo tiempo, la base de datos serializa las dos transacciones (bloqueo `UPDLOCK, HOLDLOCK` + nivel de aislamiento `SERIALIZABLE`) y la API responde `HTTP 409 Conflict` a la segunda.

## Análisis y diseño (Fase II)

| Diagrama | Archivo | Descripción |
|---|---|---|
| Casos de uso UML | [`docs/diagramas/casos-de-uso.png`](docs/diagramas/casos-de-uso.png) | Actores Recepcionista y Administrador; casos CU-01 a CU-15 con relaciones `include`/`extend` |
| Diagrama de clases UML | [`docs/diagramas/diagrama-clases.png`](docs/diagramas/diagrama-clases.png) | Usuario (Recepcionista, Administrador), Huesped, TipoHabitacion, Habitacion, Tarifa, Reserva, Pago, Comprobante, BitacoraReserva, ServicioReservas |
| Modelo relacional | [`docs/diagramas/modelo-relacional.png`](docs/diagramas/modelo-relacional.png) | 9 tablas: USUARIO, HUESPED, TIPO_HABITACION, HABITACION, TARIFA, RESERVA, PAGO, COMPROBANTE, BITACORA_RESERVA |

<p align="center">
  <img src="docs/diagramas/modelo-relacional.png" width="640" alt="Modelo relacional">
</p>

## Base de datos (SQL Server)

Los scripts se ejecutan en orden desde SQL Server Management Studio (SSMS):

| Script | Contenido |
|---|---|
| `database/01_crear_base_de_datos.sql` | Crea la base `HotelReservas` y las 9 tablas con claves primarias, foráneas, restricciones `CHECK`, columna `ROWVERSION` en RESERVA e índice para la consulta de disponibilidad |
| `database/02_transaccion_confirmar_reserva.sql` | Procedimiento `sp_ConfirmarReserva`: transacción ACID con `SERIALIZABLE`, `UPDLOCK/HOLDLOCK`, `TRY/CATCH`, `ROLLBACK` y errores `THROW` (50001 = habitación no disponible) |
| `database/03_datos_de_prueba.sql` | Usuarios, tipos, tarifas, 25 habitaciones y huéspedes de ejemplo |

Núcleo de la transacción crítica:

```sql
BEGIN TRANSACTION;
  IF EXISTS (SELECT 1 FROM RESERVA WITH (UPDLOCK, HOLDLOCK)
             WHERE id_habitacion = @hab
               AND estado IN ('CONFIRMADA','EN_CASA')
               AND fecha_entrada < @salida AND fecha_salida > @entrada)
    BEGIN ROLLBACK TRANSACTION; THROW 50001, 'Habitación no disponible', 1; END
  INSERT INTO RESERVA (...);  INSERT INTO PAGO (...);
  INSERT INTO COMPROBANTE (...);  INSERT INTO BITACORA_RESERVA (...);
COMMIT TRANSACTION;
```

## Aplicación móvil (entrega S7)

<p align="center">
  <img src="docs/pantalla-inicio.png" width="300" alt="Pantalla de inicio de Hotel Reservas">
</p>

La pantalla de inicio presenta el nombre y propósito del sistema, los módulos Reservas, Habitaciones, Huéspedes y Reportes, el resumen "Estado de hoy" (datos de ejemplo) y la navegación por pestañas (Shell TabBar). Todavía no está conectada a la API ni a la base de datos.

## Estructura del repositorio

```
HotelReservas/
├── HotelReservas.sln
├── HotelReservas/                    Proyecto .NET MAUI (net8.0-android)
│   ├── MauiProgram.cs
│   ├── App.xaml / AppShell.xaml      Arranque y navegación (TabBar, 5 rutas)
│   ├── Views/                        InicioPage y módulos en construcción
│   ├── Resources/                    Estilos, íconos, AppIcon, Splash
│   └── Platforms/Android/            MainActivity, MainApplication, AndroidManifest
├── database/
│   ├── 01_crear_base_de_datos.sql
│   ├── 02_transaccion_confirmar_reserva.sql
│   └── 03_datos_de_prueba.sql
├── docs/
│   ├── FaseI_Documentacion_Proyecto_S7_Hotel_Reservas.pdf
│   ├── FaseII_Documentacion_Proyecto_S8_Hotel_Reservas.pdf / .docx
│   ├── pantalla-inicio.png
│   └── diagramas/                    PNG + fuente .puml (PlantUML)
└── README.md
```

## Cómo ejecutar

**Base de datos**

1. Instalar SQL Server 2025 Express o Developer (gratuitos) y SQL Server Management Studio.
2. Conectarse a la instancia (`localhost` o `.\SQLEXPRESS`).
3. Abrir y ejecutar, en orden, `01_crear_base_de_datos.sql`, `02_transaccion_confirmar_reserva.sql` y `03_datos_de_prueba.sql`.
4. Probar la transacción con el ejemplo incluido al final del script 02. Para la prueba de concurrencia, ejecutar la misma llamada en dos ventanas de consulta al mismo tiempo: solo una confirma.

**Aplicación móvil**

1. Instalar Visual Studio 2022 (17.8 o superior) con la carga de trabajo **.NET Multi-platform App UI** y el SDK de Android.
2. Abrir `HotelReservas.sln` y seleccionar un emulador o dispositivo Android.
3. Ejecutar (F5). Para generar el APK:

```bash
dotnet build -f net8.0-android -c Release
```

## Documentos relacionados

- Ficha técnica del microproyecto (Sistemas Transaccionales).
- `docs/FaseI_Documentacion_Proyecto_S7_Hotel_Reservas.pdf`: contexto, problemática, necesidad y pregunta problema.
- `docs/FaseII_Documentacion_Proyecto_S8_Hotel_Reservas.pdf`: análisis y diseño (arquitectura, UML y modelo relacional).
