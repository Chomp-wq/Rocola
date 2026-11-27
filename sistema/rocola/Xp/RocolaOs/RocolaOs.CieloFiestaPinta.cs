// EL CIELO VIVO DE ROCOLA OS, FIESTAS: LO QUE SE PINTA (FI2 los adornos del horizonte, FI5 y FI6 el escaparate). FASE11_cielo-fiestas.md.
//
// FI2 (capa TrasAurora: detras de las portadas, el texto y la interfaz, delante de la aurora y su raya): una fila baja de adornos con su base en la
// raya del horizonte de XP, de a lo mas 16 px de alto (regla 3: nada por encima de donde acaban los titulos de la pared, ni del cielo que se mide en
// el reposo): farolitos (Candelaria, posadas), flores (Dia de la Mujer, de las Madres, la Guadalupana), veladoras y cempasuchil (Muertos), luces
// (Navidad, posadas), la estrella guia y tres estrellitas (Reyes) y las notas de Santa Cecilia, que suben (solo en el escaparate y el reposo, donde
// el cielo esta libre; en las demas escenas apenas se despegan de la raya).
//
// FI5 y FI6 (capa Escaparate, SOLO con la atraccion en pantalla: Modo 4): el papel picado colgado arriba (una tira de 36 px en la orilla de arriba,
// que ni la cabecera de lo mas pedido ni el cubo tocan) con los colores de la fecha y, segun la fiesta, globos, corazones o la bandera en las dos
// franjas de las orillas (a menos de 7 % del ancho: ni el cubo, ni la ficha, ni el mensaje, ni la lista de lo mas pedido llegan ahi); con una fecha
// propia, globos y confeti en esas franjas. Nada va encima de un texto ni de una portada. Lo que se mueve lo multiplica la calma.
using System;
using System.Collections.Generic;

namespace Rocola
{
    // ======================================================================== FI2: LOS ADORNOS DEL HORIZONTE
    internal sealed class RocolaOsFiestaHorizonte : RocolaOsCieloPintor
    {
        internal RocolaOsFiestaHorizonte() { Quien = "FI2 los adornos del horizonte"; Orden = RocolaOsCieloOrden.Fiestas + 10; }
        private readonly RocolaOsFiestaMalla m = new RocolaOsFiestaMalla();
        private readonly List<RocolaOsFiesta> lista = new List<RocolaOsFiesta>(4);
        private float[] gx = new float[128], gy = new float[128], gr = new float[128]; private uint[] gc = new uint[128]; private int gn;

        // (cada cuadro, antes de abrirlo) la aurora se rehace si cambio la fecha: lo mira FI1 y se hace aqui porque este es el primer pintor del grupo
        internal override void Prepara(GpuPanel p) { RocolaOsCieloFiestas.AlCuadro(); }

        private void Glow(float x, float y, float r, uint argb) { if (gn < gx.Length && (argb >> 24) > 1) { gx[gn] = x; gy[gn] = y; gr[gn] = r; gc[gn] = argb; gn++; } }

