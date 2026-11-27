// EL CIELO VIVO DE ROCOLA OS, FIESTAS: EL AMBIENTE DE LA FECHA (FI5 y FI6; 27-11-2026, «las decoraciones se me hacen muy sutiles, quiero que se note mas»;
// FASE11_cielo-fiestas.md, seccion «Que se note mas»). Un pintor de la capa TrasFondo: DETRAS de las portadas, del cubo, de la ficha y de todo texto de XP
// (antes el papel picado y los globos se pintaban encima, en la capa del escaparate, y por eso tenian que quedarse en 34 px de arriba y en las orillas):
//   - el PAPEL PICADO: banderines de 58 x 64 px con su calado, colgados en cuerdas que caen en curva y se mecen (Navidad: un feston de foquitos);
//   - los ELEMENTOS DE CADA FIESTA (confeti y serpentinas, la estrella guia y coronas, farolitos, corazones, la bandera, flores, globos, petalos, banderitas,
//     cempasuchil, veladoras y calaveritas, notas, rosas, pinatas, esferas, nochebuenas y nieve) que flotan, suben o caen por TODO el cielo libre.
// Donde se pinta (la escena de cada cuadro):
//   - la atraccion (el escaparate): todo, con el papel picado arriba y los elementos por todo el cielo; donde hay texto (la columna de la ficha y su
//     mensaje; o el «64 canciones» de abajo del cubo; la pagina de lo mas pedido mientras entra) el alfa baja a cero con un paso suave, y el papel picado se
//     recoge hacia arriba y se apaga mientras la cabecera de lo mas pedido esta a la vista;
//   - el reposo con musica: todo menos lo que cae por la columna de la letra de la cancion (alfa a un tercio) y lo que pase de la raya del horizonte;
//   - la pared: solo los elementos, a un 42 % de alfa, sin pasar de arriba de la raya, y apagados donde estan los controles de arriba y los titulos de la
//     ultima fila; detras de las portadas, asi que asoman por el cielo libre;
//   - el buscador y el disco abierto: nada (todo es texto).
// Todo lo que se mueve va por un reloj propio que avanza con la calma (Escena.Calma: 0,35 mientras alguien elige -> menos movimiento, un poco menos de luz):
// asi un cambio de calma no teletransporta nada. Ningun cuadro reserva memoria: arreglos fijos.
using System;
using System.Collections.Generic;

namespace Rocola
{
    internal sealed class RocolaOsFiestaAmbiente : RocolaOsCieloPintor
    {
        internal RocolaOsFiestaAmbiente() { Quien = "FI5 y FI6 el ambiente de la fecha"; Orden = RocolaOsCieloOrden.Fiestas + 5; }
        private readonly RocolaOsFiestaMalla m = new RocolaOsFiestaMalla();
        private double entraMs = -1, ultimoMs = -1, tm;
        // el cuadro
        private float W, H, S, T, calma, kAlfa, lib, amb, yLim;
        private bool escap; private uint[] colgCol = new uint[] { 0xFFFFC83Cu };
        // las zonas con texto (se apagan): x0, y0, x1, y1 y cuanto apagan (0 a 1)
        private readonly float[] ex0 = new float[6], ey0 = new float[6], ex1 = new float[6], ey1 = new float[6], epeso = new float[6]; private int nex;
        // los resplandores (luz sumada)
        private readonly float[] gx = new float[160], gy = new float[160], gr = new float[160]; private readonly uint[] gc = new uint[160]; private int gn;
        private uint[] palMuertos = { 0xFFFF9A1Au, 0xFFFFB521u, 0xFFFFC21Au }, palBandera = { 0xFF00A05Au, 0xFFF4F4F4u, 0xFFD8202Eu }, palNavidad = { 0xFFE03C3Cu, 0xFF22A05Au, 0xFFFFC83Cu, 0xFFF4F4F4u };
        private uint[] palRosas = { 0xFFFF7AB8u, 0xFFFF9EC8u, 0xFFFFC4DCu, 0xFFFF5C9Eu }, palMoradas = { 0xFF8E44C8u, 0xFFC890F0u, 0xFFA95BDFu, 0xFFD9B2F5u };
        private uint[] palPinata = { 0xFFFF8A2Au, 0xFF22B05Au, 0xFFE03C6Eu, 0xFF3C78FFu, 0xFFFFC83Cu, 0xFFB06CFFu, 0xFF3CC8FFu };
        private uint[] palAzulOro = { 0xFF3C78FFu, 0xFFFFC83Cu, 0xFF6CA0FFu, 0xFFFFE08Au };

        private static float Suave(float x) { if (x < 0f) x = 0f; else if (x > 1f) x = 1f; return x * x * (3f - 2f * x); }
        private static float Frac(double x) { return (float)(x - Math.Floor(x)); }
        private static float Rnd(int a, int b) { return RocolaOsCieloFiestas.Rnd(a, b); }

