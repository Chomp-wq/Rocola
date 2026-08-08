package mx.tianguis.cuentas;

import android.content.Context;

import java.io.File;
import java.io.IOException;

/**
 * Carpeta donde se dejan los archivos que se van a compartir. Vive dentro de
 * la caché de la app, así que no necesita ningún permiso y el sistema puede
 * limpiarla cuando haga falta espacio.
 */
public final class Archivos {

    public static final String CARPETA = "compartir";

    private Archivos() {
    }

    public static File carpeta(Context ctx) throws IOException {
        File dir = new File(ctx.getCacheDir(), CARPETA);
        if (!dir.exists() && !dir.mkdirs()) {
            throw new IOException("No se pudo crear la carpeta para compartir");
        }
        return dir;
    }

    /** Crea (vacío) el archivo con ese nombre, borrando el anterior si existía. */
    public static File nuevo(Context ctx, String nombre) throws IOException {
        File destino = new File(carpeta(ctx), limpiar(nombre));
        if (destino.exists() && !destino.delete()) {
            throw new IOException("No se pudo reemplazar " + nombre);
        }
        return destino;
    }

    /** Quita del nombre lo que no sirva como archivo. */
    public static String limpiar(String nombre) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < nombre.length(); i++) {
            char ch = nombre.charAt(i);
            boolean valido = Character.isLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.';
            sb.append(valido ? ch : '-');
        }
        return sb.toString();
    }
}
