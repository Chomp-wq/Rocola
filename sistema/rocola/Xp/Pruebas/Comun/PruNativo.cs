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
        // DrawString con un pincel liso (el texto de GDI+, Nativo/TextoGdiPlus.cs, con el estado del lienzo)
        public void Escribe(string s, Fte f, Color c, RectangleF r, Fmt sf)
        {
            if (string.IsNullOrEmpty(s) || f == null) return;
            using (SKPaint p = new SKPaint { Color = c.Sk() })
                TextoGdiPlus.Escribe(C, s, f.L, (AjusteLetra)(int)Letra, p, r, sf != null ? sf.F : FormatoTexto.SinFormato, EsCopia, Medio);
        }
        // un trazo (el SKPath que da ContornoRedondo) relleno y con su linea, con este estado (Nativo/Pixel.cs)
        public void RellenaRuta(object ruta, Color c) { using (SKPaint p = new SKPaint { Color = c.Sk() }) Rocola.Nativo.Pixel.Rellena(C, (SKPath)ruta, p, Suave, Medio, EsCopia); }
        public void TrazaRuta(object ruta, Color c, float ancho) { using (SKPaint p = new SKPaint { Color = c.Sk(), StrokeWidth = ancho }) Rocola.Nativo.Pixel.Traza(C, (SKPath)ruta, p, Suave, Medio, EsCopia); }
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
        // Graphics.Save y Restore: el estado (los modos, el recorte, la matriz del HDC y la cuenta de Skia) para volver a el
        private sealed class Guardado { public int Cuenta, BaseCuenta; public bool ConRecorte; public SKMatrix Base; public ModoSuave S; public ModoLetra T; public ModoCopia K; public ModoFiltro I; public ModoPixel P; }
        public object Guarda()
        {
            return new Guardado { Cuenta = C.Save(), S = Suavizado, T = Letra, K = Copia, I = Filtro, P = Pixel, Base = baseMatriz };
        }
        public void Restaura(object o)
        {
            Guardado s = (Guardado)o; if (s == null) return;
            C.RestoreToCount(s.Cuenta);
            Suavizado = s.S; Letra = s.T; Copia = s.K; Filtro = s.I; Pixel = s.P; baseMatriz = s.Base;
            conRecorte = false; baseCuenta = C.SaveCount;
        }
        // (0,0), (1,0) y (0,1) pasados por la transformacion: la matriz en seis numeros (los de dos ejes y la traslacion)
        public float[] Matriz6() { SKMatrix m = Transformacion; SKPoint p0 = m.MapPoint(0, 0), p1 = m.MapPoint(1, 0), p2 = m.MapPoint(0, 1); return new[] { p1.X - p0.X, p1.Y - p0.Y, p2.X - p0.X, p2.Y - p0.Y, p0.X, p0.Y }; }
        // (0,0) y (1,1) pasados por la transformacion del codigo: asi miran los arneses la matriz
        public PointF[] Transformados() { SKMatrix m = Transformacion; SKPoint a = m.MapPoint(0, 0), b = m.MapPoint(1, 1); return new[] { new PointF(a.X, a.Y), new PointF(b.X, b.Y) }; }
        // (lo que el programa recibe) el lienzo y su estado, en el orden de EstadoLienzo: suave, letra, copia, filtro, medio, matriz del HDC
        public object[] Estado() { return new object[] { C, Suave, (AjusteLetra)(int)Letra, EsCopia, (Filtro)(int)Filtro, Medio, Hdc }; }
    }

    // LA LETRA PARA LOS ARNESES: la familia como la resuelve GDI+ (una que no esta cae en la generica sin remates), el tamano, el estilo (los
    // bits de FontStyle: 1 negrita, 2 cursiva, 4 subrayada) y la unidad (Point o Pixel); es una Letra de Nativo/Letras.cs
    internal sealed class Fte : IDisposable
    {
        public readonly Letra L;
        public Fte(string familia, float tam, int estilo = 0, string unidad = "Point")
        {
            string fam = Letras.Resuelve(familia); bool b = (estilo & 1) != 0, i = (estilo & 2) != 0, u = (estilo & 4) != 0;
            L = unidad == "Pixel" ? Letra.EnPixeles(fam, tam, b, i, u) : Letra.EnPuntos(fam, tam, b, i, u);
        }
        private Fte(Letra l) { L = l; }
        // una letra que ya hizo el programa (una Letra), mirada como esta: la misma, y soltarla suelta la letra
        public static Fte De(object o) { return o == null ? null : new Fte((Letra)o); }
        public object Obj { get { return L; } }
        public string Nombre { get { return L.Familia; } }
        public float Tamano { get { return L.Tamano; } }
        public string Unidad { get { return L.UnidadPixel ? "Pixel" : "Point"; } }
        public bool Negrita { get { return L.Negrita; } }
        // el estilo como lo escribe FontStyle.ToString()
        public string Estilo { get { return Pru.NombreEstilo((L.Negrita ? 1 : 0) | (L.Cursiva ? 2 : 0) | (L.Subrayada ? 4 : 0)); } }
        public float AltoRenglon { get { return L.AltoRenglon; } }
        public int Alto { get { return L.AltoEntero; } }
        public float Puntos { get { return L.Puntos; } }
        public void Dispose() { L.Dispose(); }
    }

    // EL FORMATO DE TEXTO PARA LOS ARNESES: un FormatoTexto (Nativo/TextoGdiPlus.cs); los numeros son los de GDI+ (alineacion 0 a 2, banderas,
    // corte 0 a 5). Tipografico y Generico son los dos compartidos, un solo objeto cada uno.
    internal sealed class Fmt
    {
        public readonly FormatoTexto F;
        private Fmt(FormatoTexto f) { F = f; }
        public Fmt() { F = new FormatoTexto(); }
        public Fmt(int banderas) { F = new FormatoTexto((BanderasTexto)banderas); }
        private static readonly Fmt tip = new Fmt(FormatoTexto.TipograficoCompartido), gen = new Fmt(FormatoTexto.NormalCompartido);
        public static Fmt Tipografico { get { return tip; } }
        public static Fmt Generico { get { return gen; } }
        public object Obj { get { return F; } }
        public ModoAlinea Horizontal { get { return (ModoAlinea)(int)F.Horizontal; } set { F.Horizontal = (Alinea)(int)value; } }
        public ModoAlinea Vertical { get { return (ModoAlinea)(int)F.Vertical; } set { F.Vertical = (Alinea)(int)value; } }
        public int Banderas { get { return (int)F.Banderas; } set { F.Banderas = (BanderasTexto)value; } }
        public ModoCorte Corte { get { return (ModoCorte)(int)F.Corte; } set { F.Corte = (CorteTexto)(int)value; } }
        // un formato que ya hizo el programa (un FormatoTexto) mirado como este; el mismo objeto
        public static Fmt De(object o) { if (o == null) return null; FormatoTexto f = (FormatoTexto)o; return ReferenceEquals(f, FormatoTexto.TipograficoCompartido) ? tip : ReferenceEquals(f, FormatoTexto.NormalCompartido) ? gen : new Fmt(f); }
        public bool EsTipografico { get { return ReferenceEquals(F, FormatoTexto.TipograficoCompartido); } }
    }

    internal static class Pru
    {
        public const bool Capa = false;   // (esta es la del programa nuevo)
        public static readonly Type TMapa = typeof(SKBitmap);
        public static readonly Type TLienzo = typeof(SKCanvas);   // (lo que reciben las traducidas; la referencia recibe un Graphics)
        // el formato que XP le dio al mapa al crearlo (Image.PixelFormat): los puntos son siempre BGRA premultiplicado; el nombre solo se
        // apunta, como lo da la capa de la referencia
        private static readonly ConditionalWeakTable<SKBitmap, string> formatos = new ConditionalWeakTable<SKBitmap, string>();
        public static object NuevoMapa(int w, int h, string formato) { SKBitmap b = Mapas.Nuevo(w, h); formatos.AddOrUpdate(b, formato); return b; }
        public static object DeSk(SKBitmap b, string formato) { if (b != null) formatos.AddOrUpdate(b, formato); return b; }
        public static SKBitmap Sk(object o) { return o as SKBitmap; }
        // un mapa de la capa de GDI+ (solo la referencia los tiene: aqui no hay ninguno) y lo que XP miraba de su formato
        public static bool EsCapa(object o) { return false; }
        public static bool TieneAlfa(string formato) { return formato == "Format32bppPArgb" || formato == "Format32bppArgb" || formato == "Format8bppIndexed"; }
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
        // FontStyle.ToString() de los bits que XP usa
        public static string NombreEstilo(int e) { if (e == 0) return "Regular"; string r = ""; if ((e & 1) != 0) r += "Bold"; if ((e & 2) != 0) r += (r.Length > 0 ? ", " : "") + "Italic"; if ((e & 4) != 0) r += (r.Length > 0 ? ", " : "") + "Underline"; if ((e & 8) != 0) r += (r.Length > 0 ? ", " : "") + "Strikeout"; return r; }
        // el pincel liso que los arneses le dan a lo traducido: en el programa nuevo un SKPaint de ese color (soltarlo con Suelta)
        public static object Brocha(Color c) { return new SKPaint { Color = c.Sk() }; }
        public static int Marcas(object o) { return TieneAlfa(Formato(o)) ? 2 : 0; }   // (Image.Flags de la capa: HasAlpha, 2, si el formato trae alfa)
        public static SKPaint PincelLiso(Color c) { return new SKPaint { Color = c.Sk() }; }
        // los puntos, el relleno y la caja de un trazo (lo que el arnes de fondo escribe de el)
        public static string InfoRuta(object ruta, Func<RectangleF, string> r) { SKPath p = (SKPath)ruta; return " puntos=" + p.PointCount + " tipo=" + p.FillType + " caja=" + r(new RectangleF(p.Bounds.Left, p.Bounds.Top, p.Bounds.Width, p.Bounds.Height)); }
        // lo que hace un control de la referencia con OnPaint (solo ella tiene controles): aqui no hay
        public static void OnPaint(System.Reflection.MethodInfo onPaint, object control, Lz g, Rectangle r) { throw new InvalidOperationException("el programa nuevo no tiene OnPaint"); }
        // LockBits(WriteOnly, Format32bppArgb) de un mapa entero: los puntos sin premultiplicar (un uint ARGB cada uno, fila por fila) pasan al mapa
        public static unsafe void EscribePuntos(object mapa, uint[] argb)
        {
            SKBitmap s = (SKBitmap)mapa;
            fixed (uint* p = argb) Pixeles.Escribe(s, new Rectangle(0, 0, s.Width, s.Height), FormatoPuntos.Argb32, (IntPtr)p, s.Width * 4);
        }
        public static Lz Lienzo(object mapa) { return new Lz((SKBitmap)mapa); }
    }
}
