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
        public Rocola.VistaGrafica.C3 Prom, Uno, Dos;   // de que color es la portada (el cielo toma su tono: ColorFondo.cs)
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
        // Con sus tamanos reducidos (mipmaps; lados potencia de 2, como pide OpenGL ES 2): se achica sin granos (V4).
        internal Textura SubeConMipmaps(byte[] rgba, int lado)
        {
            Textura t = Sube(rgba, lado, lado);
            GL.BindTexture(GL.TEXTURE_2D, t.Id);
            GL.GenerateMipmap(GL.TEXTURE_2D);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GL.LINEAR_MIPMAP_LINEAR);
            texActual = 0;
            return t;
        }
        // V24: lo que ya esta dibujado en un rectangulo de la pantalla, copiado a una textura (siempre la misma: una a la vez). Las filas
        // vienen de abajo arriba (como las da OpenGL): se dibuja con v de 1 a 0.
        private Textura copia;
        internal Textura CopiaPantalla(float x, float y, float w, float h)
        {
            Vacia();
            int ix = Math.Max(0, (int)x), iy = Math.Max(0, (int)y), iw = Math.Min(Ancho - ix, (int)Math.Ceiling(w)), ih = Math.Min(Alto - iy, (int)Math.Ceiling(h));
            if (iw < 2 || ih < 2) return null;
            if (copia == null) { uint t; GL.GenTextures(1, &t); copia = new Textura { Id = t }; }
            GL.BindTexture(GL.TEXTURE_2D, copia.Id);
            GL.CopyTexImage2D(GL.TEXTURE_2D, 0, GL.RGB, ix, Alto - iy - ih, iw, ih, 0);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GL.LINEAR); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GL.LINEAR);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.CLAMP_TO_EDGE); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.CLAMP_TO_EDGE);
            texActual = 0;
            copia.Ancho = iw; copia.Alto = ih;
            return copia;
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
        internal void Termina() { Vacia(); GL.Disable(GL.SCISSOR_TEST); }
        // Recorta lo que se dibuje a un rectangulo (coordenadas de pantalla, y hacia abajo); Suelta lo quita.
        internal void Recorta(float x, float y, float w, float h)
        {
            Vacia();
            GL.Enable(GL.SCISSOR_TEST);
            GL.Scissor((int)Math.Floor(x), (int)Math.Floor(Alto - y - h), Math.Max(0, (int)Math.Ceiling(w)), Math.Max(0, (int)Math.Ceiling(h)));
        }
        internal void SinRecorte() { Vacia(); GL.Disable(GL.SCISSOR_TEST); }

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
        // Un triangulo lleno (la estrella de la ayuda se arma con ellos).
        internal void Triangulo(float x1, float y1, float x2, float y2, float x3, float y3, Color4 c)
        {
            if (blanca.Id != texActual || nv + 3 > vertices.Length / 8) { Vacia(); texActual = blanca.Id; }
            V(x1, y1, 0.5f, 0.5f, c); V(x2, y2, 0.5f, 0.5f, c); V(x3, y3, 0.5f, 0.5f, c);
        }
        // Una linea gruesa (un rectangulo girado), con las puntas redondeadas por un brillo duro.
        internal void Linea(float x1, float y1, float x2, float y2, float grosor, Color4 c)
        {
            float dx = x2 - x1, dy = y2 - y1, l = (float)Math.Sqrt(dx * dx + dy * dy);
            if (l < 0.01f) return;
            float nx = -dy / l * grosor / 2, ny = dx / l * grosor / 2;
            Triangulo(x1 + nx, y1 + ny, x2 + nx, y2 + ny, x1 - nx, y1 - ny, c);
            Triangulo(x2 + nx, y2 + ny, x2 - nx, y2 - ny, x1 - nx, y1 - ny, c);
        }
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

        // Un cuadrilatero cualquiera con un color por esquina (en orden: arriba-izq, arriba-der, abajo-der, abajo-izq).
        internal void Cuatro(float x0, float y0, Color4 c0, float x1, float y1, Color4 c1, float x2, float y2, Color4 c2, float x3, float y3, Color4 c3)
        {
            if (blanca.Id != texActual || nv + 6 > vertices.Length / 8) { Vacia(); texActual = blanca.Id; }
            V(x0, y0, 0.5f, 0.5f, c0); V(x1, y1, 0.5f, 0.5f, c1); V(x3, y3, 0.5f, 0.5f, c3);
            V(x1, y1, 0.5f, 0.5f, c1); V(x2, y2, 0.5f, 0.5f, c2); V(x3, y3, 0.5f, 0.5f, c3);
        }
        // Una imagen en un cuadrilatero cualquiera (arriba-izq, arriba-der, abajo-der, abajo-izq): las portadas en perspectiva (V15, V20).
        internal void ImagenLibre(Textura t, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, Color4 c)
        {
            if (t.Id != texActual || nv + 6 > vertices.Length / 8) { Vacia(); texActual = t.Id; }
            V(x0, y0, 0, 0, c); V(x1, y1, 1, 0, c); V(x3, y3, 0, 1, c);
            V(x1, y1, 1, 0, c); V(x2, y2, 1, 1, c); V(x3, y3, 0, 1, c);
        }
        // Un degradado de izquierda a derecha.
        internal void DegradadoH(float x, float y, float w, float h, Color4 izq, Color4 der) { Cuatro(x, y, izq, x + w, y, der, x + w, y + h, der, x, y + h, izq); }
        // El borde de un rectangulo de esquinas redondas, de 'grosor' pixeles hacia dentro (una tira por cada tramo: sin solapes).
        internal void Contorno(float x, float y, float w, float h, float r, float grosor, Color4 c)
        {
            r = Math.Min(r, Math.Min(w, h) / 2f);
            float ri = Math.Max(0f, r - grosor);
            // los lados rectos
            Rect(x + r, y, w - 2 * r, grosor, c); Rect(x + r, y + h - grosor, w - 2 * r, grosor, c);
            Rect(x, y + r, grosor, h - 2 * r, c); Rect(x + w - grosor, y + r, grosor, h - 2 * r, c);
            // las cuatro esquinas, como anillos de un cuarto
            int pasos = Math.Max(4, (int)(r * 0.8f));
            float[] cxs = { x + r, x + w - r, x + w - r, x + r }, cys = { y + r, y + r, y + h - r, y + h - r };
            double[] a0 = { Math.PI, 1.5 * Math.PI, 0, 0.5 * Math.PI };
            for (int e = 0; e < 4; e++)
                for (int i = 0; i < pasos; i++)
                {
                    double t0 = a0[e] + 0.5 * Math.PI * i / pasos, t1 = a0[e] + 0.5 * Math.PI * (i + 1) / pasos;
                    float c0 = (float)Math.Cos(t0), s0 = (float)Math.Sin(t0), c1 = (float)Math.Cos(t1), s1 = (float)Math.Sin(t1);
                    Cuatro(cxs[e] + r * c0, cys[e] + r * s0, c, cxs[e] + r * c1, cys[e] + r * s1, c, cxs[e] + ri * c1, cys[e] + ri * s1, c, cxs[e] + ri * c0, cys[e] + ri * s0, c);
                }
        }
        // Un rectangulo redondo con un degradado vertical (las pastillas de XP).
        internal void RedondoDegradado(float x, float y, float w, float h, float r, Color4 arriba, Color4 abajo)
        {
            r = Math.Min(r, Math.Min(w, h) / 2f);
            int pasos = Math.Max(2, (int)r);
            for (int i = 0; i < pasos; i++)
            {
                float yy = r - (i + 0.5f) * r / pasos, dx = r - (float)Math.Sqrt(Math.Max(0, r * r - yy * yy)), h1 = r / pasos;
                float ya = y + i * h1, yb = y + h - (i + 1) * h1;
                Degradado(x + dx, ya, w - 2 * dx, h1, Mezcla(arriba, abajo, (ya - y) / h), Mezcla(arriba, abajo, (ya + h1 - y) / h));
                Degradado(x + dx, yb, w - 2 * dx, h1, Mezcla(arriba, abajo, (yb - y) / h), Mezcla(arriba, abajo, (yb + h1 - y) / h));
            }
            Degradado(x, y + r, w, h - 2 * r, Mezcla(arriba, abajo, r / h), Mezcla(arriba, abajo, 1 - r / h));
        }
        internal static Color4 Mezcla(Color4 a, Color4 b, float t)
        {
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return new Color4(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }
        // Una imagen con su color (para tenir una textura blanca: las nebulosas).
        internal void ImagenTenida(Textura t, float x, float y, float w, float h, Color4 c) { Quad(t, x, y, w, h, 0, 0, 1, 1, c); }

        public void Dispose() { }
    }

    // UNA LETRA HORNEADA a un tamano: los caracteres de 32 a 255 (Latin-1: acentos y la enie) en una textura.
    internal sealed unsafe class Letra
    {
        private readonly StbTrueType.stbtt_bakedchar[] car = new StbTrueType.stbtt_bakedchar[224];
        private readonly Textura tex;
        private readonly int lado;
        public readonly float Tam;
        // NEGRITA SINTETICA (como hace GDI+ con las letras que no traen negrita, p. ej. Microsoft Sans Serif): cada letra se pinta otra
        // vez corrida un pixel (dos en las grandes) y avanza eso de mas.
        private readonly int engorda;
        private readonly float subeBase = 0.8f;   // de lo alto del renglon a la linea base, en tamanos
        // 'tamEm': el tamano es el del cuadro «em», como lo cuenta GDI+ (Font en pixeles de XP); si no, el alto de arriba a abajo de la
        // letra (lo que cuenta stb_truetype), que sale un 10-15 % mas chica.
        internal Letra(Dibujo d, byte[] ttf, float tam, bool negritaSintetica = false, bool tamEm = false)
        {
            Tam = tam;
            float alto = tam;
            int em, sube, baja;
            if (tamEm && MetricaEm(ttf, out em, out sube, out baja)) { alto = tam * (sube - baja) / (float)em; subeBase = sube / (float)em; }
            engorda = negritaSintetica ? Math.Max(1, (int)Math.Round(tam / 16f)) : 0;
            lado = alto > 40 ? 1024 : 512;
            byte[] alfa = new byte[lado * lado];
            StbTrueType.stbtt_BakeFontBitmap(ttf, 0, alto, alfa, lado, lado, 32, 224, car);
            byte[] rgba = new byte[lado * lado * 4];
            for (int i = 0; i < alfa.Length; i++) { rgba[4 * i] = 255; rgba[4 * i + 1] = 255; rgba[4 * i + 2] = 255; rgba[4 * i + 3] = alfa[i]; }
            tex = d.Sube(rgba, lado, lado);
        }
        // unitsPerEm (head) y el ascender y descender (hhea) del TTF: lo que usa stb_truetype para su alto.
        private static bool MetricaEm(byte[] f, out int em, out int sube, out int baja)
        {
            em = sube = baja = 0;
            try
            {
                int n = (f[4] << 8) | f[5];
                for (int i = 0; i < n; i++)
                {
                    int r = 12 + 16 * i, off = (f[r + 8] << 24) | (f[r + 9] << 16) | (f[r + 10] << 8) | f[r + 11];
                    string tag = "" + (char)f[r] + (char)f[r + 1] + (char)f[r + 2] + (char)f[r + 3];
                    if (tag == "head") em = (f[off + 18] << 8) | f[off + 19];
                    else if (tag == "hhea") { sube = (short)((f[off + 4] << 8) | f[off + 5]); baja = (short)((f[off + 6] << 8) | f[off + 7]); }
                }
            }
            catch (IndexOutOfRangeException) { return false; }
            return em > 0 && sube > baja;
        }
        private static int Indice(char c) { int k = c; if (k == 0x2026) k = '.'; if (k < 32 || k > 255) k = '?'; return k - 32; }
        internal float Mide(string s)
        {
            float w = 0; if (s == null) return 0;
            foreach (char c in s) w += car[Indice(c)].xadvance + engorda;
            return w;
        }
        // Escribe con la linea base en y + 0,8 * tamano (y es el borde de arriba). Devuelve el ancho.
        internal float Escribe(Dibujo d, string s, float x, float y, Color4 c)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            float x0 = x, base_ = y + Tam * subeBase;
            foreach (char ch in s)
            {
                StbTrueType.stbtt_bakedchar b = car[Indice(ch)];
                float w = b.x1 - b.x0, h = b.y1 - b.y0;
                if (w > 0 && h > 0)
                    for (int e = 0; e <= engorda; e++)
                        d.Quad(tex, (float)Math.Round(x + b.xoff) + e, (float)Math.Round(base_ + b.yoff), w, h, b.x0 / (float)lado, b.y0 / (float)lado, b.x1 / (float)lado, b.y1 / (float)lado, c);
                x += b.xadvance + engorda;
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
        internal int Lado { get { return lado; } }
        // V4 (portadas_nitidas=): las que se carguen con esto encendido llevan mipmaps, y las portadas grandes piden otra copia al doble
        internal bool Nitidas;
        private readonly Dictionary<string, Textura> grandes = new Dictionary<string, Textura>(StringComparer.Ordinal);
        private readonly LinkedList<string> ordenGrandes = new LinkedList<string>();
        internal Textura De(string ruta) { return Carga(ruta, hechas, orden, lado, 96); }
        // Para dibujar a 'tam' pixeles: si pasa del lado de siempre (con las nitidas), la copia grande (las ultimas 6).
        internal Textura Para(string ruta, float tam)
        {
            if (Nitidas && tam > lado * 1.1f) { Textura g = Carga(ruta, grandes, ordenGrandes, lado * 2, 6); if (g != null) return g; }
            return De(ruta);
        }
        private Textura Carga(string ruta, Dictionary<string, Textura> cache, LinkedList<string> ord, int max, int cuantas)
        {
            if (string.IsNullOrEmpty(ruta) || sinImagen.Contains(ruta)) return null;
            Textura t;
            if (cache.TryGetValue(ruta, out t)) { ord.Remove(ruta); ord.AddFirst(ruta); return t; }
            try
            {
                ImageResult im = ImageResult.FromMemory(File.ReadAllBytes(ruta), ColorComponents.RedGreenBlueAlpha);
                int w = im.Width, h = im.Height; byte[] px = im.Data;
                if (Math.Max(w, h) > max) { int nw = Math.Max(1, w * max / Math.Max(w, h)), nh = Math.Max(1, h * max / Math.Max(w, h)); px = Reduce(px, w, h, nw, nh); w = nw; h = nh; }
                if (Nitidas)
                {
                    int s = 1; while (s * 2 <= Math.Max(64, max)) s *= 2;   // el lado potencia de 2 que cabe
                    t = d.SubeConMipmaps(Escala(px, w, h, s, s), s);
                    t.Ancho = w; t.Alto = h;   // la forma de la portada (la textura va estirada al cuadrado)
                }
                else t = d.Sube(px, w, h);
                t.Media = ColorMedio(px);
                Rocola.VistaGrafica.ColoresDePortada(px, w, h, out t.Prom, out t.Uno, out t.Dos);
            }
            catch { sinImagen.Add(ruta); return null; }
            cache[ruta] = t; ord.AddFirst(ruta);
            while (ord.Count > cuantas) { string v = ord.Last.Value; ord.RemoveLast(); d.Borra(cache[v]); cache.Remove(v); }
            return t;
        }
        // V7, V13: LA PORTADA BORROSA (vidrio esmerilado): achicada a 24x24 por promedio y suavizada una vez mas; estirada a toda la
        // pantalla, el filtro lineal de la tarjeta hace el resto. Se hace una vez por portada (las ultimas 8): no cuesta en cada cuadro.
        private readonly Dictionary<string, Textura> borrosas = new Dictionary<string, Textura>(StringComparer.Ordinal);
        private readonly LinkedList<string> ordenBorrosas = new LinkedList<string>();
        internal Textura Borrosa(string ruta)
        {
            if (string.IsNullOrEmpty(ruta) || sinImagen.Contains(ruta)) return null;
            Textura t;
            if (borrosas.TryGetValue(ruta, out t)) { ordenBorrosas.Remove(ruta); ordenBorrosas.AddFirst(ruta); return t; }
            try
            {
                ImageResult im = ImageResult.FromMemory(File.ReadAllBytes(ruta), ColorComponents.RedGreenBlueAlpha);
                const int L = 24;
                byte[] px = Reduce(im.Data, im.Width, im.Height, L, L), r = new byte[px.Length];
                for (int y = 0; y < L; y++) for (int x = 0; x < L; x++) for (int c = 0; c < 4; c++)
                {
                    int s = 0, n = 0;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { int yy = y + dy, xx = x + dx; if (yy < 0 || xx < 0 || yy >= L || xx >= L) continue; s += px[(yy * L + xx) * 4 + c]; n++; }
                    r[(y * L + x) * 4 + c] = (byte)(s / n);
                }
                t = d.Sube(r, L, L);
            }
            catch { sinImagen.Add(ruta); return null; }
            borrosas[ruta] = t; ordenBorrosas.AddFirst(ruta);
            while (ordenBorrosas.Count > 8) { string v = ordenBorrosas.Last.Value; ordenBorrosas.RemoveLast(); d.Borra(borrosas[v]); borrosas.Remove(v); }
            return t;
        }
        // A otro tamano: achicar por promedio de cajas; agrandar, bilineal.
        private static byte[] Escala(byte[] s, int w, int h, int nw, int nh)
        {
            if (nw <= w && nh <= h) return Reduce(s, w, h, nw, nh);
            byte[] r = new byte[nw * nh * 4];
            for (int y = 0; y < nh; y++)
            {
                float fy = Math.Max(0, (y + 0.5f) * h / nh - 0.5f); int y0 = Math.Min(h - 1, (int)fy), y1 = Math.Min(h - 1, y0 + 1); float ty = fy - y0;
                for (int x = 0; x < nw; x++)
                {
                    float fx = Math.Max(0, (x + 0.5f) * w / nw - 0.5f); int x0 = Math.Min(w - 1, (int)fx), x1 = Math.Min(w - 1, x0 + 1); float tx = fx - x0;
                    for (int c = 0; c < 4; c++)
                    {
                        float a = s[(y0 * w + x0) * 4 + c] * (1 - tx) + s[(y0 * w + x1) * 4 + c] * tx, b = s[(y1 * w + x0) * 4 + c] * (1 - tx) + s[(y1 * w + x1) * 4 + c] * tx;
                        r[(y * nw + x) * 4 + c] = (byte)(a * (1 - ty) + b * ty + 0.5f);
                    }
                }
            }
            return r;
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