        // ------------------------------------------------------------------ el cuadro
        internal override void Pinta(GpuPanel p, int capa)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            if (f == null || (e.Modo != 4 && e.Modo != 1) || e.Ancho < 64) { entraMs = -1; ultimoMs = -1; return; }
            bool cal = f.FI5 && f.Hoy.Fiestas.Length > 0, prop = RocolaOsCieloFiestas.HayPropias(f);
            if (!cal && !prop) return;
            GpuStarfield st = p.RocolaOsEstrellas;
            if (st == null || !st.Ok) return;
            escap = e.Modo == 4;
            lib = escap ? 1f : Math.Max(0f, Math.Min(1f, e.Reposo));
                        if (entraMs < 0) entraMs = e.Ms;
            float k = (float)Math.Max(0, Math.Min(1, (e.Ms - entraMs) / 1200.0)); k = k * k * (3f - 2f * k);   // entra en 1,2 s
            W = e.Ancho; H = e.Alto; S = Math.Max(0.8f, Math.Min(1.6f, H / 768f)); T = (float)(e.Ms / 1000.0); calma = e.Calma;
            double dt = ultimoMs < 0 ? 0 : Math.Max(0, Math.Min(100, e.Ms - ultimoMs)); ultimoMs = e.Ms;
            tm += dt / 1000.0 * (0.55 + 0.45 * calma);
            kAlfa = k * (0.65f + 0.35f * calma);
            amb = 0.42f + 0.58f * lib;
            yLim = escap ? e.Horizonte : e.Horizonte - 6f;
            Zonas(p, e);
            m.Limpia(); gn = 0;
            if (cal)
            {
                int nf = f.Hoy.Fiestas.Length; float cuota = (nf > 1 ? 0.62f : 1f) * (escap ? 1.1f : 0.5f + 0.3f * lib);   // (lo que flota: 40 en el escaparate, 34 en el reposo, 22 en la pared)
                PapelPicado(f, e, p);
                foreach (RocolaOsFiesta fi in f.Hoy.Fiestas) Fiesta(fi, cuota);
            }
            if (prop) Propias(f);
            RocolaOsCieloFiestas.ApuntaTris(true, m.Tris, gn);
            if (m.Tris > 0) st.DrawMesh(m.V, m.Tris);
            if (gn > 0)
            {
                st.GlowBatchBegin(gn);
                for (int i = 0; i < gn; i++) st.GlowBatchAdd(gx[i], gy[i], gr[i], gc[i]);
                st.GlowBatchEnd();
            }
        }

        // DONDE HAY TEXTO en esta escena (para que nada de lo que flota llegue detras de un texto): las zonas de arriba
        private void Zonas(GpuPanel p, RocolaOsCieloEscena e)
        {
            nex = 0;
            if (escap)
            {
                bool vit, lista; float ficha;
                bool hay = p.RocolaOsEscaparate(out vit, out lista, out ficha);
                if (!hay) { vit = false; lista = false; ficha = 1f; }
                if (vit)
                {
                    System.Drawing.RectangleF col = Form1.ColumnaEscaparate((int)W, (int)H);
                    Zona(col.X - 22f, col.Y - 14f, col.Right + 22f, col.Bottom + 14f, 1f);
                }
                else
                {
                    System.Drawing.RectangleF cu = new System.Drawing.RectangleF(W / 2f - (int)(Math.Min(W, H) * 0.40) / 2f, (H - (int)(Math.Min(W, H) * 0.40)) / 2f - 30f, (int)(Math.Min(W, H) * 0.40), (int)(Math.Min(W, H) * 0.40));
                    Zona(W * 0.27f, cu.Bottom + 6f, W * 0.73f, cu.Bottom + 96f * S, 1f);
                }
                if (lista)
                {
                    System.Drawing.RectangleF pag; float cw, ch, fy, fh;
                    Form1.GeometriaTop((int)W, (int)H, out pag, out cw, out ch, out fy, out fh);
                    Zona(pag.X - 12f, pag.Y - 12f, pag.Right + 12f, pag.Bottom + 12f, 1f - ficha);
                }
            }
            else
            {
                float q = 1f - lib;   // la pared: los controles de arriba y los titulos de la ultima fila
                if (q > 0.01f) { Zona(0f, 0f, W, H * 0.135f, q); Zona(0f, e.Horizonte - 78f * S, W, e.Horizonte + 20f, 0.7f * q); }
                if (lib > 0.01f && RocolaOsLetraSuena.Actual != null) Zona(W * 0.20f, H * 0.12f, W * 0.97f, H * 0.66f, 0.85f * lib);   // la columna de la letra del reposo, estilo Apple Music (RocolaOs.LetraCielo.cs: x 224-978, y 108-488 en 1024x768), solo si hay letra: lo que flota se aparta
            }
        }
        private void Zona(float x0, float y0, float x1, float y1, float peso)
        { if (nex < ex0.Length && peso > 0.005f) { ex0[nex] = x0; ey0[nex] = y0; ex1[nex] = x1; ey1[nex] = y1; epeso[nex] = peso; nex++; } }

