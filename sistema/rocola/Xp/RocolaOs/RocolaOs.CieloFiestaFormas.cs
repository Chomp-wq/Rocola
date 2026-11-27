// EL CIELO VIVO DE ROCOLA OS, FIESTAS (FI1-FI6): LAS FORMAS (FASE11_cielo-fiestas.md). Geometria plana con color en los vertices (la malla de
// la tarjeta de XP: GpuStarfield.DrawMesh, mezcla normal; sin texturas, asi no hay nada que hornear ni que soltar al reiniciar la tarjeta): discos
// con el borde suavizado (un anillo que se apaga en 1 px), lineas con grosor, estrellas, corazones, flores, globos, farolitos, velas, notas y
// banderines de papel picado. Ningun metodo reserva memoria por cuadro: el arreglo crece una vez y se reusa.
using System;

namespace Rocola
{
    internal sealed class RocolaOsFiestaMalla
    {
        internal Tarjeta.Vtx[] V = new Tarjeta.Vtx[6144];
        internal int N;                                // vertices escritos
        internal int Tris { get { return N / 3; } }
        internal void Limpia() { N = 0; }
        private void Hay(int n) { if (N + n > V.Length) Array.Resize(ref V, Math.Max(V.Length * 2, N + n)); }
        private void P(float x, float y, uint c) { V[N].X = x; V[N].Y = y; V[N].Z = 0f; V[N].Rhw = 1f; V[N].Color = c; V[N].U = 0f; V[N].V = 0f; N++; }

        // ---- el color
        internal static uint Con(uint rgb, float alfa)
        {
            int a = (int)(alfa * 255f + 0.5f); if (a < 0) a = 0; else if (a > 255) a = 255;
            return ((uint)a << 24) | (rgb & 0x00FFFFFFu);
        }
        // un color a la mitad de camino de otro (RGB, 0 a 1)
        internal static uint Mezcla(uint a, uint b, float t)
        {
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            uint r = 0;
            for (int sh = 0; sh < 24; sh += 8)
            {
                float x = (a >> sh) & 0xFF, y = (b >> sh) & 0xFF;
                r |= ((uint)(x + (y - x) * t + 0.5f) & 0xFFu) << sh;
            }
            return r;
        }
        internal static uint Escala(uint rgb, float k)
        {
            float r = ((rgb >> 16) & 255) * k, g = ((rgb >> 8) & 255) * k, b = (rgb & 255) * k;
            return ((uint)Math.Min(255, (int)(r + 0.5f)) << 16) | ((uint)Math.Min(255, (int)(g + 0.5f)) << 8) | (uint)Math.Min(255, (int)(b + 0.5f));
        }

        internal static uint Op(uint c) { return (c >> 24) == 0 ? c | 0xFF000000u : c; }

        // ---- lo basico
        internal void Tri(float x0, float y0, float x1, float y1, float x2, float y2, uint c) { Hay(3); P(x0, y0, c); P(x1, y1, c); P(x2, y2, c); }
        internal void Cuadro4(float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, uint c)
        { Hay(6); P(x0, y0, c); P(x1, y1, c); P(x2, y2, c); P(x0, y0, c); P(x2, y2, c); P(x3, y3, c); }
        internal void Rect(float x, float y, float w, float h, uint c) { Cuadro4(x, y, x + w, y, x + w, y + h, x, y + h, c); }
        // un rectangulo con un color arriba y otro abajo
        internal void RectDeg(float x, float y, float w, float h, uint arriba, uint abajo)
        { Hay(6); P(x, y, arriba); P(x + w, y, arriba); P(x + w, y + h, abajo); P(x, y, arriba); P(x + w, y + h, abajo); P(x, y + h, abajo); }
        // una linea con grosor (de punta a punta)
        internal void Linea(float x0, float y0, float x1, float y1, float grosor, uint c)
        {
            float dx = x1 - x0, dy = y1 - y0, l = (float)Math.Sqrt(dx * dx + dy * dy);
            if (l < 0.001f) return;
            float nx = -dy / l * grosor * 0.5f, ny = dx / l * grosor * 0.5f;
            Cuadro4(x0 + nx, y0 + ny, x1 + nx, y1 + ny, x1 - nx, y1 - ny, x0 - nx, y0 - ny, c);
        }

