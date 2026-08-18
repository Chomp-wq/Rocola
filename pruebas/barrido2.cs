using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// BARRIDO de lo que se acaba de tocar: la barra de letras y el encabezado.
//
// Las otras baterias comprueban que en condiciones NORMALES se ve bien. Esta comprueba que en condiciones
// ABSURDAS no se rompe: tamanos imposibles, listas vacias, numeros disparatados, la letra elegida fuera de la
// lista, cambios de letra a toda velocidad. En una rocola que se queda encendida meses, "no se rompe nunca"
// vale mas que "se ve bonito".
class Barrido2
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type tBarra, tCab, tForm;
    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    // Dibuja y devuelve el fallo si lo hubo (null = bien).
    static string Pinta(Control c, int w, int h)
    {
        try
        {
            if (w < 1) w = 1;
            if (h < 1) h = 1;
            using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                MethodInfo rc = c.GetType().GetMethod("RenderContent", new Type[] { typeof(Graphics) });
                rc.Invoke(c, new object[] { g });
            }
            return null;
        }
        catch (TargetInvocationException e) { return e.InnerException.GetType().Name + ": " + e.InnerException.Message; }
        catch (Exception e) { return e.GetType().Name + ": " + e.Message; }
    }

    static Control NuevaBarra(int w, int h)
    {
        Control c = (Control)Activator.CreateInstance(tBarra, true);
        c.SetBounds(0, 0, Math.Max(1, w), Math.Max(1, h));
        c.Font = new Font(Fam(), 9f);
        return c;
    }
    static void Pon(Control c, string campo, object v) { c.GetType().GetField(campo).SetValue(c, v); }
    static void PonProp(Control c, string prop, object v) { c.GetType().GetProperty(prop).SetValue(c, v, null); }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        tForm = asm.GetType("Rocola.Form1", true);
        foreach (Type t in tForm.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "LetterBar") tBarra = t;
            if (t.Name == "HeaderView") tCab = t;
        }
        if (tBarra == null || tCab == null) { Console.WriteLine("faltan controles"); return 1; }

        string letras = "★#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";

        // ---------- 1) TAMANOS IMPOSIBLES ----------
        Console.WriteLine("\n  1) TAMANOS IMPOSIBLES\n");
        {
            int[] ws = { 0, 1, 3, 4, 5, 29, 30, 60, 220, 996, 4000, 12000 };
            int[] hs = { 0, 1, 3, 4, 5, 12, 33, 34, 40, 104, 400 };
            int rotos = 0; string primero = null;
            for (int i = 0; i < ws.Length; i++)
                for (int j = 0; j < hs.Length; j++)
                {
                    Control c = NuevaBarra(ws[i], hs[j]);
                    Pon(c, "Letters", letras);
                    Dictionary<char, int> cu = new Dictionary<char, int>();
                    foreach (char ch in letras) cu[ch] = 7;
                    Pon(c, "Counts", cu);
                    string err = Pinta(c, ws[i], hs[j]);
                    if (err != null) { rotos++; if (primero == null) primero = ws[i] + "x" + hs[j] + " -> " + err; }
                }
            Console.WriteLine("     combinaciones probadas: " + (ws.Length * hs.Length) + "   rotas: " + rotos);
            if (primero != null) Console.WriteLine("     primera: " + primero);
            Check(rotos == 0, "la barra se dibuja a cualquier tamano, hasta a 0x0, sin reventar");
        }

        // ---------- 2) DATOS ABSURDOS ----------
        Console.WriteLine("\n  2) DATOS ABSURDOS\n");
        {
            int rotos = 0; string primero = null;
            string[] listas = { letras, "", "A", "AB", new string('Z', 200) };
            for (int L = 0; L < listas.Length; L++)
            {
                object[][] casos = {
                    new object[]{ "sin nada",        null, null, null },
                    new object[]{ "cuentas vacias",  new Dictionary<char,int>(), new Dictionary<char,Color>(), null },
                    new object[]{ "cuentas en cero", null, null, 0 },
                    new object[]{ "cuentas negativas", null, null, -50 },
                    new object[]{ "cuentas enormes", null, null, int.MaxValue },
                    new object[]{ "un millon",       null, null, 1000000 },
                };
                for (int k = 0; k < casos.Length; k++)
                {
                    Control c = NuevaBarra(996, 40);
                    Pon(c, "Letters", listas[L]);
                    if (casos[k][3] != null)
                    {
                        Dictionary<char, int> cu = new Dictionary<char, int>();
                        Dictionary<char, Color> ti = new Dictionary<char, Color>();
                        foreach (char ch in listas[L]) { cu[ch] = (int)casos[k][3]; ti[ch] = Color.FromArgb(3, 4, 5); }
                        Pon(c, "Counts", cu); Pon(c, "Tints", ti);
                    }
                    else { Pon(c, "Counts", casos[k][1]); Pon(c, "Tints", casos[k][2]); }
                    HashSet<char> disp = new HashSet<char>();
                    if (listas[L].Length > 0) disp.Add(listas[L][0]);
                    Pon(c, "Available", disp);
                    string err = Pinta(c, 996, 40);
                    if (err != null) { rotos++; if (primero == null) primero = "lista de " + listas[L].Length + " con " + casos[k][0] + " -> " + err; }
                }
            }
            Console.WriteLine("     combinaciones probadas: " + (listas.Length * 6) + "   rotas: " + rotos);
            if (primero != null) Console.WriteLine("     primera: " + primero);
            Check(rotos == 0, "listas vacias, sin datos, cuentas en cero, negativas o de mil millones: aguanta todo");
        }

        // ---------- 3) UN NUMERO LARGO NO SE SALE DE SU CASILLA ----------
        Console.WriteLine("\n  3) UN NUMERO LARGO NO SE SALE DE SU CASILLA\n");
        {
            int[] cuentas = { 9, 99, 999, 9999, 10000, 250000, 9999999, int.MaxValue };
            float slot = 996f / letras.Length;
            int fuera = 0; string peor = null;
            for (int k = 0; k < cuentas.Length; k++)
            {
                Control c = NuevaBarra(996, 40);
                Pon(c, "Letters", letras);
                Dictionary<char, int> cu = new Dictionary<char, int>();
                HashSet<char> disp = new HashSet<char>();
                foreach (char ch in letras) { cu[ch] = cuentas[k]; disp.Add(ch); }
                Pon(c, "Counts", cu); Pon(c, "Available", disp);
                using (Bitmap b = new Bitmap(996, 40, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(b))
                    { g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
                      tBarra.GetMethod("RenderContent", new Type[] { typeof(Graphics) }).Invoke(c, new object[] { g }); }
                    // Se mira la casilla de la D. OJO: no vale mirar mas alla de sus bordes, porque TODAS las
                    // casillas llevan el mismo numero y la tinta de la vecina se colaria en la medida. Lo que
                    // se comprueba es que dentro de la casilla quede MARGEN a los dos lados: si el numero
                    // llegara al borde, estaria tocando al de al lado.
                    int i = letras.IndexOf('D');
                    int xa = (int)(i * slot), xb = (int)((i + 1) * slot);
                    int x0 = int.MaxValue, x1 = -1;
                    for (int y = 28; y < 40; y++)
                        for (int x = xa; x < xb; x++)
                            if (b.GetPixel(x, y).A >= 150) { if (x < x0) x0 = x; if (x > x1) x1 = x; }
                    int ancho = (x1 >= 0) ? (x1 - x0 + 1) : 0;
                    int margenIz = (x1 >= 0) ? (x0 - xa) : 99, margenDe = (x1 >= 0) ? (xb - 1 - x1) : 99;
                    bool ok = (ancho > 0) && margenIz >= 2 && margenDe >= 2;
                    Console.WriteLine(string.Format("     {0,10} canciones -> ocupa {1,3} px de los {2:0}, con {3} y {4} px de margen",
                                                    cuentas[k], ancho, slot, margenIz, margenDe));
                    if (!ok) { fuera++; if (peor == null) peor = cuentas[k] + " deja margenes de " + margenIz + " y " + margenDe; }
                }
            }
            if (peor != null) Console.WriteLine("     se sale: " + peor);
            Check(fuera == 0, "ni con millones de canciones el numero invade la casilla de al lado");
        }

        // ---------- 4) LA LETRA ELEGIDA FUERA DE LA LISTA ----------
        Console.WriteLine("\n  4) LA LETRA ELEGIDA FUERA DE LA LISTA\n");
        {
            Control c = NuevaBarra(996, 40);
            Pon(c, "Letters", letras);
            PonProp(c, "Current", 'Ñ');   // una letra que no esta en la barra
            string err = Pinta(c, 996, 40);
            Check(err == null, "elegir una letra que no esta en la barra no rompe nada (" + (err ?? "sin fallo") + ")");
            float cx, cy, r;
            object[] args = new object[] { 0f, 0f, 0f };
            bool hay = (bool)tBarra.GetMethod("GetCurrentGlow").Invoke(c, args);
            Check(!hay, "y el programa sabe que no hay casilla que iluminar");
            cx = (float)args[0]; cy = (float)args[1]; r = (float)args[2];
            Check(cx == 0f && cy == 0f && r == 0f, "sin dejar coordenadas sueltas de la vez anterior");
        }

        // ---------- 5) CAMBIOS DE LETRA A TODA VELOCIDAD ----------
        // Un cliente aporreando las teclas: el deslizamiento se relanza mil veces sin dejar terminar.
        Console.WriteLine("\n  5) CAMBIOS DE LETRA A TODA VELOCIDAD\n");
        {
            Control c = NuevaBarra(996, 40);
            Pon(c, "Letters", letras);
            Dictionary<char, int> cu = new Dictionary<char, int>();
            HashSet<char> disp = new HashSet<char>();
            foreach (char ch in letras) { cu[ch] = 12; disp.Add(ch); }
            Pon(c, "Counts", cu); Pon(c, "Available", disp);
            Random rnd = new Random(7);
            int rotos = 0; string primero = null;
            for (int i = 0; i < 400; i++)
            {
                PonProp(c, "Current", letras[rnd.Next(letras.Length)]);
                string err = Pinta(c, 996, 40);
                if (err != null) { rotos++; if (primero == null) primero = err; }
            }
            Console.WriteLine("     400 cambios de letra seguidos, dibujando en cada uno: rotos = " + rotos);
            if (primero != null) Console.WriteLine("     " + primero);
            Check(rotos == 0, "aporrear las letras no rompe el deslizamiento");
            // y al final, dejandolo en paz, la animacion TERMINA (si no, en modo GPU la textura se
            // rehorneaia para siempre y se comeria la maquina)
            System.Threading.Thread.Sleep(260);
            bool viva = (bool)tBarra.GetProperty("AnimViva").GetValue(c, null);
            Check(!viva, "y al dejarla en paz la animacion se apaga, no se queda corriendo para siempre");
        }

        // ---------- 6) EL ENCABEZADO, CON LO QUE LE ECHEN ----------
        Console.WriteLine("\n  6) EL ENCABEZADO, CON LO QUE LE ECHEN\n");
        {
            string[] secs = { null, "", "A", "LO MAS SONADO", new string('W', 300) };
            int[][] pgs = { new int[]{0,1}, new int[]{0,0}, new int[]{-5,3}, new int[]{99,3}, new int[]{0,-2},
                            new int[]{0,int.MaxValue}, new int[]{int.MaxValue,int.MaxValue} };
            int[] ws = { 0, 1, 8, 40, 300, 996, 4000 };
            int[] hs = { 0, 1, 8, 28, 52, 200 };
            int rotos = 0; string primero = null;
            for (int a = 0; a < ws.Length; a++)
                for (int b2 = 0; b2 < hs.Length; b2++)
                    for (int i = 0; i < secs.Length; i++)
                        for (int j = 0; j < pgs.Length; j++)
                        {
                            Control c = (Control)Activator.CreateInstance(tCab, true);
                            c.SetBounds(0, 0, Math.Max(1, ws[a]), Math.Max(1, hs[b2]));
                            c.Font = new Font(Fam(), 9f);
                            Pon(c, "Section", secs[i]); Pon(c, "Page", pgs[j][0]); Pon(c, "Total", pgs[j][1]);
                            Pon(c, "Accent", Color.FromArgb(225, 120, 30));
                            string err = Pinta(c, ws[a], hs[b2]);
                            if (err != null)
                            {
                                rotos++;
                                if (primero == null) primero = ws[a] + "x" + hs[b2] + ", seccion de " + (secs[i] == null ? "nula" : secs[i].Length + " letras")
                                    + ", pagina " + pgs[j][0] + " de " + pgs[j][1] + " -> " + err;
                            }
                        }
            int total = ws.Length * hs.Length * secs.Length * pgs.Length;
            Console.WriteLine("     combinaciones probadas: " + total + "   rotas: " + rotos);
            if (primero != null) Console.WriteLine("     primera: " + primero);
            Check(rotos == 0, "el encabezado aguanta nombre nulo, 300 letras, paginas negativas y hasta 2 mil millones");
        }

        // ---------- 7) QUE NO SE ACUMULE MEMORIA ----------
        // La tira se cachea y se rehace al cambiar los datos. Si cada cambio dejara un mapa de bits sin
        // soltar, una rocola encendida un mes se quedaria sin memoria.
        Console.WriteLine("\n  7) QUE NO SE ACUMULE MEMORIA\n");
        {
            Control c = NuevaBarra(996, 40);
            Pon(c, "Letters", letras);
            HashSet<char> disp = new HashSet<char>();
            foreach (char ch in letras) disp.Add(ch);
            Pon(c, "Available", disp);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long antes = GC.GetTotalMemory(true);
            for (int i = 0; i < 600; i++)
            {
                Dictionary<char, int> cu = new Dictionary<char, int>();
                foreach (char ch in letras) cu[ch] = i;      // los datos cambian -> la tira se rehace SIEMPRE
                Pon(c, "Counts", cu);
                Pinta(c, 996, 40);
            }
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long despues = GC.GetTotalMemory(true);
            long crecio = (despues - antes) / 1024;
            Console.WriteLine("     600 reconstrucciones de la tira -> la memoria crecio " + crecio + " KB");
            Check(crecio < 2048, "rehacer la tira 600 veces no deja mapas de bits sueltos");
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
