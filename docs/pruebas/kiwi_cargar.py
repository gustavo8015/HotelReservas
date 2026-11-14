# Carga en Kiwi TCMS el plan de pruebas, los casos y las corridas (con resultados reales) de Hotel Reservas.
# Requisitos: pip install tcms-api  y un archivo ~/.tcms.conf con [tcms] url, username y password del Kiwi TCMS.
# Uso: python kiwi_cargar.py evidencias/apk_v093/resultados_apk.json evidencias/apk_v094/resultados_apk.json evidencias/servicio/pruebas/evidencias.json
import base64, json, os, sys
from datetime import datetime
from tcms_api import TCMS

rpc = TCMS().exec
import configparser
_cfg = configparser.ConfigParser(); _cfg.read(os.path.expanduser("~/.tcms.conf"))
yo = rpc.User.filter({"username": _cfg["tcms"]["username"]})[0]["id"]

def uno(filtro, crear):
    """Devuelve el objeto que cumpla el filtro o lo crea."""
    r = filtro()
    return r[0] if r else crear()

PRIO = {p["value"]: p["id"] for p in rpc.Priority.filter({})}
ESTADO_EJEC = {s["name"]: s["id"] for s in rpc.TestExecutionStatus.filter({})}
ESTADO_CASO = {s["name"]: s["id"] for s in rpc.TestCaseStatus.filter({})}
TIPO_PLAN = {p["name"]: p["id"] for p in rpc.PlanType.filter({})}

clasif = uno(lambda: rpc.Classification.filter({"name": "Software académico"}), lambda: rpc.Classification.create({"name": "Software académico"}))
producto = uno(lambda: rpc.Product.filter({"name": "Hotel Reservas"}),
               lambda: rpc.Product.create({"name": "Hotel Reservas", "classification": clasif["id"], "description": "Sistema transaccional de reservas de hotel (UMB · Sistemas Transaccionales). APK Android (WebView) y servicios de apoyo."}))
PID = producto["id"]

def version(nombre):
    return uno(lambda: rpc.Version.filter({"product": PID, "value": nombre}), lambda: rpc.Version.create({"product": PID, "value": nombre}))
def build(ver, nombre):
    return uno(lambda: rpc.Build.filter({"version": ver["id"], "name": nombre}), lambda: rpc.Build.create({"version": ver["id"], "name": nombre}))
def categoria(nombre):
    return uno(lambda: rpc.Category.filter({"product": PID, "name": nombre}), lambda: rpc.Category.create({"product": PID, "name": nombre}))

def plan(nombre, ver, texto, tipo="Function"):
    return uno(lambda: rpc.TestPlan.filter({"product": PID, "name": nombre}),
               lambda: rpc.TestPlan.create({"product": PID, "product_version": ver["id"], "name": nombre, "type": TIPO_PLAN.get(tipo, 3), "text": texto, "is_active": True}))

def caso(pl, c, evidencias_dir=None):
    resumen = f"{c['id']} · {c['titulo']}"
    ex = rpc.TestCase.filter({"summary": resumen})
    if ex:
        tc = ex[0]
    else:
        texto = (f"**Requisito / caso de uso:** {c.get('cu','')}\n\n**Precondiciones:** aplicación instalada con los datos de demostración; usuarios de prueba vigentes.\n\n"
                 f"**Pasos:**\n{c['pasos']}\n\n**Resultado esperado:**\n{c['esperado']}")
        tc = rpc.TestCase.create({"product": PID, "category": categoria(c.get("categoria", "Funcional"))["id"], "summary": resumen, "text": texto,
                                  "priority": PRIO.get(c.get("prioridad", "P2"), 2), "case_status": ESTADO_CASO["CONFIRMED"], "is_automated": True,
                                  "script": c.get("script", "qa/ejecutar_pruebas_apk.js"), "notes": c.get("cu", "")})
        if evidencias_dir and c.get("evidencia") and os.path.exists(os.path.join(evidencias_dir, c["evidencia"])):
            with open(os.path.join(evidencias_dir, c["evidencia"]), "rb") as f:
                try: rpc.TestCase.add_attachment(tc["id"], c["evidencia"], base64.b64encode(f.read()).decode())
                except Exception as e: print("  (captura no adjuntada por el validador de Kiwi:", c["evidencia"], ")")
    try: rpc.TestPlan.add_case(pl["id"], tc["id"])
    except Exception: pass
    return tc

