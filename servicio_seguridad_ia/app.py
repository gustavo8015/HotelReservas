# Servicio inteligente de seguridad de la información y protección de datos (Hotel Reservas)
# ---------------------------------------------------------------------------------------
# Expone con FastAPI un asistente de cumplimiento que responde preguntas sobre la Ley 1266 de 2008,
# la Ley 1581 de 2012, el Decreto 1377 de 2013, la Ley 1273 de 2009, la Ley 527 de 1999 y la Política
# General de Seguridad de la Información adoptada por el proyecto. El modelo es de recuperación:
# vectoriza los artículos con TF-IDF (palabras y n-gramas de caracteres, para tolerar errores
# ortográficos) y devuelve los artículos más afines con la forma en que el sistema los cumple.
# No usa servicios externos ni envía datos a terceros, en coherencia con el artículo 26 de la Ley 1581.
#
# Ejecutar:  pip install -r requirements.txt && uvicorn app:app --port 8100
# Probar:    python probar_servicio.py  (deja las respuestas reales en pruebas/evidencias.json)
import json, re, unicodedata, os
from datetime import datetime
from typing import Optional
from fastapi import FastAPI
from pydantic import BaseModel, Field
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import cosine_similarity

AQUI = os.path.dirname(os.path.abspath(__file__))
KB = json.load(open(os.path.join(AQUI, "base_conocimiento.json"), encoding="utf-8"))

STOP = set("""de la el los las y o a en que se del por con para es al un una como su sus lo no si o u sobre entre ser
 este esta estos estas ese esa esos esas cuando donde cual cuales quien quienes mas pero sin tambien tiene tienen puede pueden
 debe deben ha han hay fue son sera seran articulo ley""".split())

def normalizar(t: str) -> str:
    t = unicodedata.normalize("NFKD", t.lower())
    t = "".join(c for c in t if not unicodedata.combining(c))
    t = re.sub(r"[^a-z0-9ñ\s]", " ", t)
    return " ".join(w for w in t.split() if w not in STOP)

# sinónimos del dominio hotelero para acercar las preguntas del recepcionista al lenguaje de la ley
SINONIMOS = {
    "cedula": "documento identificacion", "documento": "documento identificacion dato personal", "celular": "telefono contacto",
    "whatsapp": "telefono contacto mensaje", "correo": "correo electronico contacto", "huesped": "titular", "cliente": "titular",
    "compartir": "circulacion suministro terceros divulgar", "enviar": "circulacion suministro transferencia", "borrar": "supresion",
    "eliminar": "supresion", "borren": "supresion derechos titular reclamo", "borre": "supresion derechos titular", "eliminen": "supresion derechos titular reclamo", "quitar": "supresion", "supriman": "supresion derechos titular", "corregir": "rectificar actualizar", "contrasena": "clave seguridad acceso", "clave": "contrasena seguridad",
    "ia": "inteligencia artificial encargado transferencia", "azure": "transferencia internacional encargado", "openai": "transferencia internacional encargado",
    "menor": "ninos ninas adolescentes", "nino": "ninos ninas adolescentes", "foto": "dato sensible imagen", "salud": "dato sensible",
    "autorizacion": "autorizacion consentimiento", "permiso": "autorizacion consentimiento", "hackeo": "acceso abusivo sistema informatico",
    "intentos": "acceso abusivo bloqueo seguridad", "bloqueo": "seguridad acceso", "sancion": "sanciones multa", "multa": "sanciones",
    "reclamo": "reclamos consultas peticion", "queja": "reclamos consultas", "privacidad": "confidencialidad aviso politica",
}
def expandir(t: str) -> str:
    n = normalizar(t); extra = [SINONIMOS[w] for w in n.split() if w in SINONIMOS]
    return n + " " + " ".join(extra)

