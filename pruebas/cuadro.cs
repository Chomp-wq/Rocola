using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

// EN QUE SE LE VA EL TIEMPO A CADA CUADRO DEL FONDO.
//
// El usuario dice: "cuando baja los fps, la grafica NO sube su uso". Eso significa que la tarjeta esta
// esperando y el que va ahogado es el procesador. La tarjeta solo dibuja lo que se le entrega ya masticado:
// esta version le entrega TODOS los triangulos recalculados desde cero en cada cuadro, hechos por el
// procesador uno a uno. Aqui se mide cuanto cuesta ese masticado, parte por parte, para saber donde esta
// el tiempo de verdad antes de tocar nada.
//
// Se compila DENTRO del programa (-main) para poder llamar a las funciones de dentro sin intermediarios: si
// se llamaran por reflexion en un bucle caliente, la medida seria de la reflexion y no del dibujo.
namespace Rocola
{
    static class Cuadro
    {
        // MINIMO de varias tandas: la maquina esta compartida, asi que la media miente (recoge los ratos en
        // que otro proceso robo el nucleo) y el minimo es lo que de verdad cuesta el trabajo.
        const int TANDAS = 7;

        static double Mide(int reps, Action f)
        {
            f(); f();                                    // calentar (traducir a maquina)
            double mejor = double.MaxValue;
            for (int b = 0; b < TANDAS; b++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                for (int i = 0; i < reps; i++) f();
                sw.Stop();
                double ms = sw.Elapsed.TotalMilliseconds / reps;
                if (ms < mejor) mejor = ms;
            }
            return mejor;
        }

        delegate void BSV(D3D9.Vtx[] buf, float[] xs, float[] ys, float[] ph, float[] spd, float[] sz,
                          int[] kd, uint[] col, int n, double t, float fade, float pulse, float dx, float dy, float w, float h);

