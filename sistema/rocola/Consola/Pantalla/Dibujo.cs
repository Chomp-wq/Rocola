// EL DIBUJO 2D DE LA ROCOLA (OpenGL ES 2): rectangulos de color, imagenes (las caratulas) y letras, juntados en lotes (un solo dibujo
// por textura). Las letras se hornean una vez por tamano en una textura (StbTrueTypeSharp, C# puro); las caratulas se leen con
// StbImageSharp (C# puro: JPEG, PNG, BMP, GIF), se reducen en la CPU y se guardan como textura (las ultimas 96).
using System;
using System.Collections.Generic;
using System.IO;
using StbTrueTypeSharp;
using StbImageSharp;

namespace Rocola.Pantalla
{
    internal struct Color4
    {
        public float R, G, B, A;
        public Color4(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }
        public static Color4 Hex(uint rgb, float a = 1f) { return new Color4(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a); }
        public Color4 ConAlfa(float a) { return new Color4(R, G, B, A * a); }
    }

    internal sealed class Textura
    {
        public uint Id; public int Ancho, Alto;
        public Color4 Media;   // el color de la imagen (para el halo de la portada enfocada, como en XP)
    }

    internal sealed unsafe class Dibujo : IDisposable
    {
        private readonly uint programa, buffer;
        private readonly int uEscala, uTex;
        private readonly float[] vertices = new float[6 * 8 * 2048];
        private int nv;
        private uint texActual;
        private readonly Textura blanca;
        internal readonly Textura Punto;   // un punto suave (estrellas, halos)
        public int Ancho, Alto;

        internal Dibujo(int ancho, int alto)
        {
            Ancho = ancho; Alto = alto;
            programa = Programa(
                "attribute vec2 p; attribute vec2 t; attribute vec4 c; uniform vec2 esc; varying vec2 vt; varying vec4 vc;" +
                "void main(){ vt = t; vc = c; gl_Position = vec4(p.x * esc.x - 1.0, 1.0 - p.y * esc.y, 0.0, 1.0); }",
                "precision mediump float; uniform sampler2D tex; varying vec2 vt; varying vec4 vc;" +
                "void main(){ gl_FragColor = texture2D(tex, vt) * vc; }");
            uEscala = GL.GetUniformLocation(programa, "esc"); uTex = GL.GetUniformLocation(programa, "tex");
            uint b; GL.GenBuffers(1, &b); buffer = b;
            byte[] px = { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 };
            blanca = Sube(px, 2, 2);
            const int L = 64; byte[] pt = new byte[L * L * 4];
            for (int y = 0; y < L; y++) for (int x = 0; x < L; x++)
            {
                double dx = (x + 0.5) / L * 2 - 1, dy = (y + 0.5) / L * 2 - 1, r = Math.Sqrt(dx * dx + dy * dy);
                double a = r >= 1 ? 0 : Math.Pow(1 - r, 2.2);
                int o = (y * L + x) * 4; pt[o] = pt[o + 1] = pt[o + 2] = 255; pt[o + 3] = (byte)(255 * a);
            }
            Punto = Sube(pt, L, L);
        }

        private static uint Shader(uint tipo, string fuente)
        {
            uint s = GL.CreateShader(tipo);
            byte[] f = System.Text.Encoding.ASCII.GetBytes(fuente + "\0");
            fixed (byte* p = f) { byte* pp = p; GL.ShaderSource(s, 1, &pp, null); }
            GL.CompileShader(s);
            int ok; GL.GetShaderiv(s, GL.COMPILE_STATUS, &ok);
            if (ok == 0) { byte* log = stackalloc byte[1024]; int n; GL.GetShaderInfoLog(s, 1024, &n, log); throw new InvalidOperationException("shader: " + new string((sbyte*)log, 0, n)); }
            return s;
        }
        private static uint Programa(string vs, string fs)
        {
            uint p = GL.CreateProgram();
            GL.AttachShader(p, Shader(GL.VERTEX_SHADER, vs)); GL.AttachShader(p, Shader(GL.FRAGMENT_SHADER, fs));
            GL.BindAttribLocation(p, 0, "p"); GL.BindAttribLocation(p, 1, "t"); GL.BindAttribLocation(p, 2, "c");
            GL.LinkProgram(p);
            int ok; GL.GetProgramiv(p, GL.LINK_STATUS, &ok);
            if (ok == 0) throw new InvalidOperationException("programa de dibujo: no enlaza");
            return p;
        }

