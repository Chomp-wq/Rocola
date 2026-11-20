// LOS PUNTOS SIN PREMULTIPLICAR (LockBits de XP en Format32bppArgb, Format24bppRgb y Format32bppRgb), NATIVOS (port nativo, el dibujo;
// 8-11-2026). Los mapas de la rocola son SKBitmap Bgra8888 premultiplicados (como Format32bppPArgb: ahi XP lee y escribe los puntos tal cual,
// con GetPixels y RowBytes, y NotifyPixelsChanged si escribio); cuando XP los pide sin premultiplicar (para subirlos a la tarjeta, para
// analizarlos o para escribirlos punto a punto) se convierten con estas tablas: sin premultiplicar = min(255, c*255/a), premultiplicar =
// c*a/255, los dos truncando (revision del 8-11-2026). Los algoritmos son los del Dibujo2D.cs de la capa de la transicion (ya borrada; sigue en la referencia) (LockBits y UnlockBits), movidos
// aqui. OJO: un mapa que XP crea Format32bppArgb guarda en GDI+ el color tal cual y aqui premultiplicado (diferencia (a) de PORT_DIBUJO.md).
using System;
using System.Drawing;
using System.Runtime.Intrinsics;
using SkiaSharp;

namespace Rocola.Nativo
{
    // como quiere XP los puntos: BGRA sin premultiplicar (Format32bppArgb), BGR (Format24bppRgb, filas de 4 en 4 bytes) o BGRX con el alfa a
    // 255 (Format32bppRgb)
    public enum FormatoPuntos { Argb32 = 0, Rgb24 = 1, Rgb32 = 2 }