DOCS = [expandir(f"{k['norma']} {k['titulo']} {k['titulo']} {k.get('temas','')} {k.get('temas','')} {k.get('temas','')} {k['texto']} {k['aplicacion']}") for k in KB]
VEC_W = TfidfVectorizer(ngram_range=(1, 2), sublinear_tf=True).fit(DOCS)
VEC_C = TfidfVectorizer(analyzer="char_wb", ngram_range=(3, 5), sublinear_tf=True).fit(DOCS)
M_W, M_C = VEC_W.transform(DOCS), VEC_C.transform(DOCS)

def buscar(pregunta: str, k: int = 3):
    q = expandir(pregunta)
    sim = 0.6 * cosine_similarity(VEC_W.transform([q]), M_W)[0] + 0.4 * cosine_similarity(VEC_C.transform([q]), M_C)[0]
    orden = sim.argsort()[::-1][:k]
    return [(KB[i], float(sim[i])) for i in orden if sim[i] > 0.02]

def cita(e): return f"{e['norma']}, artículo {e['articulo']} ({e['titulo']})"

def redactar(pregunta: str, hits):
    if not hits:
        return "No encuentro una norma de la base de conocimiento que responda a esa pregunta. Reformúlela indicando el dato o la acción (por ejemplo: compartir el documento del huésped, borrar mis datos, enviar la consulta al modelo de IA)."
    e, s = hits[0]
    frag = e["texto"][:520].rsplit(" ", 1)[0] + ("…" if len(e["texto"]) > 520 else "")
    r = f"Según {cita(e)}: {frag} En Hotel Reservas: {e['aplicacion']}"
    if len(hits) > 1:
        r += " También aplica " + "; ".join(cita(h[0]) for h in hits[1:]) + "."
    return r

# ---- detección de datos personales (mismas reglas que FiltroDatosPersonales de la API) ----
PATRONES = [
    ("correo electrónico", re.compile(r"[\w.+-]+@[\w-]+\.[\w.]+")),
    ("documento de identidad", re.compile(r"\b(?:cc|ce|ti|pa|nit|pasaporte|cedula|cédula)\s*[:#]?\s*[a-z]{0,2}\d{5,}\b", re.I)),
    ("teléfono celular", re.compile(r"(?<![\d-])(?:\+?57\s?)?3\d{2}[\s-]?\d{3}[\s-]?\d{4}(?!\d)")),
    ("número de documento suelto", re.compile(r"(?<!rsv)(?<![\d-])\d{7,12}(?![\d-])", re.I)),
]
def enmascarar(texto: str):
    hallados = []
    for nombre, pat in PATRONES:
        if pat.search(texto):
            hallados.append(nombre); texto = pat.sub("[" + nombre.split()[0] + "]", texto)
    return texto, hallados

# ---- política de contraseñas (RNF01) ----
def evaluar_contrasena(c: str):
    reglas = {"al menos 8 caracteres": len(c) >= 8, "una mayúscula": bool(re.search(r"[A-ZÁÉÍÓÚÑ]", c)), "una minúscula": bool(re.search(r"[a-záéíóúñ]", c)),
              "un número": bool(re.search(r"\d", c)), "un símbolo": bool(re.search(r"[^\w\s]", c)), "sin espacios": " " not in c,
              "no es una clave común": c.lower() not in {"12345678", "password", "contraseña", "hotel2026", "qwerty123", "admin123"}}
    cumple = all(reglas.values()); puntaje = sum(reglas.values())
    return {"cumple": cumple, "puntaje": puntaje, "de": len(reglas), "reglas": reglas,
            "nivel": "fuerte" if cumple and len(c) >= 12 else "aceptable" if cumple else "débil"}

app = FastAPI(title="Hotel Reservas · Servicio inteligente de seguridad de la información", version="1.0",
              description="Asistente de cumplimiento basado en la Ley 1266 de 2008, la Ley 1581 de 2012 y la Política General de Seguridad de la Información.")

