// LA FRANJA DE ABAJO, POR TROZOS (rendimiento, 28 de septiembre de 2026; solo tarjeta grafica, las dos ramas).
//
// La franja de lo que suena (titulo, artista, genero, lo que sigue, el reloj, los creditos, la cola y la barra) es una
// imagen aparte que se rehace cuando cambia algo de lo que dice (UiBotSig). Con musica, lo que cambia cada segundo es el
// RELOJ ("1:47 / 4:12") y, a ratos, la barra de progreso. Por eso se rehacia la imagen ENTERA -todos los textos, las
// pastillas con sus portaditas- y se subia entera a la tarjeta, una vez por segundo.
//
// Ahora, si lo unico que cambio es el reloj o la barra, se repinta solo su recuadro y la tarjeta recibe solo ese trozo.
// Para que quede EXACTAMENTE igual que rehaciendola entera:
//   - se pinta todo como siempre y en el mismo orden, pero recortado al recuadro: dentro sale lo mismo que saldria
//     entera (los textos que se deslizan no se tocan: ver CapturaRecuadro);
//   - el recuadro se vacia con copia directa (transparente), sin depender de como trate Clear el recorte;
//   - fuera del recuadro no cambia nada: el reloj se dibuja dentro de su caja y la barra dentro de la suya (con margen
//     para su brillo). Si se mueven, o cambia cualquier otra cosa, cambia la firma fija y va entera, como siempre.
// La bateria 'franjareloj' lo compara pixel a pixel con la imagen rehecha entera, en muchas combinaciones.
#if ROCOLA_GPU
using System;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: el recuadro de la franja se vacia con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+

namespace Rocola
{
    public partial class Form1
    {
        private long uiBotSigFija = SigNada;         // UiBotSig sin el reloj ni la barra (la apunta UiBotSig; en atraccion, SigNada)
        private long uiBotFijaHecha = SigNada;       // la firma fija de la imagen que hay ahora
        private string uiBotRelojHecho;              // el reloj que tiene
        private double uiBotBarraHecha = double.NaN; // y la barra
        private int uiBotTopHecho = -1;              // y donde empieza
        private Rectangle uiBotZona = Rectangle.Empty; private int uiBotZonaVer = -1;
        internal Size tamanoFranjaPrueba = Size.Empty;   // solo las pruebas: un formulario sin encender no tiene tamano
        internal const int MargenReloj = 2, MargenBarra = 4;

        // La barra, como la pinta la franja: su valor EXACTO (-1: no va en esta imagen, la dibuja la tarjeta). Exacto y no
        // redondeado: la barra en neon pinta con el valor tal cual, y un recuadro que la dejara atras no quedaria igual que
        // la imagen rehecha entera. Su recuadro es chico: repintarla cada vez que avanza sale barato.
        private double BarraEnFranja()
        {
            if (progress == null || progresoFluido || (neonIntenso && progresoNeon)) return -1.0;
            return progress.Value;
        }

        // El trozo que hay que repintar si solo cambiaron el reloj o la barra; vacio: la imagen entera.
        private Rectangle ZonaFranja(int top, int w, int bh, long fija)
        {
            if (fija == SigNada || fija != uiBotFijaHecha || top != uiBotTopHecho) return Rectangle.Empty;
            Rectangle z = Rectangle.Empty;
            string reloj = (lblTime != null) ? lblTime.Text : null;
            if (!string.Equals(reloj, uiBotRelojHecho, StringComparison.Ordinal))
            {
                if (lblTime == null) return Rectangle.Empty;
                z = Rectangle.Inflate(lblTime.Bounds, MargenReloj, MargenReloj);
            }
            if (!BarraEnFranja().Equals(uiBotBarraHecha))
            {
                if (progress == null) return Rectangle.Empty;
                Rectangle b = Rectangle.Inflate(progress.Bounds, MargenBarra, MargenBarra);
                z = z.IsEmpty ? b : Rectangle.Union(z, b);
            }
            if (z.IsEmpty) return Rectangle.Empty;
            z.Offset(0, -top);
            z.Intersect(new Rectangle(0, 0, w, bh));
            return z;
        }

