"""Servicio inteligente de Hotel Reservas: API FastAPI que expone los modelos TensorFlow entrenados por
entrenar_modelo.py. Lo consume la API transaccional HotelReservas.Api (recurso /api/prediccion) o la app.
Arranque: uvicorn api:app --reload --port 8000  ·  documentacion interactiva: http://127.0.0.1:8000/docs
"""
import json, math
from datetime import date, timedelta
from typing import Literal, List
import numpy as np
import tensorflow as tf
from fastapi import FastAPI
from pydantic import BaseModel, Field

app = FastAPI(title="Hotel Reservas · Servicio inteligente", version="1.0.0",
              description="Predice el riesgo de cancelacion de una reserva (RF19) y la ocupacion diaria (RF21) con modelos TensorFlow/Keras.")
modelo_cancelacion = tf.keras.models.load_model("modelo_cancelacion.keras")
modelo_ocupacion = tf.keras.models.load_model("modelo_ocupacion.keras")
METRICAS = json.load(open("metricas.json", encoding="utf-8"))
TIPOS = {"Sencilla": 0.0, "Doble": 0.5, "Suite": 1.0}
TEMPORADA_ALTA = {12, 1, 6, 7}

class SolicitudCancelacion(BaseModel):
    anticipacion_dias: int = Field(ge=0, le=365, description="Dias entre la fecha de la reserva y la entrada")
    noches: int = Field(ge=1, le=30)
    personas: int = Field(ge=1, le=4)
    tipo: Literal["Sencilla", "Doble", "Suite"]
    medio_pago: Literal["TARJETA", "EFECTIVO", "TRANSFERENCIA"]
    huesped_frecuente: bool = Field(description="Dos o mas estadias anteriores (RF18)")
    fin_de_semana: bool = Field(description="Entrada en viernes o sabado")

class SolicitudOcupacion(BaseModel):
    fecha: date
    reservas_anticipadas: int = Field(ge=0, le=25, description="Reservas ya registradas para esa fecha")

class SolicitudSemana(BaseModel):
    desde: date
    reservas_por_dia: List[int] = Field(min_length=7, max_length=7)

def nivel_riesgo(p):
    return "ALTO" if p >= 0.5 else "MEDIO" if p >= 0.25 else "BAJO"

def recomendacion(nivel):
    return {"ALTO": "Solicitar garantia con tarjeta o anticipo y enviar recordatorio por WhatsApp dos dias antes.",
            "MEDIO": "Enviar recordatorio por WhatsApp un dia antes de la llegada.", "BAJO": "Sin accion adicional."}[nivel]

def vector_ocupacion(f: date, ant: int):
    dow = (f.weekday() + 1) % 7
    return [math.sin(2 * math.pi * dow / 7), math.cos(2 * math.pi * dow / 7), 1.0 if dow in (5, 6) else 0.0,
            1.0 if f.month in TEMPORADA_ALTA else 0.0, min(ant, 25) / 25]

def predecir_ocupacion(f: date, ant: int):
    x = np.array([vector_ocupacion(f, ant)], dtype=np.float32)
    frac = float(np.clip(modelo_ocupacion.predict(x, verbose=0)[0][0], 0, 1))
    return round(frac * 25, 1), "ALTA" if frac >= 0.8 else "MEDIA" if frac >= 0.5 else "BAJA"

@app.get("/")
def inicio():
    return {"servicio": "Hotel Reservas · Servicio inteligente", "estado": "activo", "tensorflow": tf.__version__,
            "modelos": ["modelo_cancelacion.keras (clasificacion, RF19)", "modelo_ocupacion.keras (regresion, RF21)"]}

@app.get("/metricas")
def metricas():
    """Metricas de validacion calculadas al entrenar: clasificacion (exactitud, precision, sensibilidad, F1, matriz) y regresion (MAE, MSE, RMSE, R2)."""
    return METRICAS

@app.post("/predecir/cancelacion")
def predecir_cancelacion(s: SolicitudCancelacion):
    x = np.array([[min(s.anticipacion_dias, 60) / 60, min(s.noches, 7) / 7, s.personas / 4, TIPOS[s.tipo],
                   1.0 if s.medio_pago == "TARJETA" else 0.0, 1.0 if s.medio_pago == "EFECTIVO" else 0.0,
                   1.0 if s.huesped_frecuente else 0.0, 1.0 if s.fin_de_semana else 0.0]], dtype=np.float32)
    p = float(modelo_cancelacion.predict(x, verbose=0)[0][0])
    nivel = nivel_riesgo(p)
    return {"probabilidad_cancelacion": round(p, 4), "nivel": nivel, "recomendacion": recomendacion(nivel),
            "nota": "Modelo entrenado con historial simulado; validar con datos reales del hotel antes de uso operativo."}

@app.post("/predecir/ocupacion")
def predecir_ocupacion_dia(s: SolicitudOcupacion):
    hab, nivel = predecir_ocupacion(s.fecha, s.reservas_anticipadas)
    return {"fecha": s.fecha.isoformat(), "habitaciones_ocupadas_previstas": hab, "capacidad": 25, "nivel": nivel}

@app.post("/predecir/ocupacion/semana")
def predecir_ocupacion_semana(s: SolicitudSemana):
    out = []
    for i, ant in enumerate(s.reservas_por_dia):
        f = s.desde + timedelta(days=i)
        hab, nivel = predecir_ocupacion(f, ant)
        out.append({"fecha": f.isoformat(), "reservas_anticipadas": ant, "habitaciones_ocupadas_previstas": hab, "nivel": nivel})
    return {"pronostico": out, "capacidad": 25}
