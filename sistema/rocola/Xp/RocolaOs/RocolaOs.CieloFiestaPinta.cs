// EL CIELO VIVO DE ROCOLA OS, FIESTAS: FI2, LA FILA DEL HORIZONTE (FASE11_cielo-fiestas.md). Lo de FI5 y FI6 (el papel picado, lo que flota por el cielo
// y el cumpleanos) esta en RocolaOs.CieloFiestaAmbiente.cs.
//
// FI2 (capa TrasAurora: detras de las portadas, el texto y la interfaz, delante de la aurora y su raya): una fila de adornos con su base en la raya del
// horizonte de XP, una cada 53 px, y encima de la raya un resplandor ancho de los colores de la fecha. 27-11-2026, «que se note mas»: antes eran de 12 a 16 px;
// ahora, en el escaparate y en el reposo (donde el cielo esta libre), de unos 34 px, y en la pared (donde la ultima fila de titulos acaba a 652 con la raya a
// 665) de unos 19 px: lo mas grande que cabe sin tocar los titulos, con mas resplandor. Cada fiesta lleva su propio adorno (por su clave):
//   Muertos cempasuchil y veladoras con llama; Posadas (16 al 23-12) farolitos y velas; Navidad (el resto de diciembre), fiestas patrias y Revolucion una cadena de foquitos con su cable que cuelga y que se
//   encienden en ola; Candelaria farolitos; Ano Nuevo, Reyes y Dia del Padre estrellas que titilan; Amor y amistad corazones; las Madres y la Guadalupana (12-12)
//   rosas con hojas; Dia de la Mujer flores moradas con tallo; Dia del Nino globitos amarrados; Dia de la Bandera banderitas; Santa Cecilia notas
//   que suben (solo en el escaparate y el reposo, donde el cielo esta libre).
using System;
using System.Collections.Generic;

namespace Rocola
{
    internal sealed class RocolaOsFiestaHorizonte : RocolaOsCieloPintor
    {
        internal RocolaOsFiestaHorizonte() { Quien = "FI2 los adornos del horizonte"; Orden = RocolaOsCieloOrden.Fiestas + 10; }
        private readonly RocolaOsFiestaMalla m = new RocolaOsFiestaMalla();
        private readonly List<RocolaOsFiesta> lista = new List<RocolaOsFiesta>(4);
        private float[] gx = new float[192], gy = new float[192], gr = new float[192], gry = new float[192]; private uint[] gc = new uint[192]; private int gn;
        private double ultimoMs = -1, tm;

        // (cada cuadro, antes de abrirlo) la aurora se rehace si cambio la fecha: lo mira FI1 y se hace aqui porque este es el primer pintor del grupo
        internal override void Prepara(GpuPanel p) { RocolaOsCieloFiestas.AlCuadro(); }

        private void Glow(float x, float y, float rx, float ry, uint rgb, float a)
        { if (gn < gx.Length && a > 0.01f) { gx[gn] = x; gy[gn] = y; gr[gn] = rx; gry[gn] = ry; gc[gn] = RocolaOsFiestaMalla.Con(rgb, Math.Min(1f, a)); gn++; } }

