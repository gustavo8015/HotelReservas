# Cómo generar e instalar el APK (versión 0.3, prototipo funcional con impuestos, comprobante por WhatsApp y reserva rápida)

## Requisitos
- Visual Studio 2022 (17.8 o superior) con la carga de trabajo **Desarrollo de la interfaz de usuario de aplicaciones multiplataforma de .NET** (.NET MAUI) y el SDK de Android.
- Un emulador Android (Android Device Manager) o un teléfono con **depuración USB** activada.

## Ejecutar en el emulador o en el teléfono (para la demostración)
1. Abrir `HotelReservas.sln`.
2. En la barra de destino elegir el emulador (por ejemplo, Pixel 5 - API 34) o el teléfono conectado.
3. Presionar **F5**. La app instala y abre en la pantalla de inicio; los botones "Nueva reserva", "Consultar disponibilidad" y las tarjetas de módulo abren el prototipo funcional.

## Generar el archivo APK para instalarlo en cualquier teléfono
Opción A (Visual Studio): clic derecho en el proyecto **HotelReservas** → **Publicar...** → Android → crear o usar un almacén de claves (keystore) → **Guardar como** → elegir formato **APK**. El archivo queda en `HotelReservas\bin\Release\net8.0-android\publish\`.

Opción B (consola, dentro de la carpeta HotelReservas):
```
dotnet publish -f net8.0-android -c Release -p:AndroidPackageFormat=apk
```
El APK firmado con la clave de depuración queda en `bin\Release\net8.0-android\publish\com.umb.hotelreservas-Signed.apk`.

## Instalar en el teléfono
Copiar el APK al teléfono (cable, correo o Drive), abrirlo y permitir "instalar aplicaciones de origen desconocido". O con el teléfono en depuración USB:
```
adb install -r com.umb.hotelreservas-Signed.apk
```

## Proyectar el teléfono en la auditoría
- Emulador: ya se ve en el portátil.
- Teléfono físico: usar **scrcpy** (gratuito) con el cable USB, o la opción "Proyectar" de Windows si el teléfono lo permite.

## Qué cambió respecto a la versión 0.1
- `Views/PrototipoPage.xaml(.cs)`: página con un WebView que carga `Resources/Raw/prototipo.html` (empaquetado como asset de Android) con el prototipo funcional: disponibilidad, confirmación transaccional con prueba de concurrencia, historial con bitácora y asistente de IA. El botón flotante "Ver servidor" muestra el registro de la API y de SQL Server simulados.
- `AppShell.xaml.cs`: ruta `prototipo` registrada.
- `Views/InicioPage.xaml.cs` y las páginas de módulo: navegan al prototipo con `prototipo?vista=disp|habs|hist|ia`.
- `HotelReservas.csproj`: incluye `Resources\Raw\**` como MauiAsset; versión 0.3. `PrototipoPage.xaml.cs` abre los enlaces externos (wa.me) con la app de WhatsApp del teléfono.
