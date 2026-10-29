"""Entrena los dos modelos del servicio inteligente de Hotel Reservas con TensorFlow/Keras y
guarda los modelos (.keras) y las metricas de validacion (metricas.json).

Modelo 1, clasificacion: probabilidad de que una reserva se cancele (RF19).
   Entradas: anticipacion/60, noches/7, personas/4, tipo (0, 0.5, 1), tarjeta, efectivo, frecuente, fin de semana.
   Metricas: exactitud, precision, sensibilidad (recall), F1 y matriz de confusion.
Modelo 2, regresion: habitaciones ocupadas de 25 en un dia (RF21).
   Entradas: sen y cos del dia de la semana, fin de semana, temporada alta, reservas anticipadas/25.
   Metricas de la ficha tecnica: MAE, MSE, RMSE y R2.
Los datos se dividen 80/20 en entrenamiento y validacion; las metricas se calculan solo sobre validacion.
"""
import csv, json, math
import numpy as np
import tensorflow as tf

tf.keras.utils.set_random_seed(42)

def leer(ruta):
    with open(ruta, encoding="utf-8") as f:
        return list(csv.DictReader(f))

# ---------- Modelo 1: cancelaciones ----------
def vector_cancelacion(r):
    return [min(float(r["anticipacion_dias"]), 60) / 60, min(float(r["noches"]), 7) / 7, float(r["personas"]) / 4,
            (float(r["tipo"]) - 1) / 2, 1.0 if r["medio_pago"] == "TARJETA" else 0.0, 1.0 if r["medio_pago"] == "EFECTIVO" else 0.0,
            float(r["huesped_frecuente"]), float(r["fin_de_semana"])]

filas = leer("datos/cancelaciones.csv")
X = np.array([vector_cancelacion(r) for r in filas], dtype=np.float32)
y = np.array([float(r["cancelada"]) for r in filas], dtype=np.float32)
n = int(len(X) * 0.8)
Xtr, ytr, Xva, yva = X[:n], y[:n], X[n:], y[n:]

clf = tf.keras.Sequential([
    tf.keras.layers.Input(shape=(8,)),
    tf.keras.layers.Dense(8, activation="tanh", kernel_regularizer=tf.keras.regularizers.l2(0.01)),
    tf.keras.layers.Dense(1, activation="sigmoid"),
])
clf.compile(optimizer=tf.keras.optimizers.Adam(0.01), loss="binary_crossentropy", metrics=["accuracy"])
# Parada temprana: conserva los pesos de la epoca con menor perdida de validacion para evitar el sobreajuste
parada = tf.keras.callbacks.EarlyStopping(monitor="val_loss", patience=25, restore_best_weights=True)
hist = clf.fit(Xtr, ytr, epochs=200, batch_size=16, verbose=0, validation_data=(Xva, yva), callbacks=[parada])
p = clf.predict(Xva, verbose=0).ravel()
pred = (p >= 0.5).astype(int); real = yva.astype(int)
tp = int(((pred == 1) & (real == 1)).sum()); tn = int(((pred == 0) & (real == 0)).sum())
fp = int(((pred == 1) & (real == 0)).sum()); fn = int(((pred == 0) & (real == 1)).sum())
prec = tp / (tp + fp) if tp + fp else 0.0; rec = tp / (tp + fn) if tp + fn else 0.0
met_clf = {"exactitud": (tp + tn) / len(real), "precision": prec, "sensibilidad": rec,
           "f1": 2 * prec * rec / (prec + rec) if prec + rec else 0.0,
           "matriz_confusion": {"VP": tp, "FN": fn, "FP": fp, "VN": tn},
           "perdida_final_entrenamiento": float(hist.history["loss"][-1]), "perdida_final_validacion": float(hist.history["val_loss"][-1]),
           "ejemplos_entrenamiento": int(n), "ejemplos_validacion": int(len(real)), "epocas_ejecutadas": len(hist.history["loss"]), "mejor_epoca": int(np.argmin(hist.history["val_loss"])) + 1}
clf.save("modelo_cancelacion.keras")

# ---------- Modelo 2: ocupacion ----------
def vector_ocupacion(dow, finde, temp, ant):
    return [math.sin(2 * math.pi * dow / 7), math.cos(2 * math.pi * dow / 7), float(finde), float(temp), min(float(ant), 25) / 25]

filas = leer("datos/ocupacion.csv")
X = np.array([vector_ocupacion(int(r["dia_semana"]), int(r["fin_de_semana"]), int(r["temporada_alta"]), float(r["reservas_anticipadas"])) for r in filas], dtype=np.float32)
y = np.array([float(r["habitaciones_ocupadas"]) / 25 for r in filas], dtype=np.float32)
n = int(len(X) * 0.8)
Xtr, ytr, Xva, yva = X[:n], y[:n], X[n:], y[n:]

reg = tf.keras.Sequential([
    tf.keras.layers.Input(shape=(5,)),
    tf.keras.layers.Dense(6, activation="tanh"),
    tf.keras.layers.Dense(1, activation="linear"),
])
reg.compile(optimizer=tf.keras.optimizers.Adam(0.01), loss="mse")
hist = reg.fit(Xtr, ytr, epochs=300, batch_size=16, verbose=0, validation_data=(Xva, yva))
yp = np.clip(reg.predict(Xva, verbose=0).ravel(), 0, 1) * 25; yr = yva * 25
err = yr - yp
mae = float(np.mean(np.abs(err))); mse = float(np.mean(err ** 2)); rmse = math.sqrt(mse)
r2 = float(1 - np.sum(err ** 2) / np.sum((yr - yr.mean()) ** 2))
met_reg = {"MAE_habitaciones": mae, "MSE_habitaciones2": mse, "RMSE_habitaciones": rmse, "R2": r2,
           "MAE_porcentaje_capacidad": mae / 25 * 100, "dias_entrenamiento": int(n), "dias_validacion": int(len(yr)), "epocas": 300,
           "perdida_final_entrenamiento": float(hist.history["loss"][-1]), "perdida_final_validacion": float(hist.history["val_loss"][-1])}
reg.save("modelo_ocupacion.keras")

json.dump({"cancelacion": met_clf, "ocupacion": met_reg, "tensorflow": tf.__version__}, open("metricas.json", "w", encoding="utf-8"), indent=2, ensure_ascii=False)
print(json.dumps({"cancelacion": met_clf, "ocupacion": met_reg}, indent=2, ensure_ascii=False))
print("Modelos guardados: modelo_cancelacion.keras, modelo_ocupacion.keras · metricas en metricas.json")
