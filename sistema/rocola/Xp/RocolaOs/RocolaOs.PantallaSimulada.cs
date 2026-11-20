// ROCOLA OS (fase 11, la hoja Pantalla de Ctrl+F4): UNA PANTALLA DE KMS SIMULADA, SOLO PARA LAS PRUEBAS (FASE11_pantalla.md). Dibuja fuera
// de pantalla como FueraDePantalla (Consola/Pantalla/Kms.cs: Mesa sin superficie y un framebuffer de OpenGL, que el compositor captura) y
// hace de monitor y de controlador: sus modos, el automatico (el mismo Kms.ModoAutomatico), cambiar de modo (la textura del framebuffer a la
// medida nueva; los que se le digan fallan como un drmModeSetCrtc que no acepta), la tabla de gamma y la matriz de color (no se ven en la
// captura: en la tarjeta de verdad las aplica la salida, despues del framebuffer; se apuntan con sus numeros). Solo la abre g86 con la
// funcion «pantalla» y ROCOLA_KMS_SIMULADO puesta:
//   ROCOLA_KMS_SIMULADO="1920x1080@60*,1920x1080@50,1280x1024@75,1024x768@75,1024x768@60;gamma=256;lut=0;ctm=0;falla=1280x1024@75;controlador=radeon"
//     los modos (con «*» el preferido del monitor; «i» al final, entrelazado), y despues, separados por «;»: gamma= el tamano de la tabla
//     de la llamada de siempre (0: sin tabla), lut= el de GAMMA_LUT (0: sin la propiedad), ctm=1 si tiene matriz de color, falla= los
//     modos que el controlador no acepta (separados por «/»), controlador= su nombre.
//   ROCOLA_KMS_SIMULADO_TRAZA=RUTA   cada cosa que se le pide, una linea (modo, falla, gamma con la tabla entera, ctm, fabrica, fin)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed unsafe class RocolaOsPantallaSimulada : ISuperficie, IModosPantalla
    {
        private readonly ModoPantalla[] modos;
        private readonly int automatico;
        private int actual;
        private readonly HashSet<string> fallan = new HashSet<string>(StringComparer.Ordinal);
        private readonly int gammaTam, lutTam;
        private readonly bool ctm;
        private readonly string traza;
        private readonly IntPtr egl, ctx;
        private readonly uint fbo, tex;
        private bool tocada;
        public int Ancho { get; private set; }
        public int Alto { get; private set; }
        public string Descripcion { get; private set; }
        public string FalloAlArrancar { get; private set; }
        public string UltimoFallo { get { return null; } }
        public string Controlador { get; private set; }

        internal RocolaOsPantallaSimulada(string spec)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            string[] partes = spec.Split(';');
            List<ModoPantalla> l = new List<ModoPantalla>();
            foreach (string m0 in partes[0].Split(','))
            {
                string m = m0.Trim(); if (m.Length == 0) continue;
                bool pref = m.EndsWith("*"); if (pref) m = m.Substring(0, m.Length - 1);
                bool ent = m.EndsWith("i"); if (ent) m = m.Substring(0, m.Length - 1);
                int x = m.IndexOf('x'), a = m.IndexOf('@');
                int w = int.Parse(m.Substring(0, x), ci), h = int.Parse(m.Substring(x + 1, a - x - 1), ci), hz = int.Parse(m.Substring(a + 1), ci);
                l.Add(new ModoPantalla(l.Count, w, h, hz, hz, ent, pref));
            }
            if (l.Count == 0) l.Add(new ModoPantalla(0, 1024, 768, 60, 60, false, true));
            modos = l.ToArray();
            gammaTam = 256; Controlador = "simulado";
            for (int i = 1; i < partes.Length; i++)
            {
                int k = partes[i].IndexOf('='); if (k <= 0) continue;
                string c = partes[i].Substring(0, k).Trim(), v = partes[i].Substring(k + 1).Trim();
                if (c == "gamma") gammaTam = int.Parse(v, ci);
                else if (c == "lut") lutTam = int.Parse(v, ci);
                else if (c == "ctm") ctm = v == "1";
                else if (c == "falla") foreach (string f in v.Split('/')) { if (f.Trim().Length > 0) fallan.Add(f.Trim()); }
                else if (c == "controlador") Controlador = v;
            }
            traza = Environment.GetEnvironmentVariable("ROCOLA_KMS_SIMULADO_TRAZA");
            // EL MODO: lo automatico de siempre y, si alguien lo pide (g86), el guardado; si el «controlador» no lo acepta, lo automatico
            automatico = Kms.ModoAutomatico(modos, Form1.ResolucionNativaGuardada());
            int elegido = automatico;
            Func<ModoPantalla[], int, int> elige = Kms.EligeModo;
            if (elige != null) { int e = elige((ModoPantalla[])modos.Clone(), automatico); if (e >= 0 && e < modos.Length) elegido = e; }
            if (elegido != automatico && fallan.Contains(modos[elegido].Clave))
            {
                FalloAlArrancar = modos[elegido].Clave + ": el controlador no lo acepta (simulado)";
                Apunta("falla " + modos[elegido].Clave);
                elegido = automatico;
            }
            IntPtr cfg;
            EGL.Contexto(EGL.PLATFORM_SURFACELESS_MESA, IntPtr.Zero, 0, false, out egl, out cfg, out ctx);
            if (EGL.MakeCurrent(egl, IntPtr.Zero, IntPtr.Zero, ctx) == 0) throw new InvalidOperationException("EGL: sin contexto actual");
            uint t, f2;
            GL.GenTextures(1, &t); GL.BindTexture(GL.TEXTURE_2D, t);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GL.LINEAR);
            tex = t;
            Pon(elegido);
            GL.GenFramebuffers(1, &f2); GL.BindFramebuffer(GL.FRAMEBUFFER, f2);
            GL.FramebufferTexture2D(GL.FRAMEBUFFER, GL.COLOR_ATTACHMENT0, GL.TEXTURE_2D, t, 0);
            fbo = f2;
        }
        // la textura del framebuffer a la medida del modo (el mismo framebuffer: el compositor lo guardo al nacer)
        private void Pon(int i)
        {
            actual = i;
            Ancho = modos[i].Ancho; Alto = modos[i].Alto;
            GL.BindTexture(GL.TEXTURE_2D, tex);
            GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, Ancho, Alto, 0, GL.RGBA, GL.UNSIGNED_BYTE, null);
            Descripcion = Ancho + "x" + Alto + " a " + modos[i].Hz + " Hz, KMS simulado (" + Controlador + "), " + GL.Texto(GL.RENDERER);
            Apunta("modo " + modos[i].Clave);
        }
        private void Apunta(string que)
        {
            if (string.IsNullOrEmpty(traza)) return;
            try { File.AppendAllText(traza, (Environment.TickCount64 % 100000000).ToString(CultureInfo.InvariantCulture) + " " + que + "\n"); } catch { }
        }

        public void Presenta() { GL.Finish(); }
        public void Suelta() { }
        public void Recupera() { }

        public ModoPantalla[] Modos { get { return (ModoPantalla[])modos.Clone(); } }
        public int Automatico { get { return automatico; } }
        public ModoPantalla Actual { get { return modos[actual]; } }
        public bool CambiaModo(int indice, out string error)
        {
            error = null;
            if (indice < 0 || indice >= modos.Length) { error = "ese modo no es del monitor"; return false; }
            if (fallan.Contains(modos[indice].Clave)) { error = "el controlador no lo acepta (simulado)"; Apunta("falla " + modos[indice].Clave); return false; }
            Pon(indice);
            return true;
        }
        public int TamanoGamma { get { return lutTam > 0 ? lutTam : gammaTam; } }
        public string ViaGamma { get { return TamanoGamma == 0 ? null : lutTam > 0 ? "GAMMA_LUT" : "drmModeCrtcSetGamma"; } }
        public bool ConCtm { get { return ctm; } }
        public bool PonGamma(ushort[] r, ushort[] g, ushort[] b, out string error)
        {
            error = null;
            int n = TamanoGamma;
            if (n == 0) { error = "el controlador no tiene tabla de gamma"; return false; }
            if (r == null || r.Length != n || g.Length != n || b.Length != n) { error = "la tabla no mide " + n; return false; }
            tocada = true;
            StringBuilder sb = new StringBuilder("gamma ").Append(n).Append(' ').Append(ViaGamma);
            ushort[][] t = { r, g, b }; string[] nom = { " r", " g", " b" };
            for (int c = 0; c < 3; c++) { sb.Append(nom[c]); foreach (ushort v in t[c]) sb.Append(' ').Append(v); }
            Apunta(sb.ToString());
            return true;
        }
        public bool PonCtm(double[] m, out string error)
        {
            error = null;
            if (!ctm) { error = "el controlador no tiene matriz de color (CTM)"; return false; }
            tocada = true;
            if (m == null) { Apunta("ctm ninguna"); return true; }
            StringBuilder sb = new StringBuilder("ctm");
            foreach (double v in m) sb.Append(' ').Append(Kms.S31_32(v).ToString("x16", CultureInfo.InvariantCulture));
            Apunta(sb.ToString());
            return true;
        }
        public bool QuitaColores(out string error)
        {
            error = null;
            tocada = false;
            Apunta("fabrica");
            return true;
        }
        public void Dispose()
        {
            if (tocada) { string e; QuitaColores(out e); }
            Apunta("fin " + modos[actual].Clave);
            EGL.MakeCurrent(egl, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero); EGL.DestroyContext(egl, ctx); EGL.Terminate(egl);
        }
    }
}