        // cuanto se ve algo en (x, y): 1 en el cielo libre, 0 sobre un texto, con un paso suave de 'fe' px
        private float Z(float x, float y, float fe)
        {
            float z = 1f;
            for (int i = 0; i < nex; i++)
            {
                float dx = Math.Max(Math.Max(ex0[i] - x, 0f), x - ex1[i]), dy = Math.Max(Math.Max(ey0[i] - y, 0f), y - ey1[i]);
                float d = (float)Math.Sqrt(dx * dx + dy * dy), a = Suave(d / fe);
                z *= 1f - epeso[i] * (1f - a);
            }
            // la raya del horizonte (la franja de abajo es de textos)
            float lim = Suave((yLim - y) / (26f * S));
            return z * lim;
        }
        private float Al(float x, float y, float fe) { return kAlfa * amb * Z(x, y, fe); }

        private void Glow(float x, float y, float r, uint rgb, float a) { if (gn < gx.Length && a > 0.01f) { gx[gn] = x; gy[gn] = y; gr[gn] = r; gc[gn] = RocolaOsFiestaMalla.Con(rgb, Math.Min(1f, a)); gn++; } }
        private static uint Pal(uint[] c, int i) { return c[Math.Abs(i) % c.Length] & 0x00FFFFFFu; }
        private float Mueve { get { return 0.35f + 0.65f * calma; } }   // cuanto se mecen las cosas (la calma las aquieta)

        // ------------------------------------------------------------------ el papel picado
        // dos guirnaldas que cuelgan en curva (de -10 a la mitad y de la mitad al otro borde, 46 px de caida), con banderines de 44 x 54 px con su calado
        // y, cada cuatro banderines, un colgante de la fiesta (esferas en Navidad, pinatas en Posadas, farolitos en Candelaria)
        private void PapelPicado(RocolaOsCieloFoto f, RocolaOsCieloEscena e, GpuPanel p)
        {
            if (lib < 0.02f) return;   // (la pared: arriba van los controles)
            uint[] col = RocolaOsCieloFiestas.Colores(f);
            if (col.Length == 0) return;
            colgCol = col; string clave = f.Hoy.Fiestas[0].Clave; int colg = 0;
            foreach (RocolaOsFiesta x in f.Hoy.Fiestas) { if (x.Clave == "navidad") { colg = 1; clave = x.Clave; } else if (x.Clave == "posadas" && colg != 1) { colg = 2; clave = x.Clave; } else if (x.Clave == "candelaria") { colg = 3; clave = x.Clave; } }
            int calado = 0;   // 1 calavera (Muertos), 2 flor de petalos (Amor, Mujer, Madres, Guadalupana)
            foreach (RocolaOsFiesta x in f.Hoy.Fiestas) { if (x.Clave == "muertos") calado = 1; else if (x.Clave == "amor" || x.Clave == "mujer" || x.Clave == "madres" || x.Clave == "guadalupana") calado = 2; }
            // recogido mientras la lista de lo mas pedido (su cabecera empieza a 38 px) esta a la vista: sube y se apaga
            float vis = 1f;
            if (escap) { bool vit, lista; float ficha; if (p.RocolaOsEscaparate(out vit, out lista, out ficha) && lista) vis = Suave(Math.Max(0f, Math.Min(1f, ficha))); }
            if (vis < 0.01f) return;
            float pk = escap ? 1f : 0.84f, wk = W / 1024f;
            float bw = 44f * S * pk, bh = 54f * S * pk, sag = 46f * S * pk, pitch = 47f * S * pk;
            float y0 = -2f * S - (1f - vis) * (bh + sag + 14f * S);
            float a = kAlfa * Suave(lib) * vis;
            uint cuerda = RocolaOsFiestaMalla.Con(0xE8D8B8, 0.75f * a);
            for (int g = 0; g < 2; g++)
            {
                float xs = g == 0 ? -10f * wk : W * 0.5f, span = W * 0.5f + 10f * wk;
                int n = Math.Max(4, (int)(span / pitch));
                float xa = xs, ya = y0;
                for (int i = 0; i <= n; i++)
                {
                    float u = (i + 0.5f) / n; if (i == n) u = 1f;
                    float x = xs + span * u, ys = y0 + sag * 4f * u * (1f - u);
                    m.Linea(xa, ya, x, ys, 1.5f * Math.Min(1.3f, S), cuerda);
                    xa = x; ya = ys;
                    if (i == n) break;
                    int id = g * 40 + i;
                    float sw = (float)Math.Sin(tm * 1.5 + id * 0.7) * 3.2f * S * Mueve;
                    float al = a * Z(x, ys + bh * 0.5f, 8f * S);
                    if (al > 0.01f) m.Banderin(x, ys, bw, bh, Pal(col, id), 0.96f * al, sw, calado == 1 ? ((id & 1) == 0 ? 1 : 0) : (calado == 2 && (id & 1) == 1 ? 2 : 0));
                    if (colg != 0 && (id & 3) == 2) Colgante(colg, x, ys + 2f * S, id, a, (float)Math.Sin(tm * 1.1 + id * 1.9) * 0.12f * Mueve);
                }
            }
        }
        // lo que cuelga de la guirnalda con su hilo: esferas (1), pinatas (2), farolitos (3)
        private void Colgante(int clase, float x0, float y0, int id, float a, float ang)
        {
            float largo = (clase == 2 ? 62f : 46f + 30f * Rnd(id, 7)) * S, r = (clase == 2 ? 24f : clase == 1 ? 15f : 13f) * S;
            float x = x0 + largo * (float)Math.Sin(ang), y = y0 + largo * (float)Math.Cos(ang);
            float al = a * Z(x, y, 10f * S); if (al < 0.01f) return;
            m.Linea(x0, y0, x, y - r * 1.15f, 1.2f * S, RocolaOsFiestaMalla.Con(0xD8D0B8, 0.8f * al));
            float nc = 0.4f + 0.6f * RocolaOsCielo.NocheCuadro;
            if (clase == 1) { uint c = Pal(palNavidad, id >> 2); m.Esfera(x, y, r, c, 0.97f * al); Glow(x, y, r * 2.4f, c, 0.25f * al * nc); }
            else if (clase == 2) m.Pinata(x, y, r, palPinata, ang * 1.5f + (float)tm * 0.15f, 0.97f * al);
            else { m.Farolito(x, y, r * 0.85f, r * 1.1f, Pal(colgCol, id >> 2), 0.97f * al); Glow(x, y, r * 2.4f, Pal(colgCol, id >> 2), 0.35f * al * nc); }
        }

