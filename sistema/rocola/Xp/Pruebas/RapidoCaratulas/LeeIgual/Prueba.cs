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
        // MAPAS CON PUNTOS «INVALIDOS» de premultiplicado (color mayor que el alfa, alfa 0 con color: lo que dejaria escribir a mano por
        // GetPixels), al azar y a proposito: Mapas.Copia de ahora (los bytes) y de la base (DrawBitmap sobre transparente), los mismos puntos
        Random az = new Random(11);
        foreach (int[] m in new int[][] { new[] { 1, 1 }, new[] { 7, 5 }, new[] { 256, 256 }, new[] { 1024, 768 }, new[] { 333, 17 } })
            for (int modo = 0; modo < 3; modo++)
            {
                SKBitmap b = new SKBitmap(new SKImageInfo(m[0], m[1], SKColorType.Bgra8888, SKAlphaType.Premul));
                unsafe
                {
                    byte* p = (byte*)b.GetPixels();
                    for (int y = 0; y < b.Height; y++)
                        for (int x = 0; x < b.Width * 4; x += 4)
                        {
                            byte* q = p + (long)y * b.RowBytes + x;
                            if (modo == 0) { for (int i = 0; i < 4; i++) q[i] = (byte)az.Next(256); }                       // todo al azar
                            else if (modo == 1) { q[0] = q[1] = q[2] = (byte)(128 + az.Next(128)); q[3] = (byte)az.Next(128); }   // color > alfa
                            else { q[0] = (byte)az.Next(256); q[1] = (byte)az.Next(256); q[2] = (byte)az.Next(256); q[3] = 0; }  // alfa 0 con color
                        }
                }
                b.NotifyPixelsChanged();
                foreach (Rectangle r in new Rectangle[] { new Rectangle(0, 0, b.Width, b.Height), new Rectangle(b.Width / 3, b.Height / 4, Math.Max(1, b.Width / 2), Math.Max(1, b.Height / 2)) })
                {
                    SKBitmap cn, cv;
                    string scn = Uno(() => Rocola.Nativo.Mapas.Copia(b, r), out cn), scv = Uno(() => Rocola.NativoViejo.Mapas.Copia(b, r), out cv);
                    Compara("invalidos modo " + modo + " " + m[0] + "x" + m[1] + " Copia " + r, scn, scv);
                    if (cn != null) cn.Dispose(); if (cv != null) cv.Dispose();
                }
                b.Dispose();
            }
        // UN STREAM QUE DICE OTRO LARGO del que da (se acorta o crece mientras se lee): lo de la base (CopyTo hasta el final)
        int grandes = 0;
        foreach (string ruta in File.ReadAllLines(a[0]))
        {
            if (ruta.Trim().Length == 0) continue;
            byte[] d = File.ReadAllBytes(ruta);
            if (d.Length < 4 || (d.Length > 400000 && (++grandes % 10) != 0)) continue;   // (de las grandes, una de cada diez: el tiempo)
            string n = Path.GetFileName(ruta);
            foreach (int delta in new[] { -d.Length / 3, -1, 1, 4096 })
            {
                Rocola.Nativo.FormatoImagen f1 = 0; Rocola.NativoViejo.FormatoImagen g1 = 0; SKBitmap x1, x2;
                string s1 = Uno(() => { using (Stream m = new LargoFalso(d, delta)) return Rocola.Nativo.Imagenes.Lee(m, out f1); }, out x1);
                string s2 = Uno(() => { using (Stream m = new LargoFalso(d, delta)) return Rocola.NativoViejo.Imagenes.Lee(m, out g1); }, out x2);
                Compara(n + " Lee(Stream de largo falso " + delta + ")", s1 + " " + f1, s2 + " " + g1);
                if (x1 != null) x1.Dispose(); if (x2 != null) x2.Dispose();
            }
        }
        Console.WriteLine("lee-igual: " + casos + " comparaciones, " + mal + " distintas");
        return mal == 0 && casos > 0 ? 0 : 1;
    }
    // dice Length = los bytes + delta (delta < 0: da mas de lo que dice; delta > 0: da menos)
    sealed class LargoFalso : Stream
    {
        readonly MemoryStream m; readonly int delta; internal LargoFalso(byte[] d, int delta) { m = new MemoryStream(d); this.delta = delta; }
        public override bool CanRead => true; public override bool CanSeek => true; public override bool CanWrite => false;
        public override long Length => Math.Max(0, m.Length + delta); public override long Position { get => m.Position; set => m.Position = value; }
        public override int Read(byte[] b, int o, int c) => m.Read(b, o, Math.Min(c, 1000));
        public override void Flush() { } public override long Seek(long o, SeekOrigin s) => m.Seek(o, s);
        public override void SetLength(long v) => throw new NotSupportedException(); public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
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
