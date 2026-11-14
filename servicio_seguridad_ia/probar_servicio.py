# Pruebas automáticas del servicio inteligente de seguridad: deja las solicitudes y respuestas reales en pruebas/evidencias.json
import json, os, sys, urllib.request
BASE = os.environ.get("BASE", "http://127.0.0.1:8100")
def llamar(metodo, ruta, cuerpo=None):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    req = urllib.request.Request(BASE + ruta, data=datos, method=metodo, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=20) as r: return r.status, json.loads(r.read().decode())
    except urllib.error.HTTPError as e: return e.code, json.loads(e.read().decode())
PRUEBAS = [
 ("Catálogo", "GET", "/", None, lambda s, r: s == 200 and r["articulos"] >= 30),
 ("Compartir el documento del huésped por WhatsApp con otro hotel", "POST", "/consultar", {"pregunta": "¿Puedo compartir la cédula y el celular de un huésped con otro hotel por WhatsApp?", "rol": "RECEPCIONISTA"}, lambda s, r: s == 200 and any(f["norma"] in ("Ley 1266 de 2008", "Ley 1581 de 2012") for f in r["fuentes"])),
 ("Borrar mis datos (cliente)", "POST", "/consultar", {"pregunta": "Quiero que borren mis datos personales del hotel", "rol": "CLIENTE"}, lambda s, r: s == 200 and any(f["articulo"] in (8, 15, 9) for f in r["fuentes"])),
 ("Enviar la pregunta al modelo de IA en Azure", "POST", "/consultar", {"pregunta": "¿Se pueden enviar los datos del huésped al modelo de inteligencia artificial de Azure OpenAI?"}, lambda s, r: s == 200 and any(f["articulo"] in (26, 25, 18) for f in r["fuentes"])),
 ("Autorización en el registro", "POST", "/consultar", {"pregunta": "¿Necesito autorización del cliente para guardar su correo y teléfono?"}, lambda s, r: s == 200 and any(f["articulo"] in (9, 5, 12) for f in r["fuentes"])),
 ("Menores de edad", "POST", "/consultar", {"pregunta": "Un niño con tarjeta de identidad se va a hospedar, ¿qué datos puedo registrar?"}, lambda s, r: s == 200 and any(f["articulo"] == 7 for f in r["fuentes"])),
 ("Intentos de acceso", "POST", "/consultar", {"pregunta": "Alguien intentó entrar varias veces con la clave de recepción, ¿qué norma aplica?"}, lambda s, r: s == 200 and any(f["norma"] == "Ley 1273 de 2009" or f["articulo"] == 4 for f in r["fuentes"])),
 ("La pregunta trae datos personales: se retiran", "POST", "/consultar", {"pregunta": "¿Puedo enviar el correo laura@correo.com y la CC 1010101010 al modelo?"}, lambda s, r: s == 200 and "correo electrónico" in r["datos_personales_retirados"] and "@" not in r["pregunta"]),
 ("Verificar un texto con teléfono", "POST", "/verificar-dato", {"texto": "Confirmar reserva de Carlos, cel 3109876543, hab 204"}, lambda s, r: s == 200 and r["contiene_datos_personales"] and "3109876543" not in r["texto_seguro"]),
 ("Verificar un texto limpio", "POST", "/verificar-dato", {"texto": "¿Qué dobles hay libres del 16 al 18?"}, lambda s, r: s == 200 and not r["contiene_datos_personales"]),
 ("Contraseña débil", "POST", "/evaluar-contrasena", {"contrasena": "hotel2026"}, lambda s, r: s == 200 and not r["cumple"]),
 ("Contraseña fuerte", "POST", "/evaluar-contrasena", {"contrasena": "Recepcion#2026ok"}, lambda s, r: s == 200 and r["cumple"] and r["nivel"] == "fuerte"),
 ("Política", "GET", "/politica", None, lambda s, r: s == 200 and len(r["controles"]) >= 8),
 ("Solicitud inválida (pregunta vacía) → 422", "POST", "/consultar", {"pregunta": ""}, lambda s, r: s == 422),
]
res, ok = [], 0
for nombre, m, ruta, cuerpo, chk in PRUEBAS:
    s, r = llamar(m, ruta, cuerpo); paso = bool(chk(s, r)); ok += paso
    res.append({"prueba": nombre, "metodo": m, "ruta": ruta, "solicitud": cuerpo, "estado_http": s, "respuesta": r, "resultado": "PASA" if paso else "FALLA"})
    print(("PASA " if paso else "FALLA"), nombre, "·", s)
os.makedirs("pruebas", exist_ok=True)
json.dump({"servicio": BASE, "pasan": ok, "total": len(PRUEBAS), "pruebas": res}, open("pruebas/evidencias.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(f"{ok}/{len(PRUEBAS)} pruebas pasan"); sys.exit(0 if ok == len(PRUEBAS) else 1)