        // Lo que tiene la imagen despues de pintarla, y que trozo cambio (vacio: entera).
        private void ApuntaFranja(int top, long fija, Rectangle zona)
        {
            uiBotFijaHecha = fija; uiBotTopHecho = top;
            uiBotRelojHecho = (lblTime != null) ? lblTime.Text : null;
            uiBotBarraHecha = BarraEnFranja();
            uiBotZona = zona; uiBotZonaVer = uiBotVer;
        }

        // El trozo que cambio desde la version 'desde' (la que tiene la tarjeta). Solo se sabe de la de justo antes; de
        // cualquier otra, nada (vacio: se sube entera).
        private Rectangle UiBotCambioDesde(int desde)
        { return (desde == uiBotVer - 1 && uiBotZonaVer == uiBotVer) ? uiBotZona : Rectangle.Empty; }

        // LOS TEXTOS QUE SE DESLIZAN, EN EL REPINTADO POR RECUADRO. Una cinta solo se apunta si su caja cae dentro de lo que
        // se ve (CintasTexto.Add), y con el recorte al reloj las del titulo quedarian fuera: se borrarian. Aqui se apuntan en
        // un grupo aparte que se tira al acabar. Las de verdad no cambian -solo cambio el reloj o la barra, que nunca van
        // deslizandose-, y un texto que si se desliza y cae dentro del recuadro sigue sin pintarse en la imagen, igual que
        // al rehacerla entera.
        private IDisposable CapturaRecuadro()
        {
            if (!textosDeslizantes || !gpuEnabled) return new CapturaCintas(null);
            return new CintasDeUsarYTirar();
        }
        private sealed class CintasDeUsarYTirar : IDisposable
        {
            private readonly CintasTexto grupo = new CintasTexto();
            private readonly CapturaCintas captura;
            internal CintasDeUsarYTirar() { captura = new CapturaCintas(grupo); }
            public void Dispose() { captura.Dispose(); grupo.Dispose(); }
        }

        // Vacia el recuadro (transparente, copia directa) y deja recortado ahi todo lo que se pinte despues.
        private static void EmpiezaRecuadro(SKCanvas g, bool suave, bool medio, Rectangle zona)   // EN LINUX: el lienzo de Skia y el estado del que llama que pesa en el relleno (suavizado y medio pixel); Form1.Video.cs (ya traducido) la llama directo con su lienzo
        {
            g.Save(); g.ClipRect(new SKRect(zona.Left, zona.Top, zona.Right, zona.Bottom), SKClipOperation.Intersect, false);   // EN LINUX: g.SetClip(zona): un recorte de pixeles enteros, sin suavizar, en un Save del lienzo como lo pone la capa (quien lo llama no traia recorte que reemplazar)
            // EN LINUX: CompositingMode antes = g.CompositingMode: la copia va solo en el relleno de abajo (Pixel.Rellena), no en el lienzo: no hay nada que guardar
            bool copia = true;   // EN LINUX: g.CompositingMode = SourceCopy, en una variable
            using (SKPaint b = new SKPaint { Color = Color.Transparent.Sk() }) Pixel.Rellena(g, zona, b, suave, medio, copia);   // EN LINUX: SolidBrush(Transparent) y g.FillRectangle (Nativo/Pixel.cs)
            // EN LINUX: g.CompositingMode = antes: el lienzo no cambio (la copia era de ese relleno)
        }
    }

    internal sealed partial class GpuPanel
    {
        public Func<int, Rectangle> GetUiBotCambio;   // que trozo de la franja cambio desde una version (vacio: entera)

        // Sube la franja: si la tarjeta tiene la version de justo antes y el formulario sabe que trozo cambio, solo ese.
        private void SubeUiBot(Rocola.Dibujo2D.Bitmap bb)   // EN LINUX: la franja la hace Form1.Video.cs (GetUiBot) y la suben Upload y UpdateOverlayRegion (D3D9Star.cs), aun con el Bitmap de la capa: pasa tal cual
        {
            if (uiBotTex != IntPtr.Zero && GetUiBotCambio != null && bb.Width == uiBotW && bb.Height == uiBotH)
            {
                Rectangle zona = GetUiBotCambio(uiBotVer);
                if (!zona.IsEmpty && new Rectangle(0, 0, bb.Width, bb.Height).Contains(zona) && stars.UpdateOverlayRegion(uiBotTex, bb, zona))
                    return;
            }
            uiBotTex = Upload(uiBotTex, ref uiBotW, ref uiBotH, bb, out uiBotU, out uiBotV);
        }
    }
}
#endif
