// EL CIELO VIVO DE ROCOLA OS, FIESTAS: MAS FORMAS (27-11-2026, «que se note mas», FASE11_cielo-fiestas.md, seccion «Que se note mas»). La misma malla plana
// de colores en los vertices de RocolaOs.CieloFiestaFormas.cs (sin texturas, ningun metodo reserva memoria por cuadro): el banderin de papel picado con su
// calado, confeti, serpentinas, petalos, flores, coronas, calaveritas sencillas, esferas, nochebuenas, pinatas de picos, banderas, copos y notas.
// Las tablas de seno y coseno de los discos se arman una vez (antes cada vertice llamaba a Math.Cos y Math.Sin).
using System;

namespace Rocola
{
    internal sealed partial class RocolaOsFiestaMalla
    {
        private static readonly float[][] CosT = ArmaTabla(true), SinT = ArmaTabla(false);
        private static float[][] ArmaTabla(bool coseno)
        {
            float[][] t = new float[33][];
            for (int n = 6; n <= 32; n++)
            {
                t[n] = new float[n + 1];
                for (int i = 0; i <= n; i++) { double a = i * 2 * Math.PI / n; t[n][i] = (float)(coseno ? Math.Cos(a) : Math.Sin(a)); }
            }
            return t;
        }

        // ---- lo basico nuevo
        // un triangulo con un color en cada vertice (los rayos de la estrella guia: del centro, que brilla, a la punta, que se apaga)
        internal void TriDeg(float x0, float y0, uint c0, float x1, float y1, uint c1, float x2, float y2, uint c2) { Hay(3); P(x0, y0, c0); P(x1, y1, c1); P(x2, y2, c2); }
        // una elipse girada 'rot' radianes (rx a lo largo de la direccion girada), con borde suavizado opcional
        internal void ElipseRot(float cx, float cy, float rx, float ry, float rot, uint c, int seg, float suave)
        {
            if (rx < 0.2f || ry < 0.2f) return;
            if (seg < 6) seg = 6; else if (seg > 32) seg = 32;
            Hay(seg * 3 + (suave > 0f ? seg * 6 : 0));
            uint c0 = c & 0x00FFFFFFu;
            float cr = (float)Math.Cos(rot), sr = (float)Math.Sin(rot);
            float[] ct = CosT[seg], sn = SinT[seg];
            float px = cx + rx * cr, py = cy + rx * sr, fx = cx + (rx + suave) * cr, fy = cy + (rx + suave) * sr;
            for (int i = 1; i <= seg; i++)
            {
                float co = ct[i], si = sn[i];
                float ex = rx * co, ey = ry * si;
                float x = cx + ex * cr - ey * sr, y = cy + ex * sr + ey * cr;
                P(cx, cy, c); P(px, py, c); P(x, y, c);
                if (suave > 0f)
                {
                    float gx0 = (rx + suave) * co, gy0 = (ry + suave) * si, gx = cx + gx0 * cr - gy0 * sr, gy = cy + gx0 * sr + gy0 * cr;
                    P(px, py, c); P(fx, fy, c0); P(gx, gy, c0);
                    P(px, py, c); P(gx, gy, c0); P(x, y, c);
                    fx = gx; fy = gy;
                }
                px = x; py = y;
            }
        }
        // un rectangulo girado (confeti, hojitas): centro, medio ancho, medio alto, angulo
        internal void Rotado(float cx, float cy, float hw, float hh, float ang, uint c)
        {
            float ca = (float)Math.Cos(ang), sa = (float)Math.Sin(ang);
            Cuadro4(cx - hw * ca + hh * sa, cy - hw * sa - hh * ca, cx + hw * ca + hh * sa, cy + hw * sa - hh * ca,
                    cx + hw * ca - hh * sa, cy + hw * sa + hh * ca, cx - hw * ca - hh * sa, cy - hw * sa + hh * ca, c);
        }
        // un pedazo de confeti que da la vuelta: el ancho se achica y crece con 'giro' (como un papelito de verdad)
        internal void Confeti(float cx, float cy, float tam, float ang, float giro, uint c)
        {
            float b = tam * 0.5f * Math.Abs((float)Math.Cos(giro)) + tam * 0.12f;
            Rotado(cx, cy, tam, b, ang, c);
        }
        // una serpentina: una cinta que ondula hacia abajo desde (x0, y0)
        internal void Serpentina(float x0, float y0, float largo, float amp, float fase, float grosor, uint c, int segs)
        {
            float px = x0 + amp * (float)Math.Sin(fase), py = y0;
            for (int k = 1; k <= segs; k++)
            {
                float y = y0 + largo * k / segs, x = x0 + amp * (float)Math.Sin(fase + k * 0.95f) * (0.6f + 0.4f * k / segs);
                Linea(px, py, x, y, grosor, c); px = x; py = y;
            }
        }