        // un disco o elipse con el borde suavizado: el relleno (abanico) y un anillo que se apaga en 'suave' pixeles
        internal void Elipse(float cx, float cy, float rx, float ry, uint c, int seg, float suave)
        {
            if (rx < 0.2f || ry < 0.2f) return;
            if (seg < 6) seg = 6;
            Hay(seg * 3 + (suave > 0f ? seg * 6 : 0));
            uint c0 = c & 0x00FFFFFFu;
            float px = cx + rx, py = cy, fx = cx + rx + suave, fy = cy;
            for (int i = 1; i <= seg; i++)
            {
                float a = (float)(i * 2 * Math.PI / seg), co = (float)Math.Cos(a), si = (float)Math.Sin(a);
                float x = cx + rx * co, y = cy + ry * si;
                P(cx, cy, c); P(px, py, c); P(x, y, c);
                if (suave > 0f)
                {
                    float gx = cx + (rx + suave) * co, gy = cy + (ry + suave) * si;
                    P(px, py, c); P(fx, fy, c0); P(gx, gy, c0);
                    P(px, py, c); P(gx, gy, c0); P(x, y, c);
                    fx = gx; fy = gy;
                }
                px = x; py = y;
            }
        }
        internal void Disco(float cx, float cy, float r, uint c) { Elipse(cx, cy, r, r, c, r > 6f ? 16 : 10, 0.9f); }

