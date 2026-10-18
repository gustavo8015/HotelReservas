# Hotel Reservas - APK de demostración para Android Studio

Proyecto Android nativo (Kotlin) que empaqueta el prototipo funcional en una APK.
Es el **plan B** para generar la APK cuando no se dispone de Visual Studio con .NET MAUI.
El proyecto oficial del sistema sigue siendo el de .NET MAUI (carpeta HotelReservas del repositorio).

## Pasos
1. Abrir Android Studio → **File → Open** → seleccionar la carpeta `HotelReservasDemo`.
2. Esperar la sincronización de Gradle (descarga lo necesario la primera vez; requiere internet).
   Si pregunta por el JDK, usar el incluido en Android Studio (17). Si pide actualizar el plugin de Gradle, aceptar.
3. Crear o iniciar un emulador en **Device Manager** (por ejemplo, Pixel 7, API 34) o conectar un teléfono con depuración USB.
4. Presionar **Run ▶**. La app abre el prototipo a pantalla completa.

## Generar el archivo APK
**Build → Build Bundle(s) / APK(s) → Build APK(s)**. Al terminar, clic en *locate*:
`app/build/outputs/apk/debug/app-debug.apk`. Copiarlo al teléfono e instalarlo (permitir orígenes desconocidos)
o instalarlo por cable con `adb install -r app-debug.apk`.

## Proyectar en la auditoría
Emulador: se ve directamente en el portátil. Teléfono físico: scrcpy por USB.
