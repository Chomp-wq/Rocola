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
            bool top = m.TopNocheOn && topLista != null && topLista.Count > 0;
            if (!m.AtraccionFundidos || (!top && !m.MosaicoExitos)) return;
            double ciclo = m.MosaicoExitos ? 45.0 : 30.0, fase = (reloj.Elapsed.TotalSeconds - atraccionInicio) % ciclo;
            double cerca = Math.Min(fase, ciclo - fase);
            if (top) cerca = Math.Min(cerca, Math.Abs(fase - 15.0));
            if (m.MosaicoExitos) cerca = Math.Min(cerca, Math.Abs(fase - 30.0));
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
    

        // ---- LAS CHISPAS (V18 y V23): unas pocas particulas, cada una un punto suave que se apaga; si llevan destino, se curvan hacia el ----
        private sealed class Chispa { public float X, Y, Vx, Vy, Edad, Vida, Tam, Tx, Ty; public bool Destino; public Color4 C; }
        private readonly System.Collections.Generic.List<Chispa> chispas = new System.Collections.Generic.List<Chispa>();
        private readonly Random azar = new Random(11);
        private double chispasT = -1;
        private void Suelta(float x, float y, int n, Color4 c, float vel, float vida, float tam, bool destino = false, float tx = 0, float ty = 0)
        {
            for (int i = 0; i < n && chispas.Count < 400; i++)
            {
                double a = azar.NextDouble() * 2 * Math.PI; float v = vel * (0.4f + (float)azar.NextDouble());
                chispas.Add(new Chispa { X = x, Y = y, Vx = v * (float)Math.Cos(a), Vy = v * (float)Math.Sin(a) - (destino ? vel * 0.8f : 0), Vida = vida * (0.7f + 0.6f * (float)azar.NextDouble()), Tam = tam * (0.6f + 0.8f * (float)azar.NextDouble()), C = c, Destino = destino, Tx = tx, Ty = ty });
            }
        }
        private void Chispas()
        {
            double t = reloj.Elapsed.TotalSeconds; float dt = chispasT < 0 ? 0 : (float)Math.Max(0, Math.Min(0.1, t - chispasT)); chispasT = t;
            for (int i = chispas.Count - 1; i >= 0; i--)
            {
                Chispa p = chispas[i];
                p.Edad += dt;
                if (p.Edad >= p.Vida) { chispas.RemoveAt(i); continue; }
                if (p.Destino)
                {
                    // hacia el destino: un muelle suave que se hace firme al final (llegan todas, en arco)
                    float u = p.Edad / p.Vida, f = 6f + 40f * u * u;
                    p.Vx += ((p.Tx - p.X) * f - p.Vx * 3f) * dt; p.Vy += ((p.Ty - p.Y) * f - p.Vy * 3f) * dt;
                }
                else { p.Vy += 60 * k * dt; p.Vx *= 1f - 1.5f * dt; p.Vy *= 1f - 1.5f * dt; }
                p.X += p.Vx * dt; p.Y += p.Vy * dt;
                float a = 1f - p.Edad / p.Vida;
                d.Brillo(p.X, p.Y, p.Tam * 2.6f, p.C.ConAlfa(0.35f * a));
                d.Brillo(p.X, p.Y, p.Tam, Blanco.ConAlfa(0.9f * a));
            }
        }

        // V18: la estrella de la ayuda con su sombra en el piso, un reflejo de luz y una estela de chispas cuando vuela (sin cara)
        private float estAntX = -1, estAntY;
        private void LuzDeLaEstrella(Controlador c, float cx, float cy, float r, float alfa)
        {
            if (!c.motor.EstrellaLuz) return;
            d.Quad(d.Punto, cx - r * 1.1f, cy + r * 1.38f, r * 2.2f, r * 0.45f, 0, 0, 1, 1, new Color4(0, 0, 0.04f, 0.45f * alfa));   // la sombra
            if (estAntX >= 0)
            {
                float dx = cx - estAntX, dy = cy - estAntY;
                if (dx * dx + dy * dy > 9 * k * k) Suelta(cx - dx * 0.5f, cy - dy * 0.5f, 2, Color4.Hex(0xC8B8FF), 30 * k, 0.6f, 2.4f * k);
            }
            estAntX = cx; estAntY = cy;
        }
        private void BrilloDeLaEstrella(Controlador c, float cx, float cy, float r, float alfa)
        {
            if (!c.motor.EstrellaLuz) return;
            d.Brillo(cx - r * 0.32f, cy - r * 0.38f, r * 0.42f, Blanco.ConAlfa(0.32f * alfa));   // la luz de arriba a la izquierda
        }

        // V17: los resultados del buscador entran en cascada (cada uno 45 ms despues del anterior, 220 ms de entrada)
        private string cascadaFirma; private double cascadaDesde;
        private float Cascada(Controlador c, int i, out float corre)
        {
            corre = 0;
            if (!c.motor.ResultadosCascada) return 1f;
            string f = c.Escrito + "|" + c.Resultados.Count;
            double t = reloj.Elapsed.TotalSeconds;
            if (f != cascadaFirma) { cascadaFirma = f; cascadaDesde = t; }
            float u = Suave((float)((t - cascadaDesde - i * 0.045) / 0.22));
            corre = 24 * k * (1f - u);
            return u;
        }

        // V19: en la atraccion, la portada del disco en grande detras de todo, acercandose y moviendose despacio (como en el cine)
        private void PortadaDeCine(Form1.Disco disco, float W, float H, double edad, float alfa)
        {
            Textura t = caratulas.Para(disco.Portada, W);
            if (t == null) return;
            double u = Math.Max(0, Math.Min(1, edad / 8.0));
            float zoom = 1.05f + 0.12f * (float)u, uw = 1f / zoom, vh = Math.Min(1f, uw * H / W);
            uint h32 = 2166136261; foreach (char ch in disco.Carpeta ?? "") h32 = (h32 ^ ch) * 16777619;
            float dirX = (h32 & 1) == 0 ? 1f : -1f, dirY = (h32 & 2) == 0 ? 1f : -1f;
            float u0 = (1f - uw) * (0.5f + 0.4f * dirX * ((float)u - 0.5f)), v0 = (1f - vh) * (0.5f + 0.4f * dirY * ((float)u - 0.5f));
            d.Quad(t, 0, 0, W, H, u0, v0, u0 + uw, v0 + vh, new Color4(1, 1, 1, 0.30f * alfa));
            d.Degradado(0, 0, W, H, Color4.Hex(0x0A0E26, 0.55f * alfa), Color4.Hex(0x0A0E26, 0.80f * alfa));
        }

        // V13 y V14: en el reposo, la portada derrama su color por el cielo, y un anillo de luz late con los golpes alrededor del cubo
        private void LuzDelReposo(Form1 m, float W, float top, float cx, float cy, float lado, Textura tx)
        {
            if (m.ReposoLuz && m.SonandoPortada != null)
            {
                Textura bo = caratulas.Borrosa(m.SonandoPortada);
                if (bo != null) d.Imagen(bo, -W * 0.1f, -top * 0.1f, W * 1.2f, top * 1.15f, 0.38f);
                if (tx != null) d.Brillo(cx, cy, W * 0.42f, tx.Media.ConAlfa(0.30f + 0.15f * pGraves));
            }
            if (m.AnilloMusica)
            {
                Color4 c = m.ColorGenero && m.ColorSonando != 0 ? Color4.Hex(m.ColorSonando) : Color4.Hex(0x58D2FF);
                for (int i = 0; i < 3; i++)
                {
                    float rr = lado * (0.68f + 0.10f * i + 0.10f * pGraves), g = (3 - i) * 1.4f * k;
                    d.Contorno(cx - rr, cy - rr, 2 * rr, 2 * rr, rr, g, Dibujo.Mezcla(c, Blanco, 0.3f).ConAlfa((0.45f - 0.12f * i) * (0.4f + 0.6f * pGraves)));
                }
            }
        }
    

        // V24: el vidrio de los avisos: lo que hay detras, copiado y dibujado nueve veces corrido (un promedio: borroso) y aclarado
        private bool VidrioDetras(float x, float y, float w, float h, float alfa)
        {
            Textura t = d.CopiaPantalla(x, y, w, h);
            if (t == null) return false;
            float r = 6 * k; int n = 0;
            d.Recorta(x + 3 * k, y, w - 6 * k, h);   // sin salirse (las esquinas redondas las tapa el borde)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    n++;
                    d.Quad(t, x + dx * r, y + dy * r, w, h, 0, 1, 1, 0, new Color4(1, 1, 1, alfa / n));   // promedio corrido: 1, 1/2, 1/3...
                }
            d.SinRecorte();
            d.Degradado(x + 3 * k, y, w - 6 * k, h, new Color4(1, 1, 1, 0.10f * alfa), new Color4(1, 1, 1, 0.03f * alfa));
            return true;
        }
    

        // V15: LA COLA EN CARRUSEL: la siguiente al frente, grande, y las demas alejandose a la derecha en perspectiva (la cara de la
        // derecha mas baja); cuando la cola avanza, todas se corren un puesto (450 ms).
        private string carruselCabeza; private double carruselDesde = -10;
        private void Carrusel(Form1 m, System.Collections.Generic.List<Form1.EnColaVista> cola, float xc, float top, float W, float H, Letra small, Letra normal)
        {
            double t = reloj.Elapsed.TotalSeconds;
            string cabeza = cola[0].Ruta ?? cola[0].Titulo;
            if (cabeza != carruselCabeza) { if (carruselCabeza != null) carruselDesde = t; carruselCabeza = cabeza; }
            float corre = 1f - Suave((float)((t - carruselDesde) / 0.45));   // de 1 (un puesto atras) a 0
            small.Escribe(d, "A CONTINUACIÓN  /  " + m.EnCola + (m.EnCola == 1 ? " CANCIÓN" : " CANCIONES"), xc, top + 10 * k, Turquesa);
            float ay = top + 36 * k, ah = H - 46 * k - ay, aw = W - 24 * k - xc;
            float s0 = Math.Min(ah * 0.86f, aw * 0.40f), x0 = xc + 6 * k, y0 = ay + (ah - s0) / 2 - 6 * k;
            for (int i = cola.Count - 1; i >= 0; i--)
            {
                float p = i + corre;   // el puesto (con decimales mientras se corren)
                float esc = (float)Math.Pow(0.80, p), s = s0 * esc;
                float x = p <= 0 ? x0 : x0 + s0 * 0.72f + (s0 * 0.42f) * (float)((1 - Math.Pow(0.80, p - 1)) / 0.20);
                if (x + s > W - 10 * k) continue;
                float y = y0 + (s0 - s) / 2, baja = p < 0.5f ? 0 : s * 0.10f;   // la cara de la derecha, mas corta: se aleja
                float a = Math.Max(0.15f, 1f - 0.16f * p);
                Textura tq = caratulas.Para(cola[i].Portada, s);
                d.Brillo(x + s / 2, y + s + 4 * k, s * 0.5f, new Color4(0, 0, 0.04f, 0.35f * a));
                if (tq != null) d.ImagenLibre(tq, x, y, x + s, y + baja, x + s, y + s - baja, x, y + s, new Color4(a, a, a, 1f));
                else { d.Cuatro(x, y, Color4.Hex(0x4A3E9A, a), x + s, y + baja, Color4.Hex(0x4A3E9A, a), x + s, y + s - baja, Color4.Hex(0x2A2470, a), x, y + s, Color4.Hex(0x2A2470, a)); }
                if (p < 0.5f) d.Contorno(x - 2 * k, y - 2 * k, s + 4 * k, s + 4 * k, 6 * k, 2 * k, Turquesa.ConAlfa(0.8f));
            }
            // el titulo de la siguiente, debajo
            Form1.EnColaVista q = cola[0];
            float ty = y0 + s0 + 8 * k;
            normal.Escribe(d, normal.Ajusta(q.Titulo ?? "", aw), xc, Math.Min(ty, H - 40 * k), Blanco);
        }

        // V20: MOSAICO DE LO MAS PEDIDO (una pantalla mas de la atraccion, 15 s): las portadas de los discos mas pedidos en una pared que
        // va apareciendo volteandose, una a una, y cada dos segundos una se voltea y ensena otro disco.
        private void Mosaico(Controlador c, float W, float H, double edad)
        {
            System.Collections.Generic.List<Form1.Disco> lista = new System.Collections.Generic.List<Form1.Disco>();
            if (c.discos != null) foreach (Form1.Disco dd in c.discos) if (!dd.Videos && dd.Portada != null) lista.Add(dd);
            lista.Sort((a, b) => b.Pedidas.CompareTo(a.Pedidas));
            if (lista.Count == 0) return;
            int cols = 6, filas = 3;
            float mg = 40 * k, gap = 12 * k, tw = (W - 2 * mg - (cols - 1) * gap) / cols, th = tw, alto = filas * th + (filas - 1) * gap;
            float y0 = Math.Max(110 * k, (H - alto) / 2 + 20 * k);
            Letra fT = Tre(Math.Max(18f, H * .045f), true);
            float alfa = (float)Math.Min(1, Math.Min(edad / 0.5, (15.0 - edad) / 0.5));
            Espaciado(fT, "LO MÁS PEDIDO", (W - fT.Mide("LO MÁS PEDIDO") - 12 * fT.Tam * 0.2f) / 2, y0 - fT.Tam * 1.8f, fT.Tam * 0.2f, Dorado.ConAlfa(alfa));
            for (int i = 0; i < cols * filas; i++)
            {
                int fila = i / cols, col = i % cols;
                // la que toca en esta casilla: va cambiando cada 2 s (una casilla distinta cada vez)
                int vuelta = (int)(edad / 2.0), cual = (i + (vuelta > 0 && (vuelta * 7) % (cols * filas) == i ? vuelta : 0)) % lista.Count;
                double entra = edad - (fila * cols + col) * 0.08, gira = edad - vuelta * 2.0;
                float abre = Suave((float)(entra / 0.4));
                if ((vuelta * 7) % (cols * filas) == i && vuelta > 0 && gira < 0.5) abre = Math.Abs((float)Math.Cos(gira / 0.5 * Math.PI));   // se voltea
                if (abre <= 0.02f) continue;
                float x = mg + col * (tw + gap), y = y0 + fila * (th + gap), w = tw * abre;
                Textura tx = caratulas.Para(lista[cual].Portada, tw);
                if (tx != null) d.Quad(tx, x + (tw - w) / 2, y, w, th, 0, 0, 1, 1, new Color4(1, 1, 1, alfa));
                else d.Rect(x + (tw - w) / 2, y, w, th, Color4.Hex(0x2A3A78, alfa));
                if (i < 3 && abre > 0.9f) { Circulo(x + 18 * k, y + 18 * k, 13 * k, Dorado.ConAlfa(alfa)); mini.EscribeCentrado(d, (i + 1).ToString(), x + 18 * k, y + 18 * k - mini.Tam * 0.55f, new Color4(0.1f, 0.08f, 0.02f, alfa)); }
            }
        }
    }
}