        internal override void Pinta(GpuPanel p, int capa)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f == null || !f.FI2 || f.Hoy.Fiestas.Length == 0) return;
            lista.Clear();
            foreach (RocolaOsFiesta x in f.Hoy.Fiestas) if (x.Donde == "horizonte" || x.Donde == "aurora") lista.Add(x);
            if (lista.Count == 0) return;
            GpuStarfield st = p.RocolaOsEstrellas;
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            if (st == null || !st.Ok || e.Ancho < 64 || e.Horizonte < 40f || e.Horizonte > e.Alto - 10f) return;
            float s = Math.Max(0.8f, Math.Min(1.6f, e.Alto / 768f)) * 1.3f, yb = e.Horizonte - 1f, calma = e.Calma, t = (float)(e.Ms / 1000.0);   // (los adornos de 16 px: ver arriba)
            float noche = 0.3f + 0.7f * RocolaOsCielo.NocheCuadro;   // los resplandores (luz sumada) casi no se ven de dia
            bool libre = e.Modo == 4 || e.Reposo > 0.5f;
            m.Limpia(); gn = 0;
            float paso = 40f * s; int n = (int)(e.Ancho / paso) + 2;
            for (int i = 0; i < n; i++)
            {
                RocolaOsFiesta fi = lista[i % lista.Count];
                float x = (i + 0.5f) * paso + (RocolaOsCieloFiestas.Rnd(i, 7) - 0.5f) * 8f * s;
                Item(fi, i, x, yb, s, t, calma, noche, libre, e);
            }
            if (m.Tris > 0) st.DrawMesh(m.V, m.Tris);
            if (gn > 0)
            {
                st.GlowBatchBegin(gn);
                for (int i = 0; i < gn; i++) st.GlowBatchAdd(gx[i], gy[i], gr[i], gc[i]);
                st.GlowBatchEnd();
            }
        }

        private static uint Col(RocolaOsFiesta fi, int i) { return fi.Colores[Math.Abs(i) % fi.Colores.Length] & 0x00FFFFFFu; }
        private const uint Tallo = 0xFF2E8B4Au;

        private void Item(RocolaOsFiesta fi, int i, float x, float yb, float s, float t, float calma, float noche, bool libre, RocolaOsCieloEscena e)
        {
            float ph = RocolaOsCieloFiestas.Rnd(i, 3) * 6.2832f;
            float tw = 1f - (1f - (0.78f + 0.22f * (float)Math.Sin(t * 2.3f + ph))) * calma;   // titileo (menos con la calma)
            switch (fi.Adorno)
            {
                case "farolitos":
                case "farolitos_luces":
                    if (fi.Adorno == "farolitos_luces" && (i & 1) == 1) { Bombilla(x, yb - 4f * s, 2.1f * s, Col(fi, i / 2 + 1), i, t, calma, s, noche); break; }
                    {
                        uint c = Col(fi, i / (fi.Adorno == "farolitos" ? 1 : 2));
                        float sw = (float)Math.Sin(t * 1.1f + ph) * 1.2f * s * calma;
                        m.Linea(x, yb, x + sw * 0.3f, yb - 3.2f * s, 1f, RocolaOsFiestaMalla.Con(0x4A3A2A, 0.75f));
                        m.Farolito(x + sw, yb - 7f * s, 3.4f * s, 4f * s, c, 1f);
                        Glow(x + sw, yb - 7f * s, 15f * s, RocolaOsFiestaMalla.Con(c, 0.34f * tw * noche));
                    }
                    break;
                case "flores_moradas":
                    m.Flor(x, yb - 4.8f * s, 3.7f * s, 5, RocolaOsFiestaMalla.Escala(Col(fi, i), 0.9f), 0xFFE066, yb, Tallo); break;
                case "flores":
                    m.Flor(x, yb - 4.8f * s, 3.7f * s, 5, RocolaOsFiestaMalla.Escala(Col(fi, i), 0.9f), 0xFFD84A, yb, Tallo); break;
                case "rosas":
                    m.Flor(x, yb - 5f * s, 4.1f * s, 6, RocolaOsFiestaMalla.Escala(Col(fi, i), 0.9f), 0xC2185B, yb, Tallo);
                    break;
                case "veladoras_flores":
                    if ((i & 1) == 0)
                        m.Flor(x, yb - 6.6f * s, 4.7f * s, 8, (i & 2) == 0 ? 0xFF9A1Au : 0xFFC21Au, 0xB85A00, yb, Tallo);
                    else
                    {
                        float fl = 0.82f + 0.18f * (float)Math.Sin(t * 9f + ph) * calma;
                        m.Rect(x - 1.7f * s, yb - 6f * s, 3.4f * s, 6f * s, 0xFFFFF3DC);
                        m.Elipse(x, yb - 8.4f * s, 1.5f * s, 2.5f * s * fl, 0xFFFFB347, 8, 0.7f);
                        Glow(x, yb - 8.6f * s, 13f * s, RocolaOsFiestaMalla.Con(0xFFB347, 0.5f * fl * noche));
                    }
                    break;
                case "luces":
                    Luces(fi, i, x, yb, s, t, calma, noche);
                    break;
                case "estrella_guia":
                    Reyes(fi, i, x, yb, s, t, calma, tw, noche, e);
                    break;
                case "notas":
                    if ((i & 1) == 0)
                    {
                        float dur = 8f + RocolaOsCieloFiestas.Rnd(i, 2) * 5f, u = RocolaOsCieloFiestas.Frac(t / dur * (0.5f + 0.5f * calma) + RocolaOsCieloFiestas.Rnd(i, 1));
                        float alto = libre ? 115f * s : 3f * s;
                        float y = yb - 4f * s - u * alto, a = (float)Math.Pow(Math.Sin(u * Math.PI), 0.7) * (0.35f + 0.55f * calma);
                        float dx = (float)Math.Sin(t * 0.9f + ph) * 4f * s * calma;
                        m.Nota(x + dx, y, 12f * s, RocolaOsFiestaMalla.Con(Col(fi, i / 2), a));
                    }
                    break;
            }
        }

        // una bombilla de una guirnalda (y su halo)
        private void Bombilla(float x, float y, float r, uint rgb, int i, float t, float calma, float s, float noche)
        {
            float k = 0.55f + 0.45f * (float)Math.Sin(t * 2.6f - i * 0.8f);
            k = 1f - (1f - k) * calma;
            m.Disco(x, y, r, RocolaOsFiestaMalla.Con(RocolaOsFiestaMalla.Mezcla(rgb, 0xFFFFFF, 0.2f), 0.55f + 0.45f * k));
            Glow(x, y, 9f * s, RocolaOsFiestaMalla.Con(rgb, 0.5f * k * noche));
        }
        // Navidad y posadas: el cable cuelga en tramos de 6 y de el, las bombillas
        private void Luces(RocolaOsFiesta fi, int i, float x, float yb, float s, float t, float calma, float noche)
        {
            const int tramo = 6;
            float u0 = (i % tramo) / (float)tramo, u1 = ((i % tramo) + 1) / (float)tramo, paso = 40f * s;
            float y0 = yb - 9f * s + 3.6f * s * 4f * u0 * (1f - u0), y1 = yb - 9f * s + 3.6f * s * 4f * u1 * (1f - u1);
            m.Linea(x, y0, x + paso, y1, 1f, RocolaOsFiestaMalla.Con(0x1F3D2B, 0.8f));
            Bombilla(x, y0 + 2.4f * s, 2.3f * s, Col(fi, i), i, t, calma, s, noche);
        }
        // Reyes: la estrella guia (grande, con cruz y halo) y tres estrellitas en la fila
        private void Reyes(RocolaOsFiesta fi, int i, float x, float yb, float s, float t, float calma, float tw, float noche, RocolaOsCieloEscena e)
        {
            if (i == (int)(e.Ancho * 0.68f / (40f * s)))
            {
                float cy = yb - 7f * s, R = 6.2f * s, rot = (float)Math.Sin(t * 0.6f) * 0.08f * calma;
                m.Estrella(x, cy, R, R * 0.42f, 5, rot, RocolaOsFiestaMalla.Con(0xFFE9A8, 1f));
                m.Linea(x - 13f * s, cy, x + 13f * s, cy, 1f, RocolaOsFiestaMalla.Con(0xFFE9A8, 0.55f * tw));
                m.Linea(x, cy - 5f * s, x, cy + 5f * s, 1f, RocolaOsFiestaMalla.Con(0xFFE9A8, 0.4f * tw));
                Glow(x, cy, 26f * s, RocolaOsFiestaMalla.Con(0xFFC83C, 0.5f * tw * noche));
            }
            else if (i % 4 == 1)
            {
                float cy = yb - 5f * s;
                m.Estrella(x, cy, 3.4f * s, 1.5f * s, 4, 0.78f, RocolaOsFiestaMalla.Con(Col(fi, i / 4), 0.5f + 0.5f * tw));
                Glow(x, cy, 9f * s, RocolaOsFiestaMalla.Con(Col(fi, i / 4), 0.35f * tw * noche));
            }
        }
    }

    // ======================================================================== FI5 y FI6: EL ESCAPARATE
    internal sealed class RocolaOsFiestaEscaparate : RocolaOsCieloPintor
    {
        internal RocolaOsFiestaEscaparate() { Quien = "FI5 y FI6 el escaparate"; Orden = RocolaOsCieloOrden.Fiestas + 20; }
        private readonly RocolaOsFiestaMalla m = new RocolaOsFiestaMalla();
        private double entraMs = -1;

        internal override void Pinta(GpuPanel p, int capa)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            if (f == null || e.Modo != 4) { entraMs = -1; return; }   // solo en la atraccion (el escaparate)
            bool cal = f.FI5 && f.Hoy.Fiestas.Length > 0, prop = RocolaOsCieloFiestas.HayPropias(f);
            if (!cal && !prop) return;
            GpuStarfield st = p.RocolaOsEstrellas;
            if (st == null || !st.Ok || e.Ancho < 64) return;
            if (entraMs < 0) entraMs = e.Ms;
            float k = (float)Math.Max(0, Math.Min(1, (e.Ms - entraMs) / 1200.0)); k = k * k * (3f - 2f * k);   // entra en 1,2 s
            float s = Math.Max(0.8f, Math.Min(1.6f, e.Alto / 768f)), t = (float)(e.Ms / 1000.0), calma = e.Calma, w = e.Ancho, h = e.Alto;
            m.Limpia();
            if (cal)
            {
                PapelPicado(RocolaOsCieloFiestas.Colores(f), w, s, t, calma, k);
                foreach (RocolaOsFiesta fi in f.Hoy.Fiestas)
                    switch (fi.Adorno)
                    {
                        case "globos": Globos(fi.Colores, w, h, s, t, calma, k, 0); break;
                        case "corazones": Corazones(fi.Colores, w, h, s, t, calma, k); break;
                        case "bandera": Bandera(w, h, s, t, calma, k); break;
                    }
            }
            if (prop) { Globos(RocolaOsCieloFiestas.Fiesta, w, h, s, t, calma, k, 5); Confeti(RocolaOsCieloFiestas.Fiesta, w, h, s, t, calma, k); }
            if (m.Tris > 0) st.DrawMesh(m.V, m.Tris);
        }

        // las dos franjas de las orillas (centro, a un 3,3 % del ancho de cada lado): ahi no llega nada de XP en el escaparate
        private static float CentroIzq(float w) { return w * 0.033f; }
        private static float CentroDer(float w) { return w * 0.967f; }

        // el papel picado: dos tramos de cuerda con banderines de los colores de la fecha, que se mecen; todo en los 36 px de arriba
        private void PapelPicado(uint[] col, float w, float s, float t, float calma, float k)
        {
            float pitch = 33f * s, bw = 25f * s, bh = 24f * s, y0 = 2f * s, sag = 5f * s, tramo = pitch * 6f;
            int n = (int)(w / pitch) + 1;
            float xa = 0f, ya = y0;
            uint cuerda = RocolaOsFiestaMalla.Con(0xE8D8B8, 0.55f * k);
            for (int i = 0; i < n; i++)
            {
                float x = (i + 0.5f) * pitch, u = RocolaOsCieloFiestas.Frac(x / tramo), ys = y0 + sag * 4f * u * (1f - u);
                m.Linea(xa, ya, x, ys, 1.1f * Math.Min(1.3f, s), cuerda);
                float sw = (float)Math.Sin(t * 1.4f + i * 0.7f) * 1.7f * s * calma;
                m.Banderin(x, ys, bw, bh, col[i % col.Length] & 0x00FFFFFFu, 0.9f * k, sw);
                xa = x; ya = ys;
            }
        }
        // globos que suben por las dos franjas
        private void Globos(uint[] col, float w, float h, float s, float t, float calma, float k, int semilla)
        {
            for (int lado = 0; lado < 2; lado++)
                for (int j = 0; j < 3; j++)
                {
                    int id = semilla * 10 + lado * 3 + j;
                    float dur = 17f + RocolaOsCieloFiestas.Rnd(id, 1) * 7f, u = RocolaOsCieloFiestas.Frac(t / dur * (0.6f + 0.4f * calma) + (j + 0.5f * lado) / 3f + RocolaOsCieloFiestas.Rnd(id, 4) * 0.2f);
                    float ry = 13f * s, y = h + ry * 3f - u * (h + ry * 6f);
                    float cx = (lado == 0 ? CentroIzq(w) : CentroDer(w)) + (float)Math.Sin(t * 0.8f + id * 1.9f) * 7f * s * calma;
                    m.Globo(cx, y, 10.5f * s, ry, col[(id) % col.Length] & 0x00FFFFFFu, 0.93f * k, t * 1.3f + id, 0xEFEFEF);
                }
        }
        private void Corazones(uint[] col, float w, float h, float s, float t, float calma, float k)
        {
            for (int lado = 0; lado < 2; lado++)
                for (int j = 0; j < 4; j++)
                {
                    int id = 40 + lado * 4 + j;
                    float dur = 11f + RocolaOsCieloFiestas.Rnd(id, 1) * 6f, u = RocolaOsCieloFiestas.Frac(t / dur * (0.6f + 0.4f * calma) + j / 4f + 0.13f * lado);
                    float y = h + 20f * s - u * (h + 40f * s), a = (float)Math.Min(1.0, Math.Min(u * 8f, (1f - u) * 8f)) * 0.92f * k;
                    float cx = (lado == 0 ? CentroIzq(w) : CentroDer(w)) + (float)Math.Sin(t * 1.1f + id * 2.3f) * 9f * s * calma;
                    float tam = (13f + 6f * RocolaOsCieloFiestas.Rnd(id, 2)) * s;
                    m.Corazon(cx, y, tam, RocolaOsFiestaMalla.Con(col[(id) % col.Length], a));
                }
        }
        // la bandera de Mexico en un asta de la franja de la izquierda (verde, blanco y rojo, con el escudo): ondea
        private void Bandera(float w, float h, float s, float t, float calma, float k)
        {
            float px = w * 0.016f, y0 = h * 0.16f, bw = 42f * s, bh = 27f * s;
            uint asta = RocolaOsFiestaMalla.Con(0xCFCFCF, 0.9f * k);
            m.Rect(px - 1f, y0 - 3f * s, 2.2f, h * 0.36f, asta);
            m.Disco(px, y0 - 4f * s, 2.2f * s, RocolaOsFiestaMalla.Con(0xE8C860, k));
            const int tiras = 14;
            uint[] c3 = { 0xFF00A05Au, 0xFFF4F4F4u, 0xFFD8202Eu };
            for (int j = 0; j < tiras; j++)
            {
                float x0 = px + 2f + bw * j / tiras, x1 = px + 2f + bw * (j + 1) / tiras;
                float dy0 = (float)Math.Sin(t * 3.2f - j * 0.62f) * 2.6f * s * calma * (j / (float)tiras), dy1 = (float)Math.Sin(t * 3.2f - (j + 1) * 0.62f) * 2.6f * s * calma * ((j + 1) / (float)tiras);
                uint c = c3[Math.Min(2, j * 3 / tiras)] & 0x00FFFFFFu;
                float sombra = 0.9f + 0.12f * (float)Math.Cos(t * 3.2f - j * 0.62f) * (calma * 0.6f + 0.4f);
                uint cc = RocolaOsFiestaMalla.Con(RocolaOsFiestaMalla.Escala(c, sombra), k);
                m.Cuadro4(x0, y0 + dy0, x1, y0 + dy1, x1, y0 + bh + dy1, x0, y0 + bh + dy0, cc);
            }
            float ex = px + 2f + bw * 0.5f, ey = y0 + bh * 0.5f + (float)Math.Sin(t * 3.2f - 7 * 0.62f) * 2.6f * s * calma * 0.5f;
            m.Disco(ex, ey, 3.8f * s, RocolaOsFiestaMalla.Con(0x8B5A2B, 0.9f * k));
            m.Disco(ex, ey, 1.9f * s, RocolaOsFiestaMalla.Con(0xD4A017, 0.9f * k));
        }
        // confeti que cae por las dos franjas (rectangulos que giran)
        private void Confeti(uint[] col, float w, float h, float s, float t, float calma, float k)
        {
            for (int lado = 0; lado < 2; lado++)
                for (int j = 0; j < 14; j++)
                {
                    int id = 80 + lado * 14 + j;
                    float dur = 6f + RocolaOsCieloFiestas.Rnd(id, 1) * 4f, u = RocolaOsCieloFiestas.Frac(t / dur * (0.6f + 0.4f * calma) + RocolaOsCieloFiestas.Rnd(id, 2));
                    float y = -8f * s + u * (h + 16f * s);
                    float cx = (lado == 0 ? CentroIzq(w) : CentroDer(w)) + (RocolaOsCieloFiestas.Rnd(id, 3) - 0.5f) * 40f * s + (float)Math.Sin(t * 1.6f + id) * 6f * s * calma;
                    float ang = t * (1.5f + RocolaOsCieloFiestas.Rnd(id, 5) * 3f) + id, ca = (float)Math.Cos(ang), sa = (float)Math.Sin(ang);
                    float a = 3.2f * s, b = 1.9f * s * Math.Abs((float)Math.Cos(ang * 0.7f)) + 0.5f * s;
                    uint c = RocolaOsFiestaMalla.Con(col[id % col.Length], 0.95f * k);
                    m.Cuadro4(cx - a * ca + b * sa, y - a * sa - b * ca, cx + a * ca + b * sa, y + a * sa - b * ca, cx + a * ca - b * sa, y + a * sa + b * ca, cx - a * ca - b * sa, y - a * sa + b * ca, c);
                }
        }
    }
}
