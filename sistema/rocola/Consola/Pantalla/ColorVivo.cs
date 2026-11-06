// COLOR VIVO (decision del 7-11-2026, maqueta «Rocola OS: mas color», la opcion Vivo: C1-C6, C9-C11; C7 y C8 no). Mas color que la de
// XP, con los vidrios: textos del color de su genero (C1), titulos en degradado que corre (C2), la barra de letras de colores (C3), el
// vidrio del color de lo que suena con su cambio suave (C4), un borde de neon que da vueltas (C5), el vidrio que late con el bajo (C6),
// portadas que brillan con su color (C9), un destello al pedir (C10) y la barra de avance de neon (C11).
// LA CALMA MIENTRAS SE USA (como siempre en la rocola: el reposo y la estrella ya lo hacian): la «viveza» va de 1 (nadie toca) a 0,35
// (alguien eligiendo). Baja en 0,4 s al primer boton y vuelve en 3 s cuando pasan 15 s sin botones (o en la atraccion). Todo lo que se
// mueve o brilla se multiplica por ella; los movimientos, ademas, van mas despacio (el degradado corre a la velocidad de la viveza).
using System;
using System.Collections.Generic;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        internal const float VivezaEnUso = 0.35f;
        private float viveza = 1f; private double vivezaT, faseDegradado;
        // C4: el color de lo que suena, que pasa de uno a otro en 1,5 s
        private Color4 vivo1 = Color4.Hex(0x6F8CFF), vivo2 = Color4.Hex(0x9A6CFF), vivo3 = Color4.Hex(0x4FC3FF), vivoDesde, vivoMeta = Color4.Hex(0x6F8CFF);
        private double vivoCambio = -10;
        private static readonly Color4 AzulVidrio = Color4.Hex(0x6F8CFF);
        private static readonly Color4[] PaletaDegradado = { Color4.Hex(0x5FF3FF), Color4.Hex(0xFF5FD2), Color4.Hex(0xFFC928) };

        // Una vez por cuadro: la viveza, el color vivo y el paso del degradado.
        private void ColorVivoAlDia(Controlador c)
        {
            Form1 m = c.motor;
            double t = reloj.Elapsed.TotalSeconds, dt = Math.Max(0, Math.Min(0.1, t - vivezaT)); vivezaT = t;
            bool enUso = !c.Atraccion && (c.MsSinTocar < 15000 || c.AjusteSel >= 0 || c.Buscando || c.AyudaAbierta || c.AsistentePaso >= 0);
            if (enUso) viveza = Math.Max(VivezaEnUso, viveza - (float)(dt / 0.4));
            else viveza = Math.Min(1f, viveza + (float)(dt / 3.0));
            faseDegradado += dt / 6.0 * viveza;   // una vuelta cada 6 s en calma; casi quieto mientras se usa
            if (faseDegradado > 1000) faseDegradado -= 1000;
            Color4 meta = m.Sonando != null ? ColorSonandoVivo(m) : AzulVidrio;
            if (Distinto(meta, vivoMeta)) { vivoDesde = vivo1; vivoMeta = meta; vivoCambio = t; }
            float u = (float)Math.Min(1.0, (t - vivoCambio) / 1.5); u = u * u * (3 - 2 * u);
            vivo1 = u >= 1f ? vivoMeta : Dibujo.Mezcla(vivoDesde, vivoMeta, u);
            vivo2 = GiraTono(vivo1, 45f); vivo3 = GiraTono(vivo1, -40f);
        }
        private static bool Distinto(Color4 a, Color4 b) { return Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) > 0.01f; }

        // ---- el color en tono, saturacion y brillo ----
        private static void Thb(Color4 c, out float h, out float s, out float v)
        {
            float mx = Math.Max(c.R, Math.Max(c.G, c.B)), mn = Math.Min(c.R, Math.Min(c.G, c.B)), dd = mx - mn;
            v = mx; s = mx > 1e-5f ? dd / mx : 0f; h = 0f;
            if (dd > 1e-5f)
            {
                if (mx == c.R) h = 60f * (((c.G - c.B) / dd) % 6f);
                else if (mx == c.G) h = 60f * ((c.B - c.R) / dd + 2f);
                else h = 60f * ((c.R - c.G) / dd + 4f);
                if (h < 0) h += 360f;
            }
        }
        private static Color4 DeThb(float h, float s, float v, float a)
        {
            h = ((h % 360f) + 360f) % 360f;
            float cc = v * s, x = cc * (1 - Math.Abs((h / 60f) % 2 - 1)), m0 = v - cc, r, g, b;
            if (h < 60) { r = cc; g = x; b = 0; } else if (h < 120) { r = x; g = cc; b = 0; } else if (h < 180) { r = 0; g = cc; b = x; }
            else if (h < 240) { r = 0; g = x; b = cc; } else if (h < 300) { r = x; g = 0; b = cc; } else { r = cc; g = 0; b = x; }
            return new Color4(r + m0, g + m0, b + m0, a);
        }
        // Un color mas vivo: al menos 55 % de saturacion y 85 % de brillo (un gris se queda en el azul de la rocola).
        private static Color4 Vivido(Color4 c)
        {
            float h, s, v; Thb(c, out h, out s, out v);
            if (s < 0.08f) return AzulVidrio.ConAlfa(c.A);
            return DeThb(h, Math.Max(s, 0.55f), Math.Max(v, 0.85f), c.A);
        }
        private static Color4 GiraTono(Color4 c, float grados) { float h, s, v; Thb(c, out h, out s, out v); return DeThb(h + grados, s, v, c.A); }

        // El color de un disco: el de su portada si tiene color (asi combina con ella), si no el de su genero si ya se sabe (y no es
        // «Otros»); vivido. 'pide': que se lea su genero si hace falta.
        private Color4 ColorDeDisco(Form1 m, Form1.Disco disco, bool pide)
        {
            Textura tx = disco != null ? caratulas.De(disco.Portada) : null;
            return ColorDe(m, tx, pide ? (disco != null && TieneColor(tx) ? "" : m.GeneroDeDisco(disco)) : m.GeneroSabido(disco));
        }
        private Color4 ColorSonandoVivo(Form1 m)
        {
            return ColorDe(m, m.SonandoPortada != null ? caratulas.De(m.SonandoPortada) : null, m.EtiquetaSonando);
        }
        private static bool TieneColor(Textura tx) { if (tx == null) return false; float h, s, v; Thb(tx.Media, out h, out s, out v); return s >= 0.20f && v >= 0.12f; }
        private static Color4 ColorDe(Form1 m, Textura tx, string genero)
        {
            if (TieneColor(tx)) return Vivido(tx.Media);
            if (!string.IsNullOrEmpty(genero) && genero != "Otros") return Vivido(Color4.Hex(Form1.ColorDeEtiqueta(genero)));
            return AzulVidrio;
        }
        // El tinte de los vidrios que no son de un disco (cabecera, barra de letras): con C4 el de lo que suena.
        private Color4 TinteVidrio(Form1 m) { return m.VidrioColor ? vivo1 : AzulVidrio; }

        // ---- C2: un texto en degradado que corre ----
        private static readonly string[] UnaLetra = Tabla1();
        private static string[] Tabla1() { string[] t = new string[256]; for (int i = 0; i < 256; i++) t[i] = ((char)i).ToString(); return t; }
        private static string Letra1(char ch) { return ch < 256 ? UnaLetra[ch] : ch.ToString(); }
        // El color del degradado en 'p' (0 a 1 da una vuelta a la paleta), mas palido mientras se usa la rocola.
        private Color4 ColorDegradado(double p, float alfa)
        {
            double q = (p % 1.0 + 1.0) % 1.0 * PaletaDegradado.Length;
            int i = (int)q; float f = (float)(q - i); f = f * f * (3 - 2 * f);
            Color4 c = Dibujo.Mezcla(PaletaDegradado[i % PaletaDegradado.Length], PaletaDegradado[(i + 1) % PaletaDegradado.Length], f);
            return Dibujo.Mezcla(c, Blanco, (1f - viveza) * 0.45f).ConAlfa(alfa);
        }
        // Escribe 's' letra a letra con el degradado (cada letra del color de su sitio); 'aire': espacio de mas entre letras.
        private float EscribeDegradado(Letra f, string s, float x, float y, float aire, float alfa = 1f)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            float x0 = x, ancho = Math.Max(1f, f.Mide(s) + aire * s.Length);
            foreach (char ch in s)
            {
                string l = Letra1(ch); float w = f.Mide(l);
                double p = (x + w / 2 - x0) / ancho * 0.6 - faseDegradado;
                f.Escribe(d, l, x, y, ColorDegradado(p, alfa));
                x += w + aire;
            }
            return x - x0;
        }

        // ---- C5: el borde de neon que da vueltas, y C6: el brillo que late con el bajo ----
        // Un punto del borde de un rectangulo redondeado, por su avance 's' (0 a 1) desde arriba a la izquierda, en el sentido del reloj.
        private static void PuntoDelBorde(float x, float y, float w, float h, float r, float s, out float px, out float py)
        {
            px = 0; py = 0;
            float lh = Math.Max(0, w - 2 * r), lv = Math.Max(0, h - 2 * r), arco = (float)(Math.PI / 2) * r, total = 2 * lh + 2 * lv + 4 * arco;
            float u = (s % 1f + 1f) % 1f * total;
            if (u < lh) { px = x + r + u; py = y; return; } u -= lh;
            if (u < arco) { double a = -Math.PI / 2 + u / Math.Max(1e-3f, r); px = x + w - r + r * (float)Math.Cos(a); py = y + r + r * (float)Math.Sin(a); return; } u -= arco;
            if (u < lv) { px = x + w; py = y + r + u; return; } u -= lv;
            if (u < arco) { double a = u / Math.Max(1e-3f, r); px = x + w - r + r * (float)Math.Cos(a); py = y + h - r + r * (float)Math.Sin(a); return; } u -= arco;
            if (u < lh) { px = x + w - r - u; py = y + h; return; } u -= lh;
            if (u < arco) { double a = Math.PI / 2 + u / Math.Max(1e-3f, r); px = x + r + r * (float)Math.Cos(a); py = y + h - r + r * (float)Math.Sin(a); return; } u -= arco;
            if (u < lv) { px = x; py = y + h - r - u; return; } u -= lv;
            { double a = Math.PI + u / Math.Max(1e-3f, r); px = x + r + r * (float)Math.Cos(a); py = y + r + r * (float)Math.Sin(a); }
        }
        // Lo vivo de un vidrio grande (lo que suena, el disco abierto, el reposo): el latido (C6) por fuera y el neon (C5) en el filo.
        private void VidrioVivo(Form1 m, float x, float y, float w, float h, float r, Color4 tinte, float alfa)
        {
            if (alfa <= 0.01f) return;
            if (m.VidrioLate)
            {
                float a = (0.05f + 0.30f * pGraves) * viveza * alfa;
                for (int i = 1; i <= 3; i++) { float e = i * 2.5f * k; d.Contorno(x - e, y - e, w + 2 * e, h + 2 * e, r + e, 2.5f * k, tinte.ConAlfa(a * (1f - 0.28f * i))); }
            }
            if (m.NeonBorde)
            {
                // una cola de luz del 20 % del borde: del color del disco a blanco en la punta; una vuelta cada 4,5 s (mas despacio
                // mientras se usa la rocola, con el mismo paso que el degradado)
                const int n = 26; float cola = 0.20f, cabeza = (float)(faseDegradado * 6.0 / 4.5 % 1.0);
                float a0 = 0.85f * viveza * alfa, g = 2.2f * k;
                float px0, py0; PuntoDelBorde(x, y, w, h, r, cabeza - cola, out px0, out py0);
                for (int i = 1; i <= n; i++)
                {
                    float f = i / (float)n, px1, py1;
                    PuntoDelBorde(x, y, w, h, r, cabeza - cola + cola * f, out px1, out py1);
                    Color4 c = Dibujo.Mezcla(tinte, Blanco, f * f * 0.6f).ConAlfa(a0 * f * f);
                    d.Linea(px0, py0, px1, py1, g, c);
                    px0 = px1; py0 = py1;
                }
                d.Brillo(px0, py0, 18 * k, tinte.ConAlfa(0.45f * a0));
                d.Brillo(px0, py0, 5 * k, Blanco.ConAlfa(0.8f * a0));
            }
        }

        // ---- C3: el color de cada letra de la barra ----
        // El genero que mas se repite entre los discos de la letra cuyo genero ya se sabe (no se pide nada: se va sabiendo al ver los
        // discos); sin ninguno, un color por su sitio en el abecedario. Se rehace cada 2 s.
        private Color4[] colorLetras = new Color4[0]; private double colorLetrasT = -10; private int colorLetrasN = -1;
        private Color4 ColorDeLetra(Controlador c, int i)
        {
            double t = reloj.Elapsed.TotalSeconds;
            if (colorLetrasN != c.letras.Count || t - colorLetrasT > 2.0)
            {
                colorLetrasT = t; colorLetrasN = c.letras.Count;
                if (colorLetras.Length != c.letras.Count) colorLetras = new Color4[c.letras.Count];
                Dictionary<string, int> cuenta = new Dictionary<string, int>();
                for (int j = 0; j < c.letras.Count; j++)
                {
                    cuenta.Clear();
                    foreach (Form1.Disco x in c.DiscosEnLetra(j))
                    {
                        string g = c.motor.GeneroSabido(x);
                        if (g.Length == 0 || g == "Otros") continue;
                        int n; cuenta.TryGetValue(g, out n); cuenta[g] = n + 1;
                    }
                    string mejor = null; int mx = 0;
                    foreach (KeyValuePair<string, int> kv in cuenta) if (kv.Value > mx) { mx = kv.Value; mejor = kv.Key; }
                    colorLetras[j] = mejor != null ? Vivido(Color4.Hex(Form1.ColorDeEtiqueta(mejor))) : DeThb(200f + 300f * j / Math.Max(1, c.letras.Count), 0.55f, 1f, 1f);
                }
            }
            return i >= 0 && i < colorLetras.Length ? colorLetras[i] : AzulVidrio;
        }

        // ---- C10: el destello al pedir ----
        private float destelloX, destelloY; private Color4 destelloColor; private int destelloTick;
        private void Destello(float x, float y, Color4 c) { destelloX = x; destelloY = y; destelloColor = c; destelloTick = Environment.TickCount | 1; }
        private void DibujaDestello()
        {
            if (destelloTick == 0) return;
            int e = unchecked(Environment.TickCount - destelloTick);
            if (e < 0 || e > 700) { destelloTick = 0; return; }
            // corto y suave: un aro que se abre y un resplandor que se apaga en 0,7 s (al 70 %: se pide con alguien mirando)
            float u = e / 700f, a = (1f - u) * (1f - u) * 0.7f, rr = (14 + 90 * (1 - (1 - u) * (1 - u))) * k;
            d.Brillo(destelloX, destelloY, rr * 1.2f, destelloColor.ConAlfa(0.35f * a));
            d.Contorno(destelloX - rr, destelloY - rr, 2 * rr, 2 * rr, rr, 3f * k * (1f - u) + 1f, Dibujo.Mezcla(destelloColor, Blanco, 0.3f).ConAlfa(a));
        }

        // ---- C11: la barra de avance de neon ----
        // La barra de 'x' a x + w (ya lleno), alto 'h': degradado de vivo3 a vivo1 a vivo2 y un brillo que pulsa con el bajo.
        private void BarraDeNeon(float x, float y, float w, float h)
        {
            if (w < h) w = h;
            float r = h / 2, mid = x + w / 2;
            d.Brillo(mid, y + r, w * 0.55f, vivo1.ConAlfa((0.10f + 0.30f * pGraves) * viveza));
            Circulo(x + r, y + r, r, vivo3);
            if (w > 2 * h)
            {
                d.DegradadoH(x + r, y, mid - x - r, h, vivo3, vivo1);
                d.DegradadoH(mid, y, x + w - r - mid, h, vivo1, vivo2);
            }
            else d.Rect(x + r, y, Math.Max(0, w - h), h, vivo1);
            Circulo(x + w - r, y + r, r, vivo2);
            d.Degradado(x + r, y, Math.Max(0, w - h), h * 0.45f, Blanco.ConAlfa(0.35f), Blanco.ConAlfa(0f));   // el reflejo de arriba
            d.Brillo(x + w, y + r, 16 * k, Dibujo.Mezcla(vivo2, Blanco, 0.5f).ConAlfa(0.65f));
            d.Brillo(x + w, y + r, 5 * k, Blanco);
        }
    }
}
