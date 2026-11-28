// EL CIELO VIVO DE ROCOLA OS, FIESTAS: FI4, LOS FUEGOS ARTIFICIALES (FASE11_cielo-fiestas.md). Solo en el Grito (15-9 a las 23:00) y en Ano Nuevo (1-1 a
// las 00:00, tras una cuenta de 10 s que se ve con numeros de siete segmentos), o la hora que ponga rocola_fechas.txt («fuegos;HH:MM»); duran 4 minutos
// (RocolaOsCalendario.FuegosMinutos). La capa es TrasFondo: detras de las portadas, la interfaz y todo texto. Y como la regla del dueno dice «nunca
// detras de un texto», solo se pintan donde el cielo esta libre de texto:
//   - la atraccion con la ficha de las canciones (el escaparate): la franja de arriba, por encima del cubo y de la ficha (y mientras la lista de lo
//     mas pedido la tapa, nada: su cabecera y sus filas llegan hasta arriba);
//   - el reposo con musica (la franja AHORA SUENA abajo): el cielo de arriba, por encima de la raya del horizonte que subio;
//   - en la pared, el buscador y el disco abierto no hay cielo libre de texto: no hay fuegos.
// Cada rafaga lleva su limite de abajo: la chispa que cae mas alla se apaga antes de llegar (nunca llega a un texto). Como mucho 3 destellos (rafagas
// nuevas) por segundo, cada uno con su resplandor chico (no hay destellos de pantalla: el mas fuerte pinta 0,3 de luz en 90 px); al 35 % (calma) si
// alguien elige (menos rafagas, y mas apagadas). Un cuadro nunca reserva memoria: arreglos fijos.
using System;
using System.Globalization;
using System.IO;

namespace Rocola
{
    internal sealed class RocolaOsFiestaFuegos : RocolaOsCieloPintor
    {
        internal RocolaOsFiestaFuegos() { Quien = "FI4 los fuegos artificiales"; Orden = RocolaOsCieloOrden.Fiestas + 30; }
        private const int Rafagas = 4, Chispas = 36, Dentro = 12, MaxPorSegundo = 3;
        private const float Vida = 1.7f;
        private struct Rafaga { internal bool On; internal float T, Cx, Cy, R, Gir; internal uint C0, C1; internal int Semilla; }
        private readonly Rafaga[] r = new Rafaga[Rafagas];
        private readonly double[] ultimos = new double[MaxPorSegundo];   // cuando nacieron las tres ultimas (s del reloj de la tarjeta)
        private int ult; private double proximoS = -1; private int total; private uint rng = 2463534242u;
        private bool apuntado; private double inicioFuegos = double.NaN;
        private readonly RocolaOsFiestaMalla dig = new RocolaOsFiestaMalla();
        private readonly float[] gx = new float[Rafagas * Chispas * 4 + 8], gy = new float[Rafagas * Chispas * 4 + 8], gr = new float[Rafagas * Chispas * 4 + 8];
        private readonly uint[] gc = new uint[Rafagas * Chispas * 4 + 8];
        private int gn;
        // las pruebas apuntan cada nacimiento (ROCOLA_FUEGOS_LOG = archivo): «segundos x y»
        private static readonly string registro = Environment.GetEnvironmentVariable("ROCOLA_FUEGOS_LOG");
        // (pruebas) ROCOLA_FUEGOS_CALMA=0.35 fija la calma de los fuegos (la de verdad solo baja con una tecla, y una tecla saca del escaparate)
        private static readonly float calmaPrueba = ParseaCalma(Environment.GetEnvironmentVariable("ROCOLA_FUEGOS_CALMA"));
        private static float ParseaCalma(string v) { float c; return !string.IsNullOrEmpty(v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out c) && c >= 0.2f && c <= 1f ? c : -1f; }

        private float Azar() { rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5; return (rng & 0xFFFFFF) / 16777216f; }
        private void Alto() { for (int i = 0; i < Rafagas; i++) r[i].On = false; proximoS = -1; total = 0; inicioFuegos = double.NaN; }

