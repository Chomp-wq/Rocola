// LA PANTALLA DE LA ROCOLA DE XP EN ROCOLA OS. Lo que en XP hace Windows al juntar las ventanas: aqui se junta en cada cuadro lo que
// pinta GDI+ (las capas del escritorio, Formas/Escritorio.cs) con lo que pinta la tarjeta (el buffer de atras de cada dispositivo de
// Direct3D, D3d9.cs) y se presenta por KMS (sistema/rocola/Consola/Pantalla/Kms.cs) o, para probar sin monitor, fuera de pantalla
// (ROCOLA_PANTALLA=fuera:1024x768), donde se puede guardar el cuadro (Captura).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using static Rocola.Linux.D3d9;

namespace Rocola.Linux
{
    internal sealed unsafe class Pantalla : IAnfitrion
    {
        internal static bool Lista;
        internal static global::Rocola.Pantalla.ISuperficie Sup;
        private static uint fboPantalla;
        private static uint prog; private static int uRect, uTam, uOp, uVolteo, uTex;
        private static readonly Dictionary<Capa, (uint tex, int version, int w, int h)> texCapas = new Dictionary<Capa, (uint, int, int, int)>();
        private static readonly List<uint> porBorrar = new List<uint>(), fbosPorBorrar = new List<uint>();
        internal static int Cuadros;
        internal static Action TrasPresentar;   // las pruebas (Captura)
        internal static string Descripcion { get { return Sup == null ? "sin pantalla" : Sup.Descripcion; } }

        internal static void Inicia()
        {
            string modo = Environment.GetEnvironmentVariable("ROCOLA_PANTALLA") ?? "";
            if (modo.StartsWith("fuera"))
            {
                int w = 1024, h = 768;
                int dos = modo.IndexOf(':');
                if (dos > 0) { string[] p = modo.Substring(dos + 1).Split('x'); if (p.Length == 2) { int.TryParse(p[0], out w); int.TryParse(p[1], out h); } }
                Sup = new global::Rocola.Pantalla.FueraDePantalla(w, h);
                int f; Gles.GetIntegerv(Gles.FRAMEBUFFER_BINDING, &f); fboPantalla = (uint)f;
            }
            else { Sup = new global::Rocola.Pantalla.Kms(); fboPantalla = 0; }
            Escritorio.Pantalla = new Size(Sup.Ancho, Sup.Alto);
            Gl.Inicia();
            prog = Gles.Programa(
                "attribute vec2 aPos; uniform vec4 uRect; uniform vec2 uTam; uniform float uVolteo; varying vec2 vT;\n" +
                "void main() { vec2 p = uRect.xy + aPos * uRect.zw; vT = aPos; gl_Position = vec4(p.x / uTam.x * 2.0 - 1.0, 1.0 - p.y / uTam.y * 2.0, 0.0, 1.0); }\n",
                "precision mediump float; uniform sampler2D uTex; uniform float uOp; varying vec2 vT;\n" +
                "void main() { vec4 c = texture2D(uTex, vT).bgra; gl_FragColor = c * uOp; }\n", "aPos");
            uRect = Gles.Uniforme(prog, "uRect"); uTam = Gles.Uniforme(prog, "uTam"); uOp = Gles.Uniforme(prog, "uOp"); uTex = Gles.Uniforme(prog, "uTex");
            Escritorio.Anfitrion = new Pantalla();
            Lista = true;
        }

        public void Compone() { Presenta(null); }
        internal static void BorraLuego(uint tex, uint fbo) { if (tex != 0) porBorrar.Add(tex); if (fbo != 0) fbosPorBorrar.Add(fbo); }

