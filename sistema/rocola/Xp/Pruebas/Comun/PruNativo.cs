// LO QUE LOS ARNESES USAN DEL DIBUJO EN EL PROGRAMA NUEVO (port nativo, oleada 8b, 13-11-2026): mapas y lienzos con SkiaSharp y las funciones
// nativas de Xp/Nativo, SIN nombrar la capa de GDI+ (Rocola.Dibujo2D) ni la de WinForms (Rocola.Ventanas), que se borran en N7. La referencia
// congelada compila PruCapa.cs (la misma clase Pru con la misma forma, sobre su capa): el arnes llama a Pru y no sabe en cual de las dos esta.
// Se compila solo si la carpeta Fuente tiene Nativo/ (el programa nuevo); ver los .csproj de los arneses.
// El lienzo de prueba (Lz) no imita a ningun Graphics: es un SKCanvas con el estado que el programa recibe como parametros (suavizado,
// modo de letra, copia, filtro, medio pixel y la matriz del HDC) y solo lo que los arneses hacen con el: limpiar, rellenar un rectangulo,
// mover, escalar, girar, recortar, sellar y decir como queda. Sus cuentas son las del Graphics de la capa (Dibujo2D.cs: Save, SetClip,
// SellaRecorte, Transform y ClipBounds), copiadas aqui como el arnes las usa.
using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;
using Rocola;
using Rocola.Nativo;
using SkiaSharp;

namespace Banco
{
    internal sealed class Lz : IDisposable
    {
        public readonly SKBitmap Mapa;
        public readonly SKCanvas C;
        public ModoSuave Suavizado = ModoSuave.None;
        public ModoLetra Letra = ModoLetra.SystemDefault;
        public ModoCopia Copia = ModoCopia.SourceOver;
        public ModoFiltro Filtro = ModoFiltro.Bilinear;
        public ModoPixel Pixel = ModoPixel.Default;
        private int baseCuenta;      // la cuenta de Skia antes del recorte (para poder cambiarlo)
        private bool conRecorte;
        private SKMatrix baseMatriz;
        public Lz(SKBitmap mapa) { Mapa = mapa; C = new SKCanvas(mapa); baseCuenta = C.SaveCount; baseMatriz = C.TotalMatrix; }
        public void Dispose() { C.Flush(); C.Dispose(); }
        public bool Suave { get { return Suavizado == ModoSuave.AntiAlias || Suavizado == ModoSuave.HighQuality; } }
        public bool Medio { get { return Pixel == ModoPixel.Half || Pixel == ModoPixel.HighQuality; } }
        public bool EsCopia { get { return Copia == ModoCopia.SourceCopy; } }
        public object Objeto { get { return null; } }   // (en la referencia, su Graphics)
        public SKMatrix Hdc { get { return baseMatriz; } }
        public void Limpia(Color c) { C.Clear(c.Sk()); }
        // un rectangulo de un solo color (FillRectangle con un pincel liso)
        public void Rellena(Color c, float x, float y, float w, float h)
        {
            using (SKPaint p = new SKPaint { Color = c.Sk() }) Rocola.Nativo.Pixel.Rellena(C, new RectangleF(x, y, w, h), p, Suave, Medio, EsCopia);
        }
        public void Traslada(float dx, float dy) { C.Translate(dx, dy); }
        public void Escala(float sx, float sy) { C.Scale(sx, sy); }
        public void Gira(float grados) { C.RotateDegrees(grados); }
        // el recorte de antes se cambia por este (CombineMode.Replace)
        public void Recorta(RectangleF r)
        {
            QuitaRecorte();
            baseCuenta = C.Save(); conRecorte = true;
            C.ClipRect(new SKRect(r.Left, r.Top, r.Right, r.Bottom), SKClipOperation.Intersect, false);
        }
        public void Recorta(Rectangle r) { Recorta((RectangleF)r); }
        // el recorte se intersecta con el de antes (CombineMode.Intersect)
        public void Intersecta(RectangleF r)
        {
            if (!conRecorte) { baseCuenta = C.Save(); conRecorte = true; }
            C.ClipRect(new SKRect(r.Left, r.Top, r.Right, r.Bottom), SKClipOperation.Intersect, false);
        }
        private void QuitaRecorte() { if (!conRecorte) return; RecorteLienzo.Quita(C, baseCuenta); conRecorte = false; }
        // (lo que Ventanas.Escritorio hace a un OnPaint) el recorte de ahora pasa a ser la base y la matriz de ahora la del HDC
        public void Sella() { conRecorte = false; baseCuenta = C.SaveCount; baseMatriz = C.TotalMatrix; }
        public RectangleF LimitesRecorte { get { return RecorteLienzo.Limites(C); } }
        // la transformacion del codigo sobre la del HDC (Graphics.Transform), vista en dos puntos
        public SKMatrix Transformacion { get { SKMatrix inv; return baseMatriz.TryInvert(out inv) ? inv.PreConcat(C.TotalMatrix) : C.TotalMatrix; } }
        // (0,0) y (1,1) pasados por la transformacion del codigo: asi miran los arneses la matriz
        public PointF[] Transformados() { SKMatrix m = Transformacion; SKPoint a = m.MapPoint(0, 0), b = m.MapPoint(1, 1); return new[] { new PointF(a.X, a.Y), new PointF(b.X, b.Y) }; }
        // (lo que el programa recibe) el lienzo y su estado, en el orden de EstadoLienzo: suave, letra, copia, filtro, medio, matriz del HDC
        public object[] Estado() { return new object[] { C, Suave, (AjusteLetra)(int)Letra, EsCopia, (Filtro)(int)Filtro, Medio, Hdc }; }
    }

