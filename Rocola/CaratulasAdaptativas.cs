using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        // El LOD es visual. Los perfiles pedidos por el usuario ajustan la prioridad,
        // nunca desactivan las protecciones ni reducen la calidad final.
        private bool caratulasLod = true;
        private int caratulasCarga; // 0 automatico, 1 fluidez, 2 equilibrado, 3 carga rapida

        internal static int PerfilCargaCaratulas(string text)
        {
            int n;
            return int.TryParse(text, out n) && n >= 0 && n <= 3 ? n : 0;
        }

        private void AplicaPerfilCargaCaratulas()
        {
#if ROCOLA_GPU
            if (coverGovernor != null) coverGovernor.SetProfile(caratulasCarga);
#endif
        }

        private Tarjeta TarjetaCargaCaratulas(Font fNom, Font fDes, Color cTexto, Color cTenue)
        {
            Tarjeta card = new Tarjeta(); card.Size = new Size(456, 412);
            card.Titulo = "CARGA DE CARATULAS"; card.Acento = Color.FromArgb(150, 190, 255);
            string[] names = { "Automático", "Fluidez", "Equilibrado", "Carga rápida" };
            string[] descriptions = {
                "Se adapta al margen del equipo. Recomendado para empezar.",
                "Menos trabajo simultáneo: prioriza la respuesta de la pantalla.",
                "Un punto intermedio entre suavidad y velocidad de carga.",
                "Más trabajo si hay margen; retrocede si detecta saturación."
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
            Label note = new Label(); note.SetBounds(16, 312, 424, 84);
            note.Font = fDes; note.ForeColor = cTenue; note.BackColor = Color.Transparent;
            note.Text = "Solo GPU. La imagen final tiene la misma calidad en todos.\n"
                + "Los HDD y equipos de dos hilos conservan un lector.\n"
                + "Compara los perfiles en Imagen > Medir, con la misma\n"
                + "resolución y efectos. Se guarda al cerrar Ctrl+F3.";
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
        private int healthy, pressure, operations, lateStreak;
        private double frameTime, lastBackground = -1000, lastFrameCover;
        private bool backgroundCandidate;
        internal bool PendingUpload, DeferBake;
        internal volatile int ReaderLimit;
        internal volatile int ReaderPause;
        internal long RemainingTicks { get { return Math.Max(1, (long)((allowance - spent) * Stopwatch.Frequency / 1000)); } }
        private readonly double[] intervals = new double[4096];
        private int intervalCount, intervalPos;
        internal int SlowFrames, CompletedPages, StartedPages;
        internal double WorstInterval, WorstPageMs;
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
            DeferBake = PendingUpload; frameTime = now; backgroundCandidate = false;
            allowance = ProfileBudget(Budget(baseMs, fps));
            if (pressure > 0) allowance = Math.Min(allowance, .5);
        }
        internal bool BeginBackground(double now)
        {
            if (PendingUpload || pressure > 0) return false;
            spent = frameCover = 0; operations = 0; DeferBake = false;
            frameTime = now; backgroundCandidate = false;
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
            // Una operacion indivisible visible progresa; bajo presion solo una cada cuatro cuadros.
            return visible && operations == 0 && (pressure == 0 || (pressure & 3) == 0);
        }
        internal void Spend(long ticks)
        {
            double ms = Math.Max(0, ticks * 1000.0 / Stopwatch.Frequency);
            spent += ms; frameCover += ms; operations++;
            if (backgroundCandidate) { lastBackground = frameTime; backgroundCandidate = false; }
        }
        internal void Observe(double now, double work, int fps)
        {
            double interval = previousAccepted > 0 ? now - previousAccepted : work;
            previousAccepted = now;
            lateStreak = interval > 36 ? lateStreak + 1 : 0;
            double uncovered = Math.Max(.1, work - frameCover);
            lastFrameCover = frameCover;
            baseMs += (uncovered - baseMs) * (uncovered > baseMs ? .3 : .025);
            RecordInterval(interval);
            // Un mensaje aislado de WinForms no demuestra saturacion de lecturas.
            // Reaccion inmediata a trabajo caro; entre cuadros, exigir presion sostenida.
            if ((work > 30 && frameCover > 1) || (lateStreak >= 3 && baseMs > 20))
            { pressure = 12; healthy = 0; ReaderLimit = Math.Max(1, ReaderLimit / 2); }
            else
            {
                if (pressure > 0) pressure--;
                if (++healthy >= (profile == 3 ? 45 : 90))
                { ReaderLimit = Math.Min(CargaTrabajo.Lectores(processors), ReaderLimit + 1); healthy = 0; }
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
        { intervalCount = intervalPos = SlowFrames = CompletedPages = StartedPages = 0; WorstInterval = WorstPageMs = 0; previousAccepted = 0; pageKey = null; pageStart = -1; }
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
    internal sealed class GpuPreviewStore
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
        }
        private void Load()
        {
            if (loaded) return; loaded = true;
            try
            {
                if (!File.Exists(path)) return;
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader r = new BinaryReader(fs))
                {
                    if (fs.Length > 64L * 1024 * 1024 || fs.Length < 4 || r.ReadInt32() != Magic)
                    { writable = false; return; }
                    while (fs.Length - fs.Position >= RecordBytes)
                    {
                        ulong key = r.ReadUInt64(); uint crc = r.ReadUInt32(); byte[] bytes = r.ReadBytes(Bytes);
                        if (Checksum(bytes) == crc) Put(key, bytes);
                        records++;
                    }
                    if (fs.Position != fs.Length) writable = false; // cola incompleta: recuperar mediante compactacion
                }
            }
            catch { writable = false; }
        }
        internal Bitmap Get(string identity)
        {
            lock (sync)
            {
                Load(); byte[] bytes;
                if (!data.TryGetValue(Key(identity), out bytes)) return null;
                return FromBytes(bytes);
            }
        }
        internal bool Contains(string identity) { lock (sync) { Load(); return data.ContainsKey(Key(identity)); } }
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
            if (bytes == null || bytes.Length != Bytes) return;
            lock (sync)
            {
                Load(); ulong key = Key(identity);
                if (data.ContainsKey(key)) return;
                Put(key, bytes);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    if (!writable || records >= capacity + 128) { Compact(); return; }
                    using (FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (BinaryWriter w = new BinaryWriter(fs))
                    {
                        if (fs.Length == 0) w.Write(Magic);
                        w.Write(key); w.Write(Checksum(bytes)); w.Write(bytes);
                    }
                    records++;
                }
                catch { writable = false; } // cache opcional: la imagen final sigue funcionando
            }
        }
        private void Compact()
        {
            string tmp = path + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                using (BinaryWriter w = new BinaryWriter(File.Create(tmp)))
                {
                    w.Write(Magic);
                    foreach (KeyValuePair<ulong, byte[]> p in data) { w.Write(p.Key); w.Write(Checksum(p.Value)); w.Write(p.Value); }
                }
                if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
                records = data.Count; writable = true;
            }
            finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
        }
    }

    public partial class Form1
    {
        private readonly CoverGovernor coverGovernor = new CoverGovernor(CargaTrabajo.Procesadores);
        private GpuPreviewStore gpuPreviewStore;
        private readonly Dictionary<string, Bitmap> gpuPreviewCache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<string> gpuPreviewOrder = new Queue<string>();
        private int gpuPreviewPage, gpuPreviewLoaded = -1;
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
            for (int i = 0; i < keys.Count; i++)
            {
                lock (coverLock) { if (current.Gen != coverGen) return; if (gpuPreviewCache.ContainsKey(keys[i])) continue; }
                Bitmap b = null;
                try { b = store.Get(ThumbCachePath(keys[i])); } catch { }
                if (b == null) continue;
                lock (coverLock) { if (current.Gen == coverGen) PutGpuPreview(keys[i], b); else b.Dispose(); }
            }
        }
        private int AdaptiveCoverReaders()
        { return gpuEnabled ? Math.Max(1, Math.Min(coverCarriles, coverGovernor.ProfileReaders)) : coverCarriles; }
        private bool NeedsGpuPreview(Album a)
        {
            if (!gpuEnabled || !caratulasLod) return false;
            try { return !PreviewStore().Contains(ThumbCachePath(a.CoverPath)); } catch { return false; }
        }
        private void WarmGpuPreview(Album a, Image im, string identity)
        {
            if (!gpuEnabled || !caratulasLod || im == null) return;
            if (identity != ThumbCachePath(a.CoverPath)) return; // fuente reemplazada durante la lectura
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
            List<string> old = null;
            foreach (KeyValuePair<string, LodTex> p in lodTextures)
                if (all || unchecked(cellFrame - p.Value.Frame) > 4)
                { if (old == null) old = new List<string>(); old.Add(p.Key); }
            if (old == null) return;
            foreach (string key in old) { D3D9.Release(lodTextures[key].Tex); lodTextures.Remove(key); }
        }
    }
#endif
}
