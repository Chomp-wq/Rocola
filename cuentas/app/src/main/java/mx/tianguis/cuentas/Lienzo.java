package mx.tianguis.cuentas;

import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import android.graphics.Typeface;

import java.util.ArrayList;
import java.util.List;

/**
 * Motor de dibujo del corte de caja.
 *
 * Convierte un {@link Reporte} en una lista de bloques que saben pintarse
 * solos sobre un Canvas y cuánto miden de alto. La imagen PNG y el PDF usan
 * este mismo motor —sólo cambia la escala y quién pagina—, de modo que los
 * dos salen idénticos.
 *
 * Las medidas están en puntos (los del PDF: 595 × 842 para una hoja carta
 * A4); la escala multiplica todo cuando se quiere una imagen más nítida.
 */
public class Lienzo {

    public static final float ANCHO_HOJA = 595f;
    public static final float ALTO_HOJA = 842f;
    public static final float MARGEN = 34f;

    private static final int COLOR_TEXTO = 0xFF17202A;
    private static final int COLOR_SUAVE = 0xFF5C6B73;
    private static final int COLOR_LINEA = 0xFFDCE2E4;
    private static final int COLOR_FONDO = 0xFFFFFFFF;

    private final float esc;

    private final Paint pFondo = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pBanda = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pNegocio = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pTituloDoc = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pSubtituloDoc = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pSeccion = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pTinte = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pEncabezadoCol = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pTexto = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pSuave = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pNumero = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pMonto = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pPieEtiqueta = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pPieMonto = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pLinea = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pTotalEtiqueta = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pTotalMonto = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pGranEtiqueta = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pGranMonto = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint pNota = new Paint(Paint.ANTI_ALIAS_FLAG);

    private final Typeface normal = Typeface.create("sans-serif", Typeface.NORMAL);
    private final Typeface negrita = Typeface.create("sans-serif", Typeface.BOLD);
    private final Typeface media = Typeface.create("sans-serif-medium", Typeface.NORMAL);

    public Lienzo(float escala) {
        this.esc = escala;

        pFondo.setColor(COLOR_FONDO);
        pBanda.setColor(Reportes.COLOR_PRIMARIO);

        preparar(pNegocio, negrita, 8.5f, 0xB3FFFFFF);
        preparar(pTituloDoc, negrita, 19f, Color.WHITE);
        preparar(pSubtituloDoc, normal, 11f, 0xE6FFFFFF);
        preparar(pSeccion, negrita, 11f, Reportes.COLOR_PRIMARIO);
        preparar(pEncabezadoCol, negrita, 7.8f, COLOR_SUAVE);
        preparar(pTexto, normal, 10f, COLOR_TEXTO);
        preparar(pSuave, normal, 8.6f, COLOR_SUAVE);
        preparar(pNumero, normal, 9.6f, COLOR_SUAVE);
        preparar(pMonto, media, 10f, COLOR_TEXTO);
        preparar(pPieEtiqueta, negrita, 9.6f, COLOR_TEXTO);
        preparar(pPieMonto, negrita, 11f, COLOR_TEXTO);
        preparar(pTotalEtiqueta, normal, 10.5f, COLOR_TEXTO);
        preparar(pTotalMonto, negrita, 11.5f, COLOR_TEXTO);
        preparar(pGranEtiqueta, negrita, 10.5f, Color.WHITE);
        preparar(pGranMonto, negrita, 19f, Color.WHITE);
        preparar(pNota, normal, 8.6f, COLOR_SUAVE);

        pNumero.setTextAlign(Paint.Align.RIGHT);
        pMonto.setTextAlign(Paint.Align.RIGHT);
        pPieMonto.setTextAlign(Paint.Align.RIGHT);
        pTotalMonto.setTextAlign(Paint.Align.RIGHT);
        pGranMonto.setTextAlign(Paint.Align.RIGHT);

        pLinea.setColor(COLOR_LINEA);
        pLinea.setStrokeWidth(0.8f * esc);
    }

    private void preparar(Paint p, Typeface tipo, float tam, int color) {
        p.setTypeface(tipo);
        p.setTextSize(tam * esc);
        p.setColor(color);
    }

    public float escala() {
        return esc;
    }

    public float anchoHoja() {
        return ANCHO_HOJA * esc;
    }

    public float altoHoja() {
        return ALTO_HOJA * esc;
    }

    public float margen() {
        return MARGEN * esc;
    }

    public float anchoContenido() {
        return anchoHoja() - 2 * margen();
    }

