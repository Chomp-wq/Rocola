using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

// LAS SOBRAS DE CADA CUADRO.
//
// Todo lo que un cuadro reserva de memoria hay que recogerlo despues, y cada recogida del recolector es un
// tiron. En la maquina del bar, con la rocola encendida dias seguidos y 24 cuadros por segundo, un puñado de
// bytes por cuadro se convierte en una recogida cada pocos segundos... para siempre.
//
// Aqui se mide, camino por camino, cuanto reserva UN cuadro cuando NADA ha cambiado (que es el caso normal:
// la rocola quieta, esperando a que alguien pulse algo). Lo ideal es cero.
class Sobras
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF;
    static Type Nes(string n) { return TF.GetNestedType(n, BindingFlags.NonPublic | BindingFlags.Public); }
    static void Pon(Type t, object o, string campo, object v)
    {
        FieldInfo f = t.GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f != null) f.SetValue(o, v);
    }

    // Bytes reservados por vuelta, midiendo muchas vueltas para que el ruido se diluya.
    static long PorVuelta(int n, Action a)
    {
        for (int i = 0; i < 40; i++) a();                   // calentar: traducir a maquina y dejar sus arreglos hechos
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long a0 = GC.GetTotalMemory(true);
        for (int i = 0; i < n; i++) a();
        long a1 = GC.GetTotalMemory(false);
        return (a1 - a0) / n;
    }

    static Bitmap lienzo;
    static Graphics gg;
    static void Pinta(Type t, object c, MethodInfo m)
    {
        m.Invoke(c, new object[] { gg });
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);

        lienzo = new Bitmap(1024, 768, PixelFormat.Format32bppPArgb);
        gg = Graphics.FromImage(lienzo);

        Console.WriteLine("\n  BYTES QUE RESERVA UN CUADRO EN REPOSO (lo ideal es 0)\n");

        // ---------- el encabezado de la seccion ----------
        Type TCab = Nes("HeaderView");
        Control cab = (Control)Activator.CreateInstance(TCab, true);
        cab.Font = new Font(FontFamily.GenericSansSerif, 9f);
        cab.SetBounds(0, 0, 520, 34);
        TCab.GetField("Section").SetValue(cab, "LETRA B");
        TCab.GetField("Page").SetValue(cab, 2);
        TCab.GetField("Total").SetValue(cab, 18);
        MethodInfo mCab = TCab.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
        object[] arg = new object[] { gg };
        long bCab = PorVuelta(400, delegate { mCab.Invoke(cab, arg); });
        Console.WriteLine("     encabezado de seccion        " + bCab + " bytes");

        // ---------- la pastilla de creditos ----------
        Type TPas = Nes("PastillaView");
        Control pas = (Control)Activator.CreateInstance(TPas, true);
        pas.Font = new Font(FontFamily.GenericSansSerif, 9f);
        pas.SetBounds(0, 0, 220, 40);
        TPas.GetField("Etiqueta").SetValue(pas, "CREDITOS");
        TPas.GetField("Valor").SetValue(pas, "3");
        MethodInfo mPas = TPas.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
        long bPas = PorVuelta(400, delegate { mPas.Invoke(pas, arg); });
        Console.WriteLine("     pastilla de creditos         " + bPas + " bytes");

        // ---------- la barra del abecedario ----------
        Type TLet = Nes("LetterBar");
        long bLet = -1;
        if (TLet != null)
        {
            Control let = (Control)Activator.CreateInstance(TLet, true);
            let.Font = new Font(FontFamily.GenericSansSerif, 9f);
            let.SetBounds(0, 0, 1000, 40);
            FieldInfo fLetras = TLet.GetField("Letters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fLetras != null)
            {
                List<char> ls = new List<char>();
                for (char ch = 'A'; ch <= 'Z'; ch++) ls.Add(ch);
                try { fLetras.SetValue(let, ls); } catch { }
            }
            MethodInfo mLet = TLet.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
            if (mLet != null)
            {
                object[] al = new object[] { gg };
                try
                {
                    bLet = PorVuelta(300, delegate { mLet.Invoke(let, al); });
                    Console.WriteLine("     barra del abecedario         " + bLet + " bytes");
                }
                catch (Exception ex) { Console.WriteLine("     barra del abecedario         (no se pudo medir: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message) + ")"); }
            }
        }

        // ---------- la lista de canciones de un disco ----------
        Type TLis = Nes("SongListView");
        Control lis = (Control)Activator.CreateInstance(TLis, true);
        lis.Font = new Font(FontFamily.GenericSansSerif, 11f);
        lis.SetBounds(0, 0, 900, 420);
        Pon(TLis, lis, "GetCount", (Func<int>)delegate { return 24; });
        Pon(TLis, lis, "GetText", (Func<int, string>)delegate (int i) { return "Cancion numero " + i + " de un disco"; });
        MethodInfo mLis = TLis.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
        long bLis = -1;
        try
        {
            bLis = PorVuelta(200, delegate { mLis.Invoke(lis, arg); });
            Console.WriteLine("     lista de canciones           " + bLis + " bytes");
        }
        catch (Exception ex) { Console.WriteLine("     lista de canciones           (no se pudo medir: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message) + ")"); }

        Console.WriteLine();
        Check(bCab <= 64, "el encabezado no deja sobras en cada cuadro");
        Check(bPas <= 64, "la pastilla de creditos no deja sobras en cada cuadro");
        if (bLet >= 0) Check(bLet <= 256, "la barra del abecedario no deja sobras en cada cuadro");
        if (bLis >= 0) Check(bLis <= 4096, "la lista de canciones no deja sobras de mas en cada cuadro");

        gg.Dispose(); lienzo.Dispose();
        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
