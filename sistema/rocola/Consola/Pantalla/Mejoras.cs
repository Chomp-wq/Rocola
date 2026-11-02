// LAS MEJORAS VISUALES DE ROCOLA OS EN LA PANTALLA (Motor/MejorasVisuales.cs tiene sus interruptores; las eligio el dueño el 2-11-2026).
// Tanda 1: el espectro sobre el horizonte (V11), la barra con la forma de la cancion (V10), el ecualizador de verdad (V9), el cambio de
// titulo con transicion (V12), el cubo con luz y su sombra (V5), la moneda de metal (V22) y el teclado que se siente (V16).
// Todo con lo que ya hay (rectangulos, degradados y el punto suave): sin programas nuevos en la tarjeta.
using System;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        // ---- el espectro de lo que se oye, suavizado: sube al instante y cae despacio; los picos esperan un poco y caen ----
        private readonly float[] espCrudo = new float[Form1.BandasEspectro], esp = new float[Form1.BandasEspectro], picos = new float[Form1.BandasEspectro];
        private readonly double[] picoDesde = new double[Form1.BandasEspectro];
        private double espT = -1;
        private bool hayEspectro;
        private void EspectroAlDia(Controlador c)
        {
            double t = reloj.Elapsed.TotalSeconds, dt = espT < 0 ? 0 : Math.Max(0, Math.Min(0.2, t - espT)); espT = t;
            long oido = c.CuadrosOidos;
            hayEspectro = oido >= 0 && c.motor.Espectro(oido, espCrudo);
            float caida = (float)Math.Pow(0.08, dt);   // en 1 s queda el 8 %
            for (int i = 0; i < esp.Length; i++)
            {
                float v = hayEspectro ? espCrudo[i] : 0f;
                esp[i] = v > esp[i] ? v : esp[i] * caida;
                if (esp[i] >= picos[i]) { picos[i] = esp[i]; picoDesde[i] = t; }
                else if (t - picoDesde[i] > 0.35) picos[i] = Math.Max(esp[i], picos[i] - (float)(dt * 0.9));
            }
        }

        // V11: las 16 bandas, como barras finas sobre la linea del horizonte, del color de lo que suena (o el cian de la rocola)
        private void EspectroHorizonte(Controlador c, float W, float H)
        {
            Form1 m = c.motor;
            if (!m.EspectroFranja || m.SonandoTitulo == null || m.SonandoVideo) return;
            float yh = Horizonte(H), alto = 46 * k, ancho = W / (Form1.BandasEspectro * 2f);
            uint cg = m.ColorGenero ? m.ColorSonando : 0;
            Color4 col = cg != 0 ? Dibujo.Mezcla(Color4.Hex(cg), Blanco, 0.35f) : Color4.Hex(0x58D2FF);
            for (int i = 0; i < Form1.BandasEspectro * 2; i++)
            {
                // 32 barras: cada banda y su punto medio con la siguiente (asi no se ven escalones de 16)
                int b = i / 2; float v = i % 2 == 0 ? esp[b] : (esp[b] + esp[Math.Min(Form1.BandasEspectro - 1, b + 1)]) / 2;
                float p = i % 2 == 0 ? picos[b] : (picos[b] + picos[Math.Min(Form1.BandasEspectro - 1, b + 1)]) / 2;
                float x = i * ancho + ancho * 0.18f, w = ancho * 0.64f, h = Math.Max(1.5f * k, alto * v);
                d.Degradado(x, yh - h, w, h, col.ConAlfa(0.05f), col.ConAlfa(0.38f));
                if (p > 0.03f) d.Rect(x, yh - alto * p - 2 * k, w, 2 * k, col.ConAlfa(0.55f));
            }
        }

        // V9: las cuatro barritas, con el sonido de verdad (graves, medios bajos, medios altos, agudos), sin vaiven inventado
        private bool BarritasReales(float x, float y, float w, float h, Color4 c)
        {
            if (!hayEspectro) return false;
            float bw = w / 4 - 2 * k;
            for (int i = 0; i < 4; i++)
            {
                float v = 0; for (int j = 0; j < 4; j++) v = Math.Max(v, esp[i * 4 + j]);
                v = Math.Max(0.12f, Math.Min(1f, v * 1.15f));
                d.Rect(x + i * (bw + 2 * k), y + h * (1 - v), bw, h * v, c);
            }
            return true;
        }

        // V10: la barra de avance con la silueta del volumen de la cancion; lo ya sonado, en neon. false si aun no se sabe la forma.
        private bool BarraConForma(Form1 m, float xb, float yb, float wb, float hb, float p, Color4 neon)
        {
            float[] f = m.FormaDe(m.Sonando);
            if (f == null) return false;
            int n = Math.Max(24, Math.Min(f.Length, (int)(wb / (3 * k))));
            float paso = wb / n, cy = yb + hb / 2, maxH = hb * 2.6f;
            for (int i = 0; i < n; i++)
            {
                int a = i * f.Length / n, z = Math.Max(a + 1, (i + 1) * f.Length / n); float v = 0;
                for (int j = a; j < z; j++) v = Math.Max(v, f[j]);
                float h = Math.Max(hb * 0.35f, maxH * v), x = xb + i * paso;
                bool sonado = (i + 0.5f) / n <= p;
                Color4 col = sonado ? Dibujo.Mezcla(neon, Blanco, 0.35f * v) : Color4.Hex(0x2A3A78, 0.95f);
                d.Rect(x + paso * 0.15f, cy - h / 2, Math.Max(1f, paso * 0.7f), h, col);
            }
            float xp = xb + wb * p;
            d.Brillo(xp, cy, 16 * k, Blanco.ConAlfa(0.55f));
            d.Rect(xp - 1.2f * k, cy - maxH / 2 - 2 * k, 2.4f * k, maxH + 4 * k, Blanco);
            return true;
        }

        // V12: el titulo de lo que suena; al cambiar, el viejo sube y se apaga y el nuevo entra desde abajo (380 ms)
        private string tituloVisto, tituloViejo; private double tituloDesde = -10;
        private void TituloConTransicion(Controlador c, Letra l, string t, float x, float y, float ancho, Color4 col)
        {
            double ahora = reloj.Elapsed.TotalSeconds;
            if (!string.Equals(t, tituloVisto)) { tituloViejo = tituloVisto; tituloVisto = t; tituloDesde = ahora; }
            float u = c.motor.TituloTransicion && tituloViejo != null ? Suave((float)((ahora - tituloDesde) / 0.38)) : 1f;
            if (u >= 1f) { EscribeDesliza(c, l, t, x, y, ancho, col); return; }
            float mov = 18 * k;
            d.Recorta(x, y - 4 * k, ancho, l.Tam * 1.4f + 8 * k);
            l.Escribe(d, l.Ajusta(tituloViejo, ancho), x, y - mov * u, col.ConAlfa(1f - u));
            l.Escribe(d, l.Ajusta(t, ancho), x, y + mov * (1f - u), col.ConAlfa(u));
            d.SinRecorte();
        }

        // V5: el brillo que recorre una cara del cubo (una franja de luz que se corre con el giro) y la sombra en el piso
        private void LuzDeCara(float iz, float de, float y, float lado, double anguloCara, double frente)
        {
            if (frente <= 0.05) return;
            float u = (float)(0.5 - 0.5 * Math.Cos(anguloCara)), a = (float)(0.30 * Math.Pow(frente, 3)), w = (de - iz) * 0.30f;
            float xc = iz + (de - iz) * u;
            float x0 = Math.Max(iz, xc - w), x1 = Math.Min(de, xc + w);
            if (x1 - x0 < 1f) return;
            Color4 nada = new Color4(1, 1, 1, 0), luz = new Color4(1, 1, 1, a);
            if (xc > x0) d.DegradadoH(x0, y, xc - x0, lado, nada, luz);
            if (x1 > xc) d.DegradadoH(xc, y, x1 - xc, lado, luz, nada);
        }
        private void SombraEnElPiso(float cx, float abajo, float ancho)
        {
            d.Quad(d.Punto, cx - ancho * 0.55f, abajo - ancho * 0.07f, ancho * 1.1f, ancho * 0.2f, 0, 0, 1, 1, new Color4(0, 0, 0.03f, 0.55f));
        }

        // V22: la moneda que cae, con brillo de metal (degradado, canto oscuro y un reflejo) y un rebote al llegar a los creditos
        private void MonedaDeMetal(float cx, float cy, float r, float giro)
        {
            float rx = Math.Max(2 * k, r * giro);
            d.Brillo(cx, cy, r * 2.4f, Dorado.ConAlfa(0.30f));
            d.RedondoDegradado(cx - rx - 1.5f * k, cy - r, 2 * rx + 3 * k, 2 * r, rx + 1.5f * k, Color4.Hex(0xB07A1E), Color4.Hex(0x6A420A));   // el canto
            d.RedondoDegradado(cx - rx, cy - r, 2 * rx, 2 * r, rx, Color4.Hex(0xFFF1B8), Color4.Hex(0xC98A22));
            if (rx > 4 * k)
            {
                d.Contorno(cx - rx * 0.72f, cy - r * 0.72f, 1.44f * rx, 1.44f * r, rx * 0.72f, Math.Max(1f, 1.4f * k), Color4.Hex(0x9A5E12, 0.55f));
                d.Brillo(cx - rx * 0.35f, cy - r * 0.45f, r * 0.45f * giro, Blanco.ConAlfa(0.8f));
            }
        }

        // V16: la tecla que se acaba de pulsar se hunde y brilla en dorado unos 260 ms
        private string escritoVisto = ""; private int teclaFila = -9, teclaCol = -9; private double teclaDesde = -10;
        private void TeclaPulsada(Controlador c)
        {
            if (c.Escrito != escritoVisto) { escritoVisto = c.Escrito; teclaFila = c.FilaTecla; teclaCol = c.ColTecla; teclaDesde = reloj.Elapsed.TotalSeconds; }
        }
        // cuanto esta hundida esa tecla (0 nada, 1 del todo)
        private float Hundida(Controlador c, int fila, int col)
        {
            if (!c.motor.TecladoVivo || fila != teclaFila || col != teclaCol) return 0f;
            double e = reloj.Elapsed.TotalSeconds - teclaDesde;
            if (e < 0 || e > 0.26) return 0f;
            return (float)Math.Sin(e / 0.26 * Math.PI);
        }
    

        // V21: entre «el disco» y «lo mas pedido esta noche» (cada 15 s), un fundido a cielo de 0,9 s en vez del salto
        private void VeloAtraccion(Controlador c, float W, float H)
        {
            Form1 m = c.motor;
            if (!m.AtraccionFundidos || !m.TopNocheOn || topLista == null || topLista.Count == 0) return;
            double fase = (reloj.Elapsed.TotalSeconds - atraccionInicio) % 30.0;
            double cerca = Math.Min(Math.Abs(fase - 15.0), Math.Min(fase, 30.0 - fase));
            if (cerca >= 0.45) return;
            float v = Suave((float)(1 - cerca / 0.45));
            d.Rect(0, 0, W, H, new Color4(0.05f, 0.06f, 0.16f, 0.92f * v));
        }
    

        // V29: LA ROCOLA DESDE EL PRIMER SEGUNDO. En cuanto se tiene la pantalla, antes de leer los datos y la biblioteca: el cielo, la
        // estrella dorada y el nombre, con lo que esta haciendo debajo (un cuadro; no hace falta animarlo: dura lo que tarda en cargar).
        internal void Presentacion(string que)
        {
            float W = sup.Ancho, H = sup.Alto;
            d.Empieza(Fondo);
            Cielo(W, H, reloj.Elapsed.TotalSeconds);
            float cx = W / 2, cy = H * 0.40f, r = 46 * k;
            d.Brillo(cx, cy, r * 3.2f, Dorado.ConAlfa(0.28f));
            for (int i = 0; i < 5; i++)
            {
                double a0 = -Math.PI / 2 + i * 2 * Math.PI / 5, a1 = a0 + Math.PI / 5, a2 = a0 - Math.PI / 5;
                float ox = cx + r * (float)Math.Cos(a0), oy = cy + r * (float)Math.Sin(a0);
                float ix1 = cx + r * 0.42f * (float)Math.Cos(a1), iy1 = cy + r * 0.42f * (float)Math.Sin(a1), ix2 = cx + r * 0.42f * (float)Math.Cos(a2), iy2 = cy + r * 0.42f * (float)Math.Sin(a2);
                d.Triangulo(cx, cy, ox, oy, ix1, iy1, Dorado); d.Triangulo(cx, cy, ix2, iy2, ox, oy, Color4.Hex(0xE0A93A));
            }
            Letra fT = Tre(Math.Max(28f, 64 * k), true), fQ = Tre(Math.Max(14f, 20 * k), false);
            Espaciado(fT, "ROCOLA", cx - (fT.Mide("ROCOLA") + 5 * fT.Tam * 0.18f) / 2, cy + r * 1.6f, fT.Tam * 0.18f, Blanco);
            fQ.EscribeCentrado(d, que, cx, cy + r * 1.6f + fT.Tam * 1.4f, Color4.Hex(0xB4C0E6));
            d.Termina(); sup.Presenta();
        }
    }
}
