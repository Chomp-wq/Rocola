package mx.tianguis.cuentas;

import java.text.DecimalFormat;
import java.text.DecimalFormatSymbols;
import java.util.Locale;

/**
 * Conversion entre lo que la vendedora teclea ("150", "150.50", "1,200")
 * y los centavos que se guardan en la base, mas el formato para mostrar.
 */
public final class Dinero {

    private static final DecimalFormat CON_SIGNO;
    private static final DecimalFormat SIN_SIGNO;

    static {
        DecimalFormatSymbols simbolos = new DecimalFormatSymbols(new Locale("es", "MX"));
        simbolos.setDecimalSeparator('.');
        simbolos.setGroupingSeparator(',');
        CON_SIGNO = new DecimalFormat("'$'#,##0.00", simbolos);
        SIN_SIGNO = new DecimalFormat("#,##0.00", simbolos);
    }

    private Dinero() {
    }

    /** "$1,234.50" */
    public static String formato(long centavos) {
        synchronized (CON_SIGNO) {
            return CON_SIGNO.format(centavos / 100.0);
        }
    }

    /** "1,234.50" (para columnas donde el signo va aparte). */
    public static String formatoSinSigno(long centavos) {
        synchronized (SIN_SIGNO) {
            return SIN_SIGNO.format(centavos / 100.0);
        }
    }

    /**
     * Convierte lo tecleado a centavos. Acepta "150", "150.5", "150.50",
     * "1,200.00" y tambien coma decimal ("150,50"). Devuelve -1 si no se
     * entiende el texto.
     */
    public static long aCentavos(String texto) {
        if (texto == null) {
            return -1;
        }
        String limpio = texto.trim().replace("$", "").replace(" ", "");
        if (limpio.length() == 0) {
            return -1;
        }
        // Si trae coma y punto, la coma es separador de miles.
        if (limpio.indexOf(',') >= 0 && limpio.indexOf('.') >= 0) {
            limpio = limpio.replace(",", "");
        } else if (limpio.indexOf(',') >= 0) {
            // Solo coma: si separa 1 o 2 digitos finales es decimal, si no, miles.
            int pos = limpio.lastIndexOf(',');
            int decimales = limpio.length() - pos - 1;
            if (decimales == 1 || decimales == 2) {
                limpio = limpio.substring(0, pos) + "." + limpio.substring(pos + 1);
            } else {
                limpio = limpio.replace(",", "");
            }
        }

        int punto = limpio.indexOf('.');
        String enteros;
        String decimales;
        if (punto < 0) {
            enteros = limpio;
            decimales = "00";
        } else {
            enteros = limpio.substring(0, punto);
            decimales = limpio.substring(punto + 1);
            if (limpio.indexOf('.', punto + 1) >= 0) {
                return -1;
            }
        }
        if (enteros.length() == 0) {
            enteros = "0";
        }
        if (decimales.length() == 0) {
            decimales = "00";
        } else if (decimales.length() == 1) {
            decimales = decimales + "0";
        } else if (decimales.length() > 2) {
            decimales = decimales.substring(0, 2);
        }

        if (!soloDigitos(enteros) || !soloDigitos(decimales)) {
            return -1;
        }
        try {
            long pesos = Long.parseLong(enteros);
            long cent = Long.parseLong(decimales);
            return pesos * 100 + cent;
        } catch (NumberFormatException e) {
            return -1;
        }
    }

    private static boolean soloDigitos(String s) {
        if (s.length() == 0) {
            return false;
        }
        for (int i = 0; i < s.length(); i++) {
            if (!Character.isDigit(s.charAt(i))) {
                return false;
            }
        }
        return true;
    }
}
