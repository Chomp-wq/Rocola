using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
class CristalCache
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks, readsNew, readsOld;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Set(object o, string name, object v) { o.GetType().GetField(name, F).SetValue(o, v); }
    static object Get(object o, string name) { return o.GetType().GetField(name, F).GetValue(o); }
    static object Call(object o, string name, params object[] args) { return o.GetType().GetMethod(name, F).Invoke(o, args); }
    static IDisposable Capture(Control c, object group)
    {
        Type capture = c.GetType().DeclaringType.GetNestedType("CapturaCintas", F);
        return (IDisposable)Activator.CreateInstance(capture, F, null, new object[] { group }, null);
    }
    static void Equal(Bitmap a, Bitmap b, string name)
    {
        Check(a.Size == b.Size, name + " dimensiones");
        Rectangle r = new Rectangle(Point.Empty, a.Size);
        BitmapData ad = a.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        BitmapData bd = b.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] ar = new byte[a.Width * 4], br = new byte[b.Width * 4];
            for (int y = 0; y < a.Height; y++)
            {
                Marshal.Copy(new IntPtr(ad.Scan0.ToInt64() + y * ad.Stride), ar, 0, ar.Length);
                Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + y * bd.Stride), br, 0, br.Length);
                for (int x = 0; x < ar.Length; x++)
                    if (ar[x] != br[x]) throw new Exception(name + " pixel distinto: " + (x / 4) + "," + y + " canal " + (x % 4) + ": " + ar[x] + " / " + br[x]);
            }
        }
        finally { a.UnlockBits(ad); b.UnlockBits(bd); }
        checks++;
    }
    // Referencia del algoritmo anterior: repintar la pagina completa bajo un clip para borrar la fila.
    static Bitmap LegacyRow(Control c, ref int previous)
    {
        int w = c.Width, h = (int)Get(c, "RowH"), sel = (int)Get(c, "Sel"), start = (int)Get(c, "PageStart");
        Bitmap result = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        try
        {
            using (Bitmap raw = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(raw))
            using (Graphics output = Graphics.FromImage(result))
            using (ImageAttributes key = new ImageAttributes())
            {
                Set(c, "gpuFilaY", (sel - start) * h);
                using (Capture(c, null))
                {
                    Call(c, "PintaPagina", g, sel);
                    using (Graphics snapshot = Graphics.FromImage((Bitmap)Get(c, "hornoPag")))
                    {
                        if (previous >= start && previous < start + (int)c.GetType().GetProperty("RowsPerPage", F).GetValue(c, null))
                        {
                            snapshot.SetClip(new Rectangle(0, (previous - start) * h, w, h));
                            Set(c, "Sel", -1); Call(c, "PintaPagina", snapshot, start); Set(c, "Sel", sel);
                        }
                        snapshot.ResetClip(); snapshot.DrawImageUnscaled(raw, 0, (sel - start) * h);
                    }
                }
                if (c.GetType().DeclaringType.GetField("capturandoCintas", F).GetValue(null) != null)
                    Call(c, "PintaPagina", g, sel);
                previous = sel;
                key.SetColorKey(c.BackColor, c.BackColor, ColorAdjustType.Bitmap);
                output.DrawImage(raw, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, key);
            }
            return result;
        }
        catch { result.Dispose(); throw; }
        finally { Set(c, "Sel", sel); Set(c, "gpuFilaY", 0); }
    }
    static string Title(int i) { return "Canción " + i + " · Б 世界 / Enjoy the Silence con título largo para verificar el recorte"; }
    // Las dos rutas reciben exactamente los mismos sprites. No comparamos dos horneados
    // independientes de glifos GDI+ (redondean distinto ocasionalmente), sino su composición.
    static void SameSprites(Control source, Control target)
    {
        foreach (string name in new string[] { "bmpCirc", "bmpCircCola", "bmpCircSon" })
        {
            ((Bitmap)Get(target, name)).Dispose(); Set(target, name, ((Bitmap)Get(source, name)).Clone());
        }
        foreach (string name in new string[] { "bmpDig", "bmpDigCola", "bmpDigSon" })
        {
            Bitmap[] originals = (Bitmap[])Get(source, name), copies = (Bitmap[])Get(target, name);
            for (int i = 0; i < copies.Length; i++) { copies[i].Dispose(); copies[i] = (Bitmap)originals[i].Clone(); }
        }
        foreach (string name in new string[] { "hornoGen", "hornoPastV", "hornoPastA" })
        {
            IDictionary from = (IDictionary)Get(source, name), to = (IDictionary)Get(target, name);
            foreach (Bitmap b in to.Values) b.Dispose(); to.Clear();
            foreach (DictionaryEntry entry in from) to.Add(entry.Key, ((Bitmap)entry.Value).Clone());
        }
    }
    static void Configure(Control c, int width, int row, Font font, bool modern)
    {
        c.Size = new Size(width, row * 9 + 17); c.Font = font; c.BackColor = Color.FromArgb(9, 13, 24);
        Set(c, "RowH", row); Set(c, "Accent", Color.CornflowerBlue); Set(c, "Sel", 0);
        Set(c, "GetCount", new Func<int>(delegate { return 80; }));
        Set(c, "GetText", new Func<int, string>(delegate(int i) { if (modern) readsNew++; else readsOld++; return Title(i); }));
        Set(c, "GetRight", new Func<int, string>(delegate(int i) { return "4:36"; }));
        Set(c, "GetGenero", new Func<int, string>(delegate(int i) { return i % 2 == 0 ? "Rock" : "Pop"; }));
        Set(c, "GetQueuePos", new Func<int, int>(delegate(int i) { return i % 3 == 0 ? 2 : 0; }));
        Set(c, "GetSonando", new Func<int, bool>(delegate(int i) { return i == 4; }));
    }
    [STAThread] static int Main(string[] args)
    {
        Type list = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1").GetNestedType("SongListView", F);
        foreach (int width in new int[] { 480, 640, 1200, 2300 })
        foreach (int row in new int[] { 44, 64, 80 })
        foreach (int features in new int[] { 0, 7 })
        using (Control modern = (Control)Activator.CreateInstance(list, true))
        using (Control legacy = (Control)Activator.CreateInstance(list, true))
        using (Font font = new Font("Tahoma", row * .34f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (IDisposable modernGroup = (IDisposable)Activator.CreateInstance(list.Assembly.GetType("Rocola.CintasTexto"), true))
        using (IDisposable legacyGroup = (IDisposable)Activator.CreateInstance(list.Assembly.GetType("Rocola.CintasTexto"), true))
        {
            Configure(modern, width, row, font, true); Configure(legacy, width, row, font, false);
            Set(modern, "Vivo", features); Set(legacy, "Vivo", features);
            foreach (Control c in new Control[] { modern, legacy }) Set(c, "GetEspera", new Func<int, int>(delegate(int pos) { return 9; }));
            using (Bitmap b = (Bitmap)Call(modern, "BakeGpuListLayer", false)) { }
            using (Bitmap b = (Bitmap)Call(legacy, "BakeGpuListLayer", false)) { }
            SameSprites(modern, legacy);
            int previous = -1; Bitmap scratch = null, backup = null; ImageAttributes key = null;
            for (int phase = 0; phase < 3; phase++)
            {
                if (phase > 0)
                {
                    Call(modern, "StartGpuSlide", 1, phase == 2); Call(legacy, "StartGpuSlide", 1, phase == 2);
                    Equal((Bitmap)Get(modern, "hornoAnt"), (Bitmap)Get(legacy, "hornoAnt"), "pagina saliente");
                }
                Color color = phase == 0 ? Color.CornflowerBlue : phase == 1 ? Color.HotPink : Color.DarkSeaGreen;
                foreach (Control c in new Control[] { modern, legacy })
                {
                    Set(c, "PageStart", phase * 9); Set(c, "Sel", phase * 9);
                    Set(c, "Accent", color); c.BackColor = Color.FromArgb(7 + phase * 10, 13, 24);
                }
                using (Bitmap a = (Bitmap)Call(modern, "BakeGpuListLayer", false))
                using (Bitmap b = (Bitmap)Call(legacy, "BakeGpuListLayer", false)) Equal(a, b, "pagina");
                previous = -1;
                foreach (int rowIndex in new int[] { 0, 1, 8, 4, 4, 2, 0, 7, 3 })
                {
                    Set(modern, "Sel", phase * 9 + rowIndex); Set(legacy, "Sel", phase * 9 + rowIndex);
                    int beforeNew = readsNew, beforeOld = readsOld;
                    Bitmap paintedNew, paintedOld;
                    using (Capture(modern, phase == 2 ? modernGroup : null)) paintedNew = (Bitmap)Call(modern, "BakeGpuListLayer", true);
                    using (Capture(legacy, phase == 2 ? legacyGroup : null)) paintedOld = LegacyRow(legacy, ref previous);
                    using (paintedNew) using (paintedOld) Equal(paintedNew, paintedOld, "fila");
                    if (phase == 2)
                    {
                        IList newRuns = (IList)Get(modernGroup, "Items"), oldRuns = (IList)Get(legacyGroup, "Items");
                        Check(newRuns.Count == oldRuns.Count && (width > 640 || newRuns.Count > 0), "Cintas perdidas: " + width + " px " + newRuns.Count + "/" + oldRuns.Count);
                        for (int i = 0; i < newRuns.Count; i++)
                            Check(Get(newRuns[i], "Key").Equals(Get(oldRuns[i], "Key")) && Get(newRuns[i], "Rect").Equals(Get(oldRuns[i], "Rect")), "Cinta cambia contenido o posicion");
                    }
                    Equal((Bitmap)Get(modern, "hornoPag"), (Bitmap)Get(legacy, "hornoPag"), "instantanea");
                    foreach (string field in new string[] { "eqIdx", "eqX", "eqW", "eqH", "eqBarra", "eqHueco" })
                        Check(Get(modern, field).Equals(Get(legacy, field)), "Ecualizador desubicado: " + field);
                    if (rowIndex != 0) Check(readsNew - beforeNew < readsOld - beforeOld, "No reduce consultas de canciones");
                    if (scratch == null) { scratch = (Bitmap)Get(modern, "gpuSelectedRaw"); backup = (Bitmap)Get(modern, "gpuSnapshotRow"); key = (ImageAttributes)Get(modern, "claveGpu"); }
                    Check(ReferenceEquals(scratch, Get(modern, "gpuSelectedRaw")), "Reasigna fila intermedia");
                    Check(ReferenceEquals(backup, Get(modern, "gpuSnapshotRow")), "Reasigna copia de fila");
                    Check(ReferenceEquals(key, Get(modern, "claveGpu")), "Reasigna clave de color");
                }
            }
            Call(modern, "SueltaAcabadoGpu");
            using (Bitmap a = (Bitmap)Call(modern, "BakeGpuListLayer", false))
            using (Bitmap b = (Bitmap)Call(legacy, "BakeGpuListLayer", false)) Equal(a, b, "clave recreada");
            previous = -1;
            using (Bitmap a = (Bitmap)Call(modern, "BakeGpuListLayer", true))
            using (Bitmap b = LegacyRow(legacy, ref previous)) Equal(a, b, "fila recreada");
            modern.Size = new Size(width + 31, modern.Height); Set(modern, "RowH", row + 6);
            using (Bitmap b = (Bitmap)Call(modern, "BakeGpuListLayer", false)) { }
            using (Bitmap b = (Bitmap)Call(modern, "BakeGpuListLayer", true)) { }
            Check(((Bitmap)Get(modern, "gpuSelectedRaw")).Size == new Size(width + 31, row + 6), "Fila no se adapta al tamaño");
            modern.Dispose();
            Check(Get(modern, "gpuSelectedRaw") == null && Get(modern, "gpuSnapshotRow") == null && Get(modern, "claveGpu") == null, "No libera buffers GPU");
        }
        Console.WriteLine("OK cristalcache: " + checks + " comprobaciones; comparación pixel a pixel y reutilización de dos filas, no FPS.");
        return 0;
    }
}