        // ------------------------------------------------------------------ las fiestas
        private static int Cuenta(int n, float cuota) { return Math.Max(1, (int)(n * cuota + 0.5f)); }
        private void Fiesta(RocolaOsFiesta fi, float q)
        {
            switch (fi.Clave)
            {
                case "anio_nuevo": Confeti(fi.Colores, Cuenta(30, q), 100, 6.6f); Serpentinas(fi.Colores, Cuenta(7, q), 200); Chispas(fi.Colores, Cuenta(12, q), 300, 11f); break;
                case "reyes": Corona(fi.Colores, Cuenta(5, q), 400, 40f); Chispas(fi.Colores, Cuenta(14, q), 500, 10f); EstrellaGuia(); break;
                case "candelaria": Subida(0, fi.Colores, Cuenta(12, q), 600, 1f, fi.Colores); Chispas(fi.Colores, Cuenta(6, q), 650, 8f); break;
                case "amor": Subida(1, fi.Colores, Cuenta(16, q), 700, 1f, fi.Colores); Petalos(fi.Colores, Cuenta(10, q), 750, 9f); break;
                case "bandera": BanderaGrande(); Banderitas(Cuenta(9, q), 800); Confeti(palBandera, Cuenta(16, q), 850, 6.4f); break;
                case "mujer": Flores(palMoradas, Cuenta(13, q), 900, 22f, false); Petalos(palMoradas, Cuenta(12, q), 950, 10f); break;
                case "nino": Subida(2, fi.Colores, Cuenta(13, q), 1000, 1f, fi.Colores); Confeti(fi.Colores, Cuenta(28, q), 1050, 6.4f); Serpentinas(fi.Colores, Cuenta(4, q), 1100); break;
                case "madres": Petalos(palRosas, Cuenta(30, q), 1150, 13f); Flores(palRosas, Cuenta(5, q), 1200, 20f, true); break;
                case "padre": Subida(2, palAzulOro, Cuenta(13, q), 1250, 1f, palAzulOro); Chispas(fi.Colores, Cuenta(9, q), 1300, 10f); Confeti(palAzulOro, Cuenta(14, q), 1350, 6.2f); break;
                case "independencia": Banderitas(Cuenta(10, q), 1400); Confeti(palBandera, Cuenta(32, q), 1450, 6.6f); Serpentinas(palBandera, Cuenta(5, q), 1500); break;
                case "muertos": Petalos(palMuertos, Cuenta(36, q), 1550, 13f); Veladoras(Cuenta(8, q), 1600); Calaveras(Cuenta(4, q), 1650); Flores(palMuertos, Cuenta(4, q), 1700, 17f, false); break;
                case "revolucion": Banderitas(Cuenta(10, q), 1750); Confeti(palBandera, Cuenta(24, q), 1800, 6.6f); break;
                case "santa_cecilia": Notas(fi.Colores, Cuenta(14, q), 1850); Chispas(fi.Colores, Cuenta(9, q), 1900, 9f); break;
                case "guadalupana": Rosas(palRosas, Cuenta(10, q), 1950); Petalos(new uint[] { 0xFF6CA0FFu, 0xFFFF9EC8u, 0xFFFFFFFFu }, Cuenta(18, q), 2000, 11f); Chispas(new uint[] { 0xFFFFE08Au }, Cuenta(9, q), 2050, 10f); break;
                case "posadas": Subida(0, fi.Colores, Cuenta(9, q), 2150, 1f, fi.Colores); Chispas(fi.Colores, Cuenta(9, q), 2200, 10f); Confeti(fi.Colores, Cuenta(14, q), 2250, 6.2f); break;
                case "navidad": Nochebuenas(Cuenta(6, q), 2350); Nieve(Cuenta(60, q), 2400, false); Nieve(Cuenta(26, q), 2500, true); break;
                default: Confeti(fi.Colores.Length > 0 ? fi.Colores : RocolaOsCieloFiestas.Fiesta, Cuenta(24, q), 2600, 6.4f); Chispas(fi.Colores, Cuenta(8, q), 2650, 9f); break;
            }
        }
        // un cumpleanos o aniversario propio (FI6): globos, confeti y serpentinas de feria
        private void Propias(RocolaOsCieloFoto f)
        {
            uint[] pal = RocolaOsCieloFiestas.Fiesta;
            Subida(2, pal, 12, 3000, 1f, pal); Confeti(pal, 28, 3100, 6.6f); Serpentinas(pal, 6, 3200);
        }