        internal override void Pinta(GpuPanel p, int capa)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f == null || !f.FI4 || !f.Hoy.ProximosFuegos.HasValue) { Alto(); return; }
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            double t = (RocolaOsCielo.AhoraLocal() - f.Hoy.ProximosFuegos.Value).TotalSeconds;   // <0: falta para empezar (la cuenta)
            int cuenta = f.Hoy.CuentaFuegos;
            if (t < -cuenta || t >= RocolaOsCalendario.FuegosMinutos * 60) { Alto(); return; }
            // donde hay cielo libre de texto en esta escena
            float ymax, ytop, xmin, xmax, vis = 1f;
            if (e.Modo == 4)
            {
                bool vitrina, lista; float ficha;
                bool hay = p.RocolaOsEscaparate(out vitrina, out lista, out ficha);
                RectangleFCubo(e.Ancho, e.Alto, out ymax);
                vis = hay ? (vitrina || !lista ? ficha : 0f) : 1f;   // la lista de lo mas pedido tapa los fuegos (su cabecera llega hasta arriba)
                xmin = e.Ancho * 0.10f; xmax = e.Ancho * 0.90f; ytop = 40f * e.Alto / 768f;
            }
            else if (e.Reposo > 0.5f && e.Modo != 2 && e.Modo != 3)
            {
                ymax = e.Horizonte - 14f; vis = Math.Max(0f, Math.Min(1f, (e.Reposo - 0.5f) * 2f));
                xmin = e.Ancho * 0.10f; xmax = e.Ancho * 0.90f; ytop = 30f * e.Alto / 768f;
            }
            else { Alto(); return; }
            if (ymax - ytop < 70f || vis <= 0.01f) { return; }
            GpuStarfield st = p.RocolaOsEstrellas;
            if (st == null || !st.Ok) return;
            float s = Math.Max(0.8f, Math.Min(1.6f, e.Alto / 768f)), calma = calmaPrueba >= 0f ? calmaPrueba : e.Calma; double ahoraS = e.Ms / 1000.0;
            if (!apuntado) { apuntado = true; RocolaOs.Apunta("cielo", "fuegos artificiales: empiezan " + f.Hoy.ProximosFuegos.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + (cuenta > 0 ? " con una cuenta de " + cuenta + " s" : "")); }

