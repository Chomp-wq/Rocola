package mx.tianguis.cuentas;

import android.content.Context;
import android.content.SharedPreferences;

/**
 * Ajustes sencillos: como se llaman los dos artesanos y cual quedo
 * seleccionado la ultima vez.
 */
public final class Prefs {

    private static final String ARCHIVO = "cuentas_tianguis";
    private static final String NOMBRE_A = "nombre_a";
    private static final String NOMBRE_B = "nombre_b";
    private static final String NEGOCIO = "negocio";
    private static final String ULTIMO_ARTESANO = "ultimo_artesano";

    public static final String PREDETERMINADO_A = "Angel";
    public static final String PREDETERMINADO_B = "Bryan";

    private Prefs() {
    }

    private static SharedPreferences p(Context c) {
        return c.getSharedPreferences(ARCHIVO, Context.MODE_PRIVATE);
    }

    public static String nombreA(Context c) {
        return p(c).getString(NOMBRE_A, PREDETERMINADO_A);
    }

    public static String nombreB(Context c) {
        return p(c).getString(NOMBRE_B, PREDETERMINADO_B);
    }

    /** Nombre del artesano a partir de la letra. */
    public static String nombre(Context c, String artesano) {
        return Venta.ARTESANO_A.equals(artesano) ? nombreA(c) : nombreB(c);
    }

    /** "Angel (A)" */
    public static String nombreConLetra(Context c, String artesano) {
        return nombre(c, artesano) + " (" + artesano + ")";
    }

    public static void guardarNombres(Context c, String a, String b) {
        SharedPreferences.Editor e = p(c).edit();
        e.putString(NOMBRE_A, a.trim().length() == 0 ? PREDETERMINADO_A : a.trim());
        e.putString(NOMBRE_B, b.trim().length() == 0 ? PREDETERMINADO_B : b.trim());
        e.apply();
    }

    public static String negocio(Context c) {
        return p(c).getString(NEGOCIO, "Artesanias");
    }

    public static String ultimoArtesano(Context c) {
        return p(c).getString(ULTIMO_ARTESANO, Venta.ARTESANO_A);
    }

    public static void guardarUltimoArtesano(Context c, String artesano) {
        p(c).edit().putString(ULTIMO_ARTESANO, artesano).apply();
    }
}
