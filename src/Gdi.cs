using System.Drawing;
using System.Drawing.Drawing2D;

namespace Rocola
{
    // Recursos GDI+ compartidos para el hilo de UI. Crear/destruir fuentes,
    // pinceles y lapices en cada OnPaint (25 fps) era el mayor costo del
    // programa original; aqui viven una sola vez durante toda la sesion.
    // Solo deben usarse desde el hilo de UI (todo el pintado ocurre ahi).
    internal static class Gdi
    {
        // Fuentes fijas usadas por los controles (mismos parametros que antes).
        public static readonly Font SansBold95 = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold);
        public static readonly Font SansBold20 = new Font(FontFamily.GenericSansSerif, 20f, FontStyle.Bold);
        public static readonly Font SansBold155 = new Font(FontFamily.GenericSansSerif, 15.5f, FontStyle.Bold);
        public static readonly Font SansBold15 = new Font(FontFamily.GenericSansSerif, 15f, FontStyle.Bold);
        public static readonly Font SansBold13 = new Font(FontFamily.GenericSansSerif, 13f, FontStyle.Bold);
        public static readonly Font SansBold11 = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Bold);
        public static readonly Font SansReg11 = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Regular);
        public static readonly Font SansItalic11 = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Italic);
        public static readonly Font SansBold105 = new Font(FontFamily.GenericSansSerif, 10.5f, FontStyle.Bold);
        public static readonly Font SansBold75 = new Font(FontFamily.GenericSansSerif, 7.5f, FontStyle.Bold);

        private static readonly SolidBrush sharedBrush = new SolidBrush(Color.Black);
        private static readonly Pen sharedPen = new Pen(Color.Black, 1f);

        // Devuelve un pincel solido reutilizable con el color pedido. No
        // conservar la referencia mas alla de la llamada de dibujo inmediata.
        public static SolidBrush Brush(Color c)
        {
            sharedBrush.Color = c;
            return sharedBrush;
        }

        // Lapiz reutilizable; mismo contrato que Brush().
        public static Pen Pen(Color c, float width)
        {
            sharedPen.Color = c;
            sharedPen.Width = width;
            return sharedPen;
        }
    }
}
