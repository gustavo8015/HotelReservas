"""Pruebas automatizadas del servicio inteligente (se ejecutan sin levantar el servidor, con TestClient).
Cada prueba guarda la solicitud y la respuesta reales en pruebas/evidencias.json para adjuntarlas al documento.
Uso: python probar_api.py
"""
import json, os, sys
from fastapi.testclient import TestClient
import api

c = TestClient(api.app)
evid = []; fallas = 0

def prueba(nombre, metodo, ruta, cuerpo, esperado, comprobar):
    global fallas
    r = c.post(ruta, json=cuerpo) if metodo == "POST" else c.get(ruta)
    ok = r.status_code == esperado and comprobar(r.json())
    evid.append({"prueba": nombre, "metodo": metodo, "ruta": ruta, "solicitud": cuerpo, "codigo_http": r.status_code, "respuesta": r.json(), "resultado": "PASA" if ok else "FALLA"})
    print(("PASA " if ok else "FALLA") + " · " + nombre + " · HTTP " + str(r.status_code))
    if not ok: fallas += 1

prueba("P1 Servicio activo", "GET", "/", None, 200, lambda j: j["estado"] == "activo")
prueba("P2 Metricas de validacion disponibles", "GET", "/metricas", None, 200, lambda j: "cancelacion" in j and "ocupacion" in j)
prueba("P3 Cancelacion: reserva con tarjeta, 2 dias, frecuente → riesgo bajo", "POST", "/predecir/cancelacion",
       {"anticipacion_dias": 2, "noches": 2, "personas": 2, "tipo": "Doble", "medio_pago": "TARJETA", "huesped_frecuente": True, "fin_de_semana": False},
       200, lambda j: j["nivel"] == "BAJO" and j["probabilidad_cancelacion"] < 0.25)
prueba("P4 Cancelacion: efectivo, 55 dias, no frecuente → riesgo alto", "POST", "/predecir/cancelacion",
       {"anticipacion_dias": 55, "noches": 3, "personas": 2, "tipo": "Suite", "medio_pago": "EFECTIVO", "huesped_frecuente": False, "fin_de_semana": False},
       200, lambda j: j["nivel"] == "ALTO" and j["probabilidad_cancelacion"] >= 0.5)
prueba("P5 Cancelacion: dato fuera de rango (anticipacion -3) → 422", "POST", "/predecir/cancelacion",
       {"anticipacion_dias": -3, "noches": 2, "personas": 2, "tipo": "Doble", "medio_pago": "TARJETA", "huesped_frecuente": False, "fin_de_semana": False},
       422, lambda j: "detail" in j)
prueba("P6 Cancelacion: medio de pago no valido → 422", "POST", "/predecir/cancelacion",
       {"anticipacion_dias": 3, "noches": 2, "personas": 2, "tipo": "Doble", "medio_pago": "CHEQUE", "huesped_frecuente": False, "fin_de_semana": False},
       422, lambda j: "detail" in j)
prueba("P7 Ocupacion: sabado de temporada alta con 15 reservas → ocupacion alta", "POST", "/predecir/ocupacion",
       {"fecha": "2026-12-19", "reservas_anticipadas": 15}, 200, lambda j: j["habitaciones_ocupadas_previstas"] >= 15 and j["nivel"] in ("ALTA", "MEDIA"))
prueba("P8 Ocupacion: martes ordinario sin reservas → ocupacion baja", "POST", "/predecir/ocupacion",
       {"fecha": "2026-10-20", "reservas_anticipadas": 0}, 200, lambda j: j["habitaciones_ocupadas_previstas"] < 12.5)
prueba("P9 Ocupacion: pronostico de 7 dias", "POST", "/predecir/ocupacion/semana",
       {"desde": "2026-10-16", "reservas_por_dia": [1, 2, 1, 0, 0, 0, 0]}, 200, lambda j: len(j["pronostico"]) == 7)
prueba("P10 Ocupacion: fecha invalida → 422", "POST", "/predecir/ocupacion", {"fecha": "31/02/2026", "reservas_anticipadas": 3}, 422, lambda j: "detail" in j)

os.makedirs("pruebas", exist_ok=True)
json.dump(evid, open("pruebas/evidencias.json", "w", encoding="utf-8"), indent=2, ensure_ascii=False)
print(f"{len(evid) - fallas} de {len(evid)} pruebas pasan · evidencias en pruebas/evidencias.json")
sys.exit(1 if fallas else 0)
