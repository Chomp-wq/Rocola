// EL VIDRIO (decision del 3-11-2026, G1-G11): lo que hay detras de un panel, desenfocado de verdad. Dos fuentes:
// - EL CIELO BORROSO (coste bajo): la capa del cielo (CieloXp) achicada a 1/8 y desenfocada (un sombreador gaussiano de 9 toques,
//   horizontal y vertical, dos veces) una vez por cuadro, solo si algun vidrio del cielo se dibujo en el cuadro anterior. Los paneles
//   que solo tienen cielo detras la leen en su sitio.
// - LO DE DETRAS DE VERDAD (coste medio): se copia el trozo de pantalla del panel (con un margen), se achica y se desenfoca igual; para
//   los paneles que tienen portadas detras (la ficha, los ajustes, los avisos, el globo de la ayuda).
// Las texturas de las capas tienen la fila de arriba en v = 1 (como la capa del cielo y la copia de la pantalla).
using System;

namespace Rocola.Pantalla
{
    internal sealed unsafe partial class Dibujo
    {
        // Donde leer lo de detras: u = Au * x + Bu, v = Av * y + Bv (x, y de la pantalla). Sin textura: no hay.
        internal struct Fondo { public Textura T; public float Au, Bu, Av, Bv; public bool Hay { get { return T != null; } } }
        internal Fondo CieloBorroso;
        private uint programaBorroso, programaVidrio; private int uBorEsc, uBorTex, uBorPaso, uVidEsc, uVidTex;
        private Capa borA, borB, borCielo; private int borW, borH; private bool borFallo;

        private bool PreparaBorroso()
        {
            if (borFallo) return false;
            if (borA != null) return true;
            try
            {
                programaBorroso = Programa(
                    "attribute vec2 p; attribute vec2 t; attribute vec4 c; uniform vec2 esc; varying vec2 vt; varying vec4 vc;" +
                    "void main(){ vt = t; vc = c; gl_Position = vec4(p.x * esc.x - 1.0, 1.0 - p.y * esc.y, 0.0, 1.0); }",
                    "precision mediump float; uniform sampler2D tex; uniform vec2 paso; varying vec2 vt; varying vec4 vc;" +
                    "void main(){ vec3 s = texture2D(tex, vt).rgb * 0.2270270;" +
                    " s += (texture2D(tex, vt + paso * 1.3846154).rgb + texture2D(tex, vt - paso * 1.3846154).rgb) * 0.3162162;" +
                    " s += (texture2D(tex, vt + paso * 3.2307692).rgb + texture2D(tex, vt - paso * 3.2307692).rgb) * 0.0702703;" +
                    " gl_FragColor = vec4(s, 1.0); }");
                uBorEsc = GL.GetUniformLocation(programaBorroso, "esc"); uBorTex = GL.GetUniformLocation(programaBorroso, "tex"); uBorPaso = GL.GetUniformLocation(programaBorroso, "paso");
                // EL VIDRIO EN UNA PASADA: lo de detras desenfocado y encima el velo con su tinte (el color del vertice, su alfa = cuanto
                // tapa), mezclados en el sombreador: sale opaco, sin mezclar con la pantalla (antes, dos pasadas, la segunda mezclada)
                programaVidrio = Programa(
                    "attribute vec2 p; attribute vec2 t; attribute vec4 c; uniform vec2 esc; varying vec2 vt; varying vec4 vc;" +
                    "void main(){ vt = t; vc = c; gl_Position = vec4(p.x * esc.x - 1.0, 1.0 - p.y * esc.y, 0.0, 1.0); }",
                    "precision mediump float; uniform sampler2D tex; varying vec2 vt; varying vec4 vc;" +
                    "void main(){ gl_FragColor = vec4(texture2D(tex, vt).rgb * (1.0 - vc.a) + vc.rgb * vc.a, 1.0); }");
                uVidEsc = GL.GetUniformLocation(programaVidrio, "esc"); uVidTex = GL.GetUniformLocation(programaVidrio, "tex");
            }
            catch { borFallo = true; return false; }
            borW = Math.Max(4, (Ancho + 7) / 8); borH = Math.Max(4, (Alto + 7) / 8);
            borA = CapaNueva(borW, borH); borB = CapaNueva(borW, borH); borCielo = CapaNueva(borW, borH);
            if (borA == null || borB == null || borCielo == null) { borFallo = true; return false; }
            borA.Tex.Opaca = true; borCielo.Tex.Opaca = true;   // el desenfoque sale con alfa 1: se dibuja sin mezclar
            return true;
        }

