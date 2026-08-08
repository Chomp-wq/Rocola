package mx.tianguis.cuentas;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.util.List;

/** El corte convertido en una imagen PNG lista para mandar por WhatsApp. */
public final class ExportImagen {

    /** Alto máximo en píxeles, para no quedarnos sin memoria en un teléfono modesto. */
    private static final float ALTO_MAXIMO_PX = 8000f;

    private ExportImagen() {
    }

    public static File generar(Context ctx, Reporte r) throws IOException {
        Bitmap bmp = dibujar(r, 2f);
        File destino = Archivos.nuevo(ctx, r.nombreArchivo + ".png");
        FileOutputStream salida = new FileOutputStream(destino);
        try {
            bmp.compress(Bitmap.CompressFormat.PNG, 100, salida);
        } finally {
            try {
                salida.close();
            } catch (IOException ignorada) {
                // El archivo ya quedó escrito.
            }
            bmp.recycle();
        }
        return destino;
    }

    private static Bitmap dibujar(Reporte r, float escala) {
        Lienzo lienzo = new Lienzo(escala);
        List<Lienzo.Bloque> bloques = lienzo.bloques(r, true);

        float margen = lienzo.margen();
        float alto = margen * 2 + Lienzo.altoTotal(bloques) + 26f * escala;

        // Si el corte salió larguísimo se baja la escala en vez de reventar.
        if (alto > ALTO_MAXIMO_PX && escala > 1f) {
            float nueva = Math.max(1f, escala * (ALTO_MAXIMO_PX / alto));
            if (nueva < escala) {
                return dibujar(r, nueva);
            }
        }

        int ancho = Math.round(lienzo.anchoHoja());
        Bitmap bmp = Bitmap.createBitmap(ancho, Math.round(alto), Bitmap.Config.ARGB_8888);
        Canvas c = new Canvas(bmp);
        lienzo.fondo(c, ancho, alto);

        float x = margen;
        float anchoContenido = lienzo.anchoContenido();
        float y = margen;
        for (int i = 0; i < bloques.size(); i++) {
            Lienzo.Bloque b = bloques.get(i);
            b.dibujar(c, x, y, anchoContenido);
            y += b.alto();
        }

        lienzo.pieDePagina(c, x, alto - margen + 6f * escala, anchoContenido,
                "Cuentas Tianguis · generado el " + Fechas.selloDeTiempo());
        return bmp;
    }
}
