// LO QUE DIBUJA EL DIRECT3D DE MENTIRA (D3d9.cs) con OpenGL ES 2: las dos etapas de textura de funcion fija se vuelven un sombreador
// (uno por combinacion de estados, guardado), los vertices XYZRHW se llevan a la pantalla con la regla de Direct3D 9 (el centro del
// pixel en el entero: +0,5) y con su 1/RHW como w (perspectiva correcta, como la tarjeta), y las texturas se suben de la copia del
// procesador solo por las filas que se escribieron. Las imagenes intermedias van derechas (fila 0 arriba) y la pantalla la voltea. Todas
// las texturas guardan los canales en el orden de la memoria de Direct3D (B,G,R,A): los sombreadores leen y escriben con .bgra.
using System;
using System.Collections.Generic;
using System.Text;
using static Rocola.Linux.D3d9;

namespace Rocola.Linux
{
    internal static unsafe class Gl
    {
        internal static int MaxTextura = 2048;
        private static uint negra;   // la textura de una etapa sin textura: (0,0,0,1), como Direct3D 9
        internal static void Inicia()
        {
            int m; Gles.GetIntegerv(Gles.MAX_TEXTURE_SIZE, &m); if (m >= 64) MaxTextura = Math.Min(m, 8192);
            uint t; Gles.GenTextures(1, &t); Gles.BindTexture(Gles.TEXTURE_2D, t);
            uint px = 0xFF000000u;
            Gles.TexImage2D(Gles.TEXTURE_2D, 0, (int)Gles.RGBA, 1, 1, 0, Gles.RGBA, Gles.UNSIGNED_BYTE, &px);
            Gles.TexParameteri(Gles.TEXTURE_2D, Gles.TEXTURE_MIN_FILTER, (int)Gles.NEAREST); Gles.TexParameteri(Gles.TEXTURE_2D, Gles.TEXTURE_MAG_FILTER, (int)Gles.NEAREST);
            negra = t;
            Gles.Disable(Gles.DITHER); Gles.Disable(Gles.CULL_FACE); Gles.Disable(Gles.DEPTH_TEST);
            Gles.PixelStorei(Gles.UNPACK_ALIGNMENT, 4);
        }

        internal static int Vertices(uint tipo, int n)
        {
            switch (tipo) { case 4: return n * 3; case 5: case 6: return n + 2; case 1: return n; case 2: return n * 2; case 3: return n + 1; default: return 0; }
        }
        private static uint ModoGl(uint tipo)
        {
            switch (tipo) { case 4: return Gles.TRIANGLES; case 5: return Gles.TRIANGLE_STRIP; case 6: return Gles.TRIANGLE_FAN; case 1: return 0; case 2: return 1; case 3: return 3; default: return Gles.TRIANGLES; }
        }

