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
        public bool Opaca;   // todos sus puntos opacos (las portadas): dibujada con alfa 1, no hace falta mezclar
        public Color4 Media;   // el color de la imagen (para el halo de la portada enfocada, como en XP)
        public Rocola.VistaGrafica.C3 Prom, Uno, Dos;   // de que color es la portada (el cielo toma su tono: ColorFondo.cs)
        // Un cuadrito blanco opaco dentro de la textura (los atlas de letras y el punto suave): con el, un rectangulo de color se dibuja
        // sin cambiar de textura, en el mismo lote que las letras (menos llamadas a la tarjeta; se ve igual).
        public bool TieneBlanco; public float BlancoU, BlancoV;
    }

    internal sealed unsafe partial class Dibujo : IDisposable
    {
        private readonly uint programa, buffer, programaCine;
        private readonly int uCineEsc, uCineTex, uCineA;
        private readonly int uEscala, uTex;
        private readonly float[] vertices = new float[6 * 8 * 2048];
        private int nv;
        private uint texActual; private Textura texActualT;
        private readonly Textura blanca;
        internal readonly Textura Punto;   // un punto suave (estrellas, halos); el punto ocupa v de 0 a puntoV1
        internal readonly float puntoV1;
        public int Ancho, Alto;

        internal static Dibujo Actual;   // para el perfil del banco (vaciar el lote en cada marca)
        internal void VaciaYa() { Vacia(); }
        internal Dibujo(int ancho, int alto)
        {
            Ancho = ancho; Alto = alto; Actual = this;
            programa = Programa(
                "attribute vec2 p; attribute vec2 t; attribute vec4 c; uniform vec2 esc; varying vec2 vt; varying vec4 vc;" +
                "void main(){ vt = t; vc = c; gl_Position = vec4(p.x * esc.x - 1.0, 1.0 - p.y * esc.y, 0.0, 1.0); }",
                "precision mediump float; uniform sampler2D tex; varying vec2 vt; varying vec4 vc;" +
                "void main(){ gl_FragColor = texture2D(tex, vt) * vc; }");
            uEscala = GL.GetUniformLocation(programa, "esc"); uTex = GL.GetUniformLocation(programa, "tex");
            // LA PORTADA DE CINE CON SU VELO EN UNA SOLA PASADA (V19): la imagen con alfa 'a' y encima un velo de color (el del vertice, con
            // su alfa); lo mismo que dibujar las dos a pantalla completa una tras otra, pero la tarjeta lee y escribe la pantalla una vez.
            // Sale premultiplicado: se mezcla con (1, 1 - alfa).
            programaCine = Programa(
                "attribute vec2 p; attribute vec2 t; attribute vec4 c; uniform vec2 esc; varying vec2 vt; varying vec4 vc;" +
                "void main(){ vt = t; vc = c; gl_Position = vec4(p.x * esc.x - 1.0, 1.0 - p.y * esc.y, 0.0, 1.0); }",
                "precision mediump float; uniform sampler2D tex; uniform float a; varying vec2 vt; varying vec4 vc;" +
                "void main(){ vec4 t = texture2D(tex, vt); float x = a * t.a; gl_FragColor = vec4(t.rgb * x * (1.0 - vc.a) + vc.rgb * vc.a, 1.0 - (1.0 - x) * (1.0 - vc.a)); }");
            uCineEsc = GL.GetUniformLocation(programaCine, "esc"); uCineTex = GL.GetUniformLocation(programaCine, "tex"); uCineA = GL.GetUniformLocation(programaCine, "a");
            uint b; GL.GenBuffers(1, &b); buffer = b;
            byte[] px = { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 };
            blanca = Sube(px, 2, 2);
            const int L = 64; byte[] pt = new byte[L * (L + 6) * 4];
            for (int y = 0; y < L; y++) for (int x = 0; x < L; x++)
            {
                double dx = (x + 0.5) / L * 2 - 1, dy = (y + 0.5) / L * 2 - 1, r = Math.Sqrt(dx * dx + dy * dy);
                double a = r >= 1 ? 0 : Math.Pow(1 - r, 2.2);
                int o = (y * L + x) * 4; pt[o] = pt[o + 1] = pt[o + 2] = 255; pt[o + 3] = (byte)(255 * a);
            }
            for (int i = (L + 2) * L * 4; i < pt.Length; i++) pt[i] = 255;   // dos filas transparentes y cuatro blancas debajo (el cuadrito blanco)
            Punto = SubeBlanca(pt, L, L + 6);
            Punto.TieneBlanco = true; Punto.BlancoU = 0.5f; Punto.BlancoV = (L + 4f) / (L + 6f);
            puntoV1 = L / (L + 6f);
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
        // BLANCO CON SU ALFA EN DOS BYTES (3-11-2026): las letras, el punto suave y la nebulosa son blancos con transparencia; en
        // luminancia + alfa la tarjeta lee exactamente lo mismo (1, 1, 1, alfa) con la mitad de memoria y de lectura.
        internal Textura SubeBlanca(byte[] rgba, int ancho, int alto)
        {
            byte[] la = new byte[ancho * alto * 2];
            for (int i = 0, j = 0; j < la.Length; i += 4, j += 2)
            {
                if (rgba[i] != 255 || rgba[i + 1] != 255 || rgba[i + 2] != 255) return Sube(rgba, ancho, alto);   // no era blanca: como siempre
                la[j] = 255; la[j + 1] = rgba[i + 3];
            }
            return Sube(la, ancho, alto, GL.LUMINANCE_ALPHA);
        }
        internal Textura Sube(byte[] rgba, int ancho, int alto) { return Sube(rgba, ancho, alto, GL.RGBA); }
        internal Textura SubeLuminancia(byte[] la, int ancho, int alto) { return Sube(la, ancho, alto, GL.LUMINANCE_ALPHA); }
        // Un atlas de letras vacio (blanco transparente, en luminancia + alfa) con el cuadrito blanco opaco de 4x4 en la esquina de abajo a
        // la derecha; las letras se le suben despues, cada una al usarla (SubeTrozo).
        private static byte[] ceros;
        internal Textura AtlasBlanco(int lado)
        {
            if (ceros == null || ceros.Length < lado * lado * 2) ceros = new byte[lado * lado * 2];   // siempre ceros: solo se lee
            Textura t = Sube(ceros, lado, lado, GL.LUMINANCE_ALPHA);
            byte* b = stackalloc byte[4 * 4 * 2];
            for (int i = 0; i < 32; i++) b[i] = 255;
            SubeTrozo(t, lado - 4, lado - 4, 4, 4, b);
            return t;
        }
        // Un trozo de w x h puntos (luminancia + alfa) en x, y de la textura t.
        internal void SubeTrozo(Textura t, int x, int y, int w, int h, byte* la)
        {
            GL.BindTexture(GL.TEXTURE_2D, t.Id);
            GL.PixelStorei(GL.UNPACK_ALIGNMENT, 1);
            GL.TexSubImage2D(GL.TEXTURE_2D, 0, x, y, w, h, GL.LUMINANCE_ALPHA, GL.UNSIGNED_BYTE, la);
            GL.BindTexture(GL.TEXTURE_2D, texActual);   // la atada sigue siendo la del lote (lo pendiente se dibuja con ella)
        }
        private Textura Sube(byte[] rgba, int ancho, int alto, uint formato)
        {
            Vacia();   // lo pendiente se dibuja antes con su textura (subir cambia la textura atada)
            uint t; GL.GenTextures(1, &t);
            GL.BindTexture(GL.TEXTURE_2D, t);
            GL.PixelStorei(GL.UNPACK_ALIGNMENT, 1);
            fixed (byte* p = rgba) GL.TexImage2D(GL.TEXTURE_2D, 0, (int)formato, ancho, alto, 0, formato, GL.UNSIGNED_BYTE, p);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GL.LINEAR); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GL.LINEAR);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.CLAMP_TO_EDGE); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.CLAMP_TO_EDGE);
            texActual = 0; texActualT = null;
            return new Textura { Id = t, Ancho = ancho, Alto = alto };
        }
        // Con sus tamanos reducidos (mipmaps; lados potencia de 2, como pide OpenGL ES 2): se achica sin granos (V4).
        internal Textura SubeConMipmaps(byte[] rgba, int lado)
        {
            Textura t = Sube(rgba, lado, lado);
            GL.BindTexture(GL.TEXTURE_2D, t.Id);
            GL.GenerateMipmap(GL.TEXTURE_2D);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GL.LINEAR_MIPMAP_LINEAR);
            texActual = 0; texActualT = null;
            return t;
        }
        // V24: lo que ya esta dibujado en un rectangulo de la pantalla, copiado a una textura (siempre la misma: una a la vez). Las filas
        // vienen de abajo arriba (como las da OpenGL): se dibuja con v de 1 a 0.
        private Textura copia;
        internal Textura CopiaPantalla(float x, float y, float w, float h)
        {
            Vacia(); LimpiaSiFalta();
            int ix = Math.Max(0, (int)x), iy = Math.Max(0, (int)y), iw = Math.Min(Ancho - ix, (int)Math.Ceiling(w)), ih = Math.Min(Alto - iy, (int)Math.Ceiling(h));
            if (iw < 2 || ih < 2) return null;
            if (copia == null) { uint t; GL.GenTextures(1, &t); copia = new Textura { Id = t }; }
            GL.BindTexture(GL.TEXTURE_2D, copia.Id);
            GL.CopyTexImage2D(GL.TEXTURE_2D, 0, GL.RGB, ix, Alto - iy - ih, iw, ih, 0);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GL.LINEAR); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GL.LINEAR);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.CLAMP_TO_EDGE); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.CLAMP_TO_EDGE);
            texActual = 0; texActualT = null;
            copia.Ancho = iw; copia.Alto = ih;
            return copia;
        }
        // UNA CAPA (dibujar en una textura en vez de en la pantalla, y despues ponerla de un golpe): el fondo lento del cielo. Del tamano
        // de la pantalla, con las mismas coordenadas; las filas quedan de abajo arriba (se pone con v de 1 a 0). null si la tarjeta no puede.
        internal sealed class Capa { internal Textura Tex; internal uint Fbo; }
        private int fboPantalla;
        internal Capa CapaNueva() { return CapaNueva(Ancho, Alto); }
        internal Capa CapaNueva(int ancho, int alto)
        {
            Vacia();
            int antes; GL.GetIntegerv(GL.FRAMEBUFFER_BINDING, &antes);
            uint t; GL.GenTextures(1, &t);
            GL.BindTexture(GL.TEXTURE_2D, t);
            GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, ancho, alto, 0, GL.RGBA, GL.UNSIGNED_BYTE, null);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GL.LINEAR); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GL.LINEAR);
            GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.CLAMP_TO_EDGE); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.CLAMP_TO_EDGE);
            uint f; GL.GenFramebuffers(1, &f);
            GL.BindFramebuffer(GL.FRAMEBUFFER, f);
            GL.FramebufferTexture2D(GL.FRAMEBUFFER, GL.COLOR_ATTACHMENT0, GL.TEXTURE_2D, t, 0);
            bool ok = GL.CheckFramebufferStatus(GL.FRAMEBUFFER) == GL.FRAMEBUFFER_COMPLETE;
            GL.BindFramebuffer(GL.FRAMEBUFFER, (uint)antes);
            texActual = 0; texActualT = null;
            if (!ok) { GL.DeleteTextures(1, &t); return null; }
            return new Capa { Tex = new Textura { Id = t, Ancho = ancho, Alto = alto }, Fbo = f };
        }
        internal void DibujaEn(Capa c)
        {
            Vacia();
            int antes; GL.GetIntegerv(GL.FRAMEBUFFER_BINDING, &antes); fboPantalla = antes;
            GL.Disable(GL.SCISSOR_TEST); SinRect();
            GL.BindFramebuffer(GL.FRAMEBUFFER, c.Fbo); enCapa = true;
            GL.Viewport(0, 0, Ancho, Alto);
        }
        internal void Vuelve()
        {
            Vacia();
            GL.Disable(GL.SCISSOR_TEST); SinRect();
            GL.BindFramebuffer(GL.FRAMEBUFFER, (uint)fboPantalla); enCapa = false;
            GL.Viewport(0, 0, Ancho, Alto);
        }
        // La capa sobre toda la pantalla, sin mezclar (es opaca: lo de debajo no se lee, la tarjeta solo copia).
        internal void PonCapa(Capa c) { PonCapa(c, 0, 0, 0, 0); }
        // La imagen 't' (su trozo u0..u1, v0..v1) en x, y, w, h con alfa 'a', y encima un velo de 'arriba' a 'abajo' (degradado), de una vez.
        internal void ImagenConVelo(Textura t, float x, float y, float w, float h, float u0, float v0, float u1, float v1, float a, Color4 arriba, Color4 abajo)
        {
            Vacia();
            texActual = t.Id; texActualT = t; opacoV = false;
            GL.UseProgram(programaCine); GL.Uniform2f(uCineEsc, 2f / Ancho, 2f / Alto); GL.Uniform1i(uCineTex, 0); GL.Uniform1f(uCineA, a);
            GL.BlendFunc(GL.ONE, GL.ONE_MINUS_SRC_ALPHA);
            V(x, y, u0, v0, arriba); V(x + w, y, u1, v0, arriba); V(x, y + h, u0, v1, abajo);
            V(x + w, y, u1, v0, arriba); V(x + w, y + h, u1, v1, abajo); V(x, y + h, u0, v1, abajo);
            Vacia();
            GL.BlendFunc(GL.SRC_ALPHA, GL.ONE_MINUS_SRC_ALPHA); GL.UseProgram(programa);
        }
        // Sin mezclar (para lo opaco: la tarjeta no lee lo de debajo; con alfa 1 sale lo mismo) o mezclando, como siempre.
        internal void Mezcla(bool si) { Vacia(); mezclando = si; if (si) GL.Enable(GL.BLEND); else GL.Disable(GL.BLEND); }
        // ... salvo un rectangulo que luego se tapa con algo opaco (la ventana de los ajustes): solo lo que queda alrededor.
        internal void PonCapa(Capa c, float ex, float ey, float ew, float eh)
        {
            Vacia();
            if (!enCapa) limpiar = false;   // la capa (y, si hay hueco, lo opaco que lo tapa) cubre toda la pantalla
            GL.Disable(GL.BLEND); mezclando = false;
            Color4 b = new Color4(1, 1, 1, 1);
            if (ew <= 0 || eh <= 0) Quad(c.Tex, 0, 0, Ancho, Alto, 0, 1, 1, 0, b);
            else
            {
                float x1 = ex + ew, y1 = ey + eh;
                ParteCapa(c, 0, 0, Ancho, ey, b); ParteCapa(c, 0, y1, Ancho, Alto - y1, b);
                ParteCapa(c, 0, ey, ex, eh, b); ParteCapa(c, x1, ey, Ancho - x1, eh, b);
            }
            Vacia();
            GL.Enable(GL.BLEND); mezclando = true;
        }
        private void ParteCapa(Capa c, float x, float y, float w, float h, Color4 b)
        {
            if (w <= 0 || h <= 0) return;
            Quad(c.Tex, x, y, w, h, x / Ancho, 1f - y / Alto, (x + w) / Ancho, 1f - (y + h) / Alto, b);
        }
        internal void Borra(Textura t) { if (t == null) return; Vacia(); uint id = t.Id; GL.DeleteTextures(1, &id); texActual = 0; texActualT = null; }

        internal void Empieza(Color4 fondo)
        {
            Zoom = false;   // (si un cuadro fallo a medio zoom, el siguiente no sale chiquito)
            GL.Viewport(0, 0, Ancho, Alto);
            // BORRAR SOLO SI HACE FALTA: si lo primero es la capa del cielo (opaca, a toda la pantalla), borrar seria escribir la pantalla
            // entera para nada; se borra cuando se vaya a dibujar otra cosa antes
            GL.ClearColor(fondo.R, fondo.G, fondo.B, 1f); limpiar = true; enCapa = false;
            GL.Enable(GL.BLEND); mezclando = true; GL.BlendFunc(GL.SRC_ALPHA, GL.ONE_MINUS_SRC_ALPHA);
            GL.UseProgram(programa); GL.Uniform2f(uEscala, 2f / Ancho, 2f / Alto); GL.Uniform1i(uTex, 0); GL.ActiveTexture(GL.TEXTURE0);
            GL.BindBuffer(GL.ARRAY_BUFFER, buffer);
            GL.VertexAttribPointer(0, 2, GL.FLOAT, 0, 32, (IntPtr)0); GL.VertexAttribPointer(1, 2, GL.FLOAT, 0, 32, (IntPtr)8); GL.VertexAttribPointer(2, 4, GL.FLOAT, 0, 32, (IntPtr)16);
            GL.EnableVertexAttribArray(0); GL.EnableVertexAttribArray(1); GL.EnableVertexAttribArray(2);
            texActual = 0; texActualT = null; nv = 0;
        }
        internal void Termina() { Vacia(); LimpiaSiFalta(); GL.Disable(GL.SCISSOR_TEST); SinRect(); }
        private void SinRect() { rx0 = ry0 = 0; rx1 = ry1 = 1e9f; }
        // Recorta lo que se dibuje a un rectangulo (coordenadas de pantalla, y hacia abajo); Suelta lo quita.
        internal void Recorta(float x, float y, float w, float h)
        {
            if (Zoom) { x = x * ZoomS + ZoomX; y = y * ZoomS + ZoomY; w *= ZoomS; h *= ZoomS; }
            Vacia(); LimpiaSiFalta();
            GL.Enable(GL.SCISSOR_TEST); rx0 = x; ry0 = y; rx1 = x + w; ry1 = y + h;
            GL.Scissor((int)Math.Floor(x), (int)Math.Floor(Alto - y - h), Math.Max(0, (int)Math.Ceiling(w)), Math.Max(0, (int)Math.Ceiling(h)));
        }
        internal void SinRecorte() { Vacia(); GL.Disable(GL.SCISSOR_TEST); SinRect(); }

        // Para el banco: sin tocar la tarjeta (solo lo que cuesta el programa) y la cuenta de dibujos y vertices.
        internal static readonly bool SinGL = Environment.GetEnvironmentVariable("ROCOLA_BANCO_SIN_GL") != null;
        internal static long Dibujos, Vertices;
        // EL RELLENO (solo con el banco): cuantos puntos se pintan por cuadro, sumando el area de cada triangulo recortada a la pantalla
        // (o al recorte puesto). En la tarjeta del A4 (memoria compartida) es lo que mas cuesta: cada pantalla entera mezclada lee y
        // escribe toda la imagen.
        internal static bool CuentaRelleno; internal static double Relleno, RellenoMezclado;   // lo mezclado lee y escribe; lo opaco solo escribe
        private float rx0, ry0, rx1 = 1e9f, ry1 = 1e9f;
        private void Rellena()
        {
            float x0 = Math.Max(0, rx0), y0 = Math.Max(0, ry0), x1 = Math.Min(Ancho, rx1), y1 = Math.Min(Alto, ry1);
            for (int i = 0; i + 2 < nv; i += 3)
            {
                int a = i * 8, b = a + 8, c = b + 8;
                float ax = Math.Clamp(vertices[a], x0, x1), ay = Math.Clamp(vertices[a + 1], y0, y1), bx = Math.Clamp(vertices[b], x0, x1), by = Math.Clamp(vertices[b + 1], y0, y1), cx = Math.Clamp(vertices[c], x0, x1), cy = Math.Clamp(vertices[c + 1], y0, y1);
                Relleno += Math.Abs((bx - ax) * (cy - ay) - (cx - ax) * (by - ay)) * 0.5;
            }
        }
        private bool limpiar, enCapa;
        private void LimpiaSiFalta() { if (limpiar && !enCapa) { limpiar = false; GL.Disable(GL.SCISSOR_TEST); GL.Clear(GL.COLOR_BUFFER_BIT); } }
        private void Vacia()
        {
            if (nv == 0) return;
            LimpiaSiFalta();
            Dibujos++; Vertices += nv;
            if (CuentaRelleno) { double antes = Relleno; Rellena(); if (mezclando && !(loteOpaco && !sinLotes)) RellenoMezclado += Relleno - antes; }
            if (SinGL) { nv = 0; loteOpaco = true; loteGrande = false; return; }
            GL.BindTexture(GL.TEXTURE_2D, texActual);
            fixed (float* v = vertices) GL.BufferData(GL.ARRAY_BUFFER, (IntPtr)(nv * 32), v, GL.STREAM_DRAW);
            bool sinMezcla = loteOpaco && mezclando && !sinLotes;
            if (sinMezcla) GL.Disable(GL.BLEND);
            GL.DrawArrays(GL.TRIANGLES, 0, nv);
            if (sinMezcla) GL.Enable(GL.BLEND);
            nv = 0; loteOpaco = true; loteGrande = false;
        }
        // LO OPACO SIN MEZCLAR (3-11-2026): si todo lo de un lote es opaco (portadas opacas o colores lisos, con alfa 1), se dibuja sin
        // mezclar: sale igual (con alfa 1 la mezcla da el color tal cual) y la tarjeta no lee lo de debajo (la mitad de memoria por punto).
        // Cada forma dice antes de sus vertices si lo suyo es opaco (opacoV); el alfa de cada vertice tambien cuenta.
        private bool loteOpaco = true, opacoV, mezclando = true, loteGrande;
        // Una forma opaca grande (mas de 1/16 de pantalla) va en su lote propio, sin mezclar, aunque cueste una llamada mas; lo translucido
        // que venga detras empieza otro lote.
        private void Forma(bool opaca, float area)
        {
            opacoV = opaca;
            if (sinLotes || nv == 0) { if (opaca && area * 16f > (float)Ancho * Alto) loteGrande = true; return; }
            if (opaca && area * 16f > (float)Ancho * Alto) { if (!loteOpaco) Vacia(); loteGrande = true; }
            else if (loteGrande && !opaca) Vacia();
        }
        // EL ZOOM (7-11-2026: el artista de Spotify que se abre desde su casilla del buscador): lo que se dibuje con Zoom puesto se escala
        // y se corre (x * ZoomS + ZoomX) y su alfa se multiplica por ZoomA. Apagado no cambia nada.
        internal bool Zoom; internal float ZoomS = 1f, ZoomX, ZoomY, ZoomA = 1f;
        private void V(float x, float y, float u, float w, Color4 c)
        {
            if (Zoom) { x = x * ZoomS + ZoomX; y = y * ZoomS + ZoomY; c.A *= ZoomA; }
            if (!opacoV || c.A < 1f) loteOpaco = false;
            int o = nv * 8;
            vertices[o] = x; vertices[o + 1] = y; vertices[o + 2] = u; vertices[o + 3] = w;
            vertices[o + 4] = c.R; vertices[o + 5] = c.G; vertices[o + 6] = c.B; vertices[o + 7] = c.A; nv++;
        }
        // LO INVISIBLE NO SE MANDA (3-11-2026): con la mezcla de siempre (alfa, 1 - alfa), lo de alfa 0 no cambia ni un punto (lo que se
        // desvanece y ya no se ve): ni vertices ni relleno.
        internal void Quad(Textura t, float x, float y, float w, float h, float u0, float v0, float u1, float v1, Color4 c)
        {
            if (c.A <= 0f) return;
            if (t.Id != texActual || nv + 6 > vertices.Length / 8) { Vacia(); texActual = t.Id; texActualT = t; }
            Forma(t.Opaca && c.A >= 1f, Math.Abs(w * h));
            V(x, y, u0, v0, c); V(x + w, y, u1, v0, c); V(x, y + h, u0, v1, c);
            V(x + w, y, u1, v0, c); V(x + w, y + h, u1, v1, c); V(x, y + h, u0, v1, c);
        }
        // Donde tomar el blanco para n vertices de color liso: el cuadrito blanco de la textura que ya esta puesta, o la textura blanca.
        private static readonly bool sinLotes = Environment.GetEnvironmentVariable("ROCOLA_SIN_LOTES") != null;   // pruebas: como antes
        private void Liso(int n, out float u, out float v)
        {
            if (nv + n > vertices.Length / 8) Vacia();
            if (!sinLotes && texActualT != null && texActualT.TieneBlanco && texActual == texActualT.Id) { u = texActualT.BlancoU; v = texActualT.BlancoV; return; }
            if (blanca.Id != texActual) { Vacia(); texActual = blanca.Id; texActualT = blanca; }
            u = 0.5f; v = 0.5f;
        }
        internal void Rect(float x, float y, float w, float h, Color4 c)
        {
            if (c.A <= 0f) return;
            float u, v; Liso(6, out u, out v); Forma(c.A >= 1f, Math.Abs(w * h));
            V(x, y, u, v, c); V(x + w, y, u, v, c); V(x, y + h, u, v, c);
            V(x + w, y, u, v, c); V(x + w, y + h, u, v, c); V(x, y + h, u, v, c);
        }
        // Un triangulo lleno (la estrella de la ayuda se arma con ellos).
        internal void Triangulo(float x1, float y1, float x2, float y2, float x3, float y3, Color4 c)
        {
            if (c.A <= 0f) return;
            float u, v; Liso(3, out u, out v); Forma(c.A >= 1f, 0);
            V(x1, y1, u, v, c); V(x2, y2, u, v, c); V(x3, y3, u, v, c);
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
            if (arriba.A <= 0f && abajo.A <= 0f) return;
            float u, v; Liso(6, out u, out v); Forma(arriba.A >= 1f && abajo.A >= 1f, Math.Abs(w * h));
            V(x, y, u, v, arriba); V(x + w, y, u, v, arriba); V(x, y + h, u, v, abajo);
            V(x + w, y, u, v, arriba); V(x + w, y + h, u, v, abajo); V(x, y + h, u, v, abajo);
        }
        // EL BRILLO GRANDE, SIN SUS ESQUINAS (3-11-2026): el punto se apaga como (1 - r)^2,2, asi que las esquinas del cuadrado y el anillo
        // de fuera no cambian ningun punto (lo que pondrian es menos de medio escalon de 255); un poligono de 16 lados que cubre solo lo
        // que se ve pinta un 25-40 % menos. Los pequenos siguen en cuadrado (ahi no cuenta).
        internal static bool BrilloCuadrado = Environment.GetEnvironmentVariable("ROCOLA_SIN_LOTES") != null;
        private static readonly float[] cos16 = new float[17], sin16 = new float[17];
        static Dibujo() { for (int i = 0; i <= 16; i++) { double a = i * Math.PI / 8; cos16[i] = (float)Math.Cos(a); sin16[i] = (float)Math.Sin(a); } }
        internal void Brillo(float cx, float cy, float radio, Color4 c)
        {
            if (c.A <= 0f) return;
            if (radio < 24f || BrilloCuadrado) { Quad(Punto, cx - radio, cy - radio, 2 * radio, 2 * radio, 0, 0, 1, puntoV1, c); return; }
            // hasta donde se ve: donde (1 - r)^2,2 por el alfa baja de medio escalon (1/510), mas un tramo de dos puntos de la textura por
            // el filtrado; los vertices van por fuera de ese circulo (el poligono lo envuelve)
            float corte = 1f - (float)Math.Pow(1.0 / (510.0 * Math.Min(1f, c.A)), 1.0 / 2.2) + 0.05f;
            float R = Math.Min(0.98f, Math.Max(0.3f, corte)) / 0.98079f;   // 0,98079 = cos(11,25 grados): el poligono por fuera del circulo
            if (Punto.Id != texActual || nv + 48 > vertices.Length / 8) { Vacia(); texActual = Punto.Id; texActualT = Punto; }
            Forma(false, 0);
            float uc = 0.5f, vc = 0.5f * puntoV1;
            for (int i = 0; i < 16; i++)
            {
                float x0 = cos16[i] * R, y0 = sin16[i] * R, x1 = cos16[i + 1] * R, y1 = sin16[i + 1] * R;
                V(cx, cy, uc, vc, c);
                V(cx + x0 * radio, cy + y0 * radio, 0.5f + 0.5f * x0, (0.5f + 0.5f * y0) * puntoV1, c);
                V(cx + x1 * radio, cy + y1 * radio, 0.5f + 0.5f * x1, (0.5f + 0.5f * y1) * puntoV1, c);
            }
        }
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
            if (c0.A <= 0f && c1.A <= 0f && c2.A <= 0f && c3.A <= 0f) return;
            float u, v; Liso(6, out u, out v); Forma(c0.A >= 1f && c1.A >= 1f && c2.A >= 1f && c3.A >= 1f, 0);
            V(x0, y0, u, v, c0); V(x1, y1, u, v, c1); V(x3, y3, u, v, c3);
            V(x1, y1, u, v, c1); V(x2, y2, u, v, c2); V(x3, y3, u, v, c3);
        }
        // Una imagen en un cuadrilatero cualquiera (arriba-izq, arriba-der, abajo-der, abajo-izq): las portadas en perspectiva (V15, V20).
        internal void ImagenLibre(Textura t, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, Color4 c)
        {
            if (t.Id != texActual || nv + 6 > vertices.Length / 8) { Vacia(); texActual = t.Id; texActualT = t; }
            Forma(t.Opaca && c.A >= 1f, 0);
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
            Span<float> cxs = stackalloc float[] { x + r, x + w - r, x + w - r, x + r }, cys = stackalloc float[] { y + r, y + r, y + h - r, y + h - r };   // en la pila: sin basura
            float[] cs = CosSen(pasos);
            for (int e = 0; e < 4; e++)
                for (int i = 0; i < pasos; i++)
                {
                    int j = 2 * (e * (pasos + 1) + i);
                    float c0 = cs[j], s0 = cs[j + 1], c1 = cs[j + 2], s1 = cs[j + 3];
                    Cuatro(cxs[e] + r * c0, cys[e] + r * s0, c, cxs[e] + r * c1, cys[e] + r * s1, c, cxs[e] + ri * c1, cys[e] + ri * s1, c, cxs[e] + ri * c0, cys[e] + ri * s0, c);
                }
        }
        // LOS ANGULOS DE LAS ESQUINAS, UNA VEZ (3-11-2026): el vidrio pone cinco contornos por panel (tres de sombra, el filo y el de las
        // pastillas) y cada uno calculaba 16 senos y cosenos por paso. Se guardan por numero de pasos, con la misma cuenta (en double, y a
        // float al final): los mismos numeros, sin calcularlos en cada cuadro.
        private static readonly Dictionary<int, float[]> cosSen = new Dictionary<int, float[]>();
        private static float[] CosSen(int pasos)
        {
            float[] t;
            if (cosSen.TryGetValue(pasos, out t)) return t;
            double[] a0 = { Math.PI, 1.5 * Math.PI, 0, 0.5 * Math.PI };
            t = new float[2 * 4 * (pasos + 1)];
            for (int e = 0; e < 4; e++)
                for (int i = 0; i <= pasos; i++)
                {
                    double a = a0[e] + 0.5 * Math.PI * i / pasos;
                    t[2 * (e * (pasos + 1) + i)] = (float)Math.Cos(a); t[2 * (e * (pasos + 1) + i) + 1] = (float)Math.Sin(a);
                }
            if (cosSen.Count < 512) cosSen[pasos] = t;
            return t;
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

        // UNA IMAGEN CON MUCHO TRANSPARENTE, POR TRAMOS (3-11-2026; las nebulosas): la textura se parte en celdas de 'lado' texeles y se
        // apuntan, fila a fila, los tramos de celdas con algo (una celda cuenta como vacia si ella y un texel alrededor -lo que lee el
        // filtrado- son transparentes del todo). Al dibujarla solo van esos tramos, y los que caen dentro del recorte puesto: lo vacio no
        // pintaba nada (alfa 0), asi que sale igual con bastante menos relleno.
        internal sealed class Rejilla { public int Lado, TexW, TexH; public int[] Tramos; }   // fila, celda inicial, celda final (exclusiva)
        internal static Rejilla HazRejilla(byte[] rgba, int w, int h, int lado)
        {
            int cols = (w + lado - 1) / lado, filas = (h + lado - 1) / lado;
            List<int> t = new List<int>();
            for (int fy = 0; fy < filas; fy++)
            {
                int empieza = -1;
                for (int fx = 0; fx <= cols; fx++)
                {
                    bool algo = false;
                    if (fx < cols)
                        for (int y = Math.Max(0, fy * lado - 1); y < Math.Min(h, fy * lado + lado + 1) && !algo; y++)
                            for (int x = Math.Max(0, fx * lado - 1); x < Math.Min(w, fx * lado + lado + 1); x++)
                                if (rgba[(y * w + x) * 4 + 3] != 0) { algo = true; break; }
                    if (algo && empieza < 0) empieza = fx;
                    else if (!algo && empieza >= 0) { t.Add(fy); t.Add(empieza); t.Add(fx); empieza = -1; }
                }
            }
            return new Rejilla { Lado = lado, TexW = w, TexH = h, Tramos = t.ToArray() };
        }
        internal void QuadRejilla(Textura t, Rejilla r, float x, float y, float w, float h, float u0, float v0, float u1, float v1, Color4 c)
        {
            if (c.A <= 0f) return;
            if (r == null || sinLotes) { Quad(t, x, y, w, h, u0, v0, u1, v1, c); return; }
            if (t.Id != texActual) { Vacia(); texActual = t.Id; texActualT = t; }
            Forma(false, 0);
            float su = w / (u1 - u0), sv = h / (v1 - v0);
            float bx0 = Math.Max(0, rx0), by0 = Math.Max(0, ry0), bx1 = Math.Min(Ancho, rx1), by1 = Math.Min(Alto, ry1);
            int[] tr = r.Tramos;
            for (int i = 0; i < tr.Length; i += 3)
            {
                float uA = tr[i + 1] * r.Lado / (float)r.TexW, uB = Math.Min(1f, tr[i + 2] * r.Lado / (float)r.TexW);
                float vA = tr[i] * r.Lado / (float)r.TexH, vB = Math.Min(1f, (tr[i] + 1) * r.Lado / (float)r.TexH);
                float xa = x + (uA - u0) * su, xb = x + (uB - u0) * su, ya = y + (vA - v0) * sv, yb = y + (vB - v0) * sv;
                if (Math.Max(xa, xb) <= bx0 || Math.Min(xa, xb) >= bx1 || Math.Max(ya, yb) <= by0 || Math.Min(ya, yb) >= by1) continue;   // fuera de lo que se ve
                if (nv + 6 > vertices.Length / 8) { Vacia(); Forma(false, 0); }
                V(xa, ya, uA, vA, c); V(xb, ya, uB, vA, c); V(xa, yb, uA, vB, c);
                V(xb, ya, uB, vA, c); V(xb, yb, uB, vB, c); V(xa, yb, uA, vB, c);
            }
        }

        public void Dispose() { }
    }

    // UNA LETRA HORNEADA a un tamano: los caracteres de 32 a 255 (Latin-1: acentos y la enie) en una textura.
    internal sealed unsafe class Letra
    {
        // LAS LETRAS SE DIBUJAN AL USARLAS (3-11-2026): antes se horneaban las 192 de golpe al crear cada tamano (2,5 a 15 ms aqui, varias
        // veces eso en el A4; la transicion al reposo pide cinco tamanos nuevos por cuadro). Ahora al crear el tamano solo se miden (avance,
        // caja y desplazamiento, sin dibujar) y cada letra se dibuja con stb_truetype la primera vez que se escribe, en el mismo atlas y con
        // la misma funcion y la misma escala que el horneado de antes: los mismos puntos, solo cambia donde caen dentro del atlas (en
        // estantes, con un punto de separacion; el cuadrito blanco siempre libre). Si un atlas se llenara, se hornea entero como antes.
        private readonly StbTrueType.stbtt_bakedchar[] car = new StbTrueType.stbtt_bakedchar[224];
        private readonly Textura tex;
        private readonly int lado;
        public readonly float Tam;
        // NEGRITA SINTETICA (como hace GDI+ con las letras que no traen negrita, p. ej. Microsoft Sans Serif): cada letra se pinta otra
        // vez corrida un pixel (dos en las grandes) y avanza eso de mas.
        private readonly int engorda;
        private readonly float subeBase = 0.8f;   // de lo alto del renglon a la linea base, en tamanos
        private readonly Dibujo d; private readonly byte[] ttf; private readonly float alto;
        private readonly StbTrueType.stbtt_fontinfo fuente; private readonly float escala;
        private readonly int[] glifo = new int[224]; private readonly bool[] puesta = new bool[224];   // puesta: ya dibujada en el atlas
        private int px = 1, py = 1, pyAbajo = 1; private bool entera;   // el estante donde va la siguiente; entera: horneada de golpe
        internal static bool Horneadas = Environment.GetEnvironmentVariable("ROCOLA_LETRAS_HORNEADAS") != null;   // la de antes, para comparar
        // 'tamEm': el tamano es el del cuadro «em», como lo cuenta GDI+ (Font en pixeles de XP); si no, el alto de arriba a abajo de la
        // letra (lo que cuenta stb_truetype), que sale un 10-15 % mas chica.
        internal Letra(Dibujo d, byte[] ttf, float tam, bool negritaSintetica = false, bool tamEm = false)
        {
            Tam = tam; this.d = d; this.ttf = ttf;
            float alto = tam;
            int em, sube, baja;
            if (tamEm && MetricaEm(ttf, out em, out sube, out baja)) { alto = tam * (sube - baja) / (float)em; subeBase = sube / (float)em; }
            this.alto = alto;
            engorda = negritaSintetica ? Math.Max(1, (int)Math.Round(tam / 16f)) : 0;
            lado = alto > 40 ? 1024 : 512;
            fuente = Horneadas ? null : Fuente(ttf);
            if (fuente == null) { tex = Hornea(); return; }
            // solo medir: lo mismo que apunta el horneado de stb (stbtt_BakeFontBitmap) de cada letra, menos donde cae en el atlas
            escala = StbTrueType.stbtt_ScaleForPixelHeight(fuente, alto);
            for (int i = 0; i < 224; i++)
            {
                int g = StbTrueType.stbtt_FindGlyphIndex(fuente, 32 + i), avance, lsb, x0, y0, x1, y1;
                StbTrueType.stbtt_GetGlyphHMetrics(fuente, g, &avance, &lsb);
                StbTrueType.stbtt_GetGlyphBitmapBox(fuente, g, escala, escala, &x0, &y0, &x1, &y1);
                glifo[i] = g;
                car[i].x0 = 0; car[i].y0 = 0; car[i].x1 = (ushort)(x1 - x0); car[i].y1 = (ushort)(y1 - y0);   // por ahora solo su tamano
                car[i].xadvance = escala * avance; car[i].xoff = x0; car[i].yoff = y0;
            }
            EspacioDe();
            tex = d.AtlasBlanco(lado);   // vacio (todo transparente) con el cuadrito blanco de 4x4 en la esquina de abajo a la derecha
            tex.TieneBlanco = true; tex.BlancoU = tex.BlancoV = (lado - 2f) / lado;
        }
        // el espacio entre palabras, al menos EspacioMinimo del tamano (L1 trae un espacio estrecho: a tamanos chicos las palabras se juntan)
        private void EspacioDe() { if (EspacioMinimo > 0 && car[0].xadvance < EspacioMinimo * Tam) car[0].xadvance = (float)Math.Round(EspacioMinimo * Tam); }
        // El horneado de antes: las 192 letras de golpe (con ROCOLA_LETRAS_HORNEADAS, o si un atlas se llena).
        private Textura Hornea()
        {
            entera = true;
            byte[] alfa = new byte[lado * lado];
            StbTrueType.stbtt_BakeFontBitmap(ttf, 0, alto, alfa, lado, lado, 32, 224, car);
            EspacioDe();
            bool blanco = true;   // si ninguna letra llega a la esquina de abajo a la derecha, ahi va el cuadrito blanco (4x4)
            foreach (StbTrueType.stbtt_bakedchar b in car) if (b.x1 > lado - 6 && b.y1 > lado - 6) { blanco = false; break; }
            if (blanco) for (int y = lado - 4; y < lado; y++) for (int x = lado - 4; x < lado; x++) alfa[y * lado + x] = 255;
            byte[] la = new byte[lado * lado * 2];
            for (int i = 0; i < alfa.Length; i++) { la[2 * i] = 255; la[2 * i + 1] = alfa[i]; }
            Textura t = d.SubeLuminancia(la, lado, lado);
            if (blanco) { t.TieneBlanco = true; t.BlancoU = t.BlancoV = (lado - 2f) / lado; }
            return t;
        }
        // La letra i, dibujada en el atlas (la primera vez que se escribe). Falso si ya no cabe.
        private bool Pon(int i)
        {
            int gw = car[i].x1, gh = car[i].y1;
            if (px + gw + 1 >= lado) { py = pyAbajo; px = 1; }
            // como stb (y + gh + 1 < lado), y sin pisar la esquina del cuadrito blanco (los ultimos 6 puntos de cada lado)
            if (py + gh + 1 >= lado || (px + gw > lado - 6 && py + gh > lado - 6)) return false;
            if (gw > 0 && gh > 0)
            {
                byte[] a = Trozo(gw * gh * 3);
                fixed (byte* p = a)
                {
                    byte* alfa = p + gw * gh * 2;
                    for (int k = 0; k < gw * gh; k++) alfa[k] = 0;
                    StbTrueType.stbtt_MakeGlyphBitmap(fuente, alfa, gw, gh, gw, escala, escala, glifo[i]);
                    for (int k = 0; k < gw * gh; k++) { p[2 * k] = 255; p[2 * k + 1] = alfa[k]; }
                    d.SubeTrozo(tex, px, py, gw, gh, p);
                }
            }
            car[i].x0 = (ushort)px; car[i].y0 = (ushort)py; car[i].x1 = (ushort)(px + gw); car[i].y1 = (ushort)(py + gh);
            puesta[i] = true;
            px += gw + 1;
            if (py + gh + 1 > pyAbajo) pyAbajo = py + gh + 1;
            return true;
        }
        private static byte[] trozo;
        private static byte[] Trozo(int n) { if (trozo == null || trozo.Length < n) trozo = new byte[Math.Max(n, 64 * 1024)]; return trozo; }
        // La fuente de un archivo, una vez: copiada fuera del recolector (stb_truetype guarda el puntero a sus datos).
        private static readonly Dictionary<byte[], StbTrueType.stbtt_fontinfo> fuentes = new Dictionary<byte[], StbTrueType.stbtt_fontinfo>(ReferenceEqualityComparer.Instance);
        private static StbTrueType.stbtt_fontinfo Fuente(byte[] ttf)
        {
            StbTrueType.stbtt_fontinfo f;
            if (fuentes.TryGetValue(ttf, out f)) return f;
            byte* p = (byte*)System.Runtime.InteropServices.Marshal.AllocHGlobal(ttf.Length);
            System.Runtime.InteropServices.Marshal.Copy(ttf, 0, (IntPtr)p, ttf.Length);
            f = new StbTrueType.stbtt_fontinfo();
            if (StbTrueType.stbtt_InitFont(f, p, 0) == 0) { System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)p); f = null; }
            return fuentes[ttf] = f;
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
        // LAS LETRAS DE FUERA (7-11-2026): las letras de la rocola van de la 32 a la 255 (Latin-1); lo demas salia como «?». Ahora las
        // comillas y apostrofes tipograficos (’ “ ”), los guiones (– — −), la vineta y las letras del latin extendido (ł, ő, ş, č...: muchos
        // titulos de Spotify y de MP3) se dibujan con su igual mas cercano. Lo que no tiene igual sigue como «?».
        private const string LatinExtendido = "AaAaAaCcCcCcCcDdDdEeEeEeEeEeGgGgGgGgHhHhIiIiIiIiIiIiJjKkkLlLlLlLlLlNnNnNnnNnOoOoOoOoRrRrRrSsSsSsSsTtTtTtUuUuUuUuUuUuWwYyYZzZzZzs";
        private static int Indice(char c)
        {
            int k = c;
            if (k > 255)
            {
                if (k >= 0x100 && k < 0x180) k = LatinExtendido[k - 0x100];
                else switch (k)
                {
                    case 0x2026: k = '.'; break;
                    case 0x2018: case 0x2019: case 0x201A: case 0x2032: case 0x02BC: k = '\''; break;
                    case 0x201C: case 0x201D: case 0x201E: case 0x2033: k = '"'; break;
                    case 0x2010: case 0x2011: case 0x2012: case 0x2013: case 0x2014: case 0x2015: case 0x2212: k = '-'; break;
                    case 0x2022: case 0x2219: case 0x30FB: k = 0xB7; break;
                    case 0x2009: case 0x200A: case 0x202F: k = ' '; break;
                    case 0x20AC: k = 'E'; break;
                    default: k = '?'; break;
                }
            }
            if (k < 32) k = '?';
            return k - 32;
        }
        internal float Mide(string s)
        {
            float w = 0; if (s == null) return 0;
            foreach (char c in s) w += car[Indice(c)].xadvance + engorda;
            return w;
        }
        internal static float EspacioMinimo;   // 0: el de la letra
        internal int Usada;   // el ultimo cuadro en que se pidio (VistaGrafica.PodaLetras)
        internal void Libera(Dibujo d) { d.Borra(tex); }   // su textura (al cambiar de letra)
        // El atlas se lleno (no pasa con los tamanos de la rocola: el horneado entero cabe): se hornea entero en la misma textura.
        private void Rehace()
        {
            Textura t = Hornea();
            d.Borra(tex); tex.Id = t.Id; tex.TieneBlanco = t.TieneBlanco; tex.BlancoU = t.BlancoU; tex.BlancoV = t.BlancoV;
        }
        // Escribe con la linea base en y + 0,8 * tamano (y es el borde de arriba). Devuelve el ancho.
        internal float Escribe(Dibujo d, string s, float x, float y, Color4 c)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            float x0 = x, base_ = y + Tam * subeBase;
            foreach (char ch in s)
            {
                int ic = Indice(ch);
                if (!entera && !puesta[ic] && !Pon(ic)) Rehace();
                StbTrueType.stbtt_bakedchar b = car[ic];
                float w = b.x1 - b.x0, h = b.y1 - b.y0;
                if (w > 0 && h > 0)
                    for (int e = 0; e <= engorda; e++)
                        d.Quad(tex, (float)Math.Round(x + b.xoff) + e, (float)Math.Round(base_ + b.yoff), w, h, b.x0 / (float)lado, b.y0 / (float)lado, b.x1 / (float)lado, b.y1 / (float)lado, c);
                x += b.xadvance + engorda;
            }
            return x - x0;
        }
        // Cabe en 'ancho': si no, se corta con puntos suspensivos.
        // Lo cortado se recuerda (los mismos textos se cortan en cada cuadro: sin basura que recoger); se olvida todo al pasar de 1024.
        private readonly Dictionary<(string, float), string> ajustadas = new Dictionary<(string, float), string>();
        internal string Ajusta(string s, float ancho)
        {
            if (s == null) return "";
            if (Mide(s) <= ancho) return s;
            string r;
            if (ajustadas.TryGetValue((s, ancho), out r)) return r;
            if (ajustadas.Count >= 1024) ajustadas.Clear();
            return ajustadas[(s, ancho)] = Corta(s, ancho);
        }
        private string Corta(string s, float ancho)
        {
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
        internal Textura De(string ruta) { return Carga(ruta, Clase.Normal, false); }
        // Para dibujar a 'tam' pixeles: si pasa del lado de siempre (con las nitidas), la copia grande (las ultimas 6).
        internal Textura Para(string ruta, float tam)
        {
            if (Nitidas && tam > lado * 1.1f) { Textura g = Carga(ruta, Clase.Grande, false); if (g != null) return g; }
            return De(ruta);
        }
        // V7, V13: LA PORTADA BORROSA (vidrio esmerilado): achicada a 24x24 por promedio y suavizada una vez mas; estirada a toda la
        // pantalla, el filtro lineal de la tarjeta hace el resto. Se hace una vez por portada (las ultimas 8): no cuesta en cada cuadro.
        private readonly Dictionary<string, Textura> borrosas = new Dictionary<string, Textura>(StringComparer.Ordinal);
        private readonly LinkedList<string> ordenBorrosas = new LinkedList<string>();
        internal Textura Borrosa(string ruta) { return Carga(ruta, Clase.Borrosa, false); }
        // Que se vaya leyendo (la pagina de al lado): sin prisa, detras de lo que se esta viendo.
        internal void Precarga(string ruta) { Carga(ruta, Clase.Normal, true); }

        // LEER UNA PORTADA SIN PARAR LA PANTALLA (2-11-2026): leer el archivo y descomprimirlo (lo que tarda: decenas de milisegundos en
        // el A4) va en un hilo de fondo de prioridad baja, de una en una; la pantalla solo la sube a la tarjeta (unas pocas por cuadro).
        // Mientras llega, se ve lo de siempre sin portada (el cuadro de color con la inicial), como al arrancar. En las capturas de
        // prueba (AlMomento) se lee en el sitio, como antes.
        internal static bool AlMomento;
        private enum Clase { Normal, Grande, Borrosa }
        private sealed class Listo { public string Ruta; public Clase Que; public byte[] Px; public int W, H, S; public bool Mip, Falla, Opaca; public Color4 Media; public Rocola.VistaGrafica.C3 Prom, Uno, Dos; }
        private readonly LinkedList<KeyValuePair<string, Clase>> pedidas = new LinkedList<KeyValuePair<string, Clase>>();
        private readonly HashSet<string> enCamino = new HashSet<string>(StringComparer.Ordinal);
        private readonly System.Collections.Concurrent.ConcurrentQueue<Listo> listas = new System.Collections.Concurrent.ConcurrentQueue<Listo>();
        private readonly System.Threading.SemaphoreSlim hay = new System.Threading.SemaphoreSlim(0);
        private System.Threading.Thread lector;
        private bool nitidasPedidas;
        private Textura Carga(string ruta, Clase que, bool precarga)
        {
            if (string.IsNullOrEmpty(ruta) || sinImagen.Contains(ruta)) return null;
            Dictionary<string, Textura> cache = que == Clase.Normal ? hechas : que == Clase.Grande ? grandes : borrosas;
            LinkedList<string> ord = que == Clase.Normal ? orden : que == Clase.Grande ? ordenGrandes : ordenBorrosas;
            Textura t;
            if (cache.TryGetValue(ruta, out t)) { if (!precarga) { ord.Remove(ruta); ord.AddFirst(ruta); } return t; }
            if (AlMomento) { Pon(Decodifica(ruta, que, Nitidas)); cache.TryGetValue(ruta, out t); return t; }
            string clave = (int)que + ruta;
            lock (pedidas)
            {
                if (enCamino.Contains(clave)) return null;
                if (precarga && pedidas.Count >= 48) return null;   // la cola de lo que se adelanta, acotada
                enCamino.Add(clave);
                KeyValuePair<string, Clase> p = new KeyValuePair<string, Clase>(ruta, que);
                if (precarga) pedidas.AddLast(p); else pedidas.AddFirst(p);   // lo que se ve, antes que lo que se adelanta
                nitidasPedidas = Nitidas;
            }
            if (lector == null)
            {
                lector = new System.Threading.Thread(Lee) { IsBackground = true, Name = "portadas", Priority = System.Threading.ThreadPriority.BelowNormal };
                lector.Start();
            }
            hay.Release();
            return null;
        }
        private void Lee()
        {
            try { Rocola.Prioridad.Fondo(); } catch { }
            while (true)
            {
                hay.Wait();
                KeyValuePair<string, Clase> p; bool nit;
                lock (pedidas) { if (pedidas.Count == 0) continue; p = pedidas.First.Value; pedidas.RemoveFirst(); nit = nitidasPedidas; }
                listas.Enqueue(Decodifica(p.Key, p.Value, nit));
            }
        }
        // Lo que ya esta leido, a la tarjeta (llamar una vez por cuadro, desde el hilo de la pantalla): como mucho 'cuantas'.
        internal void Recoge(int cuantas)
        {
            Listo l;
            for (int i = 0; i < cuantas && listas.TryDequeue(out l); i++)
            {
                lock (pedidas) enCamino.Remove((int)l.Que + l.Ruta);
                Pon(l);
            }
        }
        internal const long PortadaMaxPx = 64L * 1000 * 1000;
        // Leer y descomprimir (cualquier hilo: no toca la tarjeta).
        private Listo Decodifica(string ruta, Clase que, bool nitidas)
        {
            Listo l = new Listo { Ruta = ruta, Que = que };
            try
            {
                // LAS PORTADAS GIGANTES (4-11-2026): una foto de camara de 100 Mpx ocupa 400 MB descomprimida; en el A4 (3,5 GB) puede
                // acabar con la memoria y Linux mata a la rocola entera. Primero solo la cabecera: mas de PortadaMaxPx (o un archivo de
                // mas de 64 MB), sin portada, como una que no se puede leer.
                byte[] bytes;
                using (FileStream fs = File.OpenRead(ruta))
                {
                    if (fs.Length > 64L * 1024 * 1024) throw new InvalidDataException("portada demasiado grande (archivo)");
                    ImageInfo? info = ImageInfo.FromStream(fs);
                    if (info == null || (long)info.Value.Width * info.Value.Height > PortadaMaxPx) throw new InvalidDataException("portada demasiado grande o ilegible");
                    fs.Position = 0; bytes = new byte[fs.Length]; fs.ReadExactly(bytes);
                }
                ImageResult im = ImageResult.FromMemory(bytes, ColorComponents.RedGreenBlueAlpha);
                if (que == Clase.Borrosa)
                {
                    const int L = 24;
                    byte[] px = Reduce(im.Data, im.Width, im.Height, L, L), r = new byte[px.Length];
                    for (int y = 0; y < L; y++) for (int x = 0; x < L; x++) for (int c = 0; c < 4; c++)
                    {
                        int sm = 0, n = 0;
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { int yy = y + dy, xx = x + dx; if (yy < 0 || xx < 0 || yy >= L || xx >= L) continue; sm += px[(yy * L + xx) * 4 + c]; n++; }
                        r[(y * L + x) * 4 + c] = (byte)(sm / n);
                    }
                    l.Px = r; l.W = l.H = L; return l;
                }
                int max = que == Clase.Grande ? lado * 2 : lado;
                int w = im.Width, h = im.Height; byte[] pxs = im.Data;
                if (Math.Max(w, h) > max) { int nw = Math.Max(1, w * max / Math.Max(w, h)), nh = Math.Max(1, h * max / Math.Max(w, h)); pxs = Reduce(pxs, w, h, nw, nh); w = nw; h = nh; }
                l.W = w; l.H = h;
                l.Media = ColorMedio(pxs);
                Rocola.VistaGrafica.ColoresDePortada(pxs, w, h, out l.Prom, out l.Uno, out l.Dos);
                if (nitidas)
                {
                    int sz = 1; while (sz * 2 <= Math.Max(64, max)) sz *= 2;   // el lado potencia de 2 que cabe
                    l.Px = Escala(pxs, w, h, sz, sz); l.S = sz; l.Mip = true;
                }
                else l.Px = pxs;
                bool op = true; byte[] q = l.Px;
                for (int i = 3; i < q.Length; i += 4) if (q[i] != 255) { op = false; break; }
                l.Opaca = op;   // sin un punto transparente: se puede dibujar sin mezclar
            }
            catch { l.Falla = true; }
            return l;
        }
        // Subir lo leido a la tarjeta (hilo de la pantalla) y guardarlo en su cache (las ultimas 96, 6 grandes, 8 borrosas).
        private void Pon(Listo l)
        {
            if (l.Falla) { sinImagen.Add(l.Ruta); return; }
            Dictionary<string, Textura> cache = l.Que == Clase.Normal ? hechas : l.Que == Clase.Grande ? grandes : borrosas;
            LinkedList<string> ord = l.Que == Clase.Normal ? orden : l.Que == Clase.Grande ? ordenGrandes : ordenBorrosas;
            int cuantas = l.Que == Clase.Normal ? 96 : l.Que == Clase.Grande ? 6 : 8;
            if (cache.ContainsKey(l.Ruta)) return;
            Textura t;
            if (l.Mip) { t = d.SubeConMipmaps(l.Px, l.S); t.Ancho = l.W; t.Alto = l.H; }   // la forma de la portada (la textura va estirada al cuadrado)
            else t = d.Sube(l.Px, l.W, l.H);
            t.Media = l.Media; t.Prom = l.Prom; t.Uno = l.Uno; t.Dos = l.Dos; t.Opaca = l.Opaca;
            cache[l.Ruta] = t; ord.AddFirst(l.Ruta);
            while (ord.Count > cuantas) { string v = ord.Last.Value; ord.RemoveLast(); d.Borra(cache[v]); cache.Remove(v); }
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
