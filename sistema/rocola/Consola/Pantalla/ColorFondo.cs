// EL COLOR DEL CIELO SEGUN LO QUE SUENA (2-11-2026), como el fondo Legacy de XP (Form1.Fondo.cs: ColorDeLoQueSuena, TinteConPortada,
// ColoresNebulosa, SkyColor/SkyColorMid/SkyColor2 y ColoresDePortada, con sus mismas cuentas). Con una cancion, el degradado del cielo
// toma un dejo del genero y las dos nebulosas se separan: una lleva el GENERO y la otra la PORTADA (de la portada solo se toma el TONO;
// el brillo y la viveza los pone el genero, asi ninguna portada puede lavar ni apagar la pantalla). Sin nada sonando, en un video o en
// la atraccion, el azul de siempre: el cielo sale exactamente como antes. Los colores llegan deslizandose (16 % por cuadro de 24/s).
// «Que el fondo tome el color de la portada» (fondo_portada=) apaga la parte de la portada, como en XP.
using System;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        // un color en 0..255 (las cuentas de XP son con Color de GDI+); A = 0: «no se sabe»
        internal struct C3 { public float R, G, B, A; public C3(float r, float g, float b, float a = 255) { R = r; G = g; B = b; A = a; } }
        private static C3 DeHex(uint rgb) { return new C3((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
        private static readonly C3 Defecto = new C3(45, 95, 205), SinColor = new C3(0, 0, 0, 0);
        private static float PortadaManda = 0.5f, AzulCielo = 1f;   // las perillas de la hoja Colores (se toman en cada cuadro)
        private const float TonoMinViveza = 0.22f, TonoMinBrillo = 0.14f, TonoMinAcuerdo = 0.35f;

        private static float TonoDe(C3 c)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f, mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), d = mx - mn;
            if (d < 0.0001f) return 0f;
            float h = mx == r ? 60f * (((g - b) / d) % 6f) : mx == g ? 60f * ((b - r) / d + 2f) : 60f * ((r - g) / d + 4f);
            while (h < 0f) h += 360f; while (h >= 360f) h -= 360f;
            return h;
        }
        private static C3 GiraTono(C3 c, float grados, float brillo)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f, mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            float v = mx, d = mx - mn, sat = mx <= 0.0001f ? 0f : d / mx, hue = 0f;
            if (d > 0.0001f) hue = mx == r ? 60f * (((g - b) / d) % 6f) : mx == g ? 60f * ((b - r) / d + 2f) : 60f * ((r - g) / d + 4f);
            hue += grados; while (hue < 0f) hue += 360f; while (hue >= 360f) hue -= 360f;
            v = Math.Min(1f, v * brillo);
            float cc = v * sat, x = cc * (1f - Math.Abs((hue / 60f) % 2f - 1f)), m = v - cc, rr, gg, bb;
            if (hue < 60f) { rr = cc; gg = x; bb = 0f; } else if (hue < 120f) { rr = x; gg = cc; bb = 0f; } else if (hue < 180f) { rr = 0f; gg = cc; bb = x; }
            else if (hue < 240f) { rr = 0f; gg = x; bb = cc; } else if (hue < 300f) { rr = x; gg = 0f; bb = cc; } else { rr = cc; gg = 0f; bb = x; }
            return new C3((int)((rr + m) * 255f + 0.5f), (int)((gg + m) * 255f + 0.5f), (int)((bb + m) * 255f + 0.5f));
        }
        private static C3 MezclaDeTono(C3 a, C3 b, float t)
        {
            float d = TonoDe(b) - TonoDe(a);
            while (d > 180f) d -= 360f; while (d < -180f) d += 360f;
            return GiraTono(a, d * t, 1f);
        }
        private static C3 TinteConPortada(C3 genero, C3 portada)
        {
            if (portada.A == 0) return genero;
            float mx = Math.Max(genero.R, Math.Max(genero.G, genero.B)), mn = Math.Min(genero.R, Math.Min(genero.G, genero.B));
            if (mx - mn < 2) return genero;
            return MezclaDeTono(genero, portada, PortadaManda);
        }
        private static C3 TonoSoloPortada(C3 portada) { return portada.A == 0 ? Defecto : MezclaDeTono(Defecto, portada, 1f); }
        private static C3 ColorNebulosa(C3 a) { return new C3((int)(a.R + 140) / 2, (int)(a.G + 120) / 2, (int)(a.B + 255) / 2); }
        private static C3 CompaneroDelGenero(C3 g)
        {
            float mx = Math.Max(g.R, Math.Max(g.G, g.B)), mn = Math.Min(g.R, Math.Min(g.G, g.B)), viveza = mx <= 0 ? 0f : (mx - mn) / mx;
            if (viveza >= TonoMinViveza) return GiraTono(g, 45f, 1f);
            return new C3((int)g.R * 82 / 100, (int)g.G * 92 / 100, Math.Min(255, (int)g.B * 125 / 100));
        }
        private static void ColoresNebulosa(C3 genero, C3 p1, C3 p2, float fase, out C3 deGenero, out C3 dePortada)
        {
            deGenero = ColorNebulosa(genero);
            C3 cp;
            if (p1.A != 0 && p2.A != 0) cp = TinteConPortada(genero, MezclaDeTono(p1, p2, 0.5f + 0.5f * (float)Math.Sin(fase)));
            else if (p1.A != 0) cp = TinteConPortada(genero, p1);
            else cp = CompaneroDelGenero(genero);
            dePortada = ColorNebulosa(cp);
        }
        // las tres paradas del cielo de XP (arriba, en medio y abajo), con su oscurecido (SkyDark 75) y el azul de fabrica (cielo_azul=100)
        private static C3 SkyArriba(C3 a) { return new C3(Math.Min(22, 3 + (int)a.R / 22), Math.Min(22, 4 + (int)a.G / 22), Math.Min(30, 8 + (int)a.B / 18)); }
        private static C3 SkyMedio(C3 a, float azul = 1f)
        {
            int r = Math.Min(60, (int)(22 * azul) + (int)a.R / 10), g = Math.Min(105, (int)(50 * azul) + (int)a.G / 8), b = Math.Min(205, (int)(120 * azul) + (int)a.B / 7);
            return new C3(r * 75 / 100, g * 75 / 100, b * 75 / 100);
        }
        private static C3 SkyAbajo(C3 a)
        {
            int mx = (int)Math.Max(a.R, Math.Max(a.G, a.B)), r, g, b;
            if (mx < 10) { r = 50; g = 88; b = 188; }
            else { r = ((int)a.R * 165 / mx * 3 + 50) / 4; g = ((int)a.G * 165 / mx * 3 + 88) / 4; b = ((int)a.B * 165 / mx * 3 + 188) / 4; }
            r = Math.Min(205, r); g = Math.Min(205, g); b = Math.Min(220, b);
            return new C3(r * 75 / 100, g * 75 / 100, b * 75 / 100);
        }

        // DE QUE COLOR ES UNA PORTADA (ColoresDePortada de XP): como mucho 32x32 puntos, sin los casi negros ni los casi grises; el
        // promedio (si los colores se ponen de acuerdo) y los dos tonos que mas mandan. Se mide una vez, al decodificarla.
        internal static void ColoresDePortada(byte[] px, int w, int h, out C3 prom, out C3 uno, out C3 dos)
        {
            prom = uno = dos = SinColor;
            if (px == null || w < 1 || h < 1) return;
            int pasoX = Math.Max(1, (w + 31) / 32), pasoY = Math.Max(1, (h + 31) / 32);
            double sx = 0, sy = 0, peso = 0; double[] cp = new double[24], cx = new double[24], cy = new double[24];
            for (int y = pasoY / 2; y < h; y += pasoY)
                for (int x = pasoX / 2; x < w; x += pasoX)
                {
                    int i = (y * w + x) * 4;
                    float r = px[i] / 255f, g = px[i + 1] / 255f, b = px[i + 2] / 255f, mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
                    if (mx < TonoMinBrillo) continue;
                    float d = mx - mn, sat = d / mx;
                    if (sat < TonoMinViveza) continue;
                    float hue = mx == r ? 60f * (((g - b) / d) % 6f) : mx == g ? 60f * ((b - r) / d + 2f) : 60f * ((r - g) / d + 4f);
                    if (hue < 0f) hue += 360f;
                    double pes = sat * mx * (px[i + 3] / 255.0), rad = hue * Math.PI / 180.0, ux = Math.Cos(rad) * pes, uy = Math.Sin(rad) * pes;
                    sx += ux; sy += uy; peso += pes;
                    int k = Math.Min(23, (int)(hue * 24 / 360f));
                    cp[k] += pes; cx[k] += ux; cy[k] += uy;
                }
            if (peso <= 0.0001) return;
            int p1 = 0; for (int k = 1; k < 24; k++) if (cp[k] > cp[p1]) p1 = k;
            if (cp[p1] > 0.0001)
            {
                float t1 = TonoDeCasilla(cx, cy, p1); uno = GiraTono(new C3(255, 0, 0), t1, 1f);
                int p2 = -1;
                for (int k = 0; k < 24; k++)
                {
                    if (cp[k] <= 0.0001) continue;
                    float dd = Math.Abs(TonoDeCasilla(cx, cy, k) - t1); if (dd > 180f) dd = 360f - dd;
                    if (dd < 40f) continue;
                    if (p2 < 0 || cp[k] > cp[p2]) p2 = k;
                }
                if (p2 >= 0 && cp[p2] >= cp[p1] * 0.25f) dos = GiraTono(new C3(255, 0, 0), TonoDeCasilla(cx, cy, p2), 1f);
            }
            if (Math.Sqrt(sx * sx + sy * sy) / peso < TonoMinAcuerdo) return;
            double ang = Math.Atan2(sy, sx) * 180.0 / Math.PI; if (ang < 0) ang += 360.0;
            prom = GiraTono(new C3(255, 0, 0), (float)ang, 1f);
        }
        private static float TonoDeCasilla(double[] cx, double[] cy, int k)
        {
            int a = (k + 23) % 24, b = (k + 1) % 24;
            double ang = Math.Atan2(cy[a] + cy[k] + cy[b], cx[a] + cx[k] + cx[b]) * 180.0 / Math.PI; if (ang < 0) ang += 360.0;
            return (float)ang;
        }

        // LO QUE SE PINTA: el acento (el degradado) y las dos nebulosas, deslizandose hacia lo que toca. Se llama una vez por cuadro.
        private C3 acento = Defecto, nebA = ColorNebulosa(Defecto), nebB = ColorNebulosa(CompaneroDelGenero(Defecto));
        private double colorT = -1, nebFase; private float corteNegro = 42;
        private void ColorDelCielo(Controlador c)
        {
            double t = reloj.Elapsed.TotalSeconds, dt = colorT < 0 ? 0 : Math.Max(0, Math.Min(0.5, t - colorT)); colorT = t;
            nebFase = (nebFase + 0.00654 * 24 * dt) % (2 * Math.PI);
            Form1 m = c.motor;
            PortadaManda = m.PortadaManda / 100f; AzulCielo = m.CieloAzul / 100f; corteNegro = Math.Max(25, Math.Min(66, m.CieloNegro));
            C3 meta = Defecto, gen = Defecto, p1 = SinColor, p2 = SinColor; bool sinGenero = false;
            string titulo = m.SonandoTitulo;
            if (titulo != null && !c.Atraccion && !m.SonandoVideo)
            {
                uint cg = m.ColorSonando;
                sinGenero = cg == 0;
                gen = sinGenero ? Defecto : DeHex(cg);
                C3 prom = SinColor;
                Textura tp = m.FondoPortada && m.SonandoPortada != null ? caratulas.De(m.SonandoPortada) : null;
                if (tp != null) { prom = tp.Prom; p1 = tp.Uno; p2 = tp.Dos; }
                meta = sinGenero && m.FondoPortada ? TonoSoloPortada(prom.A != 0 ? prom : p1) : TinteConPortada(gen, prom);
            }
            C3 mA, mB;
            ColoresNebulosa(gen, p1, p2, (float)nebFase, out mA, out mB);
            if (sinGenero && m.FondoPortada && p1.A != 0) mA = mB = ColorNebulosa(TonoSoloPortada(p2.A == 0 ? p1 : MezclaDeTono(p1, p2, 0.5f + 0.5f * (float)Math.Sin(nebFase))));
            float f = (float)(1 - Math.Pow(0.84, dt * 24));
            acento = Hacia(acento, meta, f); nebA = Hacia(nebA, mA, f); nebB = Hacia(nebB, mB, f);
        }
        private static C3 Hacia(C3 a, C3 b, float f) { return new C3(a.R + (b.R - a.R) * f, a.G + (b.G - a.G) * f, a.B + (b.B - a.B) * f); }

        // Las paradas de Linux (medidas en la pantalla de XP con el azul de siempre) se corren lo mismo que se corren las de XP: con el azul
        // de siempre salen tal cual.
        private Color4 Parada(uint baseHex, C3 xpAhora, C3 xpDefecto, float alfa)
        {
            C3 b = DeHex(baseHex);
            return new Color4(Math.Max(0, Math.Min(255, b.R + xpAhora.R - xpDefecto.R)) / 255f, Math.Max(0, Math.Min(255, b.G + xpAhora.G - xpDefecto.G)) / 255f,
                Math.Max(0, Math.Min(255, b.B + xpAhora.B - xpDefecto.B)) / 255f, alfa);
        }
        private Color4 TinteNebulosa(uint baseHex, C3 ahora, C3 defecto, float alfa)
        {
            C3 b = DeHex(baseHex);
            return new Color4(Math.Min(255, b.R * ahora.R / Math.Max(1, defecto.R)) / 255f, Math.Min(255, b.G * ahora.G / Math.Max(1, defecto.G)) / 255f,
                Math.Min(255, b.B * ahora.B / Math.Max(1, defecto.B)) / 255f, alfa);
        }
        private static readonly C3 NebADefecto = ColorNebulosa(Defecto), NebBDefecto = ColorNebulosa(CompaneroDelGenero(Defecto));
    }
}