    public void fondo(Canvas c, float ancho, float alto) {
        c.drawRect(0, 0, ancho, alto, pFondo);
    }

    // ------------------------------------------------------------- los bloques

    /** Un trozo del reporte que sabe cuánto mide y cómo pintarse. */
    public interface Bloque {
        float alto();

        void dibujar(Canvas c, float x, float y, float ancho);
    }

    /**
     * Convierte el reporte completo en bloques. Quien llame decide si los pinta
     * todos seguidos (imagen) o los reparte en hojas (PDF).
     */
    public List<Bloque> bloques(Reporte r, boolean conEncabezado) {
        List<Bloque> lista = new ArrayList<Bloque>();
        if (conEncabezado) {
            lista.add(new Encabezado(r));
            lista.add(new Espacio(16f));
        }
        for (int i = 0; i < r.secciones.size(); i++) {
            Reporte.Seccion s = r.secciones.get(i);
            if (i > 0) {
                lista.add(new Espacio(14f));
            }
            lista.add(new TituloSeccion(s));
            lista.add(new EncabezadoColumnas(s));
            if (s.vacia()) {
                lista.add(new FilaVacia(s));
            } else {
                for (int j = 0; j < s.filas.size(); j++) {
                    lista.add(new FilaTabla(s, s.filas.get(j), j));
                }
            }
            lista.add(new PieSeccion(s));
        }
        lista.add(new Espacio(18f));
        lista.add(new RecuadroTotales(r));
        if (r.nota != null && r.nota.length() > 0) {
            lista.add(new Espacio(10f));
            lista.add(new Nota(r.nota));
        }
        return lista;
    }

    /** Suma del alto de una lista de bloques. */
    public static float altoTotal(List<Bloque> bloques) {
        float alto = 0;
        for (int i = 0; i < bloques.size(); i++) {
            alto += bloques.get(i).alto();
        }
        return alto;
    }

    // --- espacio en blanco

    public class Espacio implements Bloque {
        private final float alto;

        public Espacio(float puntos) {
            this.alto = puntos * esc;
        }

        @Override
        public float alto() {
            return alto;
        }

        @Override
        public void dibujar(Canvas c, float x, float y, float ancho) {
        }
    }

    // --- banda superior con el título del corte

    public class Encabezado implements Bloque {
        private final Reporte r;

        public Encabezado(Reporte r) {
            this.r = r;
        }

        @Override
        public float alto() {
            return 88f * esc;
        }

        @Override
        public void dibujar(Canvas c, float x, float y, float ancho) {
            RectF caja = new RectF(x, y, x + ancho, y + alto());
            c.drawRoundRect(caja, 8f * esc, 8f * esc, pBanda);

            float px = x + 18f * esc;
            c.drawText(r.negocio.toUpperCase(Fechas.MX), px, y + 24f * esc, pNegocio);
            c.drawText(r.titulo, px, y + 50f * esc, pTituloDoc);
            c.drawText(r.subtitulo, px, y + 70f * esc, pSubtituloDoc);

            // Importe del corte, alineado a la derecha de la banda.
            Paint pd = new Paint(pGranMonto);
            pd.setTextSize(20f * esc);
            c.drawText(Dinero.formato(r.granTotal()), x + ancho - 18f * esc, y + 58f * esc, pd);
            Paint pe = new Paint(pNegocio);
            pe.setTextAlign(Paint.Align.RIGHT);
            c.drawText("TOTAL", x + ancho - 18f * esc, y + 38f * esc, pe);
        }
    }

    // --- título de sección

    public class TituloSeccion implements Bloque {
        private final Reporte.Seccion s;

        public TituloSeccion(Reporte.Seccion s) {
            this.s = s;
        }

        @Override
        public float alto() {
            return 26f * esc;
        }

        @Override
        public void dibujar(Canvas c, float x, float y, float ancho) {
            pTinte.setColor(conAlfa(s.color, 26));
            RectF caja = new RectF(x, y, x + ancho, y + 22f * esc);
            c.drawRoundRect(caja, 4f * esc, 4f * esc, pTinte);

            pTinte.setColor(s.color);
            c.drawRect(x, y, x + 3.5f * esc, y + 22f * esc, pTinte);

            pSeccion.setColor(s.color);
            c.drawText(s.titulo, x + 10f * esc, y + 15.5f * esc, pSeccion);
        }
    }

    // --- encabezados de columna

    public class EncabezadoColumnas implements Bloque {
        private final Reporte.Seccion s;