        // ------------------------------------------------------------------ lo que cae
        private void Confeti(uint[] pal, int n, int bas, float tam)
        {
            for (int j = 0; j < n; j++)
            {
                int id = bas + j; float dur = 11f + Rnd(id, 1) * 8f, u = Frac(tm / dur + Rnd(id, 2));
                float y = -16f * S + u * (H + 32f * S);
                float x = ((j + Rnd(id, 3) * 0.9f) / n) * W + (float)Math.Sin(tm * 1.3 + id) * 16f * S * Mueve;
                float al = Al(x, y, 30f * S) * Math.Min(1f, Math.Min(u * 10f, (1f - u) * 10f)); if (al < 0.01f) continue;
                m.Confeti(x, y, tam * S * (0.8f + 0.5f * Rnd(id, 4)), (float)tm * (1.5f + Rnd(id, 5) * 3f) + id, (float)tm * (2f + Rnd(id, 6) * 2f) + id, RocolaOsFiestaMalla.Con(Pal(pal, id), 0.95f * al));
            }
        }
        private void Serpentinas(uint[] pal, int n, int bas)
        {
            for (int j = 0; j < n; j++)
            {
                int id = bas + j; float dur = 15f + Rnd(id, 1) * 8f, u = Frac(tm / dur + Rnd(id, 2));
                float largo = (90f + 50f * Rnd(id, 7)) * S, y = -largo + u * (H + largo);
                float x = ((j + 0.4f + Rnd(id, 3) * 0.5f) / n) * W;
                float al = Al(x, y + largo * 0.5f, 34f * S); if (al < 0.01f) continue;
                m.Serpentina(x, y, largo, 11f * S * Mueve, (float)tm * 1.1f + id, 3.4f * S, RocolaOsFiestaMalla.Con(Pal(pal, id), 0.9f * al), 9);
            }
        }
        private void Petalos(uint[] pal, int n, int bas, float largo)
        {
            for (int j = 0; j < n; j++)
            {
                int id = bas + j; float dur = 13f + Rnd(id, 1) * 8f, u = Frac(tm / dur + Rnd(id, 2));
                float y = -20f * S + u * (H + 40f * S);
                float x = ((j + Rnd(id, 3) * 0.9f) / n) * W + (float)Math.Sin(tm * 0.9 + id * 1.7) * 30f * S * Mueve;
                float al = Al(x, y, 30f * S) * Math.Min(1f, Math.Min(u * 8f, (1f - u) * 8f)); if (al < 0.01f) continue;
                float l = largo * S * (0.7f + 0.6f * Rnd(id, 4));
                m.Petalo(x, y, l, l * 0.6f, (float)tm * (0.8f + Rnd(id, 5) * 1.6f) + id, Pal(pal, id), 0.95f * al);
            }
        }
        // la nieve de Navidad: puntitos finos y, aparte, COPOS de seis brazos con ramitas, de varios tamanos, que caen despacio con vaiven y giran
        private void Nieve(int n, int bas, bool copos)
        {
            for (int j = 0; j < n; j++)
            {
                int id = bas + j; float dur = (copos ? 22f : 14f) + Rnd(id, 1) * (copos ? 14f : 10f), u = Frac(tm / dur + Rnd(id, 2));
                float r = copos ? (7f + 8f * Rnd(id, 4)) * S : (2.0f + 1.6f * Rnd(id, 4)) * S, marg = r * 1.6f;
                float y = -marg + u * (H + 2f * marg);
                float x = ((j + Rnd(id, 3) * 0.9f) / n) * W + (float)Math.Sin(tm * (copos ? 0.45 : 0.7) + id * 2.1) * (copos ? 34f : 20f) * S * Mueve;
                float al = Al(x, y, 30f * S) * Math.Min(1f, Math.Min(u * 10f, (1f - u) * 10f)); if (al < 0.01f) continue;
                if (!copos) { m.Copo(x, y, r, 0f, RocolaOsFiestaMalla.Con(0xFFFFFF, 0.85f * al)); continue; }
                m.Copo(x, y, r, (float)tm * 0.35f * (Rnd(id, 5) > 0.5f ? 1 : -1) + id, RocolaOsFiestaMalla.Con(0xEAF4FF, 0.95f * al));
                Glow(x, y, r * 2.1f, 0xCFE4FFu, 0.22f * al * (0.4f + 0.6f * RocolaOsCielo.NocheCuadro));
            }
        }
        // flores que caen girando (flores moradas, cempasuchil, flores rosas)
        private void Flores(uint[] pal, int n, int bas, float r, bool rosa)
        {
            for (int j = 0; j < n; j++)
            {
                int id = bas + j; float dur = 20f + Rnd(id, 1) * 12f, u = Frac(tm / dur + Rnd(id, 2));
                float y = -r * 1.6f * S + u * (H + r * 3.2f * S);
                float x = ((j + Rnd(id, 3) * 0.9f) / n) * W + (float)Math.Sin(tm * 0.6 + id * 1.3) * 26f * S * Mueve;
                float al = Al(x, y, 36f * S) * Math.Min(1f, Math.Min(u * 8f, (1f - u) * 8f)); if (al < 0.01f) continue;
                float rr = r * S * (0.8f + 0.4f * Rnd(id, 4)), rot = (float)tm * 0.5f * (Rnd(id, 5) > 0.5f ? 1 : -1) + id;
                uint c = Pal(pal, id);
                if (pal == palMuertos) m.FlorPlana(x, y, rr, 8, c, 0xB85A00, rot, 0.95f * al);
                else if (rosa) m.FlorPlana(x, y, rr, 5, c, 0xFFE066, rot, 0.95f * al);
                else m.FlorPlana(x, y, rr, 5, c, 0xFFE066, rot, 0.95f * al);
            }
        }
        private void Rosas(uint[] pal, int n, int bas)
        {
            for (int j = 0; j < n; j++)
            {
                int id = bas + j; float dur = 24f + Rnd(id, 1) * 12f, u = Frac(tm / dur + Rnd(id, 2));
                float y = -30f * S + u * (H + 60f * S);
                float x = ((j + Rnd(id, 3) * 0.9f) / n) * W + (float)Math.Sin(tm * 0.5 + id * 1.9) * 24f * S * Mueve;
                float al = Al(x, y, 36f * S) * Math.Min(1f, Math.Min(u * 8f, (1f - u) * 8f)); if (al < 0.01f) continue;
                m.RosaPlana(x, y, (15f + 7f * Rnd(id, 4)) * S, Pal(pal, id), (float)tm * 0.4f + id, 0.95f * al);
            }
        }
        // las notas de Santa Cecilia: suben desde abajo, grandes
        private void Notas(uint[] pal, int n, int bas)
        {
            for (int j = 0; j < n; j++)
            {
                int id = bas + j; float dur = 15f + Rnd(id, 1) * 9f, u = Frac(tm / dur + Rnd(id, 2));
                float h = (36f + 24f * Rnd(id, 4)) * S, y = H + h - u * (H + 2f * h);
                float x = ((j + Rnd(id, 3) * 0.9f) / n) * W + (float)Math.Sin(tm * 0.9 + id) * 22f * S * Mueve;
                float al = Al(x, y, 34f * S) * Math.Min(1f, Math.Min(u * 8f, (1f - u) * 8f)); if (al < 0.01f) continue;
                uint c = RocolaOsFiestaMalla.Con(Pal(pal, id), 0.95f * al);
                if ((id & 1) == 0) m.Nota(x, y, h, c); else m.NotaDoble(x, y, h, c);
            }
        }

