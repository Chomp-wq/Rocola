using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// ¿CUANTO CAMBIARIA EL ASPECTO DEL BUSCADOR SI EL RECUADRO SE DIBUJARA APARTE?
//
// Hoy, moverse una casilla obliga a rehacer y RE-SUBIR a la tarjeta grafica la pantalla ENTERA (7 MB a
// 1080p). Una forma de evitarlo es sacar la tarjeta seleccionada de la imagen grande y dibujarla como una
// pieza aparte encima. Funciona... pero la tarjeta se compondria de otra manera, y aqui las optimizaciones
// solo se aceptan si la pantalla sale IDENTICA.
//
// Asi que en vez de suponerlo, se dibuja de las DOS formas y se comparan los pixeles:
//
//   COMO ESTA HOY     el fondo cacheado, y encima la tarjeta elegida dibujada DIRECTAMENTE sobre el.
//   PIEZA APARTE      el fondo cacheado, y la tarjeta elegida dibujada en su PROPIA capa transparente
//                     que luego se pega encima (que es lo que haria la tarjeta grafica con un quad).
//
// La diferencia, si la hay, sale en los bordes suavizados: en la primera forma el borde se mezcla con lo
// que hay debajo; en la segunda se mezcla primero con transparente y luego el resultado se mezcla otra vez.
class Aspecto
{
    static Type TF, TSO, TItem, TAlb;
    static Image portada;
    static Image Cov<T>(T a) { return portada; }
    static string Gen<T>(T a) { return "rock"; }

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    static Control Monta(int W, int H, int sel)
    {
        Control so = (Control)Activator.CreateInstance(TSO, true);
        so.SetBounds(0, 0, W, H);
        so.Font = new Font(Fam(), 12f, FontStyle.Bold);
        FieldInfo fKind = TItem.GetField("Kind"), fDisp = TItem.GetField("Display"), fKey = TItem.GetField("Key"), fSc = TItem.GetField("Score");
        IList res = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TItem));
        string[] nom = { "Depeche Mode - Enjoy The Silence", "Jose Jose - El Triste", "Los Angeles Azules - Nunca Es Suficiente",
                         "Metallica - Enter Sandman", "Juan Gabriel - Amor Eterno", "Bronco - Que No Quede Huella" };
        for (int i = 0; i < 40; i++)
        {
            object it = Activator.CreateInstance(TItem, true);
            fKind.SetValue(it, i % 3); fDisp.SetValue(it, nom[i % nom.Length] + " " + i);
            fKey.SetValue(it, (nom[i % nom.Length] + " " + i).ToLowerInvariant()); fSc.SetValue(it, 5000 - i);
            res.Add(it);
        }
        object alb = Activator.CreateInstance(TAlb, true);
        TAlb.GetField("Folder").SetValue(alb, @"C:\Musica\Disco");
        TAlb.GetField("Name").SetValue(alb, "Un disco");
        TAlb.GetField("Artist").SetValue(alb, "Un artista");
        TAlb.GetField("CoverPath").SetValue(alb, @"C:\Musica\Disco\cover.jpg");
        FieldInfo fAlbRef = TItem.GetField("AlbumRef");
        for (int i = 0; i < res.Count; i++) fAlbRef.SetValue(res[i], alb);
        {
            Type tfc = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
            MethodInfo mic = typeof(Aspecto).GetMethod("Cov", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb);
            TSO.GetField("GetCover").SetValue(so, Delegate.CreateDelegate(tfc, mic));
        }
        FieldInfo fGen = TSO.GetField("GetGenero");
        if (fGen != null)
        {
            Type tf2 = typeof(Func<,>).MakeGenericType(TItem, typeof(string));
            MethodInfo mi = typeof(Aspecto).GetMethod("Gen", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TItem);
            fGen.SetValue(so, Delegate.CreateDelegate(tf2, mi));
        }
        TSO.GetField("Results").SetValue(so, res);
        TSO.GetField("Query").SetValue(so, "de");
        TSO.GetField("Accent").SetValue(so, Color.FromArgb(178, 152, 255));
        TSO.GetField("FocusResults").SetValue(so, true);
        TSO.GetField("SelResult").SetValue(so, sel);
        return so;
    }

    // Cuenta en que se diferencian dos imagenes.
    static void Compara(Bitmap a, Bitmap b, out int distintos, out int peor, out int total)
    {
        distintos = 0; peor = 0; total = a.Width * a.Height;
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
            {
                Color ca = a.GetPixel(x, y), cb = b.GetPixel(x, y);
                int d = Math.Max(Math.Max(Math.Abs(ca.R - cb.R), Math.Abs(ca.G - cb.G)),
                        Math.Max(Math.Abs(ca.B - cb.B), Math.Abs(ca.A - cb.A)));
                if (d > 0) { distintos++; if (d > peor) peor = d; }
            }
    }

    static void Mide(int W, int H, string png, int sel)
    {
        Control so = Monta(W, H, sel);
        MethodInfo mRender = TSO.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) });
        MethodInfo mCard = TSO.GetMethod("DrawOneResultCard", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fRects = TSO.GetField("resultRects", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fCache = TSO.GetField("resCache", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fOrigen = TSO.GetField("resCacheOrigin", BindingFlags.Instance | BindingFlags.NonPublic);
        int margen = (int)TSO.GetField("MargenTarj", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        Rectangle todo = new Rectangle(0, 0, W, H);

        // COMO ESTA HOY
        Bitmap A = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(A)) { g.SmoothingMode = SmoothingMode.AntiAlias; mRender.Invoke(so, new object[] { g, todo, true }); }

        IList rects = (IList)fRects.GetValue(so);
        if (rects == null || rects.Count <= sel) { Console.WriteLine("     (no hay tarjetas que comparar)"); return; }
        object kv = rects[sel];
        RectangleF rf = (RectangleF)kv.GetType().GetProperty("Key").GetValue(kv, null);
        Rectangle rr = Rectangle.Round(rf);
        Bitmap cache = (Bitmap)fCache.GetValue(so);
        Point org = (Point)fOrigen.GetValue(so);
        Rectangle zona = Rectangle.Inflate(rr, margen + 2, margen + 2);

        // SIN LA SELECCION HORNEADA: la zona de la tarjeta, tal cual esta en el cache (o sea sin recuadro)
        Bitmap S = (Bitmap)A.Clone();
        using (Graphics g = Graphics.FromImage(S))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            Rectangle enCache = new Rectangle(zona.X - org.X, zona.Y - org.Y, zona.Width, zona.Height);
            g.DrawImage(cache, zona, enCache, GraphicsUnit.Pixel);
        }

        // PIEZA APARTE: la tarjeta elegida en su propia capa transparente, pegada encima
        Bitmap B = (Bitmap)S.Clone();
        using (Bitmap capa = new Bitmap(zona.Width, zona.Height, PixelFormat.Format32bppPArgb))
        {
            using (Graphics cg = Graphics.FromImage(capa))
            {
                cg.SmoothingMode = SmoothingMode.AntiAlias;
                cg.TranslateTransform(-zona.X, -zona.Y);
                mCard.Invoke(so, new object[] { cg, sel, rr, true, "de" });
            }
            using (Graphics g = Graphics.FromImage(B)) g.DrawImageUnscaled(capa, zona.X, zona.Y);
        }

        int dist, peor, tot;
        Compara(A, B, out dist, out peor, out tot);
        Console.WriteLine();
        Console.WriteLine("  " + W + "x" + H + ", casilla " + sel + "   (la tarjeta mide " + rr.Width + "x" + rr.Height + ")");
        Console.WriteLine("     pixeles de la pantalla que cambian:  " + dist.ToString("#,0") + " de " + tot.ToString("#,0")
                          + "   (" + (100.0 * dist / tot).ToString("0.000") + "%)");
        Console.WriteLine("     la mayor diferencia de color:        " + peor + " niveles de 255");

        // Una foto: la tarjeta de las dos formas, y la diferencia aumentada
        int cw = zona.Width, ch = zona.Height;
        using (Bitmap foto = new Bitmap(cw, ch * 3 + 40, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(foto))
        {
            g.Clear(Color.FromArgb(255, 12, 16, 36));
            using (Font f = new Font(Fam(), 9f))
            {
                g.DrawImage(A, new Rectangle(0, 12, cw, ch), zona, GraphicsUnit.Pixel);
                g.DrawString("COMO ESTA HOY", f, Brushes.White, 2, 0);
                g.DrawImage(B, new Rectangle(0, ch + 24, cw, ch), zona, GraphicsUnit.Pixel);
                g.DrawString("PIEZA APARTE", f, Brushes.White, 2, ch + 12);
                g.DrawString("LA DIFERENCIA (x40)", f, Brushes.White, 2, ch * 2 + 24);
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                    {
                        Color ca = A.GetPixel(zona.X + x, zona.Y + y), cb = B.GetPixel(zona.X + x, zona.Y + y);
                        int d = Math.Max(Math.Max(Math.Abs(ca.R - cb.R), Math.Abs(ca.G - cb.G)), Math.Abs(ca.B - cb.B));
                        int v = Math.Min(255, d * 40);
                        foto.SetPixel(x, ch * 2 + 36 + y, Color.FromArgb(255, v, v, v));
                    }
            }
            foto.Save(png, ImageFormat.Png);
        }
        Console.WriteLine("     foto: " + png);
        A.Dispose(); S.Dispose(); B.Dispose(); so.Dispose();
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "SearchOverlay") TSO = t;
            if (t.Name == "SearchItem") TItem = t;
            if (t.Name == "Album") TAlb = t;
        }
        if (TSO == null || TItem == null || TAlb == null) { Console.WriteLine("no encuentro el buscador"); return 1; }
        portada = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
        using (Graphics pgg = Graphics.FromImage(portada))
        {
            pgg.Clear(Color.FromArgb(255, 30, 40, 90));
            for (int i = 0; i < 40; i++) using (SolidBrush sb2 = new SolidBrush(Color.FromArgb(90, 200, 120 + i, 255 - i))) pgg.FillEllipse(sb2, i * 7, i * 6, 60, 60);
        }
        Console.WriteLine();
        Console.WriteLine("  SI EL RECUADRO SE DIBUJARA COMO PIEZA APARTE, ¿CUANTO CAMBIARIA LA PANTALLA?");
        // Varias casillas: la primera, una de en medio, el final de una columna y la ultima. Si el recuadro
        // se saliera de su sitio en algun caso, seria en los bordes de la rejilla.
        int[] casillas = { 0, 5, 7, 20, 39 };
        foreach (int k in casillas) Mide(1024, 768, "aspecto_1024_" + k + ".png", k);
        foreach (int k in casillas) Mide(1920, 1080, "aspecto_1920_" + k + ".png", k);
        Console.WriteLine();
        return 0;
    }
}
