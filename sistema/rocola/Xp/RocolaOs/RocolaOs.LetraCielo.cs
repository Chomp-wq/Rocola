// LA LETRA EN EL CIELO DEL ESTADO EXTENDIDO (K6; decision del dueno del 20-11-2026, de la maqueta 3: «Me gusta asi la letra»; FASE11_letras.md, §6).
// Solo Rocola OS, solo la tarjeta (el dibujo por procesador sigue congelado). Interruptor: letra_cielo= (la funcion de RocolaOs.LetraSuena.cs, que ya
// pide la letra de lo que suena a LRCLIB o al MP3; encendida de fabrica). La FUENTE no se toca: esto lee RocolaOsLetraSuena.Actual.
//
// LO QUE SE VE (en el reposo con musica, en el cielo libre entre la estrella y la aurora; dos renglones centrados):
//   - el que se canta, con la letra de los titulos de XP (Trebuchet MS en negrita) y una sombra suave, BLANCO que SE LLENA DE AMBAR (#FFCE78) DE
//     IZQUIERDA A DERECHA AL COMPAS de la cancion (con borde suave de 10 px); el que sigue, mas chico y en lavanda (#D6CCFF);
//   - al cambiar de verso, el de arriba sube y se apaga en 0,3 s (el que seguia se hace grande en esos 0,3 s y entra el nuevo que sigue);
//   - «Letra: LRCLIB» (o «Letra: el MP3»), chiquito, abajo a la derecha del cielo (sobre el panel AHORA SUENA);
//   - en los tramos sin voz (un renglon vacio del LRC, o un verso que ya se canto del todo y se queda sin otro mucho rato), el cielo de XP tal cual
//     (todo se apaga en 0,3 s y vuelve con el siguiente verso);
//   - con la letra del MP3 (sin tiempos; maqueta 4): los renglones suben despacio, repartidos por la duracion de la cancion, sin llenarse; sin la
//     letra «de verdad» no se sabe cuando se canta cada uno, asi que es un repaso aproximado (y se dice: «Letra: el MP3»).
//   - no sale con el disco abierto, la pared, el buscador, la ayuda, Ctrl+F3/Ctrl+F4, la atraccion ni con un video: solo con el reposo abierto
//     (idleBlend), que es cuando XP esconde la pared; con el reposo a medias aparece con el (de 0,55 a 1). Si se abre alguno de esos con el reposo
//     aun a la vista, la letra se va en 0,15 s.
//
// LA CALMA (regla 5 del cielo vivo): la letra es CONTENIDO, no adorno: no se atenua con la calma. No hace falta: el reposo solo esta abierto cuando
// nadie toca nada (15 s), y en cuanto se toca un boton el reposo se cierra y con el la letra.
//
// LA REGLA 3 (se lee siempre): el texto blanco/ambar/lavanda lleva una sombra doble (una nube desenfocada y un filo); la prueba mide el contraste con
// el cielo de dia, de noche, con nubes y con fiestas.
//
// COMO SE PINTA: cada renglon se hornea UNA vez, en Prepara (fuera del cuadro; Direct3D no deja crear texturas con la escena abierta), como un mapa
// de bits blanco con su sombra negra: el color lo pone el color del vertice (la sombra, negra, no cambia con el tinte). Como mucho DOS horneados por
// cuadro (1-2 ms cada uno). El llenado de ambar son vertices: lo ya llenado, el ambar; lo que falta, el blanco; y en los 10 px del borde las dos
// capas con alfas contrarios. Nada de memoria nueva por cuadro (los vertices son un arreglo fijo).
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
    // UNA COSA QUE DIBUJAR (pura: la prueba la recorre)
    internal struct RocolaOsLetraPieza
    {
        internal int Verso;      // el indice en Letra.Versos
        internal bool Grande;    // true: el estilo del que se canta; false: el del que sigue (mas chico y lavanda)
        internal float Desde, Hasta;   // el sitio al empezar el cambio y al acabarlo (en renglones: -1 el que sale, 0 el que se canta, 1 el que sigue, 2 el que entra); todo sube un renglon
        internal float Alfa;     // 0 a 1
        internal float Llena;    // 0 a 1: lo llenado de ambar; -1: no se llena (blanco)
    }

    // LA CUENTA DE LO QUE SE VE EN CADA INSTANTE (pura, sin dibujar nada)
    internal static class RocolaOsLetraModelo
    {
        // lo que tarda el cambio de verso (0,3 s; ROCOLA_LETRA_CAMBIO_S, solo para las pruebas: con las capturas que paran el cuadro casi un segundo no se
        // puede ver un cambio de 0,3 s, y una de 3 s si)
        internal static readonly double CambioS = CambioDe(Environment.GetEnvironmentVariable("ROCOLA_LETRA_CAMBIO_S"));
        private static double CambioDe(string e) { double v; return double.TryParse(e, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 0.05 && v <= 10 ? v : 0.3; }
        internal const double LlenaMaxS = 10;    // lo mas que tarda en llenarse un renglon
        internal const double SobraS = 4;        // cuanto se queda lleno un renglon cuyo verso dura mucho mas de lo que se llena, antes de apagarse
        internal const uint Ambar = 0xFFCE78u, Lavanda = 0xD6CCFFu, Blanco = 0xFFFFFFu;

        // el siguiente verso con texto despues de i (-1 si no hay)
        internal static int Siguiente(RocolaOsLetra l, int i)
        {
            for (int k = i + 1; k < l.Versos.Length; k++) if (l.Versos[k].Length > 0) return k;
            return -1;
        }
        // lo que dura el verso i hasta el siguiente tiempo (el ultimo, hasta el final de la cancion o 6 s)
        internal static double Span(RocolaOsLetra l, int i, double total)
        {
            double ini = l.Tiempos[i];
            double fin = i + 1 < l.Tiempos.Length ? l.Tiempos[i + 1] : (total > ini + 1 ? total : ini + 6);
            return Math.Max(0.2, fin - ini);
        }
        // lo que se tarda en llenar el renglon i
        internal static double Dura(RocolaOsLetra l, int i, double total) { return Math.Min(Span(l, i, total), LlenaMaxS); }
        // lo llenado (0 a 1) en pos
        internal static float Llena(RocolaOsLetra l, int i, double pos, double total)
        {
            double f = (pos - l.Tiempos[i]) / Dura(l, i, total);
            return f <= 0 ? 0f : f >= 1 ? 1f : (float)f;
        }
        // ¿hay voz en el verso i en pos? (un renglon vacio no; uno que dura mucho mas de lo que se llena, se apaga SobraS despues de llenarse)
        internal static bool HayVoz(RocolaOsLetra l, int i, double pos, double total)
        {
            if (i < 0 || l.Versos[i].Length == 0) return false;
            double span = Span(l, i, total), dura = Dura(l, i, total);
            return !(span > dura + SobraS && pos - l.Tiempos[i] > dura + SobraS);
        }
        internal static float Suave(double t) { if (t <= 0) return 0; if (t >= 1) return 1; return (float)(t * t * (3 - 2 * t)); }

        // LAS PIEZAS. prev: el renglon que sale (-1 si ninguno); cur: el que se canta; nxt: el que sigue (-1 si no hay); u: 0 a 1, lo que va del cambio
        // (1 = ya cambio); llena: lo llenado de cur (-1: no se llena); llenaPrev: lo de prev (1: lleno; -1: no se llena); curEraElSiguiente: cur era el
        // que seguia (se hace grande desde su estilo chico). Devuelve cuantas puso en p (hasta 4).
        internal static int Piezas(RocolaOsLetraPieza[] p, int prev, int cur, int nxt, float u, float llena, float llenaPrev, bool curEraElSiguiente)
        {
            int n = 0;
            if (cur < 0) return 0;
            bool cambia = prev >= 0 && prev != cur && u < 1f;
            if (cambia)
            {   // una escalera: todo sube un renglon (el que se cantaba sale arriba y se apaga; el que seguia se hace grande en su sitio; entra otro abajo)
                p[n++] = new RocolaOsLetraPieza { Verso = prev, Grande = true, Desde = 0, Hasta = -1, Alfa = 1f - u, Llena = llenaPrev };
                if (curEraElSiguiente)
                {
                    p[n++] = new RocolaOsLetraPieza { Verso = cur, Grande = true, Desde = 1, Hasta = 0, Alfa = Suave((u - 0.3) / 0.7), Llena = llena };
                    p[n++] = new RocolaOsLetraPieza { Verso = cur, Grande = false, Desde = 1, Hasta = 0, Alfa = 1f - Suave(u / 0.5), Llena = -1 };
                    if (nxt >= 0) p[n++] = new RocolaOsLetraPieza { Verso = nxt, Grande = false, Desde = 2, Hasta = 1, Alfa = Suave((u - 0.4) / 0.6), Llena = -1 };
                }
                else
                {   // un salto (no era el que seguia): el nuevo entra en su sitio, sin escalera
                    p[n++] = new RocolaOsLetraPieza { Verso = cur, Grande = true, Desde = 0, Hasta = 0, Alfa = u, Llena = llena };
                    if (nxt >= 0) p[n++] = new RocolaOsLetraPieza { Verso = nxt, Grande = false, Desde = 1, Hasta = 1, Alfa = u, Llena = -1 };
                }
            }
            else
            {
                p[n++] = new RocolaOsLetraPieza { Verso = cur, Grande = true, Desde = 0, Hasta = 0, Alfa = 1f, Llena = llena };
                if (nxt >= 0) p[n++] = new RocolaOsLetraPieza { Verso = nxt, Grande = false, Desde = 1, Hasta = 1, Alfa = 1f, Llena = -1 };
            }
            return n;
        }

        // LOS RENGLONES QUE DICEN ALGO de una letra sin tiempos, en orden
        internal static int[] ConTexto(RocolaOsLetra l)
        {
            List<int> r = new List<int>();
            for (int i = 0; i < l.Versos.Length; i++) if (l.Versos[i].Length > 0) r.Add(i);
            return r.ToArray();
        }
        // LA POSICION (en renglones) de una letra sin tiempos: se reparte por la duracion de la cancion
        internal static double Reparto(int renglones, double pos, double total)
        {
            if (renglones <= 0 || total <= 1) return 0;
            double p = pos / total * renglones;
            return p < 0 ? 0 : p > renglones - 1 ? renglones - 1 : p;
        }
    }

    // UN RENGLON HORNEADO (los dos estilos: el que se canta y el que sigue)
    internal sealed class RocolaOsLetraLinea
    {
        internal IntPtr TexA, TexN; internal float UA, VA, UN, VN; internal int WA, HA, WN, HN; internal float TxA0, TxA1, TxN0, TxN1;   // (Tx0/Tx1: donde empieza y acaba el texto dentro del mapa)
        internal GpuStarfield De;
        internal int Verso;
        internal void Suelta()
        {
            if (De != null) { if (TexA != IntPtr.Zero) Tarjeta.Suelta(TexA); if (TexN != IntPtr.Zero) Tarjeta.Suelta(TexN); }
            TexA = TexN = IntPtr.Zero;
        }
    }

    internal static class RocolaOsLetraHornea
    {
        // EL MAPA DE UN RENGLON: blanco con su sombra negra, con la letra de los titulos de XP (EstadoReposoGpu: Trebuchet MS en negrita). tx0/tx1: donde
        // empieza y acaba el texto dentro del mapa (el resto es sitio para la sombra). Un renglon largo se achica hasta el 60 % y despues se corta con puntos.
        internal static SKBitmap Dibuja(string texto, float px, int anchoMax, out float tx0, out float tx1)
        {
            AjusteLetra ajuste = AjusteLetra.SuaveAjustada;
            FormatoTexto sf = new FormatoTexto(); sf.Banderas = BanderasTexto.SinPartir; sf.Vertical = Alinea.Centro; sf.Horizontal = Alinea.Cerca; sf.Corte = CorteTexto.PuntosPorLetra;
            string fam = Letras.Resuelve("Trebuchet MS");
            float p = px;
            float tw, th;
            using (Rocola.Nativo.Letra f0 = Rocola.Nativo.Letra.EnPixeles(fam, p, true))
            {
                SizeF m = TextoGdiPlus.Mide(texto, f0, ajuste, SizeF.Empty, sf);
                tw = (float)Math.Ceiling(m.Width); th = (float)Math.Ceiling(m.Height);
            }
            for (int vuelta = 0; vuelta < 4 && tw > anchoMax && p > px * 0.6f; vuelta++)
            {   // (se achica un poco de mas: con la letra que no escala justo, la medida da unos pixeles de mas y ponia puntos)
                p = Math.Max(px * 0.6f, p * anchoMax / tw * 0.97f);
                using (Rocola.Nativo.Letra f1 = Rocola.Nativo.Letra.EnPixeles(fam, p, true))
                {
                    SizeF m = TextoGdiPlus.Mide(texto, f1, ajuste, SizeF.Empty, sf);
                    tw = (float)Math.Ceiling(m.Width); th = (float)Math.Ceiling(m.Height);
                }
            }
            if (tw > anchoMax) tw = anchoMax;
            int pad = (int)Math.Ceiling(p * 0.42f) + 4;
            int w = (int)tw + 2 * pad + 24, h = (int)th + 2 * pad;
            tx0 = pad + 12; tx1 = pad + 12 + tw;
            SKBitmap bmp = Mapas.Nuevo(w, h);
            using (Rocola.Nativo.Letra f = Rocola.Nativo.Letra.EnPixeles(fam, p, true))
            {
                RectangleF caja = new RectangleF(pad + 12, pad, tw + 12, th);   // (con holgura: el recorte de GDI+ cortaba la ultima letra)
                // la sombra: el mismo texto en negro, desenfocado, en dos tamanos (una nube y un filo), un poco mas abajo
                using (SKBitmap negro = Mapas.Nuevo(w, h))
                {
                    using (SKCanvas c = new SKCanvas(negro))
                    using (SKPaint pn = new SKPaint { Color = new SKColor(0, 0, 0, 255) })
                        TextoGdiPlus.Escribe(c, texto, f, ajuste, pn, caja, sf);
                    using (SKCanvas g = new SKCanvas(bmp))
                    {
                        float dy = Math.Max(1.5f, p * 0.06f), sig = Math.Max(2.2f, p * 0.085f);
                        using (SKPaint nube = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(sig, sig) })
                        { g.DrawBitmap(negro, 0, dy, nube); g.DrawBitmap(negro, 0, dy, nube); }
                        using (SKPaint filo = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(1.0f, 1.0f) })
                            g.DrawBitmap(negro, 0, dy * 0.8f, filo);
                        using (SKPaint blanco = new SKPaint { Color = new SKColor(255, 255, 255, 255) })
                            TextoGdiPlus.Escribe(g, texto, f, ajuste, blanco, caja, sf);
                    }
                }
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
        private readonly Dictionary<int, RocolaOsLetraLinea> letraLineas = new Dictionary<int, RocolaOsLetraLinea>();
        private RocolaOsLetra letraDe;                 // la letra que tienen horneada las lineas
        private GpuStarfield letraEstrellas;
        private int letraAncho, letraAlto;
        private IntPtr letraCreditoTex; private float letraCreditoU, letraCreditoV; private int letraCreditoW, letraCreditoH; private string letraCreditoTexto;
        private GpuStarfield letraCreditoDe;
        private int letraPrev = -1, letraCur = -1; private double letraCambioMs = -1e9; private bool letraEraSiguiente;
        private double letraPos, letraMedidoMs = -1e9; private bool letraTienePos;
        private float letraVoz;                        // el fundido del tramo sin voz (0 a 1)
        private int letraUltimoConVoz = -1;
        private float letraIdle, letraPermite;         // 0 a 1: lo abierto del reposo; y si nada de lo otro esta abierto
        private readonly RocolaOsLetraPieza[] letraPiezas = new RocolaOsLetraPieza[4];
        private int letraN;
        private int[] letraConTexto; private RocolaOsLetra letraConTextoDe;
        private double letraClock = -1;
        private bool letraVa;                          // lo que decidio Prepara para este cuadro
        private float letraAlfa, letraU = 1f;
        private readonly Tarjeta.Vtx[] letraQuad = new Tarjeta.Vtx[6];
        private StreamWriter letraLog; private bool letraLogProbado; private double letraLogMs = -1e9;
        private int letraHorneados;                    // (la prueba)

        // g381: lo vivo de cada cuadro (antes de abrirlo: aqui se hornea y se sube)
        internal void RocolaOsLetraPrepara()
        {
            letraVa = false;
            if (Environment.GetEnvironmentVariable("ROCOLA_LETRA_DEBUG") != null && (AhoraMs() % 2000) < 40) Console.Error.WriteLine("LETRA prepara cielo=" + Form1.RocolaOsLetraCielo + " stars=" + (stars != null) + " act=" + (RocolaOsLetraSuena.Actual != null) + " ruta=" + (RocolaOs.LaRocola != null ? RocolaOs.LaRocola.RocolaOsLetraRuta() : "-") + " idle=" + idleBlend);
            if (!Form1.RocolaOsLetraCielo || stars == null) { LetraLibera(); return; }
            Form1 r = RocolaOs.LaRocola;
            RocolaOsLetra l = RocolaOsLetraSuena.Actual;
            double ms = AhoraMs();
            double dt = letraClock < 0 ? 0 : Math.Max(0, Math.Min(0.25, (ms - letraClock) / 1000.0)); letraClock = ms;
            string ruta = r != null ? r.RocolaOsLetraRuta() : null;
            if (r == null || l == null || l.Versos.Length == 0 || ruta == null || l.Ruta != ruta || idleBlend <= 0.001f)
            {   // (no se ve: el reposo cerrado, otra cancion o sin letra)
                letraIdle = 0; letraVoz = 0; letraPermite = 1;
                if (idleBlend <= 0.001f || l == null || ruta == null || l.Ruta != ruta) LetraLibera();
                letraTienePos = false; letraPrev = letraCur = -1; letraUltimoConVoz = -1;
                return;
            }
            if (!ReferenceEquals(letraEstrellas, stars) || letraAncho != Width || letraAlto != Height || !ReferenceEquals(letraDe, l))
            {
                LetraLibera(); letraEstrellas = stars; letraAncho = Width; letraAlto = Height; letraDe = l;
                letraTienePos = false; letraPrev = letraCur = -1; letraUltimoConVoz = -1; letraVoz = 0; letraPermite = 1;
            }
            // el reposo abierto (de 0,55 a 1) y que no haya nada abierto encima (se va en 0,15 s)
            letraIdle = RocolaOsLetraModelo.Suave((idleBlend - 0.55) / 0.45);
            float permiteMeta = r.RocolaOsLetraPermite() ? 1f : 0f;
            letraPermite += (permiteMeta - letraPermite) * (float)(1.0 - Math.Exp(-dt / 0.05));
            if (Math.Abs(letraPermite - permiteMeta) < 0.01f) letraPermite = permiteMeta;
            if (permiteMeta == 0f)
            {   // se fue: se deja ir lo que habia sin volver a calcular nada
                letraAlfa = letraIdle * letraVoz * letraPermite;
                letraVa = letraN > 0 && letraAlfa > 0.004f;
                return;
            }
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
            bool habiaVoz = letraVoz > 0.02f;
            int cur, nxt, prev = -1; float u = 1f; bool eraSig = false; float llena = -1f;
            float vozMeta = 1f;
            if (l.Sincronizada && l.Tiempos != null && l.Tiempos.Length == l.Versos.Length)
            {
                int sig; int idx = l.En(pos, out sig);
                bool voz = RocolaOsLetraModelo.HayVoz(l, idx, pos, total);
                if (voz && idx != letraCur)
                {   // el verso cambio: el que se cantaba sale (sube y se apaga) si habia uno a la vista
                    letraEraSiguiente = letraCur >= 0 && RocolaOsLetraModelo.Siguiente(l, letraCur) == idx;
                    letraPrev = habiaVoz && letraCur >= 0 ? letraCur : -1; letraCur = idx; letraCambioMs = ms;
                }
                if (voz) letraUltimoConVoz = idx;
                vozMeta = voz ? 1f : 0f;
                cur = voz ? idx : letraUltimoConVoz;
                if (cur < 0) { letraVoz = 0; letraN = 0; return; }
                nxt = RocolaOsLetraModelo.Siguiente(l, cur);
                u = RocolaOsLetraModelo.Suave((ms - letraCambioMs) / 1000.0 / RocolaOsLetraModelo.CambioS);
                prev = letraPrev; eraSig = letraEraSiguiente;
                llena = voz ? RocolaOsLetraModelo.Llena(l, cur, pos, total) : 1f;
            }
            else
            {   // sin tiempos (la del MP3, o la plainLyrics de LRCLIB): los renglones suben despacio, repartidos por la cancion
                if (!ReferenceEquals(letraConTextoDe, l)) { letraConTexto = RocolaOsLetraModelo.ConTexto(l); letraConTextoDe = l; }
                int[] ne = letraConTexto; if (ne.Length == 0) return;
                double p = RocolaOsLetraModelo.Reparto(ne.Length, pos, total);
                int i = (int)Math.Floor(p); double fr = p - i;
                float uu = RocolaOsLetraModelo.Suave((fr - 0.78) / 0.22);   // (el cambio ocupa el ultimo 22 % de cada renglon: unos 2 s en una cancion de 100 s con 10)
                if (i + 1 < ne.Length && uu > 0f) { prev = ne[i]; cur = ne[i + 1]; nxt = i + 2 < ne.Length ? ne[i + 2] : -1; u = uu; eraSig = true; }
                else { cur = ne[i]; nxt = i + 1 < ne.Length ? ne[i + 1] : -1; u = 1f; }
                letraCur = cur;
            }
            // el fundido de la voz: 0,3 s
            letraVoz += (vozMeta - letraVoz) * (float)(1.0 - Math.Exp(-dt / 0.1));
            if (Math.Abs(letraVoz - vozMeta) < 0.01f) letraVoz = vozMeta;
            if (letraVoz <= 0.004f && vozMeta == 0f) { letraN = 0; return; }
            letraU = u;
            letraN = RocolaOsLetraModelo.Piezas(letraPiezas, prev, cur, nxt, u, llena, 1f, eraSig);
            letraAlfa = letraIdle * letraVoz * letraPermite;
            // lo horneado: lo que se va a pintar (primero lo que se canta), y por adelantado lo que viene: como mucho dos por cuadro
            int hechos = 0;
            for (int k = 0; k < letraN && hechos < 2; k++)
            {
                RocolaOsLetraLinea ln = LetraLinea(letraPiezas[k].Verso, l);
                if (ln == null) continue;
                bool falta = letraPiezas[k].Grande ? ln.TexA == IntPtr.Zero : ln.TexN == IntPtr.Zero;
                if (falta) { LetraHornea(ln, l, letraPiezas[k].Grande); hechos++; }
            }
            if (hechos < 2 && nxt >= 0) { RocolaOsLetraLinea ln = LetraLinea(nxt, l); if (ln != null && ln.TexA == IntPtr.Zero) { LetraHornea(ln, l, true); hechos++; } }
            if (hechos < 2 && cur >= 0) { RocolaOsLetraLinea ln = LetraLinea(cur, l); if (ln != null && ln.TexN == IntPtr.Zero) { LetraHornea(ln, l, false); hechos++; } }
            if (hechos < 2 && (letraCreditoTex == IntPtr.Zero || letraCreditoTexto != LetraCreditoTexto(l))) LetraHorneaCredito(l);
            // las que ya no hacen falta (lejos del verso de ahora) se sueltan
            if (letraLineas.Count > 8) LetraRecorta(cur);
            // (si lo que se pinta no esta horneado, no se pinta: el siguiente cuadro)
            letraVa = letraN > 0 && letraAlfa > 0.004f;
            LetraAnota(ms, pos, cur, llena, u);
        }
        private RocolaOsLetraLinea LetraLinea(int verso, RocolaOsLetra l)
        {
            RocolaOsLetraLinea ln;
            if (letraLineas.TryGetValue(verso, out ln)) return ln;
            if (verso < 0 || verso >= l.Versos.Length || l.Versos[verso].Length == 0) return null;
            ln = new RocolaOsLetraLinea { Verso = verso, De = letraEstrellas };
            letraLineas[verso] = ln;
            return ln;
        }
        private void LetraHornea(RocolaOsLetraLinea ln, RocolaOsLetra l, bool grande)
        {
            GpuStarfield st = letraEstrellas; if (st == null) return;
            string t = l.Versos[ln.Verso];
            float px = grande ? Math.Max(18f, Height * 0.050f) : Math.Max(13f, Height * 0.033f);
            float tx0, tx1;
            SKBitmap bmp = RocolaOsLetraHornea.Dibuja(t, px, (int)(Width * 0.86f), out tx0, out tx1);
            float u, v; IntPtr tex = st.CreateOverlay(bmp, out u, out v);
            int w = bmp.Width, h = bmp.Height; bmp.Dispose();
            letraHorneados++;
            if (grande) { ln.TexA = tex; ln.UA = u; ln.VA = v; ln.WA = w; ln.HA = h; ln.TxA0 = tx0; ln.TxA1 = tx1; }
            else { ln.TexN = tex; ln.UN = u; ln.VN = v; ln.WN = w; ln.HN = h; ln.TxN0 = tx0; ln.TxN1 = tx1; }
        }
        private static string LetraCreditoTexto(RocolaOsLetra l) { return "Letra: " + (l.Proveedor ?? ""); }
        private void LetraHorneaCredito(RocolaOsLetra l)
        {
            GpuStarfield st = letraEstrellas; if (st == null) return;
            string t = LetraCreditoTexto(l);
            if (letraCreditoTex != IntPtr.Zero && ReferenceEquals(letraCreditoDe, st)) Tarjeta.Suelta(letraCreditoTex);
            letraCreditoTex = IntPtr.Zero;
            float px = Math.Max(11f, Height * 0.0175f), t0, t1;
            SKBitmap bmp = RocolaOsLetraHornea.Dibuja(t, px, (int)(Width * 0.4f), out t0, out t1);
            letraCreditoTex = st.CreateOverlay(bmp, out letraCreditoU, out letraCreditoV); letraCreditoW = bmp.Width; letraCreditoH = bmp.Height;
            bmp.Dispose(); letraCreditoDe = st; letraCreditoTexto = t; letraHorneados++;
        }
        private void LetraRecorta(int cur)
        {
            List<int> fuera = null;
            foreach (KeyValuePair<int, RocolaOsLetraLinea> kv in letraLineas)
                if (Math.Abs(kv.Key - cur) > 3) { if (fuera == null) fuera = new List<int>(); fuera.Add(kv.Key); }
            if (fuera == null) return;
            foreach (int k in fuera) { if (ReferenceEquals(letraLineas[k].De, stars)) letraLineas[k].Suelta(); letraLineas.Remove(k); }
        }
        private void LetraLibera()
        {
            if (letraLineas.Count > 0) { foreach (RocolaOsLetraLinea ln in letraLineas.Values) if (ReferenceEquals(ln.De, stars)) ln.Suelta(); letraLineas.Clear(); }
            if (letraCreditoTex != IntPtr.Zero) { if (ReferenceEquals(letraCreditoDe, stars)) Tarjeta.Suelta(letraCreditoTex); letraCreditoTex = IntPtr.Zero; }
            letraCreditoTexto = null; letraVa = false; letraN = 0; letraDe = null; letraEstrellas = null;
        }

        // g380: lo que se ve (dentro del cuadro)
        internal void RocolaOsLetraPinta()
        {
            if (!letraVa || letraN <= 0 || letraAlfa <= 0.004f) return;
            GpuStarfield st = letraEstrellas; if (st == null || !ReferenceEquals(st, stars)) return;
            float W = Width, H = Height, hor = StatusTopY;
            // los dos renglones, centrados, en el cielo libre sobre la raya del horizonte (el panel del reposo empieza en StatusTopY)
            float cy0 = hor - H * 0.115f, paso = H * 0.062f;
            for (int k = 0; k < letraN; k++)
            {
                RocolaOsLetraPieza pz = letraPiezas[k];
                RocolaOsLetraLinea ln;
                if (!letraLineas.TryGetValue(pz.Verso, out ln)) continue;
                IntPtr tex = pz.Grande ? ln.TexA : ln.TexN; if (tex == IntPtr.Zero) continue;
                float u = pz.Grande ? ln.UA : ln.UN, v = pz.Grande ? ln.VA : ln.VN; int bw = pz.Grande ? ln.WA : ln.WN, bh = pz.Grande ? ln.HA : ln.HN;
                float t0 = pz.Grande ? ln.TxA0 : ln.TxN0, t1 = pz.Grande ? ln.TxA1 : ln.TxN1;
                float cy = cy0 + (pz.Desde + (pz.Hasta - pz.Desde) * letraU) * paso;
                float x0 = (float)Math.Round((W - bw) / 2f), y0 = (float)Math.Round(cy - bh / 2f);
                int a = (int)Math.Max(0, Math.Min(255, 255f * pz.Alfa * letraAlfa + 0.5f));
                if (a <= 0) continue;
                if (pz.Grande) LetraPintaGrande(st, tex, x0, y0, bw, bh, u, v, t0, t1, a, pz.Llena);
                else { uint lav = ((uint)a << 24) | RocolaOsLetraModelo.Lavanda; LetraQuad(st, tex, x0, y0, x0 + bw, y0 + bh, 0, 0, u, v, lav, lav); }
            }
            if (letraCreditoTex != IntPtr.Zero && ReferenceEquals(letraCreditoDe, st))
            {
                int a = (int)(190f * letraAlfa + 0.5f);
                float x1 = W - W * 0.014f, y1 = hor - H * 0.004f;
                uint c = ((uint)a << 24) | 0xDDE4FFu;
                LetraQuad(st, letraCreditoTex, x1 - letraCreditoW, y1 - letraCreditoH, x1, y1, 0, 0, letraCreditoU, letraCreditoV, c, c);
            }
        }
        // el renglon que se canta: blanco, y de ambar lo llenado (llena -1: todo blanco; 1: todo ambar). Las dos capas solo se mezclan en los 10 px del borde.
        private void LetraPintaGrande(GpuStarfield st, IntPtr tex, float x0, float y0, int bw, int bh, float u, float v, float t0, float t1, int a, float llena)
        {
            uint blanco = ((uint)a << 24) | RocolaOsLetraModelo.Blanco, ambar = ((uint)a << 24) | RocolaOsLetraModelo.Ambar, ambar0 = RocolaOsLetraModelo.Ambar;
            if (llena <= 0f) { LetraQuad(st, tex, x0, y0, x0 + bw, y0 + bh, 0, 0, u, v, blanco, blanco); return; }
            if (llena >= 1f) { LetraQuad(st, tex, x0, y0, x0 + bw, y0 + bh, 0, 0, u, v, ambar, ambar); return; }
            const float borde = 10f;
            float xs = t0 - borde + llena * ((t1 - t0) + borde);   // (en pixeles del mapa) donde acaba lo lleno del todo
            xs = Math.Max(0f, Math.Min(bw, xs));
            float xe = Math.Min(bw, xs + borde);
            // lo blanco, de xs al final (bajo el borde tambien)
            if (xs < bw) LetraQuad(st, tex, x0 + xs, y0, x0 + bw, y0 + bh, u * xs / bw, 0, u, v, blanco, blanco);
            // lo ambar: lo lleno del todo y el borde (de todo a nada)
            if (xs > 0f) LetraQuad(st, tex, x0, y0, x0 + xs, y0 + bh, 0, 0, u * xs / bw, v, ambar, ambar);
            if (xe > xs) LetraQuad(st, tex, x0 + xs, y0, x0 + xe, y0 + bh, u * xs / bw, 0, u * xe / bw, v, ambar, ambar0);
        }
        private void LetraQuad(GpuStarfield st, IntPtr tex, float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1, uint c0, uint c1)
        {
            Tarjeta.Vtx[] q = letraQuad;
            q[0] = LetraVt(x0, y0, u0, v0, c0); q[1] = LetraVt(x1, y0, u1, v0, c1); q[2] = LetraVt(x1, y1, u1, v1, c1); q[5] = LetraVt(x0, y1, u0, v1, c0);
            q[3] = q[0]; q[4] = q[2];
            st.DrawQuad(tex, q);
        }
        private static Tarjeta.Vtx LetraVt(float x, float y, float u, float v, uint c) { Tarjeta.Vtx r = new Tarjeta.Vtx(); r.X = x - 0.5f; r.Y = y - 0.5f; r.Z = 0; r.Rhw = 1; r.Color = c; r.U = u; r.V = v; return r; }

        // (las pruebas: ROCOLA_LETRA_LOG=archivo: una linea por cuadro como mucho cada 50 ms, con la hora de pared, la posicion, el verso, lo llenado y el cambio)
        private void LetraAnota(double ms, double pos, int cur, float llena, float u)
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
                letraLog.WriteLine(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture) + " pos=" + pos.ToString("0.000", CultureInfo.InvariantCulture)
                    + " verso=" + cur.ToString(CultureInfo.InvariantCulture) + " llena=" + llena.ToString("0.000", CultureInfo.InvariantCulture) + " u=" + u.ToString("0.00", CultureInfo.InvariantCulture)
                    + " voz=" + letraVoz.ToString("0.00", CultureInfo.InvariantCulture) + " idle=" + letraIdle.ToString("0.00", CultureInfo.InvariantCulture) + " piezas=" + letraN.ToString(CultureInfo.InvariantCulture)
                    + " horneados=" + letraHorneados.ToString(CultureInfo.InvariantCulture));
            }
            catch { }
        }
    }
}
