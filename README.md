# Hotel Reservas

**Sistema Transaccional Móvil (APK) para la Gestión de Reservas de Habitaciones en un Hotel**

Universidad Manuela Beltrán · Ingeniería de Sistemas · Asignatura: Sistemas Transaccionales
Estudiante: Gustavo Adolfo Cardona Bayona

---

## Propósito general

Diseñar y construir un sistema transaccional, implementado como aplicación móvil Android (APK), que permita al personal de un hotel registrar huéspedes y habitaciones, consultar disponibilidad en tiempo real, realizar reservas, generar comprobantes y consultar reportes de ocupación, garantizando la integridad, la consistencia y la seguridad de la información.

La transacción crítica del sistema es **confirmar reserva**: verificar disponibilidad, bloquear la habitación para el rango de fechas, registrar el huésped y el pago, y emitir el comprobante, todo como una única unidad de trabajo con propiedades ACID. Si alguna parte falla, no queda ningún cambio a medias.

## Estado actual: Entrega S7 (primera interfaz)

Esta versión corresponde a la **primera propuesta visual** del sistema. Contiene el repositorio, la pantalla de inicio y la propuesta inicial de navegación. **No incluye** base de datos, backend, CRUD, API ni lógica transaccional; estos elementos se desarrollarán en las siguientes entregas.

<p align="center">
  <img src="docs/pantalla-inicio.png" width="330" alt="Pantalla de inicio de Hotel Reservas">
</p>

La pantalla de inicio permite identificar:

- **Nombre del sistema:** Hotel Reservas.
- **Propósito general:** descrito en la tarjeta "Propósito del sistema" y la transacción crítica.
- **Elementos principales de la solución:** los módulos Reservas, Habitaciones, Huéspedes y Reportes, más el resumen "Estado de hoy" (datos de ejemplo).
- **Propuesta inicial de navegación:** barra de pestañas inferior (Shell TabBar) con los cinco destinos, tarjetas de módulo que navegan por ruta y los botones "Nueva reserva" y "Consultar disponibilidad".

## Tecnologías definidas para el proyecto

| Capa | Tecnología |
|---|---|
| Aplicación móvil (APK) | C# con .NET MAUI (Visual Studio 2022), compilada para Android |
| Lógica de negocio | C# con ASP.NET Core Web API (servicios REST en JSON) |
| Acceso a datos y transacciones | Entity Framework Core / ADO.NET con transacciones T-SQL |
| Base de datos | Microsoft SQL Server Express, administrada con SQL Server Management Studio (SSMS) |
| Apoyo | Git y GitHub, Postman |

## Arquitectura (en capas)

```
Usuario ⇄ App móvil (APK) ⇄ Lógica de negocio ⇄ Gestión de transacciones ⇄ Acceso a datos ⇄ Base de datos
```

En esta entrega solo existe la capa de interfaz de usuario. Las demás capas se documentan en la ficha técnica y en la documentación del proyecto y se implementarán progresivamente.

## Estructura del repositorio

```
HotelReservas/
├── HotelReservas.sln
├── HotelReservas/
│   ├── HotelReservas.csproj          Proyecto .NET MAUI (net8.0-android)
│   ├── MauiProgram.cs
│   ├── App.xaml / App.xaml.cs        Recursos y arranque
│   ├── AppShell.xaml / .cs           Navegación: TabBar con 5 rutas
│   ├── Views/
│   │   ├── InicioPage.xaml / .cs     Pantalla principal (esta entrega)
│   │   ├── ReservasPage.xaml / .cs   Módulo (en construcción)
│   │   ├── HabitacionesPage.xaml     Módulo (en construcción)
│   │   ├── HuespedesPage.xaml        Módulo (en construcción)
│   │   └── ReportesPage.xaml         Módulo (en construcción)
│   ├── Resources/
│   │   ├── Styles/Colors.xaml        Paleta institucional
│   │   ├── Styles/Styles.xaml        Estilos de tarjetas, botones y títulos
│   │   ├── Images/*.svg              Íconos de los módulos
│   │   ├── AppIcon/appicon.svg
│   │   └── Splash/splash.svg
│   └── Platforms/Android/            MainActivity, MainApplication, AndroidManifest
├── docs/
│   └── pantalla-inicio.png           Captura de la primera interfaz
└── README.md
```

## Cómo ejecutar

1. Instalar Visual Studio 2022 (17.8 o superior) con la carga de trabajo **Desarrollo de la interfaz de usuario de aplicaciones multiplataforma de .NET** (.NET MAUI) y el SDK de Android.
2. Abrir `HotelReservas.sln`.
3. Seleccionar como destino un emulador Android o un dispositivo físico con depuración USB.
4. Ejecutar (F5). Para generar el APK: clic derecho en el proyecto → **Publicar** → Android, o desde consola:

```bash
dotnet build -f net8.0-android -c Release
```

El APK queda en `HotelReservas/bin/Release/net8.0-android/`.

## Plan de entregas

| Entrega | Contenido |
|---|---|
| S7 (actual) | Repositorio, primera interfaz y navegación inicial |
| Siguiente | Modelo de datos en SQL Server y diseño de la transacción de reserva |
| Siguiente | API REST en ASP.NET Core con transacciones (BEGIN TRAN / COMMIT / ROLLBACK) |
| Siguiente | Conexión de la app a la API, CRUD de huéspedes, habitaciones y reservas |
| Final | Demostración de concurrencia (dos usuarios, última habitación) y pruebas |

## Documentos relacionados

- Ficha técnica del microproyecto (Sistemas Transaccionales).
- Documentación del proyecto, Parte 1: contexto, problemática, necesidad y pregunta problema (Arquitectura de Software).
