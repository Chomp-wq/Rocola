using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

// LA LIMPIEZA DE VIDEOS QUE NO SUENAN.
//
// En una biblioteca que lleva años armandose se cuelan videos con la pista de sonido rota, muda o grabada tan
// bajo que no se oye nada. Puestos en la rocola son una loteria mala: el cliente paga, sale el video y no
// suena. La rocola los localiza y los puede mandar a la papelera.
//
// Esto BORRA ARCHIVOS DEL CLIENTE, asi que lo que hay que vigilar no es que borre, es que NO BORRE DE MAS:
//
//   1) Un video que aun no se ha medido NO PUEDE aparecer nunca en la lista. Sin medida no se sabe si suena.
//   2) Uno que suena bien tampoco, evidentemente; el corte son 30 dB por debajo del volumen de las canciones.
//   3) Si Windows no puede mandarlos a la papelera, la biblioteca tiene que quedarse EXACTAMENTE como estaba
//      -ni un archivo descontado- en vez de dar por hecho que se fueron.
//   4) Y al quitarlos, solo se descuentan los que de verdad ya no estan en el disco.
class Mudos
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF;
    static object rocola;
    static FieldInfo Campo(string n) { return TF.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); }
    static bool Pon(string c, object v) { FieldInfo f = Campo(c); if (f == null) return false; f.SetValue(rocola, v); return true; }
    static object Lee(string c) { FieldInfo f = Campo(c); return (f == null) ? null : f.GetValue(rocola); }
    static MethodInfo Metodo(string n) { return TF.GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); }

    static double Lin(double db) { return Math.Pow(10.0, db / 20.0); }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        Type TAlb = TF.GetNestedType("Album", BindingFlags.NonPublic | BindingFlags.Public);

        MethodInfo mMudos = Metodo("VideosMudos");
        MethodInfo mQuita = Metodo("QuitaDeLaBiblioteca");
        MethodInfo mBorra = Metodo("BorraVideosMudos");
        Console.WriteLine();
        if (mMudos == null || mQuita == null || mBorra == null)
        {
            Check(false, "la rocola sabe localizar los videos que no suenan (no existe: no hay limpieza)");
            Console.WriteLine("\n  FALLOS: " + fallos);
            return fallos;
        }

        rocola = FormatterServices.GetUninitializedObject(TF);
        float objetivo = 0.26f;
        Pon("normTarget", objetivo);

        // Una biblioteca de videos: unos medidos y sonando bien, otros medidos y callados, y otros SIN MEDIR
        // todavia (que son los que no se pueden tocar bajo ningun concepto).
        string[] rutas = {
            @"C:\Videos\bien_1.mp4",      @"C:\Videos\bien_2.mp4",   @"C:\Videos\justo_en_el_borde.mp4",
            @"C:\Videos\bajito_pero_se_oye.mp4",
            @"C:\Videos\mudo_a_32.mp4",   @"C:\Videos\mudo_a_45.mp4", @"C:\Videos\silencio_total.mp4",
            @"C:\Videos\sin_medir_1.mp4", @"C:\Videos\sin_medir_2.mp4",
        };
        double[] dbs = { 0, -6, -29.5, -20, -32, -45, -999, -999, -999 };   // -999 = no esta en la tabla
        Dictionary<string, float> medidas = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rutas.Length; i++)
        {
            if (dbs[i] < -900) continue;
            medidas[rutas[i]] = (float)(objetivo * Lin(dbs[i]));
        }
        // El silencio DE VERDAD: el pre-analisis lo apunta con una marca minuscula para que quede como
        // "medido y callado" en vez de como "sin medir" (si no, se volveria a medir en cada arranque y no
        // aparecia nunca aqui).
        FieldInfo fMarca = TF.GetField("LoudMudo", BindingFlags.Static | BindingFlags.NonPublic);
        float marca = (fMarca != null) ? (float)fMarca.GetValue(null) : 0.00001f;
        medidas[@"C:\Videos\silencio_total.mp4"] = marca;

        IList vids = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlb));
        object disco1 = Activator.CreateInstance(TAlb, true), disco2 = Activator.CreateInstance(TAlb, true);
        TAlb.GetField("Artist").SetValue(disco1, "Disco con mezcla");
        TAlb.GetField("Artist").SetValue(disco2, "Disco de puros mudos");
        IList l1 = (IList)TAlb.GetField("VideoFiles").GetValue(disco1);
        IList l2 = (IList)TAlb.GetField("VideoFiles").GetValue(disco2);
        for (int i = 0; i < 5; i++) l1.Add(rutas[i]);
        for (int i = 5; i < rutas.Length; i++) l2.Add(rutas[i]);
        vids.Add(disco1); vids.Add(disco2);
        Pon("videoAlbums", vids);
        Pon("loudCache", medidas);
        Pon("durCache", new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase));
        Pon("durFast", new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase));

        // ---------- 1) a quien senala ----------
        object[] arg = new object[] { null };
        IList mudos = (IList)mMudos.Invoke(rocola, arg);
        IList db = (IList)arg[0];
        Console.WriteLine("  " + rutas.Length + " videos: 4 suenan, 3 no suenan, 2 sin medir");
        Console.WriteLine("  la rocola senala " + mudos.Count + ":");
        for (int i = 0; i < mudos.Count; i++)
            Console.WriteLine("     " + ((float)db[i]).ToString("0").PadLeft(5) + " dB   " + Path.GetFileName((string)mudos[i]));
        Console.WriteLine();

        List<string> sen = new List<string>();
        foreach (object o in mudos) sen.Add((string)o);
        Check(!sen.Contains(@"C:\Videos\sin_medir_1.mp4") && !sen.Contains(@"C:\Videos\sin_medir_2.mp4"),
              "un video SIN MEDIR no se senala jamas (sin medida no se sabe si suena)");
        Check(!sen.Contains(@"C:\Videos\bien_1.mp4") && !sen.Contains(@"C:\Videos\bien_2.mp4")
              && !sen.Contains(@"C:\Videos\bajito_pero_se_oye.mp4"),
              "los que suenan no se senalan, ni el que va 20 dB bajo");
        Check(!sen.Contains(@"C:\Videos\justo_en_el_borde.mp4"), "ni el que se queda a medio dB del corte (-29.5 dB)");
        Check(sen.Contains(@"C:\Videos\mudo_a_32.mp4") && sen.Contains(@"C:\Videos\mudo_a_45.mp4"),
              "los que no se oyen si se senalan");
        Check(sen.Contains(@"C:\Videos\silencio_total.mp4"), "y el que no trae sonido NINGUNO tambien (el silencio queda apuntado, no se pierde)");
        Check(mudos.Count == 3, "en total, 3 de " + rutas.Length + " (senala " + mudos.Count + ")");
        bool orden = true;
        for (int i = 1; i < db.Count; i++) if ((float)db[i] < (float)db[i - 1]) orden = false;
        Check(orden, "y van del mas callado al menos, que es como se miran");

        // ---------- 2) si Windows no puede, no se descuenta nada ----------
        // Aqui no hay papelera de Windows que valga (esto no corre en Windows), asi que ALaPapelera falla.
        // Justo por eso sirve: es el ensayo de lo que pasa si en la rocola falla tambien.
        int antesVid = 0;
        foreach (object a in vids) antesVid += ((IList)TAlb.GetField("VideoFiles").GetValue(a)).Count;
        int borrados = (int)mBorra.Invoke(rocola, new object[] { mudos, db });
        int despuesVid = 0;
        foreach (object a in (IList)Lee("videoAlbums")) despuesVid += ((IList)TAlb.GetField("VideoFiles").GetValue(a)).Count;
        Check(borrados == 0, "si Windows no puede mandarlos a la papelera, no dice que borro nada (dice " + borrados + ")");
        Check(despuesVid == antesVid, "y la biblioteca se queda igual, con sus " + antesVid + " videos (quedan " + despuesVid + ")");
        Check(((IDictionary)Lee("loudCache")).Count == medidas.Count, "y ninguna medida se pierde");

        // ---------- 3) la contabilidad, con archivos de verdad ----------
        // Se crean tres archivos, se borran dos, y se comprueba que la rocola descuenta EXACTAMENTE esos dos.
        string tmp = Path.Combine(Path.GetTempPath(), "rocola_mudos_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tmp);
        try
        {
            string[] tres = { Path.Combine(tmp, "a.mp4"), Path.Combine(tmp, "b.mp4"), Path.Combine(tmp, "c.mp4") };
            foreach (string f in tres) File.WriteAllText(f, "x");
            IList vids2 = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlb));
            object d3 = Activator.CreateInstance(TAlb, true), d4 = Activator.CreateInstance(TAlb, true);
            IList la = (IList)TAlb.GetField("VideoFiles").GetValue(d3);
            IList lb = (IList)TAlb.GetField("VideoFiles").GetValue(d4);
            la.Add(tres[0]); la.Add(tres[2]);   // este disco pierde uno y le queda otro
            lb.Add(tres[1]);                    // y este se queda sin ninguno
            vids2.Add(d3); vids2.Add(d4);
            Dictionary<string, float> m2 = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (string f in tres) m2[f] = 0.001f;
            Pon("videoAlbums", vids2); Pon("loudCache", m2);
            Pon("cachedVideoPaths", new List<string>(tres));
            Pon("letterAvailable", new HashSet<char>(new char[] { 'A', '\u25BA' }));

            File.Delete(tres[0]); File.Delete(tres[1]);   // esos dos ya no estan; c.mp4 sigue
            List<string> pedidos = new List<string>(tres);
            int n = (int)mQuita.Invoke(rocola, new object[] { pedidos });
            IList qv = (IList)Lee("videoAlbums");
            int quedan = 0;
            foreach (object a in qv) quedan += ((IList)TAlb.GetField("VideoFiles").GetValue(a)).Count;
            Console.WriteLine();
            Check(n == 2, "de tres pedidos, solo se descuentan los DOS que ya no estan (descuenta " + n + ")");
            Check(quedan == 1 && qv.Count == 1, "queda un video en un solo disco; el que se quedo vacio desaparece");
            Check(((IDictionary)Lee("loudCache")).Count == 1, "y sus medidas se van con ellos");
            Check(((IList)Lee("cachedVideoPaths")).Count == 1, "tambien del cache de la biblioteca");
            HashSet<char> le = (HashSet<char>)Lee("letterAvailable");
            Check(le.Contains('\u25BA'), "y como AUN quedan videos, la casilla VIDEO sigue en la barra");

            // Y si se van todos, la casilla VIDEO se va de la barra.
            File.Delete(tres[2]);
            mQuita.Invoke(rocola, new object[] { new List<string>(new string[] { tres[2] }) });
            le = (HashSet<char>)Lee("letterAvailable");
            Check(!le.Contains('\u25BA') && ((IList)Lee("videoAlbums")).Count == 0,
                  "si se van todos, la casilla VIDEO se va de la barra sola");
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}
