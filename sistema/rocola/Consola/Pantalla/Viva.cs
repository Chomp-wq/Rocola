// LA PANTALLA VIVA DE XP, TANDAS 2 Y 3 (DiscoVivoGpu.cs y SuenaVivoGpu.cs, peticion del 24-09-2026), en lo esencial: el ecualizador de la
// que suena, el boleto que vuela a la cola, la moneda que cae, la tarjeta que invita cuando no hay creditos y los avisos que se entienden.
// Cada una con su interruptor de XP (encendidos de fabrica); el controlador apunta la hora de cada suceso y aqui se anima.
using System;
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

        // ---- LO DE ENCIMA: la moneda que cae, la tarjeta que invita y el boleto que vuela ----
        internal const int MonedaCaeMs = 650, MonedaGolpeMs = 300, MonedaSumaMs = 1300, InvitaMs = 4200, VueloMs = 620;
        private int vueloVisto; private Zona vueloDesde; private string vueloTitulo = "";
        private void Efectos(Controlador c, float W, float H)
        {
            Form1 m = c.motor;
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
