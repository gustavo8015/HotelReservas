package com.umb.hotelreservas.demo

import android.annotation.SuppressLint
import android.content.Intent
import android.net.Uri
import android.webkit.WebResourceRequest
import android.os.Bundle
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.activity.OnBackPressedCallback
import androidx.appcompat.app.AppCompatActivity

/**
 * Hotel Reservas - prototipo funcional (APK de demostración).
 * Carga el prototipo (assets/prototipo.html) en un WebView a pantalla completa.
 * El prototipo simula la API REST y SQL Server con los datos del script 03 del repositorio.
 */
class MainActivity : AppCompatActivity() {

    private lateinit var web: WebView

    @SuppressLint("SetJavaScriptEnabled")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        web = WebView(this)
        setContentView(web)

        web.settings.javaScriptEnabled = true
        web.settings.domStorageEnabled = true
        web.settings.allowFileAccess = true
        web.webViewClient = object : WebViewClient() {
            // Enlaces externos (wa.me para compartir el comprobante por WhatsApp) se abren con la app del teléfono
            override fun shouldOverrideUrlLoading(view: WebView?, request: WebResourceRequest?): Boolean {
                val url = request?.url ?: return false
                if (url.scheme == "http" || url.scheme == "https") {
                    try { startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url.toString()))) } catch (_: Exception) {}
                    return true
                }
                return false
            }
        }
        web.loadUrl("file:///android_asset/prototipo.html?apk=1&vista=inicio")

        // El botón Atrás vuelve a la pantalla de inicio del prototipo en lugar de cerrar la app
        onBackPressedDispatcher.addCallback(this, object : OnBackPressedCallback(true) {
            override fun handleOnBackPressed() {
                web.evaluateJavascript("go('inicio')", null)
            }
        })
    }
}
