// EL CIELO VIVO DE LEGACY (12 de octubre de 2026). Siete adornos para el fondo de siempre, elegidos por el usuario:
// «De sonido y fondo hazlo todo». Solo con tarjeta grafica; el dibujo por CPU esta congelado y no cambia.
//
//  1) LA LUNA CON SU FASE REAL, pequena, arriba a la derecha: la fase sale de la fecha (fase media, +-1 dia), con
//     mares, el borde algo mas oscuro, la parte apagada apenas visible (luz cenicienta) y un halo segun lo iluminada
//     que este. Tapa las estrellas que tiene detras. Cerca de la luna nueva no se ve, como en el cielo.
//  2) LLUVIAS DE ESTRELLAS EN SUS FECHAS: Cuadrantidas, Liridas, Eta Acuaridas, Perseidas, Orionidas, Leonidas y
//     Geminidas. Esos dias parte de las fugaces nace a media altura y se aleja de un mismo punto (el radiante), con la
//     cola apuntandole; cerca del maximo son mas y salen algo mas seguido. Las dos ranuras de siempre: no hay mas.
//  3) LA FUGAZ DE FESTEJO: al meter una moneda o poner una cancion cruza una fugaz dorada (una cada 2,5 s como mucho).
//  4) EL COMETA: se retiro el 13-10-2026 a peticion del usuario (foto: «Quita esa estrella fugaz»). Su clave, cometa=,
//     se ignora al leer una configuracion vieja.
//  5) MAS PROFUNDIDAD AL PASAR DE PAGINA: las nebulosas se corren un 30 % de lo que se corren las estrellas grandes y
//     la via lactea un 14 %, y vuelven despacio a su sitio. Va con "Pasar de pagina deslizando" (Ctrl+F3 -> Pantalla).
//  6) EL CIELO SEGUN LA HORA: de madrugada el cielo de abajo se oscurece un poco y al anochecer (hacia las 19:30) se
//     calienta abajo; tambien algo al amanecer. El cielo alto no cambia. Se lee el reloj cada 20 s.
//  7) EL REFLEJO DE LA AURORA: bajo la raya del horizonte, el brillo de los picos se refleja muy tenue y se apaga
//     antes de llegar al texto de la franja.
//
// Cada uno lleva su interruptor en Ctrl+F3 -> Fondo -> Cielo vivo, encendidos de fabrica ("Fondo como de fabrica"
// los enciende). Lo que dibujan reutiliza los caminos de siempre (el cuadro con textura, el halo, la estela aditiva y
// la malla aditiva): sin pasadas nuevas de pantalla completa, sin memoria por cuadro y sin hornear al dibujar.
// Las cuentas son funciones puras (pruebas/cielovivo.cs las comprueba sin tarjeta grafica).
using System;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: la luna del cielo vivo se hornea con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+

namespace Rocola
{
    public partial class Form1
    {
        // Los interruptores (Ctrl+F3 -> Fondo -> Cielo vivo). Existen en los dos ejecutables para que la configuracion
        // se conserve igual; solo la tarjeta grafica los usa.
        private bool lunaDelCielo = true, lluviasEstrellas = true, fugazFestejo = true;
        private bool profundidadPagina = true, cieloPorHora = true, reflejoAurora = true;

        // La fugaz de festejo: al meter una moneda o poner una cancion (lo llaman AddCoin y AddItem).
        private void FestejaEnElCielo()
        {
#if ROCOLA_GPU
            try { if (gpuEnabled && gpuPanel != null && fugazFestejo) gpuPanel.Festeja(); } catch { }
#endif
        }

#if ROCOLA_GPU
        // ============================================================ LAS CUENTAS (puras)