        // ---------- texturas ----------
        internal static void AseguraRt(Textura t)
        {
            if (t.Gl != 0 && t.Fbo != 0) return;
            uint g, f;
            Gles.GenTextures(1, &g); Gles.BindTexture(Gles.TEXTURE_2D, g);
            Gles.TexImage2D(Gles.TEXTURE_2D, 0, (int)Gles.RGBA, t.W, t.H, 0, Gles.RGBA, Gles.UNSIGNED_BYTE, null);
            t.Gl = g; t.DirU = t.DirV = t.Mag = t.Min = uint.MaxValue;
            Parametros(t, 3, 3, 2, 2);
            Gles.GenFramebuffers(1, &f); Gles.BindFramebuffer(Gles.FRAMEBUFFER, f);
            Gles.FramebufferTexture2D(Gles.FRAMEBUFFER, Gles.COLOR_ATTACHMENT0, Gles.TEXTURE_2D, g, 0);
            t.Fbo = f;
            Gles.ClearColor(0, 0, 0, 1); Gles.Clear(Gles.COLOR_BUFFER_BIT);   // en Direct3D empieza con basura; aqui en negro
        }
        internal static void Sube(Textura t)
        {
            if (t.Rt) { AseguraRt(t); return; }
            if (t.Mem == IntPtr.Zero) return;
            if (t.Gl == 0)
            {
                uint g; Gles.GenTextures(1, &g); t.Gl = g;
                Gles.BindTexture(Gles.TEXTURE_2D, g);
                Gles.TexImage2D(Gles.TEXTURE_2D, 0, (int)Gles.RGBA, t.W, t.H, 0, Gles.RGBA, Gles.UNSIGNED_BYTE, (void*)t.Mem);
                t.DirU = t.DirV = t.Mag = t.Min = uint.MaxValue;
                t.SuciaY0 = int.MaxValue; t.SuciaY1 = -1;
                return;
            }
            if (t.SuciaY1 > t.SuciaY0)
            {
                int y0 = Math.Max(0, t.SuciaY0), y1 = Math.Min(t.H, t.SuciaY1);
                Gles.BindTexture(Gles.TEXTURE_2D, t.Gl);
                if (y1 > y0) Gles.TexSubImage2D(Gles.TEXTURE_2D, 0, 0, y0, t.W, y1 - y0, Gles.RGBA, Gles.UNSIGNED_BYTE, (byte*)t.Mem + (long)y0 * t.W * 4);
                t.SuciaY0 = int.MaxValue; t.SuciaY1 = -1;
            }
        }
        private static void Parametros(Textura t, uint dirU, uint dirV, uint mag, uint min)
        {
            if (t.DirU != dirU) { Gles.TexParameteri(Gles.TEXTURE_2D, Gles.TEXTURE_WRAP_S, (int)Direccion(dirU)); t.DirU = dirU; }
            if (t.DirV != dirV) { Gles.TexParameteri(Gles.TEXTURE_2D, Gles.TEXTURE_WRAP_T, (int)Direccion(dirV)); t.DirV = dirV; }
            if (t.Mag != mag) { Gles.TexParameteri(Gles.TEXTURE_2D, Gles.TEXTURE_MAG_FILTER, (int)(mag == 2 ? Gles.LINEAR : Gles.NEAREST)); t.Mag = mag; }
            if (t.Min != min) { Gles.TexParameteri(Gles.TEXTURE_2D, Gles.TEXTURE_MIN_FILTER, (int)(min == 2 ? Gles.LINEAR : Gles.NEAREST)); t.Min = min; }
        }
        private static uint Direccion(uint d) { return d == 1 ? Gles.REPEAT : d == 2 ? Gles.MIRRORED_REPEAT : Gles.CLAMP_TO_EDGE; }

        // ---------- el destino ----------
        private static void PonDestino(Dispositivo d, out int w, out int h)
        {
            Textura rt = d.Destino != null && d.Destino.Tex != null ? d.Destino.Tex : d.Fondo;
            AseguraRt(rt);
            Gles.BindFramebuffer(Gles.FRAMEBUFFER, rt.Fbo);
            Gles.Viewport(0, 0, rt.W, rt.H);
            w = rt.W; h = rt.H;
        }
        internal static void Borra(Dispositivo d, uint color)
        {
            int w, h; PonDestino(d, out w, out h);
            Gles.ClearColor((color & 255) / 255f, ((color >> 8) & 255) / 255f, ((color >> 16) & 255) / 255f, (color >> 24) / 255f);   // en orden B,G,R,A
            Gles.Clear(Gles.COLOR_BUFFER_BIT);
        }

