// EL ANFITRION DE ROCOLA OS (traduccion de XP, fase 1): lo que en XP hace Windows con la pantalla. Abre la pantalla de verdad (KMS, con
// OpenGL ES 2 en el hilo de la rocola) o una fuera de pantalla (pruebas y capturas: ROCOLA_PANTALLA=fuera:1024x768, o si no hay tarjeta),
// y compone en cada cuadro las capas del escritorio, de abajo arriba: lo pintado con Dibujo2D (en texturas que solo se vuelven a subir
// cuando la capa cambia) y lo que pinta la tarjeta (el fondo del dispositivo de Direct3DaGL de cada control «Nativa»: el GpuPanel de XP).
// Despues lo presenta (KMS: cambio de pagina con el barrido).
//   ROCOLA_CAPTURA=S:ruta.png[,S2:ruta2.png...]  guarda la pantalla a los S segundos de arrancar; ROCOLA_CAPTURA_SALE=1 cierra despues
//   de la ultima (pruebas).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Rocola.Pantalla;
using SkiaSharp;
using Point = System.Drawing.Point;

namespace Rocola
{
    internal sealed unsafe class Anfitrion : IAnfitrion
    {
        internal static Anfitrion Actual;
        private readonly ISuperficie sup;
        private readonly uint fboPantalla;
        private readonly int ancho, alto;
        private uint programa; private int lugarM, lugarA, lugarS, lugarRgba;
        private sealed class CapaGl { internal uint Tex; internal int Version = -1, W, H; internal bool Vista; }
        private readonly Dictionary<Capa, CapaGl> capas = new Dictionary<Capa, CapaGl>();
        private readonly List<KeyValuePair<double, string>> capturas = new List<KeyValuePair<double, string>>();
        private readonly bool saleTrasCapturas;
        internal int Composiciones;
        private static readonly bool trazaCapas = Environment.GetEnvironmentVariable("ROCOLA_TRAZA_CAPAS") == "1";
        private double ultimaTraza;

        // Al empezar (antes del Main de XP, en el mismo hilo): la pantalla y el contexto de OpenGL.
        internal static void Arranca()
        {
            if (Actual != null) return;
            string p = Environment.GetEnvironmentVariable("ROCOLA_PANTALLA") ?? "";
            ISuperficie s = null;
            if (!p.StartsWith("fuera"))
            {
                try { s = new Kms(); }
                catch (Exception ex) { Console.Error.WriteLine("Pantalla: sin KMS (" + ex.Message + "): fuera de pantalla"); }
            }
            if (s == null)
            {
                int w = 1024, h = 768;
                int i = p.IndexOf(':');
                if (i > 0) { string[] t = p.Substring(i + 1).Split('x'); if (t.Length == 2) { int.TryParse(t[0], out w); int.TryParse(t[1], out h); } }
                s = new FueraDePantalla(w, h);
            }
            Actual = new Anfitrion(s);
            Escritorio.Anfitrion = Actual;
            ReproductorWmp.Engancha();   // el video de XP (el control del WMP) con libmpv (VideoMpv.cs)
            // las teclas: con la pantalla de verdad, los teclados y botones de /dev/input (TeclasLinux.cs); y las de prueba
            if (s is Kms || Environment.GetEnvironmentVariable("ROCOLA_TECLAS") == "1") TeclasLinux.Arranca("/dev/input");
            TeclasLinux.Programadas(Environment.GetEnvironmentVariable("ROCOLA_PULSA"));
        }

        private Anfitrion(ISuperficie s)
        {
            sup = s; ancho = s.Ancho; alto = s.Alto;
            int f = 0; GL.GetIntegerv(GL.FRAMEBUFFER_BINDING, &f);   // KMS: 0 (la ventana); fuera de pantalla: su framebuffer
            fboPantalla = (uint)f;
            Escritorio.Pantalla = new System.Drawing.Size(ancho, alto);
            int hz = 60;
            int a = s.Descripcion.IndexOf(" a "), b = s.Descripcion.IndexOf(" Hz");
            if (a > 0 && b > a) int.TryParse(s.Descripcion.Substring(a + 3, b - a - 3), out hz);
            Escritorio.RefrescoHz = hz >= 24 && hz <= 400 ? hz : 60;
            Console.WriteLine("Pantalla: " + s.Descripcion);
            string c = Environment.GetEnvironmentVariable("ROCOLA_CAPTURA");
            if (!string.IsNullOrEmpty(c))
                foreach (string e in c.Split(','))
                {
                    int k = e.IndexOf(':'); double seg;
                    if (k > 0 && double.TryParse(e.Substring(0, k), NumberStyles.Float, CultureInfo.InvariantCulture, out seg)) capturas.Add(new KeyValuePair<double, string>(seg * 1000.0, e.Substring(k + 1)));
                }
            capturas.Sort((x, y) => x.Key.CompareTo(y.Key));
            saleTrasCapturas = Environment.GetEnvironmentVariable("ROCOLA_CAPTURA_SALE") == "1";
        }

