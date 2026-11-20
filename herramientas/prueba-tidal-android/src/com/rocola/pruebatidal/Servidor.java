package com.rocola.pruebatidal;

import android.util.Log;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.net.URL;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * El puente local (lo mismo que hace RocolaPruebaTidal.exe en la PC): sirve la pagina y el SDK de TIDAL en http://127.0.0.1:8760 y
 * pasa a TIDAL, sin los encabezados del navegador (Origin, cookies...), lo que la pagina pide por /tx/openapi, /tx/auth y /tx/api.
 * No guarda llaves ni las escribe en el registro. Solo escucha en 127.0.0.1.
 */
class Servidor {
    private static final String ETQ = "RocolaTidal";
    private final MainActivity act;
    private final int puerto;
    private ServerSocket socket;
    private volatile boolean activo = true;
    private final ExecutorService hilos = Executors.newCachedThreadPool();

    Servidor(MainActivity act, int puerto) {
        this.act = act;
        this.puerto = puerto;
    }

    void inicia() throws IOException {
        socket = new ServerSocket();
        socket.setReuseAddress(true);
        socket.bind(new java.net.InetSocketAddress(InetAddress.getByName("127.0.0.1"), puerto), 64);
        Thread t = new Thread(new Runnable() {
            @Override
            public void run() {
                while (activo) {
                    try {
                        final Socket s = socket.accept();
                        hilos.execute(new Runnable() {
                            @Override
                            public void run() { atiende(s); }
                        });
                    } catch (IOException e) {
                        if (activo) Log.w(ETQ, "accept: " + e);
                    }
                }
            }
        }, "puente-aceptar");
        t.setDaemon(true);
        t.start();
    }

    void detiene() {
        activo = false;
        try { if (socket != null) socket.close(); } catch (IOException e) { /* nada */ }
        hilos.shutdownNow();
    }

    // ---------- una peticion ----------
    private static String linea(InputStream in) throws IOException {
        StringBuilder sb = new StringBuilder();
        int c;
        while ((c = in.read()) != -1) {
            if (c == '\n') break;
            if (c != '\r') sb.append((char) c);
            if (sb.length() > 16384) throw new IOException("linea muy larga");
        }
        if (c == -1 && sb.length() == 0) return null;
        return sb.toString();
    }

    private void atiende(Socket s) {
        try {
            s.setSoTimeout(30000);
            InputStream in = new java.io.BufferedInputStream(s.getInputStream(), 8192);
            OutputStream out = s.getOutputStream();
            String primera = linea(in);
            if (primera == null || primera.isEmpty()) { s.close(); return; }
            String[] p = primera.split(" ");
            if (p.length < 2) { s.close(); return; }
            String metodo = p[0], url = p[1];
            Map<String, String> cab = new LinkedHashMap<String, String>();
            String l;
            while ((l = linea(in)) != null && !l.isEmpty()) {
                int i = l.indexOf(':');
                if (i > 0) cab.put(l.substring(0, i).trim().toLowerCase(), l.substring(i + 1).trim());
            }
            int largo = 0;
            try { largo = Integer.parseInt(cab.containsKey("content-length") ? cab.get("content-length") : "0"); } catch (NumberFormatException e) { /* 0 */ }
            if (largo < 0 || largo > (8 << 20)) { responde(out, 413, "text/plain", "Muy grande".getBytes("UTF-8"), null); s.close(); return; }
            byte[] cuerpo = new byte[largo];
            int hecho = 0;
            while (hecho < largo) {
                int n = in.read(cuerpo, hecho, largo - hecho);
                if (n < 0) break;
                hecho += n;
            }
            String host = cab.containsKey("host") ? cab.get("host") : "";
            if (!host.equals("127.0.0.1:" + puerto) && !host.equals("localhost:" + puerto)) {
                responde(out, 403, "text/plain; charset=utf-8", "Solo desde esta misma app.".getBytes("UTF-8"), null);
            } else {
                ruta(out, metodo, url, cab, cuerpo);
            }
            out.flush();
            s.close();
        } catch (IOException e) {
            try { s.close(); } catch (IOException e2) { /* nada */ }
        } catch (RuntimeException e) {
            Log.w(ETQ, "peticion: " + e);
            try { s.close(); } catch (IOException e2) { /* nada */ }
        }
    }

    private void ruta(OutputStream out, String metodo, String url, Map<String, String> cab, byte[] cuerpo) throws IOException {
        String camino = url.contains("?") ? url.substring(0, url.indexOf('?')) : url;
        if (camino.startsWith("/tx/")) { puente(out, metodo, url, camino, cab, cuerpo); return; }
        boolean post = metodo.equals("POST");
        if (!post && (camino.equals("/") || camino.equals("/index.html"))) { responde(out, 200, "text/html; charset=utf-8", asset("pagina.html"), null); return; }
        if (!post && camino.equals("/tidal_sdk.js")) { responde(out, 200, "text/javascript; charset=utf-8", asset("tidal_sdk.js"), null); return; }
        if (post && camino.equals("/api/anota")) {
            Log.i(ETQ, new String(cuerpo, "UTF-8").replace('\n', ' '));
            responde(out, 200, "application/json", "{\"ok\":true}".getBytes("UTF-8"), null);
            return;
        }
        if (post && camino.equals("/api/informe")) {
            act.comparteInforme(new String(cuerpo, "UTF-8"));
            responde(out, 200, "application/json", "{\"ok\":true,\"archivo\":\"(se abrio el menu para mandarlo)\"}".getBytes("UTF-8"), null);
            return;
        }
        if (post && camino.equals("/api/salir")) {
            responde(out, 200, "application/json", "{\"ok\":true}".getBytes("UTF-8"), null);
            act.runOnUiThread(new Runnable() { @Override public void run() { act.finish(); } });
            return;
        }
        responde(out, 404, "text/plain", "No existe".getBytes("UTF-8"), null);
    }