        // Junta todo y presenta. 'quien': el dispositivo que pide presentar (null: el escritorio, porque cambio algo de GDI+).
        internal static void Presenta(Dispositivo quien)
        {
            List<Capa> capas = Escritorio.Capas();
            Gles.BindFramebuffer(Gles.FRAMEBUFFER, fboPantalla);
            int W = Sup.Ancho, H = Sup.Alto;
            Gles.Viewport(0, 0, W, H);
            Gles.Disable(Gles.SCISSOR_TEST);
            Gles.ClearColor(0, 0, 0, 1); Gles.Clear(Gles.COLOR_BUFFER_BIT);
            Gles.UseProgram(prog);
            Gles.Uniform2f(uTam, W, H); Gles.Uniform1i(uTex, 0);
            Gles.ActiveTexture(Gles.TEXTURE0);
            float* quad = stackalloc float[] { 0, 0, 1, 0, 0, 1, 1, 1 };
            Gles.BindBuffer(Gles.ARRAY_BUFFER, 0);
            for (uint i = 1; i < 4; i++) Gles.DisableVertexAttribArray(i);
            Gles.EnableVertexAttribArray(0); Gles.VertexAttribPointer(0, 2, Gles.FLOAT, 0, 8, (IntPtr)quad);
            HashSet<Capa> vistas = new HashSet<Capa>();
            foreach (Capa c in capas)
            {
                uint tex;
                if (c.Imagen != null)
                {
                    vistas.Add(c);
                    tex = TexturaDe(c);
                    Gles.Enable(Gles.BLEND); Gles.BlendFunc(Gles.ONE, Gles.ONE_MINUS_SRC_ALPHA);   // GDI+ va premultiplicado
                    Gles.Uniform1f(uOp, c.Opacidad);
                }
                else
                {
                    Dispositivo d = null;
                    lock (Dispositivo.Vivos) foreach (Dispositivo x in Dispositivo.Vivos) if (x.Hwnd == c.Nativa) { d = x; break; }
                    if (d == null || d.Fondo == null || d.Fondo.Gl == 0) continue;
                    tex = d.Fondo.Gl;
                    Gles.Disable(Gles.BLEND);
                    Gles.Uniform1f(uOp, 1f);
                }
                Gles.BindTexture(Gles.TEXTURE_2D, tex);
                Gles.Uniform4f(uRect, c.Rect.X, c.Rect.Y, c.Rect.Width, c.Rect.Height);
                Gles.DrawArrays(Gles.TRIANGLE_STRIP, 0, 4);
            }
            // las capas que ya no estan sueltan su textura
            List<Capa> fuera = null;
            foreach (Capa c in texCapas.Keys) if (!vistas.Contains(c)) (fuera ?? (fuera = new List<Capa>())).Add(c);
            if (fuera != null) foreach (Capa c in fuera) { porBorrar.Add(texCapas[c].tex); texCapas.Remove(c); }
            Gles.DisableVertexAttribArray(0);
            Sup.Presenta();
            Cuadros++;
            if (quien != null) Escritorio.UltimaPresentacionNativa = Escritorio.AhoraMs;
            Limpia();
            Action tras = TrasPresentar; if (tras != null) tras();
        }
        private static uint TexturaDe(Capa c)
        {
            (uint tex, int version, int w, int h) e;
            bool hay = texCapas.TryGetValue(c, out e);
            if (hay && e.version == c.Version) return e.tex;
            Bitmap b = c.Imagen;
            BitmapData bd = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                uint t = hay ? e.tex : 0;
                if (t == 0) { Gles.GenTextures(1, &t); Gles.BindTexture(Gles.TEXTURE_2D, t); Gles.TexParameteri(Gles.TEXTURE_2D, Gles.TEXTURE_MIN_FILTER, (int)Gles.NEAREST); Gles.TexParameteri(Gles.TEXTURE_2D, Gles.TEXTURE_MAG_FILTER, (int)Gles.NEAREST); Gles.TexParameteri(Gles.TEXTURE_2D, Gles.TEXTURE_WRAP_S, (int)Gles.CLAMP_TO_EDGE); Gles.TexParameteri(Gles.TEXTURE_2D, Gles.TEXTURE_WRAP_T, (int)Gles.CLAMP_TO_EDGE); }
                else Gles.BindTexture(Gles.TEXTURE_2D, t);
                if (bd.Stride != b.Width * 4) throw new InvalidOperationException("paso inesperado de GDI+");
                if (hay && e.w == b.Width && e.h == b.Height) Gles.TexSubImage2D(Gles.TEXTURE_2D, 0, 0, 0, b.Width, b.Height, Gles.RGBA, Gles.UNSIGNED_BYTE, (void*)bd.Scan0);
                else Gles.TexImage2D(Gles.TEXTURE_2D, 0, (int)Gles.RGBA, b.Width, b.Height, 0, Gles.RGBA, Gles.UNSIGNED_BYTE, (void*)bd.Scan0);
                texCapas[c] = (t, c.Version, b.Width, b.Height);
                return t;
            }
            finally { b.UnlockBits(bd); }
        }
        private static void Limpia()
        {
            if (porBorrar.Count > 0) { uint[] a = porBorrar.ToArray(); fixed (uint* p = a) Gles.DeleteTextures(a.Length, p); porBorrar.Clear(); }
            if (fbosPorBorrar.Count > 0) { uint[] a = fbosPorBorrar.ToArray(); fixed (uint* p = a) Gles.DeleteFramebuffers(a.Length, p); fbosPorBorrar.Clear(); }
        }

        // El cuadro de la pantalla, en PPM (solo fuera de pantalla).
        internal static void Captura(string ruta)
        {
            global::Rocola.Pantalla.FueraDePantalla f = Sup as global::Rocola.Pantalla.FueraDePantalla;
            if (f != null) f.Captura(ruta);
        }
    }
}
