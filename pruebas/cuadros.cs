using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// CUANTO LE CUESTA A LA MAQUINA EL FONDO, CON PROCESADOR Y CON TARJETA GRAFICA.
//
//     mono cuadros.exe [ejecutable] [ancho] [alto] [segundos]
//
// La rocola sabe pintar su fondo estrellado de dos maneras: con el procesador (dibujando en un bitmap, que
// es como se hace en la rocola de Windows XP) o con la tarjeta grafica. Cual conviene hay que MEDIRLO en la
// maquina donde va a estar la rocola: en un equipo modesto con grafica integrada puede no compensar, y
// entonces lo bueno es quedarse con el procesador, que ya funciona.
//
// LO QUE SE MIDE, Y POR QUE ASI. Preguntar "cuantos cuadros por segundo aguanta cada uno" enganaria, porque
// los dos no hacen lo mismo: el de procesador HORNEA el cielo cada 40 ms y el resto de los cuadros solo
// copia lo horneado, mientras que el de tarjeta rehace el cielo entero SIEMPRE (que es lo barato para una
// tarjeta). Suelto a toda velocidad, el de procesador sale ganando por copiar; a 60 cuadros por segundo,
// que es lo que de verdad va a hacer en el bar, hornea muchas mas veces por cuadro.
//
// Asi que se mide lo que importa en un equipo modesto: PIDIENDOLES LOS MISMOS 60 CUADROS POR SEGUNDO, cuanto
// tiempo de maquina se lleva cada uno. Ese tiempo es el que la rocola NO tiene para el sonido.
class Cuadros
{
    const int FPS = 60;

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola_gpu.exe";
        int W = (argv.Length > 1) ? int.Parse(argv[1]) : 1024;
        int H = (argv.Length > 2) ? int.Parse(argv[2]) : 768;
        double seg = (argv.Length > 3) ? double.Parse(argv[3], System.Globalization.CultureInfo.InvariantCulture) : 4.0;

        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        Type TS = TF.GetNestedType("Starfield", BindingFlags.NonPublic | BindingFlags.Public);
        Type TSF = asm.GetType("Rocola.GpuStarfield", false);

        Console.WriteLine();
        Console.WriteLine("=== LO QUE CUESTA EL FONDO  (" + W + "x" + H + ", pidiendo " + FPS + " cuadros por segundo) ===");
        Console.WriteLine();

        double msCpu = -1, msGpu = -1;
        string quien = "";

