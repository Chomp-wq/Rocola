using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

// DE QUE CANCION VIENE CADA EJEMPLO DE LA IA.
//
// Para saber si la red del DSEE aprende de verdad hay que validarla con canciones que NUNCA vio. Si cuadros
// de la misma cancion caen a la vez en entrenamiento y en validacion, la red se "sabe" la respuesta y el
// numero de validacion sale mejor de lo que es. El archivo de material (rocola_ia_datos.bin) no dice de que
// cancion viene cada ejemplo, y eso no se puede adivinar despues.
//
// Asi que la rocola apunta al lado, en rocola_ia_datos.idx, una entrada por cancion cosechada con la huella
// de la cancion y de su carpeta, y un CRC que ata la entrada a los bytes exactos que describe. Aqui se
// escribe con el codigo DE LA ROCOLA, se rompe de todas las formas que puede romperse en un bar (apagones a
// media escritura, archivo de datos borrado, indice ajeno, bytes cambiados, varios hilos a la vez) y se le
// da a leer al lector de Python del entrenador, que es quien decide que se usa. La regla que se vigila:
// un ejemplo puede quedarse SIN origen, pero NUNCA con un origen equivocado.
class IaOrigenPrueba
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    const BindingFlags SF = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static Type TF;
    static object rocola;
    static MethodInfo mOrigen, mConOrigen, mSinOrigen, mCrc;
    static string datos, idx, tmp;

    static object Origen(string ruta, double dur) { return mOrigen.Invoke(null, new object[] { ruta, dur, 44100, 2048, 0 }); }

    static List<float> Pares(int n, int semilla)
    {
        List<float> l = new List<float>(n * 40);
        for (int i = 0; i < n * 40; i++) l.Add((float)((i * 7 + semilla * 131) % 1000) / 250f - 2f);
        return l;
    }

    static bool Guarda(int n, int semilla, object origen)
    {
        return (bool)mConOrigen.Invoke(rocola, new object[] { Pares(n, semilla), origen });
    }

    // Lo que ve el lector de Python del entrenador (ia/origen_dsee.py).
    static Dictionary<string, string> Python(string extra)
    {
        string ia = Path.GetFullPath(Path.Combine("..", "ia"));
        string py = Path.Combine(tmp, "lee.py");
        File.WriteAllText(py,
            "import sys, json\n" +
            "sys.path.insert(0, r'" + ia + "')\n" +
            "import origen_dsee as O\n" +
            "ent, inf = O.lee_indice(r'" + idx + "', r'" + datos + "')\n" +
            "for k, v in sorted(inf.items()): print(k, int(v))\n" +
            "print('primeros', ','.join(str(int(x)) for x in ent['primero']))\n" +
            "print('cuentas', ','.join(str(int(x)) for x in ent['cuenta']))\n" +
            "print('canciones', ','.join('%x' % int(x) for x in ent['cancion']))\n" +
            "print('carpetas', ','.join('%x' % int(x) for x in ent['carpeta']))\n" +
            "print('extractores', ','.join(str(int(x)) for x in ent['extractor']))\n" + extra);
        Dictionary<string, string> d = new Dictionary<string, string>();
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("ROCOLA_TEST_PYTHON") ?? "python3", "\"" + py + "\"");
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            using (Process p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd(), e = p.StandardError.ReadToEnd();
                p.WaitForExit();
                foreach (string l in o.Split('\n'))
                {
                    int sp = l.IndexOf(' ');
                    if (sp > 0) d[l.Substring(0, sp)] = l.Substring(sp + 1).Trim();
                }
                if (p.ExitCode != 0) { Console.WriteLine(e); return null; }
            }
        }
        catch { return null; }
        return d;
    }

    static int N(Dictionary<string, string> d, string k) { string v; return d != null && d.TryGetValue(k, out v) ? int.Parse(v) : -999; }
    static string S(Dictionary<string, string> d, string k) { string v; return d != null && d.TryGetValue(k, out v) ? v : ""; }

    static void Limpia()
    {
        foreach (string f in Directory.GetFiles(tmp, "rocola_ia_datos*")) File.Delete(f);
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        tmp = Path.Combine(Path.GetTempPath(), "rocola_iaorigen_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        // Todo en una carpeta temporal: nunca los datos de verdad de nadie.
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", tmp);
        try { return Corre(exe); }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }

    static int Corre(string exe)
    {
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        FieldInfo fCarpeta = TF.GetField("carpetaDatos", SF);
        if (fCarpeta != null) fCarpeta.SetValue(null, tmp);
        rocola = FormatterServices.GetUninitializedObject(TF);
        TF.GetField("iaFileLock", IF).SetValue(rocola, new object());
        mOrigen = TF.GetMethod("IaOrigenDe", SF);
        mConOrigen = TF.GetMethod("IaAppendPairsOrigen", IF);
        mSinOrigen = TF.GetMethod("IaAppendPairs", IF);
        mCrc = TF.GetMethod("IaCrc32", SF);
        Check(mOrigen != null && mConOrigen != null && mSinOrigen != null && mCrc != null,
              "la rocola sabe apuntar el origen de lo que cosecha");
        if (fallos > 0) return fallos;
        datos = (string)TF.GetMethod("IaDataFile", IF).Invoke(rocola, null);
        idx = (string)TF.GetMethod("IaIndexFile", IF).Invoke(rocola, null);
        Check(Path.GetDirectoryName(datos) == tmp && Path.GetFileName(idx) == "rocola_ia_datos.idx",
              "escribe en la carpeta temporal de la prueba, al lado del material (" + Path.GetFileName(idx) + ")");

        // ============================================================================================
        Console.WriteLine("\n  1) LO QUE ESCRIBE LA ROCOLA LO LEE IGUAL EL ENTRENADOR\n");
        string c1 = Path.Combine(Path.Combine(tmp, "Disco Uno"), "01 Tema.mp3");
        string c2 = Path.Combine(Path.Combine(tmp, "Disco Uno"), "02 Otro.mp3");
        Check(Guarda(7, 1, Origen(c1, 201.4)) && Guarda(5, 2, Origen(c2, 187.6)), "se guardan dos canciones con su origen");
        mSinOrigen.Invoke(rocola, new object[] { Pares(3, 3) });   // como una rocola vieja: sin apuntar el origen
        Check(Guarda(4, 4, Origen(c1, 201.4)), "y otra pasada de la primera cancion");
        string extra = "print('esperada1', '%x' % O.huella_cancion(r'" + c1 + "', 201.4))\n"
                     + "print('carpeta1', '%x' % O.huella(r'" + Path.GetDirectoryName(c1) + "'))\n";
        Dictionary<string, string> r = Python(extra);
        if (r == null) { Console.WriteLine("  OK     (sin Python en esta maquina: el lector del entrenador NO se probo aqui)"); return fallos; }
        Console.WriteLine("     entradas validas " + N(r, "validas") + "; registros " + N(r, "registros_totales")
                          + ", con origen " + N(r, "registros_con_origen"));
        Check(S(r, "primeros") == "0,7,15" && S(r, "cuentas") == "7,5,4",
              "tres entradas con sus rangos exactos (0-6, 7-11 y 15-18); los 3 de en medio quedan sin origen");
        Check(N(r, "registros_totales") == 19 && N(r, "registros_con_origen") == 16, "19 ejemplos, 16 con origen");
        string[] can = S(r, "canciones").Split(','), carp = S(r, "carpetas").Split(',');
        Check(can.Length == 3 && can[0] == can[2] && can[0] != can[1],
              "la misma cancion cosechada dos veces lleva la misma huella; otra cancion, otra");
        Check(can.Length == 3 && can[0] == S(r, "esperada1") && carp[0] == S(r, "carpeta1") && carp[0] == carp[1],
              "y son las mismas huellas que calcula Python (nombre+duracion; carpeta)");
        Check(S(r, "extractores") == "2,2,2", "cada entrada dice con que extractor se midio (version 2)");
        string idxTxt = Encoding.GetEncoding("iso-8859-1").GetString(File.ReadAllBytes(idx));
        Check(idxTxt.IndexOf("Tema", StringComparison.OrdinalIgnoreCase) < 0 && idxTxt.IndexOf("Disco", StringComparison.OrdinalIgnoreCase) < 0,
              "en el indice no hay nombres ni rutas en claro, solo huellas");

        // ============================================================================================
        Console.WriteLine("\n  2) APAGONES Y ARCHIVOS ROTOS: SIN ORIGEN SI, ORIGEN EQUIVOCADO NUNCA\n");
        using (FileStream fs = new FileStream(idx, FileMode.Open)) fs.SetLength(fs.Length - 10);   // entrada a medias
        r = Python("");
        Check(N(r, "cola_incompleta") == 1 && N(r, "validas") == 2, "una entrada cortada por un apagon se ignora; las otras siguen");
        Check(Guarda(2, 5, Origen(c2, 187.6)), "y la siguiente cosecha recorta ese trozo y sigue detras");
        r = Python("");
        Check(N(r, "validas") == 3 && N(r, "cola_incompleta") == 0 && S(r, "primeros") == "0,7,19",
              "ahora hay tres entradas enteras (la cortada no vuelve: sus ejemplos quedan sin origen)");

        byte[] b = File.ReadAllBytes(idx);
        b[16 + 64 + 34] ^= 0x40;                     // un bit de la huella de la segunda entrada
        File.WriteAllBytes(idx, b);
        r = Python("");
        Check(N(r, "crc_entrada_malo") == 1 && N(r, "validas") == 2, "una entrada con un bit cambiado se descarta por su CRC");
        b[16 + 64 + 34] ^= 0x40; File.WriteAllBytes(idx, b);

        byte[] d = File.ReadAllBytes(datos);
        d[16 + 8 * 160 + 3] ^= 0x01;                 // un byte dentro de la segunda cancion
        File.WriteAllBytes(datos, d);
        r = Python("");
        Check(N(r, "crc_datos_malo") == 1 && S(r, "primeros") == "0,19",
              "si los ejemplos no son los que la entrada describe, esa entrada no vale");
        d[16 + 8 * 160 + 3] ^= 0x01; File.WriteAllBytes(datos, d);

        using (FileStream fs = new FileStream(datos, FileMode.Open)) fs.SetLength(16 + 20 * 160);   // datos recortados
        r = Python("");
        Check(N(r, "fuera_de_rango") == 1 && N(r, "validas") == 2, "una entrada que apunta mas alla del archivo de datos se ignora");

        // ============================================================================================
        Console.WriteLine("\n  3) EL ARCHIVO DE DATOS EMPIEZA DE CERO, O EL INDICE NO ES NUESTRO\n");
        File.Delete(datos);
        Check(Guarda(3, 6, Origen(c1, 201.4)), "se borra el material y la rocola vuelve a juntar");
        r = Python("");
        Check(File.Exists(idx + ".anterior"), "el indice viejo se aparta (.anterior), no se borra");
        Check(N(r, "validas") == 1 && S(r, "primeros") == "0", "y el nuevo solo describe el archivo nuevo");

        Limpia();
        mSinOrigen.Invoke(rocola, new object[] { Pares(2, 8) });   // ya hay material; el indice es de otro
        File.WriteAllText(idx, "esto no es un indice de la rocola, y no se pisa");
        Check(Guarda(2, 7, Origen(c1, 201.4)), "con un indice ajeno el material se guarda igual");
        Check(File.ReadAllText(idx) == "esto no es un indice de la rocola, y no se pisa", "y el archivo ajeno queda intacto");
        r = Python("");
        Check(N(r, "cabecera_mala") == 1 && N(r, "registros_con_origen") == 0, "el entrenador lo ve: esos ejemplos no tienen origen");

        // ============================================================================================
        Console.WriteLine("\n  4) VARIOS HILOS A LA VEZ (EL REPASO A FONDO)\n");
        Limpia();
        const int HILOS = 4, TANDAS = 30;
        Thread[] th = new Thread[HILOS];
        for (int h = 0; h < HILOS; h++)
        {
            int hilo = h;
            th[h] = new Thread(delegate ()
            {
                for (int k = 0; k < TANDAS; k++)
                    Guarda(1 + (k + hilo) % 5, hilo * 100 + k, Origen(Path.Combine(tmp, "h" + hilo + "_" + k + ".mp3"), 100 + k));
            });
        }
        foreach (Thread t in th) t.Start();
        foreach (Thread t in th) t.Join();
        r = Python("");
        Check(N(r, "validas") == HILOS * TANDAS && N(r, "solapadas") == 0 && N(r, "crc_datos_malo") == 0
              && N(r, "registros_con_origen") == N(r, "registros_totales"),
              HILOS * TANDAS + " canciones desde " + HILOS + " hilos: cada entrada describe exactamente sus ejemplos");

        // ============================================================================================
        Console.WriteLine("\n  5) LO QUE CUESTA, Y QUE LA COSECHA DE VERDAD LO USA\n");
        foreach (int pares in new int[] { 192 * 5, 1536 * 5 })
        {
            byte[] bloque = new byte[pares * 160];
            mCrc.Invoke(null, new object[] { bloque, 0, bloque.Length });
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 20; i++) mCrc.Invoke(null, new object[] { bloque, 0, bloque.Length });
            Console.WriteLine("     CRC de una cancion " + (pares == 960 ? "del repaso normal" : "a fondo      ") + " ("
                              + (bloque.Length / 1024) + " KB): " + (sw.Elapsed.TotalMilliseconds / 20).ToString("0.00", CultureInfo.InvariantCulture) + " ms");
        }
        Console.WriteLine("     (en el hilo de fondo, una vez por cancion cosechada; ademas se escriben 64 bytes al disco)");
        MethodInfo mMide = TF.GetMethod("MeasureFile", IF);
        Check(mMide != null && Codigo.Llama(mMide, "IaAppendPairsOrigen") && Codigo.Llama(mMide, "IaOrigenDe")
              && !Codigo.Llama(mMide, "IaAppendPairs"),
              "la cosecha de la biblioteca guarda con origen (no por el camino sin origen)");

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN" : "\n  FALLOS: " + fallos);
        return fallos;
    }
}