class Consulta(BaseModel):
    pregunta: str = Field(..., min_length=3, max_length=500)
    rol: Optional[str] = Field("RECEPCIONISTA", description="RECEPCIONISTA, ADMINISTRADOR o CLIENTE")
    k: int = Field(3, ge=1, le=5)

class Texto(BaseModel):
    texto: str = Field(..., min_length=1, max_length=2000)

class Clave(BaseModel):
    contrasena: str = Field(..., min_length=1, max_length=128)

@app.get("/")
def raiz():
    return {"servicio": app.title, "normas": sorted({k["norma"] for k in KB}), "articulos": len(KB),
            "recursos": ["GET /normas", "POST /consultar", "POST /verificar-dato", "POST /evaluar-contrasena", "GET /politica"]}

@app.get("/normas")
def normas():
    return [{"id": k["id"], "norma": k["norma"], "articulo": k["articulo"], "titulo": k["titulo"]} for k in KB]

@app.post("/consultar")
def consultar(c: Consulta):
    texto_filtrado, hallados = enmascarar(c.pregunta)     # la pregunta misma no debe conservar datos personales
    hits = buscar(texto_filtrado, c.k)
    return {"pregunta": texto_filtrado, "datos_personales_retirados": hallados, "rol": c.rol, "respuesta": redactar(texto_filtrado, hits),
            "fuentes": [{"norma": e["norma"], "articulo": e["articulo"], "titulo": e["titulo"], "afinidad": round(s, 3), "aplicacion": e["aplicacion"]} for e, s in hits],
            "fecha": datetime.now().isoformat(timespec="seconds")}

@app.post("/verificar-dato")
def verificar_dato(t: Texto):
    texto, hallados = enmascarar(t.texto)
    normas = [cita(k) for k in KB if (k["norma"], k["articulo"]) in {("Ley 1581 de 2012", 4), ("Ley 1581 de 2012", 26), ("Ley 1266 de 2008", 9)}] if hallados else []
    return {"contiene_datos_personales": bool(hallados), "tipos": hallados, "texto_seguro": texto,
            "recomendacion": ("El texto contiene datos personales: no debe enviarse a servicios externos ni compartirse por canales no autorizados; use la versión enmascarada. "
                              "Normas: " + "; ".join(normas)) if hallados else "El texto no contiene datos personales identificables; puede procesarse."}

@app.post("/evaluar-contrasena")
def evaluar(c: Clave):
    r = evaluar_contrasena(c.contrasena); r["norma"] = "Ley 1266 de 2008, artículo 4 (principio de seguridad); Ley 1581 de 2012, artículo 4, literal g; RNF01"
    return r

@app.get("/politica")
def politica():
    return {"nombre": "Política de Seguridad y Privacidad de la Información de Hotel Reservas", "version": "1.0", "fecha": "2026-11-14",
            "principios": ["confidencialidad", "integridad", "disponibilidad", "protección de datos personales"],
            "controles": ["autenticación con token JWT y contraseñas PBKDF2", "política de contraseñas (8 caracteres, mayúscula, minúscula, número y símbolo)",
                          "bloqueo de la cuenta durante 15 minutos tras cinco intentos fallidos", "cierre de sesión automático a los 10 minutos de inactividad",
                          "autorización de tratamiento de datos con fecha, y aviso de privacidad", "derechos del titular desde la pantalla Mis datos (consulta, rectificación, revocatoria, supresión)",
                          "filtro de datos personales antes del modelo de IA y catálogo de solo lectura por perfil", "auditoría de seguridad (AUDITORIA_SEGURIDAD) y bitácora de reservas",
                          "enmascaramiento de documentos y teléfonos en las listas del personal", "HTTPS entre la aplicación y la API; secretos fuera del código"],
            "responsables": {"responsable del tratamiento": "Hotel Reservas (administrador del hotel)", "encargados": ["Azure OpenAI Service", "servicio inteligente de predicción"],
                             "oficial de seguridad": "administrador del hotel", "canal de atención": "hotelreservas2026@gmail.com"}}
