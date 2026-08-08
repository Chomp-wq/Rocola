package mx.tianguis.cuentas;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.graphics.pdf.PdfDocument;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

/**
 * El corte en PDF tamaño A4, paginado, con encabezado en cada hoja y
 * numeración al pie. Es el formato para imprimir o archivar.
 */
public final class ExportPdf {

    private ExportPdf() {
    }

    public static File generar(Context ctx, Reporte r) throws IOException {
        Lienzo lienzo = new Lienzo(1f);
        List<Lienzo.Bloque> bloques = lienzo.bloques(r, true);

        float margen = lienzo.margen();
        float anchoContenido = lienzo.anchoContenido();
        float alturaUtilPrimera = Lienzo.ALTO_HOJA - margen - 34f;
        float alturaUtilResto = Lienzo.ALTO_HOJA - margen - 34f;

        List<List<Lienzo.Bloque>> hojas = paginar(bloques, margen, alturaUtilPrimera,
                alturaUtilResto, margen + 26f);

        PdfDocument documento = new PdfDocument();
        try {
            for (int i = 0; i < hojas.size(); i++) {
                PdfDocument.PageInfo info = new PdfDocument.PageInfo.Builder(
                        Math.round(Lienzo.ANCHO_HOJA), Math.round(Lienzo.ALTO_HOJA), i + 1).create();
                PdfDocument.Page hoja = documento.startPage(info);
                Canvas c = hoja.getCanvas();
                lienzo.fondo(c, Lienzo.ANCHO_HOJA, Lienzo.ALTO_HOJA);

                float y = margen;
                if (i > 0) {
                    y = encabezadoContinuacion(c, r, margen, anchoContenido);
                }

                List<Lienzo.Bloque> deLaHoja = hojas.get(i);
                for (int j = 0; j < deLaHoja.size(); j++) {
                    Lienzo.Bloque b = deLaHoja.get(j);
                    b.dibujar(c, margen, y, anchoContenido);
                    y += b.alto();
                }

                float pie = Lienzo.ALTO_HOJA - margen + 8f;
                lienzo.pieDePagina(c, margen, pie, anchoContenido,
                        "Cuentas Tianguis · generado el " + Fechas.selloDeTiempo());
                lienzo.pieDePaginaDerecha(c, margen, pie, anchoContenido,
                        "Página " + (i + 1) + " de " + hojas.size());

                documento.finishPage(hoja);
            }

            File destino = Archivos.nuevo(ctx, r.nombreArchivo + ".pdf");
            FileOutputStream salida = new FileOutputStream(destino);
            try {
                documento.writeTo(salida);
            } finally {
                salida.close();
            }
            return destino;
        } finally {
            documento.close();
        }
    }

    /** Reparte los bloques en hojas sin cortar ninguno por la mitad. */
    private static List<List<Lienzo.Bloque>> paginar(List<Lienzo.Bloque> bloques,
                                                     float arribaPrimera, float abajoPrimera,
                                                     float abajoResto, float arribaResto) {
        List<List<Lienzo.Bloque>> hojas = new ArrayList<List<Lienzo.Bloque>>();
        List<Lienzo.Bloque> actual = new ArrayList<Lienzo.Bloque>();
        float y = arribaPrimera;
        float limite = abajoPrimera;

        for (int i = 0; i < bloques.size(); i++) {
            Lienzo.Bloque b = bloques.get(i);
            if (y + b.alto() > limite && !actual.isEmpty()) {
                hojas.add(actual);
                actual = new ArrayList<Lienzo.Bloque>();
                y = arribaResto;
                limite = abajoResto;
            }
            actual.add(b);
            y += b.alto();
        }
        if (!actual.isEmpty()) {
            hojas.add(actual);
        }
        if (hojas.isEmpty()) {
            hojas.add(new ArrayList<Lienzo.Bloque>());
        }
        return hojas;
    }

    /** Encabezado discreto de las hojas 2 en adelante. Devuelve dónde seguir. */
    private static float encabezadoContinuacion(Canvas c, Reporte r, float margen, float ancho) {
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setTypeface(Typeface.create("sans-serif", Typeface.BOLD));
        p.setTextSize(9f);
        p.setColor(Reportes.COLOR_PRIMARIO);
        c.drawText(r.titulo + " · " + r.subtitulo, margen, margen + 4f, p);

        Paint linea = new Paint(Paint.ANTI_ALIAS_FLAG);
        linea.setColor(0xFFDCE2E4);
        linea.setStrokeWidth(0.8f);
        c.drawLine(margen, margen + 12f, margen + ancho, margen + 12f, linea);
        return margen + 26f;
    }
}
