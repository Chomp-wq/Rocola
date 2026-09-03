using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// "INSERTA UNA MONEDA" SE SALIA DE SU CAPSULA.
//
// LO QUE PASABA. El letrero de los creditos tiene dos textos de anchos muy distintos:
//
//     con creditos:  "Creditos 1"          (corto)
//     sin creditos:  "Inserta una moneda"  (largo)
//
// El ANCHO de la capsula se calcula al acomodar la pantalla, con PastillaView.Ancho. Pero quien cambia
// el texto -UpdateCredits- solo repintaba: no volvia a acomodar nada. Asi que cuando el cliente gastaba
// su ultimo credito, el letrero pasaba a "Inserta una moneda" DENTRO de una capsula medida para
// "Creditos 1", y se cortaba a media palabra. Eso es lo que se veia en la rocola del bar.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE LOS DOS TEXTOS QUEPAN, cada uno en la capsula que le toca, a cualquier tamaño de pantalla.
//   2) QUE AL CAMBIAR DE UNO A OTRO SE VUELVA A ACOMODAR. Es el fallo de verdad: no era la medida, era
//      que nadie la volvia a pedir.
//   3) Y QUE NO SE ACOMODE POR NADA: pasar de "Creditos 1" a "Creditos 2" no cambia el ancho, y
//      rehacer el acomodo de la pantalla en cada credito seria trabajo tirado.
class Moneda
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TPast;

    static MethodBase Met(Type t, string n)
    {
        if (t == null) return null;
        foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }

    // Lo que mide de ancho la capsula con ese texto, y cuanta tinta gasta el texto dentro de ella.
    static void Capsula(MethodInfo anc, string etiq, string valor, int alto, out int ancho, out int izq, out int der)
    {
        FontFamily fam = FontFamily.GenericSansSerif;
        ancho = Convert.ToInt32(anc.Invoke(null, new object[] { etiq, valor, alto, fam, false }));
        Control c = (Control)Activator.CreateInstance(TPast, true);
        c.Font = new Font(fam, 9f);
        c.SetBounds(0, 0, ancho, alto);
        TPast.GetField("Etiqueta", TODO).SetValue(c, etiq);
        TPast.GetField("Valor", TODO).SetValue(c, valor);
        TPast.GetField("Acento", TODO).SetValue(c, Color.FromArgb(255, 205, 90));
        izq = ancho; der = 0;
        using (Bitmap b = new Bitmap(ancho, alto))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.FromArgb(255, 8, 12, 36));
                ((MethodInfo)Met(TPast, "RenderContent")).Invoke(c, new object[] { g });
            }
            // Donde empieza y donde acaba la TINTA CLARA del texto (la capsula de fondo es oscura).
            for (int x = 0; x < b.Width; x++)
                for (int y = 0; y < b.Height; y++)
                {
                    Color q = b.GetPixel(x, y);
                    if (q.R > 170 && q.G > 170 && q.B > 190) { if (x < izq) izq = x; if (x > der) der = x; break; }
                }
        }
        c.Dispose();
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes())
        {
            if (t.Name == "Form1") TF = t;
            if (t.Name == "PastillaView") TPast = t;
        }
        if (TF == null || TPast == null) { Console.WriteLine("  FALLO  no se encontro la capsula"); return 1; }
        MethodInfo anc = (MethodInfo)Met(TPast, "Ancho");
        if (anc == null) { Console.WriteLine("  FALLO  la capsula no sabe medirse"); return 1; }

        Console.WriteLine("\n  1) LOS DOS TEXTOS CABEN, CADA UNO EN SU CAPSULA\n");
        int[] altos = { 26, 34, 40, 48, 60, 76 };
        Console.WriteLine("     alto   \"Creditos 1\"        \"Inserta una moneda\"");
        Console.WriteLine("     ---------------------------------------------------");
        bool caben = true;
        foreach (int h in altos)
        {
            int a1, i1, d1, a2, i2, d2;
            Capsula(anc, "Creditos", "1", h, out a1, out i1, out d1);
            Capsula(anc, "Inserta una moneda", "", h, out a2, out i2, out d2);
            Console.WriteLine(string.Format("     {0,4}   {1,4} px (aire {2,3}/{3,3})   {4,4} px (aire {5,3}/{6,3})",
                              h, a1, i1, a1 - 1 - d1, a2, i2, a2 - 1 - d2));
            if (i1 < 4 || a1 - 1 - d1 < 4) caben = false;
            if (i2 < 4 || a2 - 1 - d2 < 4) caben = false;
        }
        Check(caben, "los dos textos caben con aire a los dos lados, a cualquier tamaño de pantalla");

        // Y LO QUE HACIA FALTA VER: son anchos MUY distintos. Por eso no se puede reusar la medida.
        int aCred, aMon, z1, z2;
        Capsula(anc, "Creditos", "1", 40, out aCred, out z1, out z2);
        Capsula(anc, "Inserta una moneda", "", 40, out aMon, out z1, out z2);
        Console.WriteLine("     a 40 px de alto, uno pide " + aCred + " px y el otro " + aMon + ": " + (aMon - aCred) + " px de diferencia");
        Check(aMon - aCred > 40, "y son anchos MUY distintos: la capsula de uno no le sirve al otro");

        Console.WriteLine("\n  2) AL CAMBIAR DE TEXTO SE VUELVE A ACOMODAR\n");
        // Este es el fallo de verdad: la medida siempre estuvo bien, lo que faltaba era volver a pedirla.
        MethodBase upd = Met(TF, "UpdateCredits");
        Check(upd != null, "hay un solo sitio que cambia el letrero de los creditos (UpdateCredits)");
        if (upd != null)
        {
            Check(Codigo.Llama(upd, "Ancho"), "y mira lo que la capsula pide de ancho AHORA");
            Check(Codigo.Llama(upd, "LayoutControls"), "y si es otro, vuelve a acomodar la pantalla");
        }

        Console.WriteLine("\n  3) PERO NO POR NADA\n");
        // "Creditos 1" y "Creditos 2" miden lo mismo: acomodar la pantalla entera en cada credito seria
        // rehacerla por gusto veinte veces en una noche movida.
        int a1b, a2b;
        Capsula(anc, "Creditos", "1", 40, out a1b, out z1, out z2);
        Capsula(anc, "Creditos", "2", 40, out a2b, out z1, out z2);
        Console.WriteLine("     \"Creditos 1\" mide " + a1b + " px y \"Creditos 2\" mide " + a2b);
        Check(a1b == a2b, "un credito mas no cambia el ancho...");
        if (upd != null)
        {
            int lee, esc;
            Codigo.UsosDeCampo(upd, "pastCredAncho", out lee, out esc);
            Check(lee > 0 && esc > 0, "...y la rocola se acuerda del ancho que ya tenia, para no acomodar por gusto");
        }

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
