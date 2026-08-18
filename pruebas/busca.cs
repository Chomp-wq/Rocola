using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// LO NUEVO: el resaltado de lo tecleado en el buscador, y la marca de "ya pedida" en la lista de un disco.
//
// El resaltado es lo delicado: hay que encontrar lo tecleado ignorando mayusculas y acentos, pero pintar la
// marca sobre el texto ORIGINAL. Si se busca sobre el texto ya limpio de acentos, las posiciones se corren y
// la marca cae una letra mas alla; con "Jose" y "José" eso se nota enseguida.
class Busca
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        Type tf = asm.GetType("Rocola.Form1", true);
        MethodInfo mBusca = tf.GetMethod("BuscaTramo", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo mNorm = tf.GetMethod("Normalize", BindingFlags.Static | BindingFlags.NonPublic);
        Type tLista = null;
        foreach (Type t in tf.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            if (t.Name == "SongListView") tLista = t;

        Check(mBusca != null, "la busqueda del tramo tecleado esta donde tiene que estar");
        if (mBusca == null) return 1;

        // ---------- 1) DONDE CAE LO TECLEADO ----------
        // Cada caso: texto, lo tecleado, y que TROZO del texto original tiene que quedar marcado.
        Console.WriteLine("\n  1) DONDE CAE LO TECLEADO\n");
        string[][] casos = {
            new string[]{ "Depeche Mode - Enjoy The Silence", "de", "De" },
            new string[]{ "Depeche Mode - Enjoy The Silence", "MODE", "Mode" },
            new string[]{ "Deep Purple", "deep", "Deep" },
            new string[]{ "Jose Jose - El Triste", "jose", "Jose" },
            // con acentos: lo tecleado va SIN tilde y el texto CON tilde
            new string[]{ "José José - El Triste", "jose", "José" },
            new string[]{ "Canción Bonita", "cancion", "Canción" },
            new string[]{ "Canción Bonita", "bonita", "Bonita" },
            // la tilde a la mitad: lo marcado tiene que arrancar DESPUES de ella
            new string[]{ "Los Ángeles Azules", "azules", "Azules" },
            new string[]{ "Maná - Rayando El Sol", "rayando", "Rayando" },
            new string[]{ "Rocío Durcal", "durcal", "Durcal" },
            new string[]{ "Rocío Durcal", "rocio", "Rocío" },
        };
        int malos = 0; string primero = null;
        for (int i = 0; i < casos.Length; i++)
        {
            string texto = casos[i][0], q = casos[i][1], esperado = casos[i][2];
            string qn = (string)mNorm.Invoke(null, new object[] { q });
            object[] args = new object[] { texto, qn, 0, 0 };
            bool hay = (bool)mBusca.Invoke(null, args);
            int ini = (int)args[2], largo = (int)args[3];
            string salio = hay ? texto.Substring(ini, largo) : "(no encontro)";
            bool ok = hay && salio == esperado;
            Console.WriteLine(string.Format("     {0,-34} tecleado \"{1,-8}\" -> marca \"{2}\"{3}",
                                            texto, q, salio, ok ? "" : "   ESPERABA \"" + esperado + "\""));
            if (!ok) { malos++; if (primero == null) primero = texto + " / " + q; }
        }
        Check(malos == 0, "la marca cae EXACTAMENTE sobre lo tecleado, tambien con tildes de por medio");

        // ---------- 2) CUANDO NO HAY QUE MARCAR NADA ----------
        Console.WriteLine("\n  2) CUANDO NO HAY QUE MARCAR NADA\n");
        object[][] vacios = {
            new object[]{ "Depeche Mode", "zzz" },
            new object[]{ "Depeche Mode", "" },
            new object[]{ "", "de" },
            new object[]{ null, "de" },
            new object[]{ "Depeche Mode", null },
            new object[]{ "De", "depeche mode larguisimo" },
        };
        int marcaMal = 0;
        for (int i = 0; i < vacios.Length; i++)
        {
            object[] args = new object[] { vacios[i][0], vacios[i][1], 0, 0 };
            bool hay;
            try { hay = (bool)mBusca.Invoke(null, args); }
            catch (Exception e) { Console.WriteLine("     revento con " + (vacios[i][0] ?? "nulo") + " / " + (vacios[i][1] ?? "nulo") + ": " + e.GetType().Name); marcaMal++; continue; }
            if (hay) { marcaMal++; Console.WriteLine("     marca donde no debe: " + vacios[i][0] + " / " + vacios[i][1]); }
        }
        Console.WriteLine("     casos probados: " + vacios.Length + "   marcas indebidas o reventones: " + marcaMal);
        Check(marcaMal == 0, "sin coincidencia, con texto vacio o con nulos: no marca nada y no revienta");

        // ---------- 3) EL TRAMO SIEMPRE CAE DENTRO DEL TEXTO ----------
        // Un indice fuera de rango reventaria al hacer el Substring para pintar.
        Console.WriteLine("\n  3) EL TRAMO SIEMPRE CAE DENTRO DEL TEXTO\n");
        Random rnd = new Random(9);
        string alfabeto = "aeiouAEIOUáéíóúÁÉÍÓÚñÑ -0123456789xyzXYZ";
        int fuera = 0;
        for (int t = 0; t < 4000; t++)
        {
            int n = rnd.Next(0, 26);
            char[] cs = new char[n];
            for (int i = 0; i < n; i++) cs[i] = alfabeto[rnd.Next(alfabeto.Length)];
            string texto = new string(cs);
            int m = rnd.Next(0, 5);
            char[] qs = new char[m];
            for (int i = 0; i < m; i++) qs[i] = alfabeto[rnd.Next(alfabeto.Length)];
            string qn = (string)mNorm.Invoke(null, new object[] { new string(qs) });
            object[] args = new object[] { texto, qn, 0, 0 };
            bool hay;
            try { hay = (bool)mBusca.Invoke(null, args); }
            catch { fuera++; continue; }
            if (!hay) continue;
            int ini = (int)args[2], largo = (int)args[3];
            if (ini < 0 || largo <= 0 || ini + largo > texto.Length) { fuera++; continue; }
            try { string dummy = texto.Substring(ini, largo); if (dummy == null) fuera++; }
            catch { fuera++; }
        }
        Console.WriteLine("     4000 textos y busquedas al azar (con tildes y enes): fuera de rango = " + fuera);
        Check(fuera == 0, "con cuatro mil combinaciones al azar el tramo nunca se sale del texto");

        // ---------- 4) LA MARCA DE "YA PEDIDA" EN LA LISTA ----------
        Console.WriteLine("\n  4) LA MARCA DE YA PEDIDA\n");
        if (tLista != null)
        {
            const int ROWH = 34, LW = 620, FIL = 6;
            int[] cola = { 0, 1, 0, 0, 2, 0 };
            Bitmap[] img = new Bitmap[2];
            for (int caso = 0; caso < 2; caso++)
            {
                Control c = (Control)Activator.CreateInstance(tLista, true);
                c.SetBounds(0, 0, LW, ROWH * FIL);
                c.Font = new Font(Fam(), 11f, FontStyle.Bold);
                tLista.GetField("RowH").SetValue(c, ROWH);
                tLista.GetField("Sel").SetValue(c, -1);
                tLista.GetField("Accent").SetValue(c, Color.FromArgb(178, 152, 255));
                tLista.GetField("GetCount").SetValue(c, new Func<int>(delegate { return FIL; }));
                tLista.GetField("GetText").SetValue(c, new Func<int, string>(delegate (int i) { return "Cancion " + i; }));
                tLista.GetField("GetRight").SetValue(c, new Func<int, string>(delegate (int i) { return "4:32"; }));
                int cc = caso;
                tLista.GetField("GetQueuePos").SetValue(c, new Func<int, int>(delegate (int i) { return (cc == 0) ? 0 : cola[i]; }));
                Bitmap b = new Bitmap(LW, ROWH * FIL, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.FromArgb(255, 12, 15, 34));
                    tLista.GetMethod("RenderContent", new Type[] { typeof(Graphics) }).Invoke(c, new object[] { g });
                }
                img[caso] = b;
            }
            // El verde de "en cola" solo puede aparecer en las filas pedidas.
            int filasVerdesSin = 0, filasVerdesCon = 0, filasEsperadas = 0;
            for (int r = 0; r < FIL; r++)
            {
                if (cola[r] > 0) filasEsperadas++;
                if (Verde(img[0], r, ROWH, LW)) filasVerdesSin++;
                if (Verde(img[1], r, ROWH, LW)) { if (cola[r] > 0) filasVerdesCon++; else filasVerdesCon += 100; }
            }
            Console.WriteLine("     sin nada pedido, filas con verde: " + filasVerdesSin + " (deben ser 0)");
            Console.WriteLine("     con 2 pedidas,  filas con verde: " + filasVerdesCon + " (deben ser 2, y en las filas correctas)");
            Check(filasVerdesSin == 0, "sin nada pedido no aparece ninguna marca verde");
            Check(filasVerdesCon == filasEsperadas, "con canciones pedidas la marca sale EXACTAMENTE en esas filas");
            // Y el circulito del numero esta siempre, pedida o no.
            int conCirculo = 0;
            for (int r = 0; r < FIL; r++) if (TintaEn(img[0], r, ROWH, 16, 40)) conCirculo++;
            Console.WriteLine("     filas con circulito de numero: " + conCirculo + " de " + FIL);
            Check(conCirculo == FIL, "todas las filas llevan su numero en el circulito");
            for (int i = 0; i < 2; i++) img[i].Dispose();
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }

    // Hay verde de "en cola" en esa fila? (verde claro: el verde manda sobre el rojo)
    static bool Verde(Bitmap b, int fila, int rowH, int w)
    {
        for (int y = fila * rowH + 4; y < (fila + 1) * rowH - 4; y++)
            for (int x = w / 2; x < w - 60; x++)
            {
                Color c = b.GetPixel(x, y);
                if (c.G > 150 && c.G > c.R + 45 && c.G > c.B + 25) return true;
            }
        return false;
    }
    // Hay algo dibujado en esa franja horizontal de la fila?
    static bool TintaEn(Bitmap b, int fila, int rowH, int xa, int xb)
    {
        for (int y = fila * rowH + 3; y < (fila + 1) * rowH - 3; y++)
            for (int x = xa; x < xb; x++)
            {
                Color c = b.GetPixel(x, y);
                if (Math.Abs(c.R - 12) + Math.Abs(c.G - 15) + Math.Abs(c.B - 34) > 24) return true;
            }
        return false;
    }
}
