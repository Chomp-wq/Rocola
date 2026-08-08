package mx.tianguis.cuentas;

import java.util.ArrayList;
import java.util.List;

/**
 * Representación neutral de un corte de caja (de un día o de una semana).
 *
 * Los tres formatos que se pueden compartir —texto, imagen y PDF— se arman a
 * partir de este mismo objeto, así que los tres dicen exactamente lo mismo.
 */
public class Reporte {

    /** Título grande: "CORTE DEL DÍA". */
    public String titulo = "";
    /** Debajo del título: "Viernes 8 de agosto de 2026". */
    public String subtitulo = "";
    /** Nombre del negocio, en el encabezado. */
    public String negocio = "";
    /** Base para el nombre del archivo que se comparte, sin extensión. */
    public String nombreArchivo = "cuenta";

    public final List<Seccion> secciones = new ArrayList<Seccion>();
    /** Renglones del recuadro final de totales. */
    public final List<Linea> totales = new ArrayList<Linea>();
    /** Nota al pie, por ejemplo el número de piezas vendidas. */
    public String nota = "";

    public Seccion nuevaSeccion(String titulo, int color, String[] encabezados, int estilo) {
        Seccion s = new Seccion();
        s.titulo = titulo;
        s.color = color;
        s.encabezados = encabezados;
        s.estilo = estilo;
        secciones.add(s);
        return s;
    }

    public void agregarTotal(String etiqueta, long monto, boolean destacado) {
        Linea l = new Linea();
        l.etiqueta = etiqueta;
        l.monto = monto;
        l.destacado = destacado;
        totales.add(l);
    }

    /** El importe del renglón destacado (el gran total del reporte). */
    public long granTotal() {
        for (int i = totales.size() - 1; i >= 0; i--) {
            if (totales.get(i).destacado) {
                return totales.get(i).monto;
            }
        }
        return 0;
    }

    /** Un bloque del reporte: las piezas de un artesano, el resumen por día... */
    public static class Seccion {

        /** Cada fila es una pieza vendida: "2 × Aretes ($120.00) = $240.00". */
        public static final int ESTILO_PIEZAS = 0;
        /** Cada fila es un día: "Lunes 3 ago — A $320.00 · B $250.00 = $570.00". */
        public static final int ESTILO_DIAS = 1;

        public String titulo = "";
        /** Color de acento del bloque (ARGB). */
        public int color = 0xFF00695C;
        /** Cuatro encabezados de columna; el último es siempre el importe. */
        public String[] encabezados = {"Descripción", "Cant.", "Precio", "Importe"};
        public int estilo = ESTILO_PIEZAS;
        public final List<Fila> filas = new ArrayList<Fila>();
        /** Renglón de cierre del bloque, por ejemplo "Subtotal Angel · 3 piezas". */
        public String pieEtiqueta = "";
        public long pieMonto;
        /** Texto que se muestra cuando el bloque no tiene ninguna fila. */
        public String mensajeVacio = "Sin movimientos.";

        public void agregar(String c1, String c2, String c3, long monto) {
            Fila f = new Fila();
            f.c1 = c1;
            f.c2 = c2;
            f.c3 = c3;
            f.monto = monto;
            filas.add(f);
        }

        public boolean vacia() {
            return filas.isEmpty();
        }
    }

    /** Un renglón de una sección. */
    public static class Fila {
        public String c1 = "";
        public String c2 = "";
        public String c3 = "";
        public long monto;
    }

    /** Un renglón del recuadro de totales. */
    public static class Linea {
        public String etiqueta = "";
        public long monto;
        public boolean destacado;
    }
}