        // ---- el papel picado: un banderin de papel colgado de (cx, y0), ancho w, alto h, con su doblez de arriba, los picos de abajo y su CALADO (lo que se
        // cortaria: un marco, una flor de cuatro petalos, tres rombos y una fila de lagrimas, en un tono oscuro del mismo color para que se lea como hueco
        // por donde se ve el cielo, de dia y de noche)
        internal void Banderin(float cx, float y0, float w, float h, uint rgb, float alfa, float sway, int calado)
        {
            uint c = Con(rgb, alfa), hueco = Con(Mezcla(Escala(rgb, 0.28f), 0x0A0818u, 0.35f), 0.82f * alfa), dobles = Con(Escala(rgb, 0.72f), alfa), luz = Con(Mezcla(rgb, 0xFFFFFF, 0.55f), 0.55f * alfa);
            float x0 = cx - w * 0.5f, x1 = cx + w * 0.5f, d = h * 0.17f, yb = y0 + h - d, s = sway;
            Cuadro4(x0, y0, x1, y0, x1 + s, yb, x0 + s, yb, c);
            const int picos = 5; float tw = w / picos;
            for (int i = 0; i < picos; i++) Tri(x0 + s + i * tw, yb, x0 + s + (i + 1) * tw, yb, x0 + s + (i + 0.5f) * tw + s * 0.2f, y0 + h, c);
            Cuadro4(x0, y0, x1, y0, x1 + s * 0.15f, y0 + h * 0.07f, x0 + s * 0.15f, y0 + h * 0.07f, dobles);   // el doblez por donde pasa la cuerda
            float mx = cx + s * 0.5f, my = y0 + h * 0.43f;
            // el marco (cuatro tiras) y la flor del centro
            float mw = w * 0.38f, mh = h * 0.30f, g = Math.Max(1f, w * 0.035f);
            Rect(mx - mw, my - mh, mw * 2f, g, hueco); Rect(mx - mw, my + mh - g, mw * 2f, g, hueco);
            Rect(mx - mw, my - mh, g, mh * 2f, hueco); Rect(mx + mw - g, my - mh, g, mh * 2f, hueco);
            float pr = w * 0.15f, rr = w * 0.07f, ry = my - mh * 0.62f;
            if (calado == 1)
            {   // una calaverita calada: craneo, ojos, nariz y dientes
                Elipse(mx, my - mh * 0.08f, mw * 0.56f, mh * 0.62f, hueco, 10, 0f);
                Elipse(mx - mw * 0.24f, my - mh * 0.14f, mw * 0.17f, mh * 0.2f, luz, 6, 0f); Elipse(mx + mw * 0.24f, my - mh * 0.14f, mw * 0.17f, mh * 0.2f, luz, 6, 0f);
                Tri(mx, my + mh * 0.02f, mx - mw * 0.07f, my + mh * 0.2f, mx + mw * 0.07f, my + mh * 0.2f, luz);
                Rect(mx - mw * 0.3f, my + mh * 0.44f, mw * 0.6f, mh * 0.3f, hueco);
            }
            else if (calado == 2)
            {   // una flor de seis petalos
                for (int i = 0; i < 6; i++)
                {
                    float a = (float)(i * Math.PI / 3);
                    ElipseRot(mx + pr * 1.0f * (float)Math.Cos(a), my + pr * 1.0f * (float)Math.Sin(a), pr * 0.9f, pr * 0.5f, a, hueco, 8, 0f);
                }
                Elipse(mx, my, pr * 0.42f, pr * 0.42f, luz, 8, 0f);
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    float a = (float)(i * Math.PI / 2 + 0.785398);
                    ElipseRot(mx + pr * 0.85f * (float)Math.Cos(a), my + pr * 0.85f * (float)Math.Sin(a), pr * 0.8f, pr * 0.42f, a, hueco, 8, 0f);
                }
                Elipse(mx, my, pr * 0.34f, pr * 0.34f, luz, 8, 0f);
                // tres rombos (uno arriba y uno a cada lado de la flor)
                Cuadro4(mx, ry - rr, mx + rr, ry, mx, ry + rr, mx - rr, ry, hueco);
                Cuadro4(mx - mw * 0.62f, my - rr, mx - mw * 0.62f + rr, my, mx - mw * 0.62f, my + rr, mx - mw * 0.62f - rr, my, hueco);
                Cuadro4(mx + mw * 0.62f, my - rr, mx + mw * 0.62f + rr, my, mx + mw * 0.62f, my + rr, mx + mw * 0.62f - rr, my, hueco);
            }
            // la fila de lagrimas sobre los picos
            for (int i = 0; i < picos; i++)
            {
                float lx = x0 + s * 0.8f + (i + 0.5f) * tw, ly = yb - d * 0.18f;
                Tri(lx - tw * 0.16f, ly - d * 0.55f, lx + tw * 0.16f, ly - d * 0.55f, lx, ly + d * 0.15f, hueco);
            }
        }