        // Una pasada a una capa chica: 'fuente' (su trozo u0..u1, v0..v1) a toda la ventana vw x vh de 'destino'; con el sombreador del
        // desenfoque si paso no es cero, si no con el de siempre (achicar). Deja todo como estaba (la pantalla, el programa, la mezcla).
        private void Pasada(Capa destino, int vw, int vh, Textura fuente, float u0, float v0, float u1, float v1, float pu, float pv)
        {
            Vacia();
            int antes; GL.GetIntegerv(GL.FRAMEBUFFER_BINDING, &antes);
            bool desenfoca = pu != 0 || pv != 0;
            GL.BindFramebuffer(GL.FRAMEBUFFER, destino.Fbo); GL.Viewport(0, 0, vw, vh);
            GL.Disable(GL.SCISSOR_TEST); GL.Disable(GL.BLEND);
            if (desenfoca) { GL.UseProgram(programaBorroso); GL.Uniform2f(uBorEsc, 2f / vw, 2f / vh); GL.Uniform1i(uBorTex, 0); GL.Uniform2f(uBorPaso, pu, pv); }
            else GL.Uniform2f(uEscala, 2f / vw, 2f / vh);
            bool enCapaAntes = enCapa; enCapa = true;
            texActual = fuente.Id; texActualT = fuente; opacoV = false;
            Color4 b = new Color4(1, 1, 1, 1);
            V(0, 0, u0, v0, b); V(vw, 0, u1, v0, b); V(0, vh, u0, v1, b);
            V(vw, 0, u1, v0, b); V(vw, vh, u1, v1, b); V(0, vh, u0, v1, b);
            float ax = rx0, ay = ry0, bx = rx1, by = ry1; SinRect();
            Vacia();
            rx0 = ax; ry0 = ay; rx1 = bx; ry1 = by;
            enCapa = enCapaAntes;
            GL.BindFramebuffer(GL.FRAMEBUFFER, (uint)antes); GL.Viewport(0, 0, Ancho, Alto);
            GL.UseProgram(programa); GL.Uniform2f(uEscala, 2f / Ancho, 2f / Alto);
            GL.Enable(GL.BLEND); mezclando = true;
            if (rx1 < 1e8f) GL.Enable(GL.SCISSOR_TEST);   // el recorte que hubiera puesto sigue (su caja no se toco)
            texActual = 0; texActualT = null;
        }
        // Desenfoca el trozo sw x sh de borA (sus coordenadas: u de 0 a su, v de 0 a sv, arriba en sv) dos veces; queda en 'final'.
        private void DosDesenfoques(int sw, int sh, Capa final)
        {
            float su = sw / (float)borW, sv = sh / (float)borH, pu = 1f / borW, pv = 1f / borH;
            Pasada(borB, sw, sh, borA.Tex, 0, sv, su, 0, pu, 0);
            Pasada(borA, sw, sh, borB.Tex, 0, sv, su, 0, 0, pv);
            Pasada(borB, sw, sh, borA.Tex, 0, sv, su, 0, pu, 0);
            Pasada(final, sw, sh, borB.Tex, 0, sv, su, 0, 0, pv);
        }
        // El cielo de este cuadro (la capa entera), desenfocado: CieloBorroso.
        internal void DesenfocaCielo(Textura capa)
        {
            if (capa == null || !PreparaBorroso()) { CieloBorroso = default(Fondo); return; }
            Pasada(borA, borW, borH, capa, 0, 1, 1, 0, 0, 0);
            DosDesenfoques(borW, borH, borCielo);
            CieloBorroso = new Fondo { T = borCielo.Tex, Au = 1f / Ancho, Bu = 0, Av = -1f / Alto, Bv = 1 };
        }
        // Lo que ya esta dibujado detras de x, y, w, h (con un margen de 32 puntos para que el borde no lea de fuera), desenfocado.
        internal Fondo DesenfocaDetras(float x, float y, float w, float h)
        {
            if (!PreparaBorroso()) return default(Fondo);
            float m = 32;
            float cx = x - m, cy = y - m, cw = w + 2 * m, ch = h + 2 * m;
            int ix = Math.Max(0, (int)cx), iy = Math.Max(0, (int)cy), iw = Math.Min(Ancho - ix, (int)Math.Ceiling(cw)), ih = Math.Min(Alto - iy, (int)Math.Ceiling(ch));
            Textura t = CopiaPantalla(cx, cy, cw, ch);
            if (t == null || iw < 2 || ih < 2) return default(Fondo);
            int sw = Math.Min(borW, Math.Max(2, (iw + 7) / 8)), sh = Math.Min(borH, Math.Max(2, (ih + 7) / 8));
            Pasada(borA, sw, sh, t, 0, 1, 1, 0, 0, 0);
            DosDesenfoques(sw, sh, borA);
            float su = sw / (float)borW, sv = sh / (float)borH;
            return new Fondo { T = borA.Tex, Au = su / iw, Bu = -ix * su / iw, Av = -sv / ih, Bv = sv + iy * sv / ih };
        }