        public EncabezadoColumnas(Reporte.Seccion s) {
            this.s = s;
        }

        @Override
        public float alto() {
            return 18f * esc;
        }

        @Override
        public void dibujar(Canvas c, float x, float y, float ancho) {
            float base = y + 11f * esc;
            Paint der = new Paint(pEncabezadoCol);
            der.setTextAlign(Paint.Align.RIGHT);
            c.drawText(s.encabezados[0].toUpperCase(Fechas.MX), x + 4f * esc, base, pEncabezadoCol);
            c.drawText(s.encabezados[1].toUpperCase(Fechas.MX), x + col2(ancho), base, der);
            c.drawText(s.encabezados[2].toUpperCase(Fechas.MX), x + col3(ancho), base, der);
            c.drawText(s.encabezados[3].toUpperCase(Fechas.MX), x + ancho - 4f * esc, base, der);
            c.drawLine(x, y + 15f * esc, x + ancho, y + 15f * esc, pLinea);
        }
    }

    // --- una fila de la tabla

    public class FilaTabla implements Bloque {
        private final Reporte.Seccion s;
        private final Reporte.Fila f;
        private final int indice;
        private String[] lineas;

        public FilaTabla(Reporte.Seccion s, Reporte.Fila f, int indice) {
            this.s = s;
            this.f = f;
            this.indice = indice;
        }

        private String[] lineas(float ancho) {
            if (lineas == null) {
                lineas = partir(pTexto, f.c1, col2(ancho) - 30f * esc, 2);
            }
            return lineas;
        }

        @Override
        public float alto() {
            // El ancho real se conoce hasta dibujar; se mide con el de la hoja.
            return (lineas(anchoContenido()).length > 1 ? 30f : 19f) * esc;
        }

        @Override
        public void dibujar(Canvas c, float x, float y, float ancho) {
            if (indice % 2 == 1) {
                pTinte.setColor(0xFFF7F9FA);
                c.drawRect(x, y, x + ancho, y + alto(), pTinte);
            }
            String[] ls = lineas(ancho);
            float base = y + 13f * esc;
            c.drawText(ls[0], x + 4f * esc, base, pTexto);
            if (ls.length > 1) {
                c.drawText(ls[1], x + 4f * esc, base + 11f * esc, pSuave);
            }
            c.drawText(f.c2, x + col2(ancho), base, pNumero);
            c.drawText(f.c3, x + col3(ancho), base, pNumero);
            c.drawText(Dinero.formato(f.monto), x + ancho - 4f * esc, base, pMonto);
        }
    }

    // --- sección sin movimientos

    public class FilaVacia implements Bloque {
        private final Reporte.Seccion s;

        public FilaVacia(Reporte.Seccion s) {
            this.s = s;
        }

        @Override
        public float alto() {
            return 22f * esc;
        }

        @Override
        public void dibujar(Canvas c, float x, float y, float ancho) {
            c.drawText(s.mensajeVacio, x + 4f * esc, y + 14f * esc, pSuave);
        }
    }

    // --- subtotal de la sección

    public class PieSeccion implements Bloque {
        private final Reporte.Seccion s;

        public PieSeccion(Reporte.Seccion s) {
            this.s = s;
        }

        @Override
        public float alto() {
            return 26f * esc;
        }

        @Override
        public void dibujar(Canvas c, float x, float y, float ancho) {
            c.drawLine(x, y + 2f * esc, x + ancho, y + 2f * esc, pLinea);
            pPieEtiqueta.setColor(s.color);
            c.drawText(s.pieEtiqueta, x + 4f * esc, y + 18f * esc, pPieEtiqueta);
            pPieMonto.setColor(s.color);
            c.drawText(Dinero.formato(s.pieMonto), x + ancho - 4f * esc, y + 18f * esc, pPieMonto);
        }
    }

    // --- recuadro final con los totales

    public class RecuadroTotales implements Bloque {
        private final Reporte r;

        public RecuadroTotales(Reporte r) {
            this.r = r;
        }

        private int normales() {
            int n = 0;
            for (int i = 0; i < r.totales.size(); i++) {
                if (!r.totales.get(i).destacado) {
                    n++;
                }
            }
            return n;
        }

        @Override
        public float alto() {
            return (normales() * 22f + 52f + 20f) * esc;
        }