    public static unsafe class Pixeles
    {
        private static readonly byte[] tablaQuita = Tabla(true), tablaPon = Tabla(false);
        private static byte[] Tabla(bool quita)
        {
            byte[] t = new byte[65536];
            for (int a = 0; a < 256; a++)
                for (int c = 0; c < 256; c++)
                    t[(a << 8) | c] = quita ? (byte)(a == 0 || a == 255 ? c : Math.Min(255, c * 255 / a)) : (byte)(c * a / 255);
            return t;
        }
        // el paso (bytes por fila) de unos puntos de ese formato y ese ancho, como el Stride de GDI+ (de 4 en 4 bytes)
        public static int Paso(FormatoPuntos f, int ancho) { int bpp = f == FormatoPuntos.Rgb24 ? 3 : 4; return ((ancho * bpp) + 3) & ~3; }
        // los puntos de 'zona' de 'b', convertidos, en 'destino' (fila a fila con 'paso' bytes)
        public static void Lee(SKBitmap b, Rectangle zona, FormatoPuntos f, IntPtr destino, int paso)
        {
            int rb = b.RowBytes; byte* o = (byte*)b.GetPixels();
            int bpp = f == FormatoPuntos.Rgb24 ? 3 : 4;
            bool rgb = f == FormatoPuntos.Rgb32;
            Rectangle r = zona;
            fixed (byte* q = tablaQuita)
                for (int y = 0; y < r.Height; y++)
                {
                    uint* s = (uint*)(o + (long)(r.Y + y) * rb + (long)r.X * 4);
                    byte* t = (byte*)destino + (long)y * paso;
                    if (bpp == 4)
                    {
                        uint* tt = (uint*)t;
                        // (rapido-caratulas) de cuatro en cuatro mientras los cuatro son opacos o del todo transparentes (casi todo en una
                        // portada o en la interfaz): esos van tal cual (con el alfa a 255 en Rgb32), lo mismo que la tabla; los demas, uno a uno
                        int x = Vector128.IsHardwareAccelerated ? CuatroTalCual(s, tt, r.Width, rgb) : 0;
                        for (; x < r.Width; x++)
                        {
                            uint v = s[x], a = v >> 24;
                            if (a != 255 && a != 0) { byte* qa = q + (a << 8); v = (a << 24) | ((uint)qa[(v >> 16) & 255] << 16) | ((uint)qa[(v >> 8) & 255] << 8) | qa[v & 255]; }
                            tt[x] = rgb ? v | 0xFF000000u : v;
                        }
                    }
                    else
                        for (int x = 0; x < r.Width; x++)
                        {
                            uint v = s[x]; byte* qa = q + ((v >> 24) << 8);
                            t[x * 3] = qa[v & 255]; t[x * 3 + 1] = qa[(v >> 8) & 255]; t[x * 3 + 2] = qa[(v >> 16) & 255];
                        }
                }
        }
        // los puntos de 'origen' (fila a fila con 'paso' bytes, en ese formato), premultiplicados, en 'zona' de 'b'
        public static void Escribe(SKBitmap b, Rectangle zona, FormatoPuntos f, IntPtr origen, int paso)
        {
            int bpp = f == FormatoPuntos.Rgb24 ? 3 : 4; Rectangle r = zona;
            byte* o = (byte*)b.GetPixels(); int rb = b.RowBytes;
            bool rgb = f == FormatoPuntos.Rgb32;
            fixed (byte* pp = tablaPon)
                for (int y = 0; y < r.Height; y++)
                {
                    uint* t = (uint*)(o + (long)(r.Y + y) * rb + (long)r.X * 4);
                    byte* s = (byte*)origen + (long)y * paso;
                    if (bpp == 4)
                    {
                        uint* ss = (uint*)s;
                        int x = Vector128.IsHardwareAccelerated ? CuatroOpacos(ss, t, r.Width, rgb) : 0;   // (rapido-caratulas) los opacos, tal cual
                        for (; x < r.Width; x++)
                        {
                            uint v = ss[x]; if (rgb) v |= 0xFF000000u;
                            uint a = v >> 24;
                            if (a != 255) { byte* pa = pp + (a << 8); v = (a << 24) | ((uint)pa[(v >> 16) & 255] << 16) | ((uint)pa[(v >> 8) & 255] << 8) | pa[v & 255]; }
                            t[x] = v;
                        }
                    }
                    else
                        for (int x = 0; x < r.Width; x++) t[x] = 0xFF000000u | ((uint)s[x * 3 + 2] << 16) | ((uint)s[x * 3 + 1] << 8) | s[x * 3];
                }
            b.NotifyPixelsChanged();
        }
        // (rapido-caratulas, 20-11-2026) LOS PUNTOS QUE LA TABLA DEJA IGUAL, DE CUATRO EN CUATRO. Sin premultiplicar (Lee): un punto con alfa
        // 255 o 0 sale tal cual (la tabla ni se mira); premultiplicar (Escribe): uno con alfa 255 sale tal cual (con alfa 0 la tabla lo deja en
        // 0: ese no). Se copian de cuatro en cuatro los grupos en que los cuatro lo cumplen; los demas puntos, con la tabla: los mismos bytes que
        // punto a punto (Pruebas/RapidoCaratulas/PixelesIgual). Cada una hace los grupos de cuatro enteros de la fila y devuelve hasta donde
        // llego (lo que queda, menos de cuatro, lo hace el bucle de siempre).
        private static int CuatroTalCual(uint* s, uint* t, int ancho, bool rgb)
        {
            int x = 0;
            Vector128<uint> alfa = Vector128.Create(0xFF000000u), pon = rgb ? alfa : Vector128<uint>.Zero;
            fixed (byte* q = tablaQuita)
                while (x + 4 <= ancho)
                {
                    Vector128<uint> v = Vector128.Load(s + x), a = v & alfa;
                    if ((Vector128.Equals(a, alfa) | Vector128.Equals(a, Vector128<uint>.Zero)) == Vector128<uint>.AllBitsSet) { (v | pon).Store(t + x); x += 4; continue; }
                    for (int k = 0; k < 4; k++, x++)
                    {
                        uint u = s[x], au = u >> 24;
                        if (au != 255 && au != 0) { byte* qa = q + (au << 8); u = (au << 24) | ((uint)qa[(u >> 16) & 255] << 16) | ((uint)qa[(u >> 8) & 255] << 8) | qa[u & 255]; }
                        t[x] = rgb ? u | 0xFF000000u : u;
                    }
                }
            return x;
        }
        private static int CuatroOpacos(uint* s, uint* t, int ancho, bool rgb)
        {
            int x = 0;
            Vector128<uint> alfa = Vector128.Create(0xFF000000u), pon = rgb ? alfa : Vector128<uint>.Zero;
            fixed (byte* pp = tablaPon)
                while (x + 4 <= ancho)
                {
                    Vector128<uint> v = Vector128.Load(s + x) | pon;
                    if (Vector128.EqualsAll(v & alfa, alfa)) { v.Store(t + x); x += 4; continue; }
                    for (int k = 0; k < 4; k++, x++)
                    {
                        uint u = s[x]; if (rgb) u |= 0xFF000000u;
                        uint au = u >> 24;
                        if (au != 255) { byte* pa = pp + (au << 8); u = (au << 24) | ((uint)pa[(u >> 16) & 255] << 16) | ((uint)pa[(u >> 8) & 255] << 8) | pa[u & 255]; }
                        t[x] = u;
                    }
                }
            return x;
        }
        // lo de siempre de XP: LockBits(ReadOnly o WriteOnly, Format32bppArgb)
        public static void LeeSinPremultiplicar(SKBitmap b, Rectangle zona, IntPtr destino, int paso) { Lee(b, zona, FormatoPuntos.Argb32, destino, paso); }
        public static void EscribeSinPremultiplicar(SKBitmap b, Rectangle zona, IntPtr origen, int paso) { Escribe(b, zona, FormatoPuntos.Argb32, origen, paso); }
        // el mapa entero desde enteros ARGB (como XP los copia con Marshal.Copy a un LockBits(WriteOnly, Format32bppArgb))
        public static void EscribeSinPremultiplicar(SKBitmap b, int[] puntos, int ancho)
        {
            fixed (int* p = puntos) Escribe(b, new Rectangle(0, 0, ancho, puntos.Length / ancho), FormatoPuntos.Argb32, (IntPtr)p, ancho * 4);
        }
        // para la subida a la tarjeta (D3D9Star.cs CreateTextureFromBitmap, TexturasCompatiblesGpu.cs): sin premultiplicar a una memoria
        public static void CopiaSinPremultiplicar(SKBitmap b, Rectangle zona, IntPtr destino, int paso) { Lee(b, zona, FormatoPuntos.Argb32, destino, paso); }
    }
}
