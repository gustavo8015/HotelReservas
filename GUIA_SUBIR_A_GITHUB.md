# Guía para subir y actualizar el repositorio en GitHub

Repositorio: https://github.com/gustavo8015/HotelReservas

## Opción A: desde la página web (sin instalar nada)

1. Entrar al repositorio y hacer clic en **Add file → Upload files**.
2. Arrastrar a la zona de carga las carpetas y archivos de la actualización
   (por ejemplo `database`, `docs` y `README.md`). GitHub conserva la estructura
   de carpetas y reemplaza los archivos que ya existían con el mismo nombre.
3. En **Commit changes** escribir el mensaje, por ejemplo:
   `Entrega S8: Fase II, diagramas y scripts SQL`.
4. Dejar marcada la opción **Commit directly to the main branch** y hacer clic
   en **Commit changes**.

## Opción B: desde la consola con Git

Primera vez (clonar el repositorio en el computador):

```bash
git clone https://github.com/gustavo8015/HotelReservas.git
cd HotelReservas
```

Cada actualización (después de copiar los archivos nuevos dentro de la carpeta):

```bash
git add .
git commit -m "Entrega S8: Fase II, diagramas y scripts SQL"
git push origin main
```

Si Git pide credenciales, usar el usuario de GitHub y un *personal access token*
(Settings → Developer settings → Personal access tokens) en lugar de la contraseña.

## Verificar

Al terminar, en la página principal del repositorio debe verse el `README.md`
actualizado con las imágenes de la arquitectura y del modelo relacional, la
carpeta `database` con tres scripts y la carpeta `docs` con los documentos
de la Fase I y la Fase II.
