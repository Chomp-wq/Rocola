// EL PUENTE DEL DIBUJO, SOLO MIENTRAS DURA LA TRADUCCION (port nativo, paso D3 del dibujo, 8-11-2026). Unos archivos de XP ya dibujan con
// SkiaSharp directo y las funciones nativas (Xp/Nativo) y otros todavia con la capa que imita a GDI+ (Dibujo2D.cs); donde uno le pasa al
// otro un lienzo, un mapa, una letra o un formato, pasa por aqui. Nada de esto queda: se borra al final, con Dibujo2D.cs.
//   - Graphics -> SKCanvas y su estado (suavizado, modo de letra, copia, filtro, medio pixel y la matriz del HDC);
//   - SKCanvas -> Graphics con la matriz del HDC dada (GDI pinta con ella, no con la que lleve el lienzo) y el estado que tenia el que llama;
//   - Bitmap <-> SKBitmap sin hacerse dueno (los puntos son los mismos; solo los suelta quien los creo), y Entrega: el mapa de un horno
//     traducido para quien lo pide como Bitmap, que se hace su dueno;
//   - Font -> Letra (y una Letra -> Font con los mismos datos);
//   - StringFormat <-> FormatoTexto: el MISMO objeto, no una copia (lo que uno cambia, el otro lo ve). StringFormat.GenericTypographic es
//     FormatoTexto.TipograficoCompartido: EstadoReposoGpu.cs le cambia la alineacion y NoWrap y los demas lo heredan, esten traducidos o no.
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    // el estado que en XP llevaba el Graphics, a la vista (lo que las funciones nativas reciben como parametros)
    internal readonly struct EstadoLienzo
    {
        public readonly bool Suave, Copia, Medio; public readonly AjusteLetra Letra; public readonly Filtro Filtro; public readonly SKMatrix Hdc;
        public EstadoLienzo(bool suave, AjusteLetra letra, bool copia, Filtro filtro, bool medio, SKMatrix hdc) { Suave = suave; Letra = letra; Copia = copia; Filtro = filtro; Medio = medio; Hdc = hdc; }
    }

    // partial: cada grupo de la traduccion pone lo que le falte en su propio archivo (Plataforma/Puente.<grupo>.cs), sin tocar este
    internal static partial class PuenteDibujo
    {
        // ---- el lienzo
        internal static SKCanvas Lienzo(Graphics g) { return g.C; }
        internal static EstadoLienzo Estado(Graphics g)
        {
            return new EstadoLienzo(g.SmoothingMode == SmoothingMode.AntiAlias || g.SmoothingMode == SmoothingMode.HighQuality, (AjusteLetra)(int)g.TextRenderingHint,
                g.CompositingMode == CompositingMode.SourceCopy, (Filtro)(int)g.InterpolationMode,
                g.PixelOffsetMode == PixelOffsetMode.Half || g.PixelOffsetMode == PixelOffsetMode.HighQuality, g.MatrizHdc);
        }
        // un Graphics sobre el lienzo de un archivo ya traducido, para llamar a uno que no lo esta: con la matriz del HDC (identidad en un mapa
        // de bits) y, si se da, el estado del que llama. No suelta el lienzo. Lo que el llamado le cambie al estado sin devolverlo se lee
        // despues con Estado(g).
        internal static Graphics Graphics(SKCanvas c, SKMatrix hdc) { return new Graphics(c, hdc); }
        internal static Graphics Graphics(SKCanvas c, EstadoLienzo e)
        {
            Graphics g = new Graphics(c, e.Hdc);
            g.SmoothingMode = e.Suave ? SmoothingMode.AntiAlias : SmoothingMode.None; g.TextRenderingHint = (TextRenderingHint)(int)e.Letra;
            g.CompositingMode = e.Copia ? CompositingMode.SourceCopy : CompositingMode.SourceOver; g.InterpolationMode = (InterpolationMode)(int)e.Filtro;
            g.PixelOffsetMode = e.Medio ? PixelOffsetMode.Half : PixelOffsetMode.Default;
            return g;
        }

        // ---- los mapas: los mismos puntos, sin hacerse dueno
        internal static SKBitmap Mapa(Image i) { return i != null ? i.Sk : null; }
        // El MISMO Bitmap para el mismo SKBitmap mientras no se suelte: el codigo de XP que no esta traducido compara las imagenes por
        // identidad (D3D9Star.cs: winLastBmp, transLastBmp, selImgA... para no volver a subir la misma) y un Bitmap nuevo en cada llamada
        // le haria creer que cambio. Si XP suelta el prestado (Dispose: Sk = null) o lo pide con otro formato, se hace otro.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SKBitmap, Bitmap> prestados = new System.Runtime.CompilerServices.ConditionalWeakTable<SKBitmap, Bitmap>();
        internal static Bitmap Bitmap(SKBitmap b, PixelFormat formato = PixelFormat.Format32bppPArgb)
        {
            if (b == null) return null;
            lock (prestados)
            {
                Bitmap w;
                if (prestados.TryGetValue(b, out w) && w.Sk != null && w.PixelFormat == formato) return w;
                w = new Bitmap(b, formato) { Ajeno = true };
                prestados.AddOrUpdate(b, w);
                return w;
            }
        }
        // un mapa que hizo un archivo ya traducido (los hornos, D4) para uno que no lo esta y lo pide como Bitmap (Upload y
        // CreateTextureFromBitmap de D3D9Star.cs, un TextureBrush de GDI+): el Bitmap se hace su DUENO (al soltarlo se sueltan los puntos) y
        // lleva el formato con que XP lo creaba (los hornos: Format32bppArgb), como new Bitmap(w, h, formato)
        internal static Bitmap Entrega(SKBitmap b, PixelFormat formato = PixelFormat.Format32bppArgb)
        {
            return b == null ? null : new Bitmap(b, formato) { TraeAlfa = Image.IsAlphaPixelFormat(formato) };
        }

        // ---- las letras
        internal static Letra Letra(Font f) { return f != null ? f.L : null; }
        internal static Font Font(Letra l)
        {
            if (l == null) return null;
            FontStyle st = (l.Negrita ? FontStyle.Bold : 0) | (l.Cursiva ? FontStyle.Italic : 0) | (l.Subrayada ? FontStyle.Underline : 0);
            return new Font(new FontFamily(l.Familia, true), l.Tamano, st, l.UnidadPixel ? GraphicsUnit.Pixel : GraphicsUnit.Point);
        }

        // ---- los formatos: el mismo objeto
        internal static FormatoTexto Formato(StringFormat sf) { return sf != null ? sf.F : null; }
        internal static StringFormat StringFormat(FormatoTexto f)
        {
            if (f == null) return null;
            if (ReferenceEquals(f, FormatoTexto.TipograficoCompartido)) return Rocola.Dibujo2D.StringFormat.GenericTypographic;
            if (ReferenceEquals(f, FormatoTexto.NormalCompartido)) return Rocola.Dibujo2D.StringFormat.GenericDefault;
            return new StringFormat(f);
        }

        // ---- los pinceles (para un archivo traducido que recibe un Brush de uno que no lo esta: PulidoGpu devuelve Brush): su color o su
        // shader, como lo pinta GDI+ con ese medio pixel
        internal static SKPaint Pincel(Brush b, bool medio) { SKPaint p = new SKPaint(); b.Pon(p, !medio); return p; }
    }
}
