// EL CIELO (el fondo Legacy de la rocola de XP, hecho aqui con OpenGL ES directo). De arriba abajo: la noche en un degradado azul y
// violeta como el de XP; dos capas de nebulosa (una azul y otra violeta) que van a la deriva en sentidos contrarios, de modo que
// donde se cruzan el color cambia despacio; las estrellas, pequenas que titilan y unas cuantas grandes con su destello en cruz; la
// onda de luz que cruza la parte de abajo (late un poco con los graves); el horizonte con su linea de luz; y la luna en la fase de
// hoy. Las texturas (nebulosa y luna) se hacen una vez en el procesador.
using System;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        private Textura nebulosa, luna; private int lunaDia = -1;
        private float[] brillantes;   // x, y (0..1), tamano, fase, tinte

        // La altura del horizonte (la linea de luz de XP, a 662 de 768): abajo de ella va la franja de lo que suena.
        internal float Horizonte(float H) { return H * 0.862f; }

        private void Cielo(float W, float H, double t, float alfa = 1f)
        {
            float yh = Horizonte(H);
            // la noche: cuatro paradas (medidas en la de XP) y, bajo el horizonte, un azul que se apaga
            // con el tinte de lo que suena (ColorFondo.cs): con el azul de siempre, las mismas paradas de antes
            C3 a0 = SkyArriba(acento), am = SkyMedio(acento, AzulCielo), ab = SkyAbajo(acento), d0 = SkyArriba(Defecto), dm = SkyMedio(Defecto), db = SkyAbajo(Defecto);
            C3 a1 = new C3((a0.R + am.R) / 2, (a0.G + am.G) / 2, (a0.B + am.B) / 2), d1 = new C3((d0.R + dm.R) / 2, (d0.G + dm.G) / 2, (d0.B + dm.B) / 2);
            float corte = H * 0.40f * corteNegro / 42f;   // donde acaba el negro de arriba (cielo_negro=, 42 de fabrica)
            d.Degradado(0, 0, W, corte, Parada(0x16163A, a0, d0, alfa), Parada(0x28285C, a1, d1, alfa));
            d.Degradado(0, corte, W, H * 0.75f - corte, Parada(0x28285C, a1, d1, alfa), Parada(0x2C367D, am, dm, alfa));
            d.Degradado(0, H * 0.75f, W, yh - H * 0.75f, Parada(0x2C367D, am, dm, alfa), Parada(0x37408F, ab, db, alfa));
            d.Degradado(0, yh, W, H - yh, Parada(0x223470, ab, db, alfa), Parada(0x16234C, ab, db, alfa));
            Nebulosas(W, H, t, alfa);
            Estrellas(W, H, t, alfa);
            if (auroraAhora) Aurora(W, H, t, alfa);   // V2
            Onda(W, H, t, alfa);
            HorizonteLuz(W, H, t, alfa);
            Luna(W, H, t, alfa);
        }

        // ---- las nebulosas ----
        private void Nebulosas(float W, float H, double t, float alfa)
        {
            if (nebulosa == null) nebulosa = HazNebulosa(256, 192);
            float mx = W * 0.06f, my = H * 0.06f;
            double f = t * 0.035;   // una vuelta cada tres minutos
            float c = (float)Math.Cos(f), s = (float)Math.Sin(f);
            float fuerza = 0.55f + 0.25f * pMedios;
            d.ImagenTenida(nebulosa, -mx + c * mx, -my + s * my, W + 2 * mx, H + 2 * my, TinteNebulosa(0x4C8AD8, nebA, NebADefecto, fuerza * alfa));
            d.ImagenTenida(nebulosa, -mx - c * mx, -my - s * my, W + 2 * mx, H + 2 * my, TinteNebulosa(0x9A6EE6, nebB, NebBDefecto, fuerza * 0.85f * alfa));
        }
        // Ruido de valores en varias escalas (nubes), mas fuerte arriba y en manchas; blanco con su alfa.
        private Textura HazNebulosa(int w, int h)
        {
            Random r = new Random(20260621);
            int g = 17; float[] red = new float[g * g];
            for (int i = 0; i < red.Length; i++) red[i] = (float)r.NextDouble();
            Func<float, float, float> ruido = (x, y) =>
            {
                x = ((x % (g - 1)) + (g - 1)) % (g - 1); y = ((y % (g - 1)) + (g - 1)) % (g - 1);
                int x0 = (int)x, y0 = (int)y; float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
                float a = red[y0 * g + x0], b = red[y0 * g + x0 + 1], c = red[(y0 + 1) * g + x0], d2 = red[(y0 + 1) * g + x0 + 1];
                return a + (b - a) * fx + (c - a) * fy + (a - b - c + d2) * fx * fy;
            };
            byte[] px = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w * 4f, v = y / (float)h * 3f, n = 0, amp = 0.55f;
                    for (int o = 0; o < 4; o++) { n += amp * ruido(u * (1 << o) + o * 3.1f, v * (1 << o) + o * 1.7f); amp *= 0.5f; }
                    float alto = 1f - Math.Abs(y / (float)h - 0.32f) * 1.5f;   // mas arriba que abajo, como en XP
                    float a = Math.Max(0f, n - 0.42f) * 2.2f * Math.Max(0f, alto);
                    a = a * a * 0.55f;
                    int o4 = (y * w + x) * 4;
                    px[o4] = px[o4 + 1] = px[o4 + 2] = 255; px[o4 + 3] = (byte)Math.Min(255, a * 255);
                }
            return d.Sube(px, w, h);
        }

        // ---- las estrellas ----
        private void Estrellas(float W, float H, double t, float alfa)
        {
            float yh = Horizonte(H);
            for (int i = 0; i < estrellas.Length; i += 5)
            {
                float y = estrellas[i + 1] * yh;
                float a = Math.Min(1f, 0.40f + 0.50f * (float)(0.5 + 0.5 * Math.Sin(t * estrellas[i + 4] + estrellas[i + 3])) + 0.30f * pAgudos * (i % 3 == 0 ? 1 : 0));
                float tam = estrellas[i + 2] * 0.75f * k;
                if (aireAhora)
                {
                    // V3: cuanto mas cerca del horizonte, mas aire delante: mas tenue, mas calida y titila mas (cielo_aire de XP)
                    float fh = y / yh, f2 = fh * fh;
                    a = Math.Max(0f, Math.Min(1f, a * (1f - 0.5f * f2) + 0.35f * f2 * (float)Math.Sin(t * (4.5 + estrellas[i + 4] * 2) + estrellas[i + 3] * 3)));
                    Color4 cs = Dibujo.Mezcla(new Color4(0.86f, 0.9f, 1f), new Color4(1f, 0.78f, 0.58f), 0.55f * f2);
                    if (tam > 1.6f * k) d.Brillo(estrellas[i] * W, y, tam * 5f, cs.ConAlfa(alfa * a * 0.12f));   // el halo de las grandes
                    d.Brillo(estrellas[i] * W, y, tam * 2f, cs.ConAlfa(alfa * a * Math.Min(1f, 0.35f + tam / (2.5f * k))));
                    continue;
                }
                d.Brillo(estrellas[i] * W, y, tam * 2f, new Color4(0.86f, 0.9f, 1f, alfa * a * Math.Min(1f, 0.35f + tam / (2.5f * k))));
            }
            if (brillantes == null)
            {
                Random r = new Random(77); brillantes = new float[18 * 5];
                for (int i = 0; i < brillantes.Length; i += 5)
                {
                    brillantes[i] = (float)r.NextDouble(); brillantes[i + 1] = (float)(0.04 + r.NextDouble() * 0.88);
                    brillantes[i + 2] = (float)(0.55 + Math.Pow(r.NextDouble(), 2) * 0.9); brillantes[i + 3] = (float)(r.NextDouble() * 6.283);
                    brillantes[i + 4] = (float)r.NextDouble();
                }
            }
            for (int i = 0; i < brillantes.Length; i += 5)
            {
                float x = brillantes[i] * W, y = brillantes[i + 1] * yh, s = brillantes[i + 2];
                float a = (0.70f + 0.30f * (float)Math.Sin(t * 1.3 + brillantes[i + 3])) * alfa;
                Color4 c = brillantes[i + 4] < 0.25f ? new Color4(1f, 0.82f, 0.9f) : brillantes[i + 4] < 0.45f ? new Color4(0.8f, 0.88f, 1f) : new Color4(1f, 1f, 1f);
                Destello(x, y, s * k, c, a);
            }
        }
        // Una estrella grande: su halo y las cuatro puntas de luz (como las de XP).
        private void Destello(float x, float y, float s, Color4 c, float a)
        {
            d.Brillo(x, y, 9f * s, c.ConAlfa(0.35f * a));
            d.Brillo(x, y, 3.2f * s, new Color4(1, 1, 1, 0.95f * a));
            float L = 13f * s, g = 1.1f * s;
            Color4 centro = c.ConAlfa(0.85f * a), nada = c.ConAlfa(0f);
            d.Cuatro(x - L, y, nada, x, y - g, centro, x + L, y, nada, x, y + g, centro);
            d.Cuatro(x, y - L, nada, x + g, y, centro, x, y + L, nada, x - g, y, centro);
        }

        // ---- V2: LA AURORA QUE RESPIRA CON LA MUSICA: dos cortinas de luz sobre el horizonte (verde y turquesa, tenidas del color de lo
        // que suena), con el borde de abajo brillante y la luz subiendo hasta apagarse; los graves la estiran hacia arriba ----
        private bool aireAhora = true, auroraAhora = true;   // de fabrica (la pantalla de arranque se dibuja antes de leer los ajustes)
        private void Aurora(float W, float H, double t, float alfa)
        {
            float yh = Horizonte(H) - 30 * k;
            C3 ac = acento; float mx = Math.Max(1f, Math.Max(ac.R, Math.Max(ac.G, ac.B)));
            Color4 gen = new Color4(ac.R / mx, ac.G / mx, ac.B / mx);
            for (int capa = 0; capa < 2; capa++)
            {
                Color4 col = Dibujo.Mezcla(capa == 0 ? new Color4(0.30f, 1f, 0.62f) : new Color4(0.36f, 0.86f, 1f), gen, 0.30f);
                int n = 64; float sube = (capa == 0 ? 130 : 95) * k * (1f + 0.55f * pGraves), fase = capa * 2.1f;
                float px = 0, pb = 0, pa = 0, pal = 0;
                for (int i = 0; i <= n; i++)
                {
                    double u = i / (double)n;
                    float onda = (float)(0.5 + 0.5 * Math.Sin(u * 7.3 + t * 0.19 + fase) * Math.Sin(u * 3.1 - t * 0.11 + fase * 0.5));
                    float x = (float)(u * W), base_ = yh - 40 * k * (float)Math.Sin(u * 4.4 + t * 0.07 + fase) - 55 * k * capa;
                    float alto = sube * (0.25f + 0.75f * onda), a = 0.55f * alfa * (0.25f + 0.75f * onda);
                    if (i > 0)
                    {
                        // la cortina (de transparente arriba a su color en el borde de abajo) y el borde, que se apaga hacia abajo
                        d.Cuatro(px, pb - pa, col.ConAlfa(0f), x, base_ - alto, col.ConAlfa(0f), x, base_, col.ConAlfa(a), px, pb, col.ConAlfa(pal));
                        d.Cuatro(px, pb, col.ConAlfa(pal * 1.3f), x, base_, col.ConAlfa(a * 1.3f), x, base_ + 9 * k, col.ConAlfa(0f), px, pb + 9 * k, col.ConAlfa(0f));
                    }
                    px = x; pb = base_; pa = alto; pal = a;
                }
            }
        }

        // ---- la onda de luz ----
        private void Onda(float W, float H, double t, float alfa)
        {
            int n = 96; float yb = H * 0.66f, amp = H * 0.075f, grueso = 18f * k * (1f + 0.4f * pGraves);
            Color4 luz = new Color4(0.45f, 0.52f, 0.95f, 0.16f * alfa), nada = new Color4(0.45f, 0.52f, 0.95f, 0f), filo = new Color4(0.62f, 0.70f, 1f, 0.28f * alfa);
            float px = 0, py = 0;
            for (int i = 0; i <= n; i++)
            {
                float x = W * i / n;
                double u = x / W;
                float y = yb + amp * (float)(Math.Sin(u * 2 * Math.PI * 1.15 + t * 0.22) * 0.75 + Math.Sin(u * 2 * Math.PI * 2.7 - t * 0.31) * 0.25);
                if (i > 0)
                {
                    d.Cuatro(px, py - grueso, nada, x, y - grueso, nada, x, y, luz, px, py, luz);
                    d.Cuatro(px, py, luz, x, y, luz, x, y + grueso, nada, px, py + grueso, nada);
                    d.Cuatro(px, py - 1.2f * k, filo, x, y - 1.2f * k, filo, x, y + 1.2f * k, filo, px, py + 1.2f * k, filo);
                }
                px = x; py = y;
            }
        }

        // ---- el horizonte ----
        private void HorizonteLuz(float W, float H, double t, float alfa)
        {
            float yh = Horizonte(H);
            Color4 azul = new Color4(0.42f, 0.47f, 0.95f, 0.16f * alfa), nada = new Color4(0.42f, 0.47f, 0.95f, 0f);   // medido en la de XP: un brillo leve
            d.Degradado(0, yh - 34 * k, W, 34 * k, nada, azul);
            d.Degradado(0, yh, W, 22 * k, azul, nada);
            d.Rect(0, yh - 1 * k, W, 2 * k, new Color4(0.62f, 0.66f, 1f, 0.45f * alfa));
            // un resplandor rosado que camina despacio por el horizonte
            float xr = W * (0.5f + 0.32f * (float)Math.Sin(t * 0.05));
            d.Brillo(xr, yh, 70 * k, new Color4(0.85f, 0.45f, 0.85f, 0.30f * alfa));
            d.Brillo(xr, yh, 16 * k, new Color4(1f, 0.75f, 1f, 0.45f * alfa));
        }

        // ---- la luna, en la fase de hoy ----
        private void Luna(float W, float H, double t, float alfa)
        {
            DateTime hoy = DateTime.UtcNow;
            int dia = (int)(hoy - new DateTime(2000, 1, 1)).TotalDays;
            if (luna == null || dia != lunaDia) { if (luna != null) d.Borra(luna); luna = HazLuna(128, FaseLunar(hoy)); lunaDia = dia; }
            float r = 33 * k, cx = W * 0.83f, cy = H * 0.22f + 3 * k * (float)Math.Sin(t * 0.15);
            d.Brillo(cx, cy, r * 3.2f, new Color4(0.75f, 0.8f, 1f, 0.16f * alfa));
            d.Imagen(luna, cx - r, cy - r, 2 * r, 2 * r, alfa);
        }
        // 0 = luna nueva, 0,5 = llena (mes sinodico desde la luna nueva del 6 de enero de 2000, 18:14 UTC).
        internal static double FaseLunar(DateTime utc)
        {
            double dias = (utc - new DateTime(2000, 1, 6, 18, 14, 0, DateTimeKind.Utc)).TotalDays;
            double f = dias / 29.530588853; return f - Math.Floor(f);
        }
        private Textura HazLuna(int n, double fase)
        {
            Random r = new Random(1969);
            double[] mx = new double[9], my = new double[9], mr = new double[9];
            for (int i = 0; i < 9; i++) { mx[i] = r.NextDouble() * 1.4 - 0.7; my[i] = r.NextDouble() * 1.4 - 0.7; mr[i] = 0.12 + r.NextDouble() * 0.25; }
            double ang = fase * 2 * Math.PI;
            double lx = Math.Sin(ang), lz = -Math.Cos(ang);   // de donde viene la luz (en el hemisferio norte, crece por la derecha)
            byte[] px = new byte[n * n * 4];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double u = (x + 0.5) / n * 2 - 1, v = (y + 0.5) / n * 2 - 1, rr = u * u + v * v;
                    int o = (y * n + x) * 4;
                    if (rr > 1) { px[o + 3] = 0; continue; }
                    double z = Math.Sqrt(1 - rr);
                    double albedo = 0.82;
                    for (int i = 0; i < 9; i++) { double dx = u - mx[i], dy = v - my[i]; double dd = (dx * dx + dy * dy) / (mr[i] * mr[i]); if (dd < 1) albedo -= 0.20 * (1 - dd); }
                    double luz = u * lx + z * lz;
                    double l = Math.Max(0, Math.Min(1, luz * 6 + 0.5));   // el terminador, suave
                    double b = albedo * (0.55 + 0.45 * z) * l + 0.07 * (1 - l);   // la parte oscura, con la luz de la tierra
                    double borde = Math.Min(1, (1 - Math.Sqrt(rr)) * n / 2);   // el canto sin dientes
                    px[o] = (byte)Math.Min(255, 255 * b * 0.97); px[o + 1] = (byte)Math.Min(255, 255 * b * 0.98); px[o + 2] = (byte)Math.Min(255, 255 * b);
                    px[o + 3] = (byte)(255 * Math.Max(0, Math.Min(1, borde)));
                }
            return d.Sube(px, n, n);
        }
    }
}