        // ---------------- 1) LA LUNA
        internal const double MesSinodico = 29.530588853;
        private static readonly DateTime LunaNuevaRef = new DateTime(2000, 1, 6, 18, 14, 0, DateTimeKind.Utc);
        // 0 = luna nueva, 0,25 = cuarto creciente, 0,5 = llena, 0,75 = cuarto menguante.
        internal static double FaseLunar(DateTime utc)
        {
            double dias = (utc - LunaNuevaRef).TotalDays / MesSinodico;
            double f = dias - Math.Floor(dias);
            return (f < 0.0 || f >= 1.0 || double.IsNaN(f)) ? 0.0 : f;
        }
        internal static double LunaIluminada(double fase) { return 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * fase)); }
        internal const double LunaMinima = 0.04;   // por debajo de esto (cerca de la nueva) no se dibuja

        // Donde va y de que tamano (centro y radio en pixeles): arriba a la derecha, en el cielo.
        internal static void SitioLuna(int w, int h, out float cx, out float cy, out float r)
        {
            r = Math.Max(12f, h * 0.042f);
            cx = w * 0.83f; cy = Math.Max(r + 4f, h * 0.22f);
        }

        // Los mares, de lejos: manchas oscuras suaves donde estan los de verdad (vista desde el hemisferio norte).
        // Grandes y suaves, unidas entre si como las de verdad (Lluvias, Serenidad, Tranquilidad, Fecundidad, Nectar,
        // Oceano de las Tormentas, Nubes y Crisis): de lejos se leen como las sombras de la luna, no como puntos.
        private static readonly float[] MarX = { -0.28f, 0.05f, 0.24f, 0.52f, 0.42f, -0.55f, -0.18f, 0.66f };
        private static readonly float[] MarY = { -0.30f, -0.28f, 0.02f, 0.22f, 0.36f, 0.05f, 0.40f, -0.20f };
        private static readonly float[] MarR = { 0.42f, 0.30f, 0.34f, 0.24f, 0.20f, 0.44f, 0.26f, 0.15f };
        private static readonly float[] MarK = { 0.16f, 0.14f, 0.15f, 0.12f, 0.10f, 0.13f, 0.10f, 0.14f };

        // La luna horneada (lado x lado, ARGB). Lo iluminado en blanco calido; lo apagado en un azul gris tenue (la luz
        // cenicienta) y casi opaco, para que tape las estrellas de detras. Borde suavizado de un pixel.
        internal static SKBitmap HorneaLuna(int lado, double fase)   // EN LINUX: un SKBitmap; EnsureLunaCielo lo pasa por el puente a Upload (D3D9Star.cs, aun sin traducir)
        {
            if (lado < 8) lado = 8;
            SKBitmap b = Mapas.Nuevo(lado, lado);   // EN LINUX: new Bitmap(lado, lado, Format32bppArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
            // EN LINUX: LockBits(WriteOnly, Format32bppArgb): los puntos sin premultiplicar se juntan en 'px' y van al mapa al final
            // EN LINUX: (sin try: no queda nada bloqueado que soltar si algo falla)
            {
                int[] px = new int[lado * lado];
                double ang = 2.0 * Math.PI * fase, sx = Math.Sin(ang), sz = -Math.Cos(ang);   // de donde le da el sol
                double R = lado * 0.5 - 1.0, c = lado * 0.5;
                for (int y = 0; y < lado; y++)
                    for (int x = 0; x < lado; x++)
                    {
                        double nx = (x + 0.5 - c) / R, ny = (y + 0.5 - c) / R, d2 = nx * nx + ny * ny;
                        double dist = Math.Sqrt(d2);
                        double borde = (1.0 - dist) * R + 0.5;   // un pixel de borde suave
                        if (borde <= 0.0) { px[y * lado + x] = 0; continue; }
                        if (borde > 1.0) borde = 1.0;
                        double nz = d2 < 1.0 ? Math.Sqrt(1.0 - d2) : 0.0;
                        double albedo = 0.86;
                        for (int m = 0; m < MarX.Length; m++)
                        {
                            double ex = (nx - MarX[m]) / MarR[m], ey = (ny - MarY[m]) / MarR[m];
                            albedo -= MarK[m] * Math.Exp(-(ex * ex + ey * ey) * 1.6);
                        }
                        uint hsh = (uint)(x * 73856093) ^ (uint)(y * 19349663);   // grano fijo, sin azar
                        hsh ^= hsh >> 13; hsh *= 0x5bd1e995u; hsh ^= hsh >> 15;
                        albedo += ((hsh & 255) / 255.0 - 0.5) * 0.05;
                        if (albedo < 0.35) albedo = 0.35;
                        double limbo = 0.72 + 0.28 * nz;                  // el borde, algo mas oscuro
                        double luz = nx * sx + nz * sz;                   // > 0: le da el sol
                        double k = (luz + 0.03) / 0.10; if (k < 0) k = 0; else if (k > 1) k = 1;
                        k = k * k * (3 - 2 * k);
                        double v = albedo * limbo;
                        // De la luz cenicienta al blanco calido de la parte iluminada. La cenicienta es un AZUL GRIS APAGADO
                        // apenas mas claro que el cielo de la noche: la parte sin sol se adivina (y tapa las estrellas de
                        // detras), pero no se lee como una bola oscura.
                        double r = (34 + (240 - 34) * k) * v, g = (44 + (234 - 44) * k) * v, bl = (70 + (222 - 70) * k) * v;
                        double a = borde * (0.93 + 0.07 * k);   // casi opaca tambien sin sol: tapa las estrellas de detras
                        int A = Clamp255(a * 255.0), Rr = Clamp255(r), G = Clamp255(g), B = Clamp255(bl);
                        px[y * lado + x] = (A << 24) | (Rr << 16) | (G << 8) | B;
                    }
                Pixeles.EscribeSinPremultiplicar(b, px, lado);   // EN LINUX: Marshal.Copy y UnlockBits: 'px', premultiplicado, al mapa (Nativo/Pixeles.cs)
            }
            // EN LINUX: (el finally con UnlockBits va en la linea de arriba)
            return b;
        }
        private static int Clamp255(double v) { return v <= 0 ? 0 : v >= 255 ? 255 : (int)(v + 0.5); }

        // ---------------- 2) LAS LLUVIAS DE ESTRELLAS
        // Maximo (mes, dia), dias de actividad antes y despues, ancho de la subida y de la bajada (dias), fuerza y radiante
        // (por encima del borde de arriba: asi todas bajan, como las fugaces de siempre).
        private struct Lluvia { public string Nombre; public int Mes, Dia, Antes, Despues; public double AnchoAntes, AnchoDespues, Fuerza; public float Rx, Ry; }
        private static readonly Lluvia[] Lluvias = {
            new Lluvia { Nombre = "Cuadrantidas",  Mes = 1,  Dia = 3,  Antes = 6,  Despues = 9,  AnchoAntes = 1.0, AnchoDespues = 1.0, Fuerza = 1.0, Rx = 0.85f, Ry = -0.15f },
            new Lluvia { Nombre = "Liridas",       Mes = 4,  Dia = 22, Antes = 8,  Despues = 8,  AnchoAntes = 2.0, AnchoDespues = 1.5, Fuerza = 0.6, Rx = 0.50f, Ry = -0.20f },
            new Lluvia { Nombre = "Eta Acuaridas", Mes = 5,  Dia = 6,  Antes = 17, Despues = 22, AnchoAntes = 6.0, AnchoDespues = 5.0, Fuerza = 0.7, Rx = 0.12f, Ry = -0.06f },
            new Lluvia { Nombre = "Perseidas",     Mes = 8,  Dia = 12, Antes = 26, Despues = 12, AnchoAntes = 5.5, AnchoDespues = 2.5, Fuerza = 1.0, Rx = 0.25f, Ry = -0.12f },
            new Lluvia { Nombre = "Orionidas",     Mes = 10, Dia = 21, Antes = 19, Despues = 17, AnchoAntes = 6.0, AnchoDespues = 5.0, Fuerza = 0.6, Rx = 0.70f, Ry = -0.10f },
            new Lluvia { Nombre = "Leonidas",      Mes = 11, Dia = 17, Antes = 11, Despues = 13, AnchoAntes = 3.0, AnchoDespues = 2.0, Fuerza = 0.6, Rx = 0.60f, Ry = -0.18f },
            new Lluvia { Nombre = "Geminidas",     Mes = 12, Dia = 14, Antes = 10, Despues = 6,  AnchoAntes = 3.0, AnchoDespues = 1.2, Fuerza = 1.0, Rx = 0.40f, Ry = -0.10f } };

        // La lluvia de esa fecha (hora local): su nombre ("" si ninguna), su fuerza 0..1 y su radiante (fraccion de
        // pantalla). Si caen dos a la vez, manda la mas fuerte.
        internal static string LluviaDe(DateTime local, out double fuerza, out float rx, out float ry)
        {
            fuerza = 0; rx = 0.5f; ry = -0.1f; string nombre = "";
            for (int i = 0; i < Lluvias.Length; i++)
            {
                Lluvia l = Lluvias[i];
                double mejor = double.MaxValue;
                for (int dy = -1; dy <= 1; dy++)   // el maximo de este ano, del anterior y del siguiente (enero/diciembre)
                {
                    DateTime pico;
                    try { pico = new DateTime(local.Year + dy, l.Mes, l.Dia, 12, 0, 0); } catch { continue; }
                    double dd = (local - pico).TotalDays;
                    if (Math.Abs(dd) < Math.Abs(mejor)) mejor = dd;
                }
                if (mejor < -l.Antes || mejor > l.Despues) continue;
                double an = mejor < 0 ? l.AnchoAntes : l.AnchoDespues;   // suben despacio y bajan deprisa (Perseidas: semanas antes)
                double f = l.Fuerza * Math.Exp(-0.5 * (mejor / an) * (mejor / an));
                if (f > fuerza) { fuerza = f; rx = l.Rx; ry = l.Ry; nombre = l.Nombre; }
            }
            return nombre;
        }
        internal static int CuantasLluvias { get { return Lluvias.Length; } }

        // Una fugaz de la lluvia: nace a media altura y se aleja del radiante, con la cola apuntandole.
        internal static void TiraFugazRadiante(Random r, float w, float h, float rx, float ry,
                                               out float x, out float y, out float vx, out float vy,
                                               out float vida, out float grosor, out float cola, out bool gorda)
        {
            gorda = r.Next(10) == 0;
            x = (float)(w * (0.08 + 0.84 * r.NextDouble()));
            y = (float)(h * (0.06 + 0.56 * r.NextDouble()));
            float dx = x - rx, dy = y - ry, len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1f) { dx = 0.3f; dy = 1f; len = (float)Math.Sqrt(1.09); }
            dx /= len; dy /= len;
            float vel = gorda ? (w + h) * (0.14f + (float)r.NextDouble() * 0.08f) : (w + h) * (0.30f + (float)r.NextDouble() * 0.35f);
            vx = dx * vel; vy = dy * vel;
            vida = gorda ? 1.4f + (float)r.NextDouble() * 0.6f : 0.5f + (float)r.NextDouble() * 0.6f;
            grosor = gorda ? 2.4f + (float)r.NextDouble() * 1.0f : 0.9f + (float)r.NextDouble() * 1.1f;
            cola = gorda ? 0.14f + (float)r.NextDouble() * 0.06f : 0.06f + (float)r.NextDouble() * 0.06f;
        }

        // ---------------- 3) LA FUGAZ DE FESTEJO: dorada, gorda, cruzando la mitad de arriba en diagonal.
        internal static void TiraFugazFestejo(Random r, float w, float h, out float x, out float y, out float vx, out float vy,
                                              out float vida, out float grosor, out float cola)
        {
            bool aDerecha = r.Next(2) == 0;
            double a = (0.13 + r.NextDouble() * 0.10) * Math.PI;   // 23 a 41 grados de bajada
            float dx = (float)Math.Cos(a) * (aDerecha ? 1f : -1f), dy = (float)Math.Sin(a);
            x = aDerecha ? -FugazMargen : w + FugazMargen;
            y = (float)(h * (0.04 + r.NextDouble() * 0.20));
            float vel = (w + h) * (0.30f + (float)r.NextDouble() * 0.08f);
            vx = dx * vel; vy = dy * vel;
            vida = 2.2f + (float)r.NextDouble() * 0.5f;
            grosor = 3.0f + (float)r.NextDouble() * 0.6f;
            cola = 0.20f + (float)r.NextDouble() * 0.05f;
        }
        internal const uint FestejoColor = 0x00FFE08Au;   // dorado
        internal const int FestejoCadaMs = 2500;           // como mucho una cada 2,5 s

        private static void V3(D3D9.Vtx[] v, int i, float x, float y, uint c) { v[i].X = x; v[i].Y = y; v[i].Z = 0f; v[i].Rhw = 1f; v[i].Color = c; v[i].U = 0f; v[i].V = 0f; }

        // ---------------- 6) EL CIELO SEGUN LA HORA
        // hondo: 1 de 0:30 a 4:30 (y en rampa desde las 22:30 y hasta las 6:00). calor: el anochecer (19:30) y algo el
        // amanecer (6:30).
        internal static void CieloHora(DateTime local, out float hondo, out float calor)
        {
            double hh = local.Hour + local.Minute / 60.0 + local.Second / 3600.0;
            double ho = 0;
            if (hh >= 22.5) ho = (hh - 22.5) / 2.0;
            else if (hh < 0.5) ho = (hh + 1.5) / 2.0;
            else if (hh <= 4.5) ho = 1.0;
            else if (hh < 6.0) ho = (6.0 - hh) / 1.5;
            if (ho > 1) ho = 1; else if (ho < 0) ho = 0;
            double anoche = 1.0 - Math.Abs(hh - 19.5) / 1.25, amanece = 0.6 * (1.0 - Math.Abs(hh - 6.5) / 0.8);
            double ca = Math.Max(0.0, Math.Max(anoche, amanece));
            hondo = (float)(ho * ho * (3 - 2 * ho));
            calor = (float)(ca * ca * (3 - 2 * ca));
        }
        internal const float HondoMedio = 0.14f, HondoAbajo = 0.18f, CalorMedio = 0.04f, CalorAbajo = 0.12f;
        // Oscurece un color del cielo y lo calienta hacia un naranja de atardecer. Con los dos en 0 sale igual.
        internal static uint TineCielo(uint argb, float oscurece, float calienta)
        {
            if (oscurece <= 0f && calienta <= 0f) return argb;
            float r = (argb >> 16) & 0xFF, g = (argb >> 8) & 0xFF, b = argb & 0xFF;
            float k = 1f - Math.Max(0f, Math.Min(1f, oscurece));
            r *= k; g *= k; b *= k;
            float c = Math.Max(0f, Math.Min(1f, calienta));
            r += (255f - r) * c; g += (150f - g) * c; b += (90f - b) * c;
            return (argb & 0xFF000000u) | ((uint)Clamp255(r) << 16) | ((uint)Clamp255(g) << 8) | (uint)Clamp255(b);
        }

        // ---------------- 7) EL REFLEJO DE LA AURORA
        internal const float ReflejoEscala = 0.18f, ReflejoAlfa = 0.30f;
        internal static float ReflejoHueco(float altoPanel) { return Math.Max(6f, Math.Min(16f, altoPanel * 0.021f)); }
        // Bajo el horizonte, cada columna refleja su pico (aplastado y sin pasar del hueco) con el color de su filo, y se
        // apaga hacia abajo. Triangulos aditivos. Devuelve cuantos vertices escribio.
        internal static int AurReflejo(D3D9.Vtx[] v, float[] hCol, uint[] cFilo, float[] iFilo, int columnas, float ancho, float baseY, float hueco)
        {
            if (v == null || hCol == null || cFilo == null || iFilo == null || columnas < 1) return 0;
            if (hCol.Length < columnas + 1 || cFilo.Length < columnas + 1 || iFilo.Length < columnas + 1 || v.Length < columnas * 6) return 0;
            float paso = ancho / columnas, y0 = baseY + 1f;
            int n = 0;
            for (int i = 0; i < columnas; i++)
            {
                float x0 = i * paso, x1 = x0 + paso;
                float d0 = Math.Min(hueco, hCol[i] * ReflejoEscala), d1 = Math.Min(hueco, hCol[i + 1] * ReflejoEscala);
                uint a0 = (uint)Math.Max(0f, Math.Min(255f, 255f * ReflejoAlfa * iFilo[i]));
                uint a1 = (uint)Math.Max(0f, Math.Min(255f, 255f * ReflejoAlfa * iFilo[i + 1]));
                uint c0 = (a0 << 24) | (cFilo[i] & 0x00FFFFFFu), c1 = (a1 << 24) | (cFilo[i + 1] & 0x00FFFFFFu);
                uint f0 = cFilo[i] & 0x00FFFFFFu, f1 = cFilo[i + 1] & 0x00FFFFFFu;
                V3(v, n + 0, x0, y0, c0); V3(v, n + 1, x1, y0, c1); V3(v, n + 2, x0, y0 + d0, f0);
                V3(v, n + 3, x1, y0, c1); V3(v, n + 4, x1, y0 + d1, f1); V3(v, n + 5, x0, y0 + d0, f0);
                n += 6;
            }
            return n;
        }
