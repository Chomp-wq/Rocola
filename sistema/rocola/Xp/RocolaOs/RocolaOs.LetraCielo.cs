// LA LETRA EN EL CIELO DEL ESTADO EXTENDIDO (K6; FASE11_letras.md, §7). 27-11-2026: el dueno pidio que se vea COMO LA LETRA DE APPLE MUSIC («Te dije que la
// letra de las canciones debia verse como las de Apple Music»); sustituye la maqueta 3 (dos renglones centrados que se llenan de ambar, el siguiente en
// lavanda). Solo Rocola OS, solo la tarjeta (el dibujo por procesador sigue congelado). Interruptor: letra_cielo= (la funcion de RocolaOs.LetraSuena.cs,
// que ya pide la letra de lo que suena a LRCLIB o al MP3; encendida de fabrica). La FUENTE no se toca: esto lee RocolaOsLetraSuena.Actual.
//
// LO QUE SE VE (en el reposo con musica, en el cielo libre sobre la raya del horizonte):
//   - una COLUMNA de versos alineados a la izquierda, en letra grande y negrita (la de los titulos de XP, 0,052 del alto) con aire entre versos; un verso
//     largo se PARTE en renglones (hasta 4), no se achica. Su borde izquierdo queda a la derecha de donde puede estar la estrella en su extremo izquierdo
//     (se calcula con lo mismo que la dibuja); donde la estrella pase por encima, la letra se atenua cerca de ella; arriba y abajo se desvanece;
//   - el verso que se canta, BLANCO opaco con sombra suave (regla 3) hacia el tercio de arriba; los demas, blanco translucido y un poco desenfocado, mas
//     cuanto mas lejos (se hornean dos mapas por verso, nitido y desenfocado, y se mezclan con el alfa); se ve el anterior arriba y varios de los que siguen;
//   - al cambiar de verso la columna entera sube con un resorte critico (sin rebote, ~0,55 s), con un retraso de ~35 ms por verso hacia abajo; el nuevo pasa de
//     translucido a opaco, de desenfocado a nitido y crece de 0,96 a 1 desde su borde izquierdo (todo sale de la posicion de cada verso: alfa, desenfoque y
//     tamano son funciones de su distancia al sitio del verso que se canta);
//   - un tramo sin voz de mas de 5 s (renglon vacio del LRC, o antes del primer verso): en el sitio del verso, TRES PUNTOS que respiran uno tras otro; se van
//     justo antes del siguiente verso. Si es corto, nada;
//   - la letra del MP3 (sin tiempos), como Apple con la no sincronizada: todos los versos igual de visibles (0,85, sin desenfoque ni verso resaltado) y la
//     columna se desliza despacio y continua, repartida por la duracion de la cancion;
//   - «Letra: LRCLIB» (o «Letra: el MP3»), chiquito, abajo a la derecha del cielo;
//   - no sale con el disco abierto, la pared, el buscador, la ayuda, Ctrl+F3/Ctrl+F4, la atraccion ni con un video: solo con el reposo abierto (idleBlend,
//     de 0,55 a 1); si se abre alguno con el reposo aun a la vista, se va en 0,15 s. No se atenua con la calma.
//
// COMO SE PINTA: cada verso se hornea UNA vez, en Prepara (fuera del cuadro; Direct3D no deja crear texturas con la escena abierta): el texto partido en
// renglones, blanco con su sombra negra (el color y el alfa los ponen los vertices) y, aparte, el mismo mapa desenfocado. Como mucho DOS horneados por cuadro.
// Nada de memoria nueva por cuadro: los vertices son arreglos fijos y las posiciones, resortes por verso en arreglos del tamano de la letra (una vez por letra).
//
// Ganchos: g380 (el dibujo, en RenderFrame, tras el buscador con portadas) y g381 (Prepara, tras el paso del reposo, antes de abrir el cuadro).
// Sin ROCOLA_OS o con letra_cielo=0, los dos salen sin tocar nada.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    // LAS COSAS DE LA COLUMNA de una letra: los versos con texto y, entre ellos, los tramos sin voz largos (tres puntos). Pura: la prueba la recorre.
    internal sealed class RocolaOsLetraItems
    {
        internal int N;
        internal double[] Ini, Fin;     // desde que tiempo y hasta cual (Fin: el comienzo del siguiente, o el final de la cancion)
        internal int[] Verso;           // el indice en Letra.Versos; -1: los tres puntos
        internal bool Sinc;
    }

    // LA CUENTA DE LO QUE SE VE EN CADA INSTANTE (pura, sin dibujar nada)
    internal static class RocolaOsLetraModelo
    {
        // lo que tarda el cambio de verso (0,55 s; ROCOLA_LETRA_CAMBIO_S, solo para las pruebas: con las capturas que paran el cuadro casi un segundo no se
        // puede ver un cambio de medio segundo, y uno de 3 s si)
        internal static readonly double CambioS = CambioDe(Environment.GetEnvironmentVariable("ROCOLA_LETRA_CAMBIO_S"));
        private static double CambioDe(string e) { double v; return double.TryParse(e, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 0.05 && v <= 10 ? v : 0.55; }
        internal const double SinVozS = 5;         // un tramo sin voz de mas de esto lleva los tres puntos
        internal const double PuntosEntraS = 0.5, PuntosSalenS = 0.45;
        internal const double RetrasoS = 0.035;    // el retraso de cada verso hacia abajo (con el cambio de 0,55 s; crece y baja con el)
        internal const float AlfaMp3 = 0.85f;      // la letra sin tiempos: todos igual de visibles

        internal static float Suave(double t) { if (t <= 0) return 0; if (t >= 1) return 1; return (float)(t * t * (3 - 2 * t)); }
        internal static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        // LOS VERSOS DE LA COLUMNA. total: la duracion de la cancion (0 si no se sabe)
        internal static RocolaOsLetraItems Arma(RocolaOsLetra l, double total)
        {
            RocolaOsLetraItems r = new RocolaOsLetraItems();
            bool sinc = l.Sincronizada && l.Tiempos != null && l.Tiempos.Length == l.Versos.Length;
            List<double> ini = new List<double>(); List<int> vs = new List<int>();
            int n = l.Versos.Length;
            if (!sinc)
            {
                for (int i = 0; i < n; i++) if (l.Versos[i].Length > 0) { ini.Add(0); vs.Add(i); }
            }
            else
            {
                if (n > 0 && l.Versos[0].Length > 0 && l.Tiempos[0] > SinVozS) { ini.Add(0); vs.Add(-1); }   // antes del primer verso (sin renglon vacio que lo diga)
                for (int i = 0; i < n; i++)
                {
                    if (l.Versos[i].Length > 0) { ini.Add(l.Tiempos[i]); vs.Add(i); continue; }
                    if (i > 0 && l.Versos[i - 1].Length == 0) continue;       // (un renglon vacio tras otro: cuenta el primero)
                    int sig = -1; for (int k = i + 1; k < n; k++) if (l.Versos[k].Length > 0) { sig = k; break; }
                    if (sig < 0) continue;                                    // (el vacio del final no es un tramo sin voz)
                    if (l.Tiempos[sig] - l.Tiempos[i] > SinVozS) { ini.Add(l.Tiempos[i]); vs.Add(-1); }
                }
            }
            r.N = ini.Count; r.Sinc = sinc; r.Ini = ini.ToArray(); r.Verso = vs.ToArray(); r.Fin = new double[r.N];
            for (int k = 0; k < r.N; k++) r.Fin[k] = k + 1 < r.N ? r.Ini[k + 1] : (total > r.Ini[k] + 1 ? total : r.Ini[k] + 6);
            return r;
        }
        // LA COSA DE AHORA: la ultima que ya empezo (-1 antes de la primera)
        internal static int Actual(RocolaOsLetraItems it, double pos)
        {
            int lo = 0, hi = it.N - 1, i = -1;
            while (lo <= hi) { int m = (lo + hi) >> 1; if (it.Ini[m] <= pos) { i = m; lo = m + 1; } else hi = m - 1; }
            return i;
        }
        // lo visibles que estan los tres puntos de la cosa k en pos (0 a 1): entran 0,5 s despues de empezar y se van 0,45 s antes del siguiente verso
        internal static float PuntosVis(RocolaOsLetraItems it, int k, double pos)
        {
            if (k < 0 || k >= it.N || it.Verso[k] >= 0) return 0f;
            return Suave((pos - it.Ini[k]) / PuntosEntraS) * Suave((it.Fin[k] - pos) / PuntosSalenS);
        }
        // el brillo (0 a 1) del punto i (0 a 2) al tiempo t: crecen e iluminan uno tras otro, cada 2,6 s
        internal static float PuntoBrillo(double t, int i)
        {
            double ph = t / 2.6 - 0.16 * i;
            return (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * ph));
        }

        // LO QUE SE VE DE UN VERSO SEGUN SU DISTANCIA AL SITIO DEL QUE SE CANTA (d en versos de un renglon; negativo: arriba)
        internal static float AlfaD(float d)
        {
            float a = Math.Abs(d);
            float s = Suave(Math.Min(1f, a));
            if (d < 0f) return Lerp(1f, 0.34f, s) * (1f - 0.55f * Suave((a - 1f) / 1.2f));   // (el de arriba, y los que ya pasaron, se apagan)
            float v = Lerp(1f, 0.38f, s);
            if (d > 1f) v *= 1f - 0.20f * Suave((d - 1f) / 3f);                               // (los de abajo, un poco menos cuanto mas lejos)
            return v;
        }
        internal static float BlurD(float d)
        {
            float a = Math.Abs(d);
            if (a <= 1f) return 0.45f * Suave(a);
            return 0.45f + 0.55f * Math.Min(1f, (a - 1f) / 1.6f);
        }
        internal static float EscalaD(float d) { return 1f - 0.04f * Suave(Math.Min(1f, Math.Abs(d))); }
        // el desvanecido de arriba y de abajo de la columna: 0 fuera, 1 en el cuerpo (suave)
        internal static float Mascara(float y, float top, float bot, float fade)
        { return Suave((y - top) / fade) * Suave((bot - y) / fade); }
        // el resorte critico: x hacia meta con la frecuencia w (1/s) y velocidad v, paso dt: sin rebote, cierra solo
        internal static void Resorte(ref float x, ref float v, float meta, float w, float dt)
        {
            float rel = x - meta, e = (float)Math.Exp(-w * dt), k = v + w * rel;
            x = meta + (rel + k * dt) * e; v = (v - w * k * dt) * e;
        }
        // la posicion (en versos) de la letra sin tiempos: se reparte por la duracion de la cancion (0 al empezar; el ultimo verso al acabar)
        internal static double Reparto(int renglones, double pos, double total)
        {
            if (renglones <= 0 || total <= 1) return 0;
            double p = pos / total * (renglones - 1);
            return p < 0 ? 0 : p > renglones - 1 ? renglones - 1 : p;
        }
    }

    // UN VERSO HORNEADO: el nitido (TexA) y el desenfocado (TexN)
    internal sealed class RocolaOsLetraLinea
    {
        internal IntPtr TexA, TexN; internal float UA, VA, UN, VN; internal int WA, HA, WN, HN;
        internal GpuStarfield De;
        internal SKBitmap Pend;     // el mapa nitido, guardado hasta que el siguiente cuadro hornee el desenfocado
        internal void Suelta()
        {
            if (Pend != null) { Pend.Dispose(); Pend = null; }
            if (De != null) { if (TexA != IntPtr.Zero) Tarjeta.Suelta(TexA); if (TexN != IntPtr.Zero) Tarjeta.Suelta(TexN); }
            TexA = TexN = IntPtr.Zero;
        }
    }

    internal static class RocolaOsLetraHornea
    {
        private static FormatoTexto Formato()
        {
            FormatoTexto sf = new FormatoTexto(); sf.Banderas = BanderasTexto.SinPartir; sf.Vertical = Alinea.Centro; sf.Horizontal = Alinea.Cerca; sf.Corte = CorteTexto.PuntosPorLetra;
            return sf;
        }
        internal static float AltoRenglon(float px) { return px * 1.2f; }
        internal static int Margen(float px) { return (int)Math.Ceiling(px * 0.42f) + 4; }

        // EL TEXTO PARTIDO EN RENGLONES (alineados a la izquierda, hasta 4; el ultimo con puntos si sobra): palabra a palabra con la medida de la letra de XP
        internal static string[] Parte(string texto, float px, int anchoMax)
        {
            AjusteLetra ajuste = AjusteLetra.SuaveAjustada; FormatoTexto sf = Formato();
            List<string> r = new List<string>();
            string[] pal = texto.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            using (Rocola.Nativo.Letra f = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), px, true))
            {
                string lin = "";
                for (int i = 0; i < pal.Length; i++)
                {
                    string cand = lin.Length == 0 ? pal[i] : lin + " " + pal[i];
                    if (lin.Length > 0 && TextoGdiPlus.Mide(cand, f, ajuste, SizeF.Empty, sf).Width > anchoMax)
                    {
                        if (r.Count == 3) { lin = lin + " " + string.Join(" ", pal, i, pal.Length - i); break; }   // el ultimo renglon se queda con lo que sobra (los puntos lo cortan)
                        r.Add(lin); lin = pal[i];
                    }
                    else lin = cand;
                }
                if (lin.Length > 0) r.Add(lin);
            }
            if (r.Count == 0) r.Add("");
            return r.ToArray();
        }

        // LO QUE MIDE UN TEXTO de una linea (el credito: su mapa no lleva mas ancho que el)
        internal static int AnchoDe(string texto, float px)
        {
            using (Rocola.Nativo.Letra f = Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), px, true))
                return (int)Math.Ceiling(TextoGdiPlus.Mide(texto, f, AjusteLetra.SuaveAjustada, SizeF.Empty, Formato()).Width) + 2;
        }
        // EL MAPA DE UN VERSO: los renglones en blanco con su sombra negra (una nube y un filo), la letra de los titulos de XP (Trebuchet MS en negrita)
        internal static SKBitmap Dibuja(string[] lineas, float px, int anchoMax)
        {
            AjusteLetra ajuste = AjusteLetra.SuaveAjustada; FormatoTexto sf = Formato();
            string fam = Letras.Resuelve("Trebuchet MS");
            float lh = AltoRenglon(px); int pad = Margen(px);
            int w = anchoMax + 2 * pad + 12, h = (int)Math.Ceiling(lh * lineas.Length) + 2 * pad;
            SKBitmap bmp = Mapas.Nuevo(w, h);
            using (Rocola.Nativo.Letra f = Rocola.Nativo.Letra.EnPixeles(fam, px, true))
            using (SKBitmap negro = Mapas.Nuevo(w, h))
            {
                using (SKCanvas c = new SKCanvas(negro))
                using (SKPaint pn = new SKPaint { Color = new SKColor(0, 0, 0, 255) })
                    for (int i = 0; i < lineas.Length; i++)
                        TextoGdiPlus.Escribe(c, lineas[i], f, ajuste, pn, new RectangleF(pad, pad + i * lh, anchoMax + 12, lh), sf);
                using (SKCanvas g = new SKCanvas(bmp))
                {
                    float dy = Math.Max(1.5f, px * 0.06f), sig = Math.Max(2.2f, px * 0.085f);
                    using (SKBitmap nubeB = Mapas.Nuevo(w, h))
                    {   // (la nube se desenfoca una vez y se pone dos veces: lo mismo que desenfocarla dos veces, con la mitad del trabajo)
                        using (SKCanvas cn = new SKCanvas(nubeB))
                        using (SKPaint nube = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(sig, sig) })
                            cn.DrawBitmap(negro, 0, 0, nube);
                        g.DrawBitmap(nubeB, 0, dy); g.DrawBitmap(nubeB, 0, dy);
                    }
                    using (SKPaint filo = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(1.0f, 1.0f) })
                        g.DrawBitmap(negro, 0, dy * 0.8f, filo);
                    using (SKPaint blanco = new SKPaint { Color = new SKColor(255, 255, 255, 255) })
                        for (int i = 0; i < lineas.Length; i++)
                            TextoGdiPlus.Escribe(g, lineas[i], f, ajuste, blanco, new RectangleF(pad, pad + i * lh, anchoMax + 12, lh), sf);
                }
            }
            return bmp;
        }
        // EL MISMO MAPA, DESENFOCADO
        internal static SKBitmap Desenfoca(SKBitmap nitido, float px)
        {
            SKBitmap r = Mapas.Nuevo(nitido.Width, nitido.Height);
            float s = Math.Max(2.2f, px * 0.075f);
            using (SKCanvas c = new SKCanvas(r))
            using (SKPaint p = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(s, s) })
                c.DrawBitmap(nitido, 0, 0, p);
            return r;
        }
        // UN PUNTO (de los tres): un disco blanco con borde suave y su sombra
        internal static SKBitmap Punto(int d)
        {
            int pad = Math.Max(4, d / 3), w = d + 2 * pad;
            SKBitmap bmp = Mapas.Nuevo(w, w);
            using (SKCanvas g = new SKCanvas(bmp))
            {
                float c = w / 2f;
                using (SKPaint sombra = new SKPaint { Color = new SKColor(0, 0, 0, 200), IsAntialias = true, ImageFilter = SKImageFilter.CreateBlur(d * 0.12f, d * 0.12f) })
                    g.DrawCircle(c, c + d * 0.07f, d / 2f, sombra);
                using (SKPaint blanco = new SKPaint { Color = new SKColor(255, 255, 255, 255), IsAntialias = true })
                    g.DrawCircle(c, c, d / 2f, blanco);
            }
            return bmp;
        }
    }

    // LA ROCOLA (Form1): lo de XP que la letra necesita saber (en la pantalla)
    public partial class Form1
    {
        // lo que suena es una cancion con la letra puesta: su ruta, o null
        internal string RocolaOsLetraRuta() { return nowPlaying != null && !nowPlaying.IsVideo && !videoOn ? rocolaOsLetraRuta : null; }
        // ¿se puede ver la letra? (nada de eso abierto: el reposo ya exige que nadie toque nada, pero se abre algo con el reposo aun a la vista)
        internal bool RocolaOsLetraPermite()
        {
            return !attractOn && !searchOpen && !configMode && !ajustesAbiertos && !AsistenteVisible && !(panelSongs != null && panelSongs.Visible);
        }
        // la posicion (la que se oye) y la duracion de lo que suena, en segundos
        internal double RocolaOsLetraPos() { return PosicionActual(); }
        internal double RocolaOsLetraTotal()
        {
            try { IAudioSource r = reader; return r != null ? r.TotalTime.TotalSeconds : 0; } catch { return 0; }
        }
        // g380 y g381 (la tarjeta): sin ROCOLA_OS o con la letra apagada, nada
        internal static bool RocolaOsLetraCielo { get { return RocolaOs.Activo && RocolaOs.Encendida("letra_cielo"); } }
    }

    internal sealed partial class GpuPanel
    {
        private const int LetraMaxPz = 16;
        private readonly Dictionary<int, RocolaOsLetraLinea> letraLineas = new Dictionary<int, RocolaOsLetraLinea>();
        private RocolaOsLetra letraDe;                 // la letra que tienen horneada las lineas
        private RocolaOsLetraItems letraIt;
        private RocolaOsLetraLinea letraPend;           // el verso al que le falta el mapa desenfocado (el cuadro siguiente se lo hornea)
        private GpuStarfield letraEstrellas;
        private int letraAncho, letraAlto;
        private IntPtr letraCreditoTex; private float letraCreditoU, letraCreditoV; private int letraCreditoW, letraCreditoH; private string letraCreditoTexto;
        private GpuStarfield letraCreditoDe;
        private IntPtr letraPuntoTex; private float letraPuntoU, letraPuntoV; private int letraPuntoW; private GpuStarfield letraPuntoDe;
        // lo de cada cosa de la columna (indice k + 1: el -1 es el sitio vacio de antes del primer verso)
        private string[][] letraRen; private float[] letraY, letraVel; private bool[] letraOk;
        private int letraCurNew = int.MinValue, letraCurOld; private double letraCambioMs = -1e9;
        private double letraPos, letraMedidoMs = -1e9; private bool letraTienePos;
        private float letraIdle, letraPermite;         // 0 a 1: lo abierto del reposo; y si nada de lo otro esta abierto
        private double letraClock = -1;
        private bool letraVa;                          // lo que decidio Prepara para este cuadro
        private float letraAlfa, letraMov;
        // la geometria de la columna (la pone Prepara en cada cuadro)
        private float gL, gR, gTop, gBot, gAnc, gPx, gLh, gGap, gFade, gNom, gSt; private int gPad, gAncho;
        // lo que se dibuja (Prepara lo deja listo para Pinta)
        private readonly int[] letraPzK = new int[LetraMaxPz]; private readonly float[] letraPzY = new float[LetraMaxPz], letraPzS = new float[LetraMaxPz], letraPzA = new float[LetraMaxPz], letraPzB = new float[LetraMaxPz], letraPzP = new float[LetraMaxPz];
        private int letraN;
        private readonly Tarjeta.Vtx[] letraBatch = new Tarjeta.Vtx[6 * 120];
        private int letraBatchN;
        private readonly float[] letraYs = new float[12], letraXs = new float[28];
        private StreamWriter letraLog; private bool letraLogProbado, letraGeoAnotada; private double letraLogMs = -1e9;
        private int letraHorneados;                    // (la prueba)
        private float letraMsHornea, letraMsBlur, letraMsSube;   // (la prueba) lo que tardo lo mas caro de cada paso desde la ultima linea del log
        private float letraCurY;                       // (la prueba) donde esta el verso que se canta

        // LA GEOMETRIA de la columna: todo en proporcion al alto y al ancho. El borde izquierdo queda a la derecha de la estrella en el extremo izquierdo de su
        // recorrido (su centro minimo mas su radio y medio): la cuenta es la de StarTravelCenter con el tamano que la dibuja DrawDetailedMoon en el reposo.
        private void LetraGeometria(bool sinc)
        {
            float H = Height, W = Width;
            float rStar = DetailedMoonDiameter(Height, 1f) * MoonSceneScale(1f, true) * .5f;
            float mx = Math.Min(W * .45f, Math.Max(12f, rStar * 1.65f));
            gL = (float)Math.Round(Math.Min(W * .45f, mx + rStar * 1.5f));
            gR = (float)Math.Round(W - W * .045f);
            gTop = (float)Math.Round(H * .14f);
            gSt = StatusPanelTop(Height, uiBotH, idleH, 1f);   // (el panel con el reposo del todo abierto: la columna no se mueve mientras el panel sube)
            gBot = (float)Math.Round(gSt - H * .045f);
            if (gBot < gTop + H * .2f) gBot = gTop + H * .2f;
            gPx = Math.Max(18f, H * .052f); gLh = RocolaOsLetraHornea.AltoRenglon(gPx); gGap = gLh * .5f; gNom = gLh + gGap;
            gPad = RocolaOsLetraHornea.Margen(gPx);
            gAncho = Math.Max(120, (int)(gR - gL));
            gFade = (gBot - gTop) * .15f;
            gAnc = gTop + (gBot - gTop) * (sinc ? .27f : .14f);
        }
        private float LetraPitch(int k)
        {
            if (k < 0 || letraIt == null || k >= letraIt.N || letraIt.Verso[k] < 0) return gLh * .9f + gGap;
            return Math.Max(1, letraRen[k + 1] != null ? letraRen[k + 1].Length : 1) * gLh + gGap;
        }
        // lo que hay de la cosa k (su borde de arriba) a la c: lo que se baja si la c esta en su sitio
        private float LetraRel(int k, int c)
        {
            float r = 0;
            if (k >= c) { for (int j = c; j < k; j++) r += LetraPitch(j); }
            else { for (int j = k; j < c; j++) r -= LetraPitch(j); }
            return r;
        }
        private void LetraMide(int k, RocolaOsLetra l)
        {
            if (k < 0 || k >= letraIt.N || letraRen[k + 1] != null) return;
            int v = letraIt.Verso[k];
            letraRen[k + 1] = v < 0 ? new string[] { "" } : RocolaOsLetraHornea.Parte(l.Versos[v], gPx, gAncho);
        }

        // g381: lo vivo de cada cuadro (antes de abrirlo: aqui se hornea y se sube)
        internal void RocolaOsLetraPrepara()
        {
            letraVa = false;
            if (!Form1.RocolaOsLetraCielo || stars == null) { LetraLibera(); return; }
            Form1 r = RocolaOs.LaRocola;
            RocolaOsLetra l = RocolaOsLetraSuena.Actual;
            double ms = AhoraMs();
            double dt = letraClock < 0 ? 0 : Math.Max(0, Math.Min(0.25, (ms - letraClock) / 1000.0)); letraClock = ms;
            string ruta = r != null ? r.RocolaOsLetraRuta() : null;
            if (r == null || l == null || l.Versos.Length == 0 || ruta == null || l.Ruta != ruta || idleBlend <= 0.001f)
            {   // (no se ve: el reposo cerrado, otra cancion o sin letra)
                letraIdle = 0; letraPermite = 1;
                if (idleBlend <= 0.001f || l == null || ruta == null || l.Ruta != ruta) LetraLibera();
                letraTienePos = false; letraCurNew = int.MinValue; letraN = 0;
                return;
            }
            if (!ReferenceEquals(letraEstrellas, stars) || letraAncho != Width || letraAlto != Height || !ReferenceEquals(letraDe, l))
            {
                LetraLibera(); letraEstrellas = stars; letraAncho = Width; letraAlto = Height; letraDe = l;
                letraTienePos = false; letraCurNew = int.MinValue; letraPermite = 1;
                letraIt = RocolaOsLetraModelo.Arma(l, r.RocolaOsLetraTotal());
                int nn = letraIt.N + 2;
                letraRen = new string[nn][]; letraY = new float[nn]; letraVel = new float[nn]; letraOk = new bool[nn];
            }
            RocolaOsLetraItems it = letraIt;
            // el reposo abierto (de 0,55 a 1) y que no haya nada abierto encima (se va en 0,15 s)
            letraIdle = RocolaOsLetraModelo.Suave((idleBlend - 0.55) / 0.45);
            float permiteMeta = r.RocolaOsLetraPermite() ? 1f : 0f;
            letraPermite += (permiteMeta - letraPermite) * (float)(1.0 - Math.Exp(-dt / 0.05));
            if (Math.Abs(letraPermite - permiteMeta) < 0.01f) letraPermite = permiteMeta;
            if (permiteMeta == 0f)
            {   // se fue: se deja ir lo que habia sin volver a calcular nada
                letraAlfa = letraIdle * letraPermite;
                letraVa = letraN > 0 && letraAlfa > 0.004f;
                return;
            }
            if (it.N == 0) { letraN = 0; return; }
            // la posicion: la que se oye, medida a lo mas cada 50 ms y suavizada entre una medida y otra
            double total = r.RocolaOsLetraTotal();
            if (ms - letraMedidoMs >= 50)
            {
                double m = r.RocolaOsLetraPos(); letraMedidoMs = ms;
                if (!letraTienePos || Math.Abs(m - letraPos) > 0.35) letraPos = m; else letraPos += (m - letraPos) * 0.25;
                letraTienePos = true;
            }
            else letraPos += dt;
            double pos = letraPos;
            LetraGeometria(it.Sinc);
            int cur, lo, hi; float frac = 0f;
            letraMov = 0f;
            if (it.Sinc)
            {
                cur = RocolaOsLetraModelo.Actual(it, pos);
                if (letraCurNew == int.MinValue) { letraCurNew = letraCurOld = cur; letraCambioMs = -1e9; Array.Clear(letraOk, 0, letraOk.Length); }
                else if (cur != letraCurNew)
                {
                    if (Math.Abs(cur - letraCurNew) > 5) { letraCurNew = letraCurOld = cur; letraCambioMs = -1e9; Array.Clear(letraOk, 0, letraOk.Length); }
                    else { letraCurOld = letraCurNew; letraCurNew = cur; letraCambioMs = ms; }
                }
                lo = Math.Max(-1, Math.Min(letraCurOld, letraCurNew) - 3); hi = Math.Min(it.N - 1, Math.Max(letraCurOld, letraCurNew) + 9);
                for (int k = Math.Max(0, lo); k <= hi; k++) LetraMide(k, l);
                double el = (ms - letraCambioMs) / 1000.0;
                double stag = RocolaOsLetraModelo.RetrasoS * RocolaOsLetraModelo.CambioS / 0.55;
                float omega = (float)(5.9 / RocolaOsLetraModelo.CambioS);
                for (int k = lo; k <= hi; k++)
                {
                    if (k < 0) continue;
                    double delay = letraCurNew > letraCurOld && k > letraCurOld ? stag * Math.Min(8, k - letraCurOld) : 0;
                    int ca = el >= delay ? letraCurNew : letraCurOld;
                    float meta = gAnc + LetraRel(k, ca);
                    if (!letraOk[k + 1]) { letraY[k + 1] = meta; letraVel[k + 1] = 0f; letraOk[k + 1] = true; }
                    else RocolaOsLetraModelo.Resorte(ref letraY[k + 1], ref letraVel[k + 1], meta, omega, (float)dt);
                    letraMov = Math.Max(letraMov, Math.Abs(letraY[k + 1] - meta));
                }
                if (lo - 1 >= 0) letraOk[lo] = false; if (hi + 2 < letraOk.Length) letraOk[hi + 2] = false;   // (los que salen de la ventana vuelven a su sitio al entrar)
            }
            else
            {   // sin tiempos (la del MP3, o la plainLyrics de LRCLIB): la columna baja despacio y continua, repartida por la cancion
                double p = RocolaOsLetraModelo.Reparto(it.N, pos, total);
                cur = (int)Math.Floor(p); frac = (float)(p - cur);
                lo = Math.Max(0, cur - 3); hi = Math.Min(it.N - 1, cur + 9);
                for (int k = lo; k <= hi; k++) LetraMide(k, l);
                for (int k = lo; k <= hi; k++) letraY[k + 1] = gAnc + LetraRel(k, cur) - frac * LetraPitch(cur);
                letraCurNew = cur;
            }
            // lo que se ve: lo que cae dentro de la columna (con sus alfas, desenfoques y tamanos segun su sitio)
            letraAlfa = letraIdle * letraPermite;
            letraN = 0; letraCurY = float.NaN;
            for (int k = Math.Max(0, lo); k <= hi && letraN < LetraMaxPz; k++)
            {
                float y = letraY[k + 1];
                float alto = Math.Max(1, letraRen[k + 1].Length) * gLh;
                if (y + alto < gTop - gPad || y > gBot + gPad) continue;
                float d = (y - gAnc) / gNom, a, b, s, pv = 0f;
                if (it.Sinc)
                {
                    a = RocolaOsLetraModelo.AlfaD(d); b = RocolaOsLetraModelo.BlurD(d); s = RocolaOsLetraModelo.EscalaD(d);
                    if (it.Verso[k] < 0) { if (k != letraCurNew) continue; pv = RocolaOsLetraModelo.PuntosVis(it, k, pos); if (pv <= 0.004f) continue; }
                    if (k == letraCurNew) letraCurY = y;
                }
                else { a = RocolaOsLetraModelo.AlfaMp3; b = 0f; s = 1f; if (k == cur) letraCurY = y; }
                letraPzK[letraN] = k; letraPzY[letraN] = y; letraPzS[letraN] = s; letraPzA[letraN] = a; letraPzB[letraN] = b; letraPzP[letraN] = pv; letraN++;
            }
            // lo horneado, UN mapa por cuadro (el nitido de un verso en un cuadro y su desenfocado en el siguiente: cada uno cuesta unos milisegundos): primero el
            // desenfocado que quedo pendiente y despues, en el orden de lo que se ve (el que se canta, los que siguen, el anterior), el nitido del primero que falte
            int hechos = 0, baseK = Math.Max(0, cur);
            if (letraPend != null) { if (letraPend.Pend != null) LetraHorneaBorroso(letraPend); letraPend = null; hechos++; }
            for (int q = 0; q < 12 && hechos < 1; q++)
            {
                int k = baseK + (q == 0 ? 0 : q <= 7 ? q : -(q - 7));
                if (k < lo || k > hi || k < 0 || k >= it.N || it.Verso[k] < 0) continue;
                RocolaOsLetraLinea ln = LetraLinea(k);
                if (ln.TexA == IntPtr.Zero) { LetraHornea(ln, k, it.Sinc); hechos++; }
            }
            if (hechos < 1 && LetraTienePuntos() && letraPuntoTex == IntPtr.Zero) { LetraHorneaPunto(); hechos++; }
            if (hechos < 1 && (letraCreditoTex == IntPtr.Zero || letraCreditoTexto != LetraCreditoTexto(l))) LetraHorneaCredito(l);
            // las que ya no hacen falta (lejos del verso de ahora) se sueltan
            if (letraLineas.Count > 14) LetraRecorta(baseK);
            letraVa = letraN > 0 && letraAlfa > 0.004f;
            LetraAnota(ms, pos, cur, letraCurY, letraMov);
        }
        private bool LetraTienePuntos() { if (letraIt == null) return false; for (int k = 0; k < letraIt.N; k++) if (letraIt.Verso[k] < 0) return true; return false; }
        private RocolaOsLetraLinea LetraLinea(int k)
        {
            RocolaOsLetraLinea ln;
            if (letraLineas.TryGetValue(k, out ln)) return ln;
            ln = new RocolaOsLetraLinea { De = letraEstrellas };
            letraLineas[k] = ln;
            return ln;
        }
        // el mapa nitido de un verso (si la letra lleva tiempos, guarda el mapa para desenfocarlo en el cuadro siguiente)
        private void LetraHornea(RocolaOsLetraLinea ln, int k, bool conBlur)
        {
            GpuStarfield st = letraEstrellas; if (st == null) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            SKBitmap bmp = RocolaOsLetraHornea.Dibuja(letraRen[k + 1], gPx, gAncho);
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            float u, v; IntPtr tex = st.CreateOverlay(bmp, out u, out v);
            letraMsHornea = Math.Max(letraMsHornea, Ms(t1 - t0)); letraMsSube = Math.Max(letraMsSube, Ms(System.Diagnostics.Stopwatch.GetTimestamp() - t1));
            ln.TexA = tex; ln.UA = u; ln.VA = v; ln.WA = bmp.Width; ln.HA = bmp.Height; letraHorneados++;
            if (conBlur) { ln.Pend = bmp; letraPend = ln; } else bmp.Dispose();
        }
        // el mapa desenfocado de un verso, del nitido que quedo pendiente
        private void LetraHorneaBorroso(RocolaOsLetraLinea ln)
        {
            GpuStarfield st = letraEstrellas; SKBitmap bmp = ln.Pend; ln.Pend = null;
            if (st == null || bmp == null) { if (bmp != null) bmp.Dispose(); return; }
            long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
            SKBitmap bl = RocolaOsLetraHornea.Desenfoca(bmp, gPx);
            letraMsBlur = Math.Max(letraMsBlur, Ms(System.Diagnostics.Stopwatch.GetTimestamp() - t2));
            ln.TexN = st.CreateOverlay(bl, out ln.UN, out ln.VN); ln.WN = bl.Width; ln.HN = bl.Height; letraHorneados++;
            bl.Dispose(); bmp.Dispose();
        }
        private static float Ms(long ticks) { return (float)(ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency); }
        private void LetraHorneaPunto()
        {
            GpuStarfield st = letraEstrellas; if (st == null) return;
            int d = Math.Max(8, (int)Math.Round(Height * .02f));
            SKBitmap bmp = RocolaOsLetraHornea.Punto(d);
            letraPuntoTex = st.CreateOverlay(bmp, out letraPuntoU, out letraPuntoV); letraPuntoW = bmp.Width; letraPuntoDe = st; bmp.Dispose(); letraHorneados++;
        }
        private static string LetraCreditoTexto(RocolaOsLetra l) { return "Letra: " + (l.Proveedor ?? ""); }
        private void LetraHorneaCredito(RocolaOsLetra l)
        {
            GpuStarfield st = letraEstrellas; if (st == null) return;
            string t = LetraCreditoTexto(l);
            if (letraCreditoTex != IntPtr.Zero && ReferenceEquals(letraCreditoDe, st)) Tarjeta.Suelta(letraCreditoTex);
            letraCreditoTex = IntPtr.Zero;
            float px = Math.Max(11f, Height * 0.0175f);
            SKBitmap bmp = RocolaOsLetraHornea.Dibuja(new string[] { t }, px, RocolaOsLetraHornea.AnchoDe(t, px));
            letraCreditoTex = st.CreateOverlay(bmp, out letraCreditoU, out letraCreditoV); letraCreditoW = bmp.Width; letraCreditoH = bmp.Height;
            bmp.Dispose(); letraCreditoDe = st; letraCreditoTexto = t; letraHorneados++;
        }
        private void LetraRecorta(int cur)
        {
            List<int> fuera = null;
            foreach (KeyValuePair<int, RocolaOsLetraLinea> kv in letraLineas)
                if (kv.Key < cur - 4 || kv.Key > cur + 11) { if (fuera == null) fuera = new List<int>(); fuera.Add(kv.Key); }
            if (fuera == null) return;
            foreach (int k in fuera) { if (ReferenceEquals(letraLineas[k].De, stars)) letraLineas[k].Suelta(); letraLineas.Remove(k); }
        }
        private void LetraLibera()
        {
            if (letraLineas.Count > 0) { foreach (RocolaOsLetraLinea ln in letraLineas.Values) if (ReferenceEquals(ln.De, stars)) ln.Suelta(); letraLineas.Clear(); }
            if (letraCreditoTex != IntPtr.Zero) { if (ReferenceEquals(letraCreditoDe, stars)) Tarjeta.Suelta(letraCreditoTex); letraCreditoTex = IntPtr.Zero; }
            if (letraPuntoTex != IntPtr.Zero) { if (ReferenceEquals(letraPuntoDe, stars)) Tarjeta.Suelta(letraPuntoTex); letraPuntoTex = IntPtr.Zero; }
            letraCreditoTexto = null; letraVa = false; letraN = 0; letraDe = null; letraIt = null; letraEstrellas = null; letraPend = null;
            letraRen = null; letraY = null; letraVel = null; letraOk = null; letraCurNew = int.MinValue;
        }

        // g380: lo que se ve (dentro del cuadro)
        internal void RocolaOsLetraPinta()
        {
            if (!letraVa || letraN <= 0 || letraAlfa <= 0.004f) return;
            GpuStarfield st = letraEstrellas; if (st == null || !ReferenceEquals(st, stars)) return;
            // donde esta la estrella en este cuadro (como DrawDetailedMoon): la letra se atenua cerca de ella
            float emph = LunaProtagonista ? Math.Max(MoonEmphasis(Dim, true), idleBlend) : 0;
            float sr = DetailedMoonDiameter(Height, emph) * MoonSceneScale(idleBlend, LunaActiva) * .5f;
            PointF sc = LunaActiva ? StarSceneCenter() : DetailedMoonCenter(Width, Height, moonMotionTime, idleBlend, IdleRise, false);
            starLx = Math.Max(sr, Math.Min(Width - sr, sc.X)); starLy = Math.Max(sr, Math.Min(StatusTopY - sr, sc.Y)); starLr = sr;
            RocolaOsLetraItems it = letraIt;
            for (int q = 0; q < letraN; q++)
            {
                int k = letraPzK[q]; float y = letraPzY[q], s = letraPzS[q], aTot = letraPzA[q] * letraAlfa;
                if (aTot <= 0.004f) continue;
                if (it.Verso[k] < 0)
                {   // los tres puntos: en el sitio del verso, respirando uno tras otro
                    if (letraPuntoTex == IntPtr.Zero) continue;
                    float dd = letraPuntoW; double t = AhoraMs() / 1000.0; float gp = letraPzP[q];
                    float cy = y + gLh * .5f, paso = Height * .02f * 2.2f;
                    for (int i = 0; i < 3; i++)
                    {
                        float br = RocolaOsLetraModelo.PuntoBrillo(t, i);
                        float sz = dd * (1f + .35f * br) * (.7f + .3f * gp);
                        float cx = gL + Height * .02f * .5f + i * paso;
                        LetraCaja(st, letraPuntoTex, cx - sz / 2, cy - sz / 2, cx + sz / 2, cy + sz / 2, letraPuntoU, letraPuntoV, aTot * gp * (.40f + .55f * br));
                    }
                    continue;
                }
                RocolaOsLetraLinea ln;
                if (!letraLineas.TryGetValue(k, out ln) || ln.TexA == IntPtr.Zero) continue;
                float alto = Math.Max(1, letraRen[k + 1].Length) * gLh;
                float bw = letraPzB[q];
                if (ln.TexN == IntPtr.Zero) bw = 0f;
                // el mapa mide el texto mas el margen: el texto empieza en gL, a la escala s desde su borde izquierdo y desde el centro del bloque
                float cyb = y + alto * .5f;
                float x0 = gL - gPad * s, x1 = x0 + ln.WA * s, y0 = cyb - ln.HA * s * .5f, y1 = cyb + ln.HA * s * .5f;
                if (bw < 0.995f) LetraCaja(st, ln.TexA, x0, y0, x1, y1, ln.UA, ln.VA, aTot * (1f - bw));
                if (bw > 0.005f) LetraCaja(st, ln.TexN, x0, y0, x1, y1, ln.UN, ln.VN, aTot * bw);
            }
            if (letraCreditoTex != IntPtr.Zero && ReferenceEquals(letraCreditoDe, st))
            {
                int a = (int)(190f * letraAlfa + 0.5f);
                float x1 = Width - Width * 0.014f, y1 = gSt - Height * 0.004f;
                uint c = ((uint)a << 24) | 0xDDE4FFu;
                letraQuad[0] = LetraVt(x1 - letraCreditoW, y1 - letraCreditoH, 0, 0, c); letraQuad[1] = LetraVt(x1, y1 - letraCreditoH, letraCreditoU, 0, c);
                letraQuad[2] = LetraVt(x1, y1, letraCreditoU, letraCreditoV, c); letraQuad[5] = LetraVt(x1 - letraCreditoW, y1, 0, letraCreditoV, c);
                letraQuad[3] = letraQuad[0]; letraQuad[4] = letraQuad[2];
                st.DrawQuad(letraCreditoTex, letraQuad);
            }
        }
        private readonly Tarjeta.Vtx[] letraQuad = new Tarjeta.Vtx[6];
        private float starLx, starLy, starLr;
        // el alfa de la columna en (x, y): el desvanecido de arriba y abajo y la atenuacion cerca de la estrella
        private float LetraAlfaEn(float x, float y)
        {
            float m = RocolaOsLetraModelo.Mascara(y, gTop, gBot, gFade);
            float dx = x - starLx, dy = y - starLy, d = (float)Math.Sqrt(dx * dx + dy * dy), r0 = starLr * .8f, r1 = starLr * 1.25f;
            if (d < r1) m *= RocolaOsLetraModelo.Lerp(0.2f, 1f, RocolaOsLetraModelo.Suave((d - r0) / (r1 - r0)));
            return m;
        }
        // UNA CAJA con su mapa (blanco): recortada a la columna y partida en tiras (el desvanecido) y, cerca de la estrella, en celdas (la atenuacion); el alfa de
        // cada esquina sale de LetraAlfaEn. a: el alfa de todo.
        private void LetraCaja(GpuStarfield st, IntPtr tex, float x0, float y0, float x1, float y1, float u1, float v1, float a)
        {
            if (a <= 0.004f || x1 <= x0 || y1 <= y0) return;
            float ya = Math.Max(y0, gTop), yb = Math.Min(y1, gBot);
            if (yb <= ya) return;
            int ny = 0; letraYs[ny++] = ya;
            for (int q = 1; q <= 3; q++) { float yy = gTop + gFade * q / 4f; if (yy > ya + 0.5f && yy < yb - 0.5f) letraYs[ny++] = yy; }
            for (int q = 3; q >= 1; q--) { float yy = gBot - gFade * q / 4f; if (yy > letraYs[ny - 1] + 0.5f && yy < yb - 0.5f) letraYs[ny++] = yy; }
            letraYs[ny++] = yb;
            // ¿la estrella toca la caja? entonces celdas de ~36 px; si no, una sola columna
            float cxn = Math.Max(x0, Math.Min(x1, starLx)), cyn = Math.Max(ya, Math.Min(yb, starLy));
            float ddx = cxn - starLx, ddy = cyn - starLy;
            int nx = 1;
            if (ddx * ddx + ddy * ddy < starLr * 1.25f * starLr * 1.25f) nx = Math.Min(letraXs.Length - 1, (int)Math.Ceiling((x1 - x0) / 24f));
            for (int i = 0; i <= nx; i++) letraXs[i] = x0 + (x1 - x0) * i / nx;
            float du = u1 / (x1 - x0), dv = v1 / (y1 - y0);
            letraBatchN = 0;
            for (int j = 0; j + 1 < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    if (letraBatchN + 6 > letraBatch.Length) { st.DrawTexturedBatch(tex, letraBatch, letraBatchN / 3); letraBatchN = 0; }
                    float xa = letraXs[i], xb = letraXs[i + 1], yt = letraYs[j], yu = letraYs[j + 1];
                    uint c00 = LetraCol(xa, yt, a), c10 = LetraCol(xb, yt, a), c11 = LetraCol(xb, yu, a), c01 = LetraCol(xa, yu, a);
                    float ua = (xa - x0) * du, ub = (xb - x0) * du, va = (yt - y0) * dv, vb = (yu - y0) * dv;
                    Tarjeta.Vtx[] q = letraBatch; int o = letraBatchN;
                    q[o] = LetraVt(xa, yt, ua, va, c00); q[o + 1] = LetraVt(xb, yt, ub, va, c10); q[o + 2] = LetraVt(xb, yu, ub, vb, c11);
                    q[o + 3] = q[o]; q[o + 4] = q[o + 2]; q[o + 5] = LetraVt(xa, yu, ua, vb, c01);
                    letraBatchN += 6;
                }
            if (letraBatchN > 0) st.DrawTexturedBatch(tex, letraBatch, letraBatchN / 3);
        }
        private uint LetraCol(float x, float y, float a)
        {
            int al = (int)Math.Max(0, Math.Min(255, 255f * a * LetraAlfaEn(x, y) + 0.5f));
            return ((uint)al << 24) | 0xFFFFFFu;
        }
        private static Tarjeta.Vtx LetraVt(float x, float y, float u, float v, uint c) { Tarjeta.Vtx r = new Tarjeta.Vtx(); r.X = x - 0.5f; r.Y = y - 0.5f; r.Z = 0; r.Rhw = 1; r.Color = c; r.U = u; r.V = v; return r; }

        // (las pruebas: ROCOLA_LETRA_LOG=archivo: una linea por cuadro como mucho cada 50 ms, con la hora de pared, la posicion, la cosa de la columna, donde esta
        // el verso que se canta (y) y cuanto le falta a lo que se mueve (mov, en pixeles))
        private void LetraAnota(double ms, double pos, int cur, float ycur, float mov)
        {
            if (!letraLogProbado)
            {
                letraLogProbado = true;
                string ruta = Environment.GetEnvironmentVariable("ROCOLA_LETRA_LOG");
                if (!string.IsNullOrEmpty(ruta)) { try { letraLog = new StreamWriter(ruta, true, new System.Text.UTF8Encoding(false)); letraLog.AutoFlush = true; } catch { letraLog = null; } }
            }
            if (letraLog == null || ms - letraLogMs < 50) return;
            letraLogMs = ms;
            try
            {
                if (!letraGeoAnotada)
                {   // (la primera linea dice donde esta la columna, para que la prueba mida contra eso)
                    letraGeoAnotada = true;
                    letraLog.WriteLine("geo L=" + gL.ToString("0", CultureInfo.InvariantCulture) + " R=" + gR.ToString("0", CultureInfo.InvariantCulture) + " T=" + gTop.ToString("0", CultureInfo.InvariantCulture)
                        + " B=" + gBot.ToString("0", CultureInfo.InvariantCulture) + " A=" + gAnc.ToString("0", CultureInfo.InvariantCulture) + " px=" + gPx.ToString("0.0", CultureInfo.InvariantCulture)
                        + " lh=" + gLh.ToString("0.0", CultureInfo.InvariantCulture) + " nom=" + gNom.ToString("0.0", CultureInfo.InvariantCulture));
                }
                letraLog.WriteLine(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture) + " pos=" + pos.ToString("0.000", CultureInfo.InvariantCulture)
                    + " cosa=" + cur.ToString(CultureInfo.InvariantCulture) + " y=" + ycur.ToString("0.0", CultureInfo.InvariantCulture) + " mov=" + mov.ToString("0.0", CultureInfo.InvariantCulture)
                    + " idle=" + letraIdle.ToString("0.00", CultureInfo.InvariantCulture) + " piezas=" + letraN.ToString(CultureInfo.InvariantCulture)
                    + " horneados=" + letraHorneados.ToString(CultureInfo.InvariantCulture)
                    + " ms_dibuja=" + letraMsHornea.ToString("0.0", CultureInfo.InvariantCulture) + " ms_borra=" + letraMsBlur.ToString("0.0", CultureInfo.InvariantCulture) + " ms_sube=" + letraMsSube.ToString("0.0", CultureInfo.InvariantCulture));
                letraMsHornea = letraMsBlur = letraMsSube = 0f;
            }
            catch { }
        }
    }
}
