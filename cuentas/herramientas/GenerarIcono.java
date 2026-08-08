import java.awt.BasicStroke;
import java.awt.Color;
import java.awt.Font;
import java.awt.GradientPaint;
import java.awt.Graphics2D;
import java.awt.RenderingHints;
import java.awt.geom.RoundRectangle2D;
import java.awt.image.BufferedImage;
import java.io.File;

import javax.imageio.ImageIO;

/**
 * Genera los iconos de la aplicacion (mipmap-*) sin necesidad de Android
 * Studio: una libreta con el signo de pesos y las dos etiquetas A y B.
 *
 *   javac -d /tmp/ico GenerarIcono.java
 *   java -cp /tmp/ico GenerarIcono app/src/main/res
 */
public class GenerarIcono {

    private static final int[] TAMANOS = {48, 72, 96, 144, 192};
    private static final String[] CARPETAS = {
            "mipmap-mdpi", "mipmap-hdpi", "mipmap-xhdpi", "mipmap-xxhdpi", "mipmap-xxxhdpi"
    };

    public static void main(String[] args) throws Exception {
        File raizRes = new File(args.length > 0 ? args[0] : "app/src/main/res");
        for (int i = 0; i < TAMANOS.length; i++) {
            File carpeta = new File(raizRes, CARPETAS[i]);
            if (!carpeta.exists() && !carpeta.mkdirs()) {
                throw new IllegalStateException("No se pudo crear " + carpeta);
            }
            BufferedImage img = dibujar(TAMANOS[i]);
            ImageIO.write(img, "png", new File(carpeta, "ic_launcher.png"));
        }
        System.out.println("Iconos generados en " + raizRes.getAbsolutePath());
    }

    private static BufferedImage dibujar(int lado) {
        BufferedImage img = new BufferedImage(lado, lado, BufferedImage.TYPE_INT_ARGB);
        Graphics2D g = img.createGraphics();
        g.setRenderingHint(RenderingHints.KEY_ANTIALIASING, RenderingHints.VALUE_ANTIALIAS_ON);
        g.setRenderingHint(RenderingHints.KEY_RENDERING, RenderingHints.VALUE_RENDER_QUALITY);
        g.setRenderingHint(RenderingHints.KEY_STROKE_CONTROL, RenderingHints.VALUE_STROKE_PURE);
        g.setRenderingHint(RenderingHints.KEY_TEXT_ANTIALIASING,
                RenderingHints.VALUE_TEXT_ANTIALIAS_ON);

        float u = lado / 48f;

        // Fondo redondeado con degradado verde.
        g.setPaint(new GradientPaint(0, 0, new Color(0x00897B), 0, lado, new Color(0x00564C)));
        g.fill(new RoundRectangle2D.Float(0, 0, lado, lado, 11 * u, 11 * u));

        // Hoja de la libreta.
        float hx = 9 * u;
        float hy = 8 * u;
        float hw = 30 * u;
        float hh = 32 * u;
        g.setColor(new Color(0xFFFFFF));
        g.fill(new RoundRectangle2D.Float(hx, hy, hw, hh, 3.5f * u, 3.5f * u));

        // Renglones de la libreta.
        g.setColor(new Color(0xD5DEE0));
        g.setStroke(new BasicStroke(1.4f * u, BasicStroke.CAP_ROUND, BasicStroke.JOIN_ROUND));
        for (int i = 0; i < 3; i++) {
            float y = hy + 20 * u + i * 4.5f * u;
            g.drawLine(Math.round(hx + 4 * u), Math.round(y),
                    Math.round(hx + hw - 8 * u), Math.round(y));
        }

        // Signo de pesos en grande.
        g.setColor(new Color(0x00695C));
        g.setFont(new Font("SansSerif", Font.BOLD, Math.round(17 * u)));
        dibujarCentrado(g, "$", hx + hw / 2f, hy + 15 * u);

        // Etiquetas de los dos artesanos.
        etiqueta(g, u, hx + hw - 9 * u, hy + hh - 5 * u, new Color(0x1565C0), "A");
        etiqueta(g, u, hx + hw - 0.5f * u, hy + hh - 5 * u, new Color(0xAD1457), "B");

        g.dispose();
        return img;
    }

    private static void etiqueta(Graphics2D g, float u, float cx, float cy, Color color,
                                 String letra) {
        float r = 6.5f * u;
        g.setColor(Color.WHITE);
        g.fillOval(Math.round(cx - r - 1 * u), Math.round(cy - r - 1 * u),
                Math.round((r + 1 * u) * 2), Math.round((r + 1 * u) * 2));
        g.setColor(color);
        g.fillOval(Math.round(cx - r), Math.round(cy - r), Math.round(r * 2), Math.round(r * 2));
        g.setColor(Color.WHITE);
        g.setFont(new Font("SansSerif", Font.BOLD, Math.round(8 * u)));
        dibujarCentrado(g, letra, cx, cy);
    }

    /** Dibuja el texto centrado en el punto dado. */
    private static void dibujarCentrado(Graphics2D g, String texto, float cx, float cy) {
        java.awt.FontMetrics fm = g.getFontMetrics();
        int ancho = fm.stringWidth(texto);
        int alto = fm.getAscent() - fm.getDescent();
        g.drawString(texto, cx - ancho / 2f, cy + alto / 2f);
    }
}
