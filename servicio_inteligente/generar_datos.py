"""Genera los conjuntos de datos de entrenamiento del servicio inteligente de Hotel Reservas.

Mientras el hotel no acumula historial real, se simulan dos conjuntos con el mismo patron que usa el
prototipo de la APK (semillas fijas para que los resultados sean reproducibles):
  - datos/cancelaciones.csv : 400 reservas con 8 variables y la etiqueta cancelada (1) o cumplida (0).
  - datos/ocupacion.csv     : 365 dias con 5 variables y la ocupacion observada (habitaciones de 25).
Cuando exista historial, el script database/07_exportar_historial_ia.sql produce los mismos archivos
desde SQL Server y este generador deja de usarse.
"""
import csv, math, os, random
from datetime import date, timedelta

os.makedirs("datos", exist_ok=True)

# ---------- 1. Cancelaciones (clasificacion) ----------
rnd = random.Random(2026)
MEDIOS = ["TARJETA", "EFECTIVO", "TRANSFERENCIA"]
with open("datos/cancelaciones.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["anticipacion_dias", "noches", "personas", "tipo", "medio_pago", "huesped_frecuente", "fin_de_semana", "cancelada"])
    for _ in range(400):
        ant = rnd.randrange(60); noc = 1 + rnd.randrange(6); pers = 1 + rnd.randrange(4); tipo = 1 + rnd.randrange(3)
        medio = MEDIOS[rnd.randrange(3)]; frec = 1 if rnd.random() < 0.3 else 0; fin = 1 if rnd.random() < 0.3 else 0
        logit = (-1.6 + 3.2 * ant / 60 + 1.1 * (medio == "EFECTIVO") - 0.8 * (medio == "TARJETA") - 1.4 * frec
                 + 0.5 * noc / 7 - 0.4 * fin + 0.3 * (tipo - 1) / 2 + (rnd.random() - 0.5) * 2.0)
        w.writerow([ant, noc, pers, tipo, medio, frec, fin, 1 if logit > 0 else 0])

# ---------- 2. Ocupacion diaria (regresion) ----------
rnd = random.Random(365)
TEMP = {12, 1, 6, 7}
with open("datos/ocupacion.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    w.writerow(["fecha", "dia_semana", "fin_de_semana", "temporada_alta", "reservas_anticipadas", "habitaciones_ocupadas"])
    ini = date(2025, 10, 29)
    for i in range(365):
        d = ini + timedelta(days=i); dow = (d.weekday() + 1) % 7  # 0 = domingo como en JavaScript
        finde = 1 if dow in (5, 6) else 0; temp = 1 if d.month in TEMP else 0
        base = 0.42 + 0.22 * finde + 0.15 * temp + 0.05 * math.sin(2 * math.pi * i / 365) + (rnd.random() - 0.5) * 0.16
        ant = max(0, min(25, round(base * 25 * 0.65 + (rnd.random() - 0.5) * 4)))
        y = max(0.0, min(1.0, base + 0.25 * (ant / 25 - base * 0.65) + (rnd.random() - 0.5) * 0.08))
        w.writerow([d.isoformat(), dow, finde, temp, ant, round(y * 25, 2)])
print("datos/cancelaciones.csv (400 filas) y datos/ocupacion.csv (365 filas) generados")