        // una estrella de 'puntas' puntas (radio de fuera R, de dentro r) girada 'rot' radianes
        internal void Estrella(float cx, float cy, float R, float r, int puntas, float rot, uint c)
        {
            Hay(puntas * 6);
            for (int i = 0; i < puntas; i++)
            {
                float a0 = rot + (float)(i * 2 * Math.PI / puntas), a1 = a0 + (float)(Math.PI / puntas), a2 = a0 + (float)(2 * Math.PI / puntas);
                float x0 = cx + R * (float)Math.Cos(a0 - Math.PI / 2), y0 = cy + R * (float)Math.Sin(a0 - Math.PI / 2);
                float x1 = cx + r * (float)Math.Cos(a1 - Math.PI / 2), y1 = cy + r * (float)Math.Sin(a1 - Math.PI / 2);
                float x2 = cx + R * (float)Math.Cos(a2 - Math.PI / 2), y2 = cy + R * (float)Math.Sin(a2 - Math.PI / 2);
                P(cx, cy, c); P(x0, y0, c); P(x1, y1, c);
                P(cx, cy, c); P(x1, y1, c); P(x2, y2, c);
            }
        }
        // un corazon de ancho 's' (dos discos y una punta)
        internal void Corazon(float cx, float cy, float s, uint c)
        {
            float r = s * 0.27f;
            Elipse(cx - s * 0.23f, cy - s * 0.14f, r, r, c, 10, 0.7f);
            Elipse(cx + s * 0.23f, cy - s * 0.14f, r, r, c, 10, 0.7f);
            Tri(cx - s * 0.49f, cy - s * 0.06f, cx + s * 0.49f, cy - s * 0.06f, cx, cy + s * 0.46f, c);
            Tri(cx - s * 0.23f, cy - s * 0.12f, cx + s * 0.23f, cy - s * 0.12f, cx, cy + s * 0.2f, c);
        }
        // una flor de cabeza (cx, cy), radio r, con tallo hasta y0
        internal void Flor(float cx, float cy, float r, int petalos, uint petalo, uint centro, float y0, uint tallo)
        {
            petalo = Op(petalo); centro = Op(centro); tallo = Op(tallo);   // (un color sin alfa se toma opaco)
            if (y0 > cy + r * 0.6f) Linea(cx, cy, cx, y0, Math.Max(0.9f, r * 0.22f), tallo);
            float pr = r * (petalos <= 5 ? 0.52f : 0.4f), d = r * (petalos <= 5 ? 0.56f : 0.62f);
            for (int i = 0; i < petalos; i++)
            {
                float a = (float)(i * 2 * Math.PI / petalos - Math.PI / 2);
                Elipse(cx + d * (float)Math.Cos(a), cy + d * (float)Math.Sin(a), pr, pr, petalo, 8, 0.6f);
            }
            Elipse(cx, cy, r * 0.36f, r * 0.36f, centro, 8, 0.6f);
        }
        // una nota musical (corchea) de alto 'h': cabeza, plica y bandera
        internal void Nota(float cx, float cy, float h, uint c)
        {
            float rx = h * 0.24f, ry = h * 0.17f;
            Elipse(cx, cy, rx, ry, c, 8, 0.6f);
            float px = cx + rx * 0.85f;
            Linea(px, cy, px, cy - h * 0.78f, Math.Max(1f, h * 0.09f), c);
            Tri(px, cy - h * 0.78f, px + h * 0.34f, cy - h * 0.5f, px, cy - h * 0.5f, c);
        }
        // un globo (centro cx, cy; alto 2*ry) con su nudo, su brillo y su hilo que ondea con 'fase'
        internal void Globo(float cx, float cy, float rx, float ry, uint rgb, float alfa, float fase, uint hilo)
        {
            uint c = Con(rgb, alfa);
            Elipse(cx, cy, rx, ry, c, 14, 1f);
            Elipse(cx - rx * 0.36f, cy - ry * 0.36f, rx * 0.2f, ry * 0.26f, Con(0xFFFFFF, 0.32f * alfa), 8, 0.6f);
            Tri(cx, cy + ry * 0.98f, cx - rx * 0.16f, cy + ry * 1.18f, cx + rx * 0.16f, cy + ry * 1.18f, Con(Escala(rgb, 0.8f), alfa));
            float x = cx, y = cy + ry * 1.16f; uint h = Con(hilo, 0.55f * alfa);
            for (int i = 1; i <= 3; i++)
            {
                float nx = cx + (float)Math.Sin(fase + i * 1.3f) * rx * 0.28f * i, ny = cy + ry * 1.16f + i * ry * 0.34f;
                Linea(x, y, nx, ny, 0.9f, h); x = nx; y = ny;
            }
        }
        // un banderin de papel picado: colgado de (cx, y0), ancho w, alto h, con los dientes de abajo y su calado (rombo y dos puntos mas claros)
        internal void Banderin(float cx, float y0, float w, float h, uint rgb, float alfa, float sway)
        {
            uint c = Con(rgb, alfa), calado = Con(Mezcla(rgb, 0xFFFFFF, 0.55f), 0.4f * alfa);
            float x0 = cx - w * 0.5f, x1 = cx + w * 0.5f, d = h * 0.2f, yb = y0 + h - d, s = sway;
            Cuadro4(x0, y0, x1, y0, x1 + s, yb, x0 + s, yb, c);
            float tw = w / 4f;
            for (int i = 0; i < 4; i++) Tri(x0 + s + i * tw, yb, x0 + s + (i + 1) * tw, yb, x0 + s + (i + 0.5f) * tw + s * 0.2f, y0 + h, c);
            float mx = cx + s * 0.6f, my = y0 + h * 0.45f;
            Cuadro4(mx, my - h * 0.2f, mx + w * 0.2f, my, mx, my + h * 0.2f, mx - w * 0.2f, my, calado);
            Elipse(mx - w * 0.3f, my - h * 0.1f, w * 0.06f, w * 0.06f, calado, 6, 0.5f);
            Elipse(mx + w * 0.3f, my - h * 0.1f, w * 0.06f, w * 0.06f, calado, 6, 0.5f);
        }
        // un farolito de papel (centro cx, cy; ancho 2*rx): cuerpo, tapas y borlita
        internal void Farolito(float cx, float cy, float rx, float ry, uint rgb, float alfa)
        {
            uint c = Con(rgb, alfa), osc = Con(Escala(rgb, 0.55f), alfa);
            Elipse(cx, cy, rx, ry, c, 12, 0.8f);
            Elipse(cx, cy - ry * 0.2f, rx * 0.45f, ry * 0.55f, Con(Mezcla(rgb, 0xFFFFFF, 0.5f), 0.45f * alfa), 8, 0.6f);
            Rect(cx - rx * 0.45f, cy - ry - 1.4f, rx * 0.9f, 1.8f, osc);
            Rect(cx - rx * 0.4f, cy + ry - 0.6f, rx * 0.8f, 1.6f, osc);
            Linea(cx, cy + ry + 0.8f, cx, cy + ry + 2.6f, 0.9f, osc);
        }
    }
}