        // ---- plantas y petalos
        // un petalo que cae: una elipse girada con una vena mas clara
        internal void Petalo(float cx, float cy, float largo, float ancho, float rot, uint rgb, float alfa)
        {
            ElipseRot(cx, cy, largo, ancho, rot, Con(rgb, alfa), 8, 0f);
            ElipseRot(cx + (float)Math.Cos(rot) * largo * 0.1f, cy + (float)Math.Sin(rot) * largo * 0.1f, largo * 0.62f, ancho * 0.34f, rot, Con(Mezcla(rgb, 0xFFFFFF, 0.35f), 0.7f * alfa), 6, 0f);
        }
        // una flor plana de 'petalos' petalos girada 'rot' (sin tallo ni borde suave: son muchas y van detras)
        internal void FlorPlana(float cx, float cy, float r, int petalos, uint petalo, uint centro, float rot, float alfa)
        {
            uint cp = Con(petalo, alfa), cc = Con(centro, alfa);
            float pr = r * (petalos <= 5 ? 0.5f : 0.38f), d = r * (petalos <= 5 ? 0.55f : 0.62f);
            for (int i = 0; i < petalos; i++)
            {
                float a = rot + (float)(i * 2 * Math.PI / petalos);
                Elipse(cx + d * (float)Math.Cos(a), cy + d * (float)Math.Sin(a), pr, pr, cp, 6, 0f);
            }
            Elipse(cx, cy, r * 0.34f, r * 0.34f, cc, 6, 0f);
        }
        // una rosa: dos vueltas de petalos, la de dentro mas oscura, y el boton
        internal void RosaPlana(float cx, float cy, float r, uint rgb, float rot, float alfa)
        {
            FlorPlana(cx, cy, r, 6, rgb, Escala(rgb, 0.6f), rot, alfa);
            FlorPlana(cx, cy, r * 0.55f, 5, Escala(rgb, 0.78f), Escala(rgb, 0.45f), -rot * 1.3f + 0.6f, alfa);
        }
        // la nochebuena: seis hojas verdes y ocho bracteas rojas con el centro amarillo
        internal void Nochebuena(float cx, float cy, float r, uint rojo, float rot, float alfa)
        {
            uint verde = Con(0x1F7A3A, alfa), cr = Con(rojo, alfa);
            for (int i = 0; i < 6; i++)
            {
                float a = rot + 0.52f + (float)(i * 2 * Math.PI / 6);
                ElipseRot(cx + r * 0.46f * (float)Math.Cos(a), cy + r * 0.46f * (float)Math.Sin(a), r * 0.5f, r * 0.2f, a, verde, 8, 0f);
            }
            for (int i = 0; i < 8; i++)
            {
                float a = rot + (float)(i * 2 * Math.PI / 8);
                ElipseRot(cx + r * 0.42f * (float)Math.Cos(a), cy + r * 0.42f * (float)Math.Sin(a), r * 0.46f, r * 0.2f, a, cr, 8, 0f);
            }
            Elipse(cx, cy, r * 0.2f, r * 0.2f, Con(0xFFD23C, alfa), 6, 0f);
        }

