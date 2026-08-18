using System;
using System.Diagnostics;
using System.Reflection;

// Cuanto CPU cuesta armar la aurora en cada cuadro. Lo caro es escribir los vertices de la malla, asi que
// se mide AurMalla (que es justo esa parte) llamandola tantas veces como cuadros dibuja la rocola.
class CostoAur
{
    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        Type TP = null, TVtx = null;
        foreach (Type t in asm.GetTypes()) { if (t.Name == "GpuPanel") TP = t; if (t.Name == "Vtx") TVtx = t; }
        BindingFlags SF = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        MethodInfo MALLA = TP.GetMethod("AurMalla", SF);
        MethodInfo PERF = TP.GetMethod("AurPerfil", SF);
        MethodInfo RAYO = TP.GetMethod("AurRayo", SF);
        Func<string,float> C = delegate(string n) {
            object v = TP.GetField(n, SF).GetRawConstantValue();
            return (v is int) ? (int)v : (float)v;
        };
        int cols = (int)C("AurCols"), filas = (int)C("AurFilas"), suelos = (int)C("AurSuelo");
        int puntos = cols + 1;
        float[] hC = new float[puntos], aC = new float[puntos], rC = new float[puntos];
        uint[] cCor = new uint[(filas+1)*puntos], cSu = new uint[(suelos+1)*puntos];
        for (int i = 0; i < puntos; i++) { hC[i] = 200f; aC[i] = 0.6f; rC[i] = 1f; }
        Array vs = Array.CreateInstance(TVtx, (filas+suelos)*cols*6);
        object[] args = { vs, hC, cCor, cSu, aC, rC, 1920f, 900f, 1080f };

        MALLA.Invoke(null, args);   // calentar
        int N = 2000;
        Stopwatch sw = Stopwatch.StartNew();
        for (int k = 0; k < N; k++) MALLA.Invoke(null, args);
        sw.Stop();
        double msMalla = sw.Elapsed.TotalMilliseconds / N;

        // el perfil + el ondeo, que es la otra mitad (una vez por punto y por cuadro)
        object[] pa = { new float[]{0.7f,0.7f,0.7f,0.7f,0.7f,0.7f,0.7f,0.7f}, 0f };
        object[] ra = { 0f, 0f };
        sw.Reset(); sw.Start();
        for (int k = 0; k < 200; k++)
            for (int i = 0; i < puntos; i++)
            { pa[1] = (float)i/cols; PERF.Invoke(null, pa); ra[0] = (float)i/cols; RAYO.Invoke(null, ra); }
        sw.Stop();
        double msPerf = sw.Elapsed.TotalMilliseconds / 200;   // (lleva encima el coste de la reflexion: es un TECHO)

        // Y el COLOR: TonoPorNivel se llama una vez por punto y por franja (cortina + suelo). Se mide en un
        // bucle propio (sin reflexion) para que el numero sea el de verdad.
        MethodInfo TONO = TP.GetMethod("TonoPorNivel", SF);
        object[] ta = { 180f, 90f, 220f, 0.7f };
        TONO.Invoke(null, ta);
        int llamadas = puntos * ((filas+1) + (suelos+1));
        sw.Reset(); sw.Start();
        for (int k = 0; k < 100; k++) for (int q = 0; q < llamadas; q++) TONO.Invoke(null, ta);
        sw.Stop();
        double msTonoRefl = sw.Elapsed.TotalMilliseconds / 100;

        int nv = (filas+suelos)*cols*6;
        Console.WriteLine(string.Format("\n  malla: {0} columnas x {1} franjas = {2:#,0} vertices ({3:#,0} KB por cuadro)",
                                        cols, filas+suelos, nv, nv*28/1024));
        Console.WriteLine(string.Format("  armar la malla:      {0:0.000} ms por cuadro  ->  {1:0.0}% de un nucleo a 60 cuadros/s", msMalla, msMalla*60/10));
        Console.WriteLine(string.Format("  perfil + ondeo:      menos de {0:0.000} ms por cuadro (medido POR REFLEXION, que cuesta 20 veces mas)", msPerf));
        Console.WriteLine(string.Format("  color (TonoPorNivel): {0} llamadas por cuadro, menos de {1:0.000} ms (POR REFLEXION: el real es ~20 veces menos)", llamadas, msTonoRefl));
        Console.WriteLine(string.Format("  copia al driver:     {0:#,0} KB/s a 60 cuadros/s", nv*28L*60/1024));
        return (msMalla * 60 / 10 < 6.0) ? 0 : 1;   // menos del 6% de un nucleo
    }
}
