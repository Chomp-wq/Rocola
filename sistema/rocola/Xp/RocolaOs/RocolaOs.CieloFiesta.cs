// EL CIELO VIVO DE ROCOLA OS, GRUPO DE LAS FIESTAS (decision del dueno del 20-11-2026, PLAN_CIELO_VIVO.md §6; FASE11_cielo-fiestas.md): FI1 la aurora
// de XP con los colores de la fecha, FI2 la fila baja de adornos sobre la raya del horizonte, FI3 la fugaz dorada de XP con la estela de la fecha,
// FI4 los fuegos del Grito y de Ano Nuevo (RocolaOs.CieloFiestaFuegos.cs), FI5 el papel picado y el mensaje de la fecha en el escaparate y FI6 las
// fechas propias del dueno (cumpleanos y aniversarios de rocola_fechas.txt: globos, confeti y su mensaje). Solo la tarjeta, solo con la funcion
// «fiestas» encendida (sin ella, o con la clave de cada idea en 0, nada de esto pinta ni cambia un color: XP tal cual).
//
// Todo se engancha por los enchufes del nucleo (RocolaOs.CieloEnchufes.cs): la aurora (PonAurora, g128), la fugaz de festejo (PonFugaz, g139), los
// mensajes del escaparate (PonMensajero, g136) y las capas TrasAurora (FI2), TrasFondo (FI4) y Escaparate (FI5, FI6); mas dos ganchos propios para
// la estela de la fugaz (g340, g341: D3D9Star.cs). Todo lo que se mueve va multiplicado por la calma (regla 5: 0,35 mientras alguien elige).
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Rocola
{
    internal static class RocolaOsCieloFiestas
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra()
        {
            RocolaOsCielo.PonAurora(new RocolaOsFiestaAurora());
            RocolaOsCielo.PonFugaz(new RocolaOsFiestaFugaz());
            RocolaOsCielo.PonMensajero(new RocolaOsFiestaMensajero());
            RocolaOsCielo.PonPintor(RocolaOsCieloCapa.TrasAurora, new RocolaOsFiestaHorizonte());
            RocolaOsCielo.PonPintor(RocolaOsCieloCapa.TrasFondo, new RocolaOsFiestaFuegos());
            RocolaOsCielo.PonPintor(RocolaOsCieloCapa.Escaparate, new RocolaOsFiestaEscaparate());
            RocolaOsCieloDia.RayaTinte = RocolaOsFiestaAurora.TineRaya;
        }

        // los colores de una fecha propia (cumpleanos, aniversario): de feria
        internal static readonly uint[] Fiesta = { 0xFFFF4F6Eu, 0xFFFFC93Cu, 0xFF3CC8FFu, 0xFF7CD957u, 0xFFB06CFFu };

        // ---------- lo de hoy, ya mirado (una vez por foto: el latido cambia la foto entera, asi que aqui no se recalcula por cuadro)
        private static RocolaOsCalendarioHoy hoyVisto; private static uint[] coloresVistos = new uint[0];
        // LOS COLORES DE LAS FIESTAS DE HOY, uno detras de otro (sin repetir): las de dos fiestas el mismo dia se juntan
        internal static uint[] Colores(RocolaOsCieloFoto f)
        {
            RocolaOsCalendarioHoy h = f.Hoy;
            if (ReferenceEquals(h, hoyVisto)) return coloresVistos;
            List<uint> l = new List<uint>(8);
            foreach (RocolaOsFiesta x in h.Fiestas) foreach (uint c in x.Colores) if (!l.Contains(c)) l.Add(c);
            coloresVistos = l.ToArray(); hoyVisto = h;
            return coloresVistos;
        }
        // si hay fiesta de calendario hoy y FI1 la quiere ver en la aurora
        internal static bool AuroraActiva(RocolaOsCieloFoto f) { return f != null && f.FI1 && f.Hoy.Fiestas.Length > 0 && Colores(f).Length > 0; }
        internal static bool HayPropias(RocolaOsCieloFoto f) { return f != null && f.FI6 && f.Hoy.Propias.Length > 0; }

        // EL COLOR DE UNA COLUMNA (de 0 a 1 a lo ancho): los colores de la fecha en bandas parejas, con un paso suave entre una y otra; devuelve el
        // color con su canal mas fuerte en 255 (quien lo usa lo escala a la luz que ya tenia el de XP)
        internal static void Banda(uint[] c, float fx, out float r, out float g, out float b)
        {
            int n = c.Length;
            if (n == 1) { Norm(c[0], out r, out g, out b); return; }
            float p = fx * n; int i = (int)p; if (i >= n) i = n - 1; if (i < 0) i = 0;
            float f = p - i, w;
            int j = i;
            if (f > 0.78f && i + 1 < n) { w = Suave((f - 0.78f) / 0.44f); j = i + 1; }
            else if (f < 0.22f && i > 0) { w = Suave((0.22f - f) / 0.44f); j = i - 1; }
            else w = 0f;
            float r0, g0, b0, r1, g1, b1;
            Norm(c[i], out r0, out g0, out b0); Norm(c[j], out r1, out g1, out b1);
            r = r0 + (r1 - r0) * w; g = g0 + (g1 - g0) * w; b = b0 + (b1 - b0) * w;
        }
        private static float Suave(float x) { if (x < 0f) x = 0f; else if (x > 1f) x = 1f; return x * x * (3f - 2f * x); }
        private static void Norm(uint c, out float r, out float g, out float b)
        {
            r = (c >> 16) & 255; g = (c >> 8) & 255; b = c & 255;
            float m = Math.Max(r, Math.Max(g, b));
            if (m < 1f) { r = g = b = 255f; return; }
            float k = 255f / m; r *= k; g *= k; b *= k;
        }
        private static float Lin(float c) { c /= 255f; return c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4); }
        internal static float LumLineal(float r, float g, float b) { return 0.2126f * Lin(r) + 0.7152f * Lin(g) + 0.0722f * Lin(b); }

        // ---------- la ultima fecha vista por la aurora: si cambia (o FI1 se apaga) la aurora se rehace (FirmaAuroraExtra, g129)
        private static int claveAurora = -1;
        internal static void AlCuadro()
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            int k = 0;
            if (AuroraActiva(f)) { uint[] c = Colores(f); unchecked { k = 17; for (int i = 0; i < c.Length; i++) k = k * 31 + (int)c[i]; } }
            if (k != claveAurora) { claveAurora = k; unchecked { RocolaOsCieloDia.FirmaAuroraExtra += 4099; } }
        }

        // ---------- cosas pequenas para dibujar
        internal static uint Hash(int a, int b)
        { unchecked { uint h = (uint)(a * 374761393 + b * 668265263 + 1442695041); h = (h ^ (h >> 13)) * 1274126177u; return h ^ (h >> 16); } }
        internal static float Rnd(int a, int b) { return (Hash(a, b) & 0xFFFFFF) / 16777216f; }
        internal static float Frac(double x) { return (float)(x - Math.Floor(x)); }
    }

    // ======================================================================== FI1: LA AURORA CON LOS COLORES DE LA FECHA (g128)
    // Cambia solo el COLOR de cada columna de la aurora de XP (la cortina, el filo, el suelo y el pie): el de la fecha que toca a esa columna (bandas
    // de izquierda a derecha: el tricolor es una bandera), con la luz que XP ya le dio (su canal mas fuerte): la forma, el movimiento y el brillo de
    // la aurora son los de XP, que sigue a la musica. Sin fiesta hoy, la aurora es la de XP.
    // LA RAYA DEL HORIZONTE DE DIA (la «nota» del nucleo: DN5 pisaba el color de la raya de FI1): de noche la raya es la de la aurora, o sea con los
    // colores de la fecha; de dia DN5 pone la suya (el color de la hora), y aqui se decide que lleve la fecha tambien (60 % del color de la fecha,
    // 40 % el de la hora) con la MISMA luminancia que le dio DN5: la regla 3 se mide con esa luz y no cambia.
    internal sealed class RocolaOsFiestaAurora : RocolaOsCieloAurora
    {
        internal RocolaOsFiestaAurora() { Quien = "FI1 la aurora de la fecha"; Orden = RocolaOsCieloOrden.Fiestas; }
        internal override bool Activo { get { return RocolaOsCieloFiestas.AuroraActiva(RocolaOsCielo.Foto); } }
        internal override void Columna(GpuPanel p, int i, float fx, float nivel, bool suelo, int puntos)
        { p.RocolaOsFiestaColumna(RocolaOsCieloFiestas.Colores(RocolaOsCielo.Foto), i, fx, nivel, suelo, puntos); }

        internal static void TineRaya(float fx, ref float r, ref float g, ref float b)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (!RocolaOsCieloFiestas.AuroraActiva(f)) return;
            float fr, fg, fb; RocolaOsCieloFiestas.Banda(RocolaOsCieloFiestas.Colores(f), fx, out fr, out fg, out fb);
            const float m = 0.6f;
            float nr = r + (fr - r) * m, ng = g + (fg - g) * m, nb = b + (fb - b) * m;
            // la misma luminancia relativa (la de la regla 3, en lineal) que le dio DN5: se busca el factor por biseccion (cada canal acotado a 255)
            float l0 = 0.9f * RocolaOsCieloFiestas.LumLineal(r, g, b), lo = 0f, hi = 4f;   // (con un 10 % de margen: la raya del reposo de dia de las madres midio 0,172)
            for (int it = 0; it < 14; it++)
            {
                float k = 0.5f * (lo + hi);
                if (RocolaOsCieloFiestas.LumLineal(Math.Min(255f, nr * k), Math.Min(255f, ng * k), Math.Min(255f, nb * k)) > l0) hi = k; else lo = k;
            }
            nr = Math.Min(255f, nr * lo); ng = Math.Min(255f, ng * lo); nb = Math.Min(255f, nb * lo);
            r = nr; g = ng; b = nb;
        }
    }

    // ======================================================================== FI3 y FI6: LA FUGAZ DE FESTEJO (g139, g340, g341)
    // La fugaz dorada de XP (la de una moneda o una cancion puesta; no se anaden fugaces) sale con los colores de la fecha: la cabeza de uno, y la
    // estela se abre hacia otros dos (en la fiesta de dos colores, el segundo; en el tricolor, el verde-blanco-rojo). Con un cumpleanos propio y sin
    // fiesta de calendario, los colores de feria (el confeti, FI6). El color lleva en su byte de arriba (que XP no lee) a que estela corresponde.
    internal sealed class RocolaOsFiestaFugaz : RocolaOsCieloFugaz
    {
        internal RocolaOsFiestaFugaz() { Quien = "FI3 la fugaz de la fecha"; Orden = RocolaOsCieloOrden.Fiestas; }
        private static readonly uint[] colaA = new uint[8], colaB = new uint[8];
        private static int contador;
        internal override uint Color(uint deXp, RocolaOsCieloFoto f)
        {
            uint[] pal = null;
            if (f.FI3 && f.Hoy.Fiestas.Length > 0) pal = RocolaOsCieloFiestas.Colores(f);
            else if (RocolaOsCieloFiestas.HayPropias(f)) pal = RocolaOsCieloFiestas.Fiesta;
            if (pal == null || pal.Length == 0) return deXp;
            int n = pal.Length, k = contador++, slot = k & 7;
            uint a = pal[k % n], b = pal[(k + 1) % n], c = pal[(k + 2) % n];
            colaA[slot] = b; colaB[slot] = n >= 3 ? c : b;
            return ((uint)(slot + 1) << 24) | (a & 0x00FFFFFFu);
        }
        // el color de la punta de la estela (cual 0: un lado, 1: el otro); sin marca, el mismo de la cabeza (lo de XP)
        internal static uint Cola(uint col, uint rgb, int cual)
        {
            int m = (int)(col >> 24);
            if (m < 1 || m > 8) return rgb;
            return (cual == 0 ? colaA : colaB)[m - 1] & 0x00FFFFFFu;
        }
    }

    // ======================================================================== FI5 y FI6: EL MENSAJE DE LA FECHA EN EL ESCAPARATE (g136)
    // Entre los mensajes que XP turna cada 5 s debajo de la ficha: el de la fecha (el de fabrica o el de rocola_fechas.txt) en el segundo lugar, y el
    // cumpleanos propio («¡Feliz cumpleaños, NOMBRE!») en el primero. Con la misma letra y la misma forma de los de XP (los hornea XP).
    internal sealed class RocolaOsFiestaMensajero : RocolaOsCieloMensajero
    {
        internal RocolaOsFiestaMensajero() { Quien = "FI5 y FI6 el mensaje de la fecha"; Orden = RocolaOsCieloOrden.Fiestas; }
        internal override void Pon(List<string> m, RocolaOsCieloFoto f)
        {
            if (f.FI5)
            {
                int pos = Math.Min(1, m.Count);
                foreach (string s in f.Hoy.Mensajes) if (!string.IsNullOrEmpty(s) && !m.Contains(s)) m.Insert(pos++, s);
            }
            if (f.FI6)
            {
                int pos = 0;
                foreach (RocolaOsFechaPropia x in f.Hoy.Propias) { string s = x.Mensaje; if (!m.Contains(s)) m.Insert(pos++, s); }
            }
            if (f.FI5 || f.FI6) RocolaOs.Apunta("cielo", "escaparate: " + m.Count + " mensajes (los de la fecha: " + (f.FI5 ? f.Hoy.Mensajes.Length : 0) + (f.FI6 ? ", propios: " + f.Hoy.Propias.Length : "") + ")");
        }
    }

    // ======================================================================== LO QUE NECESITAN DE LA TARJETA DE XP (lo privado de GpuPanel y GpuStarfield)
    internal sealed partial class GpuPanel
    {
        // FI1 (g128): la columna i de la aurora con el color de la fecha y la luz que XP le dio (su canal mas fuerte)
        internal void RocolaOsFiestaColumna(uint[] colores, int i, float fx, float nivel, bool suelo, int puntos)
        {
            if (aurCortina == null || aurNc == null || aurLc == null || colores == null || colores.Length == 0) return;
            float r, g, b; RocolaOsCieloFiestas.Banda(colores, fx, out r, out g, out b);
            for (int k = 0; k <= AurFilas; k++) { int ix = k * puntos + i; aurCortina[ix] = RocolaOsFiestaConLuz(r, g, b, aurCortina[ix]); }
            aurNc[i] = aurCortina[AurFilas * puntos + i];
            uint pie = RocolaOsFiestaConLuz(r, g, b, aurLc[i]);
            aurLc[i] = pie;
            if (suelo && aurSueloC != null) AurColoresSuelo(aurSueloC, puntos, i, pie, nivel);
        }
        private static uint RocolaOsFiestaConLuz(float r, float g, float b, uint deXp)
        {
            float m = Math.Max((deXp >> 16) & 255, Math.Max((deXp >> 8) & 255, deXp & 255)) / 255f;
            return ((uint)(int)(r * m + 0.5f) << 16) | ((uint)(int)(g * m + 0.5f) << 8) | (uint)(int)(b * m + 0.5f);
        }
        // FI4, FI5 y FI6: lo que ensena el escaparate de XP ahora (la ficha de las canciones: «vitrina»; o la lista de lo mas pedido, «top»), y que
        // parte de la ficha se ve (la lista la cubre con su mezcla): 0 si no hay escaparate en este cuadro
        internal bool RocolaOsEscaparate(out bool vitrina, out bool lista, out float fichaVisible)
        {
            GpuAtraccion a = atrCuadro;
            vitrina = a.Has && a.Vitrina; lista = a.Has && a.Top;
            fichaVisible = a.Has ? Math.Max(0f, Math.Min(1f, a.UiAlfa)) : 0f;
            return a.Has;
        }
    }

    internal sealed partial class GpuStarfield
    {
        // g340 y g341 (UpdateAndDrawShoots): el color de la punta de la estela de la fugaz (XP: el de la cabeza)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint RocolaOsColaFugaz(uint col, uint rgb, int cual) { return RocolaOs.Activo ? RocolaOsFiestaFugaz.Cola(col, rgb, cual) : rgb; }
    }
}
