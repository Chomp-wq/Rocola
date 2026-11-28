// EL CIELO VIVO DE ROCOLA OS, GRUPO «NUBES Y CLIMA»: DN6 (LAS NUBES) Y CL1 (LAS NUBES DE AFUERA). PLAN_CIELO_VIVO.md §1, §3 y §8; FASE11_cielo-clima.md.
// Solo la tarjeta (el dibujo por procesador esta congelado). Claves nubes= (funcion cielo) y la funcion clima (CL1: su clave es la funcion misma).
//
// DN6 (nubes=): nubes pintadas, sin texturas: cada una son 8 manchas blandas (un abanico de 10 triangulos con el alfa que baja de 0,8 a 0 hacia
// el borde) con el color en los vertices, que va de la sombra (abajo, al lado contrario del astro) a la luz (arriba, del lado del sol o de la
// luna): el color y la luz salen de la hora del cielo (la foto: la altura del sol y de la luna, su fase, la raya de la paleta de §5). Cruzan el
// cielo despacio, de derecha a izquierda o al reves segun el viento (la rocola mira al sur: el este queda a la izquierda, como el sol), y salen
// DETRAS de todo (capa TrasNieblas, detras de la aurora y de las estrellas; las estrellas que quedan tras una nube se apagan con
// RocolaOsCieloEstrella: g127, sin gancho nuevo) y, delante de la luna de XP, una pasada con las que pasan por su sitio (capa ConLaLuna).
// SIN CLIMA (o sin internet, o con el clima caducado) son pocas y blancas: un adorno. Con CL1 y el clima sabido, las de afuera: cuantas segun
// la nubosidad de Open-Meteo, grises y oscuras con la lluvia y la tormenta, el cielo del degradado mas gris (un tinte, g121/g122: sin
// gancho nuevo: el color mezclado hacia su gris y un poco mas oscuro, nunca mas claro), y la velocidad y el sentido de la deriva con el
// viento de verdad (km/h y direccion). Las nubes del clima son las de DN6: con nubes=0 el clima solo pone lluvia, neblina y la pastilla.
//
// REGLA 3 (siempre se lee): ningun color de nube pasa de luminancia 0,15 (de dia; de noche y de madrugada menos aun, por la luz que haya), el
// cielo de debajo no pasa de 0,17 hasta 0,848 del alto: mezclado con alfa, lo que sale es siempre menor que el mayor de los dos. Las nubes
// no bajan de 0,76 del horizonte (la franja clara es de la raya y su resplandor, DN5).
// REGLA 5 (calma): mientras alguien elige la deriva se frena a 0,35 y las nubes pierden la mitad de su alfa (0,5 + 0,5 x calma): se ven mas
// quietas y tenues. REGLA 1: con todo apagado, ni se arma ni se pinta nada; de noche cerrada sin clima, solo si nubes=1 y entonces por la luz
// de la luna (poca, 0,03 de luminancia sin luna: la nube se ve como una silueta gris sobre las estrellas).
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace Rocola
{
    // LA LUZ, EL CLIMA Y LO QUE SE VE: lo que calculan juntos las nubes, la lluvia y la neblina cada cuadro (una sola vez por cuadro: la primera que
    // lo pide). Los numeros suben o bajan en 1,5 s hacia lo de la foto (no hay saltos al cambiar el clima ni con «Ver como se veria»).
    internal static class RocolaOsCieloClimaVista
    {
        // lo de ahora, suavizado
        internal static float Nubes, Gris, Lluvia, Nieve, Niebla, Viento, DirX;   // Nubes 0-1 (cuantas), Gris 0-1, Lluvia y Nieve 0-1 (cuanta), Niebla 0-1, Viento km/h, DirX -1 a 1 (hacia donde va)
        internal static float Sol, Luna, Nivel;                                          // la luz del sol, de la luna y la total (0-1)
        internal static float LitR, LitG, LitB, ShadeR, ShadeG, ShadeB, AlfaNube;        // los colores de las nubes (0-255) y su alfa de cuerpo
        internal static float Tormenta;                                                  // 0-1 (si hay relampagos)
        internal static float VientoK = 1f;                                              // cuanto se menea lo de las fiestas con el viento (1 sin viento, 2,4 con 40 km/h o mas): papel picado, copos, llamas
        internal static float AurR, AurG, AurB, AurK;                                    // el color de la aurora de ahora y cuanta hay (la base de las nubes de noche toma un poco)
        internal static double Deriva;                                                   // lo que han corrido las nubes (px de un viento de 1): crece con el tiempo y la calma
        internal static bool Hay;                                                        // hay clima que ver (sabido y encendido)
        private static double msUltimo = -1; private static bool listo;

        // lo que se quiere (de la foto): nubosidad y gris por tipo de clima
        internal static void Objetivo(RocolaOsCieloFoto f, out float nubes, out float gris, out float lluvia, out float nieve, out float niebla, out float tormenta)
        {
            nubes = 0.22f; gris = 0f; lluvia = 0f; nieve = 0f; niebla = 0f; tormenta = 0f;   // sin clima: un adorno
            if (f == null || !f.ClimaSabido || !f.ClimaVa) return;
            RocolaOsClima c = f.Clima;
            float n = c.Nubes;
            switch (c.Tipo)
            {
                case RocolaOsClimaTipo.Despejado: nubes = Math.Max(n, 0.12f); break;
                case RocolaOsClimaTipo.PocoNublado: nubes = Math.Max(n, 0.40f); gris = 0.05f; break;
                case RocolaOsClimaTipo.Nublado: nubes = Math.Max(n, 0.85f); gris = 0.40f; break;
                case RocolaOsClimaTipo.Neblina: nubes = 0.35f; gris = 0.30f; niebla = Niebla01(c.Visibilidad); break;
                case RocolaOsClimaTipo.Llovizna: nubes = Math.Max(n, 0.85f); gris = 0.50f; lluvia = 0.30f; niebla = 0.30f; break;
                case RocolaOsClimaTipo.Lluvia: nubes = Math.Max(n, 0.90f); gris = 0.65f; lluvia = Math.Max(0.45f, Math.Min(0.8f, 0.45f + c.Lluvia * 0.08f)); niebla = 0.12f; break;
                case RocolaOsClimaTipo.Aguacero: nubes = 1f; gris = 0.80f; lluvia = 1f; niebla = 0.10f; break;
                case RocolaOsClimaTipo.Tormenta: nubes = 1f; gris = 0.90f; lluvia = 0.9f; tormenta = 1f; break;
                case RocolaOsClimaTipo.Nieve: nubes = Math.Max(n, 0.85f); gris = 0.45f; nieve = Math.Max(0.5f, Math.Min(1f, 0.5f + c.Lluvia * 0.1f)); niebla = 0.30f; break;
            }
        }
        // la neblina por la visibilidad (m): menos de 1 km, entera; 3 km o mas, a 0,6; sin dato, entera
        internal static float Niebla01(float visibilidadM)
        {
            if (float.IsNaN(visibilidadM) || visibilidadM <= 1000f) return 1f;
            if (visibilidadM >= 3000f) return 0.6f;
            return 1f - 0.4f * (visibilidadM - 1000f) / 2000f;
        }
        // hacia donde se mueve en la pantalla lo que lleva el viento, en x: la direccion es de donde viene (0 norte, 90 este, 270 oeste); la rocola
        // mira al sur, el este queda a la izquierda (como el sol): viento del oeste (270) va hacia el este = a la izquierda (-1); del este (90), a
        // la derecha (+1). Con minimo de 0,3 en valor absoluto (un viento del norte o del sur tambien las mueve, poco)
        internal static float DireccionX(float direccionGrados)
        {
            float v = (float)Math.Sin(direccionGrados * Math.PI / 180.0);
            if (Math.Abs(v) < 0.3f) v = v >= 0f ? 0.3f : -0.3f;
            return v;
        }
        // la velocidad de deriva en px/s de un viento (km/h), 3 a 30 (sin viento sabido, 3,5)
        internal static float VelocidadPx(float vientoKmh, bool sabido) { return sabido ? Math.Max(3f, Math.Min(30f, vientoKmh * 0.9f)) : 3.5f; }

        // cuanto se menea lo de las fiestas con un viento (km/h): 1 sin viento, 2,4 con 40 o mas
        internal static float KDeViento(float kmh) { return 1f + 1.4f * Math.Min(1f, Math.Max(0f, kmh) / 40f); }
        private static float Hacia(float v, float objetivo, float k) { return v + (objetivo - v) * k; }
        // UNA VEZ POR CUADRO (la escena de este cuadro ya esta puesta: Prepara del cuadro)
        internal static void Actualiza()
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f == null) { Hay = false; return; }
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            if (listo && e.Ms == msUltimo) return;
            msUltimo = e.Ms;
            float dt = (float)e.Dt;
            float kf = listo ? 1f - (float)Math.Exp(-dt / 1.5) : 1f;
            float nubes, gris, lluvia, nieve, niebla, tormenta;
            Objetivo(f, out nubes, out gris, out lluvia, out nieve, out niebla, out tormenta);
            Hay = f.ClimaVa && f.ClimaSabido;
            Nubes = Hacia(Nubes, nubes, kf); Gris = Hacia(Gris, gris, kf); Lluvia = Hacia(Lluvia, lluvia, kf); Nieve = Hacia(Nieve, nieve, kf);
            Niebla = Hacia(Niebla, niebla, kf); Tormenta = Hacia(Tormenta, tormenta, kf);
            RocolaOsClima c = f.Clima;
            float viento = Hay ? c.Viento : 0f, ve = Hay ? DireccionX(c.Direccion) : -1f;   // sin clima: de derecha a izquierda, despacio
            Viento = Hacia(Viento, viento, kf); DirX = Hacia(DirX, ve, kf);
            VientoK = KDeViento(Viento);
            // la deriva: px de un viento de 1 (el sentido y la velocidad se ponen aparte); se frena con la calma (regla 5)
            Deriva += dt * e.Calma;
            // la luz (de la foto: los pesos de §5 ya mezclados por 1 s)
            float sl = RocolaOsCieloAstros.Paso(f.SolAlt, -6.0, 10.0);
            float ml = RocolaOsCieloAstros.Paso(f.LunaAlt, -3.0, 8.0) * (float)Math.Max(0.0, Math.Min(1.0, f.LunaLuz)) * (1f - sl);
            Sol = Hacia(Sol, sl, kf); Luna = Hacia(Luna, ml, kf);
            Nivel = Math.Max(Sol, Math.Min(1f, Luna * 0.5f));
            Colores(f, Sol, Luna, Gris, out LitR, out LitG, out LitB, out ShadeR, out ShadeG, out ShadeB);
            AlfaNube = Math.Min(0.85f, 0.30f + 0.55f * Math.Max(Sol, Math.Min(1f, Luna * 3f)));
            listo = true;
        }
        // LA AURORA DE AHORA (la lee Prepara de las nubes: la de XP no se toca), suavizada en 1,5 s
        internal static void Aurora(GpuPanel p, double dt)
        {
            float r, g, b, k = p.RocolaOsAuroraMedia(out r, out g, out b), kf = 1f - (float)Math.Exp(-dt / 1.5);
            AurK += (k - AurK) * kf;
            if (k > 0.02f) { AurR += (r - AurR) * kf; AurG += (g - AurG) * kf; AurB += (b - AurB) * kf; }
        }
        // LOS COLORES DE UNA NUBE (0-255): la luz (lo que da el astro: el sol blanco que se hace del color de la raya al bajar; la luna azulada) y la
        // sombra, con el gris del clima. Su luminancia NO pasa de 0,15 x sol + 0,075 x luna + 0,03 (la regla 3). Pura (la prueba la recorre).
        internal static void Colores(RocolaOsCieloFoto f, float sol, float luna, float gris, out float lr, out float lg, out float lb, out float sr, out float sg, out float sb)
        {
            // el color de la luz: el sol (blanco que se calienta al bajar), la luna (azul palido)
            float calido = 1f - RocolaOsCieloAstros.Paso(f.SolAlt, 4.0, 28.0);
            float rr = 255f, rg = 150f, rb = 80f;
            if (f.Raya != 0) { rr = (f.Raya >> 16) & 255; rg = (f.Raya >> 8) & 255; rb = f.Raya & 255; }
            float solR = 236f + (rr - 236f) * calido * 0.85f, solG = 242f + (rg - 242f) * calido * 0.85f, solB = 252f + (rb - 252f) * calido * 0.85f;
            float lunR = 170f, lunG = 186f, lunB = 226f;
            float k = sol + luna > 0.0001f ? sol / (sol + luna) : 0f;   // la parte del sol
            float cr = lunR + (solR - lunR) * k, cg = lunG + (solG - lunG) * k, cb = lunB + (solB - lunB) * k;
            // a luz lineal
            double R = Lin(cr), G = Lin(cg), B = Lin(cb);
            double gr = 0.2126 * R + 0.7152 * G + 0.0722 * B;
            // el gris del clima (lluvia, tormenta): la luz pierde color y fuerza
            double kg = gris * 0.8;
            R += (gr - R) * kg; G += (gr - G) * kg; B += (gr - B) * kg;
            double tope = 0.15 * sol + 0.075 * luna + 0.03;                 // la regla 3
            tope *= 1.0 - 0.55 * gris;                                      // y mas oscuras con el mal tiempo
            double l = 0.2126 * R + 0.7152 * G + 0.0722 * B;
            double esc = l > 1e-9 ? tope / l : 0.0;
            R *= esc; G *= esc; B *= esc;
            lr = Srgb(R); lg = Srgb(G); lb = Srgb(B);
            // la sombra: la mitad de la luz, mas azul
            sr = Srgb(R * 0.40); sg = Srgb(G * 0.44); sb = Srgb(B * 0.62);
        }
        private static double Lin(float v) { double x = v / 255.0; return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
        private static float Srgb(double l) { double x = l <= 0.0031308 ? l * 12.92 : 1.055 * Math.Pow(Math.Max(0.0, l), 1.0 / 2.4) - 0.055; return (float)Math.Max(0.0, Math.Min(255.0, x * 255.0)); }
        internal static double Luz(float r, float g, float b) { return RocolaOsCieloDia.Luminancia(r, g, b); }
    }

    // DN6: LAS NUBES (y CL1)
    internal sealed class RocolaOsCieloNubes : RocolaOsCieloPintor
    {
        internal RocolaOsCieloNubes() { Quien = "DN6 las nubes"; Orden = RocolaOsCieloOrden.Nubes; }
        internal const int Max = 14, Manchas = 8, Seg = 10;
        // los bultos de cada nube y su sitio (de un azar fijo: las mismas nubes cada vez): sitio x e y (fracciones), ancho (de la pantalla), capa
        private static readonly float[] bx = new float[Max], by = new float[Max], bw = new float[Max], bv = new float[Max];
        private static readonly float[,] ox = new float[Max, Manchas], oy = new float[Max, Manchas], orr = new float[Max, Manchas];
        private static readonly float[] cosT = new float[Seg + 1], sinT = new float[Seg + 1];
        static RocolaOsCieloNubes()
        {
            for (int i = 0; i <= Seg; i++) { cosT[i] = (float)Math.Cos(i * 2 * Math.PI / Seg); sinT[i] = (float)Math.Sin(i * 2 * Math.PI / Seg); }
            for (int k = 0; k < Max; k++)
            {
                bool lejos = k % 3 == 0;                                   // la capa de atras: mas chicas, mas lentas, mas cerca del horizonte
                bx[k] = Azar(k, 1);
                by[k] = lejos ? 0.50f + 0.26f * Azar(k, 2) : 0.10f + 0.50f * Azar(k, 2);
                bw[k] = lejos ? 0.14f + 0.07f * Azar(k, 3) : 0.19f + 0.12f * Azar(k, 3);
                bv[k] = lejos ? 0.6f : 1f;
                for (int j = 0; j < Manchas; j++)
                {
                    float t = (j + 0.5f) / Manchas - 0.5f;                 // -0,5 a 0,5 a lo ancho
                    float alto = 1f - (2f * t) * (2f * t);                  // el bulto de en medio, mas alto
                    ox[k, j] = t * 0.84f + (Azar(k * 17 + j, 4) - 0.5f) * 0.06f;
                    oy[k, j] = -alto * 0.16f * (0.7f + 0.6f * Azar(k * 17 + j, 5)) + 0.04f;   // la base casi plana
                    orr[k, j] = (0.13f + 0.10f * alto) * (0.85f + 0.3f * Azar(k * 17 + j, 6));
                }
            }
        }
        private static float Azar(int i, int s)
        {
            uint a = (uint)(i * 73 + s * 977 + 12345) * 2654435761u; a ^= a >> 15; a *= 2246822519u; a ^= a >> 13; a *= 3266489917u; a ^= a >> 16;
            return (a & 0xFFFFFF) / 16777216f;
        }
        // lo de cada nube en este cuadro (la pone Prepara; lo leen el pintor, la pasada de la luna y las estrellas)
        internal static readonly float[] Cx = new float[Max], Cy = new float[Max], Rx = new float[Max], Ry = new float[Max], Al = new float[Max];
        internal static int Visibles;     // cuantas tienen alfa
        internal static float LaneY = -1f;   // la altura del astro de ahora (el sol de dia, la luna de noche): la nube 0 pasa por ahi (frente a el); <0: ninguna
        private static Tarjeta.Vtx[] buf = new Tarjeta.Vtx[Max * Manchas * Seg * 9 + 64];
        private static int nv;
        internal static bool Activa;      // DN6 puesta y con algo que ver en este cuadro

        // UN CUADRO: donde esta cada nube (de la deriva, el viento y lo que hay que ver). Pura salvo por lo que lee de RocolaOsCieloClimaVista.
        internal static void Coloca(int ancho, float horizonte, float calma)
        {
            float cuantas = RocolaOsCieloClimaVista.Nubes * Max;
            float v = RocolaOsCieloClimaVista.VelocidadPx(RocolaOsCieloClimaVista.Viento, RocolaOsCielo.Foto != null && RocolaOsCielo.Foto.ClimaSabido) * RocolaOsCieloClimaVista.DirX;
            float grande = 1f + 0.3f * Math.Max(0f, (RocolaOsCieloClimaVista.Nubes - 0.7f) / 0.3f);
            float alfa = RocolaOsCieloClimaVista.AlfaNube * (0.5f + 0.5f * calma);
            double deriva = RocolaOsCieloClimaVista.Deriva;
            Visibles = 0;
            for (int k = 0; k < Max; k++)
            {
                float vis = RocolaOsCieloAstros.Paso(cuantas - k, 0.0, 1.0);
                float w = bw[k] * ancho * grande, span = ancho + 2f * w;
                double x = (bx[k] * span + v * bv[k] * deriva) % span; if (x < 0) x += span;
                Cx[k] = (float)x - w + w * 0.5f; Cy[k] = Math.Min(by[k], 0.76f) * horizonte;
                if (k == 0 && LaneY > 0f) Cy[k] = Math.Min(LaneY + 0.05f * w, 0.76f * horizonte);   // la que pasa frente al sol o a la luna
                Rx[k] = w * 0.5f; Ry[k] = w * 0.16f;
                Al[k] = vis * alfa * (bv[k] < 1f ? 0.8f : 1f);
                if (Al[k] > 0.01f) Visibles++;
            }
        }
        internal override void Prepara(GpuPanel p)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            RocolaOsCieloClimaVista.Actualiza();
            Tinte.SubeVersion();
            if (f == null || !f.DN6) { Activa = false; Visibles = 0; return; }
            RocolaOsCieloClimaVista.Aurora(p, RocolaOsCielo.Escena.Dt);
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            if (e.Ancho < 64 || e.Alto < 64) { Activa = false; return; }
            LaneY = -1f;
            if (RocolaOsCieloClimaVista.Sol >= 0.1f && f.DN2)
            {
                float sx, sy; RocolaOsCieloAstros.Proyecta(RocolaOsCieloAstros.Este(f.SolAlt, f.SolAz), f.SolAlt, e.Ancho, e.Horizonte, out sx, out sy);
                if (sy < 0.7f * e.Horizonte && !RocolaOsCieloAstros.ConPortadas(e)) LaneY = sy;
            }
            else if (Math.Max(RocolaOsCieloClimaVista.Luna, RocolaOsCielo.NocheCuadro * 0.7f) >= 0.3f) { float cx, cy, cr; Form1.SitioLuna(e.Ancho, e.Alto, out cx, out cy, out cr); LaneY = cy; }
            Coloca(e.Ancho, e.Horizonte, e.Calma);
            Activa = Visibles > 0;
        }

        // la malla de una mancha: abanico de Seg triangulos con tres anillos (centro a, medio 0,8 a, borde 0). Los colores: la luz del lado del astro
        private static void Mancha(float cx, float cy, float rx, float ry, float a, float lx, float ly, float lr, float lg, float lb, float sr, float sg, float sb)
        {
            if (nv + Seg * 9 > buf.Length) return;
            uint c0 = ColorDe(lr, lg, lb, sr, sg, sb, 0.5f, a);
            float ra = 0.55f;
            for (int s = 0; s < Seg; s++)
            {
                float c1 = cosT[s], s1 = sinT[s], c2 = cosT[s + 1], s2 = sinT[s + 1];
                uint m1 = ColorDe(lr, lg, lb, sr, sg, sb, 0.5f + 0.5f * (c1 * lx + s1 * ly) * ra, a * 0.8f), m2 = ColorDe(lr, lg, lb, sr, sg, sb, 0.5f + 0.5f * (c2 * lx + s2 * ly) * ra, a * 0.8f);
                uint o1 = ColorDe(lr, lg, lb, sr, sg, sb, 0.5f + 0.5f * (c1 * lx + s1 * ly), 0f), o2 = ColorDe(lr, lg, lb, sr, sg, sb, 0.5f + 0.5f * (c2 * lx + s2 * ly), 0f);
                float x1 = cx + c1 * rx * ra, y1 = cy + s1 * ry * ra, x2 = cx + c2 * rx * ra, y2 = cy + s2 * ry * ra;
                float X1 = cx + c1 * rx, Y1 = cy + s1 * ry, X2 = cx + c2 * rx, Y2 = cy + s2 * ry;
                P(cx, cy, c0); P(x1, y1, m1); P(x2, y2, m2);
                P(x1, y1, m1); P(X1, Y1, o1); P(X2, Y2, o2);
                P(x1, y1, m1); P(X2, Y2, o2); P(x2, y2, m2);
            }
        }
        private static void P(float x, float y, uint c) { buf[nv].X = x; buf[nv].Y = y; buf[nv].Z = 0f; buf[nv].Rhw = 1f; buf[nv].Color = c; buf[nv].U = 0f; buf[nv].V = 0f; nv++; }
        // el color de un vertice: de la sombra (f = 0) a la luz (f = 1)
        private static uint ColorDe(float lr, float lg, float lb, float sr, float sg, float sb, float f, float a)
        {
            if (f < 0f) f = 0f; else if (f > 1f) f = 1f;
            int al = (int)(a * 255f + 0.5f); if (al < 0) al = 0; else if (al > 255) al = 255;
            int r = (int)(sr + (lr - sr) * f + 0.5f), g = (int)(sg + (lg - sg) * f + 0.5f), b = (int)(sb + (lb - sb) * f + 0.5f);
            return ((uint)al << 24) | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
        }
        // LA MALLA de las nubes visibles (o solo las que tocan un circulo de la luna: lunaX >= 0), con el astro que da la luz. Devuelve los triangulos.
        internal static int Arma(RocolaOsCieloFoto f, RocolaOsCieloEscena e, float lunaX, float lunaY, float lunaR)
        {
            nv = 0;
            // el astro que alumbra (el sol si lo hay, si no la luna): su sitio en la pantalla, para el lado de la luz
            double alt = RocolaOsCieloClimaVista.Sol >= RocolaOsCieloClimaVista.Luna ? f.SolAlt : f.LunaAlt, az = RocolaOsCieloClimaVista.Sol >= RocolaOsCieloClimaVista.Luna ? f.SolAz : f.LunaAz;
            float ax, ay; RocolaOsCieloAstros.Proyecta(RocolaOsCieloAstros.Este(alt, az), alt, e.Ancho, e.Horizonte, out ax, out ay);
            // la base de la nube de noche toma un poco del color de la aurora de ese momento (a la luminancia de su sombra x 1,7: la regla 3 no cambia)
            float sr = RocolaOsCieloClimaVista.ShadeR, sg = RocolaOsCieloClimaVista.ShadeG, sb = RocolaOsCieloClimaVista.ShadeB;
            float ta = 0.5f * RocolaOsCieloClimaVista.AurK * Math.Max(0f, 1f - RocolaOsCieloClimaVista.Sol * 1.6f);
            if (ta > 0.01f)
            {
                float la = 0.2126f * RocolaOsCieloClimaVista.AurR + 0.7152f * RocolaOsCieloClimaVista.AurG + 0.0722f * RocolaOsCieloClimaVista.AurB;
                float ls = 0.2126f * sr + 0.7152f * sg + 0.0722f * sb, esc = la > 1f ? Math.Min(1.5f, ls * 1.7f / la) : 0f;
                sr += (RocolaOsCieloClimaVista.AurR * esc - sr) * ta; sg += (RocolaOsCieloClimaVista.AurG * esc - sg) * ta; sb += (RocolaOsCieloClimaVista.AurB * esc - sb) * ta;
            }
            for (int k = 0; k < Max; k++)
            {
                float a = Al[k]; if (a <= 0.01f) continue;
                float w = Rx[k] * 2f;
                if (Cx[k] + w < 0f || Cx[k] - w > e.Ancho) continue;
                if (lunaX >= 0f)
                {
                    float dx = (lunaX - Cx[k]) / (Rx[k] * 1.1f), dy = (lunaY - Cy[k]) / (Ry[k] * 2.2f + lunaR);
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    a *= 1f - RocolaOsCieloAstros.Paso(d, 0.9, 1.7);
                    if (a <= 0.01f) continue;
                }
                float lx = Math.Max(-1f, Math.Min(1f, (ax - Cx[k]) / (0.5f * e.Ancho))) * 0.8f, ly = -0.6f - (alt > 15 ? 0.4f : 0f);
                float n = (float)Math.Sqrt(lx * lx + ly * ly); lx /= n; ly /= n;
                for (int j = 0; j < Manchas; j++)
                    Mancha(Cx[k] + ox[k, j] * w, Cy[k] + oy[k, j] * w, orr[k, j] * w, orr[k, j] * w * 0.66f, a, lx, ly,
                           RocolaOsCieloClimaVista.LitR, RocolaOsCieloClimaVista.LitG, RocolaOsCieloClimaVista.LitB,
                           sr, sg, sb);
            }
            return nv / 3;
        }
        internal static Tarjeta.Vtx[] Malla { get { return buf; } }

        // LA LUZ DE UNA NUBE (un relampago, un fuego): la forma de la nube k sumada, de un color y un alfa, con mas luz abajo (sesgo +1) o arriba (-1);
        // devuelve los triangulos (para DrawMeshAdd con MallaLuz)
        private static readonly Tarjeta.Vtx[] bufL = new Tarjeta.Vtx[Manchas * Seg * 9 + 64];
        private static int nvL;
        internal static Tarjeta.Vtx[] MallaLuz { get { return bufL; } }
        private static void PL(float x, float y, uint c) { bufL[nvL].X = x; bufL[nvL].Y = y; bufL[nvL].Z = 0f; bufL[nvL].Rhw = 1f; bufL[nvL].Color = c; bufL[nvL].U = 0f; bufL[nvL].V = 0f; nvL++; }
        private static uint ColorL(uint rgb, float a) { int al = (int)(a * 255f + 0.5f); if (al < 0) al = 0; else if (al > 255) al = 255; return ((uint)al << 24) | (rgb & 0x00FFFFFFu); }
        internal static int ArmaLuz(int k, uint rgb, float a, float sesgo)
        {
            nvL = 0;
            if (k < 0 || k >= Max || !(a > 0.002f)) return 0;
            float w = Rx[k] * 2f, ra = 0.55f;
            for (int j = 0; j < Manchas; j++)
            {
                float cx = Cx[k] + ox[k, j] * w, cy = Cy[k] + oy[k, j] * w, rx = orr[k, j] * w, ry = orr[k, j] * w * 0.66f;
                uint c0 = ColorL(rgb, a);
                for (int s = 0; s < Seg; s++)
                {
                    float c1 = cosT[s], s1 = sinT[s], c2 = cosT[s + 1], s2 = sinT[s + 1];
                    uint m1 = ColorL(rgb, a * 0.8f * Math.Max(0f, 1f + 0.5f * sesgo * s1 * ra)), m2 = ColorL(rgb, a * 0.8f * Math.Max(0f, 1f + 0.5f * sesgo * s2 * ra)), o = ColorL(rgb, 0f);
                    float x1 = cx + c1 * rx * ra, y1 = cy + s1 * ry * ra, x2 = cx + c2 * rx * ra, y2 = cy + s2 * ry * ra, X1 = cx + c1 * rx, Y1 = cy + s1 * ry, X2 = cx + c2 * rx, Y2 = cy + s2 * ry;
                    PL(cx, cy, c0); PL(x1, y1, m1); PL(x2, y2, m2);
                    PL(x1, y1, m1); PL(X1, Y1, o); PL(X2, Y2, o);
                    PL(x1, y1, m1); PL(X2, Y2, o); PL(x2, y2, m2);
                }
            }
            return nvL / 3;
        }

        // EL BORDE BRILLANTE (la nube frente a la luna: plateado; frente al sol: calido): de los bordes de las nubes que tocan el astro (ax, ay, aR), una
        // banda sumada que brilla mas cuanto mas cerca del astro; devuelve los triangulos (DrawMeshAdd con MallaBorde)
        private static readonly Tarjeta.Vtx[] bufB = new Tarjeta.Vtx[Max * Manchas * Seg * 6 + 64];
        private static int nvB;
        private static readonly float[] expo = new float[Seg + 1];
        internal static Tarjeta.Vtx[] MallaBorde { get { return bufB; } }
        private static void PB(float x, float y, uint c) { bufB[nvB].X = x; bufB[nvB].Y = y; bufB[nvB].Z = 0f; bufB[nvB].Rhw = 1f; bufB[nvB].Color = c; bufB[nvB].U = 0f; bufB[nvB].V = 0f; nvB++; }
        private static float Cerca(float x, float y, float ax, float ay, float R2)
        {
            float dx = x - ax, dy = y - ay, d = (float)Math.Sqrt(dx * dx + dy * dy);
            float t = 1f - d / R2; if (t <= 0f) return 0f;
            return t * t;
        }
        internal static int ArmaBorde(float ax, float ay, float aR, uint rgb, float fuerza)
        {
            nvB = 0;
            if (!(fuerza > 0.01f) || !(aR > 1f)) return 0;
            float R2 = aR * 2.4f, ra = 0.55f;
            for (int k = 0; k < Max; k++)
            {
                float al = Al[k]; if (al <= 0.01f) continue;
                float dx = ax - Cx[k], dy = ay - Cy[k];
                if (dx * dx + dy * dy > (Rx[k] * 1.5f + R2) * (Rx[k] * 1.5f + R2)) continue;
                float w = Rx[k] * 2f, q = fuerza * Math.Min(1f, al / 0.6f);
                for (int j = 0; j < Manchas; j++)
                {
                    float cx = Cx[k] + ox[k, j] * w, cy = Cy[k] + oy[k, j] * w, rx = orr[k, j] * w, ry = orr[k, j] * w * 0.66f;
                    // solo el contorno de la nube: el borde de una mancha que queda dentro de otra no brilla (si no se verian aros)
                    for (int q2 = 0; q2 <= Seg; q2++)
                    {
                        float X = cx + cosT[q2] * rx, Y = cy + sinT[q2] * ry, emin = 9f;
                        for (int j2 = 0; j2 < Manchas; j2++)
                        {
                            if (j2 == j) continue;
                            float dx2 = (X - (Cx[k] + ox[k, j2] * w)) / (orr[k, j2] * w), dy2 = (Y - (Cy[k] + oy[k, j2] * w)) / (orr[k, j2] * w * 0.66f), v = dx2 * dx2 + dy2 * dy2;
                            if (v < emin) emin = v;
                        }
                        expo[q2] = RocolaOsCieloAstros.Paso(emin, 0.55, 1.0);
                    }
                    for (int s = 0; s < Seg; s++)
                    {
                        if (nvB + 6 > bufB.Length) return nvB / 3;
                        float c1 = cosT[s], s1 = sinT[s], c2 = cosT[s + 1], s2 = sinT[s + 1];
                        float x1 = cx + c1 * rx * ra, y1 = cy + s1 * ry * ra, x2 = cx + c2 * rx * ra, y2 = cy + s2 * ry * ra, X1 = cx + c1 * rx, Y1 = cy + s1 * ry, X2 = cx + c2 * rx, Y2 = cy + s2 * ry;
                        float am1 = 0.42f * q * Cerca(x1, y1, ax, ay, R2) * expo[s], am2 = 0.42f * q * Cerca(x2, y2, ax, ay, R2) * expo[s + 1], ao1 = q * Cerca(X1, Y1, ax, ay, R2) * 0.2f * expo[s], ao2 = q * Cerca(X2, Y2, ax, ay, R2) * 0.2f * expo[s + 1];
                        if (am1 + am2 + ao1 + ao2 < 0.004f) continue;
                        PB(x1, y1, ColorL(rgb, am1)); PB(X1, Y1, ColorL(rgb, ao1)); PB(X2, Y2, ColorL(rgb, ao2));
                        PB(x1, y1, ColorL(rgb, am1)); PB(X2, Y2, ColorL(rgb, ao2)); PB(x2, y2, ColorL(rgb, am2));
                    }
                }
            }
            return nvB / 3;
        }
        internal override void Pinta(GpuPanel p, int capa)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (!Activa || f == null || !f.DN6) return;
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            GpuStarfield st = p.RocolaOsEstrellas;
            if (st == null) return;
            int tris;
            if (capa == RocolaOsCieloCapa.TrasNieblas) tris = Arma(f, e, -1f, -1f, 0f);
            else if (capa == RocolaOsCieloCapa.ConLaLuna)
            {
                // delante de la luna de XP: las nubes que pasan por su sitio (solo con la luna a la vista: de noche o al anochecer)
                if (RocolaOsCieloClimaVista.Luna <= 0.02f && RocolaOsCielo.NocheCuadro < 0.3f) return;
                float cx, cy, r; Form1.SitioLuna(e.Ancho, e.Alto, out cx, out cy, out r);
                tris = Arma(f, e, cx, cy, r);
            }
            else return;
            if (tris > 0) st.DrawMesh(buf, tris);
            Borde(st, e, f, capa);
            if (capa == RocolaOsCieloCapa.TrasNieblas) RocolaOsCieloNubesRelampago.Pinta(st, e, f);
        }

        // el borde brillante de las nubes frente al sol (en la capa de atras: el sol esta ahi) y frente a la luna (en la capa de la luna)
        private static void Borde(GpuStarfield st, RocolaOsCieloEscena e, RocolaOsCieloFoto f, int capa)
        {
            float ax, ay, aR, fuerza; uint rgb;
            if (capa == RocolaOsCieloCapa.TrasNieblas)
            {
                if (RocolaOsCieloClimaVista.Sol < 0.05f || !f.DN2) return;
                if (RocolaOsCieloAstros.ConPortadas(e)) return;   // (en la pared el sol apenas asoma)
                RocolaOsCieloAstros.Proyecta(RocolaOsCieloAstros.Este(f.SolAlt, f.SolAz), f.SolAlt, e.Ancho, e.Horizonte, out ax, out ay);
                if (ay > e.Horizonte) return;
                aR = Math.Max(10f, e.Alto * RocolaOsCieloSol.RadioDelAlto); fuerza = 0.8f * RocolaOsCieloClimaVista.Sol; rgb = 0xFFF2D0;
            }
            else
            {
                float luna = Math.Max(RocolaOsCieloClimaVista.Luna, RocolaOsCielo.NocheCuadro * 0.7f); if (luna < 0.05f) return;
                Form1.SitioLuna(e.Ancho, e.Alto, out ax, out ay, out aR);
                fuerza = 0.85f * luna * (0.4f + 0.6f * (float)Math.Max(0.0, Math.Min(1.0, f.LunaLuz))); rgb = 0xE8EAFA;
            }
            fuerza *= 1f - 0.8f * RocolaOsClimaLuces.Proteccion(ax, ay);
            int t = ArmaBorde(ax, ay, aR, rgb, fuerza);
            if (t > 0) st.DrawMeshAdd(bufB, t);
        }
        // EL BRILLO DE LAS ESTRELLAS tras una nube (g127): de 1 (sin nube) a 0,15 (bajo una nube entera)
        internal sealed class Estrellas : RocolaOsCieloEstrella
        {
            internal Estrellas() { Quien = "DN6 las estrellas tras las nubes"; Orden = RocolaOsCieloOrden.Nubes; }
            internal override bool Activo { get { return (Activa && Visibles > 0) || RocolaOsCieloClimaVista.Nieve > 0.02f; } }
            // (con nieve, las estrellas un poco mas tenues: hasta 0,72 con nieve total)
            internal override float Brillo(bool actual, int i, float x, float y, int tamano) { return (Activa && Visibles > 0 ? Cobertura(x, y) : 1f) * (1f - 0.28f * Math.Min(1f, RocolaOsCieloClimaVista.Nieve)); }
        }
        // cuanto tapa una nube (0 a 1) un punto de la pantalla: por la elipse de cada una y su alfa (pura; la prueba la recorre)
        internal static float Cobertura(float x, float y)
        {
            float cov = 0f;
            for (int k = 0; k < Max; k++)
            {
                float a = Al[k]; if (a <= 0.01f) continue;
                float dx = (x - Cx[k]) / (Rx[k] * 1.05f), dy = (y - Cy[k]) / (Ry[k] * 2.2f);
                float d2 = dx * dx + dy * dy;
                if (d2 >= 1f) continue;
                float c = (1f - d2) * Math.Min(1f, a / 0.6f);
                if (c > cov) cov = c;
            }
            return 1f - 0.85f * Math.Min(1f, cov * 1.4f);
        }

        // CL1: EL CIELO MAS GRIS (el tinte del degradado, g121/g122): el color mezclado hacia su gris y mas oscuro; nada de noche cerrada (la de XP)
        internal sealed class Tinte : RocolaOsCieloTinte
        {
            internal Tinte() { Quien = "CL1 el cielo gris"; Orden = RocolaOsCieloOrden.Nubes; }
            private static int cuantoAntes = -1;
            internal static float Fuerza
            {
                get
                {
                    RocolaOsCieloFoto f = RocolaOsCielo.Foto;
                    if (f == null || !f.DN6 || !f.DN1 || !f.ClimaVa || !f.ClimaSabido) return 0f;
                    float noche = RocolaOsCielo.NocheCuadro; if (noche >= 1f) return 0f;
                    return RocolaOsCieloClimaVista.Gris * (1f - noche);
                }
            }
            internal override bool Activo { get { return Fuerza > 0.02f; } }
            internal override void Tine(float yFrac, float yPx, float horizontePx, ref float r, ref float g, ref float b)
            {
                float k = Fuerza; if (!(k > 0f)) return;
                // abajo del horizonte (el suelo) no; la franja de la raya, a medias
                float s = yPx >= horizontePx ? 0f : (yPx > horizontePx * 0.96f ? 0.5f : 1f);
                if (s <= 0f) return;
                float l = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                float t = Math.Min(0.75f, k * 0.8f) * s, osc = 1f - 0.2f * k * s;
                r = (r + (l * 0.96f - r) * t) * osc; g = (g + (l * 1.0f - g) * t) * osc; b = (b + (l * 1.06f - b) * t) * osc;
            }
            // (para que la imagen intermedia del cielo se vuelva a pintar cuando cambia: en pasos de 1/32)
            internal static void SubeVersion()
            {
                int q = (int)(Fuerza * 32f);
                if (q != cuantoAntes) { cuantoAntes = q; RocolaOsCielo.TintesVersion++; }
            }
        }
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra()
        {
            RocolaOsCieloNubes n = new RocolaOsCieloNubes();
            RocolaOsCielo.PonPintor(RocolaOsCieloCapa.TrasNieblas, n);
            RocolaOsCielo.PonPintor(RocolaOsCieloCapa.ConLaLuna, n);
            RocolaOsCielo.PonEstrella(new Estrellas());
            RocolaOsCielo.PonTinte(new Tinte());
        }
    }
}
