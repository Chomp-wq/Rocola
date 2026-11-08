// LOS TRES PROGRAMAS DE LA TARJETA (port nativo de XP, 8-11-2026).
//
// En XP las etapas de textura de Direct3D 9 (SetTextureStageState) se combinan de tres maneras y solo de tres: «color» (el color del
// vertice: D3D9Star.cs TssDiffuse), «textura por color» (TssModulate) y «dos texturas» (el grano y la vineta en una pasada:
// PerformanceGpu.cs DibujaDosTexturas). Aqui son tres programas fijos de OpenGL ES 2 con el texto EXACTO que la traduccion con capas
// (Plataforma/Direct3DaGL.cs, FuenteEtapas) generaba para esas tres combinaciones: el mismo texto da el mismo codigo en el mismo
// controlador (uno «equivalente» puede compilarse distinto -una FMA- y dar un punto de diferencia). La prueba de los textos esta en
// Pruebas/Tarjeta (--fuentes).
//
// Los atributos: p=0 (XYZRHW, en pixeles, con el centro del pixel en el entero como en Direct3D 9: +0,5), c=1 (el color D3DCOLOR, 4 bytes
// normalizados: B, G, R, A en memoria; asi se queda de punta a punta, en las texturas y en el fondo, y el compositor lo lee en su orden),
// t0=2, t1=3, e=4 (el especular, que XP no usa). El uniforme m = (2/ancho, 2/alto) del destino. Sin dar la vuelta: la fila 0 de Direct3D
// es la fila 0 del framebuffer de textura donde se pinta (asi Mesa aplica la misma regla de arriba a la izquierda que Direct3D 9).
//
// Dibujar: Triangulos (el vertice de XP de 28 bytes, de la memoria del programa: DrawPrimitiveUP), TriangulosDeBufer (del bufer de
// vertices: DrawPrimitive) y Triangulos2 (el de 36 bytes de la pasada de dos texturas). Cada uno pone los punteros y llama a glDrawArrays.
// Por cada dispositivo se compila un juego (sus uniformes son de su destino) y se guarda lo que XP le pidio a Direct3D en las etapas 0 y 1,
// solo para escribirlo igual que antes en la traza de los dibujos (ROCOLA_TRAZA_DIBUJOS; ver TrazaTarjeta).
#if ROCOLA_GPU
using System;
using System.Runtime.InteropServices;
using System.Text;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed unsafe class SombreadoresTarjeta
    {
        private const string Cabecera =
            "#ifdef GL_FRAGMENT_PRECISION_HIGH\nprecision highp float;\n#else\nprecision mediump float;\n#endif\n" +
            "varying vec4 vc; varying vec4 ve; varying vec2 vt0; varying vec2 vt1;\nuniform sampler2D s0; uniform sampler2D s1; uniform vec4 tf; uniform float ar;\n" +
            "void main() {\n  vec4 cur = vc; vec4 tmp = vec4(0.0);\n";
        internal const string FuenteVertices =
            "attribute vec4 p; attribute vec4 c; attribute vec4 e; attribute vec2 t0; attribute vec2 t1;\n" +
            "uniform vec2 m;\n" +
            "varying vec4 vc; varying vec4 ve; varying vec2 vt0; varying vec2 vt1;\n" +
            "void main() {\n" +
            "  float w = 1.0 / p.w;\n" +
            "  gl_Position = vec4(((p.xy + 0.5) * m - 1.0) * w, 0.0, w);\n" +
            "  vc = c; ve = e; vt0 = t0; vt1 = t1;\n" +
            "}\n";
        // TssDiffuse: SELECTARG1(DIFFUSE) en el color y en el alfa
        internal const string FuenteColor = Cabecera +
            "  vec4 r0 = clamp(vec4((vc).rgb, (vc).a), 0.0, 1.0);\n  cur = r0;\n  gl_FragColor = cur;\n}\n";
        // TssModulate: MODULATE(TEXTURE, DIFFUSE) en el color y en el alfa
        internal const string FuenteTexturaPorColor = Cabecera +
            "  vec4 tx0 = texture2D(s0, vt0);\n  vec4 r0 = clamp(vec4((tx0).rgb * (vc).rgb, (tx0).a * (vc).a), 0.0, 1.0);\n  cur = r0;\n  gl_FragColor = cur;\n}\n";
        // DibujaDosTexturas: etapa 0 MODULATE(TEXTURE, TEXTURE|ALPHAREPLICATE) y SELECTARG1(TEXTURE); etapa 1 MODULATE(TEXTURE|
        // ALPHAREPLICATE|COMPLEMENT, CURRENT) y ADDSMOOTH(TEXTURE, CURRENT), con su juego de coordenadas (TEXCOORDINDEX 1)
        internal const string FuenteDosTexturas = Cabecera +
            "  vec4 tx0 = texture2D(s0, vt0);\n  vec4 r0 = clamp(vec4((tx0).rgb * (vec4((tx0).a)).rgb, (tx0).a), 0.0, 1.0);\n  cur = r0;\n" +
            "  vec4 tx1 = texture2D(s1, vt1);\n  vec4 r1 = clamp(vec4((vec4(((1.0 - tx1)).a)).rgb * (cur).rgb, (tx1).a + (cur).a - (tx1).a * (cur).a), 0.0, 1.0);\n  cur = r1;\n  gl_FragColor = cur;\n}\n";

        internal sealed class Programa { internal uint Nombre; internal int LugarM; internal int Ancho = -1, Alto = -1; }
        internal Programa Color, TexturaPorColor, DosTexturas;
        private uint vertices;
        private Programa puesto;   // el que tiene OpenGL (null: no se sabe; otro pudo cambiarlo)

        // Los tres, compilados (null si OpenGL no puede).
        internal static SombreadoresTarjeta Compila()
        {
            SombreadoresTarjeta s = new SombreadoresTarjeta();
            s.vertices = Sombreador(GL.VERTEX_SHADER, FuenteVertices);
            if (s.vertices == 0) return null;
            s.Color = s.Enlaza(FuenteColor); s.TexturaPorColor = s.Enlaza(FuenteTexturaPorColor); s.DosTexturas = s.Enlaza(FuenteDosTexturas);
            if (s.Color == null || s.TexturaPorColor == null || s.DosTexturas == null) { s.Suelta(); return null; }
            s.puesto = null;
            return s;
        }
        private Programa Enlaza(string fragmentos)
        {
            uint f = Sombreador(GL.FRAGMENT_SHADER, fragmentos);
            if (f == 0) return null;
            uint p = GL.CreateProgram();
            GL.AttachShader(p, vertices); GL.AttachShader(p, f);
            GL.BindAttribLocation(p, 0, "p"); GL.BindAttribLocation(p, 1, "c"); GL.BindAttribLocation(p, 2, "t0"); GL.BindAttribLocation(p, 3, "t1"); GL.BindAttribLocation(p, 4, "e");
            GL.LinkProgram(p);
            int ok; GL.GetProgramiv(p, GL.LINK_STATUS, &ok);
            GlMas.DeleteShader(f);
            if (ok == 0)
            {
                byte* log = stackalloc byte[1024]; int largo;
                GlMas.GetProgramInfoLog(p, 1024, &largo, log);
                Console.Error.WriteLine("Tarjeta: el programa no enlaza: " + Marshal.PtrToStringAnsi((IntPtr)log, Math.Max(0, largo)));
                GlMas.DeleteProgram(p);
                return null;
            }
            Programa r = new Programa();
            r.Nombre = p;
            GL.UseProgram(p);
            r.LugarM = GL.GetUniformLocation(p, "m");
            GL.Uniform1i(GL.GetUniformLocation(p, "s0"), 0); GL.Uniform1i(GL.GetUniformLocation(p, "s1"), 1);
            return r;
        }
        private static uint Sombreador(uint tipo, string fuente)
        {
            uint s = GL.CreateShader(tipo);
            byte[] b = Encoding.ASCII.GetBytes(fuente);
            fixed (byte* pb = b) { byte* f = pb; int largo = b.Length; GL.ShaderSource(s, 1, &f, &largo); }
            GL.CompileShader(s);
            int ok; GL.GetShaderiv(s, GL.COMPILE_STATUS, &ok);
            if (ok == 0)
            {
                byte* log = stackalloc byte[1024]; int largo;
                GL.GetShaderInfoLog(s, 1024, &largo, log);
                Console.Error.WriteLine("Tarjeta: el sombreador no compila: " + Marshal.PtrToStringAnsi((IntPtr)log, Math.Max(0, largo)) + "\n" + fuente);
                GlMas.DeleteShader(s);
                return 0;
            }
            return s;
        }
        internal void Suelta()
        {
            foreach (Programa p in new[] { Color, TexturaPorColor, DosTexturas }) if (p != null && p.Nombre != 0) { GlMas.DeleteProgram(p.Nombre); p.Nombre = 0; }
            if (vertices != 0) { GlMas.DeleteShader(vertices); vertices = 0; }
            puesto = null;
        }

        // Otro (el compositor, mpv) cambio el programa de OpenGL: el siguiente Usa lo vuelve a poner.
        internal void Olvida() { puesto = null; }
        internal void Usa(Programa p)
        {
            if (p == Color) TrazaColor(); else if (p == TexturaPorColor) TrazaTexturaPorColor(); else TrazaDosTexturas();
            if (puesto != p) { GL.UseProgram(p.Nombre); puesto = p; }
        }
        internal Programa Puesto { get { return puesto; } }
        private static void Medida(Programa p, int ancho, int alto)
        {
            if (p.Ancho != ancho || p.Alto != alto) { GL.Uniform2f(p.LugarM, 2f / ancho, 2f / alto); p.Ancho = ancho; p.Alto = alto; }
        }

        // ---- dibujar (el programa ya puesto con Usa; el destino, su medida) ----
        // Los vertices de XP (Tarjeta.Vtx, 28 bytes) de la memoria del programa, con ARRAY_BUFFER a 0. Devuelve false si la prueba pidio
        // saltarse este dibujo (ROCOLA_SALTA_DIBUJOS).
        internal bool Triangulos(IntPtr datos, int vertices, int ancho, int alto, bool enFondo)
        {
            if (!TrazaTarjeta.Dibujo(this, (byte*)datos, 28, vertices, ancho, alto, enFondo)) return false;
            Programa p = puesto;
            Medida(p, ancho, alto);
            byte* b = (byte*)datos;
            GL.VertexAttribPointer(0, 4, GL.FLOAT, 0, 28, (IntPtr)b); GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 4, GL.UNSIGNED_BYTE, 1, 28, (IntPtr)(b + 16)); GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(2, 2, GL.FLOAT, 0, 28, (IntPtr)(b + 20)); GL.EnableVertexAttribArray(2);
            GL.DrawArrays(GL.TRIANGLES, 0, vertices);
            return true;
        }
        // Los mismos, del bufer de vertices puesto en ARRAY_BUFFER, desde el vertice 'primero' (sin traza: la de antes tampoco los apuntaba).
        internal void TriangulosDeBufer(int primero, int vertices, int ancho, int alto)
        {
            Programa p = puesto;
            Medida(p, ancho, alto);
            long o = (long)primero * 28;
            GL.VertexAttribPointer(0, 4, GL.FLOAT, 0, 28, (IntPtr)o); GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 4, GL.UNSIGNED_BYTE, 1, 28, (IntPtr)(o + 16)); GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(2, 2, GL.FLOAT, 0, 28, (IntPtr)(o + 20)); GL.EnableVertexAttribArray(2);
            GL.DrawArrays(GL.TRIANGLES, 0, vertices);
        }
        // Los de la pasada de dos texturas (Tarjeta.Vtx2, 36 bytes: el segundo juego de coordenadas en el atributo 3, que se apaga al acabar).
        internal bool Triangulos2(IntPtr datos, int vertices, int ancho, int alto, bool enFondo)
        {
            if (!TrazaTarjeta.Dibujo(this, (byte*)datos, 36, vertices, ancho, alto, enFondo)) return false;
            Programa p = puesto;
            Medida(p, ancho, alto);
            byte* b = (byte*)datos;
            GL.VertexAttribPointer(0, 4, GL.FLOAT, 0, 36, (IntPtr)b); GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 4, GL.UNSIGNED_BYTE, 1, 36, (IntPtr)(b + 16)); GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(2, 2, GL.FLOAT, 0, 36, (IntPtr)(b + 20)); GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(3, 2, GL.FLOAT, 0, 36, (IntPtr)(b + 28)); GL.EnableVertexAttribArray(3);
            GL.DrawArrays(GL.TRIANGLES, 0, vertices);
            GlMas.DisableVertexAttribArray(3);
            return true;
        }

        // ---- lo que XP le habia pedido a Direct3D 9 en las etapas 0 y 1 (D3DTSS: COLOROP, COLORARG1, COLORARG2, ALPHAOP, ALPHAARG1,
        // ALPHAARG2), solo para la traza. Al crear el dispositivo y tras cada Reset, lo de fabrica de Direct3D 9; TssDiffuse no toca los
        // segundos argumentos; TssModulate deja los dos en DIFFUSE; la pasada de dos texturas deja el COLORARG2 de la etapa 0 en
        // TEXTURE|ALPHAREPLICATE y, al acabar, la etapa 1 apagada con sus argumentos.
        internal readonly int[] Etapa0 = { 4, 2, 1, 2, 2, 1 }, Etapa1 = { 1, 2, 1, 1, 2, 1 };
        internal void EtapasDeFabrica() { int[] a = { 4, 2, 1, 2, 2, 1 }, b = { 1, 2, 1, 1, 2, 1 }; a.CopyTo(Etapa0, 0); b.CopyTo(Etapa1, 0); }
        private void TrazaColor() { Etapa0[0] = 2; Etapa0[1] = 0; Etapa0[3] = 2; Etapa0[4] = 0; }
        private void TrazaTexturaPorColor() { Etapa0[0] = 4; Etapa0[1] = 2; Etapa0[2] = 0; Etapa0[3] = 4; Etapa0[4] = 2; Etapa0[5] = 0; }
        private void TrazaDosTexturas()
        {
            Etapa0[0] = 4; Etapa0[1] = 2; Etapa0[2] = 2 | 0x20; Etapa0[3] = 2; Etapa0[4] = 2;
            Etapa1[0] = 4; Etapa1[1] = 2 | 0x20 | 0x10; Etapa1[2] = 1; Etapa1[3] = 11; Etapa1[4] = 2; Etapa1[5] = 1;
        }
        internal void FinDosTexturas() { Etapa1[0] = 1; Etapa1[3] = 1; }
    }
}
#endif
