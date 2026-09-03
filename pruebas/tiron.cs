using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// EL TIRON AL PONER UNA CANCION.
//
// "Al poner una cancion desde el buscador, da un tiron la rocola". Se ve, y con musica sonando se oye:
// el hilo de la pantalla es el mismo que lleva la cuenta del sonido.
//
// Poner una cancion (AddItem) hace de golpe, en ese hilo, cuatro cosas que cuestan:
//
//   1) GUARDAR EL ESTADO EN DISCO. Y no es una escritura cualquiera: el archivo se escribe aparte, se
//      BAJA AL DISCO DE VERDAD (fsync) y se cambia de un tiron. Eso es lo que hace que un corte de luz
//      no deje la cola a medias... y tambien lo que puede tardar cientos de milisegundos en un disco
//      mecanico o en una memoria USB, que es lo que suele haber en un bar.
//   2) REHACER LA COLA y avisar a las tres pantallas que llevan marcas.
//   3) REHORNEAR LAS TARJETAS DEL BUSCADOR, porque las marcas van dentro de su mapa de bits.
//   4) Con tarjeta grafica, ademas, rehacer la textura de pantalla entera.
//
// ESTA BATERIA LOS MIDE POR SEPARADO. No para tener numeros bonitos: para saber CUAL es el tiron, que
// es la unica forma de arreglar el que es y no el que uno se imagina.
//
// Y deja un tope escrito: lo que se hace en el hilo de la pantalla al pulsar un boton tiene que caber
// holgadamente en un cuadro. A 24 cuadros por segundo un cuadro son 41 ms.
class Tiron
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TSO, TItem, TAlb, TTrack;

    static MethodBase Met(Type t, string n)
    {
        if (t == null) return null;
        foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }
    static string Fam()
    {
        foreach (FontFamily f in FontFamily.Families)
            if (f.Name == "DejaVu Sans" || f.Name == "Liberation Sans" || f.Name == "Arial") return f.Name;
        return FontFamily.GenericSansSerif.Name;
    }

    // La mediana de varias pasadas: una maquina compartida da una lenta de vez en cuando y la media se la
    // traga entera. La mediana no.
    static double Mediana(List<double> v)
    {
        v.Sort();
        return v[v.Count / 2];
    }

    static IList resultados;
    static int pedida = -1;
    // Cada tarjeta que SE DIBUJA pregunta una vez de que genero es. Contando eso se cuentan las tarjetas
    // dibujadas, que es lo que esta bateria quiere saber sin depender de lo rapida que sea la maquina.
    static int cuentaGen = 0;
    static string Genero<T>(T it) { cuentaGen++; return "Rock"; }
    static int Pos<T>(T it)
    {
        for (int i = 0; i < resultados.Count; i++) if (ReferenceEquals(resultados[i], it)) return (i == pedida) ? 1 : 0;
        return 0;
    }
    static bool Suena<T>(T it) { return false; }

    static Control MontaBuscador(Assembly asm, int w, int h)
    {
        Control so = (Control)Activator.CreateInstance(TSO, true);
        so.SetBounds(0, 0, w, h);
        so.Font = new Font(Fam(), 12f, FontStyle.Bold);
        object alb = Activator.CreateInstance(TAlb, true);
        TAlb.GetField("Folder").SetValue(alb, @"C:\Musica\Ultra");
        TAlb.GetField("Name").SetValue(alb, "Ultra (Deluxe)");
        TAlb.GetField("Artist").SetValue(alb, "Depeche Mode");
        Type tLista = typeof(List<>).MakeGenericType(TItem);
        IList res = (IList)Activator.CreateInstance(tLista);
        for (int i = 0; i < 50; i++)
        {
            object it = Activator.CreateInstance(TItem, true);
            object tk = Activator.CreateInstance(TTrack, true);
            TTrack.GetField("Path").SetValue(tk, @"C:\Musica\Ultra\" + i + ".mp3");
            TItem.GetField("Kind").SetValue(it, 2);
            TItem.GetField("Display").SetValue(it, "Depeche Mode - Cancion numero " + i);
            TItem.GetField("Key").SetValue(it, "depeche mode - cancion numero " + i);
            TItem.GetField("Score").SetValue(it, 5000 - i);
            TItem.GetField("AlbumRef").SetValue(it, alb);
            TItem.GetField("TrackRef").SetValue(it, tk);
            res.Add(it);
        }
        resultados = res;
        TSO.GetField("Results").SetValue(so, res);
        TSO.GetField("Query").SetValue(so, "Depeche");
        FieldInfo fEs = TSO.GetField("Escrito");
        if (fEs != null) fEs.SetValue(so, "Depeche");
        TSO.GetField("Accent").SetValue(so, Color.FromArgb(178, 152, 255));
        TSO.GetField("FocusResults").SetValue(so, true);
        TSO.GetField("SelResult").SetValue(so, 0);
        FieldInfo fq = TSO.GetField("GetQueuePos"), fs = TSO.GetField("GetSonando");
        if (fq != null)
            fq.SetValue(so, Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TItem, typeof(int)),
                typeof(Tiron).GetMethod("Pos", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TItem)));
        if (fs != null)
            fs.SetValue(so, Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TItem, typeof(bool)),
                typeof(Tiron).GetMethod("Suena", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TItem)));
        FieldInfo fg = TSO.GetField("GetGenero");
        if (fg != null)
            fg.SetValue(so, Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TItem, typeof(string)),
                typeof(Tiron).GetMethod("Genero", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TItem)));
        return so;
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
            if (t.Name == "Track") TTrack = t;
        }
        if (TSO == null) { Console.WriteLine("  FALLO  no se encontro el buscador"); return 1; }

        Console.WriteLine("\n  1) GUARDAR EL ESTADO: LA ESCRITURA QUE BAJA AL DISCO\n");
        // Es la MISMA funcion que usa la rocola para guardar la cola, con el mismo tamaño de texto: un
        // archivo diminuto. Lo que se mide no es escribirlo, es bajarlo al disco de verdad y cambiarlo de
        // un tiron, que es lo que cuesta.
        MethodBase esc = Met(TF, "EscribeSeguro");
        Check(esc != null, "la rocola guarda por un solo sitio (EscribeSeguro)");
        double msGuardar = -1;
        if (esc != null)
        {
            string dir = Path.Combine(Path.GetTempPath(), "rocola_tiron_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string ruta = Path.Combine(dir, "estado.txt");
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("ROCOLA-ESTADO v1\r\nts=").Append(DateTime.Now.Ticks).Append("\r\ncreditos=9\r\n");
            for (int i = 0; i < 6; i++) sb.Append("cola=S|C:\\Musica\\Ultra\\").Append(i).Append(".mp3\r\n");
            string texto = sb.ToString();
            esc.Invoke(null, new object[] { ruta, texto });   // la primera no cuenta: crea el archivo
            List<double> v = new List<double>();
            for (int k = 0; k < 9; k++)
            {
                Stopwatch w = Stopwatch.StartNew();
                esc.Invoke(null, new object[] { ruta, texto });
                w.Stop();
                v.Add(w.Elapsed.TotalMilliseconds);
            }
            msGuardar = Mediana(v);
            v.Sort();
            Console.WriteLine("     guardar el estado: " + msGuardar.ToString("0.0") + " ms (de "
                              + v[0].ToString("0.0") + " a " + v[v.Count - 1].ToString("0.0") + " en 9 pasadas)");
            Console.WriteLine("     el archivo son " + texto.Length + " bytes: lo que cuesta NO es escribirlo");
            try { Directory.Delete(dir, true); } catch { }
        }

        Console.WriteLine("\n  2) REHORNEAR LAS TARJETAS DEL BUSCADOR\n");
        // Las marcas van dentro del mapa de bits de las tarjetas, asi que al cambiar la cola hay que
        // rehacerlo. Aqui se mide lo que cuesta ese rehorneado con la pantalla llena de resultados.
        const int W = 1920, H = 1080;
        Control so = MontaBuscador(asm, W, H);
        MethodInfo mR = TSO.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) });
        FieldInfo fCola = TSO.GetField("ColaVer");
        double msTarjetas = -1;
        using (Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(b))
        {
            mR.Invoke(so, new object[] { g, new Rectangle(0, 0, W, H), false });   // la primera arma las fuentes
            // (a) EL REHORNEADO ENTERO: se cambia la consulta, que es lo que de verdad obliga a rehacerlo todo.
            FieldInfo fQ = TSO.GetField("Query");
            List<double> v = new List<double>();
            for (int k = 0; k < 9; k++)
            {
                fQ.SetValue(so, "Depeche" + k);
                Stopwatch w = Stopwatch.StartNew();
                mR.Invoke(so, new object[] { g, new Rectangle(0, 0, W, H), false });
                w.Stop();
                v.Add(w.Elapsed.TotalMilliseconds);
            }
            double msEntero = Mediana(v);
            Console.WriteLine("     rehornear el buscador ENTERO: " + msEntero.ToString("0.0") + " ms con "
                              + resultados.Count + " resultados en pantalla");

            // (b) Y LO QUE CUESTA PEDIR UNA CANCION: cambia la cola, y con ella la marca de UNA tarjeta.
            fQ.SetValue(so, "Depeche");
            mR.Invoke(so, new object[] { g, new Rectangle(0, 0, W, H), false });
            v = new List<double>();
            for (int k = 0; k < 9; k++)
            {
                pedida = 3 + k;                                   // otra cancion pedida: cambia UNA marca
                if (fCola != null) fCola.SetValue(so, k + 100);
                Stopwatch w = Stopwatch.StartNew();
                mR.Invoke(so, new object[] { g, new Rectangle(0, 0, W, H), false });
                w.Stop();
                v.Add(w.Elapsed.TotalMilliseconds);
            }
            msTarjetas = Mediana(v);
            Console.WriteLine("     y pedir UNA cancion:          " + msTarjetas.ToString("0.0") + " ms  ("
                              + (msEntero - msTarjetas).ToString("0.0") + " ms menos)");
            Console.WriteLine("     (los " + msTarjetas.ToString("0.0") + " que quedan son dibujar la pantalla del");
            Console.WriteLine("      buscador entera -teclado, cubo, cabecera-, que se hace igual)");
            // El retoque no puede quitar TODO el coste: dibujar la pantalla del buscador cuesta lo que cuesta
            // aunque no cambie ni una tarjeta. Lo que si tiene que quitar es la parte de rehornear las
            // cincuenta, que es la que se ahorra.
            // Y AHORA LO QUE DE VERDAD IMPORTA, que no depende de lo rapida que sea esta maquina: CUANTAS
            // TARJETAS SE DIBUJAN. Cada tarjeta que se dibuja pregunta una vez de que genero es, asi que
            // contando esas preguntas se cuentan exactamente las tarjetas dibujadas.
            //
            // Los milisegundos de arriba dicen cuanto se ahorra aqui; este numero dice que se ahorra SIEMPRE,
            // en cualquier maquina, y es el que no se puede falsear.
            fQ.SetValue(so, "otra cosa");
            cuentaGen = 0;
            mR.Invoke(so, new object[] { g, new Rectangle(0, 0, W, H), false });
            int genEntero = cuentaGen;
            pedida = 40;
            if (fCola != null) fCola.SetValue(so, 900);
            cuentaGen = 0;
            mR.Invoke(so, new object[] { g, new Rectangle(0, 0, W, H), false });
            int genUna = cuentaGen;
            Console.WriteLine("     tarjetas dibujadas:  rehorneando entero " + genEntero + ",  al pedir una cancion " + genUna);
            Check(genEntero > 20, "rehornear entero dibuja las cincuenta, como debe");
            Check(genUna > 0 && genUna * 4 < genEntero, "y pedir una cancion dibuja UNA (y sus dos vecinas), no las cincuenta");
            // Y que el ahorro venga de donde se dice: si alguien quita el retoque, esto lo canta.
            bool retoca = false;
            foreach (MethodInfo m in TSO.GetMethods(TODO | BindingFlags.DeclaredOnly))
            {
                int l3, e3;
                Codigo.UsosDeCampo(m, "resCacheMarca", out l3, out e3);
                if (l3 > 0) retoca = true;
            }
            Check(retoca, "y es porque solo retoca las tarjetas que cambiaron, no por casualidad");
        }

        Console.WriteLine("\n  3) QUIEN SE LLEVA EL TIRON\n");
        double total = (msGuardar > 0 ? msGuardar : 0) + (msTarjetas > 0 ? msTarjetas : 0);
        Console.WriteLine("     de los dos, guardar en disco es el " + ((msGuardar / Math.Max(total, 0.001)) * 100).ToString("0") + "%");
        Console.WriteLine();
        Console.WriteLine("     (en la maquina del bar hay que multiplicar por 4 o 5, y su disco es mas lento)");
        // EL TOPE. Lo que se hace en el hilo de la pantalla al pulsar un boton tiene que caber en un cuadro:
        // a 24 cuadros por segundo, 41 ms. Si no cabe, se ve como un tiron y se oye como un chasquido.
        Check(msTarjetas >= 0 && msTarjetas < 41, "rehornear el buscador cabe en un cuadro");
        Check(msGuardar >= 0, "la medida de guardar salio");

        Console.WriteLine("\n  4) Y GUARDAR NO ERA EL PROBLEMA\n");
        // Se sospechaba de la escritura -baja al disco de verdad para sobrevivir a un corte de luz- y la
        // medida dijo que no: son decimas de milisegundo con un archivo de 233 bytes. Se deja medido para
        // que el dia que alguien la toque se vea aqui, y no se vuelva a sospechar de ella sin datos.
        Check(msGuardar >= 0 && msGuardar < 15, "guardar el estado sigue costando poco (" + msGuardar.ToString("0.0") + " ms)");
        MethodBase guarda = Met(TF, "SaveState");
        Check(guarda != null, "sigue habiendo un solo sitio que guarda el estado (SaveState)");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
