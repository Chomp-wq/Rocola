// EL PUENTE DEL GRUPO «VISTAS-B», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026). La segunda mitad de
// Form1.Vistas.cs (HeaderView: el encabezado de la seccion; PastillaView: las capsulas de los creditos y de la cola; GeneroView: la pastilla
// del genero; ProgressView: la barra de avance; SongListView: la lista de canciones de un disco y sus hornos) pasa a dibujar con SkiaSharp
// directo y las funciones nativas (Xp/Nativo). Donde la llama, o donde llama ella a, codigo que todavia dibuja con la capa que imita a GDI+
// (Dibujo2D.cs), pasa por aqui. La regla: el puente va del lado de lo que no esta traducido, y los que llaman no cambian. Nada de esto queda:
// cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al final, con Dibujo2D.cs y Puente.cs (PORT_vistas-b.md dice
// cual es cual).
//   - Las firmas de XP de RenderContent (con un Graphics) de las cinco vistas, para sus OnPaint (WinForms), DrawCtrlOverlay (Form1.Video.cs),
//     EnSeco (Form1.Portadas.cs, PrecargaPaginaGpu.cs) y, la de la capsula, RenderContentGpu (DiscoVivoGpu.cs): el lienzo del Graphics (con
//     su matriz y su recorte) y su estado; despues, lo que XP le deja puesto al Graphics (el suavizado AntiAlias, con las mismas condiciones).
//   - PastillaView.Ancho con una FontFamily (Form1.cs, Form1.Datos.cs): la familia por su nombre.
//   - Lo que la traducida le pide a lo que no lo esta: AnchoTexto (Form1.Ajustes.cs) mide con un Graphics, o sin el, y una Font.
//   - SueltaMapasVistasB: SueltaBitmaps (Form1.Pared.cs) para un arreglo de SKBitmap.
using System;
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        // ---- LO QUE LA TRADUCIDA LE PIDE A LO QUE AUN NO LO ESTA: AnchoTexto (Form1.Ajustes.cs)
        // con un Graphics: solo mide (MeasureString con su modo de letra y un formato suyo), asi que basta uno sobre el mismo lienzo con ese modo
        // (lo demas, lo de fabrica de un Graphics de un mapa de bits; no suelta el lienzo ni le cambia nada) y la Font de esa Letra (los mismos
        // datos: familia, tamano, unidad y estilo), soltada al acabar. Se quita cuando AnchoTexto reciba (AjusteLetra, string, Letra).
        private static int AnchoTextoVistasB(SKCanvas g, AjusteLetra ajuste, string t, Rocola.Nativo.Letra f)
        {
            using (Graphics gg = PuenteDibujo.Graphics(g, new EstadoLienzo(false, ajuste, false, Filtro.Bilineal, false, SKMatrix.Identity)))
            using (Font ff = PuenteDibujo.Font(f))
                return AnchoTexto(gg, t, ff);
        }
        // sin Graphics (PastillaView.Ancho, el acomodo): la Font de esa Letra, soltada al acabar
        private static int AnchoTextoVistasB(string t, Rocola.Nativo.Letra f)
        {
            using (Font ff = PuenteDibujo.Font(f)) return AnchoTexto(t, ff);
        }
#if ROCOLA_GPU
        // DiscoVivoGpu.cs (grupo pantalla-viva, ya juntado) mide con AnchoTextoPantallaViva(lienzo, modo de letra, texto, Font) y le pasaba fCola,
        // la letra de las marcas de la lista, que era una Font de Form1.Vistas.cs: ahora es una Letra, y sus cinco llamadas no cambian.
        private static int AnchoTextoPantallaViva(SKCanvas g, AjusteLetra ajuste, string t, Rocola.Nativo.Letra f) { return AnchoTextoVistasB(g, ajuste, t, f); }
#endif
        // ---- SueltaBitmaps (Form1.Pared.cs, aun sin traducir: pide Bitmap[] de la capa) para las cifras horneadas de la lista, que ya son
        // SKBitmap: la misma cuenta. Se quita cuando SueltaBitmaps reciba SKBitmap[].
        private static void SueltaMapasVistasB(ref SKBitmap[] images)
        {
            SKBitmap[] old = images; images = null;
            if (old != null) for (int i = 0; i < old.Length; i++) if (old[i] != null) old[i].Dispose();
        }

        // ---- LAS FIRMAS DE XP DE RenderContent (con un Graphics). Cada una pasa a la traducida el lienzo del Graphics y lo que pesa de su
        // estado, y despues le pone al Graphics lo que XP le deja puesto: SmoothingMode = AntiAlias, si la vista llego a ponerlo (las mismas
        // cuentas de sus primeras lineas). Lo demas que las vistas cambian (el filtro de las portaditas, los Save/Restore) lo devuelven ellas.
        private partial class HeaderView
        {
            public void RenderContent(Graphics g)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContent(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Medio);
                if (!(Width < 8 || Height < 8)) g.SmoothingMode = SmoothingMode.AntiAlias;
            }
        }
        private partial class PastillaView
        {
            public void RenderContent(Graphics g)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContent(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Filtro, e.Medio);
                if (!(Width < 8 || Height < 8)) g.SmoothingMode = SmoothingMode.AntiAlias;
            }
#if ROCOLA_GPU
            // RenderContentGpu (DiscoVivoGpu.cs: pone la bandera de la llegada y llama a RenderContent), para DrawCtrlOverlay (Form1.Video.cs)
            public void RenderContentGpu(Graphics g)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContentGpu(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Filtro, e.Medio);
                if (!(Width < 8 || Height < 8)) g.SmoothingMode = SmoothingMode.AntiAlias;
            }
#endif
            // Ancho con la FontFamily de la letra del Form (Form1.cs, Form1.Datos.cs): la familia por su nombre; la nula es la generica sin
            // remates, como la toma new Font(null, ...) de la capa
            public static int Ancho(string etiq, string valor, int alto, FontFamily fam, bool conPortadas)
            { return Ancho(etiq, valor, alto, fam != null ? fam.Name : Letras.SinRemates, conPortadas); }
        }
        private partial class GeneroView
        {
            public void RenderContent(Graphics g)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContent(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Medio);
                if (!(Width < 6 || Height < 6 || texto.Length == 0)) g.SmoothingMode = SmoothingMode.AntiAlias;
            }
        }
        private partial class ProgressView
        {
            public void RenderContent(Graphics g)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContent(PuenteDibujo.Lienzo(g), e.Copia, e.Medio);
                g.SmoothingMode = SmoothingMode.AntiAlias;   // (lo pone antes de mirar su medida)
            }
        }
        private partial class SongListView
        {
            // (no le cambia nada al Graphics: copia la pagina horneada, que pinta PintaPagina en su propio lienzo)
            public void RenderContent(Graphics g)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContent(PuenteDibujo.Lienzo(g), e.Copia, e.Filtro, e.Medio);
            }
        }
    }
}
