using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// QUE CUESTA MOVERSE ENTRE LOS RESULTADOS DEL BUSCADOR.
//
// El cliente pulsa una flecha y el recuadro pasa al resultado de al lado. Eso deberia costar casi nada: las
// tarjetas ya estan dibujadas y guardadas, solo cambia CUAL lleva el recuadro. Aqui se mide de verdad, para
// ver donde se va el tiempo: en las tarjetas, en el teclado, en el cubo de la vista previa, o en rehacer
// toda la pantalla.
class BuscaDib
{
    static Type TF, TSO, TItem, TAlb;
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static double Mide(int veces, Action a)
    {
        a(); a();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        double mejor = double.MaxValue;
        for (int t = 0; t < 7; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < veces; i++) a();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / veces;
            if (ms < mejor) mejor = ms;
        }
        return mejor;
    }
    static void Fila(string q, double ms) { Console.WriteLine(string.Format("     {0,-52} {1,8:0.000} ms   {2,5:0.0}% del cuadro", q, ms, 100.0 * ms / 16.666)); }

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
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
        }
        Check(TSO != null && TItem != null, "el buscador esta donde tiene que estar");
        if (TSO == null || TItem == null) return 1;

        const int W = 1024, H = 768;
        Control so = (Control)Activator.CreateInstance(TSO, true);
        so.SetBounds(0, 0, W, H);
        so.Font = new Font(Fam(), 12f, FontStyle.Bold);

        // 40 resultados, que es lo que se ensena como mucho.
        FieldInfo fKind = TItem.GetField("Kind"), fDisp = TItem.GetField("Display"), fKey = TItem.GetField("Key"), fSc = TItem.GetField("Score");
        Type tLista = typeof(List<>).MakeGenericType(TItem);
        IList res = (IList)Activator.CreateInstance(tLista);
        string[] nom = { "Depeche Mode - Enjoy The Silence", "Jose Jose - El Triste", "Los Angeles Azules - Nunca Es Suficiente",
                         "Metallica - Enter Sandman", "Juan Gabriel - Amor Eterno", "Bronco - Que No Quede Huella" };
        for (int i = 0; i < 40; i++)
        {
            object it = Activator.CreateInstance(TItem, true);
            fKind.SetValue(it, i % 3); fDisp.SetValue(it, nom[i % nom.Length] + " " + i);
            fKey.SetValue(it, (nom[i % nom.Length] + " " + i).ToLowerInvariant()); fSc.SetValue(it, 5000 - i);
            res.Add(it);
        }
        // Cada resultado apunta a un disco con caratula de verdad: sin esto no se dibuja el cubo de la vista
        // previa, que es justo lo que hay que medir (girar una caratula es lo mas caro que hace el buscador).
        FieldInfo fAlbRef = TItem.GetField("AlbumRef");
        object alb = Activator.CreateInstance(TAlb, true);
        TAlb.GetField("Folder").SetValue(alb, @"C:\Musica\Disco");
        TAlb.GetField("Name").SetValue(alb, "Un disco");
        TAlb.GetField("Artist").SetValue(alb, "Un artista");
        TAlb.GetField("CoverPath").SetValue(alb, @"C:\Musica\Disco\cover.jpg");
        for (int i = 0; i < res.Count; i++) fAlbRef.SetValue(res[i], alb);
        portada = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
        using (Graphics pgg = Graphics.FromImage(portada))
        {
            pgg.Clear(Color.FromArgb(255, 30, 40, 90));
            for (int i = 0; i < 40; i++) using (SolidBrush sb2 = new SolidBrush(Color.FromArgb(90, 200, 120 + i, 255 - i))) pgg.FillEllipse(sb2, i * 7, i * 6, 60, 60);
        }
        {
            Type tfc = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
            MethodInfo mic = typeof(BuscaDib).GetMethod("Cov", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb);
            TSO.GetField("GetCover").SetValue(so, Delegate.CreateDelegate(tfc, mic));
        }

        TSO.GetField("Results").SetValue(so, res);
        TSO.GetField("Query").SetValue(so, "de");
        TSO.GetField("Accent").SetValue(so, Color.FromArgb(178, 152, 255));
        TSO.GetField("FocusResults").SetValue(so, true);
        FieldInfo fSel = TSO.GetField("SelResult");
        FieldInfo fSelR = TSO.GetField("SelRow"), fSelC = TSO.GetField("SelCol");
        fSel.SetValue(so, 0);
        // El genero de cada resultado, como en el programa de verdad.
        FieldInfo fGen = TSO.GetField("GetGenero");
        if (fGen != null)
        {
            Type tf2 = typeof(Func<,>).MakeGenericType(TItem, typeof(string));
            MethodInfo mi = typeof(BuscaDib).GetMethod("Gen", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TItem);
            fGen.SetValue(so, Delegate.CreateDelegate(tf2, mi));
        }

        MethodInfo mRender = TSO.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) });
        Check(mRender != null, "se puede pedir al buscador que se dibuje");
        if (mRender == null) return 1;

        Rectangle todo = new Rectangle(0, 0, W, H);
        using (Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            object[] argCpu = new object[] { g, todo, false };
            object[] argGpu = new object[] { g, todo, true };

            Console.WriteLine("\n  EL BUSCADOR ENTERO (" + W + "x" + H + ", 40 resultados)");
            Console.WriteLine("  presupuesto: 16.666 ms por cuadro a 60 por segundo\n");
            Fila("dibujarlo entero, version sin tarjeta grafica", Mide(60, delegate { mRender.Invoke(so, argCpu); }));
            Fila("dibujarlo entero, version con tarjeta grafica", Mide(60, delegate { mRender.Invoke(so, argGpu); }));

            // MOVERSE ENTRE RESULTADOS: solo cambia cual lleva el recuadro.
            Console.WriteLine("\n  PULSANDO UNA FLECHA (cambia el resultado elegido)\n");
            int[] sel = new int[] { 0 };
            double mov = Mide(60, delegate
            {
                sel[0] = (sel[0] + 1) % 40;
                fSel.SetValue(so, sel[0]);
                mRender.Invoke(so, argGpu);
            });
            Fila("cambiar de resultado y redibujar el buscador", mov);

            // Y lo mismo moviendose por el TECLADO.
            int[] rc = new int[] { 0, 0 };
            double movT = Mide(60, delegate
            {
                rc[1] = (rc[1] + 1) % 10; if (rc[1] == 0) rc[0] = (rc[0] + 1) % 4;
                fSelR.SetValue(so, rc[0]); fSelC.SetValue(so, rc[1]);
                mRender.Invoke(so, argGpu);
            });
            Fila("moverse por el teclado y redibujar el buscador", movT);

            // Lo que costaria REHACER LA PANTALLA ENTERA, que es lo que hace el modo con tarjeta grafica cada
            // vez que cambia algo de la firma: limpiar el lienzo grande y volver a componerlo.
            Console.WriteLine("\n  LO QUE CUESTA LA PANTALLA COMPLETA (lo que se rehace al mover el recuadro)\n");
            using (Bitmap big = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
            using (Graphics bg = Graphics.FromImage(big))
            {
                Fila("solo LIMPIAR el lienzo de pantalla completa", Mide(60, delegate { bg.Clear(Color.Transparent); }));
            }
            // Y a 1920x1080, que es la pantalla de la version HD.
            using (Bitmap hd = new Bitmap(1920, 1080, PixelFormat.Format32bppPArgb))
            using (Graphics hg = Graphics.FromImage(hd))
            {
                Fila("solo LIMPIAR el lienzo a 1920x1080", Mide(40, delegate { hg.Clear(Color.Transparent); }));
                Console.WriteLine("     (y ademas hay que SUBIRLO a la tarjeta grafica: " + (1920 * 1080 * 4 / 1024 / 1024) + " MB cada vez)");
            }

            // LO QUE DE VERDAD PASA AL PULSAR UNA FLECHA EN MODO TARJETA GRAFICA:
            //   1) se LIMPIA un lienzo del tamano de la pantalla
            //   2) se vuelve a componer encima todo el buscador
            //   3) se SUBE entero a la tarjeta grafica (copia fila por fila)
            // Se reproducen los tres pasos para saber cuanto cuesta una sola pulsacion.
            Console.WriteLine("\n  UNA PULSACION COMPLETA EN MODO TARJETA GRAFICA\n");
            int[,] pantallas = { { 1024, 748 }, { 1366, 998 }, { 1920, 1000 } };
            for (int pi = 0; pi < pantallas.GetLength(0); pi++)
            {
                int pw = pantallas[pi, 0], ph = pantallas[pi, 1];
                so.SetBounds(0, 0, pw, ph);
                using (Bitmap tex = new Bitmap(pw, ph, PixelFormat.Format32bppPArgb))
                using (Graphics tg = Graphics.FromImage(tex))
                {
                    object[] ag = new object[] { tg, new Rectangle(0, 0, pw, ph), true };
                    tg.SmoothingMode = SmoothingMode.AntiAlias;
                    int[] s2 = new int[] { 0 };
                    double rehacer = Mide(30, delegate
                    {
                        s2[0] = (s2[0] + 1) % 40; fSel.SetValue(so, s2[0]);
                        tg.Clear(Color.Transparent);
                        mRender.Invoke(so, ag);
                    });
                    // Y la subida: exactamente lo que hace el programa (bloquear el bitmap y copiar fila a fila).
                    IntPtr destino = System.Runtime.InteropServices.Marshal.AllocHGlobal(pw * ph * 4);
                    double subir = Mide(30, delegate
                    {
                        BitmapData bd = tex.LockBits(new Rectangle(0, 0, pw, ph), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        for (int y = 0; y < ph; y++)
                            CopiaFila(new IntPtr(destino.ToInt64() + (long)y * pw * 4), new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), pw * 4);
                        tex.UnlockBits(bd);
                    });
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(destino);
                    Console.WriteLine(string.Format("     {0}x{1}  ({2:0.0} MB):  rehacer {3,6:0.00} ms  +  subir {4,6:0.00} ms  =  {5,6:0.00} ms por pulsacion  ({6:0}% del cuadro)",
                                                    pw, ph, pw * ph * 4.0 / 1048576.0, rehacer, subir, rehacer + subir, 100.0 * (rehacer + subir) / 16.666));
                }
            }
            so.SetBounds(0, 0, W, H);

            // DESGLOSE: cuanto de todo eso son las tarjetas, y cuanto el resto.
            Console.WriteLine("\n  DESGLOSE DEL BUSCADOR\n");
            MethodInfo mCubo = TSO.GetMethod("DrawPreviewCube", BindingFlags.Instance | BindingFlags.NonPublic);
            if (mCubo != null)
            {
                int M2 = 30, cY = M2 + 70 + 28, cH = H - cY - M2;
                int kW = (int)((W - 2 * M2) * 0.52);
                object[] ac = new object[] { g, new Rectangle(M2, cY, kW, cH) };
                double[] ang = new double[] { 0 };
                Fila("EL CUBO DE VISTA PREVIA girando con su caratula", Mide(40, delegate
                {
                    ang[0] += 0.075; TSO.GetField("CubeAngle").SetValue(so, ang[0]);
                    mCubo.Invoke(so, ac);
                }));
            }
            MethodInfo mKeys = TSO.GetMethod("DrawKeys", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo mRes = TSO.GetMethod("DrawResults", BindingFlags.Instance | BindingFlags.NonPublic);
            int M = 30, contentY = M + 70 + 28, contentH = H - contentY - M;
            int kbW = (int)((W - 2 * M) * 0.52);
            Rectangle kbRegion = new Rectangle(M, contentY, kbW, contentH);
            Rectangle resRegion = new Rectangle(M + kbW + 20, contentY, (W - 2 * M) - kbW - 20, contentH);
            if (mKeys != null) Fila("el teclado (40 teclas, ya guardado)", Mide(60, delegate { mKeys.Invoke(so, new object[] { g, kbRegion }); }));
            if (mRes != null)
            {
                object[] ar = new object[] { g, resRegion };
                Fila("los resultados (40 tarjetas, ya guardadas)", Mide(60, delegate { mRes.Invoke(so, ar); }));
                Fila("los resultados CAMBIANDO el elegido cada vez", Mide(60, delegate
                {
                    sel[0] = (sel[0] + 1) % 40; fSel.SetValue(so, sel[0]);
                    mRes.Invoke(so, ar);
                }));
                // Y lo que cuesta cuando la consulta cambia (una tecla nueva): hay que rehacer las 40 tarjetas.
                int[] k = new int[] { 0 };
                Fila("los resultados REHACIENDOLOS (una tecla nueva)", Mide(30, delegate
                {
                    k[0]++;
                    TSO.GetField("Query").SetValue(so, "de" + (char)('a' + k[0] % 20));
                    mRes.Invoke(so, ar);
                }));
            }
        }

        // ---- que parte de rehacer una tarjeta se va en buscar DONDE cae lo tecleado ----
        Console.WriteLine("\n  DENTRO DE UNA TARJETA\n");
        MethodInfo mBT = TF.GetMethod("BuscaTramo", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo mNo = TF.GetMethod("Normalize", BindingFlags.Static | BindingFlags.NonPublic);
        if (mBT != null)
        {
            string qn = (string)mNo.Invoke(null, new object[] { "de" });
            object[] ar = new object[] { "Depeche Mode - Enjoy The Silence", qn, 0, 0 };
            double t = Mide(200, delegate { for (int i = 0; i < 40; i++) { ar[0] = nom[i % nom.Length]; mBT.Invoke(null, ar); } });
            Fila("localizar lo tecleado en las 40 tarjetas (por reflexion)", t);
            double sol = Mide(400, delegate { for (int i = 0; i < 40; i++) mNo.Invoke(null, new object[] { nom[i % nom.Length] }); });
            Fila("   de eso, solo quitar tildes 40 veces", sol);
        }

        Console.WriteLine();
        return fallos;
    }

    static string Gen<T>(T it) { return "Balada"; }
    // La misma copia de bloque que usa el programa para subir la textura (RtlMoveMemory en Windows).
    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "memcpy")]
    static extern IntPtr MemCpy(IntPtr dst, IntPtr src, UIntPtr n);
    static void CopiaFila(IntPtr dst, IntPtr src, int bytes) { MemCpy(dst, src, (UIntPtr)(uint)bytes); }
    static Bitmap portada;
    static Image Cov<T>(T a) { return portada; }
}