        // ---- componer ----
        private const string FuenteV =
            "attribute vec2 p; attribute vec2 t; uniform vec2 m; varying vec2 vt;\n" +
            "void main() { gl_Position = vec4(p.x * m.x - 1.0, 1.0 - p.y * m.y, 0.0, 1.0); vt = t; }\n";
        // las texturas guardan B, G, R, A (Dibujo2D y Direct3D): se leen en el orden de OpenGL; las de Dibujo2D ya van premultiplicadas
        private const string FuenteF =
            "precision mediump float; varying vec2 vt; uniform sampler2D s; uniform float a; uniform float rgba;\n" +
            "void main() { vec4 c = texture2D(s, vt); gl_FragColor = (rgba > 0.5 ? c : c.bgra) * a; }\n";
        private void Programa()
        {
            if (programa != 0) return;
            uint v = Sombreador(GL.VERTEX_SHADER, FuenteV), f = Sombreador(GL.FRAGMENT_SHADER, FuenteF);
            uint p = GL.CreateProgram();
            GL.AttachShader(p, v); GL.AttachShader(p, f);
            GL.BindAttribLocation(p, 0, "p"); GL.BindAttribLocation(p, 1, "t");
            GL.LinkProgram(p);
            int ok; GL.GetProgramiv(p, GL.LINK_STATUS, &ok);
            if (ok == 0) throw new InvalidOperationException("Anfitrion: el programa de componer no enlaza");
            programa = p;
            lugarM = GL.GetUniformLocation(p, "m"); lugarA = GL.GetUniformLocation(p, "a"); lugarS = GL.GetUniformLocation(p, "s"); lugarRgba = GL.GetUniformLocation(p, "rgba");
        }
        private static uint Sombreador(uint tipo, string fuente)
        {
            uint s = GL.CreateShader(tipo);
            byte[] b = Encoding.ASCII.GetBytes(fuente);
            fixed (byte* pb = b) { byte* q = pb; int n = b.Length; GL.ShaderSource(s, 1, &q, &n); }
            GL.CompileShader(s);
            int ok; GL.GetShaderiv(s, GL.COMPILE_STATUS, &ok);
            if (ok == 0) throw new InvalidOperationException("Anfitrion: un sombreador no compila");
            return s;
        }

