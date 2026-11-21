// UNA TIRA DE LETRAS EN SKIA SIN OBJETOS DE .NET (rapido-general, 21-11-2026). SKCanvas.DrawText(string, x, y, SKFont, SKPaint) de
// SkiaSharp 3.116 hace, en cada llamada: SKTextBlob.Create(texto, letra) -> CountGlyphs, un SKTextBlobBuilder, AllocateRun(letra, n, 0, 0)
// sin caja, GetGlyphs en el bufer de la tira y Build (SKTextBlob.GetObject), y despues DrawText(blob, x, y, pincel) y el Dispose de los dos.
// Son las mismas llamadas a libSkiaSharp que aqui (sk_font_text_to_glyphs para contar y para llenar, en UTF-16,
// sk_textblob_builder_new/alloc_run/make/delete, sk_canvas_draw_text_blob y sk_textblob_unref), pero cada envoltorio de .NET se apunta en el
// diccionario de objetos de SkiaSharp (con su candado, que comparten todos los hilos que dibujan) y es memoria nueva que el recolector tiene
// que limpiar: con la barra de letras y su lupa era el 70 % de la memoria nueva del hilo de la pantalla al hojear. Lo que se pinta es lo
// mismo: la misma tira, de la misma letra, en el mismo sitio, con el mismo pincel (lo comprueba el banco del dibujo).
using System;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Rocola.Nativo
{
    internal static unsafe class TiraSkia
    {
        private const string L = "libSkiaSharp";
        private const int Utf16 = 1;   // sk_text_encoding_t: UTF8 0, UTF16 1, UTF32 2, GLYPH_ID 3 (SKTextEncoding)
        [StructLayout(LayoutKind.Sequential)] private struct BuferTira { internal void* Glifos, Pos, Utf8, Grupos; }   // sk_textblob_builder_runbuffer_t
        [DllImport(L)] private static extern int sk_font_text_to_glyphs(IntPtr font, void* texto, IntPtr bytes, int codigo, ushort* glifos, int max);
        [DllImport(L)] private static extern IntPtr sk_textblob_builder_new();
        [DllImport(L)] private static extern void sk_textblob_builder_delete(IntPtr b);
        [DllImport(L)] private static extern void sk_textblob_builder_alloc_run(IntPtr b, IntPtr font, int n, float x, float y, void* caja, BuferTira* bufer);
        [DllImport(L)] private static extern IntPtr sk_textblob_builder_make(IntPtr b);
        [DllImport(L)] private static extern void sk_textblob_unref(IntPtr blob);
        [DllImport(L)] private static extern void sk_canvas_draw_text_blob(IntPtr lienzo, IntPtr blob, float x, float y, IntPtr pincel);

        // lo mismo que c.DrawText(s, x, y, f, p) (alineado a la izquierda)
        internal static void Dibuja(SKCanvas c, string s, float x, float y, SKFont f, SKPaint p)
        {
            if (c == null) throw new ArgumentNullException("canvas");
            if (f == null) throw new ArgumentNullException("font");
            if (p == null) throw new ArgumentNullException("paint");
            if (s == null) throw new ArgumentNullException("text");
            IntPtr fh = f.Handle;
            fixed (char* t = s)
            {
                IntPtr bytes = (IntPtr)(s.Length * 2);
                int n = sk_font_text_to_glyphs(fh, t, bytes, Utf16, null, 0);
                if (n <= 0) return;
                IntPtr b = sk_textblob_builder_new();
                IntPtr blob = IntPtr.Zero;
                try
                {
                    BuferTira bt;
                    sk_textblob_builder_alloc_run(b, fh, n, 0f, 0f, null, &bt);
                    sk_font_text_to_glyphs(fh, t, bytes, Utf16, (ushort*)bt.Glifos, n);
                    blob = sk_textblob_builder_make(b);
                }
                finally { sk_textblob_builder_delete(b); }
                if (blob == IntPtr.Zero) return;
                sk_canvas_draw_text_blob(c.Handle, blob, x, y, p.Handle);
                sk_textblob_unref(blob);
            }
            GC.KeepAlive(c); GC.KeepAlive(f); GC.KeepAlive(p);
        }
    }
}
