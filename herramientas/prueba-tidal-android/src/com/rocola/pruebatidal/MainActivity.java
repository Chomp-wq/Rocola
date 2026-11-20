package com.rocola.pruebatidal;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.net.Uri;
import android.os.Bundle;
import android.util.Log;
import android.view.View;
import android.view.Window;
import android.webkit.ConsoleMessage;
import android.webkit.PermissionRequest;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.TextView;

import java.io.IOException;

/**
 * PRUEBA DE TIDAL (Android): el mismo programa que la prueba de la PC, dentro de un WebView. La pagina y el SDK oficial de TIDAL
 * para la web son los mismos; el puente (Servidor) vive dentro de la app y sirve http://127.0.0.1:8760/ (el mismo origen y la misma
 * direccion de regreso que se registra en la app de TIDAL en la PC). El sonido sale del WebView (el reproductor oficial, con Widevine).
 */
public class MainActivity extends Activity {
    static final int PUERTO = 8760;
    static final String ORIGEN = "http://127.0.0.1:" + PUERTO;
    private WebView web;
    private Servidor servidor;

    @Override
    protected void onCreate(Bundle estado) {
        super.onCreate(estado);
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        try {
            servidor = new Servidor(this, PUERTO);
            servidor.inicia();
        } catch (IOException e) {
            TextView t = new TextView(this);
            t.setPadding(40, 80, 40, 40);
            t.setTextSize(18);
            t.setText("No pude abrir el puerto " + PUERTO + " (" + e.getMessage() + ").\n\nCierra la app por completo y vuelve a abrirla.");
            setContentView(t);
            return;
        }
        WebView.setWebContentsDebuggingEnabled(true);
        web = new WebView(this);
        web.setBackgroundColor(Color.parseColor("#0b0d14"));
        WebSettings s = web.getSettings();
        s.setJavaScriptEnabled(true);
        s.setDomStorageEnabled(true);
        s.setMediaPlaybackRequiresUserGesture(false);
        s.setLoadWithOverviewMode(true);
        s.setUseWideViewPort(true);
        web.setWebChromeClient(new WebChromeClient() {
            // Widevine (el DRM de TIDAL) pide este permiso al WebView; solo se da a la pagina de esta app.
            @Override
            public void onPermissionRequest(final PermissionRequest r) {
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        String origen = r.getOrigin() == null ? "" : r.getOrigin().toString();
                        boolean nuestra = origen.startsWith(ORIGEN);
                        if (nuestra) {
                            java.util.ArrayList<String> ok = new java.util.ArrayList<String>();
                            for (String x : r.getResources()) {
                                if (PermissionRequest.RESOURCE_PROTECTED_MEDIA_ID.equals(x)) ok.add(x);
                            }
                            if (!ok.isEmpty()) { r.grant(ok.toArray(new String[0])); return; }
                        }
                        r.deny();
                    }
                });
            }

            @Override
            public boolean onConsoleMessage(ConsoleMessage m) {
                Log.d("RocolaTidal", m.message() + " (" + m.sourceId() + ":" + m.lineNumber() + ")");
                return true;
            }
        });
        web.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(WebView v, android.webkit.WebResourceRequest r) {
                Uri u = r.getUrl();
                String esq = u.getScheme() == null ? "" : u.getScheme();
                // http(s) se queda en el WebView (la entrada de TIDAL incluida); otros (tidal://, intent://...) no se abren.
                return !(esq.equals("http") || esq.equals("https"));
            }
        });
        setContentView(web);
        web.loadUrl(ORIGEN + "/");
    }

    /** Para la pagina: el informe se manda por el menu de compartir. */
    void comparteInforme(final String texto) {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                Intent i = new Intent(Intent.ACTION_SEND);
                i.setType("text/plain");
                i.putExtra(Intent.EXTRA_SUBJECT, "Informe de la prueba de TIDAL");
                i.putExtra(Intent.EXTRA_TEXT, texto.length() > 200000 ? texto.substring(texto.length() - 200000) : texto);
                startActivity(Intent.createChooser(i, "Mandar el informe"));
            }
        });
    }

    @Override
    public void onBackPressed() {
        if (web != null && web.canGoBack()) web.goBack();
        else super.onBackPressed();
    }

    @Override
    protected void onDestroy() {
        if (web != null) { web.destroy(); web = null; }
        if (servidor != null) servidor.detiene();
        super.onDestroy();
    }
}
