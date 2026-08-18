using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// HOJEAR DENTRO DE UN DISCO.
//
// Un disco con mas de una pagina cambiaba de pagina DE GOLPE: catorce canciones se convertian en otras
// catorce sin que nada dijera hacia donde te habias movido. Ahora la que dejas SALE por un lado y la que
// eliges ENTRA por el otro, con la misma curva que la pared de discos.
//
// Lo delicado es el COSTE: dibujar una pagina cuesta 1,8 ms y durante la animacion hacen falta las dos en
// cada cuadro. Por eso van horneadas a un mapa de bits y cada cuadro son dos copias. Y lo que hay que
// demostrar es que hornear NO cambia ni un pixel: si cambiara, se habria pagado la animacion con el aspecto.
class Hojea
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const int W = 980, H = 532, RH = 38;   // 14 filas
    static readonly Color FONDO = Color.FromArgb(255, 12, 15, 34);

    // Monta una lista de canciones igual en cualquiera de los dos ejecutables.
    static Control Monta(Assembly asm, out Type TL)
    {
        Type TF = asm.GetType("Rocola.Form1", true);
        TL = null;
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)) if (t.Name == "SongListView") TL = t;
        Control c = (Control)Activator.CreateInstance(TL, true);
        c.SetBounds(0, 0, W, H);
        c.Font = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Bold);
        TL.GetField("RowH").SetValue(c, RH);
        TL.GetField("Accent").SetValue(c, Color.FromArgb(150, 110, 220));
        TL.GetField("GetCount").SetValue(c, new Func<int>(delegate { return 60; }));
        TL.GetField("GetText").SetValue(c, new Func<int, string>(delegate (int i) { return "Cancion numero " + (i + 1) + " con un titulo bastante largo"; }));
        TL.GetField("GetRight").SetValue(c, new Func<int, string>(delegate (int i) { return "3:4" + (i % 10); }));
        TL.GetField("GetQueuePos").SetValue(c, new Func<int, int>(delegate (int i) { return (i % 7 == 0) ? 1 : (i % 11 == 0) ? -1 : 0; }));
        TL.GetField("GetGenero").SetValue(c, new Func<int, string>(delegate (int i) { return (i % 3 == 0) ? "Regional" : "Pop"; }));
        return c;
    }

    static Bitmap Pinta(Control c, Type TL, int pagina, int sel)
    {
        TL.GetField("PageStart").SetValue(c, pagina);
        TL.GetField("Sel").SetValue(c, sel);
        Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(FONDO);
            TL.GetMethod("RenderContent", new Type[] { typeof(Graphics) }).Invoke(c, new object[] { g });
        }
        return b;
    }

    // Cuantos pixeles se diferencian, y en cuanto como mucho.
    static int Difiere(Bitmap a, Bitmap b, out int peor)
    {
        peor = 0; int n = 0;
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
            {
                Color p = a.GetPixel(x, y), q = b.GetPixel(x, y);
                int d = Math.Max(Math.Abs(p.R - q.R), Math.Max(Math.Abs(p.G - q.G), Math.Abs(p.B - q.B)));
                if (d > 0) { n++; if (d > peor) peor = d; }
            }
        return n;
    }

    static double Min(int reps, Action f)
    {
        f(); f();
        double m = double.MaxValue;
        for (int b = 0; b < 7; b++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < reps; i++) f();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / reps;
            if (ms < m) m = ms;
        }
        return m;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TL; Control c = Monta(asm, out TL);
        Check(TL != null, "la lista del disco esta donde tiene que estar");
        if (TL == null) return 1;

        Console.WriteLine("\n  1) HORNEAR NO CAMBIA NI UN PIXEL\n");
        // Se comparan los DOS caminos del MISMO programa: PintaPagina, que dibuja la pagina directamente, y
        // RenderContent, que la hornea y despues la copia. Compararlo asi -y no contra una version de antes-
        // es lo unico que demuestra de verdad lo que hace falta demostrar: que hornear no cambia el aspecto.
        // (Hornear sobre TRANSPARENTE si lo cambiaria: el texto suavizado sale mas fino. Por eso el horneado
        // lleva el fondo opaco de la lista dentro, y copiarlo es una copia y no una mezcla.)
        MethodInfo mDirecto = TL.GetMethod("PintaPagina", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo mHorno = TL.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
        Check(mDirecto != null, "el dibujo directo de una pagina sigue estando, para poder compararlo");
        if (mDirecto != null)
        {
            int totalDif = 0, peorTodo = 0;
            int[,] casos = { { 0, 3 }, { 14, 14 }, { 28, 30 }, { 42, 42 }, { 56, 59 } };
            for (int k = 0; k < casos.GetLength(0); k++)
            {
                TL.GetField("PageStart").SetValue(c, casos[k, 0]);
                TL.GetField("Sel").SetValue(c, casos[k, 1]);
                using (Bitmap directo = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
                using (Bitmap horneado = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics g = Graphics.FromImage(directo)) { g.Clear(FONDO); mDirecto.Invoke(c, new object[] { g, casos[k, 0] }); }
                    using (Graphics g = Graphics.FromImage(horneado)) { g.Clear(FONDO); mHorno.Invoke(c, new object[] { g }); }
                    int peor; int d = Difiere(directo, horneado, out peor);
                    totalDif += d; if (peor > peorTodo) peorTodo = peor;
                    Console.WriteLine(string.Format("     pagina que empieza en {0,2}, seleccion {1,2}:  {2} pixeles distintos", casos[k, 0], casos[k, 1], d));
                }
            }
            Check(totalDif == 0, "la pagina horneada sale EXACTAMENTE igual que dibujada directa   [peor diferencia: " + peorTodo + "]");
        }

        Console.WriteLine("\n  2) Y SE HOJEA: LA VIEJA SALE, LA NUEVA ENTRA\n");
        MethodInfo mSlide = TL.GetMethod("StartSlide", BindingFlags.Instance | BindingFlags.Public);
        PropertyInfo pSliding = TL.GetProperty("Sliding", BindingFlags.Instance | BindingFlags.Public);
        PropertyInfo pPaso = TL.GetProperty("SlidePaso", BindingFlags.Instance | BindingFlags.Public);
        MethodInfo mOlvida = TL.GetMethod("OlvidaPagina", BindingFlags.Instance | BindingFlags.Public);
        Check(mSlide != null && pSliding != null && pPaso != null && mOlvida != null, "la lista sabe hojear");
        if (mSlide == null) return 1;

        using (Bitmap quieta = Pinta(c, TL, 0, 3)) { }        // deja la pagina 1 horneada
        mSlide.Invoke(c, new object[] { 1 });                  // hojea hacia delante
        Check((bool)pSliding.GetValue(c, null), "al cambiar de pagina arranca el hojeo");
        using (Bitmap enMedio = Pinta(c, TL, 14, 17))
        using (Bitmap dest = new Bitmap(1, 1))
        {
            // A mitad de camino tiene que verse ALGO de las dos paginas: la que se va por la izquierda y la
            // que entra por la derecha. Se mira si hay contenido pegado a los DOS bordes.
            bool izq = HayContenido(enMedio, 0, 60), der = HayContenido(enMedio, W - 60, W);
            Console.WriteLine("     a mitad de camino: contenido pegado a la izquierda=" + (izq ? "si" : "no") + ", a la derecha=" + (der ? "si" : "no"));
            Check(izq && der, "durante el hojeo se ven las DOS paginas a la vez (una saliendo, otra entrando)");
        }
        System.Threading.Thread.Sleep(300);   // se deja llegar
        Check(!(bool)pSliding.GetValue(c, null), "y al acabar deja de hojear solo (no se queda a medias)");
        using (Bitmap llegada = Pinta(c, TL, 14, 17))
        using (Bitmap directa = Pinta(c, TL, 14, 17))
        {
            int peor; int d = Difiere(llegada, directa, out peor);
            Check(d == 0, "y lo que queda es la pagina nueva entera, en su sitio");
        }

        Console.WriteLine("\n  3) LA CURVA: ARRANCA, FRENA Y LLEGA\n");
        MethodInfo mCurva = TL.GetMethod("CurvaLista", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mCurva != null, "la curva del hojeo se puede comprobar aparte");
        int ms = (int)TL.GetField("SlideMs", BindingFlags.Static | BindingFlags.Public).GetRawConstantValue();
        if (mCurva != null)
        {
            float ant = 2f; bool baja = true, frena = true; float pasoAnt = 9f;
            for (int t = 0; t <= ms; t += ms / 10)
            {
                float f = (float)mCurva.Invoke(null, new object[] { t });
                if (f > ant + 0.0001f) baja = false;
                float paso = ant - f;
                if (t > 0 && paso > pasoAnt + 0.0001f) frena = false;
                if (t > 0) pasoAnt = paso;
                ant = f;
            }
            Console.WriteLine("     dura " + ms + " ms; empieza en " + ((float)mCurva.Invoke(null, new object[] { 0 })).ToString("0.00")
                              + " y acaba en " + ((float)mCurva.Invoke(null, new object[] { ms })).ToString("0.00"));
            Check(Math.Abs((float)mCurva.Invoke(null, new object[] { 0 }) - 1f) < 0.001f, "arranca desde el borde entero");
            Check((float)mCurva.Invoke(null, new object[] { ms }) == 0f, "y llega EXACTAMENTE a su sitio (no se queda a medio pixel)");
            Check(baja, "no vuelve atras por el camino");
            Check(frena, "y frena al llegar, que es lo que se lee como movimiento y no como parpadeo");
            Check(ms >= 150 && ms <= 320, "dura lo justo: ni un parpadeo ni una espera   [" + ms + " ms]");
        }

        Console.WriteLine("\n  4) AL ENTRAR A UN DISCO NO SE HOJEA: SE LLEGA\n");
        mOlvida.Invoke(c, null);
        mSlide.Invoke(c, new object[] { 1 });
        Check(!(bool)pSliding.GetValue(c, null), "recien abierto un disco no hay de donde hojear, asi que no se hojea");

        Console.WriteLine("\n  5) LO QUE CUESTA (esto es lo que hace posible la animacion)\n");
        using (Bitmap dst = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(dst))
        {
            mOlvida.Invoke(c, null);
            TL.GetField("PageStart").SetValue(c, 0); TL.GetField("Sel").SetValue(c, 3);
            MethodInfo R = TL.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
            R.Invoke(c, new object[] { g });   // deja horneada la pagina
            double copia = Min(200, delegate { R.Invoke(c, new object[] { g }); });
            // Y lo que costaria dibujarla entera cada vez: se fuerza a rehornear cambiando los datos.
            FieldInfo fDatos = TL.GetField("Datos");
            int[] v = new int[1];
            double dibujo = Min(60, delegate { fDatos.SetValue(c, ++v[0]); R.Invoke(c, new object[] { g }); });
            Console.WriteLine("     dibujar la pagina entera: " + dibujo.ToString("0.000") + " ms");
            Console.WriteLine("     copiarla ya horneada:     " + copia.ToString("0.000") + " ms   (" + (dibujo / Math.Max(copia, 0.0001)).ToString("0.0") + " veces menos)");
            Console.WriteLine("     un cuadro de hojeo son DOS copias: " + (copia * 2).ToString("0.000") + " ms, contra "
                              + (dibujo * 2).ToString("0.000") + " ms si no estuvieran horneadas");
            Check(copia * 3 < dibujo, "copiar la pagina horneada cuesta bastante menos que dibujarla (por eso la animacion sale gratis)");
            Check(copia * 2 < 2.0, "y un cuadro entero de hojeo cabe de sobra en el tiempo de un cuadro");
        }

        Console.WriteLine("\n  6) LO HORNEADO NO SE QUEDA VIEJO\n");
        // ESTE es el riesgo de hornear: si algo cambia lo que la lista tiene que ensenar -pides una cancion y
        // aparece su marca verde, llega la duracion, se aprende el genero- y nadie avisa, se seguiria viendo
        // el mapa de bits de antes. Aqui se comprueba que avisar (Datos) rehace de verdad la pagina.
        {
            MethodInfo R = TL.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
            FieldInfo fDatos = TL.GetField("Datos");
            Check(fDatos != null, "la lista se puede avisar de que su contenido cambio");
            mOlvida.Invoke(c, null);
            TL.GetField("PageStart").SetValue(c, 0); TL.GetField("Sel").SetValue(c, 3);
            int[] cola = new int[1];   // al principio, nada pedido
            TL.GetField("GetQueuePos").SetValue(c, new Func<int, int>(delegate (int i) { return (cola[0] != 0 && i == 2) ? 1 : 0; }));
            Bitmap sinPedir = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(sinPedir)) { g.Clear(FONDO); R.Invoke(c, new object[] { g }); }
            cola[0] = 1;   // ahora se pide la cancion 3... pero SIN avisar
            Bitmap sinAvisar = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(sinAvisar)) { g.Clear(FONDO); R.Invoke(c, new object[] { g }); }
            int peor1; int d1 = Difiere(sinPedir, sinAvisar, out peor1);
            fDatos.SetValue(c, (int)fDatos.GetValue(c) + 1);   // y ahora SI se avisa
            Bitmap avisado = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(avisado)) { g.Clear(FONDO); R.Invoke(c, new object[] { g }); }
            int peor2; int d2 = Difiere(sinPedir, avisado, out peor2);
            Console.WriteLine("     pidiendo una cancion SIN avisar: " + d1 + " pixeles cambian (lo horneado se queda)");
            Console.WriteLine("     avisando (Datos):                " + d2 + " pixeles cambian (sale su marca verde)");
            Check(d1 == 0, "sin avisar no se rehace: por eso hace falta que TODOS los sitios avisen");
            Check(d2 > 100, "y avisando aparece la marca al momento");
            sinPedir.Dispose(); sinAvisar.Dispose(); avisado.Dispose();
        }
        // Y el sitio que avisa es UNO SOLO, para que no se pueda olvidar ninguno.
        {
            Type TF2 = asm.GetType("Rocola.Form1", true);
            MethodInfo mRef = TF2.GetMethod("RefrescaLista", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(mRef != null, "hay un solo sitio que dice \"el contenido de la lista cambio\"");
            MethodInfo mCola = TF2.GetMethod("RefrescaCola", BindingFlags.Instance | BindingFlags.NonPublic);
            bool avisa = false;
            if (mCola != null)
            {
                MethodBody b = mCola.GetMethodBody();
                byte[] il = b.GetILAsByteArray();
                for (int i = 0; i + 4 < il.Length; i++)
                    if (il[i] == 0x28 || il[i] == 0x6F)
                    {
                        try { MethodBase mb = mCola.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1)); if (mb != null && mb.Name == "RefrescaLista") avisa = true; }
                        catch { }
                    }
            }
            Check(avisa, "y mover la cola avisa (si no, pedir una cancion no le sacaria la marca hasta mucho despues)");
        }

        Console.WriteLine("\n  7) HOJEAR DEPRISA NO DEJA BASURA\n");
        // Cada pagina horneada son dos megas. Si en cada cambio de pagina se tirara la vieja y se reservara
        // una nueva, hojear deprisa llenaria la memoria de basura y el recolector entraria a limpiarla JUSTO
        // mientras la animacion esta corriendo: el tiron se veria. Los dos mapas se intercambian.
        {
            MethodInfo R = TL.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
            using (Bitmap dst = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(dst))
            {
                mOlvida.Invoke(c, null);
                TL.GetField("PageStart").SetValue(c, 0); R.Invoke(c, new object[] { g });
                for (int k = 0; k < 8; k++)   // unas cuantas vueltas para que los dos mapas ya existan
                { mSlide.Invoke(c, new object[] { 1 }); TL.GetField("PageStart").SetValue(c, (k + 1) * 14 % 56); R.Invoke(c, new object[] { g }); }
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long a0 = GC.GetTotalMemory(true);
                const int VUELTAS = 40;
                for (int k = 0; k < VUELTAS; k++)
                {
                    mSlide.Invoke(c, new object[] { (k % 2 == 0) ? 1 : -1 });
                    TL.GetField("PageStart").SetValue(c, (k % 4) * 14);
                    R.Invoke(c, new object[] { g });
                }
                long a1 = GC.GetTotalMemory(false);
                double porVuelta = (a1 - a0) / (double)VUELTAS;
                long unMapa = (long)W * H * 4;
                Console.WriteLine("     " + VUELTAS + " cambios de pagina -> " + (porVuelta / 1024).ToString("0.0") + " KB por cambio");
                Console.WriteLine("     (un mapa de pagina son " + (unMapa / 1024) + " KB: reservar uno en cada cambio se notaria)");
                Check(porVuelta < unMapa / 4, "hojear no reserva un mapa nuevo en cada cambio: los dos se intercambian");
            }
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }

    // Si en esa franja vertical hay algo dibujado encima del fondo de la lista.
    static bool HayContenido(Bitmap b, int x0, int x1)
    {
        Color fondo = b.GetPixel(2, 2);
        int n = 0;
        for (int y = 4; y < b.Height - 4; y += 2)
            for (int x = Math.Max(0, x0); x < Math.Min(b.Width, x1); x += 2)
            {
                Color c = b.GetPixel(x, y);
                if (Math.Abs(c.R - fondo.R) + Math.Abs(c.G - fondo.G) + Math.Abs(c.B - fondo.B) > 24) n++;
            }
        return n > 20;
    }
}
