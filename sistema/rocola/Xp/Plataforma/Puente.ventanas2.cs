// EL PUENTE DE VENTANAS2 (port nativo, paso C: Ctrl+F3), SOLO MIENTRAS DURA LA TRADUCCION. Form1.Ajustes.cs ya mide el texto y pinta el
// aviso de siempre con SkiaSharp y las funciones nativas (Xp/Nativo); aqui queda, para los archivos que todavia no estan traducidos y las
// llaman con un Graphics, una Font o un StringFormat de la capa (Form1.Vistas.cs, DiscoVivoGpu.cs, Form1.cs y los puentes de otros grupos),
// la FIRMA DE XP de cada una: saca del Graphics lo unico que la funcion miraba de el (el modo de letra), pasa la letra y el formato por el
// puente (los mismos datos; el formato, el MISMO objeto) y llama a la traducida. Regla de PORT_DIBUJO.md: el puente va del lado de lo que
// todavia no esta traducido. Cuando esos archivos se traduzcan llamaran directo a las de Form1.Ajustes.cs y esto se borra, con Puente.cs y
// Dibujo2D.cs.
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        // AnchoTexto(string, Font) de XP (Form1.Ajustes.cs:320): la misma medida sin un Graphics a mano (la pide Form1.Vistas.cs:1565 con la
        // Font de la lista); sin letra o sin texto, 0, como alli. Se va cuando Form1.Vistas.cs mida con una Letra.
        private static int AnchoTexto(string t, Font f)
        {
            if (string.IsNullOrEmpty(t) || f == null) return 0;
            return AnchoTexto(t, PuenteDibujo.Letra(f));
        }
        // AnchoToast(string, Font) de XP (Form1.Ajustes.cs:1834): la pide el aviso sin tarjeta de Form1.cs con la letra de lblToast
        internal static int AnchoToast(string text, Font font)
        {
            return AnchoToast(text, PuenteDibujo.Letra(font));
        }
        // new Region(GraphicsPath) de XP con el SKPath que da ya RoundRect (Form1.Ajustes.cs): Form1.cs:2270 le da a lblToast (una etiqueta de
        // la capa, aun sin traducir) su forma por Control.Region, que es de la capa. La misma copia del trazo que hace la Region de la capa con
        // un GraphicsPath (los mismos puntos y el mismo relleno par-impar); quien llama suelta su SKPath como soltaba el GraphicsPath. Se va en
        // W6, cuando el aviso sin tarjeta deje de ser un Control de la capa.
        private static Region RegionDeRuta(SKPath p)
        {
            Region r = new Region(RectangleF.Empty);
            r.P.Dispose();
            r.P = new SKPath(p);
            return r;
        }
    }
}
