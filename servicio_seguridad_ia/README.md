# Servicio inteligente de seguridad de la información

Asistente de cumplimiento normativo del proyecto Hotel Reservas. Responde preguntas del personal y de los clientes sobre
protección de datos personales y seguridad de la información a partir de una base de conocimiento de 36 artículos tomados de:

- Ley 1266 de 2008 (hábeas data): artículos 1, 3, 4, 6, 7, 8, 9 y 16.
- Ley 1581 de 2012 (protección de datos personales): artículos 1 a 10, 12 a 15, 17 a 19, 21, 23, 25 y 26.
- Decreto 1377 de 2013: artículos 5, 9 y 13. Ley 1273 de 2009: artículos 269A y 269F. Ley 527 de 1999: artículo 10.
- Política General de Seguridad de la Información (modelo DAFP v02, 2026): política general, roles y sanciones.

Cada artículo lleva, además del texto, la forma en que Hotel Reservas lo cumple (campo `aplicacion`), de modo que la respuesta
cita la norma y señala el control del sistema. Los textos fuente están en `docs/seguridad/normas/`.

## Cómo funciona
Modelo de recuperación (no genera texto libre ni usa servicios externos): vectoriza los artículos con TF-IDF de palabras y de
n-gramas de caracteres (scikit-learn), expande la pregunta con sinónimos del dominio hotelero y devuelve los tres artículos más
afines. La pregunta pasa primero por el filtro de datos personales (mismas reglas que `FiltroDatosPersonales` de la API), de modo
que documentos, teléfonos y correos nunca se procesan ni se registran (Ley 1581 de 2012, artículo 26).

## Ejecutar
```
pip install -r requirements.txt
uvicorn app:app --port 8100
python probar_servicio.py        # 14 pruebas automáticas; deja pruebas/evidencias.json
```
Recursos: `GET /` (catálogo), `GET /normas`, `POST /consultar {pregunta, rol}`, `POST /verificar-dato {texto}`,
`POST /evaluar-contrasena {contrasena}`, `GET /politica`. La API .NET lo expone en `/api/seguridad/consultar`,
`/api/seguridad/verificar-dato` y `/api/seguridad/politica` (SeguridadIAController) con la URL de `appsettings.json`
(`SeguridadIA:Url`). La aplicación móvil usa la misma base de conocimiento en modo demostración (asistente, preguntas de normas).