        static void Main()
        {
            const int W = 1024, H = 768;      // la pantalla del bar
            Console.WriteLine("\n  DONDE SE LE VA EL TIEMPO A UN CUADRO DEL FONDO  (" + W + "x" + H + ")\n");

            // ---------- 1) EL CAMPO DE ESTRELLAS ----------
            // Se rearma entero cada cuadro: un cuadrangulo por estrella, con su seno de titileo.
            int nStars = Math.Max(60, W * H / 6500); if (nStars > 260) nStars = 260;
            MethodInfo miB = typeof(GpuStarfield).GetMethod("BuildStarVerts", BindingFlags.Static | BindingFlags.NonPublic);
            BSV bsv = (BSV)Delegate.CreateDelegate(typeof(BSV), miB);
            float[] sx = new float[nStars], sy = new float[nStars], sph = new float[nStars], ssp = new float[nStars], ssz = new float[nStars];
            int[] skd = new int[nStars]; uint[] scol = new uint[nStars];
            Random rnd = new Random(1234);
            for (int i = 0; i < nStars; i++)
            {
                sx[i] = (float)(rnd.NextDouble() * W); sy[i] = (float)(rnd.NextDouble() * H);
                sph[i] = (float)(rnd.NextDouble() * 6.28); ssp[i] = 0.5f + (float)rnd.NextDouble();
                ssz[i] = 1f + (float)rnd.NextDouble() * 3f; skd[i] = rnd.Next(3); scol[i] = 0x00FFFFFF;
            }
            D3D9.Vtx[] starV = new D3D9.Vtx[nStars * 6];
            double tEstrellas = Mide(300, delegate { bsv(starV, sx, sy, sph, ssp, ssz, skd, scol, nStars, 12.5, 1f, 0.5f, 3f, 2f, W, H); });

            // ---------- 2) LA AURORA ----------
            // Se llama al MISMO metodo que usa el programa. La tarjeta no existe en esta maquina, asi que se
            // le pone un campo de estrellas "apagado" (Ok=false): las llamadas de dibujo no hacen nada y lo
            // que queda medido es EXACTAMENTE el trabajo del procesador armando la malla.
            GpuPanel pan = new GpuPanel();
            pan.SetBounds(0, 0, W, H);
            object apagado = FormatterServices.GetUninitializedObject(typeof(GpuStarfield));   // sin tarjeta: Ok=false
            typeof(GpuPanel).GetField("stars", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pan, apagado);
            MethodInfo miAur = typeof(GpuPanel).GetMethod("DrawAurora", BindingFlags.Instance | BindingFlags.NonPublic);

            GpuAurora au = new GpuAurora();
            au.Has = true;
            au.B = new float[64];
            for (int i = 0; i < au.B.Length; i++) au.B[i] = 0.25f + 0.7f * (float)Math.Abs(Math.Sin(i * 0.31));
            au.BaseY = H - 96f; au.Alto = H * 0.40f;
            au.Rgb = 0x7A3CC8; au.Rgb2 = 0x3C6AC8; au.RgbA = 0xC85AA0; au.Rgb2A = 0x8A5AC8;
            au.Dim = 1f; au.Calma = 0f;
            object[] arg = new object[] { au };
            miAur.Invoke(pan, arg); miAur.Invoke(pan, arg);           // calentar + reservar sus arreglos
            double tAurora = Mide(120, delegate { miAur.Invoke(pan, arg); });

            // Y las dos partes de la aurora por separado, para saber cual pesa.
            FieldInfo fHs = typeof(GpuPanel).GetField("aurHs", BindingFlags.Instance | BindingFlags.NonPublic);
            float[] hs = (float[])fHs.GetValue(pan);
            uint[] cort = (uint[])typeof(GpuPanel).GetField("aurCortina", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pan);
            uint[] suelo = (uint[])typeof(GpuPanel).GetField("aurSueloC", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pan);
            float[] aA = (float[])typeof(GpuPanel).GetField("aurA", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pan);
            float[] aR = (float[])typeof(GpuPanel).GetField("aurR", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pan);
            uint[] nc = (uint[])typeof(GpuPanel).GetField("aurNc", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pan);
            float[] ni = (float[])typeof(GpuPanel).GetField("aurNi", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pan);
            uint[] lc = (uint[])typeof(GpuPanel).GetField("aurLc", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pan);
            float[] li = (float[])typeof(GpuPanel).GetField("aurLi", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pan);
            D3D9.Vtx[] aurV = (D3D9.Vtx[])typeof(GpuPanel).GetField("aurV", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pan);
            D3D9.Vtx[] aurVb = (D3D9.Vtx[])typeof(GpuPanel).GetField("aurVb", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pan);
            int nMalla = 0, nBrillo = 0;
            double tMalla = Mide(300, delegate { nMalla = GpuPanel.AurMalla(aurV, hs, cort, suelo, aA, aR, W, au.BaseY, H); });
            double tBrillo = Mide(300, delegate { nBrillo = GpuPanel.AurBrillo(aurVb, hs, nc, ni, lc, li, W, au.BaseY); });
            float[] tmp = new float[hs.Length];
            double tSuav = Mide(2000, delegate { GpuPanel.AurSuaviza(hs, tmp); });

            // ---------- 3) CLAVAR LOS ARREGLOS PARA ENTREGARLOS ----------
            // Cada llamada de dibujo "clava" (pinned) su arreglo para pasarle la direccion al driver. Clavar
            // impide al recolector de basura mover ese trozo de memoria mientras dure la llamada.
            double tClavar = Mide(20000, delegate
            {
                GCHandle gh = GCHandle.Alloc(aurV, GCHandleType.Pinned);
                gh.Free();
            });

            int vAur = nMalla + nBrillo, vEst = nStars * 6;
            int vTot = vAur + vEst + 12;
            Console.WriteLine("     parte                                   ms/cuadro   vertices");
            Console.WriteLine("     " + new string('-', 62));
            Console.WriteLine(F("campo de estrellas (rearmado entero)", tEstrellas, vEst));
            Console.WriteLine(F("aurora COMPLETA (lo que hace el programa)", tAurora, vAur));
            Console.WriteLine(F("   de eso: la malla de relleno", tMalla, nMalla));
            Console.WriteLine(F("   de eso: el filo de neon + horizonte", tBrillo, nBrillo));
            Console.WriteLine(F("   de eso: los tres suavizados", tSuav * 3, 0));
            Console.WriteLine(F("   de eso: el resto (colores columna a columna)", tAurora - tMalla - tBrillo - tSuav * 3, 0));
            Console.WriteLine(F("clavar un arreglo para entregarlo (x1)", tClavar, 0));
            Console.WriteLine("     " + new string('-', 62));
            double total = tEstrellas + tAurora;
            Console.WriteLine(F("SUMA del masticado por cuadro", total, vTot));
            Console.WriteLine();
            Console.WriteLine("     bytes de vertices entregados por cuadro: " + (vTot * D3D9.VtxSize / 1024) + " KB");
            Console.WriteLine("     a 60 cuadros/seg eso es " + (vTot * D3D9.VtxSize * 60 / 1024 / 1024) + " MB/seg copiados por el driver");
            Console.WriteLine();
            Console.WriteLine("     techo de cuadros/seg que deja este masticado (un solo nucleo): "
                              + (total > 0.001 ? (1000.0 / total).ToString("0") : "-"));
            Console.WriteLine("     lo que se come de un cuadro de 16.7 ms (60 fps): " + (total / 16.7 * 100).ToString("0.0") + " %");
            Console.WriteLine();
            return;
        }

        static string F(string q, double ms, int verts)
        {
            return string.Format("     {0,-44} {1,7:0.000}   {2}", q, ms, verts > 0 ? verts.ToString() : "");
        }
    }
}
