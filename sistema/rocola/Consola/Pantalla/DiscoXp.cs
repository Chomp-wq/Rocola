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
            if (m.DiscoVidrio)
            {
                // V7: la portada en grande y desenfocada detras de todo (vidrio esmerilado), con un velo para que se lea
                Textura bo = caratulas.Borrosa(disco.Portada);
                if (bo != null) { d.Imagen(bo, -W * 0.05f, top - 30 * k, W * 1.1f, abajo - top + 40 * k, 0.55f); d.Rect(0, top - 30 * k, W, abajo - top + 40 * k, Color4.Hex(0x0A1030, 0.35f)); }
            }
            Letra lTit = Titulo(20.5f * k);
            if (m.TitulosDegradado) EscribeDegradado(lTit, lTit.Ajusta(titulo, xp - 40 * k), 22 * k, 116 * k, 0);   // C2
            else lTit.Escribe(d, lTit.Ajusta(titulo, xp - 40 * k), 22 * k, 116 * k, Blanco);
            Color4 cd = ColorDeDisco(m, disco, false);   // C1, C5, C9: el color del disco
            // el cubo grande
            // P2: con la tira «Más de este artista» (Motor/SpotifyPortadas.cs), el cubo se achica para dejarle sitio debajo
            List<Form1.Disco> mas = c.MasDisco; float tira = mas != null && mas.Count > 0 ? 150 * k : 0;
            float lado = Math.Max(120 * k, Math.Min(xp - 80 * k, abajo - 210 * k - tira)), cx0 = (xp - lado) / 2 + 4 * k, cy0 = top + 62 * k;
            if (m.ColorGenero) { string g = m.GeneroDeDisco(disco); if (g.Length > 0) Resplandor(Form1.ColorDeEtiqueta(g), cx0 + lado / 2, cy0 + lado / 2, lado * 0.85f); }
            Textura tx = caratulas.De(disco.Portada);
            d.Brillo(cx0 + lado / 2, cy0 + lado / 2, lado * 0.85f, (tx != null ? tx.Media : Dorado).ConAlfa(0.28f));
            if (m.PortadasBrillan) d.Brillo(cx0 + lado / 2, cy0 + lado / 2, lado * 0.95f, cd.ConAlfa(0.20f + 0.20f * viveza));   // C9
            d.Brillo(cx0 + lado / 2, cy0 + lado + 6 * k, lado * 0.6f, new Color4(0, 0, 0.05f, 0.5f));
            Cubo(disco.Portada, disco.Artista, cx0, cy0, lado, reloj.Elapsed.TotalSeconds * 0.55);
            if (Form1.EsRutaSpotify(disco.Carpeta)) m.QuierePortada(disco);
            if (tira > 0) TiraMas(c, mas, 26 * k, cy0 + lado + 26 * k, xp - 52 * k, abajo - 10 * k - (cy0 + lado + 26 * k));
            // el panel de las canciones
            float px = xp, py = top + 76 * k, pw = W - px - 28 * k, ph = abajo - py - 12 * k;
            if (m.VidrioDisco) { Vidrio(px, py, pw, ph, 18 * k, tx != null ? tx.Media : Color4.Hex(0x6F8CFF), 1f, m.DiscoVidrio, 0.50f); VidrioVivo(m, px, py, pw, ph, 18 * k, cd, 1f); }   // G5 (con V7, detras esta la portada borrosa: vidrio de verdad); C5, C6
            else d.RedondoDegradado(px, py, pw, ph, 18 * k, PanelDisco.ConAlfa(0.82f), Color4.Hex(0x0E1534, 0.86f));
            d.Contorno(px, py, pw, ph, 18 * k, 1.3f * k, m.ListaColores && tx != null ? Dibujo.Mezcla(tx.Media, Blanco, 0.4f).ConAlfa(0.45f) : Color4.Hex(0x9AAAE0, 0.40f));   // V8
            bool gr = m.ListaGrande;
            // FilaGrande y CuerpoGrande de XP: unas nueve filas por pagina (44 a 80 px) y la letra de un tercio de la fila (en puntos); la normal, 30 px y 13 pt
            float fila = gr ? Math.Max(44 * k, Math.Min(80 * k, (float)Math.Floor((ph - 8 * k) / 9))) : 30 * k, y0 = py + 4 * k;
            int caben = Math.Max(1, (int)((ph - 8 * k) / fila));
            c.PorPagina = caben;
            string suena = m.Sonando;
            Dictionary<string, int> enCola = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), minutos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            List<Form1.EnColaVista> cola = m.ColaParaPantalla(64);
            for (int q = 0; q < cola.Count; q++) if (cola[q].Ruta != null && !enCola.ContainsKey(cola[q].Ruta)) { enCola[cola[q].Ruta] = q + 1; minutos[cola[q].Ruta] = cola[q].Minutos; }
            int n = disco.Rutas.Count, desde = Math.Max(0, Math.Min(c.cursorCancion - caben / 2, n - caben));
            Letra lt = Titulo(gr ? fila / 3f * 4f / 3f : 13 * 4f / 3f * k);
            // M3: la lista se corre suave (desdeF sigue a desde) y las filas de los bordes se desvanecen; M2: la fila elegida viaja
            double ahoraS = reloj.Elapsed.TotalSeconds;
            float desdeF = m.ListasSuaves ? desdeLista.Hacia(desde, ahoraS) : desde;
            int j0 = Math.Max(0, (int)Math.Floor(desdeF)), j1 = Math.Min(n, (int)Math.Ceiling(desdeF) + caben);
            Zona zMueve = default(Zona); bool mueve = m.SeleccionDesliza && selCancion.Ahora(ahoraS, out zMueve);
            zoomBase = d.Zoom; zoomBaseA = d.ZoomA;
            bool recorta = n > caben;   // (lo que entra y sale por los bordes, dentro del panel)
            if (recorta) d.Recorta(px, y0, pw, caben * fila);
            if (mueve) FilaElegida(m, tx, zMueve.X, zMueve.Y, zMueve.W, zMueve.H);
            for (int j = j0; j < j1; j++)
            {
                float y = y0 + (j - desdeF) * fila, fy = y + 4 * k, fh = fila - 8 * k, cy = fy + fh / 2;
                float sobra = Math.Max(y0 - y, y + fila - (y0 + caben * fila)) / fila;
                if (sobra >= 0.6f) continue;
                AlfaFila(1f - Math.Max(0f, sobra) / 0.6f);
                bool sel = j == c.cursorCancion && c.cursorMas < 0;   // (en la tira, ninguna cancion elegida)
                if (sel)
                {
                    selCancion.Meta(new Zona(px + 4 * k, fy, pw - 8 * k, fh)); zCancion = new Zona(px + 4 * k, fy, pw - 8 * k, fh);
                    if (!mueve) FilaElegida(m, tx, px + 4 * k, fy, pw - 8 * k, fh);
                }
                // el numero en su circulo
                float rc = 12 * k, xc = px + 26 * k;
                if (m.TextosColor)
                {   // C1: el numero con el color del disco
                    Circulo(xc, cy, rc, sel ? Dibujo.Mezcla(cd, Color4.Hex(0x101830), 0.35f) : Dibujo.Mezcla(cd, Color4.Hex(0x101830), 0.80f));
                    Anillo(xc, cy, rc, 1.3f * k, cd.ConAlfa(sel ? 1f : 0.75f));
                }
                else
                {
                    Circulo(xc, cy, rc, sel ? Color4.Hex(0x5A6CA8) : Color4.Hex(0x26305A));
                    Anillo(xc, cy, rc, 1.3f * k, Color4.Hex(0xAEBBEE, sel ? 0.9f : 0.5f));
                }
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
                if (sel) { EscribeDesliza(c, lt, disco.Titulos[j], xt, cy - lt.Tam * 0.58f, der - xt, Blanco); if (recorta) d.Recorta(px, y0, pw, caben * fila); }
                else lt.Escribe(d, lt.Ajusta(disco.Titulos[j], der - xt), xt, cy - lt.Tam * 0.58f, Color4.Hex(0xE6EAFA, 0.9f));
            }
            AlfaFila(1f); if (recorta) d.SinRecorte();
            // si hay mas de las que caben, una barrita a la derecha dice donde se esta
            if (n > caben)
            {
                float bh = ph - 24 * k, bt = py + 12 * k, alto = Math.Max(20 * k, bh * caben / n), pos = bt + (bh - alto) * desdeF / Math.Max(1, n - caben);
                d.Redondo(px + pw - 6 * k, bt, 3 * k, bh, 1.5f * k, Color4.Hex(0x3A4678, 0.6f));
                d.Redondo(px + pw - 6 * k, pos, 3 * k, alto, 1.5f * k, Color4.Hex(0xAEBBEE, 0.9f));
            }
        }
        // Un anillo fino (el borde de los circulos de los numeros).
        private static readonly float[] Cos28 = Tabla(28, true), Sin28 = Tabla(28, false);
        private void Anillo(float cx, float cy, float r, float g, Color4 c)
        {
            const int n = 28;
            for (int i = 0; i < n; i++)
            {
                float c0 = Cos28[i], s0 = Sin28[i], c1 = Cos28[i + 1], s1 = Sin28[i + 1];
                d.Cuatro(cx + r * c0, cy + r * s0, c, cx + r * c1, cy + r * s1, c, cx + (r - g) * c1, cy + (r - g) * s1, c, cx + (r - g) * c0, cy + (r - g) * s0, c);
            }
        }

        // ---------------- EL REPOSO ----------------
        private void Extendido(Controlador c, float W, float H, float top)
        {
            Form1 m = c.motor;
            float ph = H - top;
            if (m.VidrioReposo)
            {
                Vidrio(10 * k, top + 2 * k, W - 20 * k, ph - 10 * k, 26 * k, m.VidrioColor ? vivo1 : TinteSonando(m), 1f, false, 0.42f, 1.2f);   // G6 (C4)
                if (m.SonandoTitulo != null) VidrioVivo(m, 10 * k, top + 2 * k, W - 20 * k, ph - 10 * k, 26 * k, vivo1, 1f);   // C5, C6
            }
            else
            {
                d.Degradado(0, top - 26 * k, W, 26 * k, Color4.Hex(0x0E1534, 0f), Color4.Hex(0x0E1534, 0.55f));
                d.Degradado(0, top, W, ph, Color4.Hex(0x111A3C, 0.94f), Color4.Hex(0x0A1028, 0.96f));
                d.Rect(0, top, W, 1.5f * k, Color4.Hex(0x7FA0FF, 0.55f));
                d.Brillo(W * 0.5f, top, W * 0.35f, Color4.Hex(0x6A7CFF, 0.12f));
            }
            string t = m.SonandoTitulo; if (t == null) return;
            Perfil.Marca("ext vidrio");
            // SP11: la foto del artista de fondo (Spotify), estirada y tenue (la foto es chica: estirada ya se ve difuminada)
            string anio; string foto = m.FotoArtistaSonando(out anio);
            Textura tfa = foto != null ? caratulas.De(foto) : null;
            if (tfa != null)
            {
                d.Recorta(10 * k, top + 2 * k, W - 20 * k, ph - 10 * k);
                float ladoFoto = W * 0.62f;
                d.Imagen(tfa, W - ladoFoto, top + ph / 2 - ladoFoto / 2, ladoFoto, ladoFoto, 0.30f);
                d.DegradadoH(W - ladoFoto, top, ladoFoto * 0.6f, ph, Color4.Hex(0x0A1028, 0.95f), Color4.Hex(0x0A1028, 0f));
                d.SinRecorte();
            }
            Perfil.Marca("ext foto");
            List<Form1.EnColaVista> cola = m.ColaParaPantalla(m.ColaCarrusel ? 7 : m.ColaDoce ? 12 : 3);
            Perfil.Marca("ext cola");
            float xc = cola.Count == 0 ? W : W * 0.52f;   // donde empieza la cola
            // LAS LETRAS DEL PANEL DE XP (RenderIdlePanelEn): Trebuchet MS en negrita, a la escala del alto del panel
            float sc = Math.Max(.8f, Math.Min(1.3f, ph / (260f * k))) * k;
            Letra small = Tre(15 * sc, true), normal = Tre(20 * sc, true), big = Tre(Math.Max(22 * k, Math.Min(36 * sc, W / 30f)), true);
            // AHORA SUENA
            if (m.TitulosDegradado) EscribeDegradado(small, "AHORA SUENA", 24 * k, top + 12 * k, small.Tam * 0.42f);   // C2
            else Espaciado(small, "AHORA SUENA", 24 * k, top + 12 * k, small.Tam * 0.42f, Turquesa);
            float lado = Math.Max(40 * k, Math.Min(ph - 56 * k, W * 0.17f)), lx = 26 * k, ly = top + 46 * k;
            Textura tx = caratulas.De(m.SonandoPortada);
            LuzDelReposo(m, W, top, lx + lado / 2, ly + lado / 2, lado, tx);   // V13, V14
            if (m.ColorGenero && m.ColorSonando != 0) Resplandor(m.ColorSonando, lx + lado / 2, ly + lado / 2, lado * 0.9f);
            else d.Brillo(lx + lado / 2, ly + lado / 2, lado * 0.85f, (tx != null ? tx.Media : Dorado).ConAlfa(0.30f + 0.25f * pGraves));
            if (m.PortadasBrillan) d.Brillo(lx + lado / 2, ly + lado / 2, lado * 1.0f, vivo1.ConAlfa((0.18f + 0.22f * pGraves) * viveza));   // C9
            Cubo(m.SonandoPortada, m.SonandoDisco ?? t, lx, ly, lado, reloj.Elapsed.TotalSeconds * 0.5);
            Perfil.Marca("ext cubo");
            float x = lx + lado + 22 * k, ancho = xc - x - 24 * k;
            // el titulo, en hasta dos renglones con puntos (como el DrawString con LineLimit de XP, en un alto de min(90, 0,28 del panel))
            List<string> rt = Envuelve(t, big, ancho);
            int maxR = Math.Max(1, (int)(Math.Min(90 * k, ph * .28f) / (big.Tam * 1.16f)));
            if (rt.Count > maxR) { rt[maxR - 1] = big.Ajusta(rt[maxR - 1] + " " + string.Join(" ", rt.GetRange(maxR, rt.Count - maxR)), ancho); rt.RemoveRange(maxR, rt.Count - maxR); }
            for (int i = 0; i < rt.Count; i++) big.Escribe(d, rt[i], x, top + 56 * k + i * big.Tam * 1.16f, Blanco);
            float y = top + 56 * k + Math.Min(90 * k, ph * .28f) + 6 * k;
            if (m.SonandoDisco != null) { normal.Escribe(d, normal.Ajusta(m.SonandoDisco + (string.IsNullOrEmpty(anio) ? "" : " · " + anio), ancho), x, y, m.TextosColor ? Dibujo.Mezcla(vivo1, Blanco, 0.35f) : Turquesa); y += normal.Tam * 1.5f; }   // (el ano: SP11; C1)
            string gen = m.EtiquetaSonando;
            if (!string.IsNullOrEmpty(gen) && gen != "Otros")
            {
                Color4 cg = m.ColorGenero && m.ColorSonando != 0 ? Color4.Hex(m.ColorSonando) : Dorado;
                float wg = small.Mide(gen) + 20 * k;
                d.Brillo(x + wg / 2, y + 11 * k, wg * 0.8f, cg.ConAlfa(0.25f * pGraves));
                d.Redondo(x, y, wg, 22 * k, 11 * k, cg.ConAlfa(0.30f));
                EnRenglon(small, gen, x + 10 * k, y, wg, 22 * k, Blanco);
            }
            // el reloj y la barra neon, abajo
            string rl = m.SonandoTiempo; if (string.IsNullOrEmpty(rl)) rl = "0:00 / 0:00";
            Letra reloj_ = Ui(17 * sc);   // Tahoma en negrita en XP
            float by = H - 22 * k, wr = reloj_.Mide(rl);
            reloj_.Escribe(d, rl, x, by - reloj_.Tam * 0.62f, Blanco);
            float bx = x + wr + 18 * k, bw = Math.Max(20 * k, xc - 24 * k - bx), hb = 6 * k;
            d.Redondo(bx, by - hb / 2, bw, hb, hb / 2, Color4.Hex(0x1A2450, 0.95f));
            float p = Math.Max(0f, Math.Min(1f, c.Progreso));
            if (p > 0 && m.BarraNeon) BarraDeNeon(bx, by - hb / 2, Math.Max(hb, bw * p), hb);   // C11
            else if (p > 0)
            {
                Color4 neon = m.ColorGenero && m.ColorSonando != 0 ? Color4.Hex(m.ColorSonando) : Color4.Hex(0x58D2FF);
                float wp = Math.Max(hb, bw * p);
                d.RedondoDegradado(bx, by - hb / 2, wp, hb, hb / 2, Dibujo.Mezcla(neon, Blanco, 0.5f), neon);
                d.Brillo(bx + wp, by, 14 * k, Blanco.ConAlfa(0.7f)); d.Brillo(bx + wp, by, 4 * k, Blanco);
            }
            Perfil.Marca("ext textos");
            if (cola.Count == 0) return;
            if (m.ColaCarrusel) { Carrusel(m, cola, xc, top, W, H, small, normal); return; }   // V15
            // A CONTINUACION: tarjetas en tres columnas (dos si caben pocas)
            small.Escribe(d, "A CONTINUACIÓN  /  " + m.EnCola + (m.EnCola == 1 ? " CANCIÓN" : " CANCIONES"), xc, top + 10 * k, Turquesa);
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
                Letra qt = Tre(Math.Max(9, Math.Min(15 * sc, (fh - 8 * k) * .46f)), true), qa = Tre(Math.Max(8, Math.Min(12 * sc, (fh - 8 * k) * .34f)), true);
                qt.Escribe(d, qt.Ajusta(q.Titulo, aw), tx0, qy + 4 * k, Blanco);
                string sub = q.Video ? "video" : (q.Disco ?? "");
                if (fh > 30 * k && sub.Length > 0) qa.Escribe(d, qa.Ajusta(sub, aw), tx0, qy + 4 * k + qt.Tam * 1.16f, Turquesa);
            }
        }
        // Un letrero con las letras separadas (AHORA SUENA de XP).
        private void Espaciado(string s, float x, float y, Color4 c)
        {
            foreach (char ch in s) { string l = ch.ToString(); nChica.Escribe(d, l, x, y, c); x += nChica.Mide(l) + (ch == ' ' ? 6 * k : 3.5f * k); }
        }
        // La fila elegida de la lista de un disco (en su sitio, o donde va llegando con M2)
        private void FilaElegida(Form1 m, Textura tx, float x, float y, float w, float h)
        {
            Color4 s1 = Color4.Hex(0x4A6CB8, 0.92f), s2 = Color4.Hex(0x3A58A0, 0.92f), sb = Color4.Hex(0xC6D2FF, 0.55f);
            if (m.ListaColores && tx != null)
            {
                // V8: la fila elegida con el color de la portada, oscurecido lo justo para que el texto blanco se lea
                Color4 cm = tx.Media; s1 = Dibujo.Mezcla(cm, Color4.Hex(0x101830), 0.45f).ConAlfa(0.92f); s2 = Dibujo.Mezcla(cm, Color4.Hex(0x101830), 0.62f).ConAlfa(0.92f); sb = Dibujo.Mezcla(cm, Blanco, 0.5f).ConAlfa(0.7f);
            }
            d.RedondoDegradado(x, y, w, h, 12 * k, s1, s2);
            d.Contorno(x, y, w, h, 12 * k, 1.4f * k, sb);
        }
    }
}
