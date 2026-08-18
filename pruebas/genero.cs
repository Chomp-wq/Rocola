using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// EL GENERO DE CADA CANCION DENTRO DEL DISCO, al lado de la duracion.
//
// Lo delicado no es pintarlo: es que quepa. En una fila conviven el numero, el nombre de la cancion, la
// marca de "ya pedida", el genero y la duracion. Si el genero se encima con la duracion, o le come el sitio
// al nombre, se ve mal justo en la pantalla que mas mira el cliente. Aqui se dibuja de verdad y se miran
// los pixeles.
class Genero
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type tLista, TF;
    const int ROWH = 34;
    static readonly Color FONDO = Color.FromArgb(255, 12, 15, 34);

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    static string[] generos, textos;
    static int[] cola;

    static Bitmap Pinta(int w, int filas, bool conGenero, bool conCola, string textoLargo)
    {
        Control c = (Control)Activator.CreateInstance(tLista, true);
        c.SetBounds(0, 0, w, ROWH * filas);
        c.Font = new Font(Fam(), 11f, FontStyle.Bold);
        tLista.GetField("RowH").SetValue(c, ROWH);
        tLista.GetField("Sel").SetValue(c, -1);
        tLista.GetField("Accent").SetValue(c, Color.FromArgb(178, 152, 255));
        tLista.GetField("GetCount").SetValue(c, new Func<int>(delegate { return filas; }));
        tLista.GetField("GetText").SetValue(c, new Func<int, string>(delegate (int i) { return textoLargo ?? textos[i % textos.Length]; }));
        tLista.GetField("GetRight").SetValue(c, new Func<int, string>(delegate (int i) { return "4:32"; }));
        bool cc = conCola;
        tLista.GetField("GetQueuePos").SetValue(c, new Func<int, int>(delegate (int i) { return cc ? cola[i % cola.Length] : 0; }));
        bool cg = conGenero;
        tLista.GetField("GetGenero").SetValue(c, new Func<int, string>(delegate (int i) { return cg ? generos[i % generos.Length] : ""; }));
        Bitmap b = new Bitmap(w, ROWH * filas, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(FONDO);
            tLista.GetMethod("RenderContent", new Type[] { typeof(Graphics) }).Invoke(c, new object[] { g });
        }
        return b;
    }

    static bool Tinta(Color p) { return Math.Abs(p.R - FONDO.R) + Math.Abs(p.G - FONDO.G) + Math.Abs(p.B - FONDO.B) > 24; }

    // Columnas con algo dibujado en esa fila, dentro de [xa,xb)
    static bool HayTinta(Bitmap b, int fila, int xa, int xb)
    {
        if (xa < 0) xa = 0; if (xb > b.Width) xb = b.Width;
        for (int y = fila * ROWH + 2; y < (fila + 1) * ROWH - 2; y++)
            for (int x = xa; x < xb; x++) if (Tinta(b.GetPixel(x, y))) return true;
        return false;
    }

    // Todas las franjas verticales VACIAS de una fila, para localizar los huecos entre elementos.
    static List<int> Huecos(Bitmap b, int fila, int desdeX)
    {
        List<int> h = new List<int>();
        for (int x = desdeX; x < b.Width; x++)
        {
            bool vacia = true;
            for (int y = fila * ROWH + 2; y < (fila + 1) * ROWH - 2 && vacia; y++) if (Tinta(b.GetPixel(x, y))) vacia = false;
            if (vacia) h.Add(x);
        }
        return h;
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            if (t.Name == "SongListView") tLista = t;
        Check(tLista != null, "la lista del interior del disco esta donde tiene que estar");
        if (tLista == null) return 1;
        Check(tLista.GetField("GetGenero") != null, "la lista sabe pedir el genero de cada cancion");
        if (tLista.GetField("GetGenero") == null) return 1;

        generos = new string[] { "Rock", "Balada", "Cumbia", "Banda", "Pop" };
        textos = new string[] { "Cancion corta", "Una cancion con un nombre bastante mas largo de lo normal", "Otra" };
        cola = new int[] { 0, 1, 0, 0, 2, 0 };

        // ---------- 1) LA PASTILLA SALE, Y SOLO CUANDO HAY GENERO ----------
        Console.WriteLine("\n  1) LA PASTILLA DE GENERO SALE EN SU SITIO\n");
        const int LW = 620, FIL = 6;
        // Con nombres CORTOS a proposito: asi lo unico que puede aparecer en esa franja es la pastilla.
        // (Con un nombre largo el texto usa ese hueco cuando no hay pastilla, que es justo lo que debe hacer.)
        Bitmap sin = Pinta(LW, FIL, false, false, "Corta");
        Bitmap con = Pinta(LW, FIL, true, false, "Corta");
        // La franja donde vive el genero: entre el nombre y la duracion. Sin genero tiene que estar limpia.
        int xa = LW - 210, xb = LW - 78;
        int filasSin = 0, filasCon = 0;
        for (int r = 0; r < FIL; r++)
        {
            if (HayTinta(sin, r, xa, xb)) filasSin++;
            if (HayTinta(con, r, xa, xb)) filasCon++;
        }
        Console.WriteLine("     sin genero, filas con algo en esa franja: " + filasSin + " (deben ser 0)");
        Console.WriteLine("     con genero, filas con algo en esa franja: " + filasCon + " (deben ser " + FIL + ")");
        Check(filasSin == 0, "cuando aun no se sabe el genero no se pinta nada de mas");
        Check(filasCon == FIL, "cuando se sabe, todas las filas llevan su pastilla");

        // ---------- 2) NO SE TOCA CON LA DURACION ----------
        // Entre la pastilla y la duracion tiene que quedar un hueco limpio de arriba a abajo.
        Console.WriteLine("\n  2) NO SE ENCIMA CON LA DURACION NI CON LA MARCA DE PEDIDA\n");
        int sinHueco = 0, huecoMin = 9999;
        for (int r = 0; r < FIL; r++)
        {
            List<int> hs = Huecos(con, r, LW - 120);
            // el hueco mas ancho entre la pastilla y la duracion
            int mejor = 0, run = 0;
            for (int i = 0; i < hs.Count; i++)
            {
                if (i > 0 && hs[i] == hs[i - 1] + 1) run++; else run = 1;
                if (run > mejor) mejor = run;
            }
            if (mejor < 3) sinHueco++;
            if (mejor < huecoMin) huecoMin = mejor;
        }
        Console.WriteLine("     hueco limpio mas estrecho entre pastilla y duracion: " + huecoMin + " px");
        Check(sinHueco == 0, "entre la pastilla y la duracion siempre queda un hueco (no se rozan)");

        // Con canciones YA PEDIDAS conviven cuatro cosas en el lado derecho.
        Bitmap conTodo = Pinta(LW, FIL, true, true, null);
        int pegados = 0;
        for (int r = 0; r < FIL; r++)
        {
            if (cola[r % cola.Length] == 0) continue;
            List<int> hs = Huecos(conTodo, r, LW - 260);
            int trozos = 0;
            for (int i = 0; i < hs.Count; i++) if (i == 0 || hs[i] != hs[i - 1] + 1) trozos++;
            // marca de pedida | pastilla | duracion  ->  al menos dos huecos que las separen
            if (trozos < 2) pegados++;
        }
        Check(pegados == 0, "con la marca de \"ya pedida\" puesta, las tres cosas siguen separadas");

        // ---------- 3) EL NOMBRE NO SE PIERDE ----------
        // Un nombre larguisimo tiene que cortarse ANTES de la pastilla, no pintarse por debajo.
        Console.WriteLine("\n  3) UN NOMBRE LARGUISIMO SE CORTA, NO SE ENCIMA\n");
        string larguisimo = "Nombre de cancion absurdamente largo que no cabe de ninguna manera en la fila " +
                            "y que tiene que cortarse con puntos suspensivos antes de llegar a la pastilla";
        Bitmap largo = Pinta(LW, 3, true, false, larguisimo);
        int encimados = 0;
        for (int r = 0; r < 3; r++)
        {
            List<int> hs = Huecos(largo, r, LW - 260);
            int trozos = 0;
            for (int i = 0; i < hs.Count; i++) if (i == 0 || hs[i] != hs[i - 1] + 1) trozos++;
            if (trozos < 2) encimados++;   // nombre | pastilla | duracion
        }
        Check(encimados == 0, "con el nombre mas largo posible siguen viendose separados nombre, genero y duracion");

        // ---------- 4) EN PANTALLA ESTRECHA MANDA EL NOMBRE ----------
        Console.WriteLine("\n  4) EN UNA LISTA ESTRECHA LA PASTILLA SE APARTA\n");
        int[] anchos = { 200, 260, 320, 420, 620, 900 };
        int rotos = 0;
        foreach (int w in anchos)
        {
            Bitmap b = null;
            try { b = Pinta(w, 3, true, true, null); }
            catch (Exception e) { Console.WriteLine("     revento a " + w + " px: " + e.GetType().Name); rotos++; continue; }
            // nada puede pintarse fuera de la lista ni quedarse sin nombre visible
            bool hayNombre = HayTinta(b, 0, 40, Math.Min(w - 10, 130));
            if (!hayNombre) { rotos++; Console.WriteLine("     a " + w + " px no se ve el nombre de la cancion"); }
            b.Dispose();
        }
        Console.WriteLine("     anchos probados: " + anchos.Length + "   con problemas: " + rotos);
        Check(rotos == 0, "de 200 a 900 px de ancho el nombre siempre se ve y nada revienta");

        // ---------- 5) SIN REVENTAR CON DATOS RAROS ----------
        Console.WriteLine("\n  5) CON GENEROS RAROS O SIN GENERO\n");
        string[] raros = { "", null, " ", "Un genero con un nombre larguisimo que jamas cabria", "Rock & Roll", "Ñu", "日本" };
        int malos = 0;
        foreach (string ra in raros)
        {
            try
            {
                Control c = (Control)Activator.CreateInstance(tLista, true);
                c.SetBounds(0, 0, 620, ROWH * 3);
                c.Font = new Font(Fam(), 11f, FontStyle.Bold);
                tLista.GetField("RowH").SetValue(c, ROWH);
                tLista.GetField("Sel").SetValue(c, 0);
                tLista.GetField("Accent").SetValue(c, Color.FromArgb(178, 152, 255));
                tLista.GetField("GetCount").SetValue(c, new Func<int>(delegate { return 3; }));
                tLista.GetField("GetText").SetValue(c, new Func<int, string>(delegate (int i) { return "Cancion " + i; }));
                tLista.GetField("GetRight").SetValue(c, new Func<int, string>(delegate (int i) { return "4:32"; }));
                tLista.GetField("GetQueuePos").SetValue(c, new Func<int, int>(delegate (int i) { return 0; }));
                string rr = ra;
                tLista.GetField("GetGenero").SetValue(c, new Func<int, string>(delegate (int i) { return rr; }));
                using (Bitmap b = new Bitmap(620, ROWH * 3, PixelFormat.Format32bppArgb))
                using (Graphics g = Graphics.FromImage(b))
                { g.Clear(FONDO); tLista.GetMethod("RenderContent", new Type[] { typeof(Graphics) }).Invoke(c, new object[] { g }); }
            }
            catch (Exception e) { malos++; Console.WriteLine("     revento con \"" + (ra ?? "nulo") + "\": " + e.GetType().Name); }
        }
        // y sin nadie que conteste al genero (una lista que aun no esta conectada)
        try
        {
            Control c = (Control)Activator.CreateInstance(tLista, true);
            c.SetBounds(0, 0, 620, ROWH * 2);
            c.Font = new Font(Fam(), 11f, FontStyle.Bold);
            tLista.GetField("RowH").SetValue(c, ROWH);
            tLista.GetField("GetCount").SetValue(c, new Func<int>(delegate { return 2; }));
            tLista.GetField("GetText").SetValue(c, new Func<int, string>(delegate (int i) { return "X"; }));
            using (Bitmap b = new Bitmap(620, ROWH * 2, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(b))
            { g.Clear(FONDO); tLista.GetMethod("RenderContent", new Type[] { typeof(Graphics) }).Invoke(c, new object[] { g }); }
        }
        catch (Exception e) { malos++; Console.WriteLine("     revento sin nadie que conteste al genero: " + e.GetType().Name); }
        Check(malos == 0, "generos vacios, nulos, larguisimos o de otro alfabeto: no revienta ninguno");

        // ---------- 6) LAS PASTILLAS SE HORNEAN UNA VEZ ----------
        // Si se dibujaran a mano en cada fila y en cada repintado, la lista se encareceria mucho. Se comprueba
        // que repintar mil veces no crea basura nueva (las pastillas ya estan hechas y solo se copian).
        Console.WriteLine("\n  6) LAS PASTILLAS SE HORNEAN UNA SOLA VEZ\n");
        {
            Control c = (Control)Activator.CreateInstance(tLista, true);
            c.SetBounds(0, 0, 620, ROWH * 11);
            c.Font = new Font(Fam(), 11f, FontStyle.Bold);
            tLista.GetField("RowH").SetValue(c, ROWH);
            tLista.GetField("Sel").SetValue(c, -1);
            tLista.GetField("Accent").SetValue(c, Color.FromArgb(178, 152, 255));
            tLista.GetField("GetCount").SetValue(c, new Func<int>(delegate { return 11; }));
            tLista.GetField("GetText").SetValue(c, new Func<int, string>(delegate (int i) { return "Cancion " + i; }));
            tLista.GetField("GetRight").SetValue(c, new Func<int, string>(delegate (int i) { return "4:32"; }));
            tLista.GetField("GetQueuePos").SetValue(c, new Func<int, int>(delegate (int i) { return 0; }));
            tLista.GetField("GetGenero").SetValue(c, new Func<int, string>(delegate (int i) { return generos[i % generos.Length]; }));
            MethodInfo rc = tLista.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
            using (Bitmap b = new Bitmap(620, ROWH * 11, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(b))
            {
                object[] args = new object[] { g };
                for (int i = 0; i < 50; i++) rc.Invoke(c, args);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long antes = GC.GetTotalMemory(true);
                for (int i = 0; i < 1000; i++) rc.Invoke(c, args);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long despues = GC.GetTotalMemory(true);
                long porPintada = (despues - antes) / 1000;
                Console.WriteLine("     memoria retenida por repintado: " + porPintada + " bytes (mil repintados de 11 filas)");
                Check(porPintada < 200, "repintar la lista una y otra vez no acumula memoria: las pastillas ya estan hechas");
            }
        }

        // Una imagen para mirarla con los ojos.
        using (Bitmap muestra = Pinta(760, 6, true, true, null)) muestra.Save("genero_muestra.png", ImageFormat.Png);
        Console.WriteLine("\n     (imagen de muestra guardada en genero_muestra.png)");

        sin.Dispose(); con.Dispose(); conTodo.Dispose(); largo.Dispose();
        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