        internal override void Pinta(GpuPanel p, int capa)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f == null || !f.FI2 || f.Hoy.Fiestas.Length == 0) return;
            lista.Clear();
            foreach (RocolaOsFiesta x in f.Hoy.Fiestas) lista.Add(x);
            GpuStarfield st = p.RocolaOsEstrellas;
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            if (st == null || !st.Ok || e.Ancho < 64 || e.Horizonte < 40f || e.Horizonte > e.Alto - 10f) return;
            float sB = Math.Max(0.8f, Math.Min(1.6f, e.Alto / 768f));
            float lib = e.Modo == 4 ? 1f : (e.Modo == 1 ? Math.Max(0f, Math.Min(1f, e.Reposo)) : 0f);
            float s = sB * (1.7f + 1.3f * lib);   // pared 19 px, escaparate y reposo 34 px
            float yb = e.Horizonte - 1f, calma = e.Calma, t = (float)(e.Ms / 1000.0);
            double dt = ultimoMs < 0 ? 0 : Math.Max(0, Math.Min(100, e.Ms - ultimoMs)); ultimoMs = e.Ms; tm += dt / 1000.0 * (0.55 + 0.45 * calma);
            float nc = RocolaOsCielo.NocheCuadro, dia = 1f - nc;
            float noche = 0.12f + 0.88f * nc;   // los resplandores (luz sumada) casi no se ven de dia (regla 3)
            m.Tono = 1f - 0.75f * dia;            // y los adornos se oscurecen de dia: sobre el cielo claro de la hora un adorno blanco pasaria de 0,17 de luz
            float luz = 0.8f + 0.2f * calma;
            m.Limpia(); gn = 0;
            float paso = 53f * sB; int n = (int)(e.Ancho / paso) + 2;
            // el resplandor ancho de la fecha sobre la raya (uno cada dos adornos, del color del adorno)
            for (int i = 0; i < n; i += 2)
            {
                RocolaOsFiesta fi = lista[i % lista.Count];
                float x = (i + 0.5f) * paso + paso * 0.5f;
                Glow(x, yb - 4f * s, paso * 1.5f, 5.5f * s, Col(fi, i / 2), (0.30f + 0.12f * lib) * noche * luz);
            }
            for (int i = 0; i < n; i++)
            {
                RocolaOsFiesta fi = lista[i % lista.Count];
                float x = (i + 0.5f) * paso + (RocolaOsCieloFiestas.Rnd(i, 7) - 0.5f) * 6f * sB;
                Item(fi, i, n, x, yb, s, sB, (float)tm, calma, noche * luz, lib, paso, e);
            }
            RocolaOsCieloFiestas.ApuntaTris(false, m.Tris, gn);
            if (m.Tris > 0) st.DrawMesh(m.V, m.Tris);
            if (gn > 0)
            {
                st.GlowBatchBegin(gn);
                for (int i = 0; i < gn; i++) st.GlowBatchAdd(gx[i], gy[i], gr[i], gry[i], gc[i]);
                st.GlowBatchEnd();
            }
        }

        private static uint Col(RocolaOsFiesta fi, int i) { return fi.Colores[Math.Abs(i) % fi.Colores.Length] & 0x00FFFFFFu; }
        private const uint Tallo = 0xFF2E8B4Au;

        private void Item(RocolaOsFiesta fi, int i, int n, float x, float yb, float s, float sB, float t, float calma, float noche, float lib, float paso, RocolaOsCieloEscena e)
        {
            float ph = RocolaOsCieloFiestas.Rnd(i, 3) * 6.2832f;
            float tw = 1f - (1f - (0.78f + 0.22f * (float)Math.Sin(t * 2.3f + ph))) * calma;   // titileo (menos con la calma)
            switch (fi.Clave)
            {
                case "muertos":
                    if ((i & 1) == 0) m.FlorPlana(x, yb - 4.6f * s, 4.5f * s, 8, (i & 2) == 0 ? 0xFF9A1Au : 0xFFC21Au, 0xB85A00, (float)(i * 0.7), 1f);
                    else Vela(x, yb, s, t, calma, ph, noche, true);
                    Glow(x, yb - 4f * s, 9f * s, 8f * s, (i & 1) == 0 ? 0xFF9A1Au : 0xFFB347u, 0.22f * noche);
                    break;
                case "posadas":
                    if ((i & 1) == 0) Farol(fi, i, x, yb, s, t, calma, ph, tw, noche);
                    else Vela(x, yb, s, t, calma, ph, noche, false);
                    break;
                case "candelaria":
                    Farol(fi, i, x, yb, s, t, calma, ph, tw, noche);
                    break;
                case "navidad":
                case "independencia":
                case "revolucion":
                    Cadena(fi, i, x, yb, s, t, calma, noche, paso, sB);
                    break;
                case "anio_nuevo":
                case "reyes":
                case "padre":
                    {
                        float ti = 0.5f + 0.5f * (float)Math.Sin(t * (1.8f + RocolaOsCieloFiestas.Rnd(i, 5)) + ph); ti *= ti;
                        float R = (2.1f + 1.7f * RocolaOsCieloFiestas.Rnd(i, 6)) * s, cy = yb - 6.5f * s - (RocolaOsCieloFiestas.Rnd(i, 8) - 0.3f) * 2.5f * s;
                        uint c = Col(fi, i);
                        m.Chispa(x, cy, R * (0.8f + 0.5f * ti), RocolaOsFiestaMalla.Con(RocolaOsFiestaMalla.Mezcla(c, 0xFFFFFF, 0.35f), 0.55f + 0.45f * ti), t * 0.2f + i);
                        Glow(x, cy, R * 3.4f, R * 3.4f, c, 0.55f * (0.3f + 0.7f * ti) * noche);
                    }
                    break;
                case "amor":
                    m.Corazon(x, yb - 6.4f * s, (5f + 2.2f * RocolaOsCieloFiestas.Rnd(i, 6)) * s * (1f + 0.06f * (float)Math.Sin(t * 3f + ph) * calma), RocolaOsFiestaMalla.Con(Col(fi, i), 0.97f));
                    break;
                case "madres":
                case "guadalupana":
                    {
                        uint c = Col(fi, i), hoja = 0x2E8B4Au;
                        m.Linea(x, yb, x, yb - 6.2f * s, Math.Max(1f, 0.5f * s), RocolaOsFiestaMalla.Con(hoja, 1f));
                        m.ElipseRot(x - 2.4f * s, yb - 2.8f * s, 2.6f * s, 1.1f * s, -0.6f, RocolaOsFiestaMalla.Con(hoja, 1f), 8, 0f);
                        m.ElipseRot(x + 2.4f * s, yb - 3.8f * s, 2.6f * s, 1.1f * s, 0.6f, RocolaOsFiestaMalla.Con(hoja, 1f), 8, 0f);
                        m.FlorPlana(x, yb - 7.8f * s, 3.6f * s, 6, c, RocolaOsFiestaMalla.Escala(c, 0.55f), (float)(i * 0.9), 1f);
                    }
                    break;
                case "mujer":
                    m.Linea(x, yb, x, yb - 6.6f * s, Math.Max(1f, 0.5f * s), RocolaOsFiestaMalla.Con(Tallo, 1f));
                    m.FlorPlana(x, yb - 7.4f * s, 4.1f * s, 5, RocolaOsFiestaMalla.Escala(Col(fi, i), 0.95f), 0xFFE066, (float)(i * 0.8), 1f);
                    break;
                case "nino":
                    {
                        float ry = 4.6f * s, rx = 3.5f * s, sw = (float)Math.Sin(t * 1.4f + ph) * 1.6f * s * calma;
                        m.Globo(x + sw, yb - ry * 1.9f - 2f * s, rx, ry, Col(fi, i), 0.97f, t * 1.3f + i, 0xEFEFEF);
                    }
                    break;
                case "bandera":
                    {
                        float bw = 9f * s, bh = 5.8f * s;
                        m.Bandera(x - bw * 0.4f, yb - 12.5f * s, bw, bh, t + i, 0.5f * s * (0.4f + 0.6f * calma), 1f, 12.6f * s, false);
                    }
                    break;
                case "santa_cecilia":
                    if ((i & 1) == 0)
                    {
                        bool libre = lib > 0.5f;
                        float dur = 8f + RocolaOsCieloFiestas.Rnd(i, 2) * 5f, u = RocolaOsCieloFiestas.Frac(t / dur + RocolaOsCieloFiestas.Rnd(i, 1));
                        float alto = libre ? 150f * sB : 3f * s;
                        float y = yb - 4f * s - u * alto, a = (float)Math.Pow(Math.Sin(u * Math.PI), 0.7) * (0.45f + 0.5f * calma);
                        float dx = (float)Math.Sin(t * 0.9f + ph) * 5f * s * calma;
                        uint c = RocolaOsFiestaMalla.Con(Col(fi, i / 2), a);
                        if ((i & 3) == 0) m.Nota(x + dx, y, 8.5f * s, c); else m.NotaDoble(x + dx, y, 8f * s, c);
                    }
                    else { m.Chispa(x, yb - 5f * s, 2f * s, RocolaOsFiestaMalla.Con(Col(fi, i), 0.5f + 0.5f * tw), t * 0.2f + i); }
                    break;
                default:
                    Cadena(fi, i, x, yb, s, t, calma, noche, paso, sB);
                    break;
            }
        }

        // un farolito de papel con su estaca (el que se mece) y su resplandor
        private void Farol(RocolaOsFiesta fi, int i, float x, float yb, float s, float t, float calma, float ph, float tw, float noche)
        {
            uint c = Col(fi, i / 2);
            float sw = (float)Math.Sin(t * 1.1f + ph) * 1.2f * s * calma;
            m.Linea(x, yb, x + sw * 0.3f, yb - 3.2f * s, Math.Max(1f, 0.4f * s), RocolaOsFiestaMalla.Con(0x4A3A2A, 0.8f));
            m.Farolito(x + sw, yb - 7.4f * s, 3.6f * s, 4.2f * s, c, 1f);
            Glow(x + sw, yb - 7.4f * s, 15f * s, 15f * s, c, 0.5f * tw * noche);
        }
        // una vela (veladora): su cuerpo, su llama que titila y su resplandor
        private void Vela(float x, float yb, float s, float t, float calma, float ph, float noche, bool morado)
        {
            float fl = 0.82f + 0.18f * (float)Math.Sin(t * 9f + ph) * calma;
            m.Rect(x - 1.7f * s, yb - 6f * s, 3.4f * s, 6f * s, 0xFFFFF3DC);
            if (morado) m.Rect(x - 1.7f * s, yb - 1.8f * s, 3.4f * s, 1.8f * s, 0xFFC9A8E0);
            m.Elipse(x, yb - 8.6f * s, 1.5f * s, 2.6f * s * fl, 0xFFFFB347, 8, 0.7f);
            m.Elipse(x, yb - 8.2f * s, 0.7f * s, 1.3f * s * fl, 0xFFFFF1A8, 6, 0f);
            Glow(x, yb - 8.6f * s, 14f * s, 14f * s, 0xFFB347u, 0.6f * fl * noche);
        }
        // la cadena de foquitos: un cable que cuelga entre dos puntos y, del cable, bombillas que se encienden en ola
        private void Cadena(RocolaOsFiesta fi, int i, float x, float yb, float s, float t, float calma, float noche, float paso, float sB)
        {
            float yw = yb - 6.5f * s, drop = 2.6f * s;
            float px = x, py = yw;
            for (int q = 1; q <= 4; q++)
            {
                float u = q / 4f, xx = x + paso * u, yy = yw + drop * 4f * u * (1f - u);
                m.Linea(px, py, xx, yy, Math.Max(1f, 0.4f * s), RocolaOsFiestaMalla.Con(0x1F3D2B, 0.85f)); px = xx; py = yy;
            }
            int bulbs = s > 2.3f ? 2 : 1;
            for (int b = 0; b < bulbs; b++)
            {
                float u = (b + 0.5f) / bulbs, bx = x + paso * u, by = yw + drop * 4f * u * (1f - u) + 2.4f * s;
                float k = 0.5f + 0.5f * (float)Math.Sin(t * 3.2f - (i * bulbs + b) * 0.8f); k = 1f - (1f - k) * (0.4f + 0.6f * calma);
                uint c = Col(fi, i * bulbs + b + (b > 0 ? 1 : 0));
                m.Rect(bx - 0.6f * s, by - 2.9f * s, 1.2f * s, 1.5f * s, RocolaOsFiestaMalla.Con(0x2A3F34, 1f));
                m.Elipse(bx, by, 1.6f * s, 2.1f * s, RocolaOsFiestaMalla.Con(RocolaOsFiestaMalla.Mezcla(c, 0xFFFFFF, 0.25f), 0.55f + 0.45f * k), 8, 0f);
                Glow(bx, by, 9f * s, 9f * s, c, 0.6f * k * noche);
            }
        }
    }
}
