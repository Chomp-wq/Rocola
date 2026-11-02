// DENTRO DE UN DISCO Y EL REPOSO, CON EL ASPECTO DE LA ROCOLA DE XP (dibujo propio, OpenGL ES directo).
//   Dentro de un disco: arriba «Artista - Disco»; a la izquierda la portada en su cubo, que gira despacio; a la derecha el panel con las
//   canciones (su numero en un circulo, el titulo y lo que dura; la elegida, resaltada; SONANDO o EN COLA en su pastilla).
//   El reposo (el estado extendido): el cielo arriba y abajo un panel con AHORA SUENA (la portada grande, el titulo, el artista, el reloj
//   y la barra) y A CONTINUACION (hasta doce tarjetas de la cola en tres columnas).
using System;
using System.Collections.Generic;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        private static readonly Color4 Turquesa = Color4.Hex(0x7FD3D3), PanelDisco = Color4.Hex(0x161F42);

        private void Disco(Controlador c, float W, float abajo)
        {
            Form1.Disco disco = c.dentro;
            Form1 m = c.motor;
            string titulo = string.IsNullOrEmpty(disco.Nombre) ? disco.Artista : disco.Artista + " - " + disco.Nombre;
            float xp = W * 0.434f, top = 112 * k;
            nGrande.Escribe(d, nGrande.Ajusta(titulo, xp - 40 * k), 22 * k, 112 * k, Blanco);
            // el cubo grande
            float lado = Math.Min(xp - 80 * k, abajo - 210 * k), cx0 = (xp - lado) / 2 + 4 * k, cy0 = top + 62 * k;
            if (m.ColorGenero) { string g = m.GeneroDeDisco(disco); if (g.Length > 0) Resplandor(Form1.ColorDeEtiqueta(g), cx0 + lado / 2, cy0 + lado / 2, lado * 0.85f); }
            Textura tx = caratulas.De(disco.Portada);
            d.Brillo(cx0 + lado / 2, cy0 + lado / 2, lado * 0.85f, (tx != null ? tx.Media : Dorado).ConAlfa(0.28f));
            d.Brillo(cx0 + lado / 2, cy0 + lado + 6 * k, lado * 0.6f, new Color4(0, 0, 0.05f, 0.5f));
            Cubo(disco.Portada, disco.Artista, cx0, cy0, lado, reloj.Elapsed.TotalSeconds * 0.55);
            // el panel de las canciones
            float px = xp, py = top + 76 * k, pw = W - px - 28 * k, ph = abajo - py - 12 * k;
            d.RedondoDegradado(px, py, pw, ph, 18 * k, PanelDisco.ConAlfa(0.82f), Color4.Hex(0x0E1534, 0.86f));
            d.Contorno(px, py, pw, ph, 18 * k, 1.3f * k, Color4.Hex(0x9AAAE0, 0.40f));
            bool gr = m.ListaGrande;
            float fila = gr ? 58 * k : 51 * k, y0 = py + 4 * k;
            int caben = Math.Max(1, (int)((ph - 8 * k) / fila));
            c.PorPagina = caben;
            string suena = m.Sonando;
            Dictionary<string, int> enCola = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), minutos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            List<Form1.EnColaVista> cola = m.ColaParaPantalla(64);
            for (int q = 0; q < cola.Count; q++) if (cola[q].Ruta != null && !enCola.ContainsKey(cola[q].Ruta)) { enCola[cola[q].Ruta] = q + 1; minutos[cola[q].Ruta] = cola[q].Minutos; }
            int n = disco.Rutas.Count, desde = Math.Max(0, Math.Min(c.cursorCancion - caben / 2, n - caben));
            Letra lt = gr ? nGrande : nMedia;
            for (int i = 0; i < caben && desde + i < n; i++)
            {
                int j = desde + i; float y = y0 + i * fila, fy = y + 4 * k, fh = fila - 8 * k, cy = fy + fh / 2;
                bool sel = j == c.cursorCancion;
                if (sel)
                {
                    d.RedondoDegradado(px + 4 * k, fy, pw - 8 * k, fh, 12 * k, Color4.Hex(0x45558C, 0.85f), Color4.Hex(0x34426F, 0.85f));
                    d.Contorno(px + 4 * k, fy, pw - 8 * k, fh, 12 * k, 1.4f * k, Color4.Hex(0xC6D2FF, 0.55f));
                    zCancion = new Zona(px + 4 * k, fy, pw - 8 * k, fh);
                }
                // el numero en su circulo
                float rc = 12 * k, xc = px + 26 * k;
                Circulo(xc, cy, rc, sel ? Color4.Hex(0x5A6CA8) : Color4.Hex(0x26305A));
                Anillo(xc, cy, rc, 1.3f * k, Color4.Hex(0xAEBBEE, sel ? 0.9f : 0.5f));
                string num = (j + 1).ToString();
                mini.EscribeCentrado(d, num, xc, cy - mini.Tam * 0.55f, Blanco);
                // a la derecha: lo que dura y, si toca, SONANDO o EN COLA
                float der = px + pw - 16 * k;
                string du = m.DurText(disco.Rutas[j]);
                if (du.Length > 0) { float wd = nMedia.Mide(du); nMedia.Escribe(d, du, der - wd, cy - nMedia.Tam * 0.55f, sel ? Blanco : Color4.Hex(0xC4CCEC)); der -= wd + 14 * k; }
                string ruta = disco.Rutas[j], pastilla = null; Color4 cp = Verde; bool estaSonando = suena != null && string.Equals(suena, ruta, StringComparison.OrdinalIgnoreCase);
                int puesto;
                if (estaSonando) pastilla = "SONANDO";
                else if (enCola.TryGetValue(ruta, out puesto)) { pastilla = m.ColaMinutos ? Form1.TextoCola(puesto, minutos[ruta]) : "EN COLA " + puesto; cp = Dorado; }
                if (pastilla != null)
                {
                    float barras = estaSonando && m.EqSonando ? 26 * k : 0;
                    float wp = mini.Mide(pastilla) + 18 * k + barras, hp = 22 * k, yp = cy - hp / 2;
                    d.Redondo(der - wp, yp, wp, hp, hp / 2, cp.ConAlfa(0.20f));
                    mini.Escribe(d, pastilla, der - wp + 9 * k + barras, yp + 3 * k, cp);
                    if (barras > 0) Barritas(der - wp + 9 * k, yp + 4 * k, 18 * k, hp - 8 * k, cp);
                    der -= wp + 10 * k;
                }
                float xt = xc + rc + 14 * k;
                if (sel) EscribeDesliza(c, lt, disco.Titulos[j], xt, cy - lt.Tam * 0.58f, der - xt, Blanco);
                else lt.Escribe(d, lt.Ajusta(disco.Titulos[j], der - xt), xt, cy - lt.Tam * 0.58f, Color4.Hex(0xE6EAFA, 0.9f));
            }
            // si hay mas de las que caben, una barrita a la derecha dice donde se esta
            if (n > caben)
            {
                float bh = ph - 24 * k, bt = py + 12 * k, alto = Math.Max(20 * k, bh * caben / n), pos = bt + (bh - alto) * desde / Math.Max(1, n - caben);
                d.Redondo(px + pw - 6 * k, bt, 3 * k, bh, 1.5f * k, Color4.Hex(0x3A4678, 0.6f));
                d.Redondo(px + pw - 6 * k, pos, 3 * k, alto, 1.5f * k, Color4.Hex(0xAEBBEE, 0.9f));
            }
        }
        // Un anillo fino (el borde de los circulos de los numeros).
        private void Anillo(float cx, float cy, float r, float g, Color4 c)
        {
            const int n = 28;
            for (int i = 0; i < n; i++)
            {
                double a0 = 2 * Math.PI * i / n, a1 = 2 * Math.PI * (i + 1) / n;
                float c0 = (float)Math.Cos(a0), s0 = (float)Math.Sin(a0), c1 = (float)Math.Cos(a1), s1 = (float)Math.Sin(a1);
                d.Cuatro(cx + r * c0, cy + r * s0, c, cx + r * c1, cy + r * s1, c, cx + (r - g) * c1, cy + (r - g) * s1, c, cx + (r - g) * c0, cy + (r - g) * s0, c);
            }
        }

        // ---------------- EL REPOSO ----------------
        private void Extendido(Controlador c, float W, float H, float top)
        {
            Form1 m = c.motor;
            float ph = H - top;
            d.Degradado(0, top - 26 * k, W, 26 * k, Color4.Hex(0x0E1534, 0f), Color4.Hex(0x0E1534, 0.55f));
            d.Degradado(0, top, W, ph, Color4.Hex(0x111A3C, 0.94f), Color4.Hex(0x0A1028, 0.96f));
            d.Rect(0, top, W, 1.5f * k, Color4.Hex(0x7FA0FF, 0.55f));
            d.Brillo(W * 0.5f, top, W * 0.35f, Color4.Hex(0x6A7CFF, 0.12f));
            string t = m.SonandoTitulo; if (t == null) return;
            List<Form1.EnColaVista> cola = m.ColaParaPantalla(m.ColaDoce ? 12 : 3);
            float xc = cola.Count == 0 ? W : W * 0.52f;   // donde empieza la cola
            // AHORA SUENA
            Espaciado("AHORA SUENA", 25 * k, top + 12 * k, Turquesa);
            float lado = Math.Max(40 * k, Math.Min(ph - 56 * k, W * 0.17f)), lx = 26 * k, ly = top + 46 * k;
            Textura tx = caratulas.De(m.SonandoPortada);
            if (m.ColorGenero && m.ColorSonando != 0) Resplandor(m.ColorSonando, lx + lado / 2, ly + lado / 2, lado * 0.9f);
            else d.Brillo(lx + lado / 2, ly + lado / 2, lado * 0.85f, (tx != null ? tx.Media : Dorado).ConAlfa(0.30f + 0.25f * pGraves));
            Cubo(m.SonandoPortada, m.SonandoDisco ?? t, lx, ly, lado, reloj.Elapsed.TotalSeconds * 0.5);
            float x = lx + lado + 22 * k, ancho = xc - x - 24 * k;
            EscribeDesliza(c, nEnorme.Tam * 1.15f < ph * 0.25f ? nEnorme : nGrande, t, x, top + 54 * k, ancho, Blanco);
            float y = top + 54 * k + nEnorme.Tam * 1.55f;
            if (m.SonandoDisco != null) { nMedia.Escribe(d, nMedia.Ajusta(m.SonandoDisco, ancho), x, y, Turquesa); y += nMedia.Tam * 1.5f; }
            string gen = m.EtiquetaSonando;
            if (!string.IsNullOrEmpty(gen) && gen != "Otros")
            {
                Color4 cg = m.ColorGenero && m.ColorSonando != 0 ? Color4.Hex(m.ColorSonando) : Dorado;
                float wg = mini.Mide(gen) + 20 * k;
                d.Brillo(x + wg / 2, y + 11 * k, wg * 0.8f, cg.ConAlfa(0.25f * pGraves));
                d.Redondo(x, y, wg, 22 * k, 11 * k, cg.ConAlfa(0.30f));
                mini.Escribe(d, gen, x + 10 * k, y + 3 * k, Blanco);
            }
            // el reloj y la barra neon, abajo
            string rl = m.SonandoTiempo; if (string.IsNullOrEmpty(rl)) rl = "0:00 / 0:00";
            float by = H - 22 * k, wr = nMedia.Mide(rl);
            nMedia.Escribe(d, rl, x, by - nMedia.Tam * 0.62f, Blanco);
            float bx = x + wr + 18 * k, bw = Math.Max(20 * k, xc - 24 * k - bx), hb = 6 * k;
            d.Redondo(bx, by - hb / 2, bw, hb, hb / 2, Color4.Hex(0x1A2450, 0.95f));
            float p = Math.Max(0f, Math.Min(1f, c.Progreso));
            if (p > 0)
            {
                Color4 neon = m.ColorGenero && m.ColorSonando != 0 ? Color4.Hex(m.ColorSonando) : Color4.Hex(0x58D2FF);
                float wp = Math.Max(hb, bw * p);
                d.RedondoDegradado(bx, by - hb / 2, wp, hb, hb / 2, Dibujo.Mezcla(neon, Blanco, 0.5f), neon);
                d.Brillo(bx + wp, by, 14 * k, Blanco.ConAlfa(0.7f)); d.Brillo(bx + wp, by, 4 * k, Blanco);
            }
            if (cola.Count == 0) return;
            // A CONTINUACION: tarjetas en tres columnas (dos si caben pocas)
            nChica.Escribe(d, "A CONTINUACIÓN  /  " + m.EnCola + (m.EnCola == 1 ? " CANCIÓN" : " CANCIONES"), xc, top + 10 * k, Turquesa);
            int cols = cola.Count > 4 ? 3 : 2, filas = (cola.Count + cols - 1) / cols;
            float gx = 8 * k, cw = (W - 24 * k - xc - gx * (cols - 1)) / cols, ty = top + 38 * k, fh = Math.Min(44 * k, (H - 12 * k - ty) / Math.Max(1, filas) - 6 * k);
            for (int i = 0; i < cola.Count; i++)
            {
                Form1.EnColaVista q = cola[i];
                float qx = xc + (i % cols) * (cw + gx), qy = ty + (i / cols) * (fh + 6 * k);
                d.RedondoDegradado(qx, qy, cw, fh, 8 * k, Color4.Hex(0x1C2754, 0.85f), Color4.Hex(0x141D44, 0.85f));
                float tl = fh - 10 * k;
                Textura tq = caratulas.De(q.Portada);
                if (tq != null) d.Imagen(tq, qx + 5 * k, qy + 5 * k, tl, tl); else d.Redondo(qx + 5 * k, qy + 5 * k, tl, tl, 4 * k, Color4.Hex(0x4A3E9A));
                string nn = (i + 1).ToString("00");
                d.Redondo(qx + 5 * k, qy + fh - 15 * k, mini.Mide(nn) + 6 * k, 13 * k, 3 * k, new Color4(0, 0, 0.05f, 0.75f));
                micro.Escribe(d, nn, qx + 8 * k, qy + fh - 14.5f * k, Blanco);
                float tx0 = qx + tl + 12 * k, aw = qx + cw - 6 * k - tx0;
                nChica.Escribe(d, nChica.Ajusta(q.Titulo, aw), tx0, qy + 5 * k, Blanco);
                string sub = q.Video ? "video" : (q.Disco ?? "");
                if (fh > 36 * k && sub.Length > 0) micro.Escribe(d, micro.Ajusta(sub, aw), tx0, qy + 5 * k + nChica.Tam * 1.25f, Turquesa);
            }
        }
        // Un letrero con las letras separadas (AHORA SUENA de XP).
        private void Espaciado(string s, float x, float y, Color4 c)
        {
            foreach (char ch in s) { string l = ch.ToString(); nChica.Escribe(d, l, x, y, c); x += nChica.Mide(l) + (ch == ' ' ? 6 * k : 3.5f * k); }
        }
    }
}
