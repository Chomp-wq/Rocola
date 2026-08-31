using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

// EL VOLUMEN DE LOS VIDEOS: QUE SUENEN TODOS IGUAL.
//
// En la rocola los videos no pasan por la cadena de sonido: los reproduce el reproductor de Windows, y lo
// unico que se le puede mandar es un volumen del 0 al 100 donde el 100 es "el archivo tal cual". O sea que
// solo se puede BAJAR: a un video grabado bajito no hay forma de subirlo.
//
// Por eso el unico modo de que suenen todos parejo es bajar los fuertes hasta donde llegan los flojos, y
// para eso hacen falta dos cosas, que son las que se comprueban aqui:
//
//   1) MEDIRLOS. El repaso de la biblioteca solo miraba las canciones ('tracks'), y los videos viven aparte;
//      nunca se median, asi que el nivelado de video no tenia con que trabajar y cada uno sonaba con el
//      volumen que trajera de fabrica.
//   2) DEJAR HUECO. Con el volumen base en 85, un video solo puede subir un 18% antes de toparse con el 100.
//      Y si ademas "Subir TODO" se aplicaba AL FINAL, con +6 dB cualquier video medianamente fuerte se
//      pegaba al 100 y todos volvian a sonar distinto: el nivelado quedaba en nada.
//
// No se mira ningun numero por dentro: se arma una biblioteca de videos con volumenes muy distintos, se le
// pregunta a la rocola con que volumen pondria cada uno, y se calcula LO QUE SE OIRIA. Lo que se mide es la
// diferencia entre el video que mas suena y el que menos.
class Videos
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF;
    static object rocola;

    static FieldInfo Campo(string n)
    {
        return TF.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    }
    static bool Pon(string campo, object v)
    {
        FieldInfo f = Campo(campo);
        if (f == null) return false;
        f.SetValue(rocola, v);
        return true;
    }
    static MethodInfo Metodo(string n)
    {
        return TF.GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    }

    // EL CASO FEO A PROPOSITO: 24 dB repartidos parejo, o sea tantos videos casi mudos como buenos. Una
    // biblioteca de verdad no es asi (esa se prueba mas abajo), pero conviene saber que pasa en lo peor.
    // Los numeros son dB respecto del volumen objetivo de las canciones.
    static float[] Reparto(int n)
    {
        float[] db = new float[n];
        for (int i = 0; i < n; i++) db[i] = -18f + 24f * i / (float)(n - 1);   // de -18 dB a +6 dB
        return db;
    }
    static double Lin(double db) { return Math.Pow(10.0, db / 20.0); }
    static double Db(double lin) { return 20.0 * Math.Log10(lin); }

    // Lo que se OIRIA de cada video: su volumen grabado por el volumen (0..100) que la rocola le manda al
    // reproductor de Windows. Devuelve la diferencia en dB entre el que mas suena y el que menos, y cuantos
    // caen dentro de medio decibelio del mas fuerte (o sea, cuantos quedaron de verdad emparejados).
    static void Suenan(string[] rutas, float[] rms, MethodInfo mVol, out double horquilla, out int parejos, out string detalle)
    {
        double[] sal = new double[rutas.Length];
        double alto = double.MinValue, bajo = double.MaxValue;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < rutas.Length; i++)
        {
            int v = (int)mVol.Invoke(rocola, new object[] { rutas[i] });
            sal[i] = rms[i] * v / 100.0;
            if (sal[i] > alto) alto = sal[i];
            if (sal[i] < bajo) bajo = sal[i];
            if (i % 12 == 0) sb.Append("   ").Append(Db(rms[i] / 0.26).ToString("+0.0;-0.0")).Append(" dB -> vol ").Append(v);
        }
        horquilla = Db(alto) - Db(bajo);
        parejos = 0;
        for (int i = 0; i < sal.Length; i++) if (Db(sal[i]) >= Db(alto) - 0.5) parejos++;
        detalle = sb.ToString();
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        Type TAlb = TF.GetNestedType("Album", BindingFlags.NonPublic | BindingFlags.Public);
        Type TTrk = TF.GetNestedType("Track", BindingFlags.NonPublic | BindingFlags.Public);

        rocola = FormatterServices.GetUninitializedObject(TF);
        // EL MODO NOCHE, APAGADO MIENTRAS SE MIDE ESTO. Esta bateria mide el EMPAREJADO de los videos, y de
        // 11pm a 7am la rocola les baja 4 dB a todos por igual. Sin apagarlo, la misma prueba saldria bien de
        // dia y con otros numeros de madrugada: una prueba que depende de la hora a la que la corras no vale
        // para nada. (El modo noche tiene su propia bateria, 'noche'.)
        FieldInfo fNoche = TF.GetField("nocheOn", BindingFlags.Static | BindingFlags.NonPublic);
        if (fNoche != null) fNoche.SetValue(null, false);

        const int N = 61;
        float objetivo = 0.26f;                       // el volumen objetivo de fabrica de las canciones
        float[] db = Reparto(N);
        string[] rutas = new string[N];
        float[] rms = new float[N];
        Dictionary<string, float> medidas = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < N; i++)
        {
            rutas[i] = @"C:\Videos\video" + i.ToString("00") + ".mp4";
            rms[i] = (float)(objetivo * Lin(db[i]));
        }

        // Un disco de videos con los 61 dentro.
        IList vids = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlb));
        object disco = Activator.CreateInstance(TAlb, true);
        IList lf = (IList)TAlb.GetField("VideoFiles").GetValue(disco);
        for (int i = 0; i < N; i++) lf.Add(rutas[i]);
        vids.Add(disco);
        Pon("videoAlbums", vids);

        Console.WriteLine();
        Console.WriteLine("  UNA BIBLIOTECA DE " + N + " VIDEOS, DE " + db[0].ToString("0") + " dB A +" + db[N - 1].ToString("0") + " dB");
        Console.WriteLine("  (o sea: entre el que mas suena y el que menos hay " + (db[N - 1] - db[0]).ToString("0") + " dB de diferencia)");
        Console.WriteLine();

        // ---------- 1) ¿ENTRAN LOS VIDEOS EN EL REPASO QUE LOS MIDE? ----------
        MethodInfo mTanda = Metodo("TandaPorMedir");
        if (mTanda == null)
        {
            Check(false, "el repaso de la biblioteca incluye los videos (no existe TandaPorMedir: los videos NO se miden)");
        }
        else
        {
            IDictionary durCache = (IDictionary)Activator.CreateInstance(typeof(Dictionary<string, float>));
            IList canciones = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TTrk));
            for (int i = 0; i < 5; i++)
            {
                object t = Activator.CreateInstance(TTrk, true);
                TTrk.GetField("Path").SetValue(t, @"C:\Musica\cancion" + i + ".mp3");
                canciones.Add(t);
                durCache[@"C:\Musica\cancion" + i + ".mp3"] = 200f;   // ya medidas: no tienen que volver
            }
            Pon("tracks", canciones);
            Pon("durCache", durCache);
            Pon("loudCache", Activator.CreateInstance(typeof(Dictionary<string, float>), new object[] { StringComparer.OrdinalIgnoreCase }));
            Pon("deepDone", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            Pon("deepDoneLock", new object());
            Pon("diagOn", false);
            object[] arg = new object[] { false, 0 };
            IList tanda = (IList)mTanda.Invoke(rocola, arg);
            int hayVid = 0;
            foreach (object o in tanda) if (((string)o).EndsWith(".mp4")) hayVid++;
            Console.WriteLine("  el repaso tiene " + tanda.Count + " archivos por medir: " + (tanda.Count - hayVid) + " canciones y " + hayVid + " videos");
            Check(hayVid == N, "los " + N + " videos entran en el repaso que mide la biblioteca (entran " + hayVid + ")");
            Check((int)arg[1] == 5 + N, "y cuentan para la barra de avance del repaso a fondo");

            // Uno ya medido no se vuelve a medir.
            IDictionary lc = (IDictionary)Campo("loudCache").GetValue(rocola);
            lc[rutas[0]] = 0.2f;
            arg = new object[] { false, 0 };
            tanda = (IList)mTanda.Invoke(rocola, arg);
            int hayVid2 = 0;
            foreach (object o in tanda) if (((string)o).EndsWith(".mp4")) hayVid2++;
            Check(hayVid2 == N - 1, "y el que ya tiene medida no se vuelve a medir");
        }

        // ---------- 2) CON ESAS MEDIDAS, ¿SUENAN IGUAL? ----------
        for (int i = 0; i < N; i++) medidas[rutas[i]] = rms[i];
        Pon("loudCache", medidas);
        Pon("normalizeOn", true);
        Pon("normTarget", objetivo);
        Pon("videoVol", 85);
        Pon("masterDb", 0f);
        bool hayAuto = Pon("videoAuto", false);

        MethodInfo mVol = Metodo("VideoVolumeFor");
        MethodInfo mBase = Metodo("VideoBase");
        if (mVol == null) { Console.WriteLine("  no existe VideoVolumeFor"); return 1; }

        double horq; int parejos; string det;
        Console.WriteLine();
        Console.WriteLine("  {0,-46} {1,12} {2,14}", "", "diferencia", "emparejados");
        Console.WriteLine("  " + new string('-', 76));
        Console.WriteLine("  {0,-46} {1,9:0.0} dB {2,10}/{3}", "sin nivelar nada (asi se oye hoy)", db[N - 1] - db[0], 1, N);

        Suenan(rutas, rms, mVol, out horq, out parejos, out det);
        Console.WriteLine("  {0,-46} {1,9:0.0} dB {2,10}/{3}", "perilla a mano en 85", horq, parejos, N);

        // AQUI ESTABA UNO DE LOS DOS PROBLEMAS: "Subir TODO" se aplicaba AL FINAL, empujaba los videos
        // contra el 100 y deshacia el emparejado. Ese de mas el reproductor de Windows no lo puede dar de
        // todos modos, asi que a un video ya medido ya no se le pide.
        Pon("masterDb", 6f);
        double horqM; int parejosM;
        Suenan(rutas, rms, mVol, out horqM, out parejosM, out det);
        Console.WriteLine("  {0,-46} {1,9:0.0} dB {2,10}/{3}", "   ... y con Subir TODO a +6 dB", horqM, parejosM, N);
        Check(parejosM == parejos && Math.Abs(horqM - horq) < 0.01,
              "a mano, \"Subir TODO\" no toca el emparejado de los videos (" + parejos + " -> " + parejosM + ")");
        Pon("masterDb", 0f);

        // ---------- 3) EL VOLUMEN BASE, ELEGIDO SOLO ----------
        // Con la perilla en 85 el reproductor solo tiene un 18% de margen (1.4 dB), asi que casi ningun video
        // flojo alcanza. En automatico el programa mira lo que miden todos y baja el base hasta donde
        // alcanzan casi todos: los fuertes se bajan a esperar a los flojos, que es la unica manera.
        if (!hayAuto || mBase == null)
        {
            Check(false, "el volumen base de los videos se elige solo (no existe: los videos siguen sin emparejar)");
        }
        else
        {
            Pon("videoAuto", true);
            Pon("videoBaseFirma", -1);
            int baseAuto = (int)mBase.Invoke(rocola, null);
            double horqA; int parejosA;
            Suenan(rutas, rms, mVol, out horqA, out parejosA, out det);
            Console.WriteLine("  {0,-46} {1,9:0.0} dB {2,10}/{3}", "volumen base automatico (" + baseAuto + "%)", horqA, parejosA, N);
            Check(parejosA >= parejos * 3 / 2, "hasta en lo peor, en automatico se emparejan bastantes mas ("
                  + parejos + " -> " + parejosA + " de " + N + ")");
            Check(horqA <= (db[N - 1] - db[0]) - 8.0, "y la diferencia baja al menos 8 dB: de "
                  + (db[N - 1] - db[0]).ToString("0.0") + " dB a " + horqA.ToString("0.0") + " dB");
            Check(baseAuto >= 40 && baseAuto <= 100, "el base automatico se queda en su sitio (" + baseAuto + "%, nunca por debajo de 40)");

            // AQUI ESTABA EL OTRO PROBLEMA: "Subir TODO" empujaba los videos contra el 100 y deshacia el
            // emparejado. Ese de mas el reproductor de Windows no lo puede dar, asi que ya no se le pide.
            Pon("masterDb", 6f);
            double horq6; int parejos6;
            Suenan(rutas, rms, mVol, out horq6, out parejos6, out det);
            Console.WriteLine("  {0,-46} {1,9:0.0} dB {2,10}/{3}", "   ... y con Subir TODO a +6 dB", horq6, parejos6, N);
            Check(parejos6 == parejosA && Math.Abs(horq6 - horqA) < 0.01,
                  "\"Subir TODO\" ya no deshace el emparejado de los videos (" + parejosA + " -> " + parejos6 + ")");
            Pon("masterDb", 0f);

            // ---------- 4) UNA BIBLIOTECA DE VERDAD ----------
            // La de arriba es el caso feo a proposito: 24 dB repartidos parejo, o sea que hay tantos videos
            // rotos como buenos. En una biblioteca normal la mayoria estan cerca y hay una cola de rarezas.
            Dictionary<string, float> real = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            float[] rmsReal = new float[N];
            for (int i = 0; i < N; i++)
            {
                double d = (i < N - 9) ? (-5.0 + 6.0 * i / (double)(N - 10))   // el grueso: de -5 a +1 dB
                                       : (-18.0 + 1.5 * (i - (N - 9)));        // la cola: de -18 a -6 dB
                rmsReal[i] = (float)(objetivo * Lin(d));
            }
            for (int i = 0; i < N; i++) real[rutas[i]] = rmsReal[i];
            Pon("loudCache", real);
            Pon("videoBaseFirma", -1);
            int baseR = (int)mBase.Invoke(rocola, null);
            double horqR; int parejosR;
            Suenan(rutas, rmsReal, mVol, out horqR, out parejosR, out det);
            Console.WriteLine();
            Console.WriteLine("  una biblioteca normal (el grueso de -5 a +1 dB, y nueve rarezas hasta -18 dB)");
            Console.WriteLine("  {0,-46} {1,9:0.0} dB {2,10}/{3}", "   base automatico (" + baseR + "%)", horqR, parejosR, N);
            Check(parejosR >= (int)(N * 0.80), "ahi si se emparejan 8 de cada 10 (" + parejosR + " de " + N + ")");

            // Y una donde TODOS vienen bien no tiene por que bajarse: el automatico se queda arriba y no le
            // quita volumen al local por gusto.
            Dictionary<string, float> buenas = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < N; i++) buenas[rutas[i]] = (float)(objetivo * Lin(1.0 + 5.0 * i / (double)(N - 1)));
            Pon("loudCache", buenas);
            Pon("videoBaseFirma", -1);
            Check((int)mBase.Invoke(rocola, null) >= 95, "si todos los videos vienen bien, el automatico no baja nada");

            // Y sin medidas todavia -por ejemplo en Windows XP, que no sabe descomprimirlos-, manda la perilla.
            Pon("loudCache", Activator.CreateInstance(typeof(Dictionary<string, float>), new object[] { StringComparer.OrdinalIgnoreCase }));
            Pon("videoBaseFirma", -1);
            Check((int)mBase.Invoke(rocola, null) == 85, "y sin medidas manda la perilla, como siempre");
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}