    private byte[] asset(String nombre) throws IOException {
        InputStream a = act.getAssets().open(nombre);
        try {
            ByteArrayOutputStream b = new ByteArrayOutputStream(1 << 20);
            byte[] t = new byte[16384];
            int n;
            while ((n = a.read(t)) > 0) b.write(t, 0, n);
            return b.toByteArray();
        } finally {
            a.close();
        }
    }

    // ---------- el puente hacia TIDAL ----------
    private static String destino(String camino) {
        String base, resto;
        if (camino.startsWith("/tx/openapi/")) { base = "https://openapi.tidal.com/"; resto = camino.substring("/tx/openapi/".length()); }
        else if (camino.startsWith("/tx/auth/")) { base = "https://auth.tidal.com/"; resto = camino.substring("/tx/auth/".length()); }
        else if (camino.startsWith("/tx/api/")) { base = "https://api.tidal.com/"; resto = camino.substring("/tx/api/".length()); }
        else return null;
        if (resto.contains("..")) return null;
        // Solo para probar en la PC con un servidor falso (-Dprueba.tidal=http://127.0.0.1:PUERTO): nunca esta puesto en el telefono.
        String prueba = System.getProperty("prueba.tidal");
        if (prueba != null) return prueba + "/" + base.substring("https://".length(), base.indexOf('.')) + "/" + resto;
        return base + resto;
    }

    private static boolean noSePasa(String n) {
        return n.equals("host") || n.equals("origin") || n.equals("referer") || n.equals("cookie") || n.equals("connection")
            || n.equals("content-length") || n.equals("accept-encoding") || n.equals("x-requested-with") || n.startsWith("sec-");
    }

    private void puente(OutputStream out, String metodo, String url, String camino, Map<String, String> cab, byte[] cuerpo) throws IOException {
        String dest = destino(camino);
        if (dest == null) { responde(out, 404, "text/plain", "No existe".getBytes("UTF-8"), null); return; }
        if (url.contains("?")) dest += url.substring(url.indexOf('?'));
        long t0 = System.currentTimeMillis();
        HttpURLConnection c = null;
        try {
            c = (HttpURLConnection) new URL(dest).openConnection();
            c.setConnectTimeout(8000);
            c.setReadTimeout(30000);
            c.setInstanceFollowRedirects(false);
            c.setRequestMethod(metodo);
            for (Map.Entry<String, String> e : cab.entrySet()) {
                if (!noSePasa(e.getKey())) c.setRequestProperty(e.getKey(), e.getValue());
            }
            if (cuerpo.length > 0 && !metodo.equals("GET") && !metodo.equals("HEAD")) {
                c.setDoOutput(true);
                OutputStream o = c.getOutputStream();
                o.write(cuerpo);
                o.close();
            }
            int st = c.getResponseCode();
            InputStream is = st >= 400 ? c.getErrorStream() : c.getInputStream();
            byte[] b = new byte[0];
            if (is != null) {
                ByteArrayOutputStream bo = new ByteArrayOutputStream();
                byte[] t = new byte[16384];
                int n;
                while ((n = is.read(t)) > 0) bo.write(t, 0, n);
                is.close();
                b = bo.toByteArray();
            }
            String tipo = c.getContentType() == null ? "application/octet-stream" : c.getContentType();
            Map<String, String> extra = new LinkedHashMap<String, String>();
            if (c.getHeaderField("Retry-After") != null) extra.put("Retry-After", c.getHeaderField("Retry-After"));
            // Solo el camino, sin la consulta (ahi pueden ir codigos de entrada).
            Log.i(ETQ, (st < 400 ? "BIEN" : "MAL ") + " TIDAL " + metodo + " " + camino.substring(4) + " -> " + st + " (" + (System.currentTimeMillis() - t0) + " ms)");
            responde(out, st, tipo, b, extra);
        } catch (IOException e) {
            Log.w(ETQ, "MAL  TIDAL " + metodo + " " + camino.substring(4) + " -> sin respuesta: " + e);
            String m = "{\"errors\":[{\"code\":\"PUENTE\",\"detail\":\"La app no pudo hablar con TIDAL: " + String.valueOf(e.getMessage()).replace("\"", "'") + "\"}]}";
            responde(out, 502, "application/json", m.getBytes("UTF-8"), null);
        } finally {
            if (c != null) c.disconnect();
        }
    }

    private static void responde(OutputStream out, int st, String tipo, byte[] cuerpo, Map<String, String> extra) throws IOException {
        StringBuilder h = new StringBuilder();
        h.append("HTTP/1.1 ").append(st).append(' ').append(razon(st)).append("\r\n");
        h.append("Content-Type: ").append(tipo).append("\r\n");
        h.append("Content-Length: ").append(cuerpo.length).append("\r\n");
        h.append("Cache-Control: no-store\r\n");
        h.append("Connection: close\r\n");
        if (extra != null) for (Map.Entry<String, String> e : extra.entrySet()) h.append(e.getKey()).append(": ").append(e.getValue()).append("\r\n");
        h.append("\r\n");
        out.write(h.toString().getBytes("ISO-8859-1"));
        out.write(cuerpo);
    }

    private static String razon(int st) {
        switch (st) {
            case 200: return "OK";
            case 204: return "No Content";
            case 400: return "Bad Request";
            case 401: return "Unauthorized";
            case 403: return "Forbidden";
            case 404: return "Not Found";
            case 429: return "Too Many Requests";
            case 502: return "Bad Gateway";
            default: return st < 400 ? "OK" : "Error";
        }
    }
}
