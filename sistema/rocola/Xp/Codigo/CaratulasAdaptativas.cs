using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Rocola
{
    public partial class Form1
    {
        // El LOD es visual. Los perfiles pedidos por el usuario ajustan la prioridad,
        // nunca desactivan las protecciones ni reducen la calidad final.
        private bool caratulasLod = true;
        private int caratulasCarga; // 0 automatico, 1 fluidez, 2 equilibrado, 3 carga rapida
        // Ctrl+F3 -> "Caratulas en JPEG (mas rapidas)" (peticion del 11-10-2026). Es VISUAL: cambia algo la nitidez,
        // por eso lleva interruptor propio y viene APAGADO. Estatico: lo leen los guardados de miniatura, que lo son.
        internal static volatile bool caratulasJpeg;
        private readonly List<EscenaMedida> historialCaratulas = new List<EscenaMedida>();
        private event Action CoverProfileChanged;

        internal static string NombreCargaCaratulas(int profile)
        { return profile == 1 ? "Fluidez" : profile == 2 ? "Equilibrado" : profile == 3 ? "Carga rápida" : "Automático"; }

        private void RegisterCoverMeasurement(EscenaMedida measure)
        {
            if (historialCaratulas == null || measure == null) return;
            historialCaratulas.Add(measure);
            if (historialCaratulas.Count > 12) historialCaratulas.RemoveAt(0);
            if (CoverProfileChanged != null) CoverProfileChanged();
        }
        private bool CoverMeasurementStale(EscenaMedida measure)
        {
            if (measure == null) return false;
            return measure.PerfilCarga != caratulasCarga
#if ROCOLA_GPU
                || (gpuPanel != null && (measure.Ancho != gpuPanel.Width || measure.Alto != gpuPanel.Height))
#endif
                ;
        }
        private string CoverMeasurementContext(EscenaMedida measure)
        {
            return (CoverMeasurementStale(measure) ? "Anterior: " : "") + NombreCargaCaratulas(measure.PerfilCarga)
                + " · " + measure.Ancho + "×" + measure.Alto + " · " + measure.Hora.ToString("HH:mm:ss")
                + "\nGPU previas: " + measure.PreviasGpu + "/" + measure.VistasGpu + "; sin limpiar cachés.";
        }
        private Panel BuildCoverHistory(Font normal, Font small, Color text, Color muted)
        {
            Panel page = new Panel(); page.Size = new Size(920, 508); page.BackColor = Color.FromArgb(11, 14, 32);
            Label title = new Label(); title.SetBounds(16, 14, 888, 28); title.Font = normal;
            title.ForeColor = text; title.Text = "COMPARACIÓN DE CARGA DE CARÁTULAS"; page.Controls.Add(title);
            string[] headings = { "Perfil", "Resolución", "Inicio", "P95 / P99 (ms)", "FPS", "Listas/pedidas", "GPU previas", "Hora / audio" };
            int[] widths = { 130, 110, 70, 135, 60, 100, 100, 160 };
            int x = 16;
            for (int c = 0; c < headings.Length; c++) {
                Label header = new Label(); header.SetBounds(x, 54, widths[c], 22);
                header.Font = small; header.ForeColor = muted; header.Text = headings[c]; page.Controls.Add(header);
                x += widths[c];
            }
            int count = historialCaratulas == null ? 0 : historialCaratulas.Count;
            for (int i = 0; i < count; i++) {
                EscenaMedida m = historialCaratulas[count - 1 - i];
                string[] values = { NombreCargaCaratulas(m.PerfilCarga), m.Ancho + "×" + m.Alto, m.Inicio,
                    m.P95.ToString("0.0") + " / " + m.P99.ToString("0.0"), m.Fps.ToString(),
                    m.PaginasListas + "/" + m.PaginasSolicitadas, m.PreviasGpu + "/" + m.VistasGpu,
                    m.Hora.ToString("HH:mm:ss") + (m.MusicaReal ? " · música" : " · simulado") };
                Panel row = new Panel(); row.SetBounds(16, 82 + i * 30, 865, 28);
                row.BackColor = i % 2 == 0 ? Color.FromArgb(22, 28, 56) : Color.FromArgb(17, 22, 47);
                int left = 0;
                for (int c = 0; c < values.Length; c++) {
                    Label value = new Label(); value.SetBounds(left, 4, widths[c], 20);
                    value.Font = small; value.ForeColor = text; value.Text = values[c]; value.BackColor = Color.Transparent;
                    row.Controls.Add(value); left += widths[c];
                }
                page.Controls.Add(row);
            }
            Label note = new Label(); note.SetBounds(16, 456, 888, 48); note.Font = small; note.ForeColor = muted;
            note.Text = "Últimas 12 pruebas de esta sesión, con efectos altos. GPU previas: carátulas listas al primer encuentro.\n"
                + "Compara el mismo inicio, resolución y ajustes; la música y las cachés influyen. No se borran cachés.";
            page.Controls.Add(note); return page;
        }
        private void ShowCoverHistory()
        {
            using (Form dialog = new Form())
            using (Font normal = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (Font small = new Font("Segoe UI", 8.5f))
            {
                dialog.Text = "Carga de carátulas · Comparación"; dialog.ClientSize = new Size(920, 548);
                dialog.StartPosition = FormStartPosition.CenterParent; dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = dialog.MinimizeBox = false; dialog.BackColor = Color.FromArgb(11, 14, 32);
                dialog.Controls.Add(BuildCoverHistory(normal, small, Color.FromArgb(232, 238, 255), Color.FromArgb(150, 165, 200)));
                BotonPlano close = new BotonPlano(); close.SetBounds(736, 510, 168, 30); close.Font = normal; close.Text = "Cerrar";
                close.Click += delegate { dialog.Close(); }; dialog.Controls.Add(close);
                dialog.KeyPreview = true; dialog.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) dialog.Close(); };
                MonedaTambienAqui(dialog);   // una moneda con la ventana abierta tambien cuenta (Form1.Ajustes.cs)
                dialog.ShowDialog(this);
            }
        }

        internal static int PerfilCargaCaratulas(string text)
        {
            int n;
            return int.TryParse(text, out n) && n >= 0 && n <= 3 ? n : 0;
        }

        private void AplicaPerfilCargaCaratulas()
        {
#if ROCOLA_GPU
            if (coverGovernor != null) coverGovernor.SetProfile(caratulasCarga);
            EnsureCoverReaders();
#endif
            if (CoverProfileChanged != null) CoverProfileChanged();
        }

        private Tarjeta TarjetaCargaCaratulas(Font fNom, Font fDes, Color cTexto, Color cTenue)
        {
            Tarjeta card = new Tarjeta(); card.Size = new Size(456, 412);
            card.Titulo = "CARGA DE CARATULAS"; card.Acento = Color.FromArgb(150, 190, 255);
            string[] names = { "Automático", "Fluidez", "Equilibrado", "Carga rápida" };
            string[] descriptions = {
                "Se adapta al margen del equipo. Recomendado para empezar.",
                "Menos trabajo a la vez y una página de colchón: equipos débiles.",
                "Un punto intermedio entre suavidad y velocidad de carga.",
                "Más trabajo y páginas listas si hay margen; retrocede si satura."
            };
            BotonPlano[] buttons = new BotonPlano[4];
            Action paint = delegate {
                for (int j = 0; j < buttons.Length; j++) {
                    bool selected = j == caratulasCarga;
                    buttons[j].Relleno = selected ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                    buttons[j].Borde = selected ? card.Acento : Color.FromArgb(60, 72, 118);
                    buttons[j].ForeColor = selected ? cTexto : cTenue;
                    buttons[j].Invalidate();
                }
            };
            for (int i = 0; i < buttons.Length; i++) {
                int k = i, y = 44 + i * 66;
                buttons[i] = new BotonPlano(); buttons[i].SetBounds(16, y, 424, 30);
                buttons[i].Font = fNom; buttons[i].Text = names[i]; buttons[i].AccessibleName = "Carga de carátulas: " + names[i];
                buttons[i].Click += delegate { caratulasCarga = k; paint(); AplicaPerfilCargaCaratulas(); f3CambioGraf = true; };
                Label description = new Label(); description.SetBounds(16, y + 34, 424, 26);
                description.Font = fDes; description.ForeColor = cTenue; description.BackColor = Color.Transparent;
                description.Text = descriptions[i]; card.Controls.Add(buttons[i]); card.Controls.Add(description);
            }
            Interruptor jpeg = new Interruptor(); jpeg.SetBounds(16, 306, 424, 26);
            jpeg.Font = fNom; jpeg.ForeColor = cTexto; jpeg.Acento = card.Acento;
            jpeg.Text = "Carátulas en JPEG (más rápidas)"; jpeg.Checked = caratulasJpeg;
            jpeg.AccessibleName = "Carátulas en JPEG";
            jpeg.CheckedChanged += delegate { caratulasJpeg = jpeg.Checked; f3CambioGraf = true; };
            Label jpegNota = new Label(); jpegNota.SetBounds(16, 334, 424, 32);
            jpegNota.Font = fDes; jpegNota.ForeColor = cTenue; jpegNota.BackColor = Color.Transparent;
            jpegNota.Text = "6 veces menos disco: más páginas listas, también en disco duro.\nAlgo menos nitidez en letras y bordes. Las transparentes, sin pérdida.";
            card.Controls.Add(jpeg); card.Controls.Add(jpegNota);
            Label note = new Label(); note.SetBounds(16, 370, 424, 34);
            note.Font = fDes; note.ForeColor = cTenue; note.BackColor = Color.Transparent;
            note.Text = "Solo GPU. Los perfiles no cambian la calidad de las carátulas.\n"
                + "HDD y dos hilos: un solo lector. Compara en Imagen > Medir.";
            card.Controls.Add(note); paint();
            return card;
        }
    }
