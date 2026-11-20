// EL CIELO VIVO DE ROCOLA OS, NUCLEO: LOS ENCHUFES (PLAN_CIELO_VIVO.md §4; FASE11_cielo-nucleo.md, «Los enchufes»). Lo que los ganchos g120 a
// g139 de Codigo/ llaman, y donde se enchufan los cuatro grupos de despues (sol, luna y estrellas; nubes y clima; fiestas; vidrio) sin tocar las
// mismas lineas. Cada metodo que llama un gancho empieza por «if (!RocolaOs.Activo) return <lo de XP>»: sin ROCOLA_OS, el mismo valor de XP (el
// mismo color, nada dibujado, el mismo sitio); con ROCOLA_OS y lo suyo apagado, igual.
//
// LOS CLIENTES de un enchufe con varios se registran en su [ModuleInitializer] (sin trabajo, como las funciones del andamio) con un ORDEN fijo por
// grupo (RocolaOsCieloOrden): la lista es un arreglo que se cambia entero al registrar (antes del Main) y el cuadro solo la recorre: ni cerrojos
// ni delegados nuevos por cuadro. Un cliente que lanza se apunta (RocolaOs.Falla) y ya no se llama en la sesion (no tumba la rocola ni llena la
// bitacora cada cuadro).
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Rocola
{
    // LAS CAPAS (donde se pinta, en el orden de XP; FASE11_cielo-nucleo.md dice que hay debajo y encima de cada una)
    internal static class RocolaOsCieloCapa
    {
        internal const int TrasNieblas = 0;        // g130: despues del degradado y las nebulosas (y de la imagen intermedia), antes de la aurora
        internal const int TrasAurora = 1;         // g131: despues de la aurora, su raya y su reflejo; antes del grano y de las estrellas
        internal const int ConLaLuna = 2;          // g132: despues de la luna del cielo (o de quien la tome), antes de la estrella y las fugaces
        internal const int TrasFondo = 3;          // g133: despues de todo el cielo (fugaces, vineta y grano), antes del halo de la eleccion y las portadas
        internal const int AntesDeLaInterfaz = 4;  // g134: despues de portadas, cubos y contorno; antes de la textura de la interfaz (barras, pastillas)
        internal const int Escaparate = 5;         // g135: despues del escaparate (la ficha del disco, el mensaje, la lista de la noche)
        internal const int AntesDeLaFicha = 6;     // g137: antes de la ficha rapida del disco (GpuFicha)
        internal const int AntesDelEstado = 7;     // g138: antes del panel del reposo con musica (la franja «AHORA SUENA»)
        internal const int N = 8;
        internal static readonly string[] Nombres = { "tras las nebulosas", "tras la aurora", "con la luna", "tras el fondo", "antes de la interfaz",
            "el escaparate", "antes de la ficha", "antes del estado" };
    }
    // EL ORDEN DE CADA GRUPO dentro de una capa (de menor a mayor se pinta antes; los huecos son para quien llegue despues)
    internal static class RocolaOsCieloOrden
    {
        internal const int Nucleo = 0, Sol = 100, Luna = 200, Estrellas = 300, Nubes = 400, Clima = 500, Fiestas = 600, Vidrio = 700;
    }

    // UN PINTOR DE UNA CAPA (lo hace cada grupo, en su archivo): Pinta corre en la pantalla, dentro del cuadro de la tarjeta, con lo que hay en
    // GpuPanel (su parte en RocolaOs/ ve lo privado de XP). Lo que puede dibujar: FASE11_cielo-nucleo.md, «Lo que se puede dibujar».
    internal abstract class RocolaOsCieloPintor
    {
        internal string Quien = "?";         // la idea o el grupo, para la bitacora («DN2 el sol»)
        internal int Orden;                  // RocolaOsCieloOrden + lo suyo
        internal bool Caido;                 // lanzo: ya no se llama en esta sesion
        internal abstract void Pinta(GpuPanel p, int capa);
        // ANTES DEL CUADRO (gancho g120, fuera de Begin): aqui se hornea y se sube lo que haga falta (texturas: stars.CreateOverlay), una vez por
        // pintor y cuadro aunque pinte en varias capas. Dentro del cuadro (Pinta) solo se dibuja, como en XP (EnsureLuna, EnsureNieblas...).
        internal virtual void Prepara(GpuPanel p) { }
        // (solo con la capa ConLaLuna) si este pintor pinta la luna EN LUGAR de la de XP (DN3: la luna en su camino, o la de XP de dia solo si de
        // verdad esta en el cielo). Se le pregunta cada cuadro antes de la luna; el primero que dice que si la pinta en su Pinta.
        internal virtual bool TomaLaLuna(GpuPanel p) { return false; }
    }
    // UN TINTE DEL CIELO (gancho g121/g122, «los colores del cielo»): cambia el color del degradado a cada altura (las nubes del clima lo agrisan, por
    // ejemplo). Con algun tinte activo el degradado lo pinta Rocola OS aun de noche (con los colores de XP de base).
    internal abstract class RocolaOsCieloTinte
    {
        internal string Quien = "?"; internal int Orden; internal bool Caido;
        internal abstract bool Activo { get; }   // (cada cuadro: rapido)
        // yFrac 0 arriba, 1 abajo; yPx y horizontePx en pixeles; r, g, b de 0 a 255 (en sRGB)
        internal abstract void Tine(float yFrac, float yPx, float horizontePx, ref float r, ref float g, ref float b);
    }
    // EL BRILLO DE CADA ESTRELLA DE XP (gancho g127): un factor de 0 a 1 que multiplica su brillo (DN4: se encienden de una en una; DN6/CL1: las
    // que quedan detras de una nube). actual: el patron de ahora (false: el que se desvanece en el relevo de cada minuto).
    internal abstract class RocolaOsCieloEstrella
    {
        internal string Quien = "?"; internal int Orden; internal bool Caido;
        internal abstract bool Activo { get; }
        internal abstract float Brillo(bool actual, int i, float x, float y, int tamano);
    }
    // LOS MENSAJES DEL ESCAPARATE (gancho g136): se le pasa la lista que XP turna (cada 5 s, con la misma letra y la misma forma) recien armada al
    // entrar la atraccion; pone, quita o cambia los suyos (FI5 la fecha, FI6 el cumpleanos, CL5 el clima).
    internal abstract class RocolaOsCieloMensajero
    {
        internal string Quien = "?"; internal int Orden; internal bool Caido;
        internal abstract void Pon(List<string> mensajes, RocolaOsCieloFoto f);
    }
    // LA COLUMNA DE LA AURORA (gancho g128): al rehacer la aurora (cada muestra de la onda), por columna, con todo lo de esa columna ya calculado
    // (aurCortina, aurNc, aurLc, aurLi, aurSueloC en GpuPanel): FI1 cambia sus colores. DN5 (la raya) va despues de todos.
    internal abstract class RocolaOsCieloAurora
    {
        internal string Quien = "?"; internal int Orden; internal bool Caido;
        internal abstract bool Activo { get; }
        internal abstract void Columna(GpuPanel p, int i, float fx, float nivel, bool suelo, int puntos);
    }
    // EL COLOR DE LA FUGAZ DE FESTEJO (gancho g139; la de XP es dorada, 0x00FFE08A): FI3 la de la fecha, FI6 confeti.
    internal abstract class RocolaOsCieloFugaz
    {
        internal string Quien = "?"; internal int Orden; internal bool Caido;
        internal abstract uint Color(uint deXp, RocolaOsCieloFoto f);
    }

    internal static partial class RocolaOsCielo
    {
        // ---------- los registros (en el [ModuleInitializer] de cada grupo; nunca lanzan) ----------
        private static readonly object cerrojoClientes = new object();
        private static RocolaOsCieloPintor[][] capas = Vacias();
        private static RocolaOsCieloTinte[] tintes = new RocolaOsCieloTinte[0];
        private static RocolaOsCieloEstrella[] estrellas = new RocolaOsCieloEstrella[0];
        private static RocolaOsCieloMensajero[] mensajeros = new RocolaOsCieloMensajero[0];
        private static RocolaOsCieloAurora[] auroras = new RocolaOsCieloAurora[0];
        private static RocolaOsCieloFugaz[] fugaces = new RocolaOsCieloFugaz[0];
        private static RocolaOsCieloPintor[][] Vacias() { RocolaOsCieloPintor[][] c = new RocolaOsCieloPintor[RocolaOsCieloCapa.N][]; for (int i = 0; i < c.Length; i++) c[i] = new RocolaOsCieloPintor[0]; return c; }
        private static T[] Con<T>(T[] l, T x, Func<T, int> orden)
        {
            T[] r = new T[l.Length + 1]; int j = 0; bool puesto = false;
            for (int i = 0; i < l.Length; i++) { if (!puesto && orden(x) < orden(l[i])) { r[j++] = x; puesto = true; } r[j++] = l[i]; }
            if (!puesto) r[j] = x;
            return r;
        }
        private static RocolaOsCieloPintor[] pintores = new RocolaOsCieloPintor[0];   // todos, una vez cada uno (para Prepara)
        internal static void PonPintor(int capa, RocolaOsCieloPintor p)
        {
            if (p == null || capa < 0 || capa >= RocolaOsCieloCapa.N) return;
            lock (cerrojoClientes)
            {
                RocolaOsCieloPintor[][] c = (RocolaOsCieloPintor[][])capas.Clone(); c[capa] = Con(c[capa], p, x => x.Orden); Volatile.Write(ref capas, c);
                if (Array.IndexOf(pintores, p) < 0) Volatile.Write(ref pintores, Con(pintores, p, x => x.Orden));
            }
        }
        // antes de cada cuadro (g120): lo que cada pintor tenga que hornear o subir
        internal static void Prepara(GpuPanel p)
        {
            if (!Va) return;
            RocolaOsCieloPintor[] l = Volatile.Read(ref pintores);
            for (int i = 0; i < l.Length; i++)
            {
                RocolaOsCieloPintor x = l[i];
                if (x.Caido) continue;
                try { x.Prepara(p); } catch (Exception ex) { x.Caido = true; Cayo(x.Quien, "preparar el cuadro", ex); }
            }
        }
        internal static void PonTinte(RocolaOsCieloTinte t) { if (t != null) lock (cerrojoClientes) Volatile.Write(ref tintes, Con(tintes, t, x => x.Orden)); }
        internal static void PonEstrella(RocolaOsCieloEstrella e) { if (e != null) lock (cerrojoClientes) Volatile.Write(ref estrellas, Con(estrellas, e, x => x.Orden)); }
        internal static void PonMensajero(RocolaOsCieloMensajero m) { if (m != null) lock (cerrojoClientes) Volatile.Write(ref mensajeros, Con(mensajeros, m, x => x.Orden)); }
        internal static void PonAurora(RocolaOsCieloAurora a) { if (a != null) lock (cerrojoClientes) Volatile.Write(ref auroras, Con(auroras, a, x => x.Orden)); }
        internal static void PonFugaz(RocolaOsCieloFugaz f) { if (f != null) lock (cerrojoClientes) Volatile.Write(ref fugaces, Con(fugaces, f, x => x.Orden)); }
        // DN4 (estrellas_anochecer) lo pone mientras pinta estrella por estrella (g127): entonces el brillo de todo el campo por la fase (g126) es 1
        internal static volatile bool EstrellasPorEstrella;
        // los tintes suben esto cuando cambia lo suyo (para que la imagen intermedia del cielo se vuelva a pintar)
        internal static int TintesVersion;

        private static void Cayo(string quien, string que, Exception ex) { RocolaOs.Falla(quien, que + " (ya no se llama en esta sesion)", ex); }

        // ---------- lo que hacen los enchufes (en la pantalla) ----------
        internal static void PintaCapa(GpuPanel p, int capa)
        {
            if (!Va) return;
            RocolaOsCieloPintor[] l = Volatile.Read(ref capas)[capa];
            for (int i = 0; i < l.Length; i++)
            {
                RocolaOsCieloPintor x = l[i];
                if (x.Caido) continue;
                try { x.Pinta(p, capa); } catch (Exception ex) { x.Caido = true; Cayo(x.Quien, "pintar " + RocolaOsCieloCapa.Nombres[capa], ex); }
            }
        }
        internal static bool TomaLaLuna(GpuPanel p)
        {
            if (!Va) return false;
            RocolaOsCieloPintor[] l = Volatile.Read(ref capas)[RocolaOsCieloCapa.ConLaLuna];
            for (int i = 0; i < l.Length; i++)
            {
                RocolaOsCieloPintor x = l[i];
                if (x.Caido) continue;
                try { if (x.TomaLaLuna(p)) return true; } catch (Exception ex) { x.Caido = true; Cayo(x.Quien, "tomar la luna", ex); }
            }
            return false;
        }
        internal static bool HayTintes
        {
            get
            {
                RocolaOsCieloTinte[] l = Volatile.Read(ref tintes);
                for (int i = 0; i < l.Length; i++) { if (l[i].Caido) continue; try { if (l[i].Activo) return true; } catch (Exception ex) { l[i].Caido = true; Cayo(l[i].Quien, "tinte", ex); } }
                return false;
            }
        }
        internal static void Tine(float yFrac, float yPx, float horizontePx, ref float r, ref float g, ref float b)
        {
            RocolaOsCieloTinte[] l = Volatile.Read(ref tintes);
            for (int i = 0; i < l.Length; i++)
            {
                RocolaOsCieloTinte t = l[i];
                if (t.Caido) continue;
                try { if (t.Activo) t.Tine(yFrac, yPx, horizontePx, ref r, ref g, ref b); } catch (Exception ex) { t.Caido = true; Cayo(t.Quien, "tinte", ex); }
            }
        }
        internal static float BrilloEstrella(bool actual, int i, float x, float y, int tamano)
        {
            RocolaOsCieloEstrella[] l = Volatile.Read(ref estrellas);
            if (l.Length == 0 || !Va) return 1f;
            float k = 1f;
            for (int j = 0; j < l.Length; j++)
            {
                RocolaOsCieloEstrella e = l[j];
                if (e.Caido) continue;
                try { if (e.Activo) k *= e.Brillo(actual, i, x, y, tamano); } catch (Exception ex) { e.Caido = true; Cayo(e.Quien, "brillo de una estrella", ex); }
            }
            return k < 0f ? 0f : (k > 1f ? 1f : k);
        }
        internal static void Mensajes(List<string> m)
        {
            RocolaOsCieloFoto f = Foto;
            if (f == null || m == null) return;
            RocolaOsCieloMensajero[] l = Volatile.Read(ref mensajeros);
            for (int i = 0; i < l.Length; i++)
            {
                if (l[i].Caido) continue;
                try { l[i].Pon(m, f); } catch (Exception ex) { l[i].Caido = true; Cayo(l[i].Quien, "mensajes del escaparate", ex); }
            }
        }
        internal static void AuroraColumna(GpuPanel p, uint[] colorRaya, float[] brilloRaya, int i, float fx, float nivel, bool suelo, int puntos)
        {
            if (!Va) return;
            RocolaOsCieloAurora[] l = Volatile.Read(ref auroras);
            for (int j = 0; j < l.Length; j++)
            {
                RocolaOsCieloAurora a = l[j];
                if (a.Caido) continue;
                try { if (a.Activo) a.Columna(p, i, fx, nivel, suelo, puntos); } catch (Exception ex) { a.Caido = true; Cayo(a.Quien, "colores de la aurora", ex); }
            }
            RocolaOsCieloDia.Raya(colorRaya, brilloRaya, i, fx);   // DN5, la ultima
        }
        internal static uint ColorFestejo(uint xp)
        {
            RocolaOsCieloFoto f = Foto;
            if (f == null) return xp;
            uint c = xp;
            RocolaOsCieloFugaz[] l = Volatile.Read(ref fugaces);
            for (int i = 0; i < l.Length; i++)
            {
                if (l[i].Caido) continue;
                try { c = l[i].Color(c, f); } catch (Exception ex) { l[i].Caido = true; Cayo(l[i].Quien, "color de la fugaz", ex); }
            }
            return c;
        }
    }

    // ======================================================================== LO QUE LLAMAN LOS GANCHOS
    internal sealed partial class GpuPanel
    {
        private int RocolaOsAuroraFirmaUsada, RocolaOsCieloFirmaMedia;
        // g120 (RenderFrame, antes de abrir el cuadro): la escena de este cuadro, la calma, lo interpolado de la foto
        private void RocolaOsCieloCuadro()
        {
            if (!RocolaOs.Activo || stars == null) return;
            double ms = AhoraMs();
            RocolaOsCielo.TomaReloj(lastFrameWork, ms);   // (pruebas: ROCOLA_CIELO_RELOJ; tambien con las cuatro funciones apagadas, para comparar)
            if (!RocolaOsCielo.Va) return;
            RocolaOsCielo.Cuadro(stars, Width, Height, auCuadro.BaseY - AurLineaAlza, StatusTopY, sceneMode, ShowAlbum, idleBlend, ms);
            RocolaOsCielo.Prepara(this);
        }
        // g123 (CieloMedioBase): si la imagen intermedia del cielo se tiene que volver a pintar por lo de Rocola OS (DN1, el velo, los tintes)
        private bool RocolaOsCieloRepinta()
        {
            if (!RocolaOs.Activo) return false;
            int f = RocolaOsCieloDia.FirmaCielo;
            if (f == RocolaOsCieloFirmaMedia) return false;
            RocolaOsCieloFirmaMedia = f;
            return true;
        }
        // g124 y g125 (las pasadas de la nebulosa y la via lactea): su color y su alfa
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint RocolaOsNiebla(uint argb) { return RocolaOs.Activo && RocolaOsCielo.Va ? RocolaOsCieloDia.Niebla(argb) : argb; }
        // g128 (DrawAurora, al acabar cada columna): sus colores (FI1) y la raya del horizonte (DN5)
        private void RocolaOsAuroraColumna(int i, float fx, float nivel, bool suelo, int puntos)
        {
            if (!RocolaOs.Activo) return;
            RocolaOsCielo.AuroraColumna(this, aurLc, aurLi, i, fx, nivel, suelo, puntos);
        }
        // g129 (DrawAurora): si la aurora se tiene que rehacer aunque no toque muestra de la onda (cambio la luz de la raya o los colores de la fecha)
        private bool RocolaOsAuroraCambia()
        {
            if (!RocolaOs.Activo) return false;
            int f = RocolaOsCieloDia.FirmaAurora;
            if (f == RocolaOsAuroraFirmaUsada) return false;
            RocolaOsAuroraFirmaUsada = f;
            return true;
        }
        // g130, g131, g133, g134, g135, g137 y g138: una capa
        private void RocolaOsCapa(int capa) { if (RocolaOs.Activo) RocolaOsCielo.PintaCapa(this, capa); }
        // g132 (lo que va delante de las estrellas): la luna del cielo (la de XP, o la de quien la tome) y la capa ConLaLuna. Sin ROCOLA_OS, false:
        // XP pinta su luna como siempre.
        private bool RocolaOsLuna()
        {
            if (!RocolaOs.Activo) return false;
            if (!RocolaOsCielo.TomaLaLuna(this)) DibujaLunaCielo();
            RocolaOsCielo.PintaCapa(this, RocolaOsCieloCapa.ConLaLuna);
            return true;
        }
        // (para los grupos) lo de XP que suelen necesitar
        internal float RocolaOsHorizonteXp { get { return auCuadro.BaseY - AurLineaAlza; } }
        internal GpuStarfield RocolaOsEstrellas { get { return stars; } }
    }

    internal sealed partial class GpuStarfield
    {
        private Tarjeta.Vtx[] RocolaOsCieloV;
        private int RocolaOsCieloTris; private bool RocolaOsCieloListo;
        private float RocolaOsCieloDe0, RocolaOsCieloDe1, RocolaOsCieloDe2, RocolaOsCieloDe3, RocolaOsCieloDe4; private uint RocolaOsCieloDeT, RocolaOsCieloDeM, RocolaOsCieloDeB;
        private int RocolaOsCieloDeFirma; private bool RocolaOsCieloDeSuave;
        // g121 (CieloDirecto) y g122 (CieloAmplio): EL CIELO DE DN1 en lugar del degradado de XP (true: ya se pinto). Los dos caminos de XP (el directo
        // y la imagen intermedia del cielo a media resolucion) llaman aqui con lo mismo: el mismo cielo. Sin ROCOLA_OS, de noche o con DN1 apagada (y
        // sin tintes), false: XP pinta el suyo.
        internal bool RocolaOsPintaCielo(float ancho, float h, float midY, uint top, uint mid, uint bot, float extra, bool sinLoTapado)
        {
            if (!RocolaOs.Activo || gpu == null || !ReferenceEquals(this, RocolaOsCielo.Principal) || !RocolaOsCieloDia.Pinta) return false;
            float desde = sinLoTapado ? EstrellasTapadasDesde : 0f;
            int firma = RocolaOsCieloDia.FirmaCielo;
            if (!(RocolaOsCieloListo && RocolaOsCieloDe0 == ancho && RocolaOsCieloDe1 == h && RocolaOsCieloDe2 == midY && RocolaOsCieloDe3 == extra && RocolaOsCieloDe4 == desde
                  && RocolaOsCieloDeT == top && RocolaOsCieloDeM == mid && RocolaOsCieloDeB == bot && RocolaOsCieloDeFirma == firma && RocolaOsCieloDeSuave == CieloSuave))
            {
                if (RocolaOsCieloV == null) RocolaOsCieloV = new Tarjeta.Vtx[64 * 6];
                // el color del genero sin el tinte por la hora de XP (DN1 lo sustituye de dia): las paradas de XP sin TineCielo, con C2
                uint t0 = skyTop, m0 = skyMid, genero = skyBot; ColoresCielo(ref t0, ref m0, ref genero);
                RocolaOsCieloTris = RocolaOsCieloDia.Arma(RocolaOsCieloV, ancho, h, midY, top, mid, bot, genero, extra, CieloSuave, RocolaOsCielo.Escena.Horizonte, desde);
                RocolaOsCieloDe0 = ancho; RocolaOsCieloDe1 = h; RocolaOsCieloDe2 = midY; RocolaOsCieloDe3 = extra; RocolaOsCieloDe4 = desde;
                RocolaOsCieloDeT = top; RocolaOsCieloDeM = mid; RocolaOsCieloDeB = bot; RocolaOsCieloDeFirma = firma; RocolaOsCieloDeSuave = CieloSuave;
                RocolaOsCieloListo = true;
            }
            if (RocolaOsCieloTris <= 0) return false;
            gpu.DrawSkyN(RocolaOsCieloV, RocolaOsCieloTris);
            return true;
        }
        // g126 (BuildStarVerts): el brillo de todo el campo de estrellas por la fase (1 sin ROCOLA_OS, de noche o con DN1 apagada)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal float RocolaOsBrilloEstrellas() { return RocolaOs.Activo && ReferenceEquals(this, RocolaOsCielo.Principal) ? RocolaOsCieloDia.Estrellas : 1f; }
        // g127 (BuildStarVerts, por estrella): el factor de los clientes (1 sin ROCOLA_OS o sin clientes)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal float RocolaOsBrilloEstrella(float[] xs, int i, float x, float y, int tamano)
        { return RocolaOs.Activo && ReferenceEquals(this, RocolaOsCielo.Principal) ? RocolaOsCielo.BrilloEstrella(ReferenceEquals(xs, sx), i, x, y, tamano) : 1f; }
        // g139 (UpdateAndDrawShoots): el color de la fugaz de festejo (el de XP sin ROCOLA_OS o sin clientes)
        internal uint RocolaOsColorFestejo(uint xp) { return RocolaOs.Activo && ReferenceEquals(this, RocolaOsCielo.Principal) ? RocolaOsCielo.ColorFestejo(xp) : xp; }
    }

    public partial class Form1
    {
        // g136 (ArmaMensajes del escaparate): los mensajes de Rocola OS entre los que XP turna
        private void RocolaOsMensajesEscaparate(List<string> mensajes) { if (RocolaOs.Activo) RocolaOsCielo.Mensajes(mensajes); }
    }
}