        @Override
        public void dibujar(Canvas c, float x, float y, float ancho) {
            float altoNormales = normales() * 22f * esc + 20f * esc;
            RectF marco = new RectF(x, y, x + ancho, y + altoNormales);
            pTinte.setColor(0xFFF2F4F5);
            c.drawRoundRect(marco, 8f * esc, 8f * esc, pTinte);

            float cursor = y + 24f * esc;
            for (int i = 0; i < r.totales.size(); i++) {
                Reporte.Linea l = r.totales.get(i);
                if (l.destacado) {
                    continue;
                }
                c.drawText(l.etiqueta, x + 14f * esc, cursor, pTotalEtiqueta);
                c.drawText(Dinero.formato(l.monto), x + ancho - 14f * esc, cursor, pTotalMonto);
                cursor += 22f * esc;
            }

            // El gran total va en su propia caja de color.
            float arribaGran = y + altoNormales + 6f * esc;
            RectF cajaGran = new RectF(x, arribaGran, x + ancho, arribaGran + 46f * esc);
            c.drawRoundRect(cajaGran, 8f * esc, 8f * esc, pBanda);
            for (int i = 0; i < r.totales.size(); i++) {
                Reporte.Linea l = r.totales.get(i);
                if (!l.destacado) {
                    continue;
                }
                c.drawText(l.etiqueta, x + 14f * esc, arribaGran + 28f * esc, pGranEtiqueta);
                c.drawText(Dinero.formato(l.monto), x + ancho - 14f * esc,
                        arribaGran + 32f * esc, pGranMonto);
            }
        }
    }

    // --- nota al pie

    public class Nota implements Bloque {
        private final String texto;

        public Nota(String texto) {
            this.texto = texto;
        }

        @Override
        public float alto() {
            return 16f * esc;
        }

        @Override
        public void dibujar(Canvas c, float x, float y, float ancho) {
            c.drawText(texto, x + 2f * esc, y + 11f * esc, pNota);
        }
    }

    /** Renglón gris del final de cada hoja. */
    public void pieDePagina(Canvas c, float x, float y, float ancho, String texto) {
        Paint p = new Paint(pNota);
        p.setTextSize(7.6f * esc);
        c.drawLine(x, y - 8f * esc, x + ancho, y - 8f * esc, pLinea);
        c.drawText(texto, x, y + 2f * esc, p);
    }

    /** Marca de la derecha del pie ("Página 1 de 2"). */
    public void pieDePaginaDerecha(Canvas c, float x, float y, float ancho, String texto) {
        Paint p = new Paint(pNota);
        p.setTextSize(7.6f * esc);
        p.setTextAlign(Paint.Align.RIGHT);
        c.drawText(texto, x + ancho, y + 2f * esc, p);
    }

    // ------------------------------------------------------------- utilidades

    private float col2(float ancho) {
        return ancho * 0.62f;
    }

    private float col3(float ancho) {
        return ancho * 0.81f;
    }

    private static int conAlfa(int color, int alfa) {
        return (alfa << 24) | (color & 0x00FFFFFF);
    }

    /**
     * Parte un texto en varias líneas para que quepa en el ancho dado. La
     * última línea se corta con puntos suspensivos si aún se pasa.
     */
    private String[] partir(Paint p, String texto, float ancho, int maxLineas) {
        if (texto == null) {
            texto = "";
        }
        if (p.measureText(texto) <= ancho) {
            return new String[]{texto};
        }
        List<String> lineas = new ArrayList<String>();
        String resto = texto;
        while (resto.length() > 0 && lineas.size() < maxLineas) {
            int cabe = p.breakText(resto, true, ancho, null);
            if (cabe <= 0) {
                cabe = 1;
            }
            if (cabe < resto.length() && lineas.size() < maxLineas - 1) {
                // Cortar en el último espacio para no partir palabras.
                int espacio = resto.lastIndexOf(' ', cabe);
                if (espacio > 0) {
                    cabe = espacio;
                }
            }
            String linea = resto.substring(0, cabe).trim();
            resto = resto.substring(cabe).trim();
            if (lineas.size() == maxLineas - 1 && resto.length() > 0) {
                linea = recortar(p, linea + " " + resto, ancho);
                resto = "";
            }
            lineas.add(linea);
        }
        return lineas.toArray(new String[lineas.size()]);
    }

    private String recortar(Paint p, String texto, float ancho) {
        if (p.measureText(texto) <= ancho) {
            return texto;
        }
        int cabe = p.breakText(texto, true, ancho - p.measureText("…"), null);
        if (cabe <= 0) {
            return "…";
        }
        return texto.substring(0, cabe).trim() + "…";
    }
}