        // ---------- los sombreadores de funcion fija ----------
        private sealed class Prog { public uint P; public int Tam, S0, S1, X0, X1; }
        private static readonly Dictionary<(ulong, ulong), Prog> programas = new Dictionary<(ulong, ulong), Prog>();
        private static ulong ClaveEtapa(Etapa e)
        {
            return e.ColorOp | ((ulong)e.ColorA1 << 6) | ((ulong)e.ColorA2 << 14) | ((ulong)e.AlphaOp << 22) | ((ulong)e.AlphaA1 << 28) | ((ulong)e.AlphaA2 << 36) | ((ulong)(e.Indice & 1) << 44);
        }
        private const string Vs =
            "attribute vec4 aPos; attribute vec4 aCol; attribute vec2 aT0; attribute vec2 aT1;\n" +
            "uniform vec2 uTam;\n" +
            "varying vec4 vCol; varying vec2 vT0; varying vec2 vT1;\n" +
            "void main() {\n" +
            "  float w = aPos.w > 0.0 ? 1.0 / aPos.w : 1.0;\n" +
            "  vec2 p = vec2((aPos.x + 0.5) / uTam.x * 2.0 - 1.0, (aPos.y + 0.5) / uTam.y * 2.0 - 1.0);\n" +
            "  gl_Position = vec4(p * w, 0.0, w);\n" +
            "  vCol = aCol.bgra; vT0 = aT0; vT1 = aT1;\n" +
            "}\n";
        private static string Arg(uint a, int etapa)
        {
            string b;
            switch (a & 15)
            {
                case 0: b = "vCol"; break;
                case 1: b = etapa == 0 ? "vCol" : "c"; break;
                case 2: b = "t" + etapa; break;
                case 3: b = "vec4(1.0)"; break;   // TFACTOR: el de fabrica (blanco)
                default: b = "vec4(0.0)"; break;
            }
            if ((a & 0x20) != 0) b = "vec4((" + b + ").a)";
            if ((a & 0x10) != 0) b = "(vec4(1.0) - " + b + ")";
            return b;
        }
        private static string Op(uint op, string a1, string a2, string cual)
        {
            string x = "(" + a1 + ")." + cual, y = "(" + a2 + ")." + cual;
            switch (op)
            {
                case 2: return x;
                case 3: return y;
                case 4: return x + " * " + y;
                case 5: return "clamp(" + x + " * " + y + " * 2.0, 0.0, 1.0)";
                case 6: return "clamp(" + x + " * " + y + " * 4.0, 0.0, 1.0)";
                case 7: return "clamp(" + x + " + " + y + ", 0.0, 1.0)";
                case 8: return "clamp(" + x + " + " + y + " - 0.5, 0.0, 1.0)";
                case 9: return "clamp((" + x + " + " + y + " - 0.5) * 2.0, 0.0, 1.0)";
                case 10: return "clamp(" + x + " - " + y + ", 0.0, 1.0)";
                case 11: return "clamp(" + x + " + " + y + " - " + x + " * " + y + ", 0.0, 1.0)";
                default: return x + " * " + y;
            }
        }
        private static Prog Programa(Dispositivo d, int coords)
        {
            Etapa e0 = d.Etapas[0], e1 = d.Etapas[1];
            ulong k0 = ClaveEtapa(e0) | ((ulong)coords << 50), k1 = e0.ColorOp == 1 ? 0 : ClaveEtapa(e1);
            Prog p;
            if (programas.TryGetValue((k0, k1), out p)) return p;
            StringBuilder fs = new StringBuilder();
            fs.Append("#ifdef GL_FRAGMENT_PRECISION_HIGH\nprecision highp float;\n#else\nprecision mediump float;\n#endif\n");
            fs.Append("uniform sampler2D s0; uniform sampler2D s1; uniform float x0; uniform float x1;\n");
            fs.Append("varying vec4 vCol; varying vec2 vT0; varying vec2 vT1;\n");
            fs.Append("void main() {\n  vec4 c = vCol;\n");
            for (int s = 0; s < 2; s++)
            {
                Etapa e = d.Etapas[s];
                if (e.ColorOp == 1) break;
                string tc = (e.Indice & 1) == 1 && coords >= 2 ? "vT1" : "vT0";
                fs.Append("  vec4 t" + s + " = texture2D(s" + s + ", " + tc + ").bgra; t" + s + ".a = max(t" + s + ".a, x" + s + ");\n");
                fs.Append("  vec4 n" + s + " = c;\n");
                fs.Append("  n" + s + ".rgb = " + Op(e.ColorOp, Arg(e.ColorA1, s), Arg(e.ColorA2, s), "rgb") + ";\n");
                if (e.AlphaOp != 1) fs.Append("  n" + s + ".a = " + Op(e.AlphaOp, Arg(e.AlphaA1 & ~0x20u, s), Arg(e.AlphaA2 & ~0x20u, s), "a") + ";\n");
                fs.Append("  c = n" + s + ";\n");
            }
            fs.Append("  gl_FragColor = c.bgra;\n}\n");   // todas las texturas guardan B,G,R,A (como la memoria de Direct3D)
            p = new Prog();
            p.P = Gles.Programa(Vs, fs.ToString(), "aPos", "aCol", "aT0", "aT1");
            p.Tam = Gles.Uniforme(p.P, "uTam"); p.S0 = Gles.Uniforme(p.P, "s0"); p.S1 = Gles.Uniforme(p.P, "s1"); p.X0 = Gles.Uniforme(p.P, "x0"); p.X1 = Gles.Uniforme(p.P, "x1");
            Gles.UseProgram(p.P); Gles.Uniform1i(p.S0, 0); Gles.Uniform1i(p.S1, 1);
            programas[(k0, k1)] = p;
            return p;
        }
        private static uint Factor(uint f)
        {
            switch (f)
            {
                case 1: return Gles.ZERO; case 2: return Gles.ONE; case 3: return Gles.SRC_COLOR; case 4: return Gles.ONE_MINUS_SRC_COLOR;
                case 5: return Gles.SRC_ALPHA; case 6: return Gles.ONE_MINUS_SRC_ALPHA; case 7: return Gles.DST_ALPHA; case 8: return Gles.ONE_MINUS_DST_ALPHA;
                case 9: return Gles.DST_COLOR; case 10: return Gles.ONE_MINUS_DST_COLOR; default: return Gles.ONE;
            }
        }