        // ---- cosas colgantes o que flotan
        // una corona de rey (Reyes): la banda, tres picos y sus gemas
        internal void Corona(float cx, float cy, float w, float h, uint rgb, float alfa)
        {
            uint c = Con(rgb, alfa), osc = Con(Escala(rgb, 0.7f), alfa), gema = Con(0xE0304A, alfa), gema2 = Con(0x3CA0FF, alfa);
            float x0 = cx - w * 0.5f, yb = cy + h * 0.2f, yt = cy - h * 0.5f;
            Tri(x0, yb, x0 + w * 0.34f, yb, x0 + w * 0.08f, yt + h * 0.12f, c);
            Tri(x0 + w * 0.26f, yb, x0 + w * 0.74f, yb, cx, yt - h * 0.06f, c);
            Tri(x0 + w * 0.66f, yb, x0 + w, yb, x0 + w * 0.92f, yt + h * 0.12f, c);
            Rect(x0, yb, w, h * 0.3f, osc);
            float gr = Math.Max(1.2f, w * 0.07f);
            Elipse(x0 + w * 0.08f, yt + h * 0.1f, gr, gr, gema2, 6, 0f);
            Elipse(cx, yt - h * 0.08f, gr * 1.2f, gr * 1.2f, gema, 6, 0f);
            Elipse(x0 + w * 0.92f, yt + h * 0.1f, gr, gr, gema2, 6, 0f);
            Elipse(cx, yb + h * 0.15f, gr * 0.9f, gr * 0.9f, gema, 6, 0f);
        }
        // una calaverita sencilla (Muertos): craneo, mandibula, ojos con aro de color, nariz, dientes y tres puntos en la frente
        internal void Calavera(float cx, float cy, float r, uint adorno, float alfa)
        {
            uint hueso = Con(0xF3EAD6, alfa), osc = Con(0x1A1226, alfa), col = Con(adorno, alfa);
            Elipse(cx, cy, r, r * 1.04f, hueso, 12, 0.8f);
            Elipse(cx, cy + r * 0.92f, r * 0.58f, r * 0.42f, hueso, 8, 0.7f);
            Elipse(cx - r * 0.38f, cy - r * 0.08f, r * 0.34f, r * 0.36f, col, 8, 0f);
            Elipse(cx + r * 0.38f, cy - r * 0.08f, r * 0.34f, r * 0.36f, col, 8, 0f);
            Elipse(cx - r * 0.38f, cy - r * 0.08f, r * 0.22f, r * 0.25f, osc, 8, 0f);
            Elipse(cx + r * 0.38f, cy - r * 0.08f, r * 0.22f, r * 0.25f, osc, 8, 0f);
            Tri(cx, cy + r * 0.14f, cx - r * 0.12f, cy + r * 0.42f, cx + r * 0.12f, cy + r * 0.42f, osc);
            float g = Math.Max(0.9f, r * 0.07f);
            for (int i = -2; i <= 2; i++) Linea(cx + i * r * 0.2f, cy + r * 0.72f, cx + i * r * 0.2f, cy + r * 1.28f, g, osc);
            Linea(cx - r * 0.5f, cy + r * 0.98f, cx + r * 0.5f, cy + r * 0.98f, g, osc);
            Elipse(cx, cy - r * 0.66f, r * 0.12f, r * 0.12f, col, 6, 0f);
            Elipse(cx - r * 0.3f, cy - r * 0.56f, r * 0.09f, r * 0.09f, col, 6, 0f);
            Elipse(cx + r * 0.3f, cy - r * 0.56f, r * 0.09f, r * 0.09f, col, 6, 0f);
        }
        // una esfera de Navidad: su casquillo, su cuerpo, una franja clara y el brillo (el hilo lo pone quien la cuelga)
        internal void Esfera(float cx, float cy, float r, uint rgb, float alfa)
        {
            Rect(cx - r * 0.2f, cy - r - r * 0.24f, r * 0.4f, r * 0.28f, Con(0xE0B84A, alfa));
            Elipse(cx, cy, r, r, Con(rgb, alfa), 14, 0.9f);
            Elipse(cx, cy + r * 0.05f, r * 0.86f, r * 0.2f, Con(Mezcla(rgb, 0xFFFFFF, 0.45f), 0.55f * alfa), 10, 0f);
            Elipse(cx - r * 0.38f, cy - r * 0.4f, r * 0.2f, r * 0.28f, Con(0xFFFFFF, 0.5f * alfa), 6, 0f);
        }
        // una pinata de picos (Posadas): siete picos de colores, el centro claro y borlas en las puntas
        internal void Pinata(float cx, float cy, float R, uint[] cols, float rot, float alfa)
        {
            const int n = 7; float r = R * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float a0 = rot + (float)(i * 2 * Math.PI / n), a1 = a0 + (float)(Math.PI / n), a2 = a0 + (float)(2 * Math.PI / n);
                uint k = cols[i % cols.Length] & 0x00FFFFFFu;
                float x0 = cx + R * (float)Math.Sin(a0), y0 = cy - R * (float)Math.Cos(a0), x1 = cx + r * (float)Math.Sin(a1), y1 = cy - r * (float)Math.Cos(a1), x2 = cx + R * (float)Math.Sin(a2), y2 = cy - R * (float)Math.Cos(a2);
                Tri(cx, cy, x0, y0, x1, y1, Con(k, alfa)); Tri(cx, cy, x1, y1, x2, y2, Con(Escala(k, 0.78f), alfa));
                Elipse(x0, y0, R * 0.1f, R * 0.1f, Con(0xFFE9A8, alfa), 6, 0f);
            }
            Elipse(cx, cy, R * 0.34f, R * 0.34f, Con(0xFFF1CC, alfa), 10, 0.7f);
        }
        // una chispa de cuatro puntas que titila
        internal void Chispa(float cx, float cy, float R, uint c, float rot) { Estrella(cx, cy, R, R * 0.26f, 4, rot, c); }
        // la estrella guia de Reyes: cinco puntas, una cruz de rayos largos que se apagan en la punta y una cola
        internal void EstrellaGuia(float cx, float cy, float R, float rot, float brillo, float alfa)
        {
            uint oro = 0xFFE9A8u, c = Con(oro, alfa), c0 = Con(oro, 0f), cm = Con(oro, 0.8f * alfa * brillo);
            float L = R * 3.2f, a = R * 0.16f;
            TriDeg(cx - a, cy, cm, cx + a, cy, cm, cx, cy - L, c0);
            TriDeg(cx - a, cy, cm, cx + a, cy, cm, cx, cy + L * 0.8f, c0);
            TriDeg(cx, cy - a, cm, cx, cy + a, cm, cx - L * 0.9f, cy, c0);
            TriDeg(cx, cy - a, cm, cx, cy + a, cm, cx + L * 0.9f, cy, c0);
            // la cola (hacia arriba a la derecha, como la de un cometa)
            float dx = L * 1.6f, dy = -L * 0.75f, l = (float)Math.Sqrt(dx * dx + dy * dy), nx = -dy / l * R * 0.28f, ny = dx / l * R * 0.28f;
            TriDeg(cx + nx, cy + ny, Con(oro, 0.55f * alfa), cx - nx, cy - ny, Con(oro, 0.55f * alfa), cx + dx, cy + dy, c0);
            Estrella(cx, cy, R, R * 0.42f, 5, rot, c);
        }
        // una nota doble (dos corcheas unidas por la barra)
        internal void NotaDoble(float cx, float cy, float h, uint c)
        {
            float rx = h * 0.2f, ry = h * 0.14f, dx = h * 0.5f, py = h * 0.74f;
            Elipse(cx, cy, rx, ry, c, 8, 0.6f); Elipse(cx + dx, cy - h * 0.06f, rx, ry, c, 8, 0.6f);
            float g = Math.Max(1f, h * 0.085f);
            Linea(cx + rx * 0.85f, cy, cx + rx * 0.85f, cy - py, g, c);
            Linea(cx + dx + rx * 0.85f, cy - h * 0.06f, cx + dx + rx * 0.85f, cy - py - h * 0.06f, g, c);
            Cuadro4(cx + rx * 0.85f, cy - py, cx + dx + rx * 0.85f, cy - py - h * 0.06f, cx + dx + rx * 0.85f, cy - py - h * 0.06f + h * 0.15f, cx + rx * 0.85f, cy - py + h * 0.15f, c);
        }
        // un copo: chico (un punto), mediano (un disco con tres rayos) o grande (seis brazos, cada uno con dos pares de ramitas, y un centro que brilla)
        internal void Copo(float cx, float cy, float r, float rot, uint c)
        {
            if (r < 3.4f) { Elipse(cx, cy, r, r, c, 6, 0f); return; }
            float g = Math.Max(0.9f, r * 0.1f);
            if (r < 6f)
            {
                Elipse(cx, cy, r * 0.4f, r * 0.4f, c, 6, 0f);
                for (int i = 0; i < 3; i++) { float a = rot + i * 1.0472f, ca = (float)Math.Cos(a) * r, sa = (float)Math.Sin(a) * r; Linea(cx - ca, cy - sa, cx + ca, cy + sa, g, c); }
                return;
            }
            for (int i = 0; i < 6; i++)
            {
                float a = rot + i * 1.0472f, ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
                Linea(cx, cy, cx + ca * r, cy + sa * r, g * 1.25f, c);
                for (int q = 0; q < (r < 10f ? 1 : 2); q++)
                {
                    float d = r * (q == 0 ? 0.55f : 0.8f), l = r * (q == 0 ? 0.34f : 0.24f), bx = cx + ca * d, by = cy + sa * d;
                    float a1 = a + 0.9f, a2 = a - 0.9f;
                    Linea(bx, by, bx + (float)Math.Cos(a1) * l, by + (float)Math.Sin(a1) * l, g, c);
                    Linea(bx, by, bx + (float)Math.Cos(a2) * l, by + (float)Math.Sin(a2) * l, g, c);
                }
            }
            Elipse(cx, cy, r * 0.2f, r * 0.2f, c, 6, 0f);
        }

        // ---- banderas
        // una bandera de Mexico (verde, blanco y rojo) en el asta de (px, y0): bw de ancho, bh de alto, ondeando con 't' (amp: lo que ondea, ya con la calma);
        // con el escudo si es grande
        internal void Bandera(float px, float y0, float bw, float bh, float t, float amp, float alfa, float astaH, bool escudo)
        {
            uint asta = Con(0xCFCFCF, 0.9f * alfa);
            Rect(px - 1f, y0 - bh * 0.12f, Math.Max(2f, bw * 0.03f), astaH, asta);
            Elipse(px, y0 - bh * 0.14f, Math.Max(2f, bw * 0.05f), Math.Max(2f, bw * 0.05f), Con(0xE8C860, alfa), 6, 0f);
            const int tiras = 12;
            float off = px + 2f;
            for (int j = 0; j < tiras; j++)
            {
                float x0 = off + bw * j / tiras, x1 = off + bw * (j + 1) / tiras;
                float dy0 = (float)Math.Sin(t * 3.2f - j * 0.62f) * amp * (j / (float)tiras), dy1 = (float)Math.Sin(t * 3.2f - (j + 1) * 0.62f) * amp * ((j + 1) / (float)tiras);
                int tr = Math.Min(2, j * 3 / tiras);
                uint c = tr == 0 ? 0x00A05Au : (tr == 1 ? 0xF4F4F4u : 0xD8202Eu);
                float sombra = 0.9f + 0.12f * (float)Math.Cos(t * 3.2f - j * 0.62f);
                Cuadro4(x0, y0 + dy0, x1, y0 + dy1, x1, y0 + bh + dy1, x0, y0 + bh + dy0, Con(Escala(c, sombra), alfa));
            }
            if (escudo)
            {
                float ex = off + bw * 0.5f, ey = y0 + bh * 0.5f + (float)Math.Sin(t * 3.2f - 6f * 0.62f) * amp * 0.5f;
                Elipse(ex, ey, bh * 0.15f, bh * 0.15f, Con(0x8B5A2B, 0.9f * alfa), 8, 0.7f);
                Elipse(ex, ey, bh * 0.075f, bh * 0.075f, Con(0xD4A017, 0.9f * alfa), 6, 0f);
            }
        }
    }
}
