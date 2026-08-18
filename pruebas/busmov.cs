using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// EL BUSCADOR, DESPUES DE ACELERARLO: TIENE QUE VERSE EXACTAMENTE IGUAL.
//
// Se han cambiado dos cosas: las tarjetas y las pastillas de genero ahora se dibujan una vez y se copian, y
// barrer resultados con la flecha ya no encarga caratulas al disco. Lo primero podria cambiar el aspecto por
// un pixel; lo segundo, que una caratula no aparezca. Aqui se cargan el programa VIEJO y el NUEVO a la vez,
// se les da lo mismo, y se comparan las dos pantallas pixel por pixel.
class BusMov
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    static readonly string[] Nombres = {
        "Depeche Mode - Enjoy The Silence", "Jose Jose - El Triste", "Los Angeles Azules - Nunca Es Suficiente",
        "Metallica - Enter Sandman", "Juan Gabriel - Amor Eterno", "Bronco - Que No Quede Huella",
        "Rocio Durcal - Amor Eterno", "Mana - Rayando El Sol", "Cancion con acentos: Corazon Partio",
        "Un titulo muchisimo mas largo de lo que cabe en una tarjeta de resultado del buscador" };

    // Monta un buscador del ensamblado que se le pase, con los mismos datos.
    static Control Monta(Assembly asm, bool esNuevo, out Type tso, out FieldInfo fSel, int w, int h, string consulta, Bitmap portada, out int[] pedidas)
    {
        Type TF = asm.GetType("Rocola.Form1", true);
        Type TItem = null, TAlb = null; tso = null;
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "SearchOverlay") tso = t;
            if (t.Name == "SearchItem") TItem = t;
            if (t.Name == "Album") TAlb = t;
        }
        Control so = (Control)Activator.CreateInstance(tso, true);
        so.SetBounds(0, 0, w, h);
        so.Font = new Font(Fam(), 12f, FontStyle.Bold);

        object alb = Activator.CreateInstance(TAlb, true);
        TAlb.GetField("Folder").SetValue(alb, @"C:\Musica\Disco");
        TAlb.GetField("Name").SetValue(alb, "Un disco");
        TAlb.GetField("Artist").SetValue(alb, "Un artista");
        TAlb.GetField("CoverPath").SetValue(alb, @"C:\Musica\Disco\cover.jpg");

        Type tLista = typeof(List<>).MakeGenericType(TItem);
        IList res = (IList)Activator.CreateInstance(tLista);
        for (int i = 0; i < 40; i++)
        {
            object it = Activator.CreateInstance(TItem, true);
            TItem.GetField("Kind").SetValue(it, i % 3);
            TItem.GetField("Display").SetValue(it, Nombres[i % Nombres.Length] + " " + i);
            TItem.GetField("Key").SetValue(it, (Nombres[i % Nombres.Length] + " " + i).ToLowerInvariant());
            TItem.GetField("Score").SetValue(it, 5000 - i);
            TItem.GetField("AlbumRef").SetValue(it, alb);
            res.Add(it);
        }
        tso.GetField("Results").SetValue(so, res);
        tso.GetField("Query").SetValue(so, consulta);
        tso.GetField("Accent").SetValue(so, Color.FromArgb(178, 152, 255));
        tso.GetField("FocusResults").SetValue(so, true);
        fSel = tso.GetField("SelResult");
        fSel.SetValue(so, 0);

        // Contador de encargos de caratula: la version nueva no debe encargar mientras se barre.
        // Un contador POR PROGRAMA: los dos usan metodos distintos para no mezclarse.
        laPortada = portada;
        pedidas = esNuevo ? cuentaN : cuentaV;
        Type tfc = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
        MethodInfo mic = typeof(BusMov).GetMethod(esNuevo ? "PideNuevo" : "PideViejo", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb);
        tso.GetField("GetCover").SetValue(so, Delegate.CreateDelegate(tfc, mic));
        // Y la version "solo si ya esta en memoria" (solo existe en el programa nuevo): aqui nunca hay nada.
        FieldInfo fYa = tso.GetField("GetCoverYa");
        if (fYa != null)
        {
            MethodInfo miy = typeof(BusMov).GetMethod("SoloSiEsta", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb);
            fYa.SetValue(so, Delegate.CreateDelegate(tfc, miy));
        }
        FieldInfo fg = tso.GetField("GetGenero");
        if (fg != null)
        {
            Type tf2 = typeof(Func<,>).MakeGenericType(TItem, typeof(string));
            MethodInfo mig = typeof(BusMov).GetMethod("Gen", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TItem);
            fg.SetValue(so, Delegate.CreateDelegate(tf2, mig));
        }
        return so;
    }

    static Bitmap Pinta(Control so, Type tso, int w, int h, bool forGpu)
    {
        MethodInfo mR = tso.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) });
        Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(255, 8, 12, 36));
            mR.Invoke(so, new object[] { g, new Rectangle(0, 0, w, h), forGpu });
        }
        return b;
    }

    static int Main()
    {
        Assembly aV = Assembly.LoadFrom("rocola_prev.exe");   // como estaba antes de acelerar el dibujo
        Assembly aN = Assembly.LoadFrom("rocola.exe");
        Bitmap portada = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
        using (Graphics pg = Graphics.FromImage(portada))
        {
            pg.Clear(Color.FromArgb(255, 30, 40, 90));
            for (int i = 0; i < 40; i++) using (SolidBrush sb = new SolidBrush(Color.FromArgb(90, 200, 120 + i, 255 - i))) pg.FillEllipse(sb, i * 7, i * 6, 60, 60);
        }

        // ---------- 1) LA MISMA PANTALLA, PIXEL POR PIXEL ----------
        Console.WriteLine("\n  1) EL BUSCADOR SE VE EXACTAMENTE IGUAL QUE ANTES\n");
        int[,] tam = { { 1024, 768 }, { 1280, 800 }, { 1920, 1080 }, { 800, 600 } };
        string[] consultas = { "de", "", "amor", "zzz" };
        int distintas = 0, comparadas = 0, peorPix = 0, peorNivel = 0; long rozados = 0; string dondePeor = null;
        for (int t = 0; t < tam.GetLength(0); t++)
            foreach (string cons in consultas)
                for (int sel = 0; sel < 3; sel++)
                {
                    int w = tam[t, 0], h = tam[t, 1];
                    Type tv, tn; FieldInfo fv, fn; int[] p1, p2;
                    // Se espera de sobra para que el programa nuevo TAMBIEN pida la caratula: asi se comparan
                    // dos pantallas con el cubo puesto, que es el caso exigente.
                    Control sv = Monta(aV, false, out tv, out fv, w, h, cons, portada, out p1);
                    Control sn = Monta(aN, true, out tn, out fn, w, h, cons, portada, out p2);
                    fv.SetValue(sv, sel * 7); fn.SetValue(sn, sel * 7);
                    hayEnMemoria = true;                     // "ya esta en memoria": los dos ven la misma caratula
                    // Se compara el dibujo SIN el fondo estrellado: ese fondo se siembra al azar en cada
                    // arranque, asi que dos programas distintos nunca lo tienen igual y no se puede comparar.
                    // Todo lo que se ha tocado (tarjetas, pastillas, rotulos, teclado) SI entra aqui.
                    using (Bitmap bv = Pinta(sv, tv, w, h, true))
                    using (Bitmap bn = Pinta(sn, tn, w, h, true))
                    {
                        // Un pixel "distinto" solo cuenta si se mueve MAS DE UN NIVEL de 255. Dibujar el
                        // fondo de la tarjeta aparte y pegarlo mezcla el color en dos pasos en vez de uno, y
                        // eso deja diferencias de redondeo de una unidad en el borde suavizado. Un nivel de
                        // 255 es el 0,4% de un color: no hay pantalla ni ojo que lo distinga.
                        int dif = 0, tocados = 0;
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                            {
                                Color ca = bv.GetPixel(x, y), cb = bn.GetPixel(x, y);
                                if (ca.ToArgb() == cb.ToArgb()) continue;
                                tocados++;
                                int d = Math.Max(Math.Abs(ca.R - cb.R), Math.Max(Math.Abs(ca.G - cb.G), Math.Abs(ca.B - cb.B)));
                                d = Math.Max(d, Math.Abs(ca.A - cb.A));
                                if (d > 1) dif++;
                                if (d > peorNivel) peorNivel = d;
                            }
                        comparadas++;
                        rozados += tocados;
                        if (dif > 0)
                        {
                            distintas++;
                            if (dif > peorPix) { peorPix = dif; dondePeor = w + "x" + h + " \"" + cons + "\" sel=" + (sel * 7); }
                            if (distintas <= 2) { bv.Save("busmov_antes.png", ImageFormat.Png); bn.Save("busmov_ahora.png", ImageFormat.Png); }
                        }
                    }
                }
        Console.WriteLine("     pantallas comparadas: " + comparadas + " (4 tamanos x 4 busquedas x 3 elegidos)");
        Console.WriteLine("     pixeles que se mueven algo: " + rozados + "   y de esos, mas de un nivel: " + peorPix);
        Console.WriteLine("     la mayor diferencia de color en toda la prueba: " + peorNivel + " niveles de 255");
        Check(distintas == 0 && peorNivel <= 1, "el buscador se ve igual que antes: ni un color se mueve mas de 1 nivel de 255"
              + (dondePeor != null ? ("   [" + dondePeor + "]") : ""));

        // ---------- 2) BARRER RESULTADOS NO ENCARGA CARATULAS ----------
        Console.WriteLine("\n  2) BARRER RESULTADOS NO MANDA TRABAJO AL DISCO\n");
        {
            Type tv, tn; FieldInfo fv, fn; int[] pv, pn;
            Control sv = Monta(aV, false, out tv, out fv, 1024, 768, "de", portada, out pv);
            Control sn = Monta(aN, true, out tn, out fn, 1024, 768, "de", portada, out pn);
            hayEnMemoria = false;   // NADA en memoria: cada consulta al disco seria un encargo de verdad
            // Un barrido rapido, como quien deja la flecha pulsada: 40 resultados seguidos.
            pv[0] = 0; pn[0] = 0;
            for (int i = 0; i < 40; i++)
            {
                fv.SetValue(sv, i); using (Bitmap b = Pinta(sv, tv, 1024, 768, false)) { }
                fn.SetValue(sn, i); using (Bitmap b = Pinta(sn, tn, 1024, 768, false)) { }
            }
            Console.WriteLine("     recorriendo los 40 resultados de un tiron:");
            Console.WriteLine("        antes: " + pv[0] + " caratulas encargadas al disco");
            Console.WriteLine("        ahora: " + pn[0] + " caratulas encargadas al disco");
            Check(pn[0] == 0, "barriendo resultados no se encarga NI UNA caratula (antes se encargaban " + pv[0] + ")");
            Check(pv[0] > 0, "y la prueba es valida: el programa de antes SI las encargaba");

            // Y al PARARSE en un resultado, si se encarga la suya.
            // Un repintado mas para que se asiente (el buscador ajusta el elegido a los que caben en
            // pantalla, y ese ajuste cuenta como un movimiento mas), y ENTONCES se para de verdad.
            using (Bitmap b = Pinta(sn, tn, 1024, 768, false)) { }
            pn[0] = 0;
            System.Threading.Thread.Sleep(320);
            using (Bitmap b = Pinta(sn, tn, 1024, 768, false)) { }
            Console.WriteLine("     y al pararse en un resultado: " + pn[0] + " caratula encargada");
            Check(pn[0] > 0, "en cuanto el cliente se para en un resultado, SI se encarga su caratula");
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }

    static Bitmap laPortada;
    static readonly int[] cuentaV = new int[1], cuentaN = new int[1];
    static bool hayEnMemoria = true;
    static Image PideViejo<T>(T a) { cuentaV[0]++; return hayEnMemoria ? laPortada : null; }
    static Image PideNuevo<T>(T a) { cuentaN[0]++; return hayEnMemoria ? laPortada : null; }
    static Image SoloSiEsta<T>(T a) { return hayEnMemoria ? laPortada : null; }
    static string Gen<T>(T it) { return "Balada"; }
}