def corrida(pl, bld, nombre, casos, resultados, notas=""):
    run = rpc.TestRun.create({"build": bld["id"], "manager": yo, "default_tester": yo, "plan": pl["id"], "summary": nombre, "notes": notas})
    for c, tc in zip(resultados, casos):
        ej = rpc.TestRun.add_case(run["id"], tc["id"])
        ej = ej[0] if isinstance(ej, list) else ej
        estado = "PASSED" if c["resultado"] in ("PASA",) else "FAILED"
        rpc.TestExecution.update(ej["id"], {"status": ESTADO_EJEC[estado], "tested_by": yo, "stop_date": datetime.now().strftime("%Y-%m-%d %H:%M:%S")})
        rpc.TestExecution.add_comment(ej["id"], f"Resultado obtenido: {c.get('obtenido','')}" + (f"\nEvidencia: {c['evidencia']}" if c.get("evidencia") else ""))
    rpc.TestRun.update(run["id"], {"stop_date": datetime.now().strftime("%Y-%m-%d %H:%M:%S")})
    return run

def cargar_apk(ruta, nombre_build, nombre_corrida, notas):
    d = json.load(open(ruta, encoding="utf-8"))
    ver = version("APK 0.9.x (S15)")
    pl = plan("Plan de pruebas · APK Hotel Reservas 0.9.3 (entrega S15)", ver,
              "Plan de pruebas funcionales, transaccionales y de seguridad de la información de la APK Hotel Reservas (Android Studio, WebView sobre prototipo.html). "
              "Cubre CU-01 a CU-16, RF16 a RF19, RNF01 y los controles de la Política de Seguridad y Privacidad de la Información v1.0 (Ley 1266 de 2008, Ley 1581 de 2012). "
              "Ejecución automatizada con Playwright (qa/ejecutar_pruebas_apk.js); cada caso deja una captura de pantalla como evidencia.", "System")
    casos = [caso(pl, c, os.path.dirname(ruta)) for c in d["casos"]]
    run = corrida(pl, build(ver, nombre_build), nombre_corrida, casos, d["casos"], notas)
    print(f"APK · plan {pl['id']} · corrida {run['id']} · {d['pasan']}/{d['total']} pasan")

def cargar_servicio(ruta):
    d = json.load(open(ruta, encoding="utf-8"))
    ver = version("servicio_seguridad_ia 1.0")
    pl = plan("Plan de pruebas · Servicio inteligente de seguridad de la información 1.0", ver,
              "Pruebas de la API FastAPI servicio_seguridad_ia (puerto 8100): catálogo de normas, consulta con recuperación TF-IDF, retiro de datos personales, verificación de textos, política de contraseñas y validaciones. "
              "Ejecución automatizada con probar_servicio.py; las solicitudes y respuestas reales quedan en pruebas/evidencias.json.", "Integration")
    casos_json = []
    for i, p in enumerate(d["pruebas"], 1):
        casos_json.append({"id": f"SS-{i:02d}", "titulo": p["prueba"], "cu": "Servicio IA de seguridad · Ley 1266 de 2008 · Ley 1581 de 2012 · Política v1.0", "categoria": "Servicio IA",
                           "prioridad": "P1" if i <= 8 else "P2", "pasos": f"1. Enviar {p['metodo']} {p['ruta']} con el cuerpo: {json.dumps(p['solicitud'], ensure_ascii=False) if p['solicitud'] is not None else '(sin cuerpo)'}",
                           "esperado": "Respuesta HTTP 200 con las fuentes normativas pertinentes" if p["ruta"] != "/consultar" or p["solicitud"].get("pregunta") else "Respuesta HTTP 422 (validación de entrada)",
                           "obtenido": f"HTTP {p['estado_http']} · " + json.dumps(p["respuesta"], ensure_ascii=False)[:600], "resultado": p["resultado"], "script": "servicio_seguridad_ia/probar_servicio.py"})
    casos = [caso(pl, c) for c in casos_json]
    run = corrida(pl, build(ver, "servicio_seguridad_ia 1.0 (commit 359dd8b)"), "Corrida 3 · servicio de seguridad 1.0 · 14 pruebas automáticas", casos, casos_json,
                  "Ejecutado con python probar_servicio.py contra http://127.0.0.1:8100; evidencias en pruebas/evidencias.json.")
    print(f"Servicio · plan {pl['id']} · corrida {run['id']} · {d['pasan']}/{d['total']} pasan")

if __name__ == "__main__":
    a = sys.argv[1:]
    cargar_apk(a[0], "APK 0.9.3 (commit 3c97b61)", "Corrida 1 · APK 0.9.3 · ejecución inicial", "Primera ejecución del plan sobre la APK 0.9.3 publicada en GitHub el 14 de noviembre de 2026. Se detectó el defecto DEF-01 (cierre por inactividad auditado como LOGOUT).")
    cargar_apk(a[1], "APK 0.9.4 (corrección DEF-01)", "Corrida 2 · APK 0.9.4 · re-ejecución tras corregir DEF-01", "Re-ejecución completa del plan tras corregir DEF-01 en prototipo.html (el envoltorio del modo conectado no pasaba el parámetro porInactividad a logout).")
    cargar_servicio(a[2])
