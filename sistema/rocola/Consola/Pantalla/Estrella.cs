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
        private Zona zPortadaElegida;
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
        private Controlador vistaC;   // el de este cuadro (para las mejoras de la estrella)
        private void Cuerpo(float cx, float cy, float r, Gesto g, double edad, double t, float ax, float ay, float alfa)
        {
            if (vistaC != null) LuzDeLaEstrella(vistaC, cx, cy, r, alfa);   // V18: la sombra y la estela
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
            if (vistaC != null) BrilloDeLaEstrella(vistaC, cx, cy, r, alfa);   // V18
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
                objX = Math.Max(150 * k, gx - 220 * k); objY = gy + gh * 0.42f; objR = 101 * k;
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
            // EL VELO (DibujaAsistente de XP): con el menu abierto, toda la rocola detras se oscurece (alfa 138 de 0x060B1D)
            if (c.AyudaMision == 0) { veloAyuda += (1f - veloAyuda) * f; d.Rect(0, 0, W, H, Color4.Hex(0x060B1D, 0.54f * veloAyuda)); }
            else veloAyuda = 0;
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
                // PintaPanelAsistente de XP: todo en proporcion al globo (h), Trebuchet MS; el texto empieza a un veinteavo del ancho
                float wb = gw + Math.Min(32, Math.Max(18, gw / 18)), hb = gh, mx = Math.Max(12, wb / 20);
                float x0 = gx + mx, tw = gx + gw - mx - x0;
                Letra pequeno = Tre(Math.Max(10, Math.Min(18, hb * .034f)), true), tit = Tre(Math.Max(20, Math.Min(42, hb * .085f)), true);
                Letra cuerpo = Tre(Math.Max(12, Math.Min(21, hb * .043f)), false), opcion = Tre(Math.Max(14, Math.Min(27, hb * .052f)), true);
                Color4 azulT = new Color4(150 / 255f, 215 / 255f, 1f), blancoT = new Color4(246 / 255f, 248 / 255f, 1f), tenueT = new Color4(185 / 255f, 206 / 255f, 234 / 255f);
                pequeno.Escribe(d, "¡HOLA! TE ACOMPAÑO PASO A PASO", x0, gy + hb * .045f, azulT);
                tit.Escribe(d, "¿Qué quieres hacer?", x0, gy + hb * .12f, blancoT);
                List<string> intro = Envuelve("Elige y yo te voy señalando qué botón tocar. Tú lo haces y la música sigue sonando.", cuerpo, tw);
                for (int i = 0; i < intro.Count && i < 3; i++) cuerpo.Escribe(d, intro[i], x0, gy + hb * .30f + i * cuerpo.Tam * 1.15f, tenueT);
                float oh = Math.Max(22, (int)(hb * .115f)), ow = gx + gw - Math.Max(12, wb / 20) - x0;
                float dv = Math.Max(12, (int)(oh * .62f)), kq = Math.Max(9, (int)(oh * .40f)), anchoCtl = 10 + dv + 8 + kq + 8;
                Letra fOpc = opcion;
                float mas = 0; foreach (string nom in Controlador.OpcionesAyuda) mas = Math.Max(mas, fOpc.Mide(nom));
                if (mas > ow - 24 - anchoCtl) fOpc = Tre(Math.Max(opcion.Tam * .70f, opcion.Tam * (ow - 24 - anchoCtl) / mas * .98f), true);
                for (int i = 0; i < Controlador.AyudaOpciones; i++)
                {
                    bool sel = i == c.AyudaSeleccion;
                    float yy = gy + (int)(hb * (.50f + i * .135f)), rr = Math.Min(14, oh / 3);
                    d.Redondo(x0, yy, ow, oh, rr, sel ? new Color4(42 / 255f, 87 / 255f, 130 / 255f, 235 / 255f) : new Color4(28 / 255f, 42 / 255f, 68 / 255f, 150 / 255f));
                    d.Contorno(x0, yy, ow, oh, rr, sel ? 2f : 1f, sel ? Color4.Hex(0x96EBBC) : Color4.Hex(0x5A7196));
                    EnRenglon(fOpc, Controlador.OpcionesAyuda[i], x0 + 12, yy, ow - 24 - (sel ? anchoCtl : 0), oh, blancoT);
                    if (sel)
                    {
                        float cyo = yy + oh / 2, vx = x0 + ow - 10 - dv, kx = vx - 8 - kq;
                        TeclaFlecha(kx, cyo - kq / 2 - 1, kq, i > 0 ? Rojo : Color4.Hex(0x687086), true);
                        TeclaFlecha(kx, cyo + kq / 2 + 1, kq, i < Controlador.AyudaOpciones - 1 ? Rojo : Color4.Hex(0x687086), false);
                        float late = 0.75f + 0.25f * (float)Math.Sin(t * 4);
                        d.Brillo(vx + dv / 2, cyo, dv * 0.8f, VerdeBoton.ConAlfa(0.40f * late));
                        Circulo(vx + dv / 2, cyo, dv / 2, Color4.Hex(0x46C869));
                    }
                }
                // el pie: flechas rojas (mover), VERDE (elegir), ROJO (salir)
                float ld = Math.Max(12, Math.Min(26, (int)(hb * .046f))), lcy = gy + (int)(hb * .955f), lx = x0;
                TeclaFlecha(lx, lcy, ld, Rojo, true); lx += ld + 3;
                TeclaFlecha(lx, lcy, ld, Rojo, false); lx += ld + 6;
                lx += pequeno.Escribe(d, "mover", lx, lcy - pequeno.Tam * 0.6f, azulT) + ld;
                Circulo(lx + ld / 2, lcy, ld / 2, Color4.Hex(0x46C869)); lx += ld + 6;
                lx += pequeno.Escribe(d, "elegir", lx, lcy - pequeno.Tam * 0.6f, azulT) + ld;
                Circulo(lx + ld / 2, lcy, ld / 2, Rojo); lx += ld + 6;
                pequeno.Escribe(d, "salir", lx, lcy - pequeno.Tam * 0.6f, azulT);
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

        private float veloAyuda;
        // Una tecla de flecha (TeclaFlecha de XP): cuadrito con filo del color y su triangulo, centrado en 'cy'.
        private void TeclaFlecha(float x, float cy, float q, Color4 c, bool arriba)
        {
            float y = cy - q / 2;
            d.Redondo(x, y, q, q, 3 * k, Color4.Hex(0x2A0F18, 0.9f));
            d.Contorno(x, y, q, q, 3 * k, 1.3f * k, c);
            if (arriba) d.Triangulo(x + q / 2, y + q * 0.22f, x + q * 0.80f, y + q * 0.72f, x + q * 0.20f, y + q * 0.72f, c);
            else d.Triangulo(x + q * 0.20f, y + q * 0.28f, x + q * 0.80f, y + q * 0.28f, x + q / 2, y + q * 0.78f, c);
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
        // Los renglones se recuerdan (los mismos textos se reparten en cada cuadro); se da una lista nueva porque quien la pide la recorta.
        private static readonly Dictionary<(string, Letra, float), string[]> envueltos = new Dictionary<(string, Letra, float), string[]>();
        private static List<string> Envuelve(string s, Letra l, float ancho)
        {
            string[] h;
            if (!envueltos.TryGetValue((s ?? "", l, ancho), out h))
            {
                if (envueltos.Count >= 512) envueltos.Clear();
                envueltos[(s ?? "", l, ancho)] = h = Reparte(s, l, ancho).ToArray();
            }
            return new List<string>(h);
        }
        private static List<string> Reparte(string s, Letra l, float ancho)
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
