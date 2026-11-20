// LEE-IGUAL (rapido-caratulas, 20-11-2026): cada imagen de la lista leida con la Imagenes.Lee de ahora y con la de la base (por byte[] y por
// Stream), y Mapas.Copia de ahora y de la base con varios trozos: los mismos puntos (los bytes del mapa entero), la misma medida, el mismo
// formato de GDI+ y la misma excepcion. Sale con 0 solo si todo es igual.   LeeIgual LISTA
using System; using System.IO; using System.Drawing; using SkiaSharp;
static class Prueba
{
    static int casos, mal;
    static string Puntos(SKBitmap b)
    {
        if (b == null) return "null";
        byte[] t = new byte[b.Width * 4 * b.Height];
        unsafe { byte* p = (byte*)b.GetPixels(); for (int y = 0; y < b.Height; y++) System.Runtime.InteropServices.Marshal.Copy((IntPtr)(p + (long)y * b.RowBytes), t, y * b.Width * 4, b.Width * 4); }
        return b.Width + "x" + b.Height + " " + b.ColorType + "/" + b.AlphaType + " " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(t)).Substring(0, 16);
    }
    static string Uno(Func<SKBitmap> f, out SKBitmap b) { b = null; try { b = f(); return Puntos(b); } catch (Exception ex) { return "lanza " + ex.GetType().Name; } }
    static void Compara(string que, string a, string b) { casos++; if (a != b) { mal++; Console.WriteLine("DISTINTO " + que + ": ahora " + a + " / antes " + b); } }
    static int Main(string[] a)
    {
        foreach (string ruta in File.ReadAllLines(a[0]))
        {
            if (ruta.Trim().Length == 0) continue;
            byte[] d = File.ReadAllBytes(ruta);
            Rocola.Nativo.FormatoImagen f1 = 0, f2 = 0, f3 = 0; Rocola.NativoViejo.FormatoImagen g1 = 0, g2 = 0;
            SKBitmap n1, n2, v1, v2;
            string sn1 = Uno(() => Rocola.Nativo.Imagenes.Lee(d, out f1), out n1);
            string sv1 = Uno(() => Rocola.NativoViejo.Imagenes.Lee(d, out g1), out v1);
            string sn2 = Uno(() => { using (MemoryStream m = new MemoryStream(d, 0, d.Length, false)) return Rocola.Nativo.Imagenes.Lee(m, out f2); }, out n2);
            string sv2 = Uno(() => { using (MemoryStream m = new MemoryStream(d, 0, d.Length, false)) return Rocola.NativoViejo.Imagenes.Lee(m, out g2); }, out v2);
            // (un Stream que no se puede mover: la rama de siempre)
            SKBitmap n3; string sn3 = Uno(() => { using (Stream m = new NoMovible(d)) return Rocola.Nativo.Imagenes.Lee(m, out f3); }, out n3);
            string n = Path.GetFileName(Path.GetDirectoryName(ruta)) + "/" + Path.GetFileName(ruta);
            Compara(n + " Lee(byte[])", sn1 + " " + f1, sv1 + " " + g1);
            Compara(n + " Lee(Stream)", sn2 + " " + f2, sv2 + " " + g2);
            Compara(n + " Lee(Stream no movible)", sn3 + " " + f3, sv1 + " " + g1);
            if (n1 != null && v1 != null)
                foreach (Rectangle r in new Rectangle[] { new Rectangle(0, 0, n1.Width, n1.Height), new Rectangle(1, 2, Math.Max(1, n1.Width - 3), Math.Max(1, n1.Height - 5)),
                                                           new Rectangle(n1.Width / 2, n1.Height / 3, Math.Max(1, n1.Width / 3), 1), new Rectangle(-2, -1, n1.Width, n1.Height), new Rectangle(5, 5, n1.Width, n1.Height + 4) })
                {
                    SKBitmap cn, cv;
                    string scn = Uno(() => Rocola.Nativo.Mapas.Copia(n1, r), out cn), scv = Uno(() => Rocola.NativoViejo.Mapas.Copia(v1, r), out cv);
                    Compara(n + " Copia " + r, scn, scv);
                    if (cn != null) cn.Dispose(); if (cv != null) cv.Dispose();
                }
            foreach (SKBitmap b in new SKBitmap[] { n1, n2, n3, v1, v2 }) if (b != null) b.Dispose();
        }
        Console.WriteLine("lee-igual: " + casos + " comparaciones, " + mal + " distintas");
        return mal == 0 && casos > 0 ? 0 : 1;
    }
    sealed class NoMovible : Stream
    {
        readonly MemoryStream m; internal NoMovible(byte[] d) { m = new MemoryStream(d); }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] b, int o, int c) => m.Read(b, o, Math.Min(c, 777));
        public override void Flush() { } public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException(); public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
    }
}
