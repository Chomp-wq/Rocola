// EL MODO ATRACCION DE LA PANTALLA VIVA (tanda 4, 24-09-2026): el escaparate y lo mas pedido esta noche. Solo con
// tarjeta grafica.
//
// La atraccion sale cuando la rocola se queda sola: nada sonando, nada en la cola y 40 segundos sin que nadie la
// toque. Estas dos ideas la vuelven un escaparate que invita a acercarse:
//
//  - EL ESCAPARATE (escaparate=). El cubo gigante se va a la izquierda y, en vez de girar, se mece: asi la portada se
//    ve de frente. A su derecha sale el disco que esta ensenando: su nombre en grande (hasta dos renglones), el
//    artista, la pastilla del genero, cuantas canciones trae y cuantas veces se han pedido. Cada disco se queda unos
//    8 segundos (antes, casi 4) para que de tiempo a leerlo, y el texto se funde a la vez que la portada. Debajo se
//    turna un mensaje cada 5 segundos: como elegir, lo que da una moneda, lo mas pedido esta noche y como buscar.
//  - LO MAS PEDIDO ESTA NOCHE (top_noche=). Cada 30 segundos, y durante 15, el cubo se hace chico y se va a la
//    esquina, y sale la lista de las cinco canciones mas pedidas desde las 8 de la manana: entran una a una desde la
//    izquierda, con su portada, y una barra que crece dice cuantas veces se pidio cada una. Hace falta que se hayan
//    pedido al menos tres canciones distintas; si no, la lista no sale.
//
// La cuenta de la noche se lleva al entrar una cancion a la cola (RecordPlay) y se guarda en rocola_noche.txt, en la
// carpeta de datos, cada minuto si cambio y al cerrar: si la rocola se reinicia a media noche no se pierde. A las 8 de
// la manana empieza otra noche y se cuenta desde cero. Solo cuentan las canciones, no los videos.
//
// Sin tarjeta grafica no hay nada de esto: ni se lleva la cuenta ni cambia la atraccion (el dibujo por procesador
// esta congelado). Todo lo que se hornea (la ficha del disco, los mensajes, la lista) se hace en el reloj de la
// pantalla, una cosa por vuelta y solo cuando cambia; en cada cuadro solo se calculan posiciones y transparencias.
using System;
using System.Collections.Generic;
using System.Drawing; using Rocola.Dibujo2D;   // EN LINUX: GDI+ de la transicion (Plataforma/Dibujo2D.cs) hasta que este archivo se traduzca a SkiaSharp directo
using System.Globalization;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
        // Existen en los dos perfiles porque la configuracion es la misma; sin tarjeta grafica no se usan.
        private bool escaparate = true;   // "escaparate="
        private bool topNoche = true;     // "top_noche="

        // ---------------- LOS ENGANCHES (sin tarjeta grafica no hacen nada) ----------------
        // Una cancion entro a la cola (RecordPlay): cuenta para lo mas pedido esta noche.
        private void CuentaNoche(PlayItem it)
        {
#if ROCOLA_GPU
            CuentaNocheGpu(it, DateTime.Now);
#endif
        }
        // La cuenta de la noche, al disco si cambio: en segundo plano cada minuto y de una vez al cerrar.
        private void GuardaNoche(bool enFondo)
        {
#if ROCOLA_GPU
            GuardaNocheGpu(enFondo);
#endif
        }
        // Entra la atraccion, con los discos del cubo ya elegidos: se apunta lo que va a ensenar.
        private void EscaparateEntra()
        {
#if ROCOLA_GPU
            EscaparateEntraGpu();
#endif
        }
        // Se va la atraccion: lo horneado para ella se suelta (la lista a pantalla completa pesa).
        private void EscaparateSale()
        {
#if ROCOLA_GPU
            EscaparateSaleGpu();
#endif
        }
    }