#if ROCOLA_GPU
    // Un presupuesto para composicion y subidas, aprendiendo sin clasificar un PC para siempre.
    // Los intervalos incluyen Present y el trabajo entre cuadros; el coste excluye la espera del monitor.
    internal sealed class CoverGovernor
    {
        private readonly int processors;
        private volatile int profile;
        internal int ProfileReaders {
            get {
                int max = CargaTrabajo.Lectores(processors), p = profile;
                if (p == 1) max = Math.Min(max, 2);
                else if (p == 2) max = Math.Max(1, (max + 1) / 2);
                return Math.Max(1, Math.Min(ReaderLimit, max));
            }
        }
        internal void SetProfile(int value) { profile = value >= 0 && value <= 3 ? value : 0; }
        private double ProfileBudget(double automatic)
        {
            if (profile == 0) return automatic; // predeterminado: regulador original intacto
            double factor = profile == 1 ? .55 : profile == 2 ? .85 : profile == 3 ? 1.25 : 1;
            return Math.Max(.35, Math.Min(12, Math.Min(automatic * factor, Math.Max(.35, (28 - baseMs) * .65))));
        }
        private double baseMs = 1, allowance, spent, frameCover, previousAccepted;
        private int pressure, operations;
        private double healthyMs, lateMs, adaptAt = -1, pressureUntil = -1, visibleEscapeAt = -1000;
        private double frameTime, lastBackground = -1000, lastFrameCover;
        private bool backgroundCandidate, visibleCandidate;
        internal bool PendingUpload, DeferBake;
        internal volatile int ReaderLimit;
        internal volatile int ReaderPause;
        internal long RemainingTicks { get { return Math.Max(1, (long)((allowance - spent) * Stopwatch.Frequency / 1000)); } }
        private readonly double[] intervals = new double[4096];
        private int intervalCount, intervalPos;
        internal int SlowFrames, CompletedPages, StartedPages;
        internal double WorstInterval, WorstPageMs;
        private readonly HashSet<string> firstSeen = new HashSet<string>();
        internal int SeenCovers, ReadyAtFirstSeen;
        internal void VisitCover(string key, bool ready)
        {
            if (key == null || firstSeen.Count >= 2048 || !firstSeen.Add(key)) return;
            SeenCovers++; if (ready) ReadyAtFirstSeen++;
        }
        private string pageKey;
        private double pageStart;
        internal CoverGovernor(int cpu) { processors = Math.Max(1, cpu); ReaderLimit = 1; }
        internal static double Budget(double baseline, int fps)
        {
            if (double.IsNaN(baseline) || double.IsInfinity(baseline)) return .35;
            // El tope de FPS no puede hacer imposible una operacion indivisible de GDI.
            // Durante una carga se admite una fraccion de un cuadro de 60 Hz; en reposo
            // el render conserva su tope original (no se limita el equipo a 60/30 FPS).
            double target = Math.Min(33.333, Math.Max(16.667, 1000.0 / Math.Max(30, Math.Min(400, fps))));
            return Math.Max(.35, Math.Min(12, (target * .85 - Math.Max(0, baseline)) * .65));
        }
        internal void Begin(double now, int fps)
        {
            spent = frameCover = 0; operations = 0;
            DeferBake = PendingUpload; frameTime = now; backgroundCandidate = visibleCandidate = false;
            allowance = ProfileBudget(Budget(baseMs, fps));
            if (pressure > 0) allowance = Math.Min(allowance, .5);
        }
        internal bool BeginBackground(double now)
        {
            if (PendingUpload || pressure > 0) return false;
            spent = frameCover = 0; operations = 0; DeferBake = false;
            frameTime = now; backgroundCandidate = visibleCandidate = false;
            allowance = Math.Min(processors <= 2 ? 3 : 12, ProfileBudget(Math.Max(.35,
                Math.Min(processors <= 2 ? 3 : 12, (28 - baseMs - lastFrameCover) * .45))));
            return true;
        }
        internal bool CanStart(long ticks, bool visible, bool preview)
        {
            double cost = Math.Max(.01, ticks * 1000.0 / Stopwatch.Frequency);
            double limit = allowance * (preview ? 1 : .8); // reservar una franja para previews baratos
            if (spent + cost <= limit) return true;
            // Una celda puede costar mas que el intervalo de 200 FPS. No dejar la precarga
            // bloqueada para siempre: admitir una pieza ocasional con margen bajo 30 FPS.
            double cadence = profile == 1 ? 2 : profile == 2 ? 1.25 : profile == 3 ? .75 : 1;
            if (!visible && spent < .75 && pressure == 0
                && frameTime - lastBackground >= Math.Max(processors <= 2 ? 40 : 10, (processors <= 2 ? 40 : 16) * cadence)
                && baseMs + spent + cost < 28)
            { backgroundCandidate = true; return true; }
            // Progresar sin depender del numero de cuadros. Bajo presion, espaciar
            // las piezas indivisibles visibles unos 66 ms; no bloquearlas para siempre.
            if (visible && operations == 0 && (pressure == 0 || frameTime - visibleEscapeAt >= 66))
            { visibleCandidate = true; return true; }
            return false;
        }
        internal void Spend(long ticks)
        {
            double ms = Math.Max(0, ticks * 1000.0 / Stopwatch.Frequency);
            spent += ms; frameCover += ms; operations++;
            if (backgroundCandidate) { lastBackground = frameTime; backgroundCandidate = false; }
            if (visibleCandidate) { visibleEscapeAt = frameTime; visibleCandidate = false; }
        }
        internal void Observe(double now, double work, int fps)
        {
            double interval = previousAccepted > 0 ? now - previousAccepted : work;
            previousAccepted = now;
            double elapsed = adaptAt < 0 ? Math.Min(50, Math.Max(0, work)) : Math.Min(50, Math.Max(0, now - adaptAt));
            adaptAt = now; // no acumular minutos de salud si Windows estuvo suspendido
            lateMs = interval > 36 ? lateMs + elapsed : 0;
            double uncovered = Math.Max(.1, work - frameCover);
            lastFrameCover = frameCover;
            double alpha = 1 - Math.Pow(uncovered > baseMs ? .7 : .975, elapsed / 16.667);
            baseMs += (uncovered - baseMs) * alpha;
            RecordInterval(interval);
            // Un mensaje aislado de WinForms no demuestra saturacion de lecturas.
            // Reaccion inmediata a trabajo caro; entre cuadros, exigir presion sostenida.
            if ((work > 30 && frameCover > 1) || (lateMs >= 100 && baseMs > 20))
            { pressure = 1; pressureUntil = now + 200; healthyMs = 0; ReaderLimit = Math.Max(1, ReaderLimit / 2); }
            else
            {
                if (pressure > 0 && now >= pressureUntil) pressure = 0;
                if (pressure == 0)
                {
                    healthyMs += elapsed;
                    double step = profile == 3 ? 375 : 750;
                    if (healthyMs >= step)
                    { ReaderLimit = Math.Min(CargaTrabajo.Lectores(processors), ReaderLimit + 1); healthyMs -= step; }
                }
            }
            ReaderPause = pressure > 0 ? (processors <= 2 ? 16 : 8) : 0;
        }
        // Intervalo entre Present aceptados: registra tambien mensajes/entrada entre dos cuadros.
        internal void RecordInterval(double ms)
        {
            if (double.IsNaN(ms) || double.IsInfinity(ms) || ms <= 0) return;
            intervals[intervalPos++ & 4095] = ms; intervalCount = Math.Min(4096, intervalCount + 1);
            if (ms > 33.333) SlowFrames++;
            WorstInterval = Math.Max(WorstInterval, ms);
        }
        internal double Percentile(double fraction)
        {
            if (intervalCount == 0) return 0;
            double[] copy = new double[intervalCount]; Array.Copy(intervals, copy, intervalCount); Array.Sort(copy);
            return copy[Math.Min(copy.Length - 1, (int)Math.Ceiling(copy.Length * fraction) - 1)];
        }
        internal void ResetMeasure()
        { intervalCount = intervalPos = SlowFrames = CompletedPages = StartedPages = SeenCovers = ReadyAtFirstSeen = 0;
            firstSeen.Clear(); WorstInterval = WorstPageMs = 0; previousAccepted = 0; pageKey = null; pageStart = -1; }
        internal void Page(string key, bool ready, double now)
        {
            if (pageKey != key) { pageKey = key; pageStart = now; StartedPages++; }
            FinishPage(ready, now);
        }
        internal void FinishPage(bool ready, double now)
        {
            if (ready && pageStart >= 0)
            { WorstPageMs = Math.Max(WorstPageMs, now - pageStart); CompletedPages++; pageStart = -1; }
        }
    }

    // Previews 32x32 en un archivo secuencial. No 18 aperturas JPEG por pagina ni I/O en UI.
    // La clave es la identidad de la miniatura (ruta + longitud + fecha de la fuente), no solo su nombre.
    internal sealed partial class GpuPreviewStore
    {
        internal const int Side = 32, Bytes = Side * Side * 4;
        private const int Magic = 0x32444F4C, RecordBytes = 8 + 4 + Bytes;
        private readonly string path;
        private readonly int capacity;
        private readonly object sync = new object();
        private readonly Dictionary<ulong, byte[]> data = new Dictionary<ulong, byte[]>();
        private readonly Queue<ulong> order = new Queue<ulong>();
        private bool loaded, writable = true;
        private long records;
        internal GpuPreviewStore(string file, int cap) { path = file; capacity = Math.Max(18, Math.Min(8192, cap)); }
        internal static ulong Key(string identity)
        { return ulong.Parse(Path.GetFileName(identity).Substring(0, 16), System.Globalization.NumberStyles.HexNumber); }
        private static uint Checksum(byte[] pixels)
        { uint h = 2166136261; for (int i = 0; i < pixels.Length; i++) h = unchecked((h ^ pixels[i]) * 16777619); return h; }
        private void Put(ulong key, byte[] bytes)
        {
            if (!data.ContainsKey(key)) order.Enqueue(key);
            data[key] = bytes;
            while (data.Count > capacity) data.Remove(order.Dequeue());
            // El indice puede expulsar entradas crudas fuera de orden. Acotar tambien sus
            // nodos FIFO obsoletos, no solo el diccionario de pixeles.
            if (order.Count > capacity * 2) {
                HashSet<ulong> alive = new HashSet<ulong>(); int count = order.Count;
                for (int i = 0; i < count; i++) { ulong k = order.Dequeue(); if (data.ContainsKey(k) && alive.Add(k)) order.Enqueue(k); }
            }
        }
        private void Load()
        {
            IndexedLoad();
        }
        internal Bitmap Get(string identity)
        {
            lock (sync)
            {
                Load(); byte[] bytes = ReadPixels(Key(identity), null);
                if (bytes == null) return null;
                return FromBytes(bytes);
            }
        }
        internal bool Contains(string identity) { lock (sync) { Load(); return ReadPixels(Key(identity), null) != null; } }
        internal static Bitmap Small(Image source)
        {
            Bitmap b = new Bitmap(Side, Side, PixelFormat.Format32bppPArgb);
            try { using (Graphics g = Graphics.FromImage(b)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(source, 0, 0, Side, Side); } return b; }
            catch { b.Dispose(); throw; }
        }
        internal static byte[] ToBytes(Bitmap b)
        {
            byte[] bytes = new byte[Bytes];
            BitmapData bd = b.LockBits(new Rectangle(0, 0, Side, Side), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try { for (int y = 0; y < Side; y++) Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + y * (long)bd.Stride), bytes, y * Side * 4, Side * 4); }
            finally { b.UnlockBits(bd); } return bytes;
        }
        private static Bitmap FromBytes(byte[] bytes)
        {
            Bitmap b = new Bitmap(Side, Side, PixelFormat.Format32bppPArgb);
            try
            {
                BitmapData bd = b.LockBits(new Rectangle(0, 0, Side, Side), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                try { for (int y = 0; y < Side; y++) Marshal.Copy(bytes, y * Side * 4, new IntPtr(bd.Scan0.ToInt64() + y * (long)bd.Stride), Side * 4); }
                finally { b.UnlockBits(bd); } return b;
            }
            catch { b.Dispose(); throw; }
        }
        internal void Save(string identity, byte[] bytes)
        {
            IndexedSave(identity, bytes);
        }
        private void Compact()
        {
            IndexedCompact();
        }
    }

    public partial class Form1
    {
        private readonly CoverGovernor coverGovernor = new CoverGovernor(CargaTrabajo.Procesadores);
        private GpuPreviewStore gpuPreviewStore;
        private readonly Dictionary<string, Bitmap> gpuPreviewCache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<string> gpuPreviewOrder = new Queue<string>();
        private int gpuPreviewPage, gpuPreviewLoaded = -1;
        private bool coverWorkersStop;
        private int coverWorkersStarted;
        // Llamado solo por los trabajadores de caratulas, dentro del mismo turno de disco.
        private GpuPreviewStore PreviewStore()
        {
            lock (coverLock)
            {
                if (gpuPreviewStore == null) gpuPreviewStore = new GpuPreviewStore(
                    Path.Combine(CarpetaMiniaturas(), "previews32-v1.bin"), CargaTrabajo.Procesadores <= 2 ? 1024 : 4096);
                return gpuPreviewStore;
            }
        }
        private void PutGpuPreview(string key, Bitmap b)
        {
            Bitmap old;
            if (gpuPreviewCache.TryGetValue(key, out old)) { if (b != old) coverTrash.Add(b); return; }
            gpuPreviewCache[key] = b; gpuPreviewOrder.Enqueue(key);
            while (gpuPreviewCache.Count > 256)
            { string k = gpuPreviewOrder.Dequeue(); coverTrash.Add(gpuPreviewCache[k]); gpuPreviewCache.Remove(k); }
        }
        private Image PeekGpuPreview(Album a)
        {
            if (!caratulasLod || a == null || a.CoverPath == null) return null;
            lock (coverLock) { Bitmap b; return gpuPreviewCache.TryGetValue(a.CoverPath, out b) ? b : null; }
        }
        private void LoadGpuPreviewBatch(CoverJob current)
        {
            if (!gpuEnabled || !caratulasLod) return;
            List<string> keys = new List<string>();
            lock (coverLock)
            {
                if (gpuPreviewLoaded == gpuPreviewPage) return;
                gpuPreviewLoaded = gpuPreviewPage; keys.Add(current.Key);
                foreach (CoverJob job in coverQHi) { if (keys.Count >= 18) break; keys.Add(job.Key); }
                foreach (CoverJob job in coverQLo) { if (keys.Count >= 54) break; keys.Add(job.Key); }
            }
            GpuPreviewStore store = PreviewStore();
            string[] identities = new string[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                lock (coverLock) { if (current.Gen != coverGen) return; if (gpuPreviewCache.ContainsKey(keys[i])) continue; }
                try { identities[i] = ThumbCachePath(keys[i]); } catch { }
            }
            Bitmap[] images = store.GetBatch(identities);
            for (int i = 0; i < images.Length; i++)
            {
                Bitmap b = images[i];
                if (b == null) continue;
                lock (coverLock) { if (current.Gen == coverGen) PutGpuPreview(keys[i], b); else b.Dispose(); }
            }
        }
        private int AdaptiveCoverReaders()
        { return gpuEnabled ? Math.Max(1, Math.Min(coverCarriles, coverGovernor.ProfileReaders)) : coverCarriles; }

        // Solo GPU. Bajo coverLock: el cupo global incluye las lecturas antiguas
        // en vuelo; cambiar de perfil o F5 no permite duplicarlas ni las cancela.
        private int ActiveCoverReaders()
        { int n = 0; foreach (int value in coverActiveUnits.Values) n += value; return n; }

        private int WantedCoverReaders()
        {
            int cap = AdaptiveCoverReaders(), active = ActiveCoverReaders();
            if (active >= cap) return cap;
            Dictionary<string, int> admitted = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int wanted = active;
            foreach (LinkedList<CoverJob> jobs in new LinkedList<CoverJob>[] { coverQHi, coverQLo })
                foreach (CoverJob job in jobs)
                {
                    string unit = UnidadDe(job.Key); int running, extra;
                    coverActiveUnits.TryGetValue(unit, out running); admitted.TryGetValue(unit, out extra);
                    if (running + extra >= (DiscoLento(job.Key) ? 1 : cap)) continue;
                    admitted[unit] = extra + 1;
                    if (++wanted >= cap) return cap;
                }
            return wanted;
        }
        private void EnsureCoverReaders()
        {
            if (!gpuEnabled || coverGovernor == null || coverLock == null || coverWorkersStop || startupClosing || IsDisposed || Disposing) return;
            lock (coverLock)
            {
                if (coverQHi.Count != 0 || coverQLo.Count != 0) System.Threading.Monitor.PulseAll(coverLock);
                if (coverPumpCount >= AdaptiveCoverReaders() || (coverQHi.Count == 0 && coverQLo.Count == 0)) return;
                int wanted = WantedCoverReaders();
                while (coverPumpCount < wanted)
                {
                    System.Threading.Thread t = new System.Threading.Thread(CoverPump);
                    t.IsBackground = true; t.Priority = System.Threading.ThreadPriority.BelowNormal;
                    coverPumpCount++;
                    try { t.Start(); coverWorkersStarted++; } catch { coverPumpCount--; break; }
                }
            }
        }
        private void StopCoverReaders()
        {
            if (!gpuEnabled) return;
            lock (coverLock) {
                coverWorkersStop = true; coverGen++;
                coverQHi.Clear(); coverQLo.Clear(); coverLoading.Clear();
                System.Threading.Monitor.PulseAll(coverLock);
            }
        }
        private bool NeedsGpuPreview(Album a)
        {
            if (!gpuEnabled || !caratulasLod) return false;
            try { return !PreviewStore().Contains(ThumbCachePath(a.CoverPath)); } catch { return false; }
        }
        private void WarmGpuPreview(Album a, Image im, string identity)
        {
            if (!gpuEnabled || !caratulasLod || im == null) return;
            if (identity != ThumbCachePath(a.CoverPath)) return; // fuente reemplazada durante la lectura
            // Save no sobrescribe un registro que ya existe (IndexedSave sale si ReadPixels lo encuentra): si el almacen ya tiene esta identidad,
            // reducir la imagen a 32x32 y pasarla a bytes solo servia para tirar el resultado (20-10-2026). La identidad lleva el tamano y la fecha del archivo.
            if (PreviewStore().Contains(identity)) return;
            using (Bitmap small = GpuPreviewStore.Small(im))
                PreviewStore().Save(identity, GpuPreviewStore.ToBytes(small));
        }
    }

    internal sealed partial class GpuPanel
    {
        internal CoverGovernor CoverControl;
        private sealed class LodTex { internal IntPtr Tex; internal Bitmap Bmp; internal float U, V; internal int Frame; internal double FullAt = -1; }
        private readonly Dictionary<string, LodTex> lodTextures = new Dictionary<string, LodTex>();
        private readonly IntPtr[] lodTex = new IntPtr[MaxCells];
        private readonly float[] lodU = new float[MaxCells], lodV = new float[MaxCells], lodAlpha = new float[MaxCells];
        private void PrepareLod(int i, bool full)
        {
            lodTex[i] = IntPtr.Zero; lodAlpha[i] = 0;
            GpuGridCell cell = gridBuf[i];
            if (cell.Preview == null || cell.Key == null) return;
            LodTex l; lodTextures.TryGetValue(cell.Key, out l);
            if (full && l == null) return;
            if (l == null || l.Bmp != cell.Preview || l.Tex == IntPtr.Zero)
            {
                if (l == null && lodTextures.Count >= 144 && !DiscardOldLod()) return;
                if (CoverControl != null && !CoverControl.CanStart(Stopwatch.Frequency / 10000, true, true)) return;
                long start = Stopwatch.GetTimestamp(); float u, v;
                IntPtr tex = stars.CreateOverlay(cell.Preview, out u, out v);
                if (CoverControl != null) CoverControl.Spend(Stopwatch.GetTimestamp() - start);
                if (tex == IntPtr.Zero) return;
                if (l != null && l.Tex != IntPtr.Zero) D3D9.Release(l.Tex);
                l = new LodTex { Tex = tex, Bmp = cell.Preview, U = u, V = v }; lodTextures[cell.Key] = l;
            }
            l.Frame = cellFrame;
            if (full && l.FullAt < 0) l.FullAt = AhoraMs();
            if (!full) l.FullAt = -1;
            float alpha = l.FullAt < 0 ? 1 : Math.Max(0, 1 - (float)((AhoraMs() - l.FullAt) / 140));
            lodTex[i] = l.Tex; lodU[i] = l.U; lodV[i] = l.V; lodAlpha[i] = alpha;
        }
        private bool UploadedCover(GpuGridCell cell)
        {
            if (cell.Direct) return NativeCoverReady(cell);
            CellTex t = null;
            if (cell.Key != null) cellTexCache.TryGetValue(cell.Key, out t);
            else if (cell.Slot >= 0 && cell.Slot < slotTex.Length) t = slotTex[cell.Slot];
            return t != null && t.Tex != IntPtr.Zero && t.HasCover;
        }
        private void DrawLod(bool underlay)
        {
            for (int i = 0; i < gridN; i++)
            {
                if (lodTex[i] == IntPtr.Zero || lodAlpha[i] <= 0) continue;
                GpuGridCell c = gridBuf[i]; if (c.Sel) continue; // su preview ya esta en el cubo que gira
                bool queued = c.Key != null && c.Key.EndsWith("#q", StringComparison.Ordinal);
                if (queued != underlay || (queued && UploadedCover(c))) continue; // nunca tapar el palomito
                RectangleF r = c.CoverRect;
                if (r.Right <= 0 || r.Left >= Width) continue;
                stars.DrawCube(lodTex[i], lodU[i], lodV[i], r.X, r.Y, r.Width, r.Height, 0,
                    Math.Max(0, Math.Min(1, c.A * WallOpacity * lodAlpha[i])), 0);
            }
        }
        private void TrimLod(bool all)
        {
            if (all) {
                foreach (LodTex texture in lodTextures.Values) D3D9.Release(texture.Tex);
                lodTextures.Clear(); return;
            }
            // Conservar seis paginas de siluetas (432 KiB a 32x32). Antes se destruian
            // tras cuatro cuadros y habia que volver a subirlas al retroceder.
            while (lodTextures.Count > 108) if (!DiscardOldLod()) break;
        }
        private bool DiscardOldLod()
        {
            string victim = null; int oldest = 4;
            foreach (KeyValuePair<string, LodTex> row in lodTextures) {
                int age = unchecked(cellFrame - row.Value.Frame);
                if (age > oldest) { oldest = age; victim = row.Key; }
            }
            if (victim == null || !ReleaseCoverTexture(lodTextures[victim].Tex)) return false;
            lodTextures.Remove(victim); return true;
        }
    }
#endif
}