    internal static class Pru
    {
        public const bool Capa = false;   // (esta es la del programa nuevo)
        public static readonly Type TMapa = typeof(SKBitmap);
        // el formato que XP le dio al mapa al crearlo (Image.PixelFormat): los puntos son siempre BGRA premultiplicado; el nombre solo se
        // apunta, como lo da la capa de la referencia
        private static readonly ConditionalWeakTable<SKBitmap, string> formatos = new ConditionalWeakTable<SKBitmap, string>();
        public static object NuevoMapa(int w, int h, string formato) { SKBitmap b = Mapas.Nuevo(w, h); formatos.AddOrUpdate(b, formato); return b; }
        public static object DeSk(SKBitmap b, string formato) { if (b != null) formatos.AddOrUpdate(b, formato); return b; }
        public static SKBitmap Sk(object o) { return o as SKBitmap; }
        // un mapa del programa (lo que devuelve un horno) como lo mira el arnes: tal cual, y si no tenia formato apuntado, el que se diga
        public static object Aprecia(object o, string formato) { SKBitmap b = (SKBitmap)o; string f; if (!formatos.TryGetValue(b, out f)) formatos.AddOrUpdate(b, formato); return b; }
        public static string Formato(object o) { string f; return formatos.TryGetValue((SKBitmap)o, out f) ? f : "Format32bppPArgb"; }
        public static void Suelta(object o) { IDisposable d = o as IDisposable; if (d != null) d.Dispose(); }
        public static void Pon(object mapa, int x, int y, Color c) { ((SKBitmap)mapa).SetPixel(x, y, c.Sk()); }
        // LockBits(zona, ReadOnly, Format32bppArgb): los puntos como los sube la tarjeta, sin premultiplicar (Nativo/Pixeles.cs)
        public static unsafe byte[] SinPremultiplicar(object o)
        {
            SKBitmap s = (SKBitmap)o; int paso = Pixeles.Paso(FormatoPuntos.Argb32, s.Width);
            byte[] r = new byte[paso * s.Height];
            fixed (byte* p = r) Pixeles.Lee(s, new Rectangle(0, 0, s.Width, s.Height), FormatoPuntos.Argb32, (IntPtr)p, paso);
            return r;
        }
        public static Lz Lienzo(object mapa) { return new Lz((SKBitmap)mapa); }
    }
}
