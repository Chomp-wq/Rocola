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

namespace Rocola
{
    internal sealed unsafe class Anfitrion : IAnfitrion
    {
        internal static Anfitrion Actual;
        private readonly ISuperficie sup;
        private readonly uint fboPantalla;
        private readonly int ancho, alto;
        private uint programa; private int lugarM, lugarA, lugarS;
        private sealed class CapaGl { internal uint Tex; internal int Version = -1, W, H; internal bool Vista; }
        private readonly Dictionary<Capa, CapaGl> capas = new Dictionary<Capa, CapaGl>();
        private readonly List<KeyValuePair<double, string>> capturas = new List<KeyValuePair<double, string>>();
        private readonly bool saleTrasCapturas;
        internal int Composiciones;

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
            "precision mediump float; varying vec2 vt; uniform sampler2D s; uniform float a;\n" +
            "void main() { gl_FragColor = texture2D(s, vt).bgra * a; }\n";
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
            lugarM = GL.GetUniformLocation(p, "m"); lugarA = GL.GetUniformLocation(p, "a"); lugarS = GL.GetUniformLocation(p, "s");
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
            GL.BindFramebuffer(GL.FRAMEBUFFER, fboPantalla);
            GL.Viewport(0, 0, ancho, alto);
            GlMas.ColorMask(1, 1, 1, 1);
            GL.Disable(GL.SCISSOR_TEST);
            GL.ClearColor(0, 0, 0, 1); GL.Clear(GL.COLOR_BUFFER_BIT);
            GL.UseProgram(programa);
            GL.Uniform2f(lugarM, 2f / ancho, 2f / alto);
            GL.Uniform1i(lugarS, 0);
            GL.ActiveTexture(GL.TEXTURE0);
            GL.BindBuffer(GL.ARRAY_BUFFER, 0);
            foreach (CapaGl c in capas.Values) c.Vista = false;
            List<Capa> lista = Escritorio.Capas();
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
                    Cuadro(c.Rect.X, c.Rect.Y, c.Rect.Width, c.Rect.Height, (float)c.Rect.Width / n.AnchoGl, (float)c.Rect.Height / n.AltoGl, 1f);
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
            Capturas();
            sup.Presenta();
            D3D9.EnGl.EstadoTocado();   // lo puesto en OpenGL ya no es lo del dispositivo de la tarjeta
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
            Console.WriteLine("Captura: " + ruta + " (" + (t / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " s, " + Composiciones + " composiciones)");
            if (capturas.Count == 0 && saleTrasCapturas) Environment.Exit(0);
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
