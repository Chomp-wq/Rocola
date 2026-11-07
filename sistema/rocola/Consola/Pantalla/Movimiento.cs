// EL MOVIMIENTO (decision del 7-11-2026: «Quiero rediseñar todas las animaciones y transiciones para hacerlas mas esteticas, fluidas y
// menos bruscas»). Solo Rocola OS. Un solo sistema para todo lo que se mueve al usar la rocola, con las mismas curvas:
//  M1  TRANSICIONES ENTRE PANTALLAS (transiciones=): nada aparece de golpe. Un disco crece desde su portada (o desde su casilla del
//      buscador); al volver, la pared se asienta desde un poco mas cerca; el buscador baja; al cambiar de letra la pared se desliza hacia
//      donde se movio; los ajustes aparecen creciendo un poco. 0,26 a 0,36 s, saliendo rapido y llegando suave (sin rebote).
//  M2  LA SELECCION SE DESLIZA (seleccion_desliza=): la fila elegida de un disco, la pastilla de la letra, la tecla y el resultado del
//      buscador y el renglon de los ajustes viajan a su nuevo sitio (un resorte amortiguado de ~0,15 s) en vez de saltar.
//  M3  LA LISTA SE DESPLAZA SUAVE (listas_suaves=): la lista de canciones de un disco se corre suave, sin brincar de pagina; las filas
//      que entran y salen por los bordes se desvanecen.
// Nada de esto hace esperar: los botones actuan al momento y lo que se mueve solo alcanza lo que ya paso. Todo se calcula con el reloj
// (no por cuadro), asi que con pocos cuadros por segundo dura lo mismo.
using System;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        // ---- las curvas ----
        private static float SaleSuave(float u) { u = Math.Max(0f, Math.Min(1f, u)); float v = 1f - u; return 1f - v * v * v; }          // rapida al salir, suave al llegar
        private static float SaleMuySuave(float u) { u = Math.Max(0f, Math.Min(1f, u)); float v = 1f - u; return 1f - v * v * v * v * v; }   // para crecer desde una casilla

        // ---- M2: un rectangulo que sigue a su meta como un resorte amortiguado ----
        // La meta se pone mientras se dibuja la lista (donde quedo lo elegido en ESTE cuadro) y el resalte se dibuja ANTES de la lista con
        // la del cuadro anterior: asi va debajo de los textos (un cuadro de retraso no se ve). Si deja de dibujarse (otra pantalla), al
        // volver aparece en su sitio, sin cruzar la pantalla.
        private sealed class Sigue
        {
            private float x, y, w, h, mx, my, mw, mh; private bool hay; private double t; private int cuadro = -10;
            internal void Meta(Zona z)
            {
                if (!hay) { x = z.X; y = z.Y; w = z.W; h = z.H; hay = true; }
                mx = z.X; my = z.Y; mw = z.W; mh = z.H; cuadro = Reloj.Cuadros;
            }
            internal bool Ahora(double ahora, out Zona z)
            {
                z = new Zona();
                if (!hay) return false;
                if (Reloj.Cuadros - cuadro > 2) { hay = false; return false; }
                float dt = (float)Math.Max(0, Math.Min(0.1, ahora - t)); t = ahora;
                float a = 1f - (float)Math.Exp(-dt / 0.05);
                x += (mx - x) * a; y += (my - y) * a; w += (mw - w) * a; h += (mh - h) * a;
                if (Math.Abs(mx - x) < 0.3f && Math.Abs(my - y) < 0.3f) { x = mx; y = my; w = mw; h = mh; }
                z = new Zona(x, y, w, h);
                return true;
            }
            internal float Lejos { get { return Math.Abs(mx - x) + Math.Abs(my - y) + Math.Abs(mw - w) * 0.5f; } }
        }
        // ---- M3: un numero que sigue a su meta igual de suave ----
        private sealed class SigueValor
        {
            private float v; private bool hay; private double t; private int cuadro = -10;
            internal float Hacia(float meta, double ahora, float tau = 0.07f)
            {
                if (!hay || Reloj.Cuadros - cuadro > 2) { v = meta; hay = true; t = ahora; }
                cuadro = Reloj.Cuadros;
                float dt = (float)Math.Max(0, Math.Min(0.1, ahora - t)); t = ahora;
                v += (meta - v) * (1f - (float)Math.Exp(-dt / tau));
                if (Math.Abs(meta - v) < 0.002f) v = meta;
                return v;
            }
        }
        private readonly Sigue selCancion = new Sigue(), selLetra = new Sigue(), selTecla = new Sigue();
        private readonly SigueValor desdeLista = new SigueValor();

        // ---- M1: las transiciones entre pantallas ----
        // Las escenas: 0 la pared, 1 un disco, 2 el buscador, 3 la atraccion, 4 los ajustes, 5 el asistente de los botones.
        private enum Paso { Nada, Crece, Asienta, Baja, Lado, Abre, Aparece }
        private int escenaAnt = -1, letraAnt = -1; private object dentroAnt;
        private double transDesde = -1; private Paso trans; private Zona transZona; private float transLado, transDura; private bool transVerde;
        private float cabAlfa = -1f; private double cabT;
        private static int Escena(Controlador c)
        {
            if (c.AsistentePaso >= 0) return 5;
            if (c.Atraccion && c.AtraccionActual != null) return 3;
            if (c.AjusteSel >= 0) return 4;
            return c.Buscando ? 2 : c.dentro != null ? 1 : 0;
        }
        // Una vez por cuadro, antes de dibujar nada: si cambio la escena (o el disco, o la letra), empieza su transicion.
        private void MiraEscena(Controlador c, float W, float H)
        {
            int esc = Escena(c);
            bool cambio = escenaAnt >= 0 && (esc != escenaAnt || (esc == 1 && !object.ReferenceEquals(c.dentro, dentroAnt)));
            bool otraLetra = esc == 0 && escenaAnt == 0 && letraAnt >= 0 && c.letra != letraAnt && c.letras.Count > 1;
            if (cambio)
            {
                transDesde = reloj.Elapsed.TotalMilliseconds; transVerde = c.ZoomDesdeBuscador != 0; c.ZoomDesdeBuscador = 0;
                if (esc == 1 && escenaAnt == 2 && !zSelResultado.Vacia) { trans = Paso.Crece; transZona = zSelResultado; transDura = 380f; }
                else if (esc == 1 && escenaAnt == 0 && !zElegido.Vacia) { trans = Paso.Crece; transZona = zElegido; transDura = 360f; }
                else if (esc == 2) { trans = Paso.Baja; transDura = 280f; }
                else if (esc == 4) { trans = Paso.Abre; transDura = 240f; }
                else if (esc == 3) { trans = Paso.Aparece; transDura = 900f; }      // la atraccion llega despacio: nadie la esta usando
                else if (esc == 5) { trans = Paso.Aparece; transDura = 300f; }
                else { trans = Paso.Asienta; transDura = escenaAnt == 3 ? 340f : 280f; }   // de vuelta a la pared, o de un disco a otro (la tira)
            }
            else if (otraLetra)
            {
                int n = c.letras.Count, dif = ((c.letra - letraAnt) % n + n) % n;
                transDesde = reloj.Elapsed.TotalMilliseconds; trans = Paso.Lado; transDura = 260f; transLado = dif <= n / 2 ? 1f : -1f;
            }
            escenaAnt = esc; dentroAnt = c.dentro; letraAnt = c.letra;
            if (transDesde >= 0 && (!c.motor.Transiciones || reloj.Elapsed.TotalMilliseconds - transDesde >= transDura)) transDesde = -1;
        }
        // Pone el escalado y el alfa de la transicion en curso en el dibujo (Dibujo.Zoom) para lo que se dibuje hasta FinTransicion.
        private void Transicion(float W, float H)
        {
            if (transDesde < 0) return;
            float u = (float)((reloj.Elapsed.TotalMilliseconds - transDesde) / transDura);
            if (u >= 1f || u < 0f) { transDesde = -1; return; }
            float e = trans == Paso.Crece ? SaleMuySuave(u) : SaleSuave(u);
            float s = 1f, ox = 0f, oy = 0f, a = 1f;
            switch (trans)
            {
                case Paso.Crece:
                {
                    // de la casilla (o la portada) a la pantalla entera, aclarandose
                    float x = transZona.X * (1 - e), y = transZona.Y * (1 - e), w = transZona.W + (W - transZona.W) * e, h = transZona.H + (H - transZona.H) * e;
                    s = w / W; ox = x; oy = y + h / 2 - H * s / 2; a = Math.Min(1f, 0.2f + e * 1.3f);
                    d.Brillo(x + w / 2, y + h / 2, Math.Max(w, h) * 0.45f, transVerde ? new Color4(0.45f, 0.85f, 0.55f, 0.18f * (1f - e)) : new Color4(0.55f, 0.75f, 1f, 0.16f * (1f - e)));
                    break;
                }
                case Paso.Asienta:
                {
                    // un paso hacia atras: de un 4 % mas cerca a su sitio, desde la portada que estaba elegida si se sabe
                    s = 1.04f - 0.04f * e; float cx = zElegido.Vacia ? W / 2 : zElegido.Cx, cy = zElegido.Vacia ? H / 2 : zElegido.Cy;
                    ox = cx * (1 - s); oy = cy * (1 - s); a = 0.12f + 0.88f * e;
                    break;
                }
                case Paso.Baja: oy = -30 * k * (1f - e); a = e; break;
                case Paso.Lado: ox = transLado * 70 * k * (1f - e); a = 0.1f + 0.9f * e; break;
                case Paso.Abre: s = 0.965f + 0.035f * e; ox = W / 2 * (1 - s); oy = H / 2 * (1 - s); a = e; break;
                case Paso.Aparece: s = 1.025f - 0.025f * e; ox = W / 2 * (1 - s); oy = H / 2 * (1 - s); a = e * e * (3 - 2 * e); break;
            }
            d.Zoom = true; d.ZoomS = s; d.ZoomX = ox; d.ZoomY = oy; d.ZoomA = a;
        }
        private void FinTransicion() { d.Zoom = false; }
        // La cabecera (los creditos y la hora) no esta en la atraccion ni en el buscador: se va y vuelve con un fundido de 0,25 s.
        private void CabeceraSuave(Controlador c, float W, bool quiere)
        {
            double t = reloj.Elapsed.TotalSeconds, dt = Math.Max(0, Math.Min(0.1, t - cabT)); cabT = t;
            if (cabAlfa < 0f || !c.motor.Transiciones) cabAlfa = quiere ? 1f : 0f;
            else cabAlfa = Math.Max(0f, Math.Min(1f, cabAlfa + (float)(quiere ? dt / 0.25 : -dt / 0.25)));
            if (cabAlfa <= 0f) return;
            if (cabAlfa < 1f) { d.Zoom = true; d.ZoomS = 1f; d.ZoomX = 0f; d.ZoomY = (1f - SaleSuave(cabAlfa)) * -10 * k; d.ZoomA = SaleSuave(cabAlfa); }
            Cabecera(c, W);
            d.Zoom = false;
        }
        // M2 sobre lo que tiene fondo propio (las teclas, las casillas de resultados): la elegida se pinta en su sitio y un aro de luz
        // viaja desde la anterior; se desvanece al llegar. Se llama DESPUES de dibujar la lista (con la meta de este cuadro).
        private void AroQueViaja(Controlador c, Sigue s, float radio)
        {
            Zona z;
            if (!c.motor.SeleccionDesliza || !s.Ahora(reloj.Elapsed.TotalSeconds, out z)) return;
            float a = Math.Min(1f, s.Lejos / (18 * k));
            if (a <= 0.02f) return;
            d.Brillo(z.Cx, z.Cy, Math.Max(z.W, z.H) * 0.7f, Color4.Hex(0x6FA8FF, 0.30f * a));
            d.Redondo(z.X, z.Y, z.W, z.H, radio, Color4.Hex(0x3A6CFF, 0.38f * a));
            d.Contorno(z.X, z.Y, z.W, z.H, radio, 2f * k, Color4.Hex(0xB8D0FF, 0.95f * a));
        }
        // M3: lo de una fila que entra o sale por un borde, desvanecido (por encima de la transicion, si la hay).
        private float zoomBaseA = 1f; private bool zoomBase;
        private void AlfaFila(float a)
        {
            if (a >= 0.999f) { d.Zoom = zoomBase; d.ZoomA = zoomBaseA; return; }
            if (!zoomBase) { d.ZoomS = 1f; d.ZoomX = 0f; d.ZoomY = 0f; }
            d.Zoom = true; d.ZoomA = zoomBaseA * Math.Max(0f, a);
        }
    }
}
