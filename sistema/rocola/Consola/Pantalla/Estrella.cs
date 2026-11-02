// LA ESTRELLA DE LA AYUDA, DIBUJADA (en XP, la estrella 2D/3D de D3D9Star.cs y Estrella3dGpu.cs, y el globo de AsistenteRocola.cs y
// AsistenteGuiaRocola.cs). Decision del 10-10-2026: la estrella NO TIENE ROSTRO (ni ojos, ni boca, ni cejas): conserva el cuerpo del icono
// y expresa todo con movimientos, postura, brazos, manos y pasos. Sus cinco puntas son la cabeza, los dos brazos (con su mano) y las dos
// piernas (con su pie). Vive en su casilla de la barra de arriba («AYUDA», con sus dos botones dibujados, al lado de «BUSCAR»); al abrir la
// ayuda vuela a su globo y al cerrarla vuelve a su casilla.
using System;
using System.Collections.Generic;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        internal struct Zona
        {
            public float X, Y, W, H;
            public Zona(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
            public bool Vacia { get { return W <= 0 || H <= 0; } }
            public float Cx { get { return X + W / 2; } }
            public float Cy { get { return Y + H / 2; } }
        }
        // Donde quedo cada pieza en el ultimo cuadro (las apunta el dibujo de cada pantalla): lo que la guia senala.
        private Zona zElegido, zCancion, zLetras, zCreditos, zTeclado, zResultados, zBuscar, zAyuda;
        private static readonly Color4 Oro = Color4.Hex(0xFFD45A), OroClaro = Color4.Hex(0xFFF1A8), Rojo = Color4.Hex(0xE5484D), VerdeBoton = Color4.Hex(0x3DDC84),
            Amarillo = Color4.Hex(0xFFC93C), Tinta = Color4.Hex(0x1A2350);

        // UN BOTON DIBUJADO: B rojo, V verde, M la moneda, U/D/L/R las flechas (las de arriba y abajo, rojas como en la rocola; las de los
        // lados, amarillas).
        private void Boton(float cx, float cy, float r, char b)
        {
            switch (b)
            {
                case 'B': d.Brillo(cx, cy, r * 1.6f, Rojo.ConAlfa(0.35f)); Circulo(cx, cy, r, Rojo); break;
                case 'V': d.Brillo(cx, cy, r * 1.6f, VerdeBoton.ConAlfa(0.35f)); Circulo(cx, cy, r, VerdeBoton); break;
                case 'M':
                    Circulo(cx, cy, r, Oro); Circulo(cx, cy, r * 0.72f, Color4.Hex(0xE0A93A));
                    chica.EscribeCentrado(d, "$", cx, cy - chica.Tam * 0.55f, Tinta); break;
                default:
                    Color4 col = b == 'U' || b == 'D' ? Rojo : Amarillo;
                    Circulo(cx, cy, r, col.ConAlfa(0.25f));
                    float a = r * 0.62f;
                    if (b == 'U') d.Triangulo(cx, cy - a, cx + a, cy + a * 0.6f, cx - a, cy + a * 0.6f, col);
                    else if (b == 'D') d.Triangulo(cx - a, cy - a * 0.6f, cx + a, cy - a * 0.6f, cx, cy + a, col);
                    else if (b == 'L') d.Triangulo(cx - a, cy, cx + a * 0.6f, cy - a, cx + a * 0.6f, cy + a, col);
                    else d.Triangulo(cx + a, cy, cx - a * 0.6f, cy + a, cx - a * 0.6f, cy - a, col);
                    break;
            }
        }
        private void Circulo(float cx, float cy, float r, Color4 c)
        {
            const int n = 20;
            for (int i = 0; i < n; i++)
            {
                double a0 = 2 * Math.PI * i / n, a1 = 2 * Math.PI * (i + 1) / n;
                d.Triangulo(cx, cy, cx + r * (float)Math.Cos(a0), cy + r * (float)Math.Sin(a0), cx + r * (float)Math.Cos(a1), cy + r * (float)Math.Sin(a1), c);
            }
        }
        // Una fila de botones ("L/R U/D V", "B+U"...): devuelve el ancho.
        private float Teclas(string teclas, float x, float cy, float r, bool dibuja)
        {
            float x0 = x;
            foreach (char t in teclas)
            {
                if (t == ' ') { x += r * 1.4f; continue; }
                if (t == '+' || t == '/')
                {
                    string s = t == '+' ? "+" : "o";
                    if (dibuja) chica.Escribe(d, s, x + 3 * k, cy - chica.Tam * 0.55f, Blanco);
                    x += chica.Mide(s) + 6 * k; continue;
                }
                if (dibuja) Boton(x + r, cy, r, t);
                x += 2 * r + 5 * k;
            }
            return x - x0;
        }

        // ---- EL CUERPO DE LA ESTRELLA ----
        // Las cinco puntas: la cabeza arriba, los brazos a los lados, las piernas abajo. Cada gesto mueve los brazos (y sus manos), la
        // postura y los pasos; nada en la cara (no tiene). edad: segundos desde que empezo el gesto. (ax, ay): hacia donde senala.
        private void Cuerpo(float cx, float cy, float r, Gesto g, double edad, double t, float ax, float ay, float alfa)
        {
            // cuanto dura cada gesto antes de volver a quieta (senalar se queda mientras haya a donde)
            double dura = g == Gesto.Credito ? 1.6 : g == Gesto.Saludar || g == Gesto.Despedir ? 1.8 : g == Gesto.Explicar ? 1.4 : g == Gesto.Confirmar ? 0.9 : g == Gesto.Buscar ? 2.4 : 0;
            bool apunta = (ax != 0 || ay != 0);
            if (g != Gesto.Senalar && edad > dura) g = apunta ? Gesto.Senalar : Gesto.Quieta;
            float sube = (float)Math.Sin(t * 2.2) * r * 0.05f;                      // respira
            float lado = 0f, inclina = (float)Math.Sin(t * 1.3) * 0.05f;          // se mece
            double brD = -18, brI = 198;                                             // los brazos (grados; y hacia abajo)
            float largoD = 1f, largoI = 1f, paso = 0f;
            switch (g)
            {
                case Gesto.Saludar:
                case Gesto.Despedir:
                    brD = -62 + 18 * Math.Sin(edad * 2 * Math.PI * 2.6);           // la mano arriba, de lado a lado
                    inclina += g == Gesto.Despedir ? 0.12f * (float)Math.Min(1, edad * 2) : 0f;   // al despedirse, una pequena reverencia
                    break;
                case Gesto.Senalar:
                    if (apunta)
                    {
                        brD = Math.Atan2(ay, ax) * 180 / Math.PI; largoD = 1.22f;     // el brazo hacia lo que toca
                        if (ax < 0) { brI = brD; largoI = 1.22f; brD = -18; largoD = 1f; }   // si esta a la izquierda, con el otro brazo
                        inclina += 0.10f * Math.Sign(ax);
                    }
                    break;
                case Gesto.Explicar:
                    brD = -35 + 10 * Math.Sin(edad * 5); brI = 215 - 10 * Math.Sin(edad * 5 + 1);   // los brazos abiertos, contando
                    break;
                case Gesto.Confirmar:
                    brD = -75; sube -= r * 0.18f * (float)Math.Sin(Math.Min(1, edad / 0.45) * Math.PI);   // brazo arriba y un saltito
                    break;
                case Gesto.Buscar:
                    brD = -118; largoD = 0.85f;                                           // la mano a la frente, mirando a los lados
                    lado = r * 0.35f * (float)Math.Sin(edad * 2.6); inclina += 0.10f * (float)Math.Sin(edad * 2.6);
                    paso = (float)Math.Sin(edad * 5.2);
                    break;
                case Gesto.Credito:
                    brD = -58 + 10 * Math.Sin(edad * 14); brI = 238 - 10 * Math.Sin(edad * 14);   // los dos brazos arriba, festejando
                    sube -= r * 0.45f * (float)Math.Abs(Math.Sin(edad * Math.PI * 2.5));
                    break;
            }
            cx += lado; cy += sube;
            double rot = inclina;   // radianes
            // las cinco puntas (angulo y largo) y los valles entre ellas
            double[] ang = { -90, brD, 54 + 7 * paso, 126 + 7 * paso, brI };
            float[] lar = { 1f, largoD, 1f - 0.08f * Math.Max(0, paso), 1f - 0.08f * Math.Max(0, -paso), largoI };
            float[] px = new float[10], py = new float[10];
            for (int i = 0; i < 5; i++)
            {
                double a = ang[i] * Math.PI / 180 + rot;
                px[2 * i] = cx + r * lar[i] * (float)Math.Cos(a); py[2 * i] = cy + r * lar[i] * (float)Math.Sin(a);
                double b0 = ang[i], b1 = ang[(i + 1) % 5]; if (b1 < b0) b1 += 360;
                double am = ((b0 + b1) / 2) * Math.PI / 180 + rot;
                float ri = r * 0.46f;
                px[2 * i + 1] = cx + ri * (float)Math.Cos(am); py[2 * i + 1] = cy + ri * (float)Math.Sin(am);
            }
            // los brazos y las piernas, de palo negro con manos y pies redondos (detras del cuerpo), como la estrella de XP
            Color4 palo = new Color4(0.02f, 0.02f, 0.04f, alfa);
            float gp = Math.Max(2f, r * 0.10f);
            float hx = cx + (px[2] - cx) * 0.45f, hy = cy + (py[2] - cy) * 0.45f, mx2 = cx + (px[2] - cx) * 1.55f, my2 = cy + (py[2] - cy) * 1.55f;
            d.Linea(hx, hy, mx2, my2, gp, palo); Circulo(mx2, my2, gp * 1.25f, palo);
            hx = cx + (px[8] - cx) * 0.45f; hy = cy + (py[8] - cy) * 0.45f; mx2 = cx + (px[8] - cx) * 1.55f; my2 = cy + (py[8] - cy) * 1.55f;
            d.Linea(hx, hy, mx2, my2, gp, palo); Circulo(mx2, my2, gp * 1.25f, palo);
            for (int pie = 0; pie < 2; pie++)
            {
                float bx = cx + (px[4 + 2 * pie] - cx) * 0.55f, by = cy + (py[4 + 2 * pie] - cy) * 0.55f;
                float fx = bx + (pie == 0 ? 0.08f : -0.08f) * r, fy = cy + r * 1.45f - (pie == 0 ? Math.Max(0, paso) : Math.Max(0, -paso)) * r * 0.12f;
                d.Linea(bx, by, fx, fy, gp, palo);
                d.Redondo(fx - gp * 2.2f, fy - gp * 0.7f, gp * 3.4f, gp * 1.5f, gp * 0.75f, palo);
            }
            // el cuerpo: la estrella de cinco puntas en 3D, lila, que se ladea un poco al moverse
            d.Brillo(cx, cy, r * 2.0f, Color4.Hex(0x9C8CFF, 0.22f * alfa));
            Solido(cx, cy, r * 1.08f, 0.32 * Math.Sin(t * 0.9) + lado / Math.Max(1f, r) * 0.6, 0.18, rot, 0x9C8CFFu, alfa, true);
        }

        // ---- LA AYUDA EN LA PANTALLA ----
        private float estX = -1, estY, estR;   // donde va la estrella (vuela hacia su sitio)
        private double estT;
        private Zona Objetivo(int paso)
        {
            switch (paso)
            {
                case Controlador.PasoDisco: case Controlador.PasoMover: case Controlador.PasoEntrar: case Controlador.PasoPagina: return zElegido;
                case Controlador.PasoLetra: return zLetras;
                case Controlador.PasoMoneda: return zCreditos;
                case Controlador.PasoCancion: case Controlador.PasoVolver: return zCancion;
                case Controlador.PasoAbreBuscador: case Controlador.PasoCierraBuscador: return zBuscar;
                case Controlador.PasoEscribe: return zTeclado;
                case Controlador.PasoResultado: return zResultados;
            }
            return default(Zona);
        }

        private void Ayuda(Controlador c, float W, float H)
        {
            double t = reloj.Elapsed.TotalSeconds, dt = Math.Max(0, Math.Min(0.1, t - estT)); estT = t;
            bool fuera = c.AyudaAbierta || c.AyudaRegresando;
            float casaX = zAyuda.X + 20 * k, casaY = zAyuda.Cy + 1 * k, casaR = 12 * k;
            if (!fuera) { estX = casaX; estY = casaY; estR = casaR; return; }
            if (estX < 0) { estX = casaX; estY = casaY; estR = casaR; }
            double edad = unchecked(Environment.TickCount - c.AyudaGestoTick) / 1000.0;
            float objX, objY, objR; Zona obj = default(Zona);
            float gx = 0, gy = 0, gw = 0, gh = 0;   // el globo
            bool colaDerecha = false;               // la colita del globo, hacia la estrella
            if (!c.AyudaAbierta) { objX = casaX; objY = casaY; objR = casaR; }   // vuelve a su casilla
            else if (c.AyudaMision == 0 || c.AyudaDespidiendo && c.AyudaMision == 0)
            {
                // EL MENU: un globo grande al centro y la estrella a su izquierda
                gw = Math.Min(W * 0.53f, 560 * k); gh = 492 * k; gx = W - gw - 22 * k; gy = 96 * k;
                objX = Math.Max(150 * k, gx - 220 * k); objY = gy + gh * 0.50f; objR = 82 * k;
            }
            else
            {
                // GUIANDO: un globo pequeno apartado de lo que hay que mirar, y la estrella a su lado senalandolo
                obj = Objetivo(c.AyudaPaso);
                gw = Math.Min(W * 0.46f, 470 * k); gh = 170 * k;
                bool arriba = obj.Vacia || obj.Cy > H * 0.5f;
                bool izq = !obj.Vacia && obj.Cx > W * 0.5f;
                gy = arriba ? 72 * k : H - gh - 140 * k;
                if (!obj.Vacia && arriba && obj.Y < gy + gh + 10 * k) gy = Math.Max(64 * k, obj.Y - gh - 20 * k);
                gx = izq ? 110 * k : W - gw - 24 * k;
                // la estrella, del lado del globo que mira hacia lo que senala (asi la flecha no pasa por debajo del globo)
                colaDerecha = !obj.Vacia && obj.Cx > gx + gw / 2;
                objR = 44 * k; objX = colaDerecha ? gx + gw + 58 * k : gx - 58 * k; objY = gy + gh * 0.55f;
            }
            // vuela hacia su sitio (suave: se acerca un tanto cada cuadro)
            float f = 1f - (float)Math.Exp(-dt * 7.0);
            estX += (objX - estX) * f; estY += (objY - estY) * f; estR += (objR - estR) * f;
            if (!c.AyudaAbierta) { Cuerpo(estX, estY, estR, c.AyudaGesto, edad, t, 0, 0, 1f); return; }

            float ax = 0, ay = 0;
            if (!obj.Vacia) { ax = obj.Cx - estX; ay = obj.Cy - estY; }
            if (c.AyudaMision != 0 && !obj.Vacia && !c.AyudaLograda)
            {
                // lo que toca, iluminado (late) y la flecha dorada desde la estrella
                float late = 0.55f + 0.45f * (float)Math.Sin(t * 5);
                Marco(obj, Dorado.ConAlfa(0.5f + 0.5f * late), 4 * k);
                d.Brillo(obj.Cx, obj.Cy, Math.Max(obj.W, obj.H) * 0.7f, Dorado.ConAlfa(0.10f * late));
                Flecha(estX, estY, obj, t);
            }
            // el globo
            Color4 fondoG = Color4.Hex(0x15204A, 0.96f), bordeG = Color4.Hex(0x8CC8FF, 0.9f);
            d.Brillo(gx + gw / 2, gy + gh / 2, Math.Max(gw, gh) * 0.6f, Color4.Hex(0x6FA8FF, 0.10f));
            d.Redondo(gx + 4 * k, gy + 6 * k, gw, gh, 22 * k, new Color4(0, 0, 0, 0.35f));
            d.RedondoDegradado(gx, gy, gw, gh, 22 * k, Color4.Hex(0x1C2A5C, 0.97f), Color4.Hex(0x111A40, 0.97f));
            d.Contorno(gx, gy, gw, gh, 22 * k, 1.8f * k, bordeG);
            // la colita del globo, hacia la estrella (con su borde)
            float c0 = gy + gh * 0.45f, c1 = gy + gh * 0.63f, cm = gy + gh * 0.54f;
            if (colaDerecha) { d.Triangulo(gx + gw, c0 - 2 * k, gx + gw, c1 + 2 * k, gx + gw + 22 * k, cm, bordeG); d.Triangulo(gx + gw - 2 * k, c0, gx + gw - 2 * k, c1, gx + gw + 18 * k, cm, Color4.Hex(0x161F48)); }
            else { d.Triangulo(gx, c0 - 2 * k, gx, c1 + 2 * k, gx - 22 * k, cm, bordeG); d.Triangulo(gx + 2 * k, c0, gx + 2 * k, c1, gx - 18 * k, cm, Color4.Hex(0x161F48)); }
            float px = gx + 24 * k, py = gy + 18 * k, pw = gw - 48 * k;
            if (c.AyudaDespidiendo)
            {
                grandeNegrita.EscribeCentrado(d, "¡Adiós!", gx + gw / 2, gy + gh / 2 - enorme.Tam * 0.6f, Dorado);
            }
            else if (c.AyudaMision == 0)
            {
                nChica.Escribe(d, "¡HOLA! TE ACOMPAÑO PASO A PASO", px, py, Color4.Hex(0x8CC8FF));
                grandeNegrita.Escribe(d, "¿Qué quieres hacer?", px, py + 26 * k, Blanco);
                List<string> intro = Envuelve("Elige y yo te voy señalando qué botón tocar. Tú lo haces y la música sigue sonando.", nMediaFina, pw);
                for (int i = 0; i < intro.Count && i < 2; i++) nMediaFina.Escribe(d, intro[i], px, py + 92 * k + i * 27 * k, Color4.Hex(0xC4CCEA));
                float oy = py + 160 * k, oh = 56 * k;
                for (int i = 0; i < Controlador.AyudaOpciones; i++)
                {
                    bool sel = i == c.AyudaSeleccion;
                    float yy = oy + i * (oh + 12 * k);
                    if (sel) d.Brillo(px + pw / 2, yy + oh / 2, pw * 0.55f, Color4.Hex(0x48D470, 0.12f));
                    d.RedondoDegradado(px, yy, pw, oh, 10 * k, sel ? Color4.Hex(0x2A4A84, 0.95f) : Color4.Hex(0x16204A, 0.9f), sel ? Color4.Hex(0x223C6E, 0.95f) : Color4.Hex(0x111A3E, 0.9f));
                    d.Contorno(px, yy, pw, oh, 10 * k, sel ? 2f * k : 1.2f * k, sel ? Color4.Hex(0x9AF0B4) : Color4.Hex(0x7F90D0, 0.40f));
                    nMedia.Escribe(d, nMedia.Ajusta(Controlador.OpcionesAyuda[i], pw - 110 * k), px + 14 * k, yy + oh / 2 - nMedia.Tam * 0.6f, Blanco);
                    if (sel)
                    {
                        float bx = px + pw - 70 * k, q = 18 * k;
                        d.Contorno(bx, yy + oh / 2 - q - 1 * k, q, q, 3 * k, 1.3f * k, Rojo.ConAlfa(0.8f)); d.Triangulo(bx + q / 2, yy + oh / 2 - q * 0.86f, bx + q * 0.78f, yy + oh / 2 - q * 0.3f, bx + q * 0.22f, yy + oh / 2 - q * 0.3f, Rojo.ConAlfa(0.8f));
                        d.Contorno(bx, yy + oh / 2 + 1 * k, q, q, 3 * k, 1.3f * k, Rojo); d.Triangulo(bx + q * 0.22f, yy + oh / 2 + q * 0.3f, bx + q * 0.78f, yy + oh / 2 + q * 0.3f, bx + q / 2, yy + oh / 2 + q * 0.86f, Rojo);
                        float late = 0.75f + 0.25f * (float)Math.Sin(t * 4);
                        d.Brillo(px + pw - 26 * k, yy + oh / 2, 24 * k, VerdeBoton.ConAlfa(0.45f * late));
                        Circulo(px + pw - 26 * k, yy + oh / 2, 15 * k, VerdeBoton);
                    }
                }
                float ty = gy + gh - 26 * k, x = px;
                float q2 = 18 * k;
                d.Contorno(x, ty - q2 / 2, q2, q2, 3 * k, 1.3f * k, Rojo); d.Triangulo(x + q2 / 2, ty - q2 * 0.3f, x + q2 * 0.78f, ty + q2 * 0.22f, x + q2 * 0.22f, ty + q2 * 0.22f, Rojo); x += q2 + 3 * k;
                d.Contorno(x, ty - q2 / 2, q2, q2, 3 * k, 1.3f * k, Rojo); d.Triangulo(x + q2 * 0.22f, ty - q2 * 0.22f, x + q2 * 0.78f, ty - q2 * 0.22f, x + q2 / 2, ty + q2 * 0.3f, Rojo); x += q2 + 8 * k;
                x += nChica.Escribe(d, "mover", x, ty - nChica.Tam * 0.62f, Color4.Hex(0xB8D4FF)) + 22 * k;
                Boton(x + 10 * k, ty, 10 * k, 'V'); x += 26 * k; x += nChica.Escribe(d, "elegir", x, ty - nChica.Tam * 0.62f, Color4.Hex(0xB8D4FF)) + 22 * k;
                Boton(x + 10 * k, ty, 10 * k, 'B'); x += 26 * k; nChica.Escribe(d, "salir", x, ty - nChica.Tam * 0.62f, Color4.Hex(0xB8D4FF));
            }
            else if (c.AyudaLograda)
            {
                grandeNegrita.EscribeCentrado(d, "¡Lo lograste!", gx + gw / 2, gy + 34 * k, Dorado);
                media.EscribeCentrado(d, c.AyudaMision == Controlador.MisionAprender ? "Ya sabes moverte por la rocola." : "Tu canción ya está en la cola.", gx + gw / 2, gy + 100 * k, Blanco);
            }
            else
            {
                string titulo, texto, teclas; Controlador.TextoPaso(c.AyudaPaso, out titulo, out texto, out teclas);
                int total, n = Controlador.NumeroPaso(c.AyudaMision, c.AyudaPaso, c.motor.Creditos > 0, out total);
                string arriba_ = c.AyudaBienAhora ? "¡Muy bien!" : n > 0 ? "Paso " + n + " de " + total : "";
                nChica.Escribe(d, arriba_, px, py - 4 * k, c.AyudaBienAhora ? VerdeBoton : Color4.Hex(0x8CC8FF));
                nGrande.Escribe(d, nGrande.Ajusta(titulo, pw), px, py + 18 * k, Blanco);
                List<string> l = Envuelve(texto, mini, pw);
                for (int i = 0; i < l.Count && i < 3; i++) mini.Escribe(d, l[i], px, py + 58 * k + i * 21 * k, Color4.Hex(0xC4CCEA));
                Teclas(teclas, px, gy + gh - 24 * k, 12 * k, true);
            }
            Cuerpo(estX, estY, estR, c.AyudaGesto, edad, t, c.AyudaMision != 0 && !c.AyudaLograda ? ax : 0, c.AyudaMision != 0 && !c.AyudaLograda ? ay : 0, 1f);
        }

        private void Marco(Zona z, Color4 c, float g)
        {
            d.Rect(z.X, z.Y, z.W, g, c); d.Rect(z.X, z.Y + z.H - g, z.W, g, c);
            d.Rect(z.X, z.Y + g, g, z.H - 2 * g, c); d.Rect(z.X + z.W - g, z.Y + g, g, z.H - 2 * g, c);
        }
        // La flecha dorada: de la estrella hacia el borde de lo que toca, con la punta que va y viene.
        private void Flecha(float sx, float sy, Zona z, double t)
        {
            float tx = Math.Max(z.X, Math.Min(z.X + z.W, sx)), ty = Math.Max(z.Y, Math.Min(z.Y + z.H, sy));
            if (sx >= z.X && sx <= z.X + z.W && sy >= z.Y && sy <= z.Y + z.H) return;
            float dx = tx - sx, dy = ty - sy, l = (float)Math.Sqrt(dx * dx + dy * dy);
            if (l < 60 * k) return;
            float ux = dx / l, uy = dy / l;
            float va = 8 * k * (float)Math.Sin(t * 6);
            float ex = tx - ux * (14 * k + va), ey = ty - uy * (14 * k + va);
            float bx = sx + ux * 56 * k, by = sy + uy * 56 * k;
            d.Linea(bx, by, ex - ux * 18 * k, ey - uy * 18 * k, 6 * k, Dorado.ConAlfa(0.9f));
            float nx = -uy, ny = ux, p = 16 * k;
            d.Triangulo(ex, ey, ex - ux * 26 * k + nx * p, ey - uy * 26 * k + ny * p, ex - ux * 26 * k - nx * p, ey - uy * 26 * k - ny * p, Dorado);
        }
        private static List<string> Envuelve(string s, Letra l, float ancho)
        {
            List<string> r = new List<string>(); string linea = "";
            foreach (string p in (s ?? "").Split(' '))
            {
                string prueba = linea.Length == 0 ? p : linea + " " + p;
                if (l.Mide(prueba) > ancho && linea.Length > 0) { r.Add(linea); linea = p; } else linea = prueba;
            }
            if (linea.Length > 0) r.Add(linea);
            return r;
        }
    }
}