        // Un rectangulo de esquinas redondas con lo de detras (Fondo), como Redondo pero leyendo la textura en su sitio.
        internal void RedondoFondo(float x, float y, float w, float h, float r, Fondo f, Color4 c)
        {
            if (!f.Hay || c.A <= 0f) return;
            r = Math.Min(r, Math.Min(w, h) / 2f);
            TiraFondo(f, x, y + r, w, h - 2 * r, c);
            int pasos = Math.Max(2, (int)r);
            for (int i = 0; i < pasos; i++)
            {
                float yy = r - (i + 0.5f) * r / pasos;
                float dx = r - (float)Math.Sqrt(Math.Max(0, r * r - yy * yy));
                float h1 = r / pasos;
                TiraFondo(f, x + dx, y + i * h1, w - 2 * dx, h1, c);
                TiraFondo(f, x + dx, y + h - (i + 1) * h1, w - 2 * dx, h1, c);
            }
        }
        // El vidrio entero en una pasada: lo de detras (f) con el velo de 'arriba' a 'abajo' encima (su alfa: cuanto tapa). Opaco.
        internal void RedondoVidrio(float x, float y, float w, float h, float r, Fondo f, Color4 arriba, Color4 abajo)
        {
            if (!f.Hay || programaVidrio == 0) return;
            r = Math.Min(r, Math.Min(w, h) / 2f);
            Mezcla(false);
            GL.UseProgram(programaVidrio); GL.Uniform2f(uVidEsc, 2f / Ancho, 2f / Alto); GL.Uniform1i(uVidTex, 0);
            texActual = f.T.Id; texActualT = f.T; opacoV = false;
            TiraVidrio(f, x, y + r, w, h - 2 * r, arriba, abajo, y, h);
            int pasos = Math.Max(2, (int)r);
            for (int i = 0; i < pasos; i++)
            {
                float yy = r - (i + 0.5f) * r / pasos;
                float dx = r - (float)Math.Sqrt(Math.Max(0, r * r - yy * yy));
                float h1 = r / pasos;
                TiraVidrio(f, x + dx, y + i * h1, w - 2 * dx, h1, arriba, abajo, y, h);
                TiraVidrio(f, x + dx, y + h - (i + 1) * h1, w - 2 * dx, h1, arriba, abajo, y, h);
            }
            Vacia();
            GL.UseProgram(programa); texActual = 0; texActualT = null;
            Mezcla(true);
        }
        private void TiraVidrio(Fondo f, float x, float y, float w, float h, Color4 arriba, Color4 abajo, float y0, float h0)
        {
            if (w <= 0 || h <= 0) return;
            if (nv + 6 > vertices.Length / 8) Vacia();
            Color4 ca = Mezcla(arriba, abajo, (y - y0) / h0), cb = Mezcla(arriba, abajo, (y + h - y0) / h0);
            float u0 = f.Au * x + f.Bu, u1 = f.Au * (x + w) + f.Bu, v0 = f.Av * y + f.Bv, v1 = f.Av * (y + h) + f.Bv;
            V(x, y, u0, v0, ca); V(x + w, y, u1, v0, ca); V(x, y + h, u0, v1, cb);
            V(x + w, y, u1, v0, ca); V(x + w, y + h, u1, v1, cb); V(x, y + h, u0, v1, cb);
        }
        private void TiraFondo(Fondo f, float x, float y, float w, float h, Color4 c)
        {
            if (w <= 0 || h <= 0) return;
            if (f.T.Id != texActual || nv + 6 > vertices.Length / 8) { Vacia(); texActual = f.T.Id; texActualT = f.T; }
            Forma(f.T.Opaca && c.A >= 1f, w * h);
            float u0 = f.Au * x + f.Bu, u1 = f.Au * (x + w) + f.Bu, v0 = f.Av * y + f.Bv, v1 = f.Av * (y + h) + f.Bv;
            V(x, y, u0, v0, c); V(x + w, y, u1, v0, c); V(x, y + h, u0, v1, c);
            V(x + w, y, u1, v0, c); V(x + w, y + h, u1, v1, c); V(x, y + h, u0, v1, c);
        }
    }
}
