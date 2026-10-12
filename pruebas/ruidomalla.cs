using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;

// Tarjeta simulada: compara cada byte del dibujo anterior por tejas con la malla.
// No abre Direct3D/HAL ni musica; los buffers/capturas de esta prueba no son FPS.
class RuidoMalla
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks, calls; static byte[] drawn; static List<byte> captured;
    [StructLayout(LayoutKind.Sequential)] struct Vtx
    { public float X, Y, Z, Rhw; public uint Color; public float U, V; }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, B).SetValue(o, v); }
    static object Get(object o, string n) { return o.GetType().GetField(n, B).GetValue(o); }
    static object Call(object o, string n, params object[] args)
    { try { return o.GetType().GetMethod(n, B).Invoke(o, args); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static void Check(bool ok, string s) { if (!ok) throw new Exception(s); checks++; Console.WriteLine("OK " + s); }
    public static int Draw(IntPtr self, uint type, uint tris, IntPtr data, uint stride)
    { calls++; drawn = new byte[tris * 3 * stride]; Marshal.Copy(data, drawn, 0, drawn.Length); if (captured != null) captured.AddRange(drawn); return 0; }
    public static int Sampler(IntPtr self, uint stage, uint type, uint value) { return 0; }
    static void Vertex(ref Vtx v, float x, float y, float u, float vv, uint color)
    { v.X = x - .5f; v.Y = y - .5f; v.Z = 0; v.Rhw = 1; v.Color = color; v.U = u; v.V = vv; }
    static byte[] Referencia(int w, int h, float um, float vm)
    {
        List<Vtx> all = new List<Vtx>();
        for (int y = 0; y < h; y += 1024) for (int x = 0; x < w; x += 1024)
        {
            float aw = Math.Min(1024, w - x), ah = Math.Min(1024, h - y);
            float u = um * (aw / 1024), v = vm * (ah / 1024);
            uint color = 0xFFFFFFFFu;
            Vtx[] q = new Vtx[6];
            Vertex(ref q[0], x, y, 0, 0, color); Vertex(ref q[1], x + aw, y, u, 0, color);
            Vertex(ref q[2], x + aw, y + ah, u, v, color); Vertex(ref q[3], x, y, 0, 0, color);
            Vertex(ref q[4], x + aw, y + ah, u, v, color); Vertex(ref q[5], x, y + ah, 0, v, color);
            all.AddRange(q);
        }
        Vtx[] a = all.ToArray(); byte[] bytes = new byte[a.Length * Marshal.SizeOf(typeof(Vtx))];
        GCHandle pin = GCHandle.Alloc(a, GCHandleType.Pinned);
        try { Marshal.Copy(pin.AddrOfPinnedObject(), bytes, 0, bytes.Length); } finally { pin.Free(); }
        return bytes;
    }
    static bool Same(byte[] a, byte[] b)
    { if (a == null || b == null || a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    static Control Monta(Assembly asm, out object device, out object stars)
    {
        Type td = asm.GetType("Rocola.D3D9Device"), ts = asm.GetType("Rocola.GpuStarfield");
        device = FormatterServices.GetUninitializedObject(td); stars = FormatterServices.GetUninitializedObject(ts);
        Set(device, "dev", new IntPtr(1)); Set(device, "stTex", new IntPtr(2)); Set(device, "stBlend", 3); Set(device, "stTss", 2);
        Set(device, "verticesSinSoporte", true); // puntero ficticio: no usar vtable ni HAL
        FieldInfo draw = td.GetField("drawUp", B);
        draw.SetValue(device, Delegate.CreateDelegate(draw.FieldType, typeof(RuidoMalla).GetMethod("Draw")));
        FieldInfo sampler = td.GetField("setSamp", B);
        sampler.SetValue(device, Delegate.CreateDelegate(sampler.FieldType, typeof(RuidoMalla).GetMethod("Sampler")));
        Set(stars, "gpu", device);
        return (Control)Activator.CreateInstance(asm.GetType("Rocola.GpuPanel"), true);
    }
    static void Escena(Control panel, object stars, Size size, float uv)
    {
        Set(panel, "stars", null); panel.Size = size; Set(panel, "stars", stars);
        Set(panel, "ruidoTex", new IntPtr(2)); Set(panel, "ruU", uv); Set(panel, "ruV", uv * .81f);
        // El ejecutable de referencia puede ser de cuando habia Espacio natural: se compara con su Legacy.
        FieldInfo natural = panel.GetType().GetField("FondoNatural", B); if (natural != null) natural.SetValue(panel, false);
    }
    static void Libera(Control panel, object device)
    {
        if (panel != null) { Set(panel, "stars", null); Set(panel, "ruidoTex", IntPtr.Zero); panel.Dispose(); }
        if (device != null) Set(device, "dev", IntPtr.Zero); // nunca liberar punteros ficticios
    }
    [STAThread] static int Main(string[] args)
    {
        object device = null, stars = null, beforeDevice = null, beforeStars = null; Control panel = null, before = null;
        try
        {
            Assembly asm = Assembly.LoadFrom(args[0]);
            if (asm.GetType("Rocola.D3D9Device") == null) { Console.WriteLine("CPU congelado: no existe malla de ruido GPU"); return 0; }
            panel = Monta(asm, out device, out stars);
            if (args.Length > 1) before = Monta(Assembly.Load(File.ReadAllBytes(args[1])), out beforeDevice, out beforeStars);
            foreach (float uv in new float[] { 1f, .73f })
            foreach (Size size in new Size[] { new Size(640,480), new Size(1024,768), new Size(1025,1025),
                new Size(3440,1440), new Size(3840,2160), new Size(7680,4320), new Size(1024,768) })
            {
                Escena(panel, stars, size, uv);
                calls = 0; drawn = null; Call(panel, "DibujaRuido");
                Check(calls == 1 && Same(drawn, Referencia(size.Width, size.Height, uv, uv * .81f)),
                    size.Width + "x" + size.Height + ": un envio, vertices/UV/color/orden byte-identicos");
                Array mesh = (Array)Get(panel, "ruidoMalla"); int tiles = ((size.Width - 1) / 1024 + 1) * ((size.Height - 1) / 1024 + 1);
                Check(mesh.Length == tiles * 6, "capacidad limitada a las tejas de la geometria actual");
                if (before != null)
                {
                    byte[] batch = drawn;
                    Escena(before, beforeStars, size, uv); captured = new List<byte>(); calls = 0;
                    try { Call(before, "DibujaRuido"); Check(Same(batch, captured.ToArray()), "identico al ejecutable baseline; envios antes=" + calls + ", ahora=1"); }
                    finally { captured = null; calls = 1; drawn = batch; }
                }
                for (int i = 0; i < 100; i++) Call(panel, "DibujaRuido");
                Check(ReferenceEquals(mesh, Get(panel, "ruidoMalla")) && calls == 101,
                    "cien cuadros reutilizan la misma malla sin multiplicar envios");
            }
            Set(panel, "ruidoTex", IntPtr.Zero); calls = 0; Call(panel, "DibujaRuido"); Check(calls == 0, "sin textura no se envia geometria");
            Check((Codigo.Llama(panel.GetType().GetMethod("DibujaRuido", B), "DrawTexturedBatch")
                || Codigo.Llama(panel.GetType().GetMethod("DibujaRuido", B), "DrawTexturedBatchWrap"))
                && !Codigo.Llama(panel.GetType().GetMethod("DibujaRuido", B), "DrawQuad"), "bucle real de ruido usa una sola tanda texturizada");
            Console.WriteLine("PASS " + checks + " comprobaciones; tarjeta simulada, sin HAL/audio/FPS reales"); return 0;
        }
        catch (Exception e) { Console.WriteLine(e); return 1; }
        finally
        {
            Libera(panel, device); Libera(before, beforeDevice);
            GC.KeepAlive(stars);
        }
    }
}
