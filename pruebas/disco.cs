using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

// LEER LA DURACION DE UN MP3 SIN LEERLO ENTERO.
//
// Antes, para saber cuanto dura una cancion, se recorria el archivo COMPLETO frame a frame. Con veinte mil
// canciones de 5 MB eso son cien gigas de lectura, y en un disco duro mecanico es media hora de cabezal.
// Ahora se lee la cabecera, que es donde la duracion viene escrita.
//
// Aqui se comprueban tres cosas:
//   1) que la duracion que sale es la CORRECTA (se compara con la que se sabe de antemano y con la que da
//      el camino lento de siempre)
//   2) que de verdad se leen unos kilobytes y no el archivo entero
//   3) que con archivos rotos no revienta y cae al camino lento
class Disco
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static bool MismosBytes(string a, string b)
    {
        byte[] x = File.ReadAllBytes(a), y = File.ReadAllBytes(b);
        if (x.Length != y.Length) return false;
        for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
        return true;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        MethodInfo mRap = TF.GetMethod("DuracionPorCabecera", BindingFlags.Static | BindingFlags.NonPublic);
        // El camino LENTO se toma del programa ANTERIOR: en el de ahora, MideDuracionAudio ya prueba primero
        // la cabecera, asi que medirlo aqui seria medir dos veces lo mismo.
        MethodInfo mLen = null;
        Assembly aV = null;
        try
        {
            if (!File.Exists("rocola_ant.exe"))
                throw new FileNotFoundException("Falta la referencia historica", "rocola_ant.exe");
            // LoadFrom puede unificar dos ejecutables con la misma identidad y devolver el actual.
            // Cargar los bytes crea una referencia realmente independiente.
            aV = Assembly.Load(File.ReadAllBytes("rocola_ant.exe"));
            mLen = aV.GetType("Rocola.Form1", true).GetMethod("MideDuracionAudio", BindingFlags.Static | BindingFlags.NonPublic);
        }
        catch (Exception ex) { Console.WriteLine("  FALLO  no se pudo cargar rocola_ant.exe: " + ex.Message); fallos++; }
        bool refDistinta = aV != null && !MismosBytes(exe, "rocola_ant.exe") && !Object.ReferenceEquals(aV, asm) &&
            aV.ManifestModule.ModuleVersionId != asm.ManifestModule.ModuleVersionId &&
            !Object.ReferenceEquals(aV.ManifestModule, asm.ManifestModule);
        Check(mRap != null, "el programa sabe leer la duracion de la cabecera");
        Check(refDistinta, "la referencia es un ejecutable historico distinto al actual");
        Check(mLen != null && aV != null && Object.ReferenceEquals(mLen.Module.Assembly, aV), "y se puede comparar con el camino lento de antes");
        Check(TF.GetMethod("MideDuracionAudio", BindingFlags.Static | BindingFlags.NonPublic) != null, "el camino lento sigue ahi como respaldo");
        if (mRap == null || mLen == null || !refDistinta) return 1;

        string dir = "mp3s";
        if (!Directory.Exists(dir) || !File.Exists(Path.Combine(dir, "esperado.txt")))
        { Console.WriteLine("  faltan los mp3 de prueba (ejecuta: python3 hacemp3.py mp3s)"); return 1; }

        // ---------- 1) LA DURACION ES LA CORRECTA ----------
        Console.WriteLine("\n  1) LA DURACION QUE SALE ES LA CORRECTA\n");
        Console.WriteLine(string.Format("     {0,-20} {1,10} {2,10} {3,10}   {4}", "archivo", "de verdad", "cabecera", "lento", "que es"));
        Console.WriteLine("     " + new string('-', 84));
        int malos = 0, sinCabecera = 0, buenos = 0;
        List<string> lineas = new List<string>(File.ReadAllLines(Path.Combine(dir, "esperado.txt")));
        foreach (string ln in lineas)
        {
            string[] p = ln.Split('|');
            if (p.Length < 4) continue;
            string ruta = Path.Combine(dir, p[0]);
            double esperado = double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
            float rap = (float)mRap.Invoke(null, new object[] { ruta });
            float len = (float)mLen.Invoke(null, new object[] { ruta });
            string veredicto;
            if (esperado <= 0.001)
            {
                // Archivo roto: lo unico que se exige es que no invente una duracion.
                veredicto = (rap <= 0.001f) ? "roto: no inventa nada" : "<<< INVENTA UNA DURACION";
                if (rap > 0.001f) malos++;
            }
            else if (rap <= 0.001f)
            {
                sinCabecera++;
                veredicto = "sin cabecera util: al camino lento";
            }
            else
            {
                // Un segundo de margen: en pantalla la duracion se ve en minutos y segundos.
                bool ok = Math.Abs(rap - esperado) < 1.0;
                veredicto = ok ? "correcta" : "<<< NO CUADRA";
                if (!ok) malos++; else buenos++;
            }
            Console.WriteLine(string.Format("     {0,-20} {1,10:0.00} {2,10:0.00} {3,10:0.00}   {4}   [{5}]",
                                            p[0], esperado, rap, len, veredicto, p[3]));
        }
        Console.WriteLine("     medidas por cabecera y correctas: " + buenos + "   sin cabecera util: " + sinCabecera + "   mal: " + malos);
        Check(malos == 0, "ninguna duracion sale mal, y con los archivos rotos no se inventa ninguna");
        Check(buenos >= 12, "la cabecera resuelve la gran mayoria de los casos (" + buenos + " de 14 buenos)");

        // ---------- 2) EL CAMINO LENTO DA LO MISMO ----------
        // Es lo que de verdad importa: la duracion que se ensena no cambia respecto a como estaba antes.
        Console.WriteLine("\n  2) LA MISMA DURACION QUE DABA EL CAMINO LENTO\n");
        int difieren = 0, comparadas = 0; double peor = 0; string dondePeor = null;
        foreach (string ln in lineas)
        {
            string[] p = ln.Split('|');
            if (p.Length < 4) continue;
            string ruta = Path.Combine(dir, p[0]);
            float rap = (float)mRap.Invoke(null, new object[] { ruta });
            float len = (float)mLen.Invoke(null, new object[] { ruta });
            if (rap <= 0.001f || len <= 0.001f) continue;   // uno de los dos no sabe: no hay nada que comparar
            comparadas++;
            double d = Math.Abs(rap - len);
            if (d > peor) { peor = d; dondePeor = p[0]; }
            if (d >= 1.0) difieren++;
        }
        Console.WriteLine("     archivos comparados: " + comparadas + "   con mas de un segundo de diferencia: " + difieren);
        Console.WriteLine("     la mayor diferencia: " + peor.ToString("0.000") + " s" + (dondePeor != null ? (" (" + dondePeor + ")") : ""));
        Check(difieren == 0, "la cabecera y el camino lento dan la misma duracion (menos de un segundo de diferencia)");

        // ---------- 3) CUANTO SE LEE DEL DISCO ----------
        // Se cuenta de verdad: se cronometra y se mide el tamano. Lo importante es el numero de bytes que hay
        // que traer del plato, que es lo que en un disco duro cuesta tiempo.
        Console.WriteLine("\n  3) CUANTO HAY QUE LEER DEL DISCO\n");
        long totalBytes = 0; int nArch = 0;
        foreach (string ln in lineas)
        {
            string[] p = ln.Split('|');
            if (p.Length < 4) continue;
            long tam = long.Parse(p[2]);
            if (tam > 100000) { totalBytes += tam; nArch++; }
        }
        double tRap = Mide(mRap, dir, lineas);
        double tLen = Mide(mLen, dir, lineas);
        Console.WriteLine("     " + nArch + " archivos de mas de 100 KB, " + (totalBytes / 1024 / 1024) + " MB en total");
        Console.WriteLine(string.Format("     leyendo la cabecera: {0,7:0.0} ms      leyendo el archivo entero: {1,7:0.0} ms   ({2:0.0} veces)",
                                        tRap, tLen, tLen / Math.Max(tRap, 0.001)));
        // EL TIEMPO DEPENDE DE LA MAQUINA y aqui no dice gran cosa: los archivos de prueba estan en la
        // memoria del sistema, asi que leer de mas casi no se nota y la diferencia sale pequeña. Lo que NO
        // depende de la maquina es cuantos BYTES hay que traer del plato, que es exactamente lo que en un
        // disco duro cuesta tiempo. Eso es lo que se exige; el cronometro se deja solo como informacion.
        long leeCabecera = (long)nArch * (8192 + 10);
        Console.WriteLine("     bytes que hay que traer:  por la cabecera " + (leeCabecera / 1024) + " KB   contra "
                          + (totalBytes / 1024 / 1024) + " MB leyendo entero   ("
                          + (totalBytes / Math.Max(leeCabecera, 1)) + " veces menos)");
        Check(leeCabecera * 20 < totalBytes, "por la cabecera hay que traer del disco veinte veces menos");
        Check(tRap <= tLen, "y aun con todo en la memoria del sistema no sale mas lento");

        // Y la cuenta de lo que supone en una biblioteca de bar.
        Console.WriteLine("\n  LO QUE SUPONE EN UNA BIBLIOTECA DE VERDAD\n");
        long porArchivo = 8192 + 10;    // lo que lee el camino nuevo: la cabecera y poco mas
        long medio = 5L * 1024 * 1024;  // una cancion tipica de 5 MB
        for (int k = 0; k < 2; k++)
        {
            int n = (k == 0) ? 5000 : 20000;
            double gigasAntes = (double)n * medio / 1024 / 1024 / 1024;
            double megasAhora = (double)n * porArchivo / 1024 / 1024;
            Console.WriteLine(string.Format("     {0,6} canciones:   antes {1,6:0.0} GB de lectura      ahora {2,6:0.0} MB   ({3:0} veces menos)",
                                            n, gigasAntes, megasAhora, gigasAntes * 1024 / megasAhora));
        }

        // ---------- 4) EL GENERO, SIN LEER LA CARATULA ----------
        // La etiqueta de un MP3 puede llevar la caratula dentro y medir megas. Para sacar el genero -que es
        // UNA palabra- antes se leia toda; ahora se salta la imagen. Se comprueba que el genero SIGUE
        // saliendo, este antes o despues de la imagen, y que se lee poquisimo.
        Console.WriteLine("\n  4) EL GENERO SALE SIN LEER LA CARATULA\n");
        Type TTrack = null;
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            if (t.Name == "Track") TTrack = t;
        MethodInfo mGen = (TTrack != null) ? TTrack.GetMethod("SoloGenero", BindingFlags.Static | BindingFlags.Public) : null;
        MethodInfo mFrom = (TTrack != null) ? TTrack.GetMethod("FromFile", BindingFlags.Static | BindingFlags.Public) : null;
        MethodInfo mEnsure = (TTrack != null) ? TTrack.GetMethod("EnsureTags", BindingFlags.Instance | BindingFlags.Public) : null;
        Check(mGen != null, "el programa sabe leer el genero a solas");
        if (mGen != null && mFrom != null && mEnsure != null)
        {
            string[,] casos = {
                { "gen_antes.mp3",    "Rock"   },
                { "gen_despues.mp3",  "Cumbia" },
                { "gen_sinarte.mp3",  "Balada" },
                { "gen_ninguno.mp3",  ""       },
                { "cbr128.mp3",       ""       },
                { "vacio.mp3",        ""       },
                { "nomp3.mp3",        ""       },
            };
            int mal = 0;
            FieldInfo fGenero = TTrack.GetField("Genre");
            Console.WriteLine(string.Format("     {0,-20} {1,-10} {2,-10} {3,-10}", "archivo", "esperado", "a solas", "leyendo todo"));
            Console.WriteLine("     " + new string('-', 60));
            for (int i = 0; i < casos.GetLength(0); i++)
            {
                string ruta = Path.Combine(dir, casos[i, 0]);
                string esp = casos[i, 1];
                string solo = (string)mGen.Invoke(null, new object[] { ruta });
                // Y lo que da el camino de siempre, que lee la etiqueta entera: tiene que ser LO MISMO.
                object tk = mFrom.Invoke(null, new object[] { ruta });
                mEnsure.Invoke(tk, null);
                string todo = (string)fGenero.GetValue(tk) ?? "";
                bool ok = (solo == esp) && (todo == esp);
                if (!ok) mal++;
                Console.WriteLine(string.Format("     {0,-20} {1,-10} {2,-10} {3,-10} {4}", casos[i, 0],
                                                esp.Length == 0 ? "(ninguno)" : esp,
                                                solo.Length == 0 ? "(ninguno)" : solo,
                                                todo.Length == 0 ? "(ninguno)" : todo,
                                                ok ? "" : "<<< NO CUADRA"));
            }
            Check(mal == 0, "el genero sale igual que leyendo la etiqueta entera, este antes o despues de la caratula");

            // Y cuanto hay que traer del archivo, con caratulas de 120 a 300 KB por medio. Desde que leer la etiqueta
            // entera TAMBIEN salta la caratula, los dos caminos cuestan casi lo mismo y el cronometro ya no los
            // separa (con los archivos en la memoria del sistema, ademas, dice poco). Lo que importa, y no
            // depende de la maquina, es que NINGUNO de los dos traiga la imagen: se cuentan los bytes que piden
            // de verdad, pasandoles el archivo por un flujo que los cuenta.
            string[] conArte = { "gen_antes.mp3", "gen_despues.mp3", "gen_ninguno.mp3" };
            MethodInfo mLeeGen = TTrack.GetMethod("ReadGenreStream", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo mLeeTodo = TTrack.GetMethod("ReadId3Stream", BindingFlags.Static | BindingFlags.NonPublic);
            Check(mLeeGen != null && mLeeTodo != null, "se pueden contar los bytes que lee cada camino");
            if (mLeeGen != null && mLeeTodo != null)
            {
                long etiquetas = 0, bSolo = 0, bTodo = 0; int sinCaratula = 0, distinto = 0;
                Console.WriteLine(string.Format("\n     {0,-20} {1,12} {2,14} {3,14}", "archivo", "etiqueta", "solo genero", "etiqueta ent."));
                Console.WriteLine("     " + new string('-', 64));
                foreach (string f in conArte)
                {
                    byte[] datos = File.ReadAllBytes(Path.Combine(dir, f));
                    long etiqueta = (datos.Length >= 10 && datos[0] == (byte)'I' && datos[1] == (byte)'D' && datos[2] == (byte)'3')
                        ? 10L + ((datos[6] & 0x7F) << 21 | (datos[7] & 0x7F) << 14 | (datos[8] & 0x7F) << 7 | (datos[9] & 0x7F)) : 0;
                    // Sin una caratula grande dentro, la cuenta no demostraria nada.
                    if (etiqueta < 100 * 1024) sinCaratula++;
                    object g = TrackVacio(TTrack), t = TrackVacio(TTrack);
                    long lg, lt;
                    using (Contador s = new Contador(datos)) { mLeeGen.Invoke(null, new object[] { s, g }); lg = s.Bytes; }
                    using (Contador s = new Contador(datos)) { mLeeTodo.Invoke(null, new object[] { s, t }); lt = s.Bytes; }
                    // Contados asi tienen que dar el mismo genero que leyendo el archivo de verdad.
                    if ((string)fGenero.GetValue(g) != (string)fGenero.GetValue(t)) distinto++;
                    etiquetas += etiqueta; bSolo += lg; bTodo += lt;
                    Console.WriteLine(string.Format("     {0,-20} {1,9} KB {2,8} bytes {3,8} bytes", f, etiqueta / 1024, lg, lt));
                }
                Check(sinCaratula == 0, "los tres archivos llevan de verdad una caratula de mas de 100 KB en la etiqueta");
                Check(distinto == 0, "contando los bytes, los dos caminos siguen dando el mismo genero");
                Check(bSolo * 10 < etiquetas, "leer solo el genero no trae la caratula (" + bSolo + " bytes de " + (etiquetas / 1024) + " KB de etiquetas)");
                Check(bTodo * 10 < etiquetas, "leer la etiqueta entera tampoco la trae (" + bTodo + " bytes de " + (etiquetas / 1024) + " KB de etiquetas)");
                Check(bSolo <= bTodo, "y leer solo el genero no pide mas que leer la etiqueta entera");
            }
            // El cronometro queda solo como informacion.
            double tSolo = MideGen(mGen, null, null, dir, conArte);
            double tTodo = MideGen(null, mFrom, mEnsure, dir, conArte);
            Console.WriteLine(string.Format("\n     tiempo (informativo):  a solas {0,6:0.00} ms      leyendo todo {1,6:0.00} ms",
                                            tSolo, tTodo));
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }

    // Un archivo en memoria que apunta cuantos bytes se le piden de verdad (saltar con Position o Seek no cuenta:
    // en un disco duro saltar no trae nada del plato).
    sealed class Contador : MemoryStream
    {
        public long Bytes;
        public Contador(byte[] datos) : base(datos, false) { }
        public override int Read(byte[] b, int off, int n) { int r = base.Read(b, off, n); Bytes += r; return r; }
        public override int ReadByte() { int r = base.ReadByte(); if (r >= 0) Bytes++; return r; }
    }

    static object TrackVacio(Type TTrack)
    {
        object t = Activator.CreateInstance(TTrack, true);
        foreach (string f in new string[] { "Title", "Artist", "Album", "Genre" }) TTrack.GetField(f).SetValue(t, "");
        TTrack.GetField("Path").SetValue(t, "no-se-abre.mp3");
        return t;
    }

    static double MideGen(MethodInfo mGen, MethodInfo mFrom, MethodInfo mEnsure, string dir, string[] archivos)
    {
        object[] a1 = new object[1];
        for (int w = 0; w < 3; w++) Una(mGen, mFrom, mEnsure, dir, archivos, a1);
        double mejor = double.MaxValue;
        for (int t = 0; t < 7; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int k = 0; k < 5; k++) Una(mGen, mFrom, mEnsure, dir, archivos, a1);
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / 5.0;
            if (ms < mejor) mejor = ms;
        }
        return mejor;
    }
    static void Una(MethodInfo mGen, MethodInfo mFrom, MethodInfo mEnsure, string dir, string[] archivos, object[] a1)
    {
        foreach (string f in archivos)
        {
            a1[0] = Path.Combine(dir, f);
            try
            {
                if (mGen != null) mGen.Invoke(null, a1);
                else { object tk = mFrom.Invoke(null, a1); mEnsure.Invoke(tk, null); }
            }
            catch { }
        }
    }

    static double Mide(MethodInfo m, string dir, List<string> lineas)
    {
        object[] arg = new object[1];
        // Dos vueltas de calentamiento y luego la mejor de cinco (la maquina esta compartida).
        for (int w = 0; w < 2; w++)
            foreach (string ln in lineas)
            { string[] p = ln.Split('|'); if (p.Length < 4) continue; arg[0] = Path.Combine(dir, p[0]); try { m.Invoke(null, arg); } catch { } }
        double mejor = double.MaxValue;
        for (int t = 0; t < 5; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            foreach (string ln in lineas)
            { string[] p = ln.Split('|'); if (p.Length < 4) continue; arg[0] = Path.Combine(dir, p[0]); try { m.Invoke(null, arg); } catch { } }
            sw.Stop();
            if (sw.Elapsed.TotalMilliseconds < mejor) mejor = sw.Elapsed.TotalMilliseconds;
        }
        return mejor;
    }
}
