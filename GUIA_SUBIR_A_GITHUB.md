# Guía para crear el repositorio en GitHub y subir esta entrega

## 1. Crear el repositorio en GitHub
1. Ingresar a https://github.com y hacer clic en **New repository**.
2. Nombre: `HotelReservas` (o `sistema-transaccional-hotel-reservas`).
3. Descripción: "Sistema Transaccional Móvil (APK) para la Gestión de Reservas de Habitaciones en un Hotel".
4. Visibilidad: **Public** (para que el docente pueda acceder). Si se deja privado, agregar al docente como colaborador en Settings → Collaborators.
5. NO marcar "Add a README" ni ".gitignore" (ya vienen en esta carpeta).
6. Clic en **Create repository** y copiar la URL, por ejemplo `https://github.com/USUARIO/HotelReservas.git`.

## 2. Subir el proyecto desde el computador
Abrir una terminal (Git Bash, PowerShell o CMD) dentro de la carpeta `HotelReservas` descomprimida y ejecutar:

```bash
git init
git add .
git commit -m "Entrega S7: repositorio y primera interfaz del sistema Hotel Reservas"
git branch -M main
git remote add origin https://github.com/USUARIO/HotelReservas.git
git push -u origin main
```

Si Git pide identidad la primera vez:

```bash
git config --global user.name "Gustavo Adolfo Cardona Bayona"
git config --global user.email "correo@ejemplo.com"
```

## 3. Registrar un segundo avance (opcional, recomendado)
Abrir el proyecto en Visual Studio, hacer un cambio pequeño (por ejemplo el texto de un módulo) y:

```bash
git add .
git commit -m "Ajustes de la pantalla de inicio"
git push
```

## 4. Entregar
En la plataforma de la universidad pegar únicamente la URL del repositorio, por ejemplo:
`https://github.com/USUARIO/HotelReservas`