        // ---------- dibujar ----------
        internal static int Ordenes;
        internal static void Dibuja(Dispositivo d, uint tipo, int cuantos, byte* datos, int paso)
        {
            int n = Vertices(tipo, cuantos);
            if (n <= 0 || cuantos <= 0) return;
            uint fvf = d.Fvf;
            if ((fvf & 0x00E) != 0x004) return;   // solo XYZRHW
            int coords = (int)((fvf >> 8) & 0xF);
            int desCol = 16, desTex = 16 + ((fvf & 0x40) != 0 ? 4 : 0) + ((fvf & 0x80) != 0 ? 4 : 0);
            int w, h; PonDestino(d, out w, out h);
            Prog p = Programa(d, coords);
            Gles.UseProgram(p.P);
            Gles.Uniform2f(p.Tam, w, h);
            for (int s = 0; s < 2; s++)
            {
                Etapa e = d.Etapas[s];
                Gles.ActiveTexture(Gles.TEXTURE0 + (uint)s);
                Textura t = e.Tex;
                bool activa = s == 0 ? e.ColorOp != 1 : d.Etapas[0].ColorOp != 1 && e.ColorOp != 1;
                if (!activa || t == null || t.Suelta) { Gles.BindTexture(Gles.TEXTURE_2D, negra); Gles.Uniform1f(s == 0 ? p.X0 : p.X1, 1f); continue; }
                if (t == (d.Destino != null ? d.Destino.Tex : null)) { Gles.BindTexture(Gles.TEXTURE_2D, negra); Gles.Uniform1f(s == 0 ? p.X0 : p.X1, 1f); continue; }
                Sube(t);
                Gles.BindTexture(Gles.TEXTURE_2D, t.Gl);
                Parametros(t, e.DirU, e.DirV, e.Mag, e.Min);
                Gles.Uniform1f(s == 0 ? p.X0 : p.X1, t.Xrgb ? 1f : 0f);
            }
            Gles.ActiveTexture(Gles.TEXTURE0);
            if (d.Mezcla) { Gles.Enable(Gles.BLEND); Gles.BlendFunc(Factor(d.Origen), Factor(d.DestinoMezcla)); }
            else Gles.Disable(Gles.BLEND);
            Gles.BindBuffer(Gles.ARRAY_BUFFER, 0);
            Gles.EnableVertexAttribArray(0); Gles.VertexAttribPointer(0, 4, Gles.FLOAT, 0, paso, (IntPtr)datos);
            if ((fvf & 0x40) != 0) { Gles.EnableVertexAttribArray(1); Gles.VertexAttribPointer(1, 4, Gles.UNSIGNED_BYTE, 1, paso, (IntPtr)(datos + desCol)); }
            else { Gles.DisableVertexAttribArray(1); }
            if (coords >= 1) { Gles.EnableVertexAttribArray(2); Gles.VertexAttribPointer(2, 2, Gles.FLOAT, 0, paso, (IntPtr)(datos + desTex)); } else { Gles.DisableVertexAttribArray(2); Gles.VertexAttrib2f(2, 0, 0); }
            if (coords >= 2) { Gles.EnableVertexAttribArray(3); Gles.VertexAttribPointer(3, 2, Gles.FLOAT, 0, paso, (IntPtr)(datos + desTex + 8)); } else { Gles.DisableVertexAttribArray(3); Gles.VertexAttrib2f(3, 0, 0); }
            Gles.DrawArrays(ModoGl(tipo), 0, n);
            Ordenes++;
        }
    }
}