        // ---------------- con el procesador ----------------
        if (TS == null) Console.WriteLine("  con el procesador : no se encontro el campo de estrellas");
        else
        {
            object sf = Activator.CreateInstance(TS, true);
            FieldInfo fDens = TS.GetField("Density", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fDens != null) fDens.SetValue(sf, 1f);
            MethodInfo mDraw = TS.GetMethod("Draw", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            using (Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                object[] a = new object[] { g, W, H, Color.FromArgb(6, 8, 26), Color.FromArgb(26, 44, 90),
                                            Color.FromArgb(120, 160, 255), 0.0, false, 0f };
                mDraw.Invoke(sf, a);
                msCpu = ACompas(seg, delegate(int n) { a[6] = (double)n * 0.01; mDraw.Invoke(sf, a); });
            }
            Escribe("con el procesador", msCpu);
        }

        // ---------------- con la tarjeta grafica ----------------
        Form f = null;
        if (TSF == null) Console.WriteLine("  con la tarjeta    : este ejecutable no trae el fondo por tarjeta (falta -d:ROCOLA_GPU)");
        else
        {
            f = new Form();
            f.FormBorderStyle = FormBorderStyle.None; f.ClientSize = new Size(W, H);
            f.Show(); Application.DoEvents();
            object campo = Activator.CreateInstance(TSF, new object[] { f.Handle, W, H, 1234 });
            if (!(bool)TSF.GetProperty("Ok").GetValue(campo, null))
                Console.WriteLine("  con la tarjeta    : no arranco (no hay OpenGL en esta maquina)");
            else
            {
                object dev = TSF.GetField("gpu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(campo);
                PropertyInfo pQ = dev.GetType().GetProperty("Quien");
                if (pQ != null) quien = (string)pQ.GetValue(dev, null);
                MethodInfo mRender = TSF.GetMethod("Render");
                object[] ar = new object[] { W, H, 0f };
                mRender.Invoke(campo, ar);
                msGpu = ACompas(seg, delegate(int n) { mRender.Invoke(campo, ar); });
                Escribe("con la tarjeta   ", msGpu);
                TSF.GetMethod("Dispose").Invoke(campo, null);
            }
            f.Close();
        }

        Console.WriteLine();
        if (quien.Length > 0) Console.WriteLine("  quien dibuja: " + quien);
        bool porSoftware = quien.IndexOf("llvmpipe", StringComparison.OrdinalIgnoreCase) >= 0
                        || quien.IndexOf("softpipe", StringComparison.OrdinalIgnoreCase) >= 0
                        || quien.IndexOf("swrast", StringComparison.OrdinalIgnoreCase) >= 0;
        if (msCpu > 0 && msGpu > 0)
        {
            Console.WriteLine();
            if (porSoftware)
            {
                Console.WriteLine("  OJO: EN ESTA MAQUINA NO HAY TARJETA GRAFICA. Lo de arriba lo dibujo el procesador");
                Console.WriteLine("  haciendose pasar por tarjeta (eso es \"" + (quien.IndexOf("llvmpipe", StringComparison.OrdinalIgnoreCase) >= 0 ? "llvmpipe" : "software") + "\"), asi que ese numero NO contesta");
                Console.WriteLine("  si la tarjeta gana: solo dice lo que cuesta PREPARAR los cuadros. Para saber si gana");
                Console.WriteLine("  hay que correr esto EN LA MAQUINA DE LA ROCOLA.");
            }
            else
            {
                Console.WriteLine("  la tarjeta le quita a la maquina " + (msGpu / msCpu).ToString("0.00") + " veces lo que le quita el procesador");
                if (msGpu < msCpu * 0.6) Console.WriteLine("  -> la tarjeta gana con holgura: vale la pena encender el fondo por tarjeta.");
                else if (msGpu < msCpu) Console.WriteLine("  -> la tarjeta gana, pero poco. Decide si compensa.");
                else Console.WriteLine("  -> la tarjeta NO gana en esta maquina: mejor dejar el fondo por procesador (gpu=0).");
            }
        }
        Console.WriteLine();
        return 0;
    }

    // Pide 60 cuadros por segundo durante 'seg' segundos y devuelve los MILISEGUNDOS DE TRABAJO que se
    // gastaron por cada segundo de rocola. Entre cuadro y cuadro se espera, para que el de procesador
    // hornee las veces que horneara de verdad y no se le amortice el horneado de mas.
    static double ACompas(double seg, Action<int> cuadro)
    {
        Stopwatch total = Stopwatch.StartNew();
        Stopwatch trabajo = new Stopwatch();
        int n = 0;
        double paso = 1000.0 / FPS;
        double proximo = 0;
        while (total.Elapsed.TotalSeconds < seg)
        {
            double ahora = total.Elapsed.TotalMilliseconds;
            if (ahora < proximo) { Thread.Sleep(1); continue; }
            proximo += paso;
            if (proximo < ahora) proximo = ahora + paso;
            trabajo.Start(); cuadro(n); trabajo.Stop();
            n++;
        }
        total.Stop();
        if (n == 0) return -1;
        return trabajo.Elapsed.TotalMilliseconds / total.Elapsed.TotalSeconds;
    }

    static void Escribe(string quien, double msPorSeg)
    {
        if (msPorSeg < 0) { Console.WriteLine("  " + quien + " : no se pudo medir"); return; }
        Console.WriteLine("  " + quien + " : " + msPorSeg.ToString("0.0") + " ms de trabajo por segundo   ("
                          + (msPorSeg / 10.0).ToString("0.0") + "% de un nucleo)");
    }
}
