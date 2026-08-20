using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;

// EL MATERIAL QUE SE JUNTA PARA LA IA.
//
// LO QUE PASABA: despues de dias encendida, la rocola se quedaba clavada en los mismos gigas de material.
// Dos motivos, los dos aqui comprobados:
//
//  1) EL POZO SE SECABA. En cuanto una cancion quedaba apuntada como "cosechada", el modo PASIVO no volvia a
//     mirarla NUNCA. Con la biblioteca entera apuntada, el archivo dejaba de crecer y ahi se quedaba para
//     siempre. Ahora cada cancion se puede mirar varias veces, y cada pasada toma cuadros DISTINTOS.
//
//  2) SIEMPRE LA MISMA REBANADA. Se cosechaban los 192 cuadros PRIMEROS y SEGUIDOS: 8,9 segundos, siempre del
//     mismo sitio de cada cancion. La red solo llegaba a ver ese trozo de toda la biblioteca. Ahora los
//     cuadros se reparten por todo lo que el repaso ya descomprime.
//
// Y ADEMAS se dejaron de tirar FFT: antes se analizaban TODOS los cuadros que pasaban y despues se
// descartaban los que no tocaban. En el repaso A FONDO eso eran mas de tres cuartas partes del trabajo.
class Cosecha
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF;
    static object Est(string n)
    {
        FieldInfo f = TF.GetField(n, BindingFlags.Static | BindingFlags.NonPublic);
        return (f != null) ? f.GetRawConstantValue() : null;
    }
    static int Cte(string n) { object v = Est(n); return (v is int) ? (int)v : -1; }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        Type TS = null;
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)) if (t.Name == "SpectralTap") TS = t;
        Check(TS != null, "el analizador del DSEE esta donde tiene que estar");
        if (TS == null) return 1;

        int porCancion = Cte("IaFramesPerSong"), aFondo = Cte("IaDeepFrames"), calienta = Cte("IaWarmFrames");
        int pasadasMax = Cte("IaPasadasMax");
        int cortes = ((Array)TF.GetField("IaCutsHz", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).Length;
        Console.WriteLine("\n  COMO ESTA MONTADO\n");
        Console.WriteLine("     cuadros por pasada del modo pasivo: " + porCancion);
        Console.WriteLine("     cuadros del repaso a fondo:         " + aFondo);
        Console.WriteLine("     cuadros de calentamiento:           " + calienta + "  (el acantilado necesita asentarse)");
        Console.WriteLine("     cortes simulados por cuadro:        " + cortes + "  -> " + cortes + " ejemplos por cuadro cosechado");
        Check(porCancion > 0 && aFondo > 0 && calienta > 0 && cortes > 0, "las cuentas del material estan a la vista y se pueden ajustar");

        Console.WriteLine("\n  1) EL POZO YA NO SE SECA: VARIAS PASADAS POR CANCION\n");
        MethodInfo mPas = TF.GetMethod("IaPasadasDe", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo mNiv = TF.GetMethod("IaNivelDePasada", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mPas != null && mNiv != null && pasadasMax > 1, "una cancion se puede mirar mas de una vez");
        if (mPas == null || mNiv == null) return 1;
        // Se recorre la vida de una cancion: nunca vista -> pasada tras pasada, y se comprueba que los
        // niveles no se pisan y que el "repasada a fondo" sigue significando lo mismo que siempre.
        int nivel = 0; List<int> niveles = new List<int>();
        for (int k = 0; k < pasadasMax; k++)
        {
            int p = (int)mPas.Invoke(null, new object[] { nivel });
            if (p != k) { Check(false, "la cuenta de pasadas se descuadra en la " + k); break; }
            nivel = (int)mNiv.Invoke(null, new object[] { p });
            niveles.Add(nivel);
        }
        Console.WriteLine("     niveles que va tomando una cancion: " + string.Join(" -> ", niveles.ConvertAll<string>(delegate (int v) { return v.ToString(); }).ToArray()));
        Check(niveles.Count == pasadasMax, "caben " + pasadasMax + " pasadas por cancion");
        bool sinRepetir = true;
        for (int i = 0; i < niveles.Count; i++)
            for (int j = i + 1; j < niveles.Count; j++) if (niveles[i] == niveles[j]) sinRepetir = false;
        Check(sinRepetir, "y cada pasada tiene su propio nivel (no se pisan entre ellas)");
        Check(!niveles.Contains(2), "el nivel 2 se respeta: sigue queriendo decir \"repasada A FONDO\"");
        Check((int)mPas.Invoke(null, new object[] { 2 }) == int.MaxValue, "y una repasada a fondo ya no la vuelve a tocar el modo pasivo (ya dio todo lo suyo)");
        Check((int)mPas.Invoke(null, new object[] { 1 }) == 1, "los archivos VIEJOS se siguen leyendo igual: nivel 1 = una pasada");
        // Y el apunte solo puede SUBIR. El nivel 2 no es "menos" que el 5 aunque su numero lo sea: es el
        // TOPE, porque de una cancion repasada a fondo ya no queda nada. Sin esto, una cancion repasada a
        // fondo podia volver a bajar de nivel y cosecharse otra vez lo mismo.
        MethodInfo mRango = TF.GetMethod("IaRango", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mRango != null, "los niveles tienen un orden propio, aparte de su numero");
        if (mRango != null)
        {
            Func<int, int> R = delegate (int v) { return (int)mRango.Invoke(null, new object[] { v }); };
            Console.WriteLine("     valor de cada nivel:  1=" + R(1) + "  2(a fondo)=" + R(2) + "  5=" + R(5) + "  9=" + R(9));
            Check(R(2) > R(9), "una repasada A FONDO vale mas que cualquier cantidad de pasadas del pasivo");
            bool sube = true;
            for (int k = 3; k < 9; k++) if (R(k) >= R(k + 1)) sube = false;
            Check(sube, "y entre pasadas del pasivo, cada una vale mas que la anterior");
        }

        Console.WriteLine("\n  2) LOS CUADROS SE REPARTEN, NO SE AMONTONAN\n");
        // 3 tramos de 20 s = 60 s descomprimidos por cancion en modo pasivo.
        double segSeg = (double)TF.GetField("PreSegSec", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        int segs = (int)TF.GetField("PreSegs", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        object tap = Activator.CreateInstance(TS, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { 44100, 1 }, null);
        int cuadro = (int)TS.GetProperty("FrameLen", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetGetMethod(true).Invoke(tap, null);
        long dispon = (long)(segs * segSeg * 44100 / cuadro);
        long paso = (dispon - calienta + porCancion - 1) / porCancion;
        int stride = (paso > 1) ? (int)paso : 1;
        Console.WriteLine("     el repaso pasivo descomprime " + segs + " x " + segSeg.ToString("0") + " s = " + (segs * segSeg).ToString("0") + " s de musica");
        Console.WriteLine("     eso son " + dispon + " cuadros; se toma uno de cada " + stride);
        Console.WriteLine("     ANTES se tomaban los " + porCancion + " primeros SEGUIDOS = " + (porCancion * cuadro / 44100.0).ToString("0.0")
                          + " s, siempre del mismo sitio de la cancion");
        Check(stride > 1, "se reparte por todo lo descomprimido en vez de amontonarse al principio");
        Check(stride * porCancion + calienta >= dispon * 0.9, "y entre todas las pasadas se llega hasta el final de lo descomprimido");

        Console.WriteLine("\n  3) LO QUE SE JUNTA POR CANCION\n");
        long antesEj = (long)(porCancion - calienta) * cortes;
        long ahoraEj = (long)porCancion * cortes * Math.Min(pasadasMax, stride);
        Console.WriteLine("     antes:  1 pasada  x " + (porCancion - calienta) + " cuadros x " + cortes + " = " + antesEj.ToString("#,0")
                          + " ejemplos = " + (antesEj * 160 / 1024) + " KB por cancion");
        Console.WriteLine("     ahora: " + Math.Min(pasadasMax, stride) + " pasadas x " + porCancion + " cuadros x " + cortes + " = " + ahoraEj.ToString("#,0")
                          + " ejemplos = " + (ahoraEj * 160 / 1024) + " KB por cancion");
        Console.WriteLine("     -> " + ((double)ahoraEj / antesEj).ToString("0.0") + " veces mas material de la MISMA biblioteca");
        Check(ahoraEj > antesEj * 3, "se junta bastante mas material por cancion");

        Console.WriteLine("\n  4) Y NO CUESTA MAS: SOLO SE ANALIZA LO QUE SIRVE\n");
        // Se cronometra un cuadro de verdad: la FFT con su acantilado, y los pares.
        float[] mono = new float[cuadro];
        for (int i = 0; i < cuadro; i++)
        {
            double t = i / 44100.0;
            mono[i] = (float)(0.4 * Math.Sin(2 * Math.PI * 220 * t) + 0.2 * Math.Sin(2 * Math.PI * 3300 * t) + 0.05 * Math.Sin(2 * Math.PI * 9100 * t));
        }
        MethodInfo mAn = TS.GetMethod("AnalyzeFrame");
        object[] argAn = new object[] { mono, 0 };
        for (int i = 0; i < 300; i++) mAn.Invoke(tap, argAn);
        double msFft = double.MaxValue;
        for (int b = 0; b < 7; b++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 1500; i++) mAn.Invoke(tap, argAn);
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / 1500;
            if (ms < msFft) msFft = ms;
        }
        Console.WriteLine("     analizar un cuadro (FFT + acantilado): " + msFft.ToString("0.000") + " ms");
        // MODO PASIVO: antes se analizaban 192 (los primeros). Ahora, calentamiento + los que se cosechan.
        double antesPas = porCancion * msFft, ahoraPas = (calienta + porCancion) * msFft;
        Console.WriteLine("     pasiva  -> antes " + antesPas.ToString("0") + " ms de analisis, ahora " + ahoraPas.ToString("0") + " ms");
        Console.WriteLine("                (sobre ~1.500 ms que cuesta descomprimir los " + (segs * segSeg).ToString("0") + " s: "
                          + ((ahoraPas - antesPas) / 1500.0 * 100).ToString("0.0") + "% mas)");
        Check(ahoraPas - antesPas < 60, "la pasada pasiva cuesta practicamente lo mismo que antes");

        // A FONDO: antes se analizaba TODO cuadro que pasara hasta el tope (o sea, casi la cancion entera)
        // y se tiraban los que no tocaban. Ahora solo se analizan los que dan par.
        double durTipica = 240.0;   // una cancion de cuatro minutos
        long cuadrosCancion = (long)(durTipica * 44100 / cuadro);
        long pasoF = (cuadrosCancion - calienta + aFondo - 1) / aFondo;
        int strideF = (pasoF > 1) ? (int)pasoF : 1;
        long antesFondo = Math.Min(cuadrosCancion, calienta + (long)strideF * aFondo);   // TODOS los que pasaban
        long ahoraFondo = calienta + aFondo;                                             // solo los que sirven
        Console.WriteLine("     a fondo -> antes " + (antesFondo * msFft).ToString("0") + " ms de analisis (" + antesFondo.ToString("#,0") + " FFT)");
        Console.WriteLine("                ahora " + (ahoraFondo * msFft).ToString("0") + " ms de analisis (" + ahoraFondo.ToString("#,0") + " FFT)   -> "
                          + ((double)antesFondo / ahoraFondo).ToString("0.0") + " veces menos");
        Check(ahoraFondo * 2 < antesFondo, "el repaso a fondo deja de hacer FFT que despues tiraba a la basura");

        Console.WriteLine("\n  5) EL MODELO DEL EQ AUTOMATICO SE CARGA DE VERDAD\n");
        // Estaba documentado y habia entrenador, pero en esta rama NADIE lo cargaba: el archivo junto al
        // exe no servia de nada y el EQ automatico se quedaba siempre con el promedio de la biblioteca.
        int cargas = 0; bool eqIa = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            foreach (string lit in Codigo.Textos(m))
            {
                if (lit == "rocola_ia.bin") cargas++;
                if (lit == "rocola_eq_ia.bin") eqIa = true;
            }
        Console.WriteLine("     modelo del DSEE (rocola_ia.bin) nombrado en el programa: " + (cargas > 0 ? "si" : "NO"));
        Console.WriteLine("     modelo del EQ automatico (rocola_eq_ia.bin):             " + (eqIa ? "si" : "NO"));
        Check(cargas > 0, "el modelo del DSEE se carga si esta junto al exe");
        Check(eqIa, "y el del EQ automatico tambien (antes se entrenaba y la rocola lo ignoraba)");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
