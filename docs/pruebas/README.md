# Pruebas de software · Hotel Reservas (entrega S15)

Informe: `Informe_de_Pruebas_Hotel_Reservas.pdf` (16 páginas). Gestión de casos y corridas en Kiwi TCMS 12.4.

Resultados: corrida 1 APK 0.9.3 40/41 (defecto DEF-01), corrida 2 APK 0.9.4 41/41, corrida 3 servicio_seguridad_ia 1.0 14/14.

Reproducir:

```
# APK (Playwright)
npm install playwright
node ejecutar_pruebas_apk.js ../../HotelReservasDemo_AndroidStudio/app/src/main/assets/prototipo.html evidencias/apk

# Servicio (en otra terminal: uvicorn app:app --port 8100 dentro de servicio_seguridad_ia)
python ../../servicio_seguridad_ia/probar_servicio.py

# Kiwi TCMS (pip install tcms-api; ~/.tcms.conf con url, username y password)
python kiwi_cargar.py evidencias/apk_v093/resultados_apk.json evidencias/apk_v094/resultados_apk.json evidencias/servicio/evidencias.json
```

`casos_de_prueba.csv` contiene los 55 casos (pasos, esperado, obtenido, estado por corrida) y se puede importar en Kiwi TCMS, Testiny, Qase o Tuskr.