        // ------------------------------------------------------------------ lo que sube
        // clase 0 farolitos, 1 corazones, 2 globos
        private void Subida(int clase, uint[] pal, int n, int bas, float k, uint[] colores)
        {
            for (int j = 0; j < n; j++)
            {
                int id = bas + j; float dur = (clase == 1 ? 14f : 20f) + Rnd(id, 1) * 8f, u = Frac(tm / dur + Rnd(id, 2));
                float tam = (clase == 1 ? 30f + 32f * Rnd(id, 4) : 1f + 0.35f * Rnd(id, 4)) * S;
                float marg = (clase == 1 ? tam : 40f * S * tam / S);
                float y = H + marg - u * (H + 2f * marg);
                float x = ((j + Rnd(id, 3) * 0.9f) / n) * W + (float)Math.Sin(tm * 0.8 + id * 1.9) * 16f * S * Mueve;
                float al = Al(x, y, 34f * S) * Math.Min(1f, Math.Min(u * 8f, (1f - u) * 8f)); if (al < 0.01f) continue;
                uint c = Pal(pal, id);
                if (clase == 1) m.Corazon(x, y, tam, RocolaOsFiestaMalla.Con(c, 0.95f * al));
                else if (clase == 2) m.Globo(x, y, 21f * tam, 28f * tam, c, 0.96f * al, (float)tm * 1.3f + id, 0xEFEFEF);
                else
                {
                    float rx = 14f * tam, ry = 17.5f * tam;
                    m.Farolito(x, y, rx, ry, c, 0.96f * al);
                    Glow(x, y, 34f * tam, c, 0.32f * al * (0.4f + 0.6f * RocolaOsCielo.NocheCuadro));
                }
            }
        }
        // chispas de cuatro puntas que titilan en el mismo sitio
        private void Chispas(uint[] pal, int n, int bas, float R)
        {
            for (int j = 0; j < n; j++)
            {
                int id = bas + j;
                float x = (Rnd(id, 1) * 0.94f + 0.03f) * W, y = (0.04f + 0.62f * Rnd(id, 2)) * H;
                float ti = 0.5f + 0.5f * (float)Math.Sin(tm * (1.6 + Rnd(id, 3)) + id * 3.1); ti *= ti;
                float al = Al(x, y, 30f * S) * (0.35f + 0.65f * ti); if (al < 0.01f) continue;
                float r = R * S * (0.7f + 0.6f * ti) * (0.8f + 0.5f * Rnd(id, 4)); uint c = Pal(pal, id);
                m.Chispa(x, y, r, RocolaOsFiestaMalla.Con(RocolaOsFiestaMalla.Mezcla(c, 0xFFFFFF, 0.4f), 0.95f * al), (float)tm * 0.2f + id);
                Glow(x, y, r * 2.6f, c, 0.4f * al * (0.4f + 0.6f * RocolaOsCielo.NocheCuadro));
            }
        }
        // veladoras de Muertos: suben despacio con su llama que titila
        private void Veladoras(int n, int bas)
        {
            for (int j = 0; j < n; j++)
            {
                int id = bas + j; float dur = 24f + Rnd(id, 1) * 10f, u = Frac(tm / dur + Rnd(id, 2));
                float y = H + 40f * S - u * (H + 80f * S);
                float x = ((j + Rnd(id, 3) * 0.9f) / n) * W + (float)Math.Sin(tm * 0.6 + id * 1.7) * 12f * S * Mueve;
                float al = Al(x, y, 34f * S) * Math.Min(1f, Math.Min(u * 8f, (1f - u) * 8f)); if (al < 0.01f) continue;
                float fl = 0.85f + 0.15f * (float)Math.Sin(tm * 9.0 + id) * Mueve, w = 13f * S, h = 26f * S;
                m.Rect(x - w * 0.5f, y, w, h, RocolaOsFiestaMalla.Con(0xFFF3DC, 0.96f * al));
                m.Rect(x - w * 0.5f, y + h * 0.78f, w, h * 0.22f, RocolaOsFiestaMalla.Con(0xC9A8E0, 0.9f * al));
                m.Elipse(x, y - 8f * S, 5f * S, 9f * S * fl, RocolaOsFiestaMalla.Con(0xFFB347, 0.97f * al), 8, 0.7f);
                m.Elipse(x, y - 6f * S, 2.4f * S, 5f * S * fl, RocolaOsFiestaMalla.Con(0xFFF1A8, 0.97f * al), 6, 0f);
                Glow(x, y - 8f * S, 44f * S, 0xFFB347u, 0.6f * fl * al * (0.4f + 0.6f * RocolaOsCielo.NocheCuadro));
            }
        }
        // calaveritas sencillas que flotan despacio
        private void Calaveras(int n, int bas)
        {
            uint[] ad = { 0xFFFF9A1Au, 0xFFB06CFFu, 0xFFE03C6Eu, 0xFF3CC8FFu };
            for (int j = 0; j < n; j++)
            {
                int id = bas + j; float dur = 34f + Rnd(id, 1) * 14f, u = Frac(tm / dur + Rnd(id, 2));
                float r = (17f + 6f * Rnd(id, 4)) * S, y = H + 2f * r - u * (H + 4f * r);
                float x = ((j + 0.3f + Rnd(id, 3) * 0.5f) / n) * W + (float)Math.Sin(tm * 0.5 + id * 2.3) * 24f * S * Mueve;
                float al = Al(x, y, 40f * S) * Math.Min(1f, Math.Min(u * 8f, (1f - u) * 8f)); if (al < 0.01f) continue;
                m.Calavera(x, y, r, Pal(ad, id), 0.96f * al);
            }
        }
        // coronas de Reyes que se mecen en el cielo
        private void Corona(uint[] pal, int n, int bas, float w)
        {
            for (int j = 0; j < n; j++)
            {
                int id = bas + j; float dur = 30f + Rnd(id, 1) * 12f, u = Frac(tm / dur + Rnd(id, 2));
                float ww = w * S * (0.85f + 0.4f * Rnd(id, 4)), y = H + ww - u * (H + 2f * ww);
                float x = ((j + 0.3f + Rnd(id, 3) * 0.5f) / n) * W + (float)Math.Sin(tm * 0.6 + id * 2.1) * 20f * S * Mueve;
                float al = Al(x, y, 36f * S) * Math.Min(1f, Math.Min(u * 8f, (1f - u) * 8f)); if (al < 0.01f) continue;
                m.Corona(x, y, ww, ww * 0.75f, Pal(pal, id), 0.96f * al);
            }
        }
        // la estrella guia (grande, con cruz y cola), fija arriba a la izquierda, que respira
        private void EstrellaGuia()
        {
            float x = escap ? W * 0.545f : W * 0.14f, y = escap ? 92f * S : H * 0.2f, ti = 0.5f + 0.5f * (float)Math.Sin(tm * 1.7);
            float al = Al(x, y, 40f * S); if (al < 0.01f) return;
            float R = 32f * S * (0.94f + 0.08f * ti);
            m.EstrellaGuia(x, y, R, (float)Math.Sin(tm * 0.6) * 0.08f * Mueve, 0.6f + 0.4f * ti, 0.97f * al);
            Glow(x, y, 100f * S, 0xFFC83Cu, 0.55f * al * (0.4f + 0.6f * RocolaOsCielo.NocheCuadro));
        }
        // la bandera grande de Mexico, ondeando (arriba a la izquierda: asoma por detras del cubo)
        private void BanderaGrande()
        {
            float px = W * 0.035f, y0 = H * 0.16f, bw = 176f * S, bh = 116f * S;
            float al = Al(px + bw * 0.4f, y0 + bh * 0.5f, 40f * S); if (al < 0.01f) return;
            m.Bandera(px, y0, bw, bh, (float)tm, 7f * S * Mueve, 0.97f * al, H * 0.55f, true);
        }
        // banderitas de Mexico (de papel, chicas) que suben ondeando
        private void Banderitas(int n, int bas)
        {
            for (int j = 0; j < n; j++)
            {
                int id = bas + j; float dur = 22f + Rnd(id, 1) * 9f, u = Frac(tm / dur + Rnd(id, 2));
                float bw = (40f + 14f * Rnd(id, 4)) * S, bh = bw * 0.62f, y = H + 30f * S - u * (H + 90f * S);
                float x = ((j + Rnd(id, 3) * 0.9f) / n) * W + (float)Math.Sin(tm * 0.6 + id * 1.9) * 16f * S * Mueve;
                float al = Al(x, y, 36f * S) * Math.Min(1f, Math.Min(u * 8f, (1f - u) * 8f)); if (al < 0.01f) continue;
                m.Bandera(x, y, bw, bh, (float)tm + id, 3f * S * Mueve, 0.97f * al, bh * 1.9f, false);
            }
        }

        // nochebuenas que flotan despacio hacia arriba
        private void Nochebuenas(int n, int bas)
        {
            for (int j = 0; j < n; j++)
            {
                int id = bas + j; float dur = 26f + Rnd(id, 1) * 10f, u = Frac(tm / dur + Rnd(id, 2));
                float r = (22f + 8f * Rnd(id, 4)) * S, y = -r + u * (H + 2f * r);
                float x = ((j + 0.3f + Rnd(id, 3) * 0.5f) / n) * W + (float)Math.Sin(tm * 0.5 + id * 2.3) * 26f * S * Mueve;
                float al = Al(x, y, 40f * S) * Math.Min(1f, Math.Min(u * 8f, (1f - u) * 8f)); if (al < 0.01f) continue;
                m.Nochebuena(x, y, r, 0xE0303Cu, (float)tm * 0.3f + id, 0.96f * al);
            }
        }
    }
}
