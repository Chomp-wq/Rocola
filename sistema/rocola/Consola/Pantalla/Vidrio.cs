// LAS VENTANAS DE VIDRIO (decision del 3-11-2026, presentacion «Rocola OS: vidrio y letra», G1-G11). Un panel de vidrio son capas,
// no un cuadro plano: la sombra que lo despega, lo de detras desenfocado (DibujoVidrio.cs), un velo oscuro para que se lea y encima el
// tinte del color del disco (que sube un poco con los graves), un reflejo que cruza despacio y el filo de luz con su canto brillante.
// Las pastillas chicas (teclas, botones, la cola y los creditos) son vidrio sin fondo: el velo, el tinte y el filo.
using System;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        private static readonly Color4 VeloVidrio = new Color4(0.035f, 0.05f, 0.15f, 1f);
        private bool vidrioCieloUsado, vidrioCieloPedido;   // si algun vidrio leyo el cielo borroso en este cuadro; si hay que hacerlo en este

        // Una capa de color c1 con alfa a1 y encima c2 con a2, como una sola (se dibuja una vez en vez de dos).
        private static Color4 Compone(Color4 c1, float a1, Color4 c2, float a2)
        {
            float a = 1f - (1f - a1) * (1f - a2);
            if (a <= 0f) return new Color4(0, 0, 0, 0);
            return new Color4((c1.R * a1 * (1f - a2) + c2.R * a2) / a, (c1.G * a1 * (1f - a2) + c2.G * a2) / a, (c1.B * a1 * (1f - a2) + c2.B * a2) / a, a);
        }

        // Un panel de vidrio. 'oscuro': cuanto velo (0,3 deja ver mucho; 0,6 para paneles con mucho texto). 'deVerdad': desenfoca lo que
        // hay detras (portadas incluidas); si no, lee el cielo borroso. 'luz': cuanto tinte (1 normal).
        private void Vidrio(float x, float y, float w, float h, float r, Color4 tinte, float alfa, bool deVerdad, float oscuro, float luz = 1f)
        {
            if (alfa <= 0.01f || w < 4 || h < 4) return;
            r = Math.Min(r, Math.Min(w, h) / 2f);
            // la sombra: tres aros por fuera, cada vez mas tenues (lo de dentro lo tapa el vidrio)
            for (int i = 1; i <= 3; i++)
            {
                float e = i * 3 * k;
                d.Contorno(x - e, y - e + 2 * i * k, w + 2 * e, h + 2 * e, r + e, 3 * k, new Color4(0.01f, 0.02f, 0.07f, (0.17f - 0.045f * i) * alfa));
            }
            Dibujo.Fondo f = deVerdad ? d.DesenfocaDetras(x, y, w, h) : d.CieloBorroso;
            if (!deVerdad) vidrioCieloUsado = true;
            bool hay = f.Hay;
            float velo = hay ? oscuro : Math.Min(0.92f, oscuro + 0.35f), vida = 0.05f * pGraves;
            // velo, tinte y el brillo blanco del vidrio (mas arriba que abajo), en una sola capa
            Color4 arriba = Compone(VeloVidrio, velo, tinte, (0.26f + vida) * luz), abajo = Compone(VeloVidrio, Math.Min(0.95f, velo * 1.15f), tinte, 0.10f * luz);
            arriba = Compone(arriba, arriba.A, Blanco, 0.13f); abajo = Compone(abajo, abajo.A, Blanco, 0.035f);
            if (hay && alfa >= 0.999f) d.RedondoVidrio(x, y, w, h, r, f, arriba, abajo);   // de una pasada, opaco
            else
            {   // entrando o saliendo (translucido), o sin fondo: lo de detras y el velo encima, cada uno con su alfa
                if (hay) d.RedondoFondo(x, y, w, h, r, f, new Color4(1f, 1f, 1f, alfa));
                d.RedondoDegradado(x, y, w, h, r, arriba.ConAlfa(arriba.A * alfa), abajo.ConAlfa(abajo.A * alfa));
            }
            ReflejoVidrio(x, y, w, h, r, alfa);
            d.Contorno(x, y, w, h, r, Math.Max(1f, 1.3f * k), new Color4(1f, 1f, 1f, 0.34f * alfa));
            float ri = r * 0.9f;
            if (w > 2 * ri + 4) d.Degradado(x + ri, y + 1.2f * k, w - 2 * ri, 1.8f * k, new Color4(1f, 1f, 1f, 0.55f * alfa), new Color4(1f, 1f, 1f, 0f));
        }
        // El reflejo: una franja inclinada de luz que cruza el panel despacio (cada 9 s, desfasado por su sitio), siempre por dentro.
        private void ReflejoVidrio(float x, float y, float w, float h, float r, float alfa)
        {
            double t = reloj.Elapsed.TotalSeconds + x * 0.004 + y * 0.002;
            float p = (float)((t % 9.0) / 9.0);
            if (p > 0.45f) return;
            float q = p / 0.45f, s = h * 0.45f, bw = Math.Max(18 * k, w * 0.10f);
            float x0 = x + r + s, x1 = x + w - r - bw;
            if (x1 <= x0) return;
            float xb = x0 + (x1 - x0) * q, a = (float)Math.Sin(q * Math.PI) * 0.09f * alfa;
            Color4 nada = new Color4(1f, 1f, 1f, 0f), luz = new Color4(1f, 1f, 1f, a);
            d.Cuatro(xb, y, nada, xb + bw / 2, y, luz, xb + bw / 2 - s, y + h, luz, xb - s, y + h, nada);
            d.Cuatro(xb + bw / 2, y, luz, xb + bw, y, nada, xb + bw - s, y + h, nada, xb + bw / 2 - s, y + h, luz);
        }
        // Una pastilla de vidrio (sin fondo propio): 'clara' para la elegida.
        private void PildoraVidrio(float x, float y, float w, float h, float r, Color4 tinte, float alfa, bool clara)
        {
            if (alfa <= 0.01f) return;
            Color4 arriba = Compone(VeloVidrio, clara ? 0.20f : 0.30f, tinte, clara ? 0.62f : 0.22f), abajo = Compone(VeloVidrio, clara ? 0.30f : 0.42f, tinte, clara ? 0.42f : 0.08f);
            d.RedondoDegradado(x, y, w, h, r, arriba.ConAlfa(arriba.A * alfa), abajo.ConAlfa(abajo.A * alfa));
            d.Contorno(x, y, w, h, r, Math.Max(1f, 1f * k), new Color4(1f, 1f, 1f, (clara ? 0.55f : 0.28f) * alfa));
            float ri = Math.Min(r, h / 2f) * 0.9f;
            if (w > 2 * ri + 4) d.Degradado(x + ri, y + 1f * k, w - 2 * ri, 1.5f * k, new Color4(1f, 1f, 1f, (clara ? 0.6f : 0.38f) * alfa), new Color4(1f, 1f, 1f, 0f));
        }
        // El color del disco que suena (o el azul de la rocola), para teñir el vidrio.
        private Color4 TinteSonando(Form1 m)
        {
            if (m.ColorGenero && m.ColorSonando != 0) return Color4.Hex(m.ColorSonando);
            Textura tx = m.SonandoPortada != null ? caratulas.De(m.SonandoPortada) : null;
            return tx != null ? tx.Media : Color4.Hex(0x6F8CFF);
        }
    }
}