        public void Compone()
        {
            Programa();
            // los videos (VideoMpv.cs): libmpv pinta su cuadro nuevo en su textura antes de componer
            ReproductorWmp[] rs; lock (ReproductorWmp.Vivos) rs = ReproductorWmp.Vivos.ToArray();
            foreach (ReproductorWmp r in rs) { try { r.Pinta(); } catch (Exception ex) { Application.Avisa(ex); } }
            GL.BindFramebuffer(GL.FRAMEBUFFER, fboPantalla);
            GL.Viewport(0, 0, ancho, alto);
            GlMas.ColorMask(1, 1, 1, 1);
            GL.Disable(GL.SCISSOR_TEST);
            GL.ClearColor(0, 0, 0, 1); GL.Clear(GL.COLOR_BUFFER_BIT);
            GL.UseProgram(programa);
            GL.Uniform2f(lugarM, 2f / ancho, 2f / alto);
            GL.Uniform1i(lugarS, 0); GL.Uniform1f(lugarRgba, 0f);
            GL.ActiveTexture(GL.TEXTURE0);
            GL.BindBuffer(GL.ARRAY_BUFFER, 0);
            foreach (CapaGl c in capas.Values) c.Vista = false;
            List<Capa> lista = Escritorio.Capas();
            if (trazaCapas && Escritorio.AhoraMs - ultimaTraza > 1000)   // (pruebas) ROCOLA_TRAZA_CAPAS=1: las capas, una vez por segundo
            {
                ultimaTraza = Escritorio.AhoraMs;
                System.Text.StringBuilder sb = new System.Text.StringBuilder("capas " + (Escritorio.AhoraMs / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " s:");
                foreach (Capa c in lista) sb.Append(' ').Append(c.Nativa != null ? (c.Nativa.GetType().Name + "#" + c.Nativa.TexturaGl + (c.Nativa.TexturaGlRgba ? "rgba" : "")) : "dibujo").Append('@').Append(c.Rect.X).Append(',').Append(c.Rect.Y).Append(',').Append(c.Rect.Width).Append('x').Append(c.Rect.Height);
                Console.WriteLine(sb.ToString());
            }
            foreach (Capa c in lista)
            {
                if (c.Nativa != null)
                {
                    Control n = c.Nativa;
                    if (n.TexturaGl == 0 || n.AnchoGl <= 0) continue;
                    GL.Disable(GL.BLEND);
                    GL.BindTexture(GL.TEXTURE_2D, n.TexturaGl);
                    GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GlMas.NEAREST); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GlMas.NEAREST);
                    GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.CLAMP_TO_EDGE); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.CLAMP_TO_EDGE);
                    // el control enseña la esquina de arriba a la izquierda del fondo (el fondo puede ser mas grande)
                    if (n.TexturaGlRgba) GL.Uniform1f(lugarRgba, 1f);
                    Cuadro(c.Rect.X, c.Rect.Y, c.Rect.Width, c.Rect.Height, Math.Min(1f, (float)c.Rect.Width / n.AnchoGl), Math.Min(1f, (float)c.Rect.Height / n.AltoGl), 1f);
                    if (n.TexturaGlRgba) GL.Uniform1f(lugarRgba, 0f);
                    continue;
                }
                if (c.Imagen == null) continue;
                CapaGl g;
                if (!capas.TryGetValue(c, out g)) { g = new CapaGl(); capas[c] = g; }
                g.Vista = true;
                if (g.Version != c.Version || g.W != c.Imagen.Width || g.H != c.Imagen.Height) Sube(c.Imagen, g);
                g.Version = c.Version;
                GL.Enable(GL.BLEND); GL.BlendFunc(GL.ONE, GL.ONE_MINUS_SRC_ALPHA);
                GL.BindTexture(GL.TEXTURE_2D, g.Tex);
                Cuadro(c.Rect.X, c.Rect.Y, c.Rect.Width, c.Rect.Height, 1f, 1f, c.Opacidad);
            }
            // las capas que ya no estan, fuera
            List<Capa> fuera = null;
            foreach (KeyValuePair<Capa, CapaGl> k in capas) if (!k.Value.Vista) { if (fuera == null) fuera = new List<Capa>(); fuera.Add(k.Key); }
            if (fuera != null) foreach (Capa c in fuera) { uint t = capas[c].Tex; if (t != 0) GL.DeleteTextures(1, &t); capas.Remove(c); }
            GL.Disable(GL.BLEND);
            Composiciones++;
            if (conFlecha) { Flecha(); Capturas(); }   // (pruebas: ROCOLA_CAPTURA_FLECHA=1)
            else { Capturas(); Flecha(); }   // como las capturas de Windows: sin la flecha
            sup.Presenta();
            foreach (ReproductorWmp r in rs) r.TrasPresentar();
            D3D9.EnGl.EstadoTocado();   // lo puesto en OpenGL ya no es lo del dispositivo de la tarjeta
        }
        // LA FLECHA DEL RATON, encima de todo cuando XP la ensena (Cursor.Show; en la pantalla de la rocola XP la esconde): la flecha o la
        // mano (Cursors.Hand), dibujadas una vez como las de Windows XP (blanca con borde negro; la mano, con el dedo arriba).
        private uint texFlecha, texMano;
        private static readonly bool conFlecha = Environment.GetEnvironmentVariable("ROCOLA_CAPTURA_FLECHA") == "1";
        private void Flecha()
        {
            if (!Escritorio.CursorVisible) return;
            bool mano = Escritorio.CursorActual == Cursors.Hand;
            if (texFlecha == 0) { texFlecha = HazCursor(false); texMano = HazCursor(true); }
            Point p = Escritorio.PosicionRaton;
            GL.UseProgram(programa);
            GL.Uniform2f(lugarM, 2f / ancho, 2f / alto); GL.Uniform1i(lugarS, 0);
            GL.ActiveTexture(GL.TEXTURE0);
            GL.Enable(GL.BLEND); GL.BlendFunc(GL.ONE, GL.ONE_MINUS_SRC_ALPHA);
            GL.BindTexture(GL.TEXTURE_2D, mano ? texMano : texFlecha);
            if (mano) Cuadro(p.X - 6, p.Y, 32, 32, 1f, 1f, 1f); else Cuadro(p.X, p.Y, 32, 32, 1f, 1f, 1f);
            GL.Disable(GL.BLEND);
        }
        private uint HazCursor(bool mano)
        {
            using (SKBitmap b = new SKBitmap(new SKImageInfo(32, 32, SKColorType.Bgra8888, SKAlphaType.Premul)))
            using (SKCanvas c = new SKCanvas(b))
            using (SKPath r = new SKPath())
            using (SKPaint relleno = new SKPaint { Color = SKColors.White, IsAntialias = false, Style = SKPaintStyle.Fill })
            using (SKPaint borde = new SKPaint { Color = SKColors.Black, IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
            {
                c.Clear(SKColors.Transparent);
                if (!mano)
                {
                    r.MoveTo(0.5f, 0.5f); r.LineTo(0.5f, 16.5f); r.LineTo(4.5f, 12.5f); r.LineTo(7.5f, 18.5f); r.LineTo(9.5f, 17.5f); r.LineTo(6.5f, 11.5f); r.LineTo(11.5f, 11.5f); r.Close();
                }
                else
                {
                    r.MoveTo(5.5f, 0.5f); r.LineTo(8.5f, 0.5f); r.LineTo(8.5f, 7.5f); r.LineTo(16.5f, 8.5f); r.LineTo(17.5f, 10.5f); r.LineTo(16.5f, 16.5f);
                    r.LineTo(13.5f, 21.5f); r.LineTo(6.5f, 21.5f); r.LineTo(1.5f, 14.5f); r.LineTo(1.5f, 11.5f); r.LineTo(3.5f, 11.5f); r.LineTo(5.5f, 13.5f); r.Close();
                }
                c.DrawPath(r, relleno); c.DrawPath(r, borde);
                c.Flush();
                uint t; GL.GenTextures(1, &t);
                GL.BindTexture(GL.TEXTURE_2D, t);
                GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GlMas.NEAREST); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GlMas.NEAREST);
                GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.CLAMP_TO_EDGE); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.CLAMP_TO_EDGE);
                GL.PixelStorei(GL.UNPACK_ALIGNMENT, 4);
                GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, 32, 32, 0, GL.RGBA, GL.UNSIGNED_BYTE, (void*)b.GetPixels());
                return t;
            }
        }
        private void Sube(Bitmap b, CapaGl g)
        {
            SKBitmap sk = b.Sk;
            if (g.Tex == 0)
            {
                uint t; GL.GenTextures(1, &t); g.Tex = t;
                GL.BindTexture(GL.TEXTURE_2D, t);
                GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GlMas.NEAREST); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GlMas.NEAREST);
                GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.CLAMP_TO_EDGE); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.CLAMP_TO_EDGE);
            }
            else GL.BindTexture(GL.TEXTURE_2D, g.Tex);
            GL.PixelStorei(GL.UNPACK_ALIGNMENT, 4);
            if (sk.RowBytes == sk.Width * 4)
                GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, sk.Width, sk.Height, 0, GL.RGBA, GL.UNSIGNED_BYTE, (void*)sk.GetPixels());
            else
            {
                GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, sk.Width, sk.Height, 0, GL.RGBA, GL.UNSIGNED_BYTE, null);
                byte* p = (byte*)sk.GetPixels();
                for (int y = 0; y < sk.Height; y++) GL.TexSubImage2D(GL.TEXTURE_2D, 0, 0, y, sk.Width, 1, GL.RGBA, GL.UNSIGNED_BYTE, p + (long)y * sk.RowBytes);
            }
            g.W = sk.Width; g.H = sk.Height;
        }
        private void Cuadro(float x, float y, float w, float h, float u1, float v1, float a)
        {
            float* v = stackalloc float[] { x, y, 0, 0, x + w, y, u1, 0, x, y + h, 0, v1, x + w, y + h, u1, v1 };
            GL.Uniform1f(lugarA, a);
            GL.VertexAttribPointer(0, 2, GL.FLOAT, 0, 16, (IntPtr)v);
            GL.VertexAttribPointer(1, 2, GL.FLOAT, 0, 16, (IntPtr)(v + 2));
            GL.EnableVertexAttribArray(0); GL.EnableVertexAttribArray(1);
            GlMas.DisableVertexAttribArray(2); GlMas.DisableVertexAttribArray(3);
            GL.DrawArrays(GlMas.TRIANGLE_STRIP, 0, 4);
        }

        // ---- capturas (pruebas) ----
        private void Capturas()
        {
            if (capturas.Count == 0) return;
            double t = Escritorio.AhoraMs;
            if (t < capturas[0].Key) return;
            string ruta = capturas[0].Value;
            capturas.RemoveAt(0);
            GuardaPng(ruta);
            string tx = Environment.GetEnvironmentVariable("ROCOLA_CAPTURA_TEXTURAS");   // (pruebas) "nombre:AxB,..." texturas de OpenGL tal cual estan en la tarjeta
            if (!string.IsNullOrEmpty(tx))
                foreach (string e in tx.Split(','))
                {
                    string[] p = e.Split(':'); if (p.Length != 2) continue; string[] m = p[1].Split('x');
                    GuardaTextura(uint.Parse(p[0]), int.Parse(m[0]), int.Parse(m[1]), ruta + ".tex" + p[0] + ".png");
                }
            if (Environment.GetEnvironmentVariable("ROCOLA_CAPTURA_CAPAS") == "1")
            {
                // (pruebas) cada capa por separado: las de la tarjeta (su textura) y las de Dibujo2D
                int i = 0;
                foreach (Capa c in Escritorio.Capas())
                {
                    i++;
                    if (c.Nativa != null && c.Nativa.TexturaGl != 0) GuardaTextura(c.Nativa.TexturaGl, c.Nativa.AnchoGl, c.Nativa.AltoGl, ruta + ".capa" + i + "-tarjeta.png");
                    else if (c.Imagen != null) { try { c.Imagen.Save(ruta + ".capa" + i + "-dibujo2d-" + c.Rect.X + "," + c.Rect.Y + "-" + c.Rect.Width + "x" + c.Rect.Height + ".png"); } catch { } }
                }
            }
            Console.WriteLine("Captura: " + ruta + " (" + (t / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " s, " + Composiciones + " composiciones)");
            if (capturas.Count == 0 && saleTrasCapturas) Environment.Exit(0);
        }
        private void GuardaTextura(uint tex, int w, int h, string ruta)
        {
            uint f; GL.GenFramebuffers(1, &f);
            GL.BindFramebuffer(GL.FRAMEBUFFER, f);
            GL.FramebufferTexture2D(GL.FRAMEBUFFER, GL.COLOR_ATTACHMENT0, GL.TEXTURE_2D, tex, 0);
            byte[] px = new byte[w * h * 4];
            GL.PixelStorei(GL.PACK_ALIGNMENT, 4);
            fixed (byte* p = px) GL.ReadPixels(0, 0, w, h, GL.RGBA, GL.UNSIGNED_BYTE, p);
            GL.BindFramebuffer(GL.FRAMEBUFFER, fboPantalla);
            GlMas.DeleteFramebuffers(1, &f);
            using (SKBitmap b = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Unpremul)))   // la tarjeta guarda B, G, R, A, fila 0 arriba
            {
                Marshal.Copy(px, 0, b.GetPixels(), px.Length);
                using (SKData data = b.Encode(SKEncodedImageFormat.Png, 100)) using (System.IO.FileStream fs = System.IO.File.Create(ruta)) data.SaveTo(fs);
            }
        }
        internal void GuardaPng(string ruta)
        {
            byte[] px = new byte[ancho * alto * 4];
            GL.BindFramebuffer(GL.FRAMEBUFFER, fboPantalla);
            GL.PixelStorei(GL.PACK_ALIGNMENT, 4);
            fixed (byte* p = px) GL.ReadPixels(0, 0, ancho, alto, GL.RGBA, GL.UNSIGNED_BYTE, p);
            using (SKBitmap b = new SKBitmap(new SKImageInfo(ancho, alto, SKColorType.Rgba8888, SKAlphaType.Opaque)))
            {
                byte* d = (byte*)b.GetPixels();
                for (int y = 0; y < alto; y++)   // OpenGL lee de abajo arriba
                    Marshal.Copy(px, (alto - 1 - y) * ancho * 4, (IntPtr)(d + (long)y * b.RowBytes), ancho * 4);
                using (SKData data = b.Encode(SKEncodedImageFormat.Png, 100))
                using (System.IO.FileStream f = System.IO.File.Create(ruta)) data.SaveTo(f);
            }
        }
    }
}
