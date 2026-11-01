// LA PANTALLA VIVA DE XP, TANDAS 2 Y 3 (DiscoVivoGpu.cs y SuenaVivoGpu.cs, peticion del 24-09-2026), en lo esencial: el ecualizador de la
// que suena, el boleto que vuela a la cola, la moneda que cae, la tarjeta que invita cuando no hay creditos y los avisos que se entienden.
// Cada una con su interruptor de XP (encendidos de fabrica); el controlador apunta la hora de cada suceso y aqui se anima.
using System;
using System.Collections.Generic;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        private Zona zCola, zSuena, zSelResultado;
        private static int Edad(int tick) { return tick == 0 ? int.MaxValue : unchecked(Environment.TickCount - tick); }
        private static float Suave(float u) { u = Math.Max(0f, Math.Min(1f, u)); return u * u * (3f - 2f * u); }

        // Cuatro barritas que se mueven con la musica (el pulso que mide el motor a la salida).
        private void Barritas(float x, float y, float w, float h, Color4 c)
        {
            double t = reloj.Elapsed.TotalSeconds;
            float[] base_ = { pGraves, pMedios, (pMedios + pAgudos) / 2, pAgudos };
            float bw = w / 4 - 2 * k;
            for (int i = 0; i < 4; i++)
            {
                float v = Math.Max(0.15f, Math.Min(1f, 0.25f + 0.9f * base_[i] + 0.18f * (float)Math.Sin(t * (7 + 3 * i) + i)));
                d.Rect(x + i * (bw + 2 * k), y + h * (1 - v), bw, h * v, c);
            }
        }

        // El saltito de la capsula de la cola cuando llega el boleto (px hacia arriba).
        private float Salto(Controlador c)
        {
            int e = Edad(c.VueloTick) - VueloMs;
            if (e < 0 || e > 320) return 0f;
            return 6 * k * (float)Math.Sin(e / 320.0 * Math.PI);
        }

        // ---- LOS AVISOS QUE SE ENTIENDEN (avisos_claros=): entran por la derecha, con su color y su icono segun lo que dicen, en dos
        // renglones, y una linea que va ensenando cuanto les queda. El tipo, por lo que dice (en Rocola OS los avisos no traen color).
        internal enum TipoAviso { Hecho, Cuidado, Error, Novedad }
        internal static TipoAviso TipoDe(string t)
        {
            string s = (t ?? "").ToLowerInvariant();
            if (s.StartsWith("reanudando") || s.StartsWith("carpetas actualizadas") || s.Contains("guardados") || s.Contains("configurados")) return TipoAviso.Novedad;
            if (s.StartsWith("no se pudo") || s.Contains("danada") || s.Contains("dañada") || s.Contains("error") || s.Contains("fallo")) return TipoAviso.Error;
            if (s.StartsWith("inserta") || s.Contains("ya va seguida") || s.StartsWith("no se puede") || s.Contains("no esta disponible") || s.Contains("espera")) return TipoAviso.Cuidado;
            return TipoAviso.Hecho;
        }
        private static Color4 ColorAviso(TipoAviso t)
        {
            return t == TipoAviso.Cuidado ? Color4.Hex(0xF2CE48) : t == TipoAviso.Error ? Color4.Hex(0xF06460) : t == TipoAviso.Novedad ? Color4.Hex(0x96C8FF) : Color4.Hex(0x48D470);
        }
        private static Color4 FondoAviso(TipoAviso t)
        {
            return t == TipoAviso.Cuidado ? Color4.Hex(0x342808, 0.96f) : t == TipoAviso.Error ? Color4.Hex(0x3E1212, 0.96f) : t == TipoAviso.Novedad ? Color4.Hex(0x0E1C3C, 0.96f) : Color4.Hex(0x0E3422, 0.96f);
        }
        // "No se pudo reproducir: credito devuelto" -> titulo y renglon (PartirAviso de XP, tal cual).
        internal static void PartirAviso(string texto, out string titulo, out string detalle)
        {
            string t = (texto ?? "").Trim();
            titulo = t; detalle = "";
            int p = t.IndexOf(": ", StringComparison.Ordinal);
            if (p <= 0 || p >= t.Length - 2) p = -1;
            int q = t.IndexOf(" - ", StringComparison.Ordinal);
            if (p < 0 && q > 0 && q < t.Length - 3) { titulo = t.Substring(0, q); detalle = t.Substring(q + 3); return; }
            if (p > 0) { titulo = t.Substring(0, p); detalle = t.Substring(p + 2); }
        }
        private void AvisoClaro(Controlador c, string texto, float W)
        {
            TipoAviso tipo = TipoDe(texto);
            string tit, det; PartirAviso(texto, out tit, out det);
            int e = Math.Max(0, Edad(c.AvisoDesde)), dura = c.AvisoDuraMs;
            float entra = Suave(e / 220f), sale = 1f - Suave((e - (dura - 320)) / 320f);
            float alfa = Math.Min(entra, sale);
            float w = Math.Min(W * 0.6f, Math.Max(media.Mide(tit), chica.Mide(det)) + 96 * k), h = det.Length > 0 ? 70 * k : 52 * k;
            float x = W - 18 * k - w + 64 * k * (1f - entra), y = 80 * k;
            Color4 col = ColorAviso(tipo);
            d.Redondo(x - 2 * k, y - 2 * k, w + 4 * k, h + 4 * k, 14 * k, col.ConAlfa(0.8f * alfa));
            d.Redondo(x, y, w, h, 12 * k, FondoAviso(tipo).ConAlfa(alfa));
            // el icono: un circulo con su signo (hecho, cuidado, error, novedad)
            float r = 15 * k, cx = x + 16 * k + r, cy = y + h / 2;
            Circulo(cx, cy, r, col.ConAlfa(alfa));
            Color4 tinta = FondoAviso(tipo).ConAlfa(alfa);
            if (tipo == TipoAviso.Hecho) { d.Linea(cx - 7 * k, cy, cx - 2 * k, cy + 6 * k, 4 * k, tinta); d.Linea(cx - 3 * k, cy + 6 * k, cx + 8 * k, cy - 6 * k, 4 * k, tinta); }
            else if (tipo == TipoAviso.Error) { d.Linea(cx - 6 * k, cy - 6 * k, cx + 6 * k, cy + 6 * k, 4 * k, tinta); d.Linea(cx + 6 * k, cy - 6 * k, cx - 6 * k, cy + 6 * k, 4 * k, tinta); }
            else media.EscribeCentrado(d, tipo == TipoAviso.Cuidado ? "!" : "i", cx, cy - media.Tam * 0.55f, tinta);
            float tx = cx + r + 14 * k, tw = x + w - tx - 14 * k;
            media.Escribe(d, media.Ajusta(tit, tw), tx, y + (det.Length > 0 ? 10 * k : (h - media.Tam) / 2 - 2 * k), Blanco.ConAlfa(alfa));
            if (det.Length > 0) chica.Escribe(d, chica.Ajusta(det, tw), tx, y + 40 * k, Color4.Hex(0xDCE4FF, alfa));
            // la linea de lo que le queda
            float queda = Math.Max(0f, 1f - e / (float)dura);
            d.Rect(x + 12 * k, y + h - 5 * k, (w - 24 * k) * queda, 2 * k, col.ConAlfa(0.8f * alfa));
        }

        // ---- LOS TEXTOS QUE SE DESLIZAN (textos_deslizantes= de XP): si un titulo no cabe, en vez de cortarlo con puntos suspensivos se
        // corre despacio (40 px/s) hasta el final, espera y vuelve; con el interruptor apagado, como siempre.
        private void EscribeDesliza(Controlador c, Letra l, string s, float x, float y, float ancho, Color4 col)
        {
            float w = l.Mide(s);
            if (!c.motor.TextosDeslizantes || w <= ancho) { l.Escribe(d, l.Ajusta(s, ancho), x, y, col); return; }
            float sobra = w - ancho, vel = 40 * k, pausa = 1.6f, viaje = sobra / vel, ciclo = 2 * (pausa + viaje);
            float t = (float)(reloj.Elapsed.TotalSeconds % ciclo), dx;
            if (t < pausa) dx = 0; else if (t < pausa + viaje) dx = (t - pausa) * vel; else if (t < 2 * pausa + viaje) dx = sobra; else dx = sobra - (t - 2 * pausa - viaje) * vel;
            d.Recorta(x, y - l.Tam * 0.3f, ancho, l.Tam * 1.6f);
            l.Escribe(d, s, x - dx, y, col);
            d.SinRecorte();
        }

        // ---- EL BUSCADOR CON PORTADAS (buscador_portadas= de XP, BuscadorPortadasGpu.cs): arriba la casilla de lo escrito, grande, con su
        // lupa y cuantos resultados hay; debajo, los atajos; a la izquierda el teclado compacto (teclas de tamano fijo) y a la derecha las
        // tarjetas en dos columnas, con su portada (o la inicial), que es, el nombre con lo tecleado resaltado, un renglon que ayuda a elegir
        // y SONANDO o EN COLA si toca; si hay mas de las que caben, se baja fila a fila y una barrita lo dice.
        private void BuscadorPortadas(Controlador c, float W, float abajo)
        {
            Form1 m = c.motor;
            float x0 = 24 * k, y0 = 76 * k, hb = 60 * k;
            d.Redondo(x0, y0, W - 48 * k, hb, 14 * k, PanelClaro);
            // la lupa
            float lx = x0 + 30 * k, ly = y0 + hb / 2 - 3 * k, lr = 12 * k;
            Circulo(lx, ly, lr, Dorado); Circulo(lx, ly, lr - 3.5f * k, PanelClaro);
            d.Linea(lx + lr * 0.7f, ly + lr * 0.7f, lx + lr * 1.5f, ly + lr * 1.5f, 4 * k, Dorado);
            string escrito = c.Escrito.Length > 0 ? c.Escrito + "_" : "Escribe un artista, un disco o una canción";
            grande.Escribe(d, grande.Ajusta(escrito, W - 340 * k), x0 + 60 * k, y0 + (hb - grande.Tam) / 2 - 2 * k, c.Escrito.Length > 0 ? Blanco : Gris);
            if (c.Resultados.Count > 0)
            {
                string cu = c.Resultados.Count + (c.Resultados.Count == 1 ? " resultado" : " resultados");
                media.Escribe(d, cu, W - 48 * k - media.Mide(cu), y0 + (hb - media.Tam) / 2 - 2 * k, Dorado);
            }
            // los atajos
            float ay = y0 + hb + 10 * k, ah = 34 * k, axx = x0;
            for (int i = 0; i < c.Atajos.Count; i++)
            {
                string t = c.Atajos[i].Texto; float w = chica.Mide(t) + 28 * k;
                if (axx + w > W - 24 * k) break;   // si no caben todos, se quedan fuera los ultimos
                bool sel = !c.EnResultados && c.FilaTecla < 0 && c.ColTecla == i;
                d.Redondo(axx, ay, w, ah, ah / 2, sel ? Dorado : Panel.ConAlfa(0.9f));
                chica.Escribe(d, t, axx + 14 * k, ay + (ah - chica.Tam) / 2 - 1 * k, sel ? Fondo : Blanco);
                axx += w + 8 * k;
            }
            // el teclado compacto
            float tecla = 40 * k, sep = 4 * k, ty = ay + (c.Atajos.Count > 0 ? ah + 12 * k : 0), ty0 = ty;
            for (int f = 0; f < Controlador.FilasTeclado.Length; f++)
            {
                string fila = Controlador.FilasTeclado[f]; float tx = x0;
                for (int i = 0; i < fila.Length; i++)
                {
                    string et = Controlador.Etiqueta(fila[i]);
                    float w = et.Length > 1 ? tecla * 3 + sep * 2 : tecla;
                    bool sel = !c.EnResultados && f == c.FilaTecla && i == c.ColTecla;
                    d.Redondo(tx, ty, w, tecla, 8 * k, sel ? Dorado : Panel);
                    (et.Length > 1 ? chica : media).EscribeCentrado(d, et, tx + w / 2, ty + (tecla - (et.Length > 1 ? chica : media).Tam) / 2 - 2 * k, sel ? Fondo : Blanco);
                    tx += w + sep;
                }
                ty += tecla + sep;
            }
            zTeclado = new Zona(x0, ty0, 10 * (tecla + sep), ty - ty0);
            // las tarjetas
            float rx = x0 + 10 * (tecla + sep) + 20 * k, rw = W - rx - 24 * k, ry = ty0, cw = (rw - 12 * k) / 2, ch = 84 * k;
            int filas = Math.Max(1, (int)((abajo - ry - 34 * k) / (ch + 10 * k)));
            zResultados = new Zona(rx, ry, rw, filas * (ch + 10 * k));
            if (c.Resultados.Count == 0) { if (c.Escrito.Length > 0) media.Escribe(d, "Nada con eso", rx, ry, Gris); }
            int filaSel = c.SelResultado / 2, filasTot = (c.Resultados.Count + 1) / 2;
            int desde = Math.Max(0, Math.Min(filaSel - filas / 2, filasTot - filas));
            string suena = m.Sonando;
            HashSet<string> enCola = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Form1.EnColaVista q in m.ColaParaPantalla(64)) if (q.Ruta != null) enCola.Add(q.Ruta);
            for (int fi = 0; fi < filas && desde + fi < filasTot; fi++)
                for (int co = 0; co < 2; co++)
                {
                    int i = (desde + fi) * 2 + co; if (i >= c.Resultados.Count) break;
                    Form1.Resultado r = c.Resultados[i];
                    float cx = rx + co * (cw + 12 * k), cy = ry + fi * (ch + 10 * k);
                    bool sel = c.EnResultados && i == c.SelResultado;
                    if (sel) { d.Redondo(cx - 3 * k, cy - 3 * k, cw + 6 * k, ch + 6 * k, 14 * k, Dorado); zSelResultado = new Zona(cx, cy, cw, ch); }
                    d.Redondo(cx, cy, cw, ch, 12 * k, sel ? PanelClaro : Panel.ConAlfa(0.92f));
                    float pl = ch - 16 * k;
                    if (r.Tipo == 3) { Circulo(cx + 8 * k + pl / 2, cy + 8 * k + pl / 2, pl / 2, Dorado); }
                    else Portada(r.Portada, r.Texto, cx + 8 * k, cy + 8 * k, pl);
                    float tx = cx + pl + 20 * k, tw = cw - pl - 30 * k;
                    string tipo = r.Tipo == 0 ? "ARTISTA" : r.Tipo == 1 ? "DISCO" : r.Tipo == 3 ? "SERVICIO" : "CANCIÓN";
                    chica.Escribe(d, tipo, tx, cy + 8 * k, r.Tipo == 2 ? Verde : Dorado);
                    // la pastilla de SONANDO o EN COLA
                    string pas = r.Ruta != null && string.Equals(r.Ruta, suena, StringComparison.OrdinalIgnoreCase) ? "SONANDO" : r.Ruta != null && enCola.Contains(r.Ruta) ? "EN COLA" : null;
                    float twd = tw;
                    if (pas != null) { float wp = chica.Mide(pas) + 14 * k; d.Redondo(cx + cw - wp - 8 * k, cy + 56 * k, wp, 22 * k, 11 * k, (pas == "SONANDO" ? Verde : Dorado).ConAlfa(0.22f)); chica.Escribe(d, pas, cx + cw - wp - 1 * k, cy + 57 * k, pas == "SONANDO" ? Verde : Dorado); twd = tw - wp - 8 * k; }
                    Resaltado(r.Texto, c.Escrito, tx, cy + 30 * k, tw, sel);
                    if (!string.IsNullOrEmpty(r.Detalle)) chica.Escribe(d, chica.Ajusta(r.Detalle, twd), tx, cy + 58 * k, Gris);
                }
            if (filasTot > filas)
            {   // la barrita de cuanto hay
                float bh = filas * (ch + 10 * k), hh = bh * filas / filasTot, by = ry + bh * desde / filasTot;
                d.Redondo(W - 16 * k, ry, 4 * k, bh, 2 * k, Panel); d.Redondo(W - 16 * k, by, 4 * k, hh, 2 * k, Dorado);
            }
            chica.Escribe(d, "PONER: escribir / elegir    REGRESAR: borrar    REGRESAR + ARRIBA: cerrar    ARRIBA desde la primera fila: atajos", x0, abajo - 28 * k, Gris.ConAlfa(0.8f));
        }
        // El nombre con lo tecleado resaltado (sin distinguir mayusculas ni acentos).
        private void Resaltado(string texto, string buscado, float x, float y, float ancho, bool sel)
        {
            string t = media.Ajusta(texto ?? "", ancho), b = (buscado ?? "").Trim();
            int i = b.Length > 0 ? Plano(t).IndexOf(Plano(b), StringComparison.Ordinal) : -1;
            if (i < 0) { media.Escribe(d, t, x, y, sel ? Blanco : Color4.Hex(0xDCE4FF)); return; }
            float x1 = x + media.Escribe(d, t.Substring(0, i), x, y, sel ? Blanco : Color4.Hex(0xDCE4FF));
            float x2 = x1 + media.Escribe(d, t.Substring(i, b.Length), x1, y, Dorado);
            media.Escribe(d, t.Substring(i + b.Length), x2, y, sel ? Blanco : Color4.Hex(0xDCE4FF));
        }
        private static string Plano(string s)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
            foreach (char ch in s.ToUpperInvariant())
                sb.Append(ch == 'Á' ? 'A' : ch == 'É' ? 'E' : ch == 'Í' ? 'I' : ch == 'Ó' ? 'O' : ch == 'Ú' || ch == 'Ü' ? 'U' : ch);
            return sb.ToString();
        }

        // ---- LAS PISTAS DEL ROJO (guia_rojo= de XP, GuiaRojoGpu.cs): con el ROJO mantenido en la pared se encienden la barra de letras
        // (con una flecha a cada lado: ROJO + IZQ/DER cambia de letra) y las casillas BUSCAR y AYUDA, con su flecha llena de rojo vivo y un
        // brillo que late (peticion del 12-10-2026: que se resalten las que ya tienen en su contenedor, sin flechitas sueltas).
        private void GuiaRojo(Controlador c)
        {
            float late = 0.6f + 0.4f * (float)Math.Sin(reloj.Elapsed.TotalSeconds * 6);
            Color4 oro = Color4.Hex(0xFFD65E);
            if (!zLetras.Vacia && c.dentro == null && !c.Buscando)
            {
                Marco(zLetras, oro.ConAlfa(0.9f), 2 * k);
                float h = Math.Min(30 * k, zLetras.H * 0.66f), w = 18 * k, cy = zLetras.Cy;
                float xi = zLetras.X - 4 * k, xd = zLetras.X + zLetras.W + 4 * k;
                d.Triangulo(xi - w, cy, xi, cy - h / 2, xi, cy + h / 2, oro.ConAlfa(late));
                d.Triangulo(xd + w, cy, xd, cy - h / 2, xd, cy + h / 2, oro.ConAlfa(late));
            }
            foreach (Zona z in new[] { zBuscar, zAyuda })
            {
                if (z.Vacia) continue;
                d.Redondo(z.X - 2 * k, z.Y - 2 * k, z.W + 4 * k, z.H + 4 * k, z.H / 2 + 2 * k, oro.ConAlfa(0.35f * late));
                bool busca = z.X == zBuscar.X;
                float r = 9 * k, tx = z.X + z.W - 29 * k, cy = z.Cy;   // la tecla de flecha de la casilla (la misma cuenta que Casilla)
                d.Brillo(tx, cy, r * 2.6f, Rojo.ConAlfa(0.6f * late));
                Circulo(tx, cy, r + 1.5f * k, Color4.Hex(0xFFE6E6));
                Circulo(tx, cy, r, Rojo);
                float a = r * 0.62f;
                if (busca) d.Triangulo(tx, cy - a, tx + a, cy + a * 0.6f, tx - a, cy + a * 0.6f, Blanco);
                else d.Triangulo(tx - a, cy - a * 0.6f, tx + a, cy - a * 0.6f, tx, cy + a, Blanco);
            }
        }

        // ---- LO DE ENCIMA: la moneda que cae, la tarjeta que invita y el boleto que vuela ----
        internal const int MonedaCaeMs = 650, MonedaGolpeMs = 300, MonedaSumaMs = 1300, InvitaMs = 4200, VueloMs = 620;
        private int vueloVisto; private Zona vueloDesde; private string vueloTitulo = "";
        private void Efectos(Controlador c, float W, float H)
        {
            Form1 m = c.motor;
            if (m.GuiaRojo && c.RojoMantenido && !c.AyudaAbierta && c.AjusteSel < 0) GuiaRojo(c);
            // LA MONEDA QUE CAE: una moneda dorada girando hasta la capsula de Creditos; la capsula da un golpe con un destello verde y
            // sube "+4 creditos".
            int em = Edad(c.MonedaTick);
            if (m.MonedaCae && em < MonedaCaeMs + MonedaSumaMs && !zCreditos.Vacia)
            {
                if (em < MonedaCaeMs)
                {
                    float u = em / (float)MonedaCaeMs, cae = u * u;   // cae acelerando
                    float cx = zCreditos.Cx, cy = -30 * k + (zCreditos.Cy + 30 * k) * cae, r = 16 * k;
                    float giro = Math.Abs((float)Math.Cos(em / 1000.0 * 14));
                    d.Brillo(cx, cy, r * 2.2f, Dorado.ConAlfa(0.35f));
                    float rx = Math.Max(2 * k, r * giro);
                    d.Redondo(cx - rx, cy - r, 2 * rx, 2 * r, rx, Color4.Hex(0xE0A93A));
                    d.Redondo(cx - rx * 0.75f, cy - r * 0.75f, 1.5f * rx, 1.5f * r, rx * 0.75f, Color4.Hex(0xFFD45A));
                }
                else
                {
                    int eg = em - MonedaCaeMs;
                    if (eg < MonedaGolpeMs) d.Brillo(zCreditos.Cx, zCreditos.Cy, zCreditos.W * 0.8f, Verde.ConAlfa(0.5f * (1f - eg / (float)MonedaGolpeMs)));
                    float u = eg / (float)MonedaSumaMs;
                    string mas = "+" + c.MonedaSuma + (c.MonedaSuma == 1 ? " crédito" : " créditos");
                    media.EscribeCentrado(d, mas, zCreditos.Cx, zCreditos.Y + zCreditos.H + 6 * k - 18 * k * u, Verde.ConAlfa(1f - Suave((u - 0.6f) / 0.4f)));
                }
            }
            // SIN CREDITOS, INVITAR: una tarjeta amable con la ranura y una moneda entrando; se va sola.
            int ei = Edad(c.InvitaTick);
            if (m.SinCreditosInvita && ei < InvitaMs)
            {
                float alfa = Math.Min(Suave(ei / 250f), 1f - Suave((ei - (InvitaMs - 400)) / 400f));
                float w = Math.Min(W - 80 * k, 560 * k), h = 150 * k, x = (W - w) / 2, y = H * 0.32f + 12 * k * (1f - alfa);
                d.Redondo(x - 2 * k, y - 2 * k, w + 4 * k, h + 4 * k, 20 * k, Dorado.ConAlfa(0.7f * alfa));
                d.Redondo(x, y, w, h, 18 * k, Color4.Hex(0x16224A, alfa));
                // la ranura y la moneda que entra (y vuelve a entrar)
                float rx = x + 34 * k, ry = y + 40 * k, rw = 70 * k;
                float ciclo = (ei % 1400) / 1400f, baja = Suave(ciclo / 0.7f);
                float my = ry - 40 * k + 46 * k * baja;
                float va = alfa * (1f - Suave((ciclo - 0.75f) / 0.25f));
                d.Brillo(rx + rw / 2, my + 16 * k, 26 * k, Dorado.ConAlfa(0.3f * va));
                d.Redondo(rx + rw / 2 - 16 * k, my, 32 * k, 32 * k, 16 * k, Color4.Hex(0xE0A93A, va));
                d.Redondo(rx + rw / 2 - 12 * k, my + 4 * k, 24 * k, 24 * k, 12 * k, Color4.Hex(0xFFD45A, va));
                d.Rect(rx - 4 * k, ry + 6 * k, rw + 8 * k, 44 * k, Color4.Hex(0x16224A, alfa));   // tapa lo que ya entro por la ranura
                d.Redondo(rx, ry, rw, 12 * k, 6 * k, Color4.Hex(0x05081A, alfa));
                float tx = rx + rw + 26 * k, tw = x + w - tx - 20 * k;
                grande.Escribe(d, "Te falta un crédito", tx, y + 26 * k, Dorado.ConAlfa(alfa));
                string q = string.IsNullOrEmpty(c.InvitaTitulo) ? "Inserta una moneda para poner tu canción" : "Inserta una moneda para poner «" + c.InvitaTitulo + "»";
                var lineas = Envuelve(q, media, tw);
                for (int i = 0; i < lineas.Count && i < 2; i++) media.Escribe(d, lineas[i], tx, y + 72 * k + i * 30 * k, Blanco.ConAlfa(alfa));
            }
            // VER ADONDE VA: el boleto sale de la fila y vuela hasta la capsula de la cola (o a lo que suena, si no sonaba nada).
            if (c.VueloTick != vueloVisto)
            {
                vueloVisto = c.VueloTick;
                vueloDesde = c.VueloDesdeBuscador ? zSelResultado : zCancion;
                vueloTitulo = c.VueloTitulo ?? "";
            }
            int ev = Edad(c.VueloTick);
            if (m.VueloCola && ev < VueloMs && !vueloDesde.Vacia)
            {
                Zona dest = c.VueloASuena ? zSuena : zCola;
                if (!dest.Vacia)
                {
                    float u = Suave(ev / (float)VueloMs);
                    float sx = vueloDesde.Cx, sy = vueloDesde.Cy, ex = dest.Cx, ey = dest.Cy;
                    float px = sx + (ex - sx) * u, py = sy + (ey - sy) * u - (float)Math.Sin(u * Math.PI) * 120 * k;   // en arco
                    float bw = (1f - 0.6f * u) * Math.Min(260 * k, vueloDesde.W), bh = (1f - 0.5f * u) * 34 * k;
                    d.Brillo(px, py, bw * 0.6f, Dorado.ConAlfa(0.3f));
                    d.Redondo(px - bw / 2, py - bh / 2, bw, bh, bh / 2, Color4.Hex(0xFFD45A));
                    if (u < 0.6f) chica.EscribeCentrado(d, chica.Ajusta(vueloTitulo, bw - 16 * k), px, py - chica.Tam * 0.55f, Tinta.ConAlfa(1f - u / 0.6f));
                }
            }
        }
    }
}