#endif
    }

#if ROCOLA_GPU
    internal sealed partial class GpuPanel
    {
        // Los interruptores (los pone AplicaEfectosGpu).
        public bool LunaDelCielo = true, LluviasEstrellas = true, FugazFestejo = true;
        public bool ProfundidadPagina = true, CieloPorHora = true, ReflejoAurora = true;

        // ---------------- lo que se lee del reloj cada 20 s (la hora, la fase, la lluvia)
        private int cieloVivoTick; private bool cieloVivoOk;
        private float horaHondo, horaCalor;
        private double faseLuna;
        private void ActualizaCieloVivo()
        {
            int t = Environment.TickCount;
            if (cieloVivoOk && unchecked(t - cieloVivoTick) < 20000 && unchecked(t - cieloVivoTick) >= 0) { PasaCieloVivo(); return; }
            cieloVivoTick = t; cieloVivoOk = true;
            DateTime ahora = DateTime.Now;
            Form1.CieloHora(ahora, out horaHondo, out horaCalor);
            faseLuna = Form1.FaseLunar(ahora.ToUniversalTime());
            double f; float rx, ry;
            Form1.LluviaDe(ahora, out f, out rx, out ry);
            stars.Lluvia((float)f, rx, ry);
            PasaCieloVivo();
        }
        private void PasaCieloVivo()
        {
            stars.HoraHondo = CieloPorHora ? horaHondo : 0f;
            stars.HoraCalor = CieloPorHora ? horaCalor : 0f;
            stars.LluviasOn = LluviasEstrellas;
        }

        // ---------------- 3) la fugaz de festejo
        internal void Festeja() { if (stars != null && FugazFestejo) stars.PideFestejo(); }

        // ---------------- 1) la luna
        private IntPtr lunaCieloTex; private int lunaCieloBw, lunaCieloBh; private float lunaCieloU, lunaCieloV;
        private int lunaCieloPaso = -1, lunaCieloLado;
        private readonly D3D9.Vtx[] lunaCieloQuad = new D3D9.Vtx[6];
        // Antes de Begin (nada se hornea ni se sube dentro del cuadro): se rehace al cambiar de paso de fase (120 pasos
        // por mes, ~6 h) o de tamano.
        private void EnsureLunaCielo()
        {
            if (!LunaDelCielo || stars == null || !stars.Ok || Width < 64 || Height < 64) return;
            float cx, cy, r; Form1.SitioLuna(Width, Height, out cx, out cy, out r);
            int lado = r * 2f > 96f ? 192 : 128;
            int paso = (int)(faseLuna * 120.0) % 120;
            if (lunaCieloTex != IntPtr.Zero && paso == lunaCieloPaso && lado == lunaCieloLado) return;
            if (Form1.LunaIluminada(faseLuna) < Form1.LunaMinima) return;   // cerca de la nueva no se ve: nada que hornear
            using (Rocola.Dibujo2D.Bitmap b = PuenteDibujo.Entrega(Form1.HorneaLuna(lado, (paso + 0.5) / 120.0)))   // EN LINUX: Upload (D3D9Star.cs, aun sin traducir) pide un Bitmap: el mapa le llega por el puente (Plataforma/Puente.cs)
                lunaCieloTex = Upload(lunaCieloTex, ref lunaCieloBw, ref lunaCieloBh, b, out lunaCieloU, out lunaCieloV);
            lunaCieloPaso = paso; lunaCieloLado = lado;
        }
        // Al reiniciar o soltar la tarjeta (como las demas texturas): se vuelve a hornear en el siguiente cuadro.
        private void SueltaCieloVivo()
        {
            if (lunaCieloTex != IntPtr.Zero) { D3D9.Release(lunaCieloTex); lunaCieloTex = IntPtr.Zero; }
            lunaCieloBw = lunaCieloBh = 0; lunaCieloPaso = -1; lunaCieloLado = 0;
            aurDibujada = false; nebEmpuje = hazEmpuje = nebCrudo = hazCrudo = 0f;
        }
        private void DibujaLunaCielo()
        {
            if (!LunaDelCielo || lunaCieloTex == IntPtr.Zero || Width < 64 || Height < 64) return;
            double ilum = Form1.LunaIluminada(faseLuna);
            if (ilum < Form1.LunaMinima) return;
            float cx, cy, r; Form1.SitioLuna(Width, Height, out cx, out cy, out r);
            float atenua = 0.6f + 0.4f * Math.Max(0f, Math.Min(1f, Dim));   // como las estrellas al navegar, pero menos
            uint ha = (uint)Math.Max(0, Math.Min(255, (int)(70.0 * ilum * atenua)));
            stars.DrawGlow(cx, cy, r * 2.6f, (ha << 24) | 0x00C8D2EBu);
            uint a = (uint)Math.Max(0, Math.Min(255, (int)(255f * atenua)));
            SetTexQuadA(lunaCieloQuad, cx - r, cy - r, cx + r, cy + r, lunaCieloU, lunaCieloV, (a << 24) | 0x00FFFFFFu);
            stars.DrawQuad(lunaCieloTex, lunaCieloQuad);
        }

        // ---------------- 7) el reflejo de la aurora (lo que dejo DrawAurora en este cuadro)
        private D3D9.Vtx[] reflejoV; private bool aurDibujada; private float aurDibujadaBase;
        private bool reflejoListo;
        private int reflejoN, reflejoAncho;
        private float reflejoBase, reflejoHueco;
        internal int ReflejosCalculados, ReflejosReusados;
        private void DibujaReflejoAurora()
        {
            if ((ReposoSinReflejo && AhorroReposo) || !ReflejoAurora || !aurDibujada || aurHs == null || aurNc == null || aurNi == null) return;
            if (EstadoOcultaDesde(aurDibujadaBase - AurLineaAlza + 1f)) return; // completamente tapado: no cambia ningun pixel visible
            if (reflejoV == null) reflejoV = new D3D9.Vtx[AurCols * 6];
            // PEGADO A LA RAYA (foto del 13-10-2026: «la barra de estado queda un poco despegada del espectro»). Nacia en el pie
            // del relleno, 15 px por debajo de la raya del horizonte (AurLineaAlza): entre el brillo de la raya y el reflejo
            // quedaba una franja oscura, y la raya se veia suelta encima de la barra. Ahora nace justo bajo el nucleo de la raya.
            float baseY = aurDibujadaBase - AurLineaAlza, hueco = Form1.ReflejoHueco(Height);
            if (!reflejoListo || reflejoAncho != Width || reflejoBase != baseY || reflejoHueco != hueco)
            {
                reflejoListo = false;
                reflejoN = Form1.AurReflejo(reflejoV, aurHs, aurNc, aurNi, AurCols, Width, baseY, hueco);
                reflejoAncho = Width; reflejoBase = baseY; reflejoHueco = hueco; reflejoListo = true;
                ReflejosCalculados++;
            }
            else ReflejosReusados++;
            if (reflejoN >= 3) stars.DrawMeshAdd(reflejoV, reflejoN / 3);
        }

        // ---------------- 5) la profundidad al pasar de pagina: lo que se corren nebulosas y via lactea, y su vuelta
        internal const float NebEmpujeFr = 0.30f, HazEmpujeFr = 0.14f, EmpujeMargen = 0.02f, EmpujeVueltaSeg = 1.4f;
        // EL TOPE SIN CHOQUE (13-10-2026). Lo corrido se acumulaba y se cortaba en seco en el tope: hojeando rapido, las
        // nebulosas iban, chocaban contra el tope y se quedaban clavadas mientras las portadas seguian, un tiron. Ahora se
        // acumula aparte (nebCrudo/hazCrudo) y lo que se ve pasa por una rodilla: igual que antes hasta el 75 % del tope
        // (un empujon solo, de una pagina, sale identico) y desde ahi se acerca al tope sin llegar a chocar.
        private float nebEmpuje, hazEmpuje, nebCrudo, hazCrudo; private double empujeUltMs;
        internal static float Rodilla(float x, float tope)
        {
            if (!(tope > 0f)) return 0f;
            float k = 0.75f * tope, ax = x < 0f ? -x : x;
            if (ax <= k) return x;
            float y = k + (tope - k) * (float)Math.Tanh((ax - k) / (tope - k));
            return x < 0f ? -y : y;
        }
        private void EmpujaNieblas(float dx)
        {
            if (!ProfundidadPagina || Width <= 0) { nebEmpuje = hazEmpuje = nebCrudo = hazCrudo = 0f; return; }
            float tope = Width * EmpujeMargen, techo = 4f * tope;
            nebCrudo = Math.Max(-techo, Math.Min(techo, nebCrudo + dx * NebEmpujeFr));
            hazCrudo = Math.Max(-techo, Math.Min(techo, hazCrudo + dx * HazEmpujeFr));
            nebEmpuje = Rodilla(nebCrudo, tope); hazEmpuje = Rodilla(hazCrudo, tope);
        }
        private void VuelvenNieblas()
        {
            double ahora = AhoraMs();
            float dt = empujeUltMs > 0 ? (float)Math.Max(0, Math.Min(.1, (ahora - empujeUltMs) / 1000.0)) : 0f;
            empujeUltMs = ahora;
            if (!ProfundidadPagina) { nebEmpuje = hazEmpuje = nebCrudo = hazCrudo = 0f; return; }
            float k = (float)Math.Exp(-dt / EmpujeVueltaSeg * 3.0);
            nebCrudo *= k; hazCrudo *= k;
            if (Math.Abs(nebCrudo) < 0.05f) nebCrudo = 0f;
            if (Math.Abs(hazCrudo) < 0.05f) hazCrudo = 0f;
            float tope = Width * EmpujeMargen;
            nebEmpuje = Rodilla(nebCrudo, tope); hazEmpuje = Rodilla(hazCrudo, tope);
        }
    }

    internal sealed partial class GpuStarfield
    {
        // ---------------- 6) el cielo segun la hora (lo pone el panel cada 20 s)
        public float HoraHondo, HoraCalor;
        // ---------------- 2) la lluvia del dia y 3) el festejo
        public bool LluviasOn = true;
        private float lluviaFuerza, lluviaRx = 0.5f, lluviaRy = -0.1f;
        internal void Lluvia(float fuerza, float rx, float ry) { lluviaFuerza = fuerza; lluviaRx = rx; lluviaRy = ry; }
        private bool festejoPedido; private int festejoTick; private bool festejoHubo;
        internal void PideFestejo()
        {
            int t = Environment.TickCount;
            if (festejoHubo && unchecked(t - festejoTick) < Form1.FestejoCadaMs && unchecked(t - festejoTick) >= 0) return;
            festejoHubo = true; festejoTick = t; festejoPedido = true;
        }
        private float LluviaAhora { get { return LluviasOn ? lluviaFuerza : 0f; } }

    }
#endif
}