#if ROCOLA_GPU
    // Lo que la tarjeta necesita para dibujar la atraccion de esta tanda en un cuadro.
    internal struct GpuAtraccion
    {
        public bool Has;
        public float UiAlfa;   // los dos textos de siempre (van en la textura de la interfaz): se apagan para la lista
        // EL ESCAPARATE
        public bool Vitrina; public float VitrinaA;
        public Bitmap Ficha, FichaB; public int FichaVer, FichaBVer; public float FichaAlfa, FichaBAlfa, FichaX, FichaY, FichaBY;
        public Bitmap Msgs; public int MsgsVer, MsgFila; public float MsgA, MsgDy, MsgX, MsgY, MsgH;
        public float BrilloX, BrilloY, BrilloRx, BrilloRy; public uint BrilloArgb;
        // LO MAS PEDIDO ESTA NOCHE
        public bool Top; public Bitmap TopBmp; public int TopVer; public float TopX, TopY, CabH, FilasY, FilaH, CabA;
        public int Filas; public float[] FilaA, FilaDx, Barra, BarraLargo;
        public float BarraX, BarraW, BarraH, BarraDy;   // en la fila: donde empieza la barra, su largo entero, su alto y su altura
    }

    // Un disco del escaparate, tal como se ensena (se apunta al entrar la atraccion: textos, no el disco).
    internal sealed class FichaVitrina { public string Nombre = "", Artista = "", Genero = ""; public Color GeneroColor; public int Canciones, Videos, Pedidas; }
    // Una fila de lo mas pedido esta noche.
    internal sealed class FilaNoche { public string Ruta, Titulo = "", Artista = "", Portada; public int Veces; public Color Color; }

    public partial class Form1
    {
        // ======================================================================================================
        //  EL RELOJ DE LA ATRACCION
        // ======================================================================================================
        // Cada disco del escaparate: 8,6 de la fase (unos 8 s; el ciclo de siempre es 4,2) y los ultimos 1,3 de
        // fundido. La fase es la misma que usa el cubo, asi la ficha cambia a la vez que la portada.
        internal const double VitrinaCiclo = 8.6, VitrinaQuieto = 7.3;
        internal const int VitrinaMs = 30000, TopMs = 15000, PaginaMs = 900;   // 30 s de escaparate, 15 de lista; 0,9 s de paso
        internal const int MensajeMs = 5000, MensajeFundeMs = 400;             // un mensaje cada 5 s, con 0,4 s de fundido
        internal const float MensajeSube = 10f;                                // entra desde abajo y se va hacia arriba
        internal const int FilaEntraMs = 360, FilaEscalonMs = 170, BarraCreceMs = 900;
        internal const float FilaCorre = 44f;                                  // cada fila entra desde la izquierda
        internal const float MeceRad = .28f; internal const int MeceMs = 9000; // el cubo se mece +-16 grados, una vez cada 9 s
        internal const int EntraFichaMs = 700;                                 // la ficha aparece mientras el cubo llega
        internal const float FichaSube = 10f;                                  // al cambiar de disco: la vieja sube y se va, la nueva sube y llega

        // Cuando entro la atraccion, en el reloj de la tarjeta, y si esta viva. (El reloj empieza en cero al arrancar: un
        // valor 'imposible' no sirve de marca.)
        private double atrDesde; private bool atrVivo;

        internal static float Suave(float x) { if (x <= 0f) return 0f; if (x >= 1f) return 1f; return x * x * (3f - 2f * x); }
        internal static float Frena(float x) { if (x <= 0f) return 0f; if (x >= 1f) return 1f; float q = 1f - x; return 1f - q * q * q; }

        // 0 = el escaparate (o la atraccion de siempre), 1 = la lista de lo mas pedido; el paso, suavizado. Sin lista, 0.
        internal static float MezclaTop(double ms, bool hayTop)
        {
            if (!hayTop || ms < VitrinaMs || double.IsNaN(ms)) return 0f;
            double t = ms % (VitrinaMs + TopMs);
            if (t < VitrinaMs) return 0f;
            if (t < VitrinaMs + PaginaMs) return Suave((float)((t - VitrinaMs) / PaginaMs));
            if (t > VitrinaMs + TopMs - PaginaMs) return Suave((float)((VitrinaMs + TopMs - t) / PaginaMs));
            return 1f;
        }
        // Cuanto lleva la lista en pantalla (ms desde que empezo a entrar); negativo si no toca.
        internal static double MsEnTop(double ms)
        {
            if (ms < VitrinaMs || double.IsNaN(ms)) return -1;
            double t = ms % (VitrinaMs + TopMs);
            return t < VitrinaMs ? -1 : t - VitrinaMs;
        }
        // La fila i de la lista: entra desde la izquierda frenando, una detras de otra, y su barra crece despues.
        internal static void FilaTop(double tt, int i, out float alfa, out float dx, out float barra)
        {
            double desde = PaginaMs * .90 + i * FilaEscalonMs;   // con el cubo ya en su esquina: no se cruzan
            float q = Frena((float)((tt - desde) / FilaEntraMs));
            alfa = q; dx = -FilaCorre * (1f - q);
            barra = Frena((float)((tt - desde - 120) / BarraCreceMs));
        }
        // La cabecera de la lista, un poco antes que las filas.
        internal static float CabeceraTop(double tt) { return Frena((float)((tt - PaginaMs * .80) / 300.0)); }
        // Al irse la lista, se apaga en la primera mitad del paso (antes de que el cubo vuelva a pasar por encima).
        internal static float SalePagina(float m) { return Suave((m - .5f) * 2f); }
        // El escaparate, igual pero al reves: se va enseguida y vuelve cuando el cubo ya casi llego a su sitio.
        internal static float VitrinaConTop(float m) { return 1f - Suave(m * 6f); }
        // El cambio de disco, en dos tiempos: primero se va la ficha vieja y luego llega la nueva (nunca las dos encima).
        internal static void FundeFichas(float f, out float viejaA, out float viejaDy, out float nuevaA, out float nuevaDy)
        {
            float a = Suave(f * 2f), b = Suave(f * 2f - 1f);
            viejaA = 1f - a; viejaDy = -FichaSube * a;
            nuevaA = b; nuevaDy = FichaSube * (1f - b);
        }
        // El mensaje que toca y como va: entra desde abajo, se queda y se va hacia arriba, uno a la vez.
        internal static void FaseMensaje(double ms, int n, out int k, out float alfa, out float dy)
        {
            k = 0; alfa = 0f; dy = 0f;
            if (n <= 0 || ms < 0 || double.IsNaN(ms)) return;
            k = (int)(ms / MensajeMs) % n;
            double s = ms % MensajeMs;
            if (s < MensajeFundeMs) { float e = Frena((float)(s / MensajeFundeMs)); alfa = e; dy = MensajeSube * (1f - e); }
            else if (s > MensajeMs - MensajeFundeMs) { float e = Suave((float)((s - (MensajeMs - MensajeFundeMs)) / MensajeFundeMs)); alfa = 1f - e; dy = -MensajeSube * e; }
            else alfa = 1f;
        }
        // El cubo del escaparate se mece en vez de girar: la portada casi siempre de frente.
        internal static float MeceCubo(double ms) { return (float)(MeceRad * Math.Sin(ms * 2.0 * Math.PI / MeceMs)); }

        // El ciclo de portadas del cubo: la misma cuenta que AttractView.GetCoverPair, con cuanto dura cada una.
        internal static void CicloPortadas(double fase, int m, double cyc, double hold, out int idx, out float f)
        {
            idx = 0; f = 0f;
            if (m <= 0) return;
            double total = cyc * m;
            double pos = fase - Math.Floor(fase / total) * total;
            idx = (int)(pos / cyc) % m;
            double within = pos - idx * cyc;
            if (within > hold) f = (float)((within - hold) / (cyc - hold));
            if (f < 0f) f = 0f; else if (f > 1f) f = 1f;
        }
        internal static void CicloVitrina(double fase, int m, out int idx, out float f) { CicloPortadas(fase, m, VitrinaCiclo, VitrinaQuieto, out idx, out f); }

        // ======================================================================================================
        //  DONDE VA CADA COSA (en la pantalla)
        // ======================================================================================================
        // El cubo del escaparate: a la izquierda y un poco por encima del centro.
        internal static RectangleF CuboEscaparate(int w, int h)
        {
            int side = (int)Math.Min(h * .50f, w * .36f); if (side < 80) side = 80;
            return new RectangleF((int)(w * .08f), (int)((h - side) / 2f - h * .04f), side, side);
        }
        // La columna del texto, a la derecha del cubo: la ficha arriba y el mensaje a la altura del pie del cubo.
        internal static RectangleF ColumnaEscaparate(int w, int h)
        {
            RectangleF c = CuboEscaparate(w, h);
            int x0 = (int)(c.Right + w * .06f), x1 = (int)(w - w * .05f);
            return new RectangleF(x0, c.Y, Math.Max(40, x1 - x0), c.Height);
        }
        internal static int AltoMensaje(int h) { return (int)Math.Max(14f, h * .029f) + 12; }
        // Con la lista, el cubo se hace chico y se va a la esquina de arriba a la derecha.
        internal static RectangleF CuboEsquina(int w, int h)
        {
            int side = (int)Math.Max(60f, Math.Min(w, h) * .20f);
            return new RectangleF((int)(w - w * .06f - side), (int)(h * .05f), side, side);
        }
        // La pagina de lo mas pedido: la cabecera al lado del cubo chico y las filas debajo, a todo lo ancho.
        internal static void GeometriaTop(int w, int h, out RectangleF pagina, out float cabW, out float cabH, out float filasY, out float filaH)
        {
            RectangleF cubo = CuboEsquina(w, h);
            int x0 = (int)(w * .07f), x1 = (int)(w * .93f), y0 = (int)cubo.Y;
            cabW = Math.Max(40f, cubo.X - x0 - w * .02f); cabH = cubo.Height;
            filasY = (int)(cubo.Bottom + h * .025f) - y0;
            filaH = (int)Math.Min(h * .125f, (h * .93f - (y0 + filasY)) / TopFilas);
            if (filaH < 30f) filaH = 30f;
            pagina = new RectangleF(x0, y0, x1 - x0, filasY + filaH * TopFilas);
        }

        // ======================================================================================================
        //  LA CUENTA DE LA NOCHE
        // ======================================================================================================
        internal const int NocheHora = 8;       // la noche va de las 8 de la manana a las 8 de la manana siguiente
        internal const int NocheTope = 400;     // canciones distintas como mucho: ninguna noche llega, y el archivo no crece sin fin
        internal const int TopFilas = 5, TopMinimo = 3;
        private readonly Dictionary<string, int> nocheCuenta = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> nocheOrden = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private long nocheSeq; private string nocheClave; private bool nocheLeida, nocheSucia;
        private long nocheGuardaVer; private readonly object nocheGuardaLock = new object();

        // La noche a la que pertenece un momento: hasta las 8 de la manana, todavia es la de ayer.
        internal static string ClaveNoche(DateTime t) { return t.AddHours(-NocheHora).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        private string ArchivoNoche() { return RutaDatos("rocola_noche.txt"); }

        // Si ya es otra noche (dieron las 8 de la manana), se empieza de cero.
        private void CambiaDeNoche(DateTime ahora)
        {
            LeeNocheSiFalta(ahora);
            string k = ClaveNoche(ahora);
            if (k == nocheClave) return;
            nocheClave = k; nocheCuenta.Clear(); nocheOrden.Clear(); nocheSucia = true;
        }
        private void CuentaNocheGpu(PlayItem it, DateTime ahora)
        {
            if (it == null || it.IsVideo || it.Song == null || string.IsNullOrEmpty(it.Song.Path)) return;
            CambiaDeNoche(ahora);
            string p = it.Song.Path;
            int n; nocheCuenta.TryGetValue(p, out n);
            if (n == 0 && nocheCuenta.Count >= NocheTope) return;
            nocheCuenta[p] = n + 1; nocheOrden[p] = ++nocheSeq; nocheSucia = true;
        }
        // rocola_noche.txt: "noche=AAAA-MM-DD" y luego "veces|ruta", de la pedida hace mas tiempo a la ultima. Si es de
        // otra noche, o no se puede leer, se empieza de cero: es solo la cuenta de hoy. Se lee una vez, al hacer falta.
        private void LeeNocheSiFalta(DateTime ahora)
        {
            if (nocheLeida) return;
            nocheLeida = true;
            nocheClave = ClaveNoche(ahora);
            try
            {
                string[] ls = LeeLineasSeguro(ArchivoNoche());
                if (ls == null || ls.Length == 0 || ls[0].Trim() != "noche=" + nocheClave) return;
                for (int i = 1; i < ls.Length && nocheCuenta.Count < NocheTope; i++)
                {
                    string s = ls[i]; int bar = s.IndexOf('|');
                    if (bar <= 0 || bar >= s.Length - 1) continue;
                    int n;
                    if (!int.TryParse(s.Substring(0, bar), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n <= 0) continue;
                    string p = s.Substring(bar + 1);
                    nocheCuenta[p] = n; nocheOrden[p] = ++nocheSeq;
                }
            }
            catch { nocheCuenta.Clear(); nocheOrden.Clear(); }
        }
        private void GuardaNocheGpu(bool enFondo)
        {
            if (!nocheLeida || !nocheSucia) return;
            nocheSucia = false;
            string texto = TextoNoche(), ruta = ArchivoNoche();
            long ver = System.Threading.Interlocked.Increment(ref nocheGuardaVer);
            System.Action guarda = delegate
            {
                try
                {
                    lock (nocheGuardaLock)
                    {
                        if (ver != System.Threading.Interlocked.Read(ref nocheGuardaVer)) return;   // ya hay una mas nueva
                        EscribeSeguro(ruta, texto);
                    }
                }
                catch { }
            };
            if (enFondo) GuardaDetras(guarda); else guarda();   // detras: con la cuenta de pendientes (el cierre espera)
        }
        // Se arma en el hilo de la interfaz (los diccionarios son suyos) y se escribe donde toque.
        private string TextoNoche()
        {
            List<string> ks = new List<string>(nocheCuenta.Keys);
            ks.Sort(delegate (string a, string b)
            {
                long x, y; nocheOrden.TryGetValue(a, out x); nocheOrden.TryGetValue(b, out y);
                return x.CompareTo(y);
            });
            StringBuilder sb = new StringBuilder(32 + ks.Count * 96);
            sb.Append("noche=").Append(nocheClave).Append("\r\n");
            for (int i = 0; i < ks.Count; i++) sb.Append(nocheCuenta[ks[i]].ToString(CultureInfo.InvariantCulture)).Append('|').Append(ks[i]).Append("\r\n");
            return sb.ToString();
        }
        // Las mas pedidas: mas veces primero y, si empatan, la pedida mas tarde.
        internal static List<string> MasPedidasNoche(Dictionary<string, int> cuenta, Dictionary<string, long> orden, int max)
        {
            List<string> ks = new List<string>(cuenta.Keys);
            ks.Sort(delegate (string a, string b)
            {
                int ca = cuenta[a], cb = cuenta[b];
                if (ca != cb) return cb.CompareTo(ca);
                long oa, ob; orden.TryGetValue(a, out oa); orden.TryGetValue(b, out ob);
                return ob.CompareTo(oa);
            });
            if (ks.Count > max) ks.RemoveRange(max, ks.Count - max);
            return ks;
        }

        // ======================================================================================================
        //  AL ENTRAR Y AL SALIR
        // ======================================================================================================
        private readonly List<FichaVitrina> vitrina = new List<FichaVitrina>();
        private readonly List<FilaNoche> topFilas = new List<FilaNoche>();
        private readonly List<string> mensajes = new List<string>();
        private int topTotal;   // cuantas canciones se han pedido esta noche (la cabecera lo dice)

        // Si la atraccion la compone esta tanda: la tarjeta dibujando, el escaparate encendido y discos que ensenar.
        private bool EscaparateActivo() { return escaparate && gpuEnabled && gpuPanel != null && vitrina.Count > 0; }
        // Si hay lista de lo mas pedido que ensenar (y ya esta horneada: nunca un hueco en su lugar).
        private bool HayTopNoche() { return topNoche && gpuEnabled && gpuPanel != null && topFilas.Count >= TopMinimo && topBmp != null; }

        private void EscaparateEntraGpu()
        {
            Arreglos();
            atrDesde = GpuPanel.AhoraMs(); atrVivo = true;
            vitrina.Clear(); topFilas.Clear(); mensajes.Clear(); topTotal = 0;
            SueltaHorneado();
            if (!gpuEnabled || gpuPanel == null || (!escaparate && !topNoche)) return;
            // Los discos del cubo (los eligio EnterAttract): su nombre, su artista, su genero, cuantas trae y cuantas
            // veces se han pedido. Se apuntan aqui, que 'albums' no se esta rehaciendo (EnterAttract no entra si si).
            List<string> cov = (attractView != null) ? attractView.CycleCovers : null;
            if (escaparate && cov != null && cov.Count > 0)
            {
                Dictionary<string, Album> porPortada = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < cov.Count; i++) porPortada[cov[i]] = null;
                for (int i = 0; i < albums.Count; i++)
                {
                    Album a = albums[i];
                    if (a == null || a.IsTop || a.CoverPath == null) continue;
                    Album ya;
                    if (porPortada.TryGetValue(a.CoverPath, out ya) && ya == null) porPortada[a.CoverPath] = a;
                }
                for (int i = 0; i < cov.Count; i++) vitrina.Add(FichaDe(porPortada[cov[i]]));
            }
            if (topNoche) { CambiaDeNoche(DateTime.Now); ArmaTopNoche(); }
            ArmaMensajes();
            // La primera ficha y los mensajes, ya: aparecen mientras el cubo llega. La lista, en el reloj (falta rato).
            if (EscaparateActivo() && vitrina.Count > 0)
            {
                int w = ClientSize.Width, h = ClientSize.Height;
                int idx; float f; CicloVitrina(attractView.Phase, vitrina.Count, out idx, out f);
                AseguraFicha(idx, -1, w, h);
                HorneaLosMensajes(w, h);
            }
        }
        private void EscaparateSaleGpu()
        {
            atrVivo = false;
            SueltaHorneado();
        }

        private FichaVitrina FichaDe(Album a)
        {
            FichaVitrina d = new FichaVitrina();
            if (a == null) return d;
            d.Nombre = a.Name ?? ""; d.Artista = a.Artist ?? "";
            d.Canciones = a.Tracks.Count; d.Videos = a.VideoFiles.Count;
            Dictionary<string, int> artistas = null;
            for (int i = 0; i < a.Tracks.Count; i++)
            {
                Track t = a.Tracks[i];
                if (t == null) continue;
                int v; if (t.Path != null && songCounts.TryGetValue(t.Path, out v)) d.Pedidas += v;
                if (d.Artista.Length == 0 && !string.IsNullOrEmpty(t.Artist))
                {
                    if (artistas == null) artistas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    int c; artistas.TryGetValue(t.Artist, out c); artistas[t.Artist] = c + 1;
                }
            }
            if (artistas != null) { int mejor = 0; foreach (KeyValuePair<string, int> kv in artistas) if (kv.Value > mejor) { mejor = kv.Value; d.Artista = kv.Key; } }
            // El genero del disco: el que la rocola ya sabe del disco o del artista; si no, el que mas se repite.
            string g = GeneroDeCancion(null, a);
            if (g.Length == 0 || g == "Otros")
            {
                Dictionary<string, int> votos = new Dictionary<string, int>();
                int mejor = 0; g = "";
                for (int i = 0; i < a.Tracks.Count; i++)
                {
                    string e = GeneroDeCancion(a.Tracks[i], null);
                    if (e.Length == 0 || e == "Otros") continue;
                    int c; votos.TryGetValue(e, out c); votos[e] = ++c;
                    if (c > mejor) { mejor = c; g = e; }
                }
            }
            if (g.Length > 0 && g != "Otros") { d.Genero = g; d.GeneroColor = ColorDeEtiqueta(g); }
            return d;
        }

        // Las cinco de la noche, buscadas en la biblioteca (la que ya no esta, se salta). Hacen falta tres.
        private void ArmaTopNoche()
        {
            topFilas.Clear(); topTotal = 0;
            foreach (KeyValuePair<string, int> kv in nocheCuenta) topTotal += kv.Value;
            List<string> cand = MasPedidasNoche(nocheCuenta, nocheOrden, TopFilas + 10);
            Dictionary<string, int> quiero = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < cand.Count; i++) quiero[cand[i]] = i;
            FilaNoche[] hallada = new FilaNoche[cand.Count];
            int faltan = cand.Count;
            for (int i = 0; i < albums.Count && faltan > 0; i++)
            {
                Album a = albums[i];
                if (a == null || a.IsTop) continue;
                for (int k = 0; k < a.Tracks.Count; k++)
                {
                    Track t = a.Tracks[k]; int j;
                    if (t == null || t.Path == null || !quiero.TryGetValue(t.Path, out j) || hallada[j] != null) continue;
                    FilaNoche f = new FilaNoche();
                    f.Ruta = t.Path; f.Veces = nocheCuenta[cand[j]];
                    f.Titulo = TituloSinPista(string.IsNullOrEmpty(t.Title) ? t.ToString() : t.Title);
                    f.Artista = string.IsNullOrEmpty(t.Artist) ? (a.Artist ?? "") : t.Artist;
                    f.Portada = a.CoverPath;
                    string e = GeneroDeCancion(t, a);
                    f.Color = (e.Length > 0) ? ColorDeEtiqueta(e) : Color.FromArgb(96, 110, 170);
                    hallada[j] = f; faltan--;
                }
            }
            for (int j = 0; j < hallada.Length && topFilas.Count < TopFilas; j++) if (hallada[j] != null) topFilas.Add(hallada[j]);
            if (topFilas.Count < TopMinimo) topFilas.Clear();
        }

        // Los mensajes que se turnan debajo de la ficha.
        // MEJOR DICHOS (peticion del 12-10-2026: "que se entiendan mejor y que al cliente de verdad le interese lo que la
        // rocola ofrece; no super largos, mejor formulados"). Cortos, de tu a tu y cada uno con una razon para acercarse:
        // que hay mucho para elegir, cuanto rinde una moneda, lo que esta sonando esta noche, que se puede buscar por el
        // nombre y que hay quien te ensene (la ayuda de la estrella, ROJO + ABAJO). Caben en una pantalla 4:3 (sextatanda).
        private void ArmaMensajes()
        {
            mensajes.Clear();
            mensajes.AddRange(MensajesAtraccion(attractView != null ? attractView.SongCount : 0, CreditsPerCoin,
                topNoche && topFilas.Count > 0 ? topFilas[0].Titulo : null));
        }
        internal static List<string> MensajesAtraccion(int canciones, int porMoneda, string favorita)
        {
            List<string> m = new List<string>(5);
            m.Add(canciones > 0 ? "¡Elige tu canción! Hay " + canciones.ToString("#,0") + " esperándote" : "¡Elige tu canción! Toca cualquier botón");
            m.Add("Una moneda = " + porMoneda + (porMoneda == 1 ? " canción" : " canciones") + ". ¡Tú pones la música!");
            if (!string.IsNullOrEmpty(favorita)) m.Add("La favorita de esta noche: «" + favorita + "»");
            m.Add("¿Buscas una? ROJO + ARRIBA y escríbela");
            m.Add("¿Primera vez? ROJO + ABAJO y te guío");
            return m;
        }

        // ======================================================================================================
        //  LO QUE VE LA TARJETA (cada cuadro: solo cuentas, nada se hornea aqui)
        // ======================================================================================================
        private float[] filaA, filaDx, filaBarra, filaLargo;

        private GpuAtraccion BuildAtraccionState()
        {
            GpuAtraccion a = new GpuAtraccion();
            a.UiAlfa = 1f;
            if (!attractOn || attractView == null || !atrVivo || !gpuEnabled || gpuPanel == null) return a;
            Arreglos();
            bool vit = EscaparateActivo() && vitrina.Count > 0, hayTop = HayTopNoche();
            if (!vit && !hayTop) return a;
            int w = ClientSize.Width, h = ClientSize.Height;
            double ms = GpuPanel.AhoraMs() - atrDesde;
            float m = MezclaTop(ms, hayTop);
            a.Has = true;
            a.UiAlfa = 1f - m;
            if (vit)
            {
                int idx; float f; CicloVitrina(attractView.Phase, vitrina.Count, out idx, out f);
                RectangleF col = ColumnaEscaparate(w, h);
                int mh = AltoMensaje(h);
                a.Vitrina = true;
                a.VitrinaA = Suave((float)(ms / EntraFichaMs)) * VitrinaConTop(m);
                int sa = SlotDeFicha(idx), sb = (vitrina.Count > 1) ? SlotDeFicha((idx + 1) % vitrina.Count) : -1;
                float va, vdy, na, ndy; FundeFichas((sb >= 0) ? f : 0f, out va, out vdy, out na, out ndy);
                a.FichaX = col.X;
                if (sa >= 0) { a.Ficha = vitBmp[sa]; a.FichaVer = vitVer[sa]; a.FichaAlfa = va; a.FichaY = col.Y + (col.Height - mh - vitBmp[sa].Height) * .5f + vdy; }
                if (sb >= 0 && na > 0f) { a.FichaB = vitBmp[sb]; a.FichaBVer = vitVer[sb]; a.FichaBAlfa = na; a.FichaBY = col.Y + (col.Height - mh - vitBmp[sb].Height) * .5f + ndy; }
                if (msgsBmp != null && mensajes.Count > 0 && msgsAncho != null && msgsAncho.Length == mensajes.Count)
                {
                    int k; float ma, mdy; FaseMensaje(ms, mensajes.Count, out k, out ma, out mdy);
                    a.Msgs = msgsBmp; a.MsgsVer = msgsVer; a.MsgFila = k; a.MsgA = ma; a.MsgDy = mdy; a.MsgH = mh;
                    a.MsgX = col.X; a.MsgY = col.Bottom - mh;
                    // Un respiro dorado detras, como el de "toca un boton para elegir" de siempre.
                    double p = .5 + .5 * Math.Sin(phase * 3.0);
                    float tw = msgsAncho[k];
                    a.BrilloX = col.X + tw * .5f; a.BrilloY = a.MsgY + mh * .5f + mdy; a.BrilloRx = Math.Max(40f, tw * .62f); a.BrilloRy = mh * 1.25f;
                    int al = (int)((16 + 38 * p) * ma * a.VitrinaA + .5); if (al > 255) al = 255; if (al < 0) al = 0;
                    a.BrilloArgb = ((uint)al << 24) | 0x00FFDC78u;
                }
            }
            if (hayTop && m > 0f)
            {
                double tt = MsEnTop(ms);
                RectangleF pag; float cabW, cabH, filasY, filaH;
                GeometriaTop(w, h, out pag, out cabW, out cabH, out filasY, out filaH);
                a.Top = true; a.TopBmp = topBmp; a.TopVer = topVer;
                a.TopX = pag.X; a.TopY = pag.Y; a.CabH = cabH; a.FilasY = filasY; a.FilaH = filaH;
                float sale = SalePagina(m);
                a.CabA = CabeceraTop(tt) * sale;
                a.Filas = topFilas.Count;
                int max = 1; for (int i = 0; i < topFilas.Count; i++) if (topFilas[i].Veces > max) max = topFilas[i].Veces;
                for (int i = 0; i < a.Filas && i < TopFilas; i++)
                {
                    float fa, fdx, fb; FilaTop(tt, i, out fa, out fdx, out fb);
                    filaA[i] = fa * sale; filaDx[i] = fdx; filaBarra[i] = fb; filaLargo[i] = topFilas[i].Veces / (float)max;
                }
                a.FilaA = filaA; a.FilaDx = filaDx; a.Barra = filaBarra; a.BarraLargo = filaLargo;
                a.BarraX = topBarraX; a.BarraW = topBarraW; a.BarraH = topBarraH; a.BarraDy = topBarraDy;
            }
            return a;
        }

        // El cubo gigante, AHORA: el de siempre (AttractCubeRect) o el del escaparate, camino de la esquina con la lista.
        private RectangleF CuboAtraccionAhora()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            RectangleF r0 = EscaparateActivo() ? CuboEscaparate(w, h) : AttractCubeRect();
            if (!atrVivo) return r0;
            float m = MezclaTop(GpuPanel.AhoraMs() - atrDesde, HayTopNoche());
            if (m <= 0f) return r0;
            RectangleF r1 = CuboEsquina(w, h);
            return new RectangleF(r0.X + (r1.X - r0.X) * m, r0.Y + (r1.Y - r0.Y) * m, r0.Width + (r1.Width - r0.Width) * m, r0.Height + (r1.Height - r0.Height) * m);
        }
        // Su giro: el de siempre, o mecerse con el escaparate.
        private double AnguloAtraccion()
        {
            if (!EscaparateActivo() || !atrVivo) return attractView.Angle;
            return MeceCubo(GpuPanel.AhoraMs() - atrDesde);
        }
        // Sus portadas: las de siempre, o las del escaparate, que se quedan mas (hay que leer su nombre).
        private void ParCuboAtraccion(out Image a, out Image b, out float f)
        {
            if (!EscaparateActivo()) { attractView.GetCoverPair(out a, out b, out f); return; }
            a = null; b = null; f = 0f;
            List<string> cov = attractView.CycleCovers;
            if (cov == null || cov.Count == 0 || attractView.GetCoverByPath == null) return;
            int idx; CicloVitrina(attractView.Phase, cov.Count, out idx, out f);
            a = attractView.GetCoverByPath(cov[idx]);
            b = (cov.Count > 1) ? attractView.GetCoverByPath(cov[(idx + 1) % cov.Count]) : null;
        }
        // Los dos textos de siempre se apagan mientras esta la lista (sin escaparate; con el, no se pintan).
        private float UiAlfaAtraccion()
        {
            if (!attractOn || !atrVivo) return 1f;
            return 1f - MezclaTop(GpuPanel.AhoraMs() - atrDesde, HayTopNoche());
        }

        // ======================================================================================================
        //  LO QUE SE HORNEA (en el reloj de la pantalla, una cosa por vuelta)
        // ======================================================================================================
        private Bitmap[] vitBmp; private int[] vitIdx, vitVer;
        // Los arreglos de la atraccion, la primera vez que hacen falta.
        private void Arreglos()
        {
            if (vitBmp == null) { vitBmp = new Bitmap[2]; vitIdx = new int[] { -1, -1 }; vitVer = new int[2]; }
            if (filaA == null) { filaA = new float[TopFilas]; filaDx = new float[TopFilas]; filaBarra = new float[TopFilas]; filaLargo = new float[TopFilas]; }
        }
        private int vitSeq;
        private Bitmap msgsBmp; private int msgsVer; private float[] msgsAncho; private int msgsW = -1, msgsH = -1;
        private Bitmap topBmp; private int topVer, topW = -1, topH = -1, topFaltan, topReintentos, topIntento;
        private float topBarraX, topBarraW, topBarraH, topBarraDy;

        private void AtraccionTic()
        {
            if (!attractOn || !atrVivo || !gpuEnabled || gpuPanel == null || attractView == null) return;
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w < 200 || h < 150) return;
            Arreglos();
            if (EscaparateActivo() && vitrina.Count > 0)
            {
                int idx; float f; CicloVitrina(attractView.Phase, vitrina.Count, out idx, out f);
                if (AseguraFicha(idx, -1, w, h)) return;
                if (vitrina.Count > 1 && AseguraFicha((idx + 1) % vitrina.Count, idx, w, h)) return;
                if (msgsBmp == null || msgsW != w || msgsH != h) { HorneaLosMensajes(w, h); return; }
            }
            if (topNoche && topFilas.Count >= TopMinimo)
            {
                if (topBmp == null || topW != w || topH != h) { HorneaLaLista(w, h); return; }
                // Portadas que aun no habian llegado: se vuelve a hornear un poco despues (pocas veces).
                if (topFaltan > 0 && topReintentos < 8 && Environment.TickCount - topIntento > 700) { topReintentos++; HorneaLaLista(w, h); }
            }
        }

        private int SlotDeFicha(int idx)
        {
            for (int s = 0; s < 2; s++) if (vitIdx[s] == idx && vitBmp[s] != null) return s;
            return -1;
        }
        // Hornea la ficha del disco idx si no esta (en el hueco que no sea el de 'guarda'). true si horneo.
        private bool AseguraFicha(int idx, int guarda, int w, int h)
        {
            if (idx < 0 || idx >= vitrina.Count) return false;
            int s = SlotDeFicha(idx);
            RectangleF col = ColumnaEscaparate(w, h);
            if (s >= 0 && vitBmp[s].Width == (int)col.Width) return false;
            if (s < 0) s = (vitIdx[0] == guarda && guarda >= 0) ? 1 : (vitIdx[1] == guarda && guarda >= 0) ? 0 : (vitBmp[0] == null ? 0 : 1);
            if (vitBmp[s] != null) { vitBmp[s].Dispose(); vitBmp[s] = null; }
            vitBmp[s] = HorneaFichaVitrina(vitrina[idx], (int)col.Width, h);
            vitIdx[s] = idx; vitVer[s] = ++vitSeq;
            return true;
        }

        private void SueltaHorneado()
        {
            Arreglos();
            for (int s = 0; s < 2; s++) { if (vitBmp[s] != null) vitBmp[s].Dispose(); vitBmp[s] = null; vitIdx[s] = -1; }
            if (msgsBmp != null) { msgsBmp.Dispose(); msgsBmp = null; }
            if (topBmp != null) { topBmp.Dispose(); topBmp = null; }
            msgsW = msgsH = topW = topH = -1; msgsAncho = null; topFaltan = topReintentos = 0;
        }

        private static Font Letra(float px) { return new Font("Trebuchet MS", px, FontStyle.Bold, GraphicsUnit.Pixel); }
        private static readonly Color AtrTitulo = Color.White, AtrArtista = Color.FromArgb(120, 230, 150), AtrEtiqueta = Color.FromArgb(207, 224, 255),
            AtrAzul = Color.FromArgb(150, 200, 255), AtrAmbar = Color.FromArgb(255, 198, 104), AtrMensaje = Color.FromArgb(255, 226, 176),
            AtrAmarillo = Color.FromArgb(242, 206, 72), AtrTenue = Color.FromArgb(170, 184, 222);

        internal static string TextoCanciones(int canciones, int videos)
        {
            if (canciones <= 0 && videos > 0) return videos + (videos == 1 ? " video" : " videos");
            return canciones + (canciones == 1 ? " canción" : " canciones");
        }
        internal static string TextoPedidoDisco(int veces) { return veces <= 0 ? "" : "pedido " + veces + (veces == 1 ? " vez" : " veces"); }
        internal static string TextoVeces(int veces) { return veces + (veces == 1 ? " vez" : " veces"); }

        // LA FICHA: "EN ESTA ROCOLA", el nombre (hasta dos renglones), el artista y las pastillas. Mide lo que ocupa.
        internal static Bitmap HorneaFichaVitrina(FichaVitrina d, int ancho, int h)
        {
            if (ancho < 40) ancho = 40;
            using (Font fEt = Letra(Math.Max(11f, h * .019f)), fTit = Letra(Math.Max(24f, h * .062f)), fArt = Letra(Math.Max(15f, h * .033f)), fPil = Letra(Math.Max(11f, h * .021f)))
            using (StringFormat dos = new StringFormat(), una = new StringFormat())
            {
                dos.Trimming = StringTrimming.EllipsisWord; dos.FormatFlags = StringFormatFlags.LineLimit;
                una.Trimming = StringTrimming.EllipsisCharacter; una.FormatFlags = StringFormatFlags.NoWrap;
                float gapEt = h * .012f, gapTit = h * .010f, gapArt = h * .020f;
                float etH = fEt.GetHeight(), titL = fTit.GetHeight(), artH = fArt.GetHeight(), pilH = fPil.GetHeight() + 8f;
                string nombre = string.IsNullOrEmpty(d.Nombre) ? "Tu rocola" : d.Nombre;
                float titH;
                Font fNom = fTit;   // la del nombre: la grande, o una algo mas chica si no cabe en dos renglones
                try {
                // Las pastillas: el genero, cuantas veces se ha pedido y cuantas canciones trae. Si no caben en un renglon
                // pasan al segundo; la que no cabe en dos, no sale.
                List<string> pil = new List<string>(); List<Color> pc = new List<Color>();
                if (d.Genero.Length > 0) { pil.Add(d.Genero); pc.Add(d.GeneroColor); }
                string ped = TextoPedidoDisco(d.Pedidas);
                if (ped.Length > 0) { pil.Add(ped); pc.Add(AtrAmbar); }
                if (d.Canciones > 0 || d.Videos > 0) { pil.Add(TextoCanciones(d.Canciones, d.Videos)); pc.Add(AtrAzul); }
                PointF[] pos; int filas;
                using (Bitmap b1 = new Bitmap(1, 1))
                using (Graphics g1 = Graphics.FromImage(b1))
                using (StringFormat mide = new StringFormat())
                {
                    g1.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    int letras, lineas;
                    g1.MeasureString(nombre, fTit, new SizeF(ancho, titL * 40f), mide, out letras, out lineas);
                    // UN NOMBRE LARGO se cortaba con puntos a los dos renglones ("Los Tigres del Norte - Jefe de Je..."). Antes
                    // de cortarlo se prueba con la letra algo mas chica (hasta el 78 %), que en la pantalla sigue siendo grande.
                    for (int k = 0; k < 2 && lineas > 2; k++)
                    {
                        Font menor = Letra(fTit.Size * (k == 0 ? .88f : .78f));
                        g1.MeasureString(nombre, menor, new SizeF(ancho, menor.GetHeight() * 40f), mide, out letras, out lineas);
                        if (fNom != fTit) fNom.Dispose();
                        fNom = menor; titL = menor.GetHeight();
                    }
                    // Con la letra ya achicada, si aun no cabe, un TERCER renglon (si hay sitio, abajo).
                    titH = titL * Math.Max(1, Math.Min(fNom != fTit ? 3 : 2, lineas));
                    filas = DisponPastillas(g1, pil, fPil, pilH, ancho, out pos);
                }
                float gapPil = pilH * .30f;
                float resto = etH + gapEt + gapTit + (d.Artista.Length > 0 ? artH + gapArt : gapArt * .5f) + filas * pilH + Math.Max(0, filas - 1) * gapPil + 4f;
                if (titH > titL * 2.5f && resto + titH > h * .46f - AltoMensaje(h)) titH = titL * 2f;   // sin sitio: dos y puntos
                float alto = resto + titH;
                Bitmap bmp = new Bitmap(ancho, (int)Math.Ceiling(alto), PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    float y = 0f;
                    using (SolidBrush b = new SolidBrush(AtrEtiqueta)) EspaciadoEntero(g, "EN ESTA ROCOLA", fEt, b, ancho, etH, fEt.Size * .22f);
                    y += etH + gapEt;
                    using (SolidBrush b = new SolidBrush(AtrTitulo)) g.DrawString(nombre, fNom, b, new RectangleF(0, y, ancho, titH + titL * .25f), dos);
                    y += titH + gapTit;
                    if (d.Artista.Length > 0)
                    {
                        using (SolidBrush b = new SolidBrush(AtrArtista)) Renglon(g, d.Artista, fArt, b, new RectangleF(0, y, ancho, artH + 2f), una);
                        y += artH + gapArt;
                    }
                    else y += gapArt * .5f;
                    for (int i = 0; i < pil.Count; i++)
                    {
                        if (pos[i].X < 0f) continue;
                        float py = y + pos[i].Y * (pilH + gapPil);
                        if (i == 0 && d.Genero.Length > 0) PastillaGenero(g, pil[i], fPil, pc[i], pos[i].X, py, pilH, ancho);
                        else Pastilla(g, pil[i], fPil, pc[i], pos[i].X, py, pilH, ancho);
                    }
                }
                return bmp;
                }
                finally { if (fNom != fTit) fNom.Dispose(); }
            }
        }
        // Letras con aire entre ellas (el letrero de arriba, como "A H O R A   S U E N A").
        // EL LETRERO ENTERO, ARRIBA DE LA FICHA (foto del 13-10-2026: en el bar «EN ESTA ROCOLA» salia sin la parte de arriba de
        // las letras). Dibujado en la y 0 de la ficha, la letra del bar (Trebuchet MS) se salia por arriba y por la izquierda, y
        // lo que cae fuera de la imagen no existe. Ahora se dibuja con margen de sobra en una imagen aparte, se buscan sus
        // pixeles de verdad y se pega desde la primera fila con tinta, en la y 0: entero con cualquier letra y cualquier tamaño.
        // Por la izquierda se respeta donde empezaba (x 0), salvo que la letra se saliera: entonces se corre lo justo.
        private static void EspaciadoEntero(Graphics g, string txt, Font f, Brush b, int ancho, float alto, float aire)
        {
            int pad = (int)Math.Ceiling(alto) + 4;
            int tw = ancho + 2 * pad, th = (int)Math.Ceiling(alto * 2f) + 2 * pad;
            using (Bitmap tmp = new Bitmap(tw, th, PixelFormat.Format32bppPArgb))
            {
                using (Graphics gt = Graphics.FromImage(tmp))
                {
                    gt.Clear(Color.Transparent);
                    gt.SmoothingMode = g.SmoothingMode; gt.TextRenderingHint = g.TextRenderingHint;
                    Espaciado(gt, txt, f, b, pad, pad, aire);
                }
                int x0, y0, x1, y1;
                if (!Tinta(tmp, out x0, out y0, out x1, out y1)) return;
                int sx = Math.Min(x0, pad);                       // donde empezaba la linea, o antes si la letra se salia
                int ww = Math.Min(ancho, x1 - sx + 1), hh = y1 - y0 + 1;
                if (ww <= 0 || hh <= 0) return;
                CompositingMode cm = g.CompositingMode; InterpolationMode im = g.InterpolationMode;
                g.CompositingMode = CompositingMode.SourceOver; g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(tmp, new Rectangle(0, 0, ww, hh), sx, y0, ww, hh, GraphicsUnit.Pixel);
                g.CompositingMode = cm; g.InterpolationMode = im;
            }
        }
        // El rectangulo de pixeles con algo de tinta (alfa > 0). False si no hay ninguno.
        internal static bool Tinta(Bitmap bmp, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = bmp.Width; y0 = bmp.Height; x1 = -1; y1 = -1;
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] fila = new int[bmp.Width];
                for (int y = 0; y < bmp.Height; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), fila, 0, bmp.Width);
                    for (int x = 0; x < fila.Length; x++)
                        if (((uint)fila[x] >> 24) != 0)
                        {
                            if (x < x0) x0 = x; if (x > x1) x1 = x;
                            if (y < y0) y0 = y; if (y > y1) y1 = y;
                        }
                }
            }
            finally { bmp.UnlockBits(d); }
            return x1 >= 0;
        }
        private static void Espaciado(Graphics g, string txt, Font f, Brush b, float x, float y, float aire)
        {
            using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                for (int i = 0; i < txt.Length; i++)
                {
                    string c = txt.Substring(i, 1);
                    g.DrawString(c, f, b, x, y, sf);
                    x += g.MeasureString(c, f, PointF.Empty, sf).Width + aire;
                }
            }
        }
        // Una pastilla como las del lienzo: borde y letra del color, fondo oscuro. Devuelve donde acaba (+ aire), o
        // -1 si no cabe (y entonces no se pinta).
        private static float Pastilla(Graphics g, string txt, Font f, Color c, float x, float y, float alto, float maxX)
        {
            float w = AnchoPastilla(g, txt, f, alto);
            if (x + w > maxX) return -1f;
            RectangleF r = new RectangleF(x, y, w, alto);
            using (GraphicsPath p = Redondo(r, alto * .5f))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(72, 0, 0, 0))) g.FillPath(b, p);
                using (Pen pe = new Pen(Color.FromArgb(220, c), 1.4f)) g.DrawPath(pe, p);
            }
            TextoCentrado(g, txt, f, c, r);
            return x + w + alto * .45f;
        }
        // La del genero, como la del panel de reposo: relleno y borde de su color, letra clara.
        private static float PastillaGenero(Graphics g, string txt, Font f, Color c, float x, float y, float alto, float maxX)
        {
            float w = AnchoPastilla(g, txt, f, alto);
            if (x + w > maxX) return x;
            RectangleF r = new RectangleF(x, y, w, alto);
            using (GraphicsPath p = Redondo(r, alto * .5f))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(70, c))) g.FillPath(b, p);
                using (Pen pe = new Pen(Color.FromArgb(190, c), 1.4f)) g.DrawPath(pe, p);
            }
            TextoCentrado(g, txt, f, Lighten(c, .45), r);
            return x + w + alto * .45f;
        }
        // Un renglon que quepa en su caja: si no cabe, la letra se achica hasta un 25 % y, si aun asi no, se corta con
        // puntos suspensivos. Devuelve lo que ocupa.
        //
        // TEXTOS CORTADOS (aviso del usuario del 12-10-2026: "ayer salian cortados unos textos" en la atraccion). La letra se
        // achicaba a la medida JUSTA (ancho / lo que media). Pero a otro tamano las letras no miden exactamente lo
        // proporcional -se ajustan a la rejilla de pixeles-, el renglon se pasaba por uno o dos pixeles y se cortaba el
        // final: "...elige entre 12,345 cancio...", "...el boton rojo y la flech". Le pasaba a los cuatro mensajes en
        // 1024x768. Ahora se achica con un poco de margen y se VUELVE A MEDIR con la letra chica, hasta que quepa (o
        // hasta el 75 %; de ahi, puntos suspensivos como siempre).
        internal static float Renglon(Graphics g, string txt, Font f, Brush b, RectangleF r, StringFormat sf)
        { return Renglon(g, txt, f, b, r, sf, .75f); }
        // 'minimo': lo mas que se deja achicar la letra. Si ni asi cabe, se acorta POR PALABRAS con puntos suspensivos
        // (antes lo cortaba la orilla: con GDI+ ponia los puntos a media palabra, y en otros sistemas partia una letra).
        internal static float Renglon(Graphics g, string txt, Font f, Brush b, RectangleF r, StringFormat sf, float minimo)
        {
            if (string.IsNullOrEmpty(txt)) return 0f;
            float w = g.MeasureString(txt, f, PointF.Empty, sf).Width;
            if (w <= r.Width) { g.DrawString(txt, f, b, r, sf); return w; }
            float esc = EscalaQueCabe(g, txt, f, r.Width, sf, minimo);
            using (Font chica = new Font(f.FontFamily, f.Size * esc, f.Style, f.Unit))
            {
                string t = AcortaPorPalabras(g, txt, chica, r.Width, sf);
                g.DrawString(t, chica, b, r, sf);
                return Math.Min(r.Width, g.MeasureString(t, chica, PointF.Empty, sf).Width);
            }
        }
        // Cuanto hay que achicar la letra (entre 'minimo' y 1) para que el renglon quepa de verdad en 'ancho', midiendo
        // cada tamano que se prueba. Solo se usa al hornear, nunca por cuadro.
        internal static float EscalaQueCabe(Graphics g, string txt, Font f, float ancho, StringFormat sf)
        { return EscalaQueCabe(g, txt, f, ancho, sf, .75f); }
        internal static float EscalaQueCabe(Graphics g, string txt, Font f, float ancho, StringFormat sf, float minimo)
        {
            float w = g.MeasureString(txt, f, PointF.Empty, sf).Width;
            if (w <= ancho || ancho <= 0f) return 1f;
            float esc = Math.Max(minimo, ancho / w * .98f);
            for (int i = 0; i < 6 && esc > minimo; i++)
            {
                float wc;
                using (Font chica = new Font(f.FontFamily, f.Size * esc, f.Style, f.Unit)) wc = g.MeasureString(txt, chica, PointF.Empty, sf).Width;
                if (wc <= ancho) break;
                esc = Math.Max(minimo, esc * Math.Min(.99f, ancho / wc * .99f));
            }
            return esc;
        }
        // Si el texto no cabe con esa letra, se quitan palabras del final y se ponen puntos suspensivos hasta que quepa
        // (y si una sola palabra no cabe, letras). Si cabe, el mismo texto.
        internal static string AcortaPorPalabras(Graphics g, string txt, Font f, float ancho, StringFormat sf)
        {
            if (string.IsNullOrEmpty(txt) || g.MeasureString(txt, f, PointF.Empty, sf).Width <= ancho) return txt;
            string t = txt;
            while (t.Length > 1)
            {
                int corte = t.LastIndexOf(' ', t.Length - 1);
                t = (corte > 0) ? t.Substring(0, corte) : t.Substring(0, t.Length - 1);
                t = t.TrimEnd(' ', ',', '(', '-', ':', ';', '\u00ab');
                string prueba = t + "\u2026";
                if (g.MeasureString(prueba, f, PointF.Empty, sf).Width <= ancho) return prueba;
            }
            return "\u2026";
        }
        // Un texto con una cita entre comillas (« ») que ni con la letra al 75 % cabe: se acorta la CITA por palabras y se
        // le ponen puntos suspensivos dentro, para que las comillas cierren y se lea el principio entero:
        // "Lo mas pedido esta noche: «Amor Eterno (En Vivo…»". Sin cita, o si cabe, devuelve el mismo texto.
        internal static string AcortaCita(Graphics g, string txt, Font f, float ancho, StringFormat sf)
        {
            if (string.IsNullOrEmpty(txt) || ancho <= 0f) return txt;
            int a = txt.IndexOf('\u00ab'), z = txt.LastIndexOf('\u00bb');
            if (a < 0 || z <= a + 1) return txt;
            float tope = ancho / .75f;   // lo que cabe con la letra al 75 %
            if (g.MeasureString(txt, f, PointF.Empty, sf).Width <= tope) return txt;
            string antes = txt.Substring(0, a + 1), cita = txt.Substring(a + 1, z - a - 1), despues = txt.Substring(z);
            while (cita.Length > 1)
            {
                int corte = cita.LastIndexOf(' ', cita.Length - 1);
                cita = (corte > 0) ? cita.Substring(0, corte) : cita.Substring(0, cita.Length - 1);
                cita = cita.TrimEnd(' ', ',', '(', '-', ':', ';');
                string prueba = antes + cita + "\u2026" + despues;
                if (g.MeasureString(prueba, f, PointF.Empty, sf).Width <= tope) return prueba;
            }
            return txt;
        }
        // Donde va cada pastilla: X en la fila, Y = numero de fila (0 o 1); X = -1 si no cabe. Devuelve las filas usadas.
        internal static int DisponPastillas(Graphics g, List<string> textos, Font f, float alto, float ancho, out PointF[] pos)
        {
            pos = new PointF[textos.Count];
            int fila = 0, filas = 0; float x = 0f;
            for (int i = 0; i < textos.Count; i++)
            {
                float w = AnchoPastilla(g, textos[i], f, alto);
                if (x > 0f && x + w > ancho && fila == 0) { fila = 1; x = 0f; }
                if (x + w > ancho) { pos[i] = new PointF(-1f, fila); continue; }
                pos[i] = new PointF(x, fila); x += w + alto * .45f; filas = fila + 1;
            }
            return Math.Max(1, filas);
        }
        private static float AnchoPastilla(Graphics g, string txt, Font f, float alto) { return g.MeasureString(txt, f, PointF.Empty, StringFormat.GenericTypographic).Width + alto * 1.1f; }
        private static void TextoCentrado(Graphics g, string txt, Font f, Color c, RectangleF r)
        {
            using (StringFormat cen = new StringFormat())
            using (SolidBrush b = new SolidBrush(c))
            {
                cen.Alignment = StringAlignment.Center; cen.LineAlignment = StringAlignment.Center; cen.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(txt, f, b, r, cen);
            }
        }

        // LOS MENSAJES: uno por renglon, en una sola textura; la tarjeta ensena el que toca.
        private void HorneaLosMensajes(int w, int h)
        {
            if (msgsBmp != null) { msgsBmp.Dispose(); msgsBmp = null; }
            float[] anchos;
            msgsBmp = HorneaMensajes(mensajes, (int)ColumnaEscaparate(w, h).Width, h, out anchos);
            msgsAncho = anchos; msgsVer++; msgsW = w; msgsH = h;
        }
        internal static Bitmap HorneaMensajes(List<string> lista, int ancho, int h, out float[] anchos)
        {
            int n = Math.Max(1, lista.Count), mh = AltoMensaje(h);
            anchos = new float[lista.Count];
            Bitmap bmp = new Bitmap(Math.Max(40, ancho), mh * n, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font f = Letra(Math.Max(14f, h * .029f)))
            using (StringFormat una = new StringFormat())
            using (SolidBrush b = new SolidBrush(AtrMensaje))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                una.Trimming = StringTrimming.EllipsisCharacter; una.FormatFlags = StringFormatFlags.NoWrap; una.LineAlignment = StringAlignment.Center;
                for (int i = 0; i < lista.Count; i++)
                    anchos[i] = Renglon(g, AcortaCita(g, lista[i], f, bmp.Width, una), f, b, new RectangleF(0, i * mh, bmp.Width, mh), una);
            }
            return bmp;
        }

        // LA LISTA DE LA NOCHE: la cabecera y las cinco filas en una textura; la tarjeta las mete una a una y pone las
        // barras (crecen alla, no aqui).
        private void HorneaLaLista(int w, int h)
        {
            if (topBmp != null) { topBmp.Dispose(); topBmp = null; }
            RectangleF pag; float cabW, cabH, filasY, filaH;
            GeometriaTop(w, h, out pag, out cabW, out cabH, out filasY, out filaH);
            Image[] portadas = new Image[topFilas.Count];
            int faltan = 0;
            for (int i = 0; i < topFilas.Count; i++)
            {
                portadas[i] = string.IsNullOrEmpty(topFilas[i].Portada) ? null : LoadCoverByPath(topFilas[i].Portada);
                if (portadas[i] == null && !string.IsNullOrEmpty(topFilas[i].Portada)) faltan++;
            }
            float bx, bw, bh, bdy;
            topBmp = HorneaTop(topFilas, portadas, topTotal, (int)pag.Width, (int)pag.Height, cabW, cabH, filasY, filaH, out bx, out bw, out bh, out bdy);
            topBarraX = bx; topBarraW = bw; topBarraH = bh; topBarraDy = bdy;
            topVer++; topW = w; topH = h; topFaltan = faltan; topIntento = Environment.TickCount;
        }
        internal static Bitmap HorneaTop(List<FilaNoche> filas, Image[] portadas, int total, int ancho, int alto, float cabW, float cabH, float filasY, float filaH,
                                          out float barraX, out float barraW, out float barraH, out float barraDy)
        {
            Bitmap bmp = new Bitmap(Math.Max(40, ancho), Math.Max(40, alto), PixelFormat.Format32bppPArgb);
            float rh = filaH;
            // Dentro de cada fila: el puesto, la portada, titulo y artista, la barra y "N veces".
            float puestoW = rh * .80f, lado = rh * .80f, px = puestoW + rh * .10f;
                        // El titulo manda y la barra es adorno: la mitad del ancho (era el 40 % y los titulos largos se cortaban),
            // dejandole a la barra al menos 0,8 de fila para que no se meta debajo de "N veces".
            float vecesW0 = rh * 1.1f;
            float tx = px + lado + rh * .24f, tw = Math.Max(rh * 2.5f, Math.Min(ancho * .50f, ancho - tx - vecesW0 - rh * 1.35f));
            float vecesW = rh * 1.1f;
            barraX = tx + tw + rh * .30f; barraW = Math.Max(rh, ancho - vecesW - rh * .25f - barraX);
            barraH = Math.Max(8f, rh * .16f); barraDy = (rh - barraH) * .5f;
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font fCab = Letra(Math.Max(20f, cabH * .25f)), fSub = Letra(Math.Max(12f, cabH * .12f)))
            using (Font fPuesto1 = Letra(rh * .50f), fPuesto = Letra(rh * .40f), fTit = Letra(Math.Max(14f, rh * .29f)), fArt = Letra(Math.Max(11f, rh * .19f)), fVeces = Letra(Math.Max(11f, rh * .21f)))
            using (StringFormat una = new StringFormat(), cen = new StringFormat(), der = new StringFormat())
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;   // portadas de 80 a 110 px: basta, y cuesta menos
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                una.Trimming = StringTrimming.EllipsisCharacter; una.FormatFlags = StringFormatFlags.NoWrap; una.LineAlignment = StringAlignment.Center;
                cen.Alignment = StringAlignment.Center; cen.LineAlignment = StringAlignment.Center; cen.FormatFlags = StringFormatFlags.NoWrap;
                der.Alignment = StringAlignment.Far; der.LineAlignment = StringAlignment.Center; der.FormatFlags = StringFormatFlags.NoWrap;
                // LA CABECERA: una luna, "Lo mas pedido esta noche" y cuantas se han pedido.
                float lunaR = fCab.GetHeight() * .42f, cy = cabH * .42f;
                Luna(g, lunaR, lunaR * 1.15f, cy, lunaR);
                float hx = lunaR * 2.6f;
                using (SolidBrush b = new SolidBrush(AtrTitulo))
                    Renglon(g, "Lo más pedido esta noche", fCab, b, new RectangleF(hx, cy - fCab.GetHeight() * .6f, Math.Max(10f, cabW - hx), fCab.GetHeight() * 1.2f), una);
                using (SolidBrush b = new SolidBrush(AtrTenue))
                    g.DrawString(total + (total == 1 ? " canción pedida" : " canciones pedidas"), fSub, b,
                                 new RectangleF(hx + 2f, cy + fCab.GetHeight() * .62f, Math.Max(10f, cabW - hx), fSub.GetHeight() * 1.3f), una);
                for (int i = 0; i < filas.Count && i < TopFilas; i++)
                {
                    FilaNoche f = filas[i];
                    float y0 = filasY + i * rh;
                    using (SolidBrush b = new SolidBrush(i == 0 ? AtrAmarillo : AtrEtiqueta))
                        g.DrawString((i + 1).ToString(CultureInfo.InvariantCulture), i == 0 ? fPuesto1 : fPuesto, b, new RectangleF(0, y0, puestoW, rh), cen);
                    RectangleF rp = new RectangleF(px, y0 + (rh - lado) * .5f, lado, lado);
                    Image por = (portadas != null && i < portadas.Length) ? portadas[i] : null;
                    Tapa(g, rp, por, f);
                    // El titulo puede achicarse algo mas (hasta el 62 %): con filas altas (pantallas 5:4) su letra es grande.
                    using (SolidBrush b = new SolidBrush(AtrTitulo)) Renglon(g, f.Titulo, fTit, b, new RectangleF(tx, y0 + rh * .10f, tw, rh * .46f), una, .62f);
                    using (SolidBrush b = new SolidBrush(AtrTenue)) Renglon(g, f.Artista, fArt, b, new RectangleF(tx, y0 + rh * .54f, tw, rh * .34f), una);
                    // El carril de la barra (la barra la pone la tarjeta, creciendo).
                    using (GraphicsPath p = Redondo(new RectangleF(barraX, y0 + barraDy, barraW, barraH), barraH * .5f))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 255, 255, 255))) g.FillPath(b, p);
                    using (SolidBrush b = new SolidBrush(AtrAmbar)) g.DrawString(TextoVeces(f.Veces), fVeces, b, new RectangleF(ancho - vecesW, y0, vecesW, rh), der);
                }
            }
            return bmp;
        }
        // Una luna en creciente (la de la cabecera).
        private static void Luna(Graphics g, float r, float cx, float cy, float grosor)
        {
            using (GraphicsPath luna = new GraphicsPath())
            using (GraphicsPath corte = new GraphicsPath())
            {
                luna.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
                corte.AddEllipse(cx - r * .35f, cy - r * 1.2f, 2 * r, 2 * r);
                using (Region reg = new Region(luna))
                using (SolidBrush b = new SolidBrush(AtrAmarillo))
                {
                    reg.Exclude(corte);
                    g.FillRegion(b, reg);
                }
            }
        }
        // La portada chica de la fila, con las esquinas redondeadas; si aun no llega, una tapa del color de su genero
        // con las iniciales del artista.
        private static void Tapa(Graphics g, RectangleF r, Image portada, FilaNoche f)
        {
            float rad = Math.Max(4f, r.Width * .10f);
            using (GraphicsPath p = Redondo(r, rad))
            {
                if (portada != null)
                {
                    GraphicsState st = g.Save();
                    g.SetClip(p);
                    try { g.DrawImage(portada, r); } catch { }
                    g.Restore(st);
                    return;
                }
                Color c = f.Color;
                using (LinearGradientBrush lg = new LinearGradientBrush(r, Lighten(c, .15), Color.FromArgb(255, c.R / 2, c.G / 2, c.B / 2), 45f)) g.FillPath(lg, p);
                string ini = Iniciales(f.Artista.Length > 0 ? f.Artista : f.Titulo);
                using (Font fi = Letra(Math.Max(10f, r.Height * .34f))) TextoCentrado(g, ini, fi, Color.FromArgb(235, 255, 255, 255), r);
            }
        }
        internal static string Iniciales(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string[] ps = s.Split(new char[] { ' ', '-', '.', '_' }, StringSplitOptions.RemoveEmptyEntries);
            string r = "";
            for (int i = 0; i < ps.Length && r.Length < 2; i++) if (char.IsLetterOrDigit(ps[i][0])) r += char.ToUpperInvariant(ps[i][0]);
            return r;
        }
    }

    internal sealed partial class GpuPanel
    {
        public Func<GpuAtraccion> GetAtraccion;   // lo pone Form1 (Form1.Video.cs)
        // Los dos textos de siempre de la atraccion van en la textura de la interfaz: con la lista, se apagan con esto.
        internal float AtrUiAlfa = 1f;
        private GpuAtraccion atrCuadro;
        // Dos texturas para las fichas del escaparate (la que se ve y la que viene): cada ficha horneada se sube una vez.
        private readonly IntPtr[] vitTex = new IntPtr[2];
        private readonly int[] vitTexVer = { -1, -1 }, vitTexW = new int[2], vitTexH = new int[2];
        private readonly float[] vitTexU = new float[2], vitTexV = new float[2];
        private int atrSlotA = -1, atrSlotB = -1;
        private IntPtr msgsTex, topTex; private int msgsTexVer = -1, topTexVer = -1, msgsBw, msgsBh, topBw, topBh;
        private float msgsU, msgsV, topU, topV;
        private readonly Tarjeta.Vtx[] atrQuad = new Tarjeta.Vtx[6];
        private readonly Tarjeta.Vtx[] atrFilas = new Tarjeta.Vtx[6 * Form1.TopFilas], atrBarras = new Tarjeta.Vtx[6 * Form1.TopFilas];
        private readonly float[] brilloX = new float[Form1.TopFilas], brilloY = new float[Form1.TopFilas], brilloR = new float[Form1.TopFilas];
        private readonly uint[] brilloC = new uint[Form1.TopFilas];

        // Antes de Begin: lo que haya que subir.
        private void PreparaAtraccion()
        {
            atrCuadro = (GetAtraccion != null && sceneMode == 4) ? GetAtraccion() : new GpuAtraccion();
            AtrUiAlfa = atrCuadro.Has ? Math.Max(0f, Math.Min(1f, atrCuadro.UiAlfa)) : 1f;
            if (!atrCuadro.Has)
            {
                if (vitTex[0] != IntPtr.Zero || vitTex[1] != IntPtr.Zero || msgsTex != IntPtr.Zero || topTex != IntPtr.Zero) SueltaAtraccion();
                return;
            }
            atrSlotA = (atrCuadro.Ficha != null) ? SlotFicha(atrCuadro.Ficha, atrCuadro.FichaVer, atrCuadro.FichaBVer) : -1;
            atrSlotB = (atrCuadro.FichaB != null) ? SlotFicha(atrCuadro.FichaB, atrCuadro.FichaBVer, atrCuadro.FichaVer) : -1;
            if (atrCuadro.Msgs != null && (atrCuadro.MsgsVer != msgsTexVer || msgsTex == IntPtr.Zero))
            { msgsTex = Upload(msgsTex, ref msgsBw, ref msgsBh, atrCuadro.Msgs, out msgsU, out msgsV); msgsTexVer = atrCuadro.MsgsVer; }
            if (atrCuadro.Top && atrCuadro.TopBmp != null && (atrCuadro.TopVer != topTexVer || topTex == IntPtr.Zero))
            { topTex = Upload(topTex, ref topBw, ref topBh, atrCuadro.TopBmp, out topU, out topV); topTexVer = atrCuadro.TopVer; }
        }
        private int SlotFicha(Bitmap b, int ver, int otraVer)
        {
            for (int i = 0; i < 2; i++) if (vitTexVer[i] == ver && vitTex[i] != IntPtr.Zero) return i;
            int s = (vitTexVer[0] == otraVer && vitTex[0] != IntPtr.Zero) ? 1 : 0;
            vitTex[s] = Upload(vitTex[s], ref vitTexW[s], ref vitTexH[s], b, out vitTexU[s], out vitTexV[s]);
            vitTexVer[s] = ver;
            return s;
        }

        // Despues de la textura de la interfaz: el escaparate y la lista.
        private void DibujaAtraccion()
        {
            GpuAtraccion a = atrCuadro;
            if (!a.Has) return;
            float esc = SceneAlpha() * ControlsOpacity;
            if (a.Vitrina && a.VitrinaA > .003f)
            {
                if (atrSlotA >= 0 && a.Ficha != null) FichaQuad(atrSlotA, a.FichaX, a.FichaY, a.Ficha, a.FichaAlfa * a.VitrinaA * esc);
                if (atrSlotB >= 0 && a.FichaB != null) FichaQuad(atrSlotB, a.FichaX, a.FichaBY, a.FichaB, a.FichaBAlfa * a.VitrinaA * esc);
                if (a.Msgs != null && msgsTex != IntPtr.Zero && a.MsgA > .003f)
                {
                    if ((a.BrilloArgb >> 24) > 0)
                    {
                        stars.GlowBatchBegin(1);
                        stars.GlowBatchAdd(a.BrilloX, a.BrilloY, a.BrilloRx, a.BrilloRy, FadeArgb(a.BrilloArgb, esc));
                        stars.GlowBatchEnd();
                    }
                    float bw = a.Msgs.Width, bh = a.Msgs.Height;
                    float v0 = a.MsgFila * a.MsgH / bh * msgsV, v1 = (a.MsgFila + 1) * a.MsgH / bh * msgsV;
                    float y = a.MsgY + a.MsgDy;
                    QuadUV(atrQuad, a.MsgX, y, a.MsgX + bw, y + a.MsgH, 0f, v0, msgsU, v1, Alfa(a.MsgA * a.VitrinaA * esc));
                    stars.DrawQuad(msgsTex, atrQuad);
                }
            }
            if (a.Top && topTex != IntPtr.Zero && a.TopBmp != null)
            {
                float bw = a.TopBmp.Width, bh = a.TopBmp.Height;
                if (a.CabA > .003f)
                {
                    QuadUV(atrQuad, a.TopX, a.TopY, a.TopX + bw, a.TopY + a.CabH, 0f, 0f, topU, a.CabH / bh * topV, Alfa(a.CabA * esc));
                    stars.DrawQuad(topTex, atrQuad);
                }
                // Las filas, las barras y sus brillos: en la vuelta solo se apuntan, y luego va cada cosa en UN envio a la
                // tarjeta (las cinco filas juntas, las cinco barras juntas y los cinco brillos juntos). En Direct3D 9 lo
                // caro es la llamada, no el relleno (pruebas/envios.cs vigila que no crezcan dentro de un bucle).
                int nf = 0, nb = 0;
                for (int i = 0; i < a.Filas && a.FilaA != null && i < a.FilaA.Length && i < Form1.TopFilas; i++)
                {
                    float fa = a.FilaA[i] * esc;
                    if (fa <= .003f) continue;
                    float y0 = a.FilasY + i * a.FilaH, x = a.TopX + a.FilaDx[i], y = a.TopY + y0;
                    QuadUV(atrFilas, nf * 6, x, y, x + bw, y + a.FilaH, 0f, y0 / bh * topV, topU, (y0 + a.FilaH) / bh * topV, Alfa(fa)); nf++;
                    // La barra: crece de izquierda a derecha, de naranja a dorado, con un brillo en la punta.
                    float largo = a.BarraW * a.BarraLargo[i] * a.Barra[i];
                    if (largo < 1f) continue;
                    float bx = x + a.BarraX, by = y + a.BarraDy;
                    uint c0 = FadeArgb(0xFFC8661Au, fa), c1 = FadeArgb(MezclaArgb(0xFFC8661Au, 0xFFF2CE48u, a.Barra[i] * a.BarraLargo[i]), fa);
                    BarraEn(atrBarras, nb * 6, bx, by, largo, a.BarraH, c0, c1);
                    brilloX[nb] = bx + largo; brilloY[nb] = by + a.BarraH * .5f; brilloR[nb] = a.BarraH * 1.6f; brilloC[nb] = FadeArgb(0x70FFA040u, fa);
                    nb++;
                }
                if (nf > 0) stars.DrawTexturedBatch(topTex, atrFilas, nf * 2);
                if (nb > 0)
                {
                    stars.DrawMesh(atrBarras, nb * 2);
                    stars.GlowBatchBegin(nb);
                    for (int k = 0; k < nb; k++) stars.GlowBatchAdd(brilloX[k], brilloY[k], brilloR[k], brilloC[k]);   // apuntar no envia
                    stars.GlowBatchEnd();
                }
            }
        }
        private void FichaQuad(int s, float x, float y, Bitmap b, float alfa)
        {
            if (alfa <= .003f || vitTex[s] == IntPtr.Zero) return;
            SetTexQuadA(atrQuad, x, y, x + b.Width, y + b.Height, vitTexU[s], vitTexV[s], Alfa(alfa));
            stars.DrawQuad(vitTex[s], atrQuad);
        }
        private static uint Alfa(float a)
        {
            int v = (int)(a * 255f + .5f); if (v > 255) v = 255; else if (v < 0) v = 0;
            return ((uint)v << 24) | 0x00FFFFFFu;
        }
        internal static uint MezclaArgb(uint a, uint b, float t)
        {
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            uint r = 0;
            for (int sh = 0; sh < 32; sh += 8)
            {
                float x = (a >> sh) & 0xFF, y = (b >> sh) & 0xFF;
                r |= ((uint)(x + (y - x) * t + .5f) & 0xFFu) << sh;
            }
            return r;
        }
        // Un rectangulo de textura con su trozo (u0,v0)-(u1,v1), a partir del vertice 'o' de la tanda.
        private static void QuadUV(Tarjeta.Vtx[] v, float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1, uint argb)
        { QuadUV(v, 0, x0, y0, x1, y1, u0, v0, u1, v1, argb); }
        internal static void QuadUV(Tarjeta.Vtx[] v, int o, float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1, uint argb)
        {
            SetV(ref v[o + 0], x0, y0, u0, v0); SetV(ref v[o + 1], x1, y0, u1, v0); SetV(ref v[o + 2], x1, y1, u1, v1);
            SetV(ref v[o + 3], x0, y0, u0, v0); SetV(ref v[o + 4], x1, y1, u1, v1); SetV(ref v[o + 5], x0, y1, u0, v1);
            for (int i = 0; i < 6; i++) v[o + i].Color = argb;
        }
        // Un rectangulo de color en la tanda, de c0 a la izquierda a c1 a la derecha.
        internal static void BarraEn(Tarjeta.Vtx[] v, int o, float x, float y, float w, float h, uint c0, uint c1)
        {
            QuadUV(v, o, x, y, x + w, y + h, 0f, 0f, 0f, 0f, c0);
            v[o + 1].Color = v[o + 2].Color = v[o + 4].Color = c1;
        }
        private void SueltaAtraccion()
        {
            for (int i = 0; i < 2; i++) { if (vitTex[i] != IntPtr.Zero) { Tarjeta.Suelta(vitTex[i]); vitTex[i] = IntPtr.Zero; } vitTexVer[i] = -1; vitTexW[i] = vitTexH[i] = 0; }
            if (msgsTex != IntPtr.Zero) { Tarjeta.Suelta(msgsTex); msgsTex = IntPtr.Zero; }
            if (topTex != IntPtr.Zero) { Tarjeta.Suelta(topTex); topTex = IntPtr.Zero; }
            msgsTexVer = topTexVer = -1; msgsBw = msgsBh = topBw = topBh = 0; atrSlotA = atrSlotB = -1;
        }
    }
#endif
}
