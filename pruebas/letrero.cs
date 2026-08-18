using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Reflection;
using System.Windows.Forms;

// QUE NINGUN LETRERO SALGA CORTADO NI SE CALLE TENIENDO SITIO.
//
// Han salido ya dos veces cosas de esta familia y las dos son de cara al cliente:
//
//   "LO MAS SONAD"        - le faltaba la ultima letra. GDI+, con su formato normal, RECORTA POR CARACTERES:
//                           si el texto no cabe entero en su caja le quita la ultima letra y lo dibuja mas
//                           corto, sin avisar. Y lo hace incluso dandole la caja EXACTA que acaba de medirse.
//
//   "LETRA B" sin paginas - no decia cuantas paginas hay, con 150 px de sitio libre al lado. El ancho de la
//                           rayita separadora estaba metido dentro de la cuenta de los puntitos, y las letras
//                           con mas de doce paginas no llevan puntitos: el reparto se quedaba corto justo por
//                           ella. Por eso las letras con pocas paginas si se veian bien.
//
// Aqui se vigilan las dos, mirando los PIXELES de lo que de verdad se pinta.
class Letrero
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF, TCab, TPast, TLista;

    // ---------- utilidades de pixeles ----------

    // Columnas con tinta de un dibujo.
    static bool[] Columnas(Bitmap b)
    {
        bool[] col = new bool[b.Width];
        BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] fila = new byte[d.Stride];
            for (int y = 0; y < b.Height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy((IntPtr)(d.Scan0.ToInt64() + (long)y * d.Stride), fila, 0, d.Stride);
                for (int x = 0; x < b.Width; x++) if (fila[x * 4 + 3] > 24) col[x] = true;
            }
        }
        finally { b.UnlockBits(d); }
        return col;
    }

    static int Derecha(bool[] col) { for (int x = col.Length - 1; x >= 0; x--) if (col[x]) return x; return -1; }

    // ---------- el encabezado ----------

    static Control NuevoCab(string sec, int page, int total, int w, int h)
    {
        Control c = (Control)Activator.CreateInstance(TCab, true);
        c.Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Regular);
        c.SetBounds(0, 0, w, h);
        TCab.GetField("Section").SetValue(c, sec);
        TCab.GetField("Page").SetValue(c, page);
        TCab.GetField("Total").SetValue(c, total);
        TCab.GetField("Accent").SetValue(c, Color.FromArgb(240, 150, 190));
        return c;
    }

    static Bitmap PintaCab(string sec, int page, int total, int w, int h)
    {
        Control c = NuevoCab(sec, page, total, w, h);
        Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.Transparent);
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            TCab.GetMethod("RenderContent").Invoke(c, new object[] { g });
        }
        c.Dispose();
        return b;
    }

    // El NOMBRE de la seccion: es el segundo bloque de tinta (el primero es la pastillita de color).
    // Devuelve su ancho y su alto de tinta. El alto delata el CUERPO de la letra: si el nombre no cabe, el
    // encabezado lo encoge -y entonces baja el alto Y el ancho, que es correcto-. Lo que no puede pasar
    // NUNCA es que baje el ancho quedandose el alto igual: eso es que se ha comido una letra.
    static void MideNombre(Bitmap b, out int ancho, out int alto)
    {
        ancho = 0; alto = 0;
        bool[] col = Columnas(b);
        int i = 0, w = b.Width;
        while (i < w && !col[i]) i++;
        while (i < w && col[i]) i++;          // la pastillita
        while (i < w && !col[i]) i++;
        int ini = i, ult = -1, seguidos = 0;
        for (; i < w; i++)
        {
            if (col[i]) { ult = i; seguidos = 0; }
            else { seguidos++; if (seguidos > 10 && ult >= 0) break; }
        }
        if (ult < ini) return;
        ancho = ult - ini + 1;
        int arriba = int.MaxValue, abajo = -1;
        for (int y = 0; y < b.Height; y++)
            for (int x = ini; x <= ult; x++)
                if (b.GetPixel(x, y).A > 24) { if (y < arriba) arriba = y; if (y > abajo) abajo = y; break; }
        alto = (abajo >= arriba) ? (abajo - arriba + 1) : 0;
    }

    // ---------- 1) los formatos con los que se dibuja no recortan ----------

    static StringFormat CampoFormato(Type t, object inst, string campo)
    {
        FieldInfo f = t.GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic);
        return (f == null) ? null : (StringFormat)f.GetValue(inst);
    }

    static int Tinta(string txt, Font f, StringFormat sf, float caja)
    {
        using (Bitmap b = new Bitmap(600, 48, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.DrawString(txt, f, Brushes.White, new RectangleF(4, 0, caja, 48), sf);
            }
            bool[] col = Columnas(b);
            int ini = -1; for (int x = 0; x < col.Length; x++) if (col[x]) { ini = x; break; }
            int fin = Derecha(col);
            return (fin >= ini && ini >= 0) ? (fin - ini + 1) : 0;
        }
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "HeaderView") TCab = t;
            if (t.Name == "PastillaView") TPast = t;
            if (t.Name == "SongListView") TLista = t;
        }
        Check(TCab != null && TPast != null && TLista != null, "el encabezado, la pastilla y la lista estan donde tienen que estar");
        if (TCab == null || TPast == null || TLista == null) return 1;

        Console.WriteLine("\n  1) LOS FORMATOS DE DIBUJO NO RECORTAN\n");
        // Se dibuja una vez con cada control para que cree sus formatos, y luego se miran.
        Control cab = NuevoCab("LO MAS SONADO", 0, 5, 500, 34);
        using (Bitmap b = new Bitmap(500, 34, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(b)) TCab.GetMethod("RenderContent").Invoke(cab, new object[] { g });

        Control past = (Control)Activator.CreateInstance(TPast, true);
        past.Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Regular);
        past.SetBounds(0, 0, 260, 40);
        TPast.GetField("Etiqueta").SetValue(past, "Inserta una moneda");
        TPast.GetField("Valor").SetValue(past, "");
        using (Bitmap b = new Bitmap(260, 40, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(b)) TPast.GetMethod("RenderContent").Invoke(past, new object[] { g });

        StringFormat fCab = CampoFormato(TCab, cab, "sfIzq");
        StringFormat fPast = CampoFormato(TPast, past, "sfCen");
        Check(fCab != null && fCab.Trimming == StringTrimming.None, "el encabezado dibuja SIN recorte por caracteres");
        Check(fPast != null && fPast.Trimming == StringTrimming.None, "la pastilla de creditos dibuja SIN recorte por caracteres");
        Check(fCab != null && (fCab.FormatFlags & StringFormatFlags.NoClip) != 0, "y deja que un glifo asome un pixel antes que perderlo");
        Check(fCab != null && (fCab.FormatFlags & StringFormatFlags.LineLimit) == 0, "y no exige que la linea entera quepa de alto (si no, no pintaria nada)");

        // Y la prueba de verdad: la misma caja, con el formato de antes y con el de ahora.
        using (Font fr = new Font(FontFamily.GenericSansSerif, 15.2f, FontStyle.Bold))
        using (StringFormat viejo = new StringFormat())
        {
            viejo.Alignment = StringAlignment.Near; viejo.LineAlignment = StringAlignment.Center;
            viejo.FormatFlags = StringFormatFlags.NoWrap;
            int libre = Tinta("LO MAS SONADO", fr, fCab, 560);
            int justo = Tinta("LO MAS SONADO", fr, fCab, libre);
            int justoViejo = Tinta("LO MAS SONADO", fr, viejo, libre);
            Console.WriteLine("     \"LO MAS SONADO\" mide " + libre + " px; en una caja de esos mismos " + libre + " px:");
            Console.WriteLine("        con el formato de antes:  " + justoViejo + " px" + (justoViejo < libre ? "   <- le falta el final" : ""));
            Console.WriteLine("        con el de ahora:          " + justo + " px");
            Check(justo >= libre, "con la caja justa el texto sale ENTERO (antes se comia el final)");
            Check(Tinta("LO MAS SONADO", fr, fCab, libre - 30) >= libre, "y aunque la caja se quede corta prefiere asomar a perder una letra");
        }
        cab.Dispose(); past.Dispose();

        Console.WriteLine("\n  2) EL NOMBRE DE LA SECCION NUNCA PIERDE UNA LETRA\n");
        // Cuando el nombre no cabe, el encabezado lo ENCOGE de medio punto en medio punto hasta que entra.
        // Eso es correcto. Lo que no puede pasar es que se coma una letra.
        //
        // Para distinguir una cosa de la otra se calcula antes la escalera de cuerpos por la que puede bajar y
        // se mide cuanto ocupa el nombre ENTERO en cada escalon. Luego, lo que se pinte de verdad tiene que
        // coincidir con ALGUNO de esos escalones: si no coincide con ninguno, es que le falta algo.
        // (El alto de la tinta no sirve para esto: medio punto de diferencia se pierde al redondear a pixeles.)
        string[] secs = { "LO MAS SONADO", "LETRA B", "LETRA W", "VIDEOS" };
        int rarezas = 0;
        foreach (string s in secs)
        {
            List<int> escalones = new List<int>();
            for (float cuerpo = 34f * 0.446f; cuerpo > 6f; cuerpo -= 0.5f)
                using (Font fx = new Font(FontFamily.GenericSansSerif, cuerpo, FontStyle.Bold))
                    escalones.Add(Tinta(s, fx, fCab, 4000));
            // Se mira con UNA sola pagina, que es cuando detras del nombre no hay nada: asi lo que se mide es
            // el nombre PELADO. Con mas paginas la rayita separadora va pegada a la ultima letra y se contaria
            // dentro. El codigo que dibuja el nombre es el mismo en los dos casos, asi que vale igual.
            for (int w = 200; w <= 1400; w += 4)
            {
                int an, al;
                using (Bitmap b = PintaCab(s, 0, 1, w, 34)) MideNombre(b, out an, out al);
                if (an <= 0) continue;
                bool cuadra = false;
                foreach (int e in escalones) if (Math.Abs(e - an) <= 2) { cuadra = true; break; }
                if (!cuadra)
                {
                    rarezas++;
                    if (rarezas <= 4)
                        Console.WriteLine("     \"" + s + "\" caja=" + w + ": se pintaron " + an
                                          + " px, que no son los de NINGUN cuerpo entero (el mayor es " + escalones[0] + ")");
                }
            }
        }
        Console.WriteLine("     " + secs.Length + " nombres x 301 anchos de caja, contra "
                          + (int)((34f * 0.446f - 6f) / 0.5f) + " cuerpos posibles cada uno");
        Check(rarezas == 0, "el nombre sale entero a cualquier ancho (si no cabe se encoge, pero no se corta)");

        // Y con paginas detras, que nada se salga por la derecha (asomar un pixel vale; pasarse, no).
        int desbordes = 0;
        foreach (string s in secs)
            foreach (int tot in new int[] { 2, 3, 9, 12, 13, 24, 40 })
                for (int w = 200; w <= 1400; w += 12)
                    using (Bitmap b = PintaCab(s, 1, tot, w, 34))
                        if (Derecha(Columnas(b)) >= w - 1) desbordes++;
        Check(desbordes == 0, "con las paginas detras, nada se sale por el borde derecho");

        Console.WriteLine("\n  3) EL NUMERO DE PAGINAS SIEMPRE SE DICE\n");
        // El sintoma que se reporto: "LETRA B" sin numero de paginas y con 150 px de hueco al lado. Si el
        // encabezado se calla teniendo sitio, deja un hueco grande a la derecha.
        int callados = 0, peorHueco = 0, peorTot = 0, peorW = 0;
        foreach (int w in new int[] { 400, 522, 700, 858, 1086, 1400, 2028 })
            for (int tot = 2; tot <= 40; tot++)
            {
                int hueco;
                using (Bitmap b = PintaCab("LETRA B", 2, tot, w, 34))
                { bool[] col = Columnas(b); hueco = w - 1 - Derecha(col); }
                if (hueco > 45)
                {
                    callados++;
                    if (hueco > peorHueco) { peorHueco = hueco; peorTot = tot; peorW = w; }
                }
            }
        if (callados > 0)
            Console.WriteLine("     el peor: caja de " + peorW + " px con " + peorTot + " paginas deja " + peorHueco + " px sin usar");
        Check(callados == 0, "con 2 a 40 paginas y a siete anchos de pantalla, nunca se calla teniendo sitio");

        // Y el salto concreto de doce a trece, que es el que fallaba.
        int d12, d13;
        using (Bitmap b = PintaCab("LETRA B", 2, 12, 522, 34)) d12 = 521 - Derecha(Columnas(b));
        using (Bitmap b = PintaCab("LETRA B", 2, 13, 522, 34)) d13 = 521 - Derecha(Columnas(b));
        Console.WriteLine("     con 12 paginas quedan " + d12 + " px libres; con 13 (ya sin puntitos), " + d13);
        Check(Math.Abs(d13 - d12) <= 20, "pasar de doce paginas -donde ya no caben puntitos- no deja el encabezado mudo");

        Console.WriteLine("\n  4) SI NO CABE LA FRASE LARGA, SE DICE MAS CORTO ANTES QUE CALLARSE\n");
        // Se estrecha la caja poco a poco: mientras quepa algo, tiene que seguir diciendo la pagina.
        int hastaDonde = 0;
        for (int w = 1000; w >= 150; w -= 5)
        {
            int hueco;
            using (Bitmap b = PintaCab("LO MAS SONADO", 4, 18, w, 34))
            { bool[] col = Columnas(b); hueco = w - 1 - Derecha(col); }
            if (hueco > 45) { hastaDonde = w; break; }
        }
        Console.WriteLine("     con \"LO MAS SONADO\" y 18 paginas aguanta diciendo la pagina hasta una caja de "
                          + (hastaDonde == 0 ? "150" : (hastaDonde + 5).ToString()) + " px");
        Check(hastaDonde <= 260, "aguanta hasta cajas muy estrechas antes de quedarse sin decir la pagina");

        Console.WriteLine("\n  5) EL PUNTO DE LA PAGINA EN LA QUE ESTAS NO SE SALE POR EL BORDE\n");
        // El punto encendido lleva un halo alrededor, mas ancho que el punto. Estando en la ULTIMA pagina, ese
        // halo es lo ultimo que se pinta: si solo se reservara el punto, se veria cortado contra el borde.
        int pegados = 0;
        for (int tot = 2; tot <= 12; tot++)
            foreach (int w in new int[] { 300, 340, 380, 420, 460, 522 })
            {
                using (Bitmap b = PintaCab("LETRA B", tot - 1, tot, w, 34))
                {
                    bool[] col = Columnas(b);
                    if (Derecha(col) >= w - 1) pegados++;
                }
            }
        Check(pegados == 0, "estando en la ultima pagina, el halo del punto encendido no toca el borde");

        Console.WriteLine("\n  6) LA PASTILLA DE CREDITOS, A CUALQUIER TAMANO\n");
        // "Inserta una moneda" es el texto mas largo que le toca, y el que se reporto comido a resoluciones
        // altas. Se le da el ancho que ella misma pide y se comprueba que el texto cabe con aire a los lados.
        MethodInfo mAncho = TPast.GetMethod("Ancho", BindingFlags.Static | BindingFlags.Public);
        int apretadas = 0;
        foreach (int alto in new int[] { 26, 34, 40, 48, 60, 76 })
        {
            int anc = (int)mAncho.Invoke(null, new object[] { "Inserta una moneda", "", alto, FontFamily.GenericSansSerif });
            Control p2 = (Control)Activator.CreateInstance(TPast, true);
            p2.Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Regular);
            p2.SetBounds(0, 0, anc, alto);
            TPast.GetField("Etiqueta").SetValue(p2, "Inserta una moneda");
            TPast.GetField("Valor").SetValue(p2, "");
            TPast.GetField("Acento").SetValue(p2, Color.FromArgb(240, 200, 120));
            int izq = -1, der = -1;
            using (Bitmap b = new Bitmap(anc, alto, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(b))
                { g.Clear(Color.Transparent); g.TextRenderingHint = TextRenderingHint.AntiAlias; TPast.GetMethod("RenderContent").Invoke(p2, new object[] { g }); }
                // se mira solo la franja central, donde va el texto (arriba y abajo esta el borde de la capsula)
                for (int x = 0; x < anc; x++)
                    for (int y = alto / 2 - 2; y <= alto / 2 + 2; y++)
                        if (b.GetPixel(x, y).A > 120) { if (izq < 0) izq = x; der = x; break; }
            }
            p2.Dispose();
            int aireDer = anc - 1 - der;
            Console.WriteLine("     alto " + alto + ": pide " + anc + " px, el texto va de " + izq + " a " + der
                              + " (aire " + izq + " / " + aireDer + ")");
            if (izq < 3 || aireDer < 3) apretadas++;
        }
        Check(apretadas == 0, "\"Inserta una moneda\" cabe con aire a los dos lados a cualquier tamano de pantalla");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
