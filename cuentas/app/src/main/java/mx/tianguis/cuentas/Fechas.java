package mx.tianguis.cuentas;

import java.text.SimpleDateFormat;
import java.util.Calendar;
import java.util.Date;
import java.util.Locale;

/**
 * Manejo de fechas en espanol. El dia se identifica siempre con la cadena
 * "yyyy-MM-dd", que se ordena y se compara sola.
 *
 * La semana va de lunes a domingo, como se cuenta en el tianguis.
 */
public final class Fechas {

    public static final Locale MX = new Locale("es", "MX");

    private static final String[] DIAS = {
            "Domingo", "Lunes", "Martes", "Miercoles", "Jueves", "Viernes", "Sabado"
    };
    private static final String[] DIAS_CORTOS = {
            "Dom", "Lun", "Mar", "Mie", "Jue", "Vie", "Sab"
    };
    private static final String[] MESES = {
            "enero", "febrero", "marzo", "abril", "mayo", "junio",
            "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
    };
    private static final String[] MESES_CORTOS = {
            "ene", "feb", "mar", "abr", "may", "jun",
            "jul", "ago", "sep", "oct", "nov", "dic"
    };

    private Fechas() {
    }

    private static SimpleDateFormat clave() {
        SimpleDateFormat f = new SimpleDateFormat("yyyy-MM-dd", MX);
        return f;
    }

    /** Calendario limpio (sin hora) puesto en el dia indicado. */
    public static Calendar calendario(String fecha) {
        Calendar c = Calendar.getInstance(MX);
        c.setFirstDayOfWeek(Calendar.MONDAY);
        try {
            Date d = clave().parse(fecha);
            if (d != null) {
                c.setTime(d);
            }
        } catch (Exception e) {
            // Si la cadena viniera dañada se usa el dia de hoy.
        }
        limpiarHora(c);
        return c;
    }

    public static Calendar calendarioHoy() {
        Calendar c = Calendar.getInstance(MX);
        c.setFirstDayOfWeek(Calendar.MONDAY);
        limpiarHora(c);
        return c;
    }

    private static void limpiarHora(Calendar c) {
        c.set(Calendar.HOUR_OF_DAY, 0);
        c.set(Calendar.MINUTE, 0);
        c.set(Calendar.SECOND, 0);
        c.set(Calendar.MILLISECOND, 0);
    }

    public static String clave(Calendar c) {
        return clave().format(c.getTime());
    }

    public static String hoy() {
        return clave(calendarioHoy());
    }

    /** Suma (o resta) dias a una fecha "yyyy-MM-dd". */
    public static String sumarDias(String fecha, int dias) {
        Calendar c = calendario(fecha);
        c.add(Calendar.DAY_OF_MONTH, dias);
        return clave(c);
    }

    /** Lunes de la semana a la que pertenece la fecha. */
    public static String lunesDeLaSemana(String fecha) {
        Calendar c = calendario(fecha);
        int dia = c.get(Calendar.DAY_OF_WEEK);
        // Domingo cuenta como ultimo dia de la semana anterior.
        int restar = (dia == Calendar.SUNDAY) ? 6 : (dia - Calendar.MONDAY);
        c.add(Calendar.DAY_OF_MONTH, -restar);
        return clave(c);
    }

    /** "Viernes" */
    public static String nombreDia(String fecha) {
        Calendar c = calendario(fecha);
        return DIAS[c.get(Calendar.DAY_OF_WEEK) - 1];
    }

    /** "Vie" */
    public static String nombreDiaCorto(String fecha) {
        Calendar c = calendario(fecha);
        return DIAS_CORTOS[c.get(Calendar.DAY_OF_WEEK) - 1];
    }

    /** "8 de agosto" */
    public static String diaYMes(String fecha) {
        Calendar c = calendario(fecha);
        return c.get(Calendar.DAY_OF_MONTH) + " de " + MESES[c.get(Calendar.MONTH)];
    }

    /** "8 de agosto de 2026" */
    public static String diaMesAnio(String fecha) {
        Calendar c = calendario(fecha);
        return c.get(Calendar.DAY_OF_MONTH) + " de " + MESES[c.get(Calendar.MONTH)]
                + " de " + c.get(Calendar.YEAR);
    }

    /** "Viernes 8 de agosto de 2026" */
    public static String completa(String fecha) {
        return nombreDia(fecha) + " " + diaMesAnio(fecha);
    }

    /** "8 ago" */
    public static String diaMesCorto(String fecha) {
        Calendar c = calendario(fecha);
        return c.get(Calendar.DAY_OF_MONTH) + " " + MESES_CORTOS[c.get(Calendar.MONTH)];
    }

    public static int anio(String fecha) {
        return calendario(fecha).get(Calendar.YEAR);
    }

    /**
     * Rango de la semana escrito de forma corta:
     * "3 – 9 de agosto" si no cambia el mes, "29 de junio – 5 de julio" si cambia.
     */
    public static String rangoSemana(String lunes) {
        String domingo = sumarDias(lunes, 6);
        Calendar cl = calendario(lunes);
        Calendar cd = calendario(domingo);
        if (cl.get(Calendar.MONTH) == cd.get(Calendar.MONTH)) {
            return cl.get(Calendar.DAY_OF_MONTH) + " – " + cd.get(Calendar.DAY_OF_MONTH)
                    + " de " + MESES[cd.get(Calendar.MONTH)];
        }
        return diaYMes(lunes) + " – " + diaYMes(domingo);
    }

    /** Rango de la semana con año, para los reportes. */
    public static String rangoSemanaLargo(String lunes) {
        String domingo = sumarDias(lunes, 6);
        return "Del " + diaMesAnio(lunes) + " al " + diaMesAnio(domingo);
    }

    /** Fecha y hora en que se genero un reporte: "08/08/2026 14:35". */
    public static String selloDeTiempo() {
        return new SimpleDateFormat("dd/MM/yyyy HH:mm", MX).format(new Date());
    }

    /** Sufijo para nombres de archivo: "2026-08-08". */
    public static String paraArchivo(String fecha) {
        return fecha;
    }
}