            // ---- la cuenta (Ano Nuevo): los numeros de arriba, en el cielo libre
            if (t < 0)
            {
                int n = (int)Math.Ceiling(-t); if (n < 1) n = 1; if (n > 99) n = 99;
                float fr = (float)(-t - Math.Floor(-t));   // 1 -> 0 dentro del segundo
                float alto = Math.Min(90f * s, (ymax - ytop) * 0.62f);
                Cuenta(st, n, e.Ancho * 0.5f, ytop + Math.Min((ymax - ytop) * 0.22f, 40f * s), alto, fr, vis);
                return;
            }
            if (double.IsNaN(inicioFuegos)) inicioFuegos = t;
            // ---- los nacimientos (a lo mas 3 por segundo, y con la calma, mas espaciados)
            if (proximoS < 0) proximoS = ahoraS;
            if (ahoraS >= proximoS && t < RocolaOsCalendario.FuegosMinutos * 60 - 3)
            {
                if (ahoraS - ultimos[ult] >= 1.0)
                {
                    Nace(ahoraS, ymax, ytop, xmin, xmax, f, s);
                    ultimos[ult] = ahoraS; ult = (ult + 1) % MaxPorSegundo;
                    bool final = t - 0 < 3.0 && total < 3;   // el comienzo: tres seguidas
                    float gap = final ? 0.45f : 1.1f + Azar() * 1.1f;
                    proximoS = ahoraS + gap / Math.Max(0.35f, calma);
                    total++;
                }
                else proximoS = ahoraS + 0.1;
            }
            // ---- el dibujo
            gn = 0; float dt = (float)e.Dt;
            float luz = vis * (calma < 1f ? calma : 1f);
            for (int i = 0; i < Rafagas; i++)
            {
                if (!r[i].On) continue;
                r[i].T += dt;
                if (r[i].T >= Vida) { r[i].On = false; RocolaOsClimaLuces.PonFuego(i, 0f, 0f, 0f, 0u, 0f, e.Ms); continue; }
                Chispea(ref r[i], ymax, s, luz);
                RocolaOsClimaLuces.PonFuego(i, r[i].Cx, r[i].Cy, r[i].R, r[i].C0, (float)Math.Pow(1f - r[i].T / Vida, 1.3) * luz, e.Ms);   // (alumbra las nubes: RocolaOsCieloClimaLuz)
            }
            if (gn > 0)
            {
                st.GlowBatchBegin(gn);
                for (int i = 0; i < gn; i++) st.GlowBatchAdd(gx[i], gy[i], gr[i], gc[i]);
                st.GlowBatchEnd();
            }
        }

        private static void RectangleFCubo(int w, int h, out float ymax)
        {
            System.Drawing.RectangleF c = Form1.CuboEscaparate(w, h), col = Form1.ColumnaEscaparate(w, h);
            ymax = Math.Min(c.Y, col.Y) - 10f;
            if (ymax > h * 0.30f) ymax = h * 0.30f;   // (el cubo girando asoma un poco mas arriba que su cuadro)
        }

        private void Nace(double ahoraS, float ymax, float ytop, float xmin, float xmax, RocolaOsCieloFoto f, float s)
        {
            int libre = -1;
            for (int i = 0; i < Rafagas; i++) if (!r[i].On) { libre = i; break; }
            if (libre < 0) return;
            uint[] col = RocolaOsCieloFiestas.Colores(f);
            if (col.Length == 0) col = new uint[] { 0xFFFFC83Cu, 0xFFD8DEE8u, 0xFFFF7AB8u, 0xFF3C78FFu };
            r[libre].On = true; r[libre].T = 0f; r[libre].Semilla = (int)(rng & 0x7FFFFFFF);
            float zona = ymax - ytop;   // el cielo libre de texto: de ytop a ymax
            r[libre].Cx = xmin + Azar() * (xmax - xmin); r[libre].Cy = ytop + zona * (0.30f + Azar() * 0.26f);
            r[libre].R = Math.Min(135f * s, zona * (0.38f + Azar() * 0.17f)); r[libre].Gir = Azar() * 6.2832f;
            int k = (int)(Azar() * col.Length) % col.Length;
            r[libre].C0 = col[k]; r[libre].C1 = col[(k + 1 + (int)(Azar() * 2)) % col.Length];
            if (registro != null) { try { File.AppendAllText(registro, ahoraS.ToString("0.000", CultureInfo.InvariantCulture) + " " + r[libre].Cx.ToString("0", CultureInfo.InvariantCulture) + " " + r[libre].Cy.ToString("0", CultureInfo.InvariantCulture) + "\n"); } catch { } }
        }

        // las chispas de una rafaga: frenan al salir (la distancia crece con 1 - exp), caen un poco y se apagan; cada una deja dos colitas
        private void Chispea(ref Rafaga b, float ymax, float s, float luz)
        {
            float t = b.T, vida = t / Vida, a0 = (float)Math.Pow(1f - vida, 1.3);
            // el destello del estallido (chico y corto: 0,22 s)
            if (t < 0.22f && gn < gx.Length) { gx[gn] = b.Cx; gy[gn] = b.Cy; gr[gn] = b.R * 0.85f; gc[gn] = RocolaOsFiestaMalla.Con(b.C0, 0.28f * (1f - t / 0.22f) * luz); gn++; }
            for (int i = 0; i < Chispas; i++)
            {
                // dos capas: 12 chispas que van mas cerca (del segundo color) y 24 que llegan lejos (de los dos)
                bool dentro = i < Dentro;
                float az = RocolaOsCieloFiestas.Rnd(b.Semilla, i), rv = RocolaOsCieloFiestas.Rnd(b.Semilla, i + 99);
                float ang = b.Gir + (dentro ? (float)(i * 2 * Math.PI / Dentro) + 0.26f : (float)((i - Dentro) * 2 * Math.PI / (Chispas - Dentro))) + (az - 0.5f) * 0.3f;
                float vel = dentro ? 0.42f + 0.14f * rv : 0.78f + 0.22f * rv;
                uint c = (dentro ? b.C1 : RocolaOsFiestaMalla.Mezcla(b.C0, b.C1, (i & 1) == 0 ? vida : 0.5f * vida)) | 0xFF000000u;
                int colas = dentro ? 2 : 3;
                for (int q = 0; q < colas; q++)
                {
                    float tq = t - q * 0.07f; if (tq < 0f) break;
                    float d = b.R * vel * (1f - (float)Math.Exp(-3.4f * tq)), x = b.Cx + (float)Math.Cos(ang) * d, y = b.Cy + (float)Math.Sin(ang) * d + 26f * s * tq * tq;
                    float zona = Math.Max(0f, Math.Min(1f, (ymax - y) / 18f));   // la chispa que baja mas alla de donde hay cielo libre se apaga antes
                    float tw = 0.85f + 0.15f * (float)Math.Sin(t * 11f + i * 1.7f);
                    float a = a0 * zona * luz * tw * (q == 0 ? 1f : q == 1 ? 0.55f : 0.25f);
                    if (a <= 0.02f || gn + 2 > gx.Length) continue;
                    gx[gn] = x; gy[gn] = y; gr[gn] = (q == 0 ? 11f : q == 1 ? 7f : 4.6f) * s; gc[gn] = RocolaOsFiestaMalla.Con(c, a); gn++;
                    if (q == 0) { gx[gn] = x; gy[gn] = y; gr[gn] = 4.4f * s; gc[gn] = RocolaOsFiestaMalla.Con(RocolaOsFiestaMalla.Mezcla(c, 0xFFFFFF, 0.7f), Math.Min(1f, a * 1.1f)); gn++; }
                }
            }
        }

        // ---- los numeros de la cuenta, de siete segmentos (a b c d e f g), dorados con un halo; laten al cambiar cada segundo
        private static readonly int[] Seg = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };
        private void Cuenta(GpuStarfield st, int n, float cx, float y0, float alto, float fr, float vis)
        {
            string txt = n.ToString(CultureInfo.InvariantCulture);
            float pulso = 1f + 0.10f * (float)Math.Pow(fr, 3), h = alto * pulso, w = h * 0.55f, gr0 = h * 0.14f, ancho = txt.Length * (w + h * 0.3f) - h * 0.3f;
            float x = cx - ancho * 0.5f, y = y0 - (h - alto) * 0.5f;
            dig.Limpia(); gn = 0;
            uint oro = RocolaOsFiestaMalla.Con(0xFFD36B, 0.95f * vis), brasa = RocolaOsFiestaMalla.Con(0xFF9A2A, 0.14f * vis);
            for (int d = 0; d < txt.Length; d++)
            {
                int mask = Seg[txt[d] - '0'];
                float gx0 = x + d * (w + h * 0.3f);
                for (int sg = 0; sg < 7; sg++)
                {
                    bool on = (mask & (1 << sg)) != 0;
                    uint c = on ? oro : brasa;
                    float x0, y1, lw, lh;
                    switch (sg)
                    {
                        case 0: x0 = gx0 + gr0; y1 = y; lw = w - 2 * gr0; lh = gr0; break;                          // a (arriba)
                        case 1: x0 = gx0 + w - gr0; y1 = y + gr0 * 0.6f; lw = gr0; lh = h * 0.5f - gr0 * 1.2f; break;    // b
                        case 2: x0 = gx0 + w - gr0; y1 = y + h * 0.5f + gr0 * 0.6f; lw = gr0; lh = h * 0.5f - gr0 * 1.2f; break;   // c
                        case 3: x0 = gx0 + gr0; y1 = y + h - gr0; lw = w - 2 * gr0; lh = gr0; break;                // d (abajo)
                        case 4: x0 = gx0; y1 = y + h * 0.5f + gr0 * 0.6f; lw = gr0; lh = h * 0.5f - gr0 * 1.2f; break;   // e
                        case 5: x0 = gx0; y1 = y + gr0 * 0.6f; lw = gr0; lh = h * 0.5f - gr0 * 1.2f; break;         // f
                        default: x0 = gx0 + gr0; y1 = y + h * 0.5f - gr0 * 0.5f; lw = w - 2 * gr0; lh = gr0; break;   // g (en medio)
                    }
                    dig.Rect(x0, y1, lw, lh, c);
                    if (on && gn < gx.Length) { gx[gn] = x0 + lw * 0.5f; gy[gn] = y1 + lh * 0.5f; gr[gn] = Math.Max(lw, lh) * 0.9f; gc[gn] = RocolaOsFiestaMalla.Con(0xFFB04A, 0.22f * vis); gn++; }
                }
            }
            if (dig.Tris > 0) st.DrawMesh(dig.V, dig.Tris);
            if (gn > 0) { st.GlowBatchBegin(gn); for (int i = 0; i < gn; i++) st.GlowBatchAdd(gx[i], gy[i], gr[i], gc[i]); st.GlowBatchEnd(); }
        }
    }
}
