# Hotel Reservas · Servicio inteligente (TensorFlow + FastAPI)

Servicio que recibe los datos de una reserva o de una fecha, usa un modelo de aprendizaje automático entrenado con TensorFlow/Keras y devuelve la predicción en JSON. Sigue la guía "Cómo crear un servicio inteligente de software con TensorFlow" de la UMB.

## Arquitectura
App móvil / HotelReservas.Api (`/api/prediccion`, con token JWT) → **FastAPI** (`api.py`, valida la solicitud) → vector de entrada normalizado → **TensorFlow** (`modelo_cancelacion.keras`, `modelo_ocupacion.keras`) → respuesta JSON.

| Modelo | Tipo | Entradas | Salida | Requerimiento |
|---|---|---|---|---|
| modelo_cancelacion.keras | Clasificación (8-8-1, tanh + sigmoide) | anticipación, noches, personas, tipo, tarjeta, efectivo, huésped frecuente, fin de semana | probabilidad de cancelación y nivel BAJO / MEDIO / ALTO | RF19 |
| modelo_ocupacion.keras | Regresión (5-6-1, tanh + lineal) | seno y coseno del día de la semana, fin de semana, temporada alta, reservas anticipadas | habitaciones ocupadas previstas (de 25) | RF21 |

## Instalación (Python 3.10 a 3.13)
```
cd servicio_inteligente
python -m venv .venv
.venv\Scripts\activate        (Windows)   ·   source .venv/bin/activate (macOS/Linux)
python -m pip install --upgrade pip
pip install -r requirements.txt
```

## Entrenar y ejecutar
```
python generar_datos.py        # datos/cancelaciones.csv y datos/ocupacion.csv (simulados mientras no hay historial real)
python entrenar_modelo.py      # entrena, guarda los .keras y escribe metricas.json
python probar_api.py           # 10 pruebas automáticas; guarda pruebas/evidencias.json
uvicorn api:app --reload --port 8000
```
Documentación interactiva: http://127.0.0.1:8000/docs

## Endpoints
| Método y ruta | Entrada | Respuesta |
|---|---|---|
| GET / | | estado del servicio y modelos cargados |
| GET /metricas | | métricas de validación: exactitud, precisión, sensibilidad, F1, matriz de confusión; MAE, MSE, RMSE, R² |
| POST /predecir/cancelacion | `{"anticipacion_dias":36,"noches":3,"personas":2,"tipo":"Doble","medio_pago":"TARJETA","huesped_frecuente":false,"fin_de_semana":true}` | `{"probabilidad_cancelacion":0.1475,"nivel":"BAJO","recomendacion":"..."}` |
| POST /predecir/ocupacion | `{"fecha":"2026-12-19","reservas_anticipadas":15}` | `{"habitaciones_ocupadas_previstas":21.8,"capacidad":25,"nivel":"ALTA"}` |
| POST /predecir/ocupacion/semana | `{"desde":"2026-10-16","reservas_por_dia":[1,2,1,0,0,0,0]}` | pronóstico de los 7 días |

Los datos fuera de rango o con valores no permitidos responden 422 con el detalle de la validación.

## Integración con el sistema transaccional
`HotelReservas.Api/Controllers/PrediccionController.cs` reenvía las solicitudes al servicio (URL en `appsettings.json`, clave `ServicioInteligente:Url`) exigiendo el token JWT, de modo que la app no accede al servicio Python directamente. La predicción no interviene en la transacción de confirmación: el riesgo se guarda en la columna `RESERVA.riesgo_cancelacion` (script `database/07_servicio_inteligente.sql`) junto con la reserva.

## Datos reales
Cuando el hotel acumule historial, el script 07 crea las vistas `vw_historial_cancelaciones` y `vw_ocupacion_diaria`, que se exportan a los mismos CSV y reemplazan a `generar_datos.py`; después se vuelve a ejecutar `entrenar_modelo.py` y `probar_api.py`.

## Limitaciones
Los modelos se entrenaron con historial simulado (400 reservas y 365 días); las métricas describen el funcionamiento del flujo, no el desempeño que tendrán en el hotel. La recomendación de riesgo es orientativa: la decisión de exigir garantía corresponde a la política comercial del hotel.