        // Una textura RGBA (filas de arriba abajo).
        internal Textura Sube(byte[] rgba, int ancho, int alto)
        {
            Vacia();   // lo pendiente se dibuja antes con su textura (subir cambia la textura atada)
            uint t; GL.GenTextures(1, &t);
            GL.BindTexture(GL.TEXTURE_2D, t);
            GL.PixelStorei(GL.UNPACK_ALIGNMENT, 1);
            fixed (byte* p = rgba) GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, ancho, alto, 0, GL.RGBA, GL.UNSIGNED_BYTE, p);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GL.LINEAR); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GL.LINEAR);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.CLAMP_TO_EDGE); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.CLAMP_TO_EDGE);
            texActual = 0;
            return new Textura { Id = t, Ancho = ancho, Alto = alto };
        }
        internal void Borra(Textura t) { if (t == null) return; Vacia(); uint id = t.Id; GL.DeleteTextures(1, &id); texActual = 0; }

        internal void Empieza(Color4 fondo)
        {
            GL.Viewport(0, 0, Ancho, Alto);
            GL.ClearColor(fondo.R, fondo.G, fondo.B, 1f); GL.Clear(GL.COLOR_BUFFER_BIT);
            GL.Enable(GL.BLEND); GL.BlendFunc(GL.SRC_ALPHA, GL.ONE_MINUS_SRC_ALPHA);
            GL.UseProgram(programa); GL.Uniform2f(uEscala, 2f / Ancho, 2f / Alto); GL.Uniform1i(uTex, 0); GL.ActiveTexture(GL.TEXTURE0);
            GL.BindBuffer(GL.ARRAY_BUFFER, buffer);
            GL.VertexAttribPointer(0, 2, GL.FLOAT, 0, 32, (IntPtr)0); GL.VertexAttribPointer(1, 2, GL.FLOAT, 0, 32, (IntPtr)8); GL.VertexAttribPointer(2, 4, GL.FLOAT, 0, 32, (IntPtr)16);
            GL.EnableVertexAttribArray(0); GL.EnableVertexAttribArray(1); GL.EnableVertexAttribArray(2);
            texActual = 0; nv = 0;
        }
        internal void Termina() { Vacia(); }

        private void Vacia()
        {
            if (nv == 0) return;
            GL.BindTexture(GL.TEXTURE_2D, texActual);
            fixed (float* v = vertices) GL.BufferData(GL.ARRAY_BUFFER, (IntPtr)(nv * 32), v, GL.STREAM_DRAW);
            GL.DrawArrays(GL.TRIANGLES, 0, nv);
            nv = 0;
        }
        private void V(float x, float y, float u, float w, Color4 c)
        {
            int o = nv * 8;
            vertices[o] = x; vertices[o + 1] = y; vertices[o + 2] = u; vertices[o + 3] = w;
            vertices[o + 4] = c.R; vertices[o + 5] = c.G; vertices[o + 6] = c.B; vertices[o + 7] = c.A; nv++;
        }
        internal void Quad(Textura t, float x, float y, float w, float h, float u0, float v0, float u1, float v1, Color4 c)
        {
            if (t.Id != texActual || nv + 6 > vertices.Length / 8) { Vacia(); texActual = t.Id; }
            V(x, y, u0, v0, c); V(x + w, y, u1, v0, c); V(x, y + h, u0, v1, c);
            V(x + w, y, u1, v0, c); V(x + w, y + h, u1, v1, c); V(x, y + h, u0, v1, c);
        }
        internal void Rect(float x, float y, float w, float h, Color4 c) { Quad(blanca, x, y, w, h, 0.25f, 0.25f, 0.75f, 0.75f, c); }
        // Un rectangulo con un color arriba y otro abajo (degradado vertical).
        internal void Degradado(float x, float y, float w, float h, Color4 arriba, Color4 abajo)
        {
            if (blanca.Id != texActual || nv + 6 > vertices.Length / 8) { Vacia(); texActual = blanca.Id; }
            V(x, y, 0.5f, 0.5f, arriba); V(x + w, y, 0.5f, 0.5f, arriba); V(x, y + h, 0.5f, 0.5f, abajo);
            V(x + w, y, 0.5f, 0.5f, arriba); V(x + w, y + h, 0.5f, 0.5f, abajo); V(x, y + h, 0.5f, 0.5f, abajo);
        }
        internal void Brillo(float cx, float cy, float radio, Color4 c) { Quad(Punto, cx - radio, cy - radio, 2 * radio, 2 * radio, 0, 0, 1, 1, c); }
        internal void Imagen(Textura t, float x, float y, float w, float h, float alfa = 1f) { Quad(t, x, y, w, h, 0, 0, 1, 1, new Color4(1, 1, 1, alfa)); }
        // Rectangulo de esquinas redondas (hecho con rectangulos: sin otra textura).
        internal void Redondo(float x, float y, float w, float h, float r, Color4 c)
        {
            r = Math.Min(r, Math.Min(w, h) / 2f);
            Rect(x, y + r, w, h - 2 * r, c);
            int pasos = Math.Max(2, (int)r);
            for (int i = 0; i < pasos; i++)
            {
                float yy = r - (i + 0.5f) * r / pasos;
                float dx = r - (float)Math.Sqrt(Math.Max(0, r * r - yy * yy));
                float h1 = r / pasos;
                Rect(x + dx, y + i * h1, w - 2 * dx, h1, c);   // sin solapar: con colores translucidos se veria la union
                Rect(x + dx, y + h - (i + 1) * h1, w - 2 * dx, h1, c);
            }
        }

        public void Dispose() { }
    }

    // UNA LETRA HORNEADA a un tamano: los caracteres de 32 a 255 (Latin-1: acentos y la enie) en una textura.
    internal sealed unsafe class Letra
    {
        private readonly StbTrueType.stbtt_bakedchar[] car = new StbTrueType.stbtt_bakedchar[224];
        private readonly Textura tex;
        private readonly int lado;
        public readonly float Tam;
        internal Letra(Dibujo d, byte[] ttf, float tam)
        {
            Tam = tam;
            lado = tam > 40 ? 1024 : 512;
            byte[] alfa = new byte[lado * lado];
            StbTrueType.stbtt_BakeFontBitmap(ttf, 0, tam, alfa, lado, lado, 32, 224, car);
            byte[] rgba = new byte[lado * lado * 4];
            for (int i = 0; i < alfa.Length; i++) { rgba[4 * i] = 255; rgba[4 * i + 1] = 255; rgba[4 * i + 2] = 255; rgba[4 * i + 3] = alfa[i]; }
            tex = d.Sube(rgba, lado, lado);
        }
        private static int Indice(char c) { int k = c; if (k == 0x2026) k = '.'; if (k < 32 || k > 255) k = '?'; return k - 32; }
        internal float Mide(string s)
        {
            float w = 0; if (s == null) return 0;
            foreach (char c in s) w += car[Indice(c)].xadvance;
            return w;
        }
        // Escribe con la linea base en y + 0,8 * tamano (y es el borde de arriba). Devuelve el ancho.
        internal float Escribe(Dibujo d, string s, float x, float y, Color4 c)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            float x0 = x, base_ = y + Tam * 0.8f;
            foreach (char ch in s)
            {
                StbTrueType.stbtt_bakedchar b = car[Indice(ch)];
                float w = b.x1 - b.x0, h = b.y1 - b.y0;
                if (w > 0 && h > 0)
                    d.Quad(tex, (float)Math.Round(x + b.xoff), (float)Math.Round(base_ + b.yoff), w, h, b.x0 / (float)lado, b.y0 / (float)lado, b.x1 / (float)lado, b.y1 / (float)lado, c);
                x += b.xadvance;
            }
            return x - x0;
        }
        // Cabe en 'ancho': si no, se corta con puntos suspensivos.
        internal string Ajusta(string s, float ancho)
        {
            if (s == null) return "";
            if (Mide(s) <= ancho) return s;
            float pts = Mide("...");
            int n = s.Length;
            while (n > 0 && Mide(s.Substring(0, n)) + pts > ancho) n--;
            return s.Substring(0, n).TrimEnd() + "...";
        }
        internal float EscribeCentrado(Dibujo d, string s, float cx, float y, Color4 c) { return Escribe(d, s, cx - Mide(s) / 2f, y, c); }
    }

    // LAS CARATULAS: leidas, reducidas a 'lado' como mucho y subidas; las ultimas 96 se guardan.
    internal sealed class Caratulas
    {
        private readonly Dibujo d;
        private readonly int lado;
        private readonly Dictionary<string, Textura> hechas = new Dictionary<string, Textura>(StringComparer.Ordinal);
        private readonly LinkedList<string> orden = new LinkedList<string>();
        private readonly HashSet<string> sinImagen = new HashSet<string>(StringComparer.Ordinal);
        internal Caratulas(Dibujo d, int lado) { this.d = d; this.lado = lado; }
        internal Textura De(string ruta)
        {
            if (string.IsNullOrEmpty(ruta) || sinImagen.Contains(ruta)) return null;
            Textura t;
            if (hechas.TryGetValue(ruta, out t)) { orden.Remove(ruta); orden.AddFirst(ruta); return t; }
            try
            {
                ImageResult im = ImageResult.FromMemory(File.ReadAllBytes(ruta), ColorComponents.RedGreenBlueAlpha);
                int w = im.Width, h = im.Height; byte[] px = im.Data;
                if (Math.Max(w, h) > lado) { int nw = Math.Max(1, w * lado / Math.Max(w, h)), nh = Math.Max(1, h * lado / Math.Max(w, h)); px = Reduce(px, w, h, nw, nh); w = nw; h = nh; }
                t = d.Sube(px, w, h);
                t.Media = ColorMedio(px);
            }
            catch { sinImagen.Add(ruta); return null; }
            hechas[ruta] = t; orden.AddFirst(ruta);
            while (orden.Count > 96) { string v = orden.Last.Value; orden.RemoveLast(); d.Borra(hechas[v]); hechas.Remove(v); }
            return t;
        }
        // El color de la portada para su halo (como XP: pesan mas los puntos con color; una gris o negra se queda en gris).
        private static Color4 ColorMedio(byte[] px)
        {
            double r = 0, g = 0, b = 0, p = 0;
            for (int i = 0; i < px.Length; i += 4 * 37)
            {
                double cr = px[i], cg = px[i + 1], cb = px[i + 2];
                double mx = Math.Max(cr, Math.Max(cg, cb)), mn = Math.Min(cr, Math.Min(cg, cb)), peso = 0.15 + (mx - mn) / 255.0;
                r += cr * peso; g += cg * peso; b += cb * peso; p += peso;
            }
            if (p <= 0) return new Color4(0.6f, 0.6f, 0.7f);
            r /= p; g /= p; b /= p;
            double m = Math.Max(r, Math.Max(g, b)); if (m < 1) m = 1;
            return new Color4((float)(r / m), (float)(g / m), (float)(b / m));   // con toda la luz (como el halo de XP)
        }
        // Reduccion por promedio de cajas (sin escalones).
        private static byte[] Reduce(byte[] s, int w, int h, int nw, int nh)
        {
            byte[] r = new byte[nw * nh * 4];
            for (int y = 0; y < nh; y++)
            {
                int y0 = y * h / nh, y1 = Math.Max(y0 + 1, (y + 1) * h / nh);
                for (int x = 0; x < nw; x++)
                {
                    int x0 = x * w / nw, x1 = Math.Max(x0 + 1, (x + 1) * w / nw);
                    int a = 0, b = 0, c = 0, e = 0, n = 0;
                    for (int yy = y0; yy < y1; yy++) for (int xx = x0; xx < x1; xx++) { int o = (yy * w + xx) * 4; a += s[o]; b += s[o + 1]; c += s[o + 2]; e += s[o + 3]; n++; }
                    int q = (y * nw + x) * 4; r[q] = (byte)(a / n); r[q + 1] = (byte)(b / n); r[q + 2] = (byte)(c / n); r[q + 3] = (byte)(e / n);
                }
            }
            return r;
        }
    }
}
