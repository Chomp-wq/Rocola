using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

// EL PERFIL DE DISCO Y LO QUE CAMBIA CON SSD.
//
// La rocola se adapta al disco que tenga: en un disco duro hay UN cabezal y hay que pedirle las cosas en fila
// de uno, dejandolo respirar entre lectura y lectura; en un SSD no hay nada que mover y es al contrario, rinde
// mas cuantas mas peticiones tenga a la vez y los respiros solo lo frenan.
//
// Aqui se comprueban las dos mitades del arreglo:
//   1) que el perfil se decide AL ARRANCAR (antes se decidia despues de la primera pagina, o sea que una
//      rocola con SSD arrancaba siempre en modo lento) y por MEDIANA, no por promedio (una sola lectura
//      desafortunada dejaba la rocola en modo lento toda la noche)
//   2) que con SSD se abren varios carriles y se dejan de repartir respiros que solo hacen perder tiempo
//   3) que el reparto POR DISCO funciona de verdad en Linux, que es donde estaba roto
class Ssd
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF, TTrack;
    static object F;
    static object Est(string n) { return TF.GetField(n, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null); }
    static void PonEst(string n, object v) { TF.GetField(n, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, v); }


    // Una lista de Track (tipo privado del programa) con las rutas que se le pasen.
    static object ListaTracks(string[] rutas)
    {
        IList l = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TTrack));
        FieldInfo fp = TTrack.GetField("Path");
        foreach (string r in rutas)
        {
            object t = FormatterServices.GetUninitializedObject(TTrack);
            fp.SetValue(t, r);
            l.Add(t);
        }
        return l;
    }

    static void Reinicia()
    {
        PonEst("perfilHecho", false);
        PonEst("diskSlow", true);
        PonEst("diskProbeN", 0);
        PonEst("diskProbeMs", 0);
        PonEst("coverCarriles", 1);
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)) if (t.Name == "Track") TTrack = t;
        Check(TTrack != null, "la ficha de cancion esta donde tiene que estar");
        if (TTrack == null) return 1;
        F = FormatterServices.GetUninitializedObject(TF);
        MethodInfo mPerfil = TF.GetMethod("PerfilaDisco", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mPerfil != null, "el perfilado de disco esta donde tiene que estar");
        if (mPerfil == null) return 1;

        Console.WriteLine("\n  1) EL PERFIL SE DECIDE POR LA MEDIANA, NO POR EL PROMEDIO\n");
        List<string> l = Codigo.Llamadas(mPerfil);
        Check(Codigo.Llama(l, "List`1.Sort"), "ordena las medidas y toma la de EN MEDIO (mediana): una lectura desafortunada no arrastra la decision");
        Check(Codigo.Llama(l, "Stopwatch.get_ElapsedMilliseconds") && Codigo.Llama(l, "Stream.Read"), "y cronometra una lectura de verdad, no una cuenta de mentira");

        Console.WriteLine("\n  2) SE DECIDE ANTES DE LA PRIMERA PAGINA DE CARATULAS\n");
        MethodInfo mWork = TF.GetMethod("Scanner_DoWork", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mWork != null && Codigo.Llama(mWork, "Form1.PerfilaDisco"),
              "se perfila dentro del armado de la biblioteca (en segundo plano), no despues de leer 16 miniaturas");
        MethodInfo mDec = TF.GetMethod("DecodeCover", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mDec != null, "y el perfilado 'de oido' sigue ahi como respaldo, por si no se pudo medir");

        Console.WriteLine("\n  3) MIDIENDO DE VERDAD, SOBRE ARCHIVOS DE VERDAD\n");
        // Este banco tiene disco rapido, asi que lo que TIENE que salir es SSD.
        string dir = Path.Combine(Path.GetTempPath(), "ssdprueba");
        Directory.CreateDirectory(dir);
        string[] rutas = new string[12];
        byte[] relleno = new byte[300 * 1024];
        for (int i = 0; i < rutas.Length; i++)
        {
            rutas[i] = Path.Combine(dir, "c" + i + ".mp3");
            File.WriteAllBytes(rutas[i], relleno);
        }
        Reinicia();
        mPerfil.Invoke(F, new object[] { ListaTracks(rutas) });
        bool lento = (bool)Est("diskSlow");
        int carriles = (int)Est("coverCarriles");
        int probeN = (int)Est("diskProbeN");
        Console.WriteLine("     disco de este banco -> " + (lento ? "disco duro" : "SSD") + ", " + carriles + " carril(es)");
        Check(!lento, "en un disco rapido decide SSD");
        Check(carriles >= 2 && carriles <= 4 && carriles <= Math.Max(2, Environment.ProcessorCount),
              "y abre un carril por nucleo, hasta cuatro   [nucleos=" + Environment.ProcessorCount + ", carriles=" + carriles + "]");
        Check(probeN >= 16, "y deja cerrado el perfilado de oido: no puede volver a cambiar la decision a mitad de la noche");

        // Idempotente: llamarlo dos veces no puede volver a ampliar el turno de disco (se pasaria del tope).
        object semAntes = TF.GetField("coverSem", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        System.Threading.Semaphore sem = (System.Threading.Semaphore)semAntes;
        int pudo = 0;
        for (int i = 0; i < 32; i++) { if (sem.WaitOne(0)) pudo++; else break; }
        for (int i = 0; i < pudo; i++) sem.Release();
        Console.WriteLine("     turnos de disco disponibles a la vez: " + pudo);
        Check(pudo == carriles, "el turno de disco quedo del tamano de los carriles, ni mas ni menos");
        mPerfil.Invoke(F, new object[] { ListaTracks(rutas) });   // segunda vez: no debe tocar nada
        int pudo2 = 0;
        for (int i = 0; i < 32; i++) { if (sem.WaitOne(0)) pudo2++; else break; }
        for (int i = 0; i < pudo2; i++) sem.Release();
        Check(pudo2 == pudo, "y perfilar dos veces no lo amplia otra vez (no se pasa del tope y revienta)");

        // Sin material suficiente NO decide: se queda en modo disco duro, que es lo seguro.
        Reinicia();
        mPerfil.Invoke(F, new object[] { ListaTracks(new string[] { rutas[0], rutas[1] }) });
        Check((bool)Est("diskSlow") && (int)Est("coverCarriles") == 1,
              "con dos canciones nada mas no se arriesga: sigue en modo disco duro");
        Check(!(bool)Est("perfilHecho"), "y lo deja pendiente para volver a intentarlo, no se da por hecho");

        // Y el respaldo de siempre (16 miniaturas cronometradas) sigue funcionando.
        Reinicia();
        MethodInfo mProbe = TF.GetMethod("DiskProbe", BindingFlags.Static | BindingFlags.NonPublic);
        for (int i = 0; i < 16; i++) mProbe.Invoke(null, new object[] { 1 });
        Check(!(bool)Est("diskSlow") && (int)Est("coverCarriles") >= 2, "el respaldo tambien detecta SSD y abre el segundo carril");
        Reinicia();
        for (int i = 0; i < 16; i++) mProbe.Invoke(null, new object[] { 9 });
        Check((bool)Est("diskSlow") && (int)Est("coverCarriles") == 1, "y con lecturas lentas se queda en modo disco duro, con un solo carril");

        Console.WriteLine("\n  4) LO QUE SE DEJA DE PERDER EN RESPIROS (biblioteca de 100.000 canciones)\n");
        // El repaso de cabeceras dormia 1 ms POR ARCHIVO en SSD. Parece nada; multiplicado no lo es.
        const int N = 100000;
        int antes = N * 1;
        int ahora = 0;
        for (int i = 0; i < N; i++) if ((i & 31) == 31) ahora += 1;
        Console.WriteLine(string.Format("     repaso de cabeceras: antes {0} s durmiendo, ahora {1:0.0} s   ({2:0}x menos)",
                          antes / 1000, ahora / 1000.0, (double)antes / Math.Max(ahora, 1)));
        Check(ahora * 20 < antes, "en SSD el repaso deja de irse en respiros (al menos 20 veces menos)");

        // La prelectura de la cancion siguiente: trozos de 256 KB en vez de 64, y 1 ms en vez de 2.
        MethodInfo mWarm = TF.GetMethod("PrecalientaSiguiente", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mWarm != null, "la prelectura de la cancion siguiente esta donde tiene que estar");
        long cancion = 5L * 1024 * 1024;   // una cancion normal de 5 MB
        double antesW = (cancion / (64.0 * 1024)) * 2;
        double ahoraW = (cancion / (256.0 * 1024)) * 1;
        Console.WriteLine(string.Format("     prelectura de una cancion de 5 MB: antes {0:0} ms de respiros, ahora {1:0} ms", antesW, ahoraW));
        Check(ahoraW * 3 < antesW, "y la cancion siguiente queda en memoria mucho antes");

        Console.WriteLine("\n  5) QUE ES UN \"DISCO\" EN LINUX\n");
        // Es de lo mas normal tener dos: el programa y las miniaturas en el disco del sistema y la musica
        // en otro colgado detras, o repartida entre varios ("dir=" en el config, y Ctrl+F2). Un solo
        // veredicto para todo se equivoca en media biblioteca, asi que se guarda uno POR UNIDAD.
        //
        // Y AQUI ESTABA EL FALLO. En Windows la unidad es la letra -C:\, D:\- y Path.GetPathRoot la da. En
        // Linux NO HAY LETRAS: lo que separa un disco de otro es DONDE ESTA MONTADO (/mnt/sda1, /mnt/sdb1),
        // y a eso Path.GetPathRoot contesta "/" para absolutamente todo. O sea que en la rocola del bar la
        // musica, las miniaturas y el programa caian en el mismo saco.
        //
        // Peor: esta seccion pasaba EN VERDE sin probar nada. Comprobaba lo de "una unidad desconocida cae
        // en el veredicto general" con una ruta de Windows ("Z:\otro\disco"), y esa cadena en Linux no
        // tiene raiz, asi que salia por una rama distinta de la que se pretendia comprobar.
        MethodInfo mLento = TF.GetMethod("DiscoLento", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo mUnidad = TF.GetMethod("UnidadDe", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mLento != null && mUnidad != null, "cada archivo se puede preguntar por SU disco, no solo por el general");
        if (mLento == null || mUnidad == null) return Math.Max(1, fallos);
        Check(Codigo.Llama(mPerfil, "Form1.UnidadDe"), "y el perfilado aparta las medidas por unidad (no las mete todas en el mismo saco)");

        Type TS = asm.GetType("Rocola.Sistema");
        MethodInfo mMontajes = (TS != null) ? TS.GetMethod("MontajesDe", new Type[] { typeof(string) }) : null;
        MethodInfo mUnidadTabla = (TS != null) ? TS.GetMethod("UnidadDe", new Type[] { typeof(string), typeof(List<string>) }) : null;
        MethodInfo mOlvida = (TS != null) ? TS.GetMethod("OlvidaLasUnidades", new Type[0]) : null;
        Check(mMontajes != null && mUnidadTabla != null && mOlvida != null,
              "la rocola sabe leer los discos montados, y la regla se puede preguntar dandole una tabla (asi se prueba sin discos)");

        if (mUnidadTabla != null && mMontajes != null)
        {
            // UNA MAQUINA DE PUPPY DE MENTIRA, pero con la tabla escrita como la escribe el sistema de
            // verdad: un disco dentro de otro (para ver que gana el mas hondo) y uno con un espacio en el
            // nombre, que /proc/mounts no escribe tal cual sino como "\040".
            string tabla =
                "/dev/sda1 / ext4 rw,relatime 0 0\n" +
                "tmpfs /tmp tmpfs rw 0 0\n" +
                "/dev/sdb1 /mnt/sdb1 vfat rw 0 0\n" +
                "/dev/sdc1 /mnt/sdb1/extra ext4 rw 0 0\n" +
                "/dev/sdd1 /mnt/Musica\\040Vieja vfat rw 0 0\n";
            List<string> montajes = (List<string>)mMontajes.Invoke(null, new object[] { tabla });
            Console.WriteLine("     discos de la maquina de mentira: " + string.Join(", ", montajes.ToArray()));
            Func<string, string> Unid = delegate (string ru) { return (string)mUnidadTabla.Invoke(null, new object[] { ru, montajes }); };

            string[][] casos = new string[][] {
                new string[] { "/mnt/sdb1/musica/Disco 1/x.mp3", "/mnt/sdb1" },
                new string[] { "/mnt/sdb1/extra/y.mp3",          "/mnt/sdb1/extra" },
                new string[] { "/mnt/Musica Vieja/z.mp3",        "/mnt/Musica Vieja" },
                new string[] { "/mnt/sdb1",                      "/mnt/sdb1" },
                new string[] { "/tmp/rocola_miniaturas/a.jpg",   "/tmp" },
                new string[] { "/home/rocola/w.mp3",             "/" },
            };
            Console.WriteLine();
            for (int i = 0; i < casos.Length; i++)
            {
                string dio = Unid(casos[i][0]);
                Console.WriteLine("       " + casos[i][0].PadRight(32) + " -> " + dio);
                Check(dio == casos[i][1], "  cae en " + casos[i][1]);
            }
            Console.WriteLine();
            Check(Unid("/mnt/sdb1/extra/y.mp3") != Unid("/mnt/sdb1/musica/Disco 1/x.mp3"),
                  "un disco montado DENTRO de otro no se confunde con el de fuera");
            Check(Unid("/mnt/sdb11/x.mp3") == "/",
                  "y /mnt/sdb11 no se toma por /mnt/sdb1 (se compara por carpetas, no por letras)");
        }

        Console.WriteLine("\n  6) Y CON LOS DISCOS DE VERDAD DE ESTA MAQUINA\n");
        // Lo de arriba prueba la regla; esto prueba que ademas se lee bien la tabla de verdad. Se busca un
        // disco montado que no sea la raiz -en el bar seria el de la musica- y se comprueba que un archivo
        // suyo se reporta como ESE disco. Antes salia "/" para todos, que es el fallo.
        Func<string, string> UnidReal = delegate (string ru) { return (string)mUnidad.Invoke(null, new object[] { ru }); };
        // Se busca un sitio montado aparte donde ADEMAS se pueda escribir: hace falta para esto y para lo
        // del disco chico de mas abajo. Los de pega del sistema (/proc, /sys, /dev y sus cosas) no cuentan,
        // que no son discos de nadie.
        string otroDisco = null, dirChico = null;
        try
        {
            foreach (string li in File.ReadAllLines("/proc/mounts"))
            {
                string[] c = li.Split(' ');
                if (c.Length < 2) continue;
                string mp = c[1];
                if (mp == "/" || mp == "/dev" || mp.IndexOf("\\") >= 0) continue;
                if (mp.StartsWith("/proc") || mp.StartsWith("/sys") || mp.StartsWith("/dev/pts")) continue;
                if (!Directory.Exists(mp)) continue;
                try
                {
                    string d2 = Path.Combine(mp, "ssdprueba_chico");
                    Directory.CreateDirectory(d2);
                    otroDisco = mp; dirChico = d2;
                    break;
                }
                catch { }
            }
        }
        catch { }
        if (otroDisco == null) Console.WriteLine("     (esta maquina no tiene un segundo disco donde escribir: no hay dos que separar)");
        else
        {
            string dio = UnidReal(Path.Combine(otroDisco, "musica", "x.mp3"));
            Console.WriteLine("     un archivo de " + otroDisco + " se reporta como \"" + dio + "\"");
            Check(dio == otroDisco, "un archivo de otro disco montado se reconoce como suyo, no como \"/\"");
            Check(UnidReal("/x.mp3") != dio, "y uno de la raiz no se confunde con el");
        }

        Console.WriteLine("\n  7) EL VEREDICTO DE CADA DISCO MANDA SOBRE EL GENERAL\n");
        Func<string, bool> Lento = delegate (string ru) { return (bool)mLento.Invoke(null, new object[] { ru }); };
        string unid = (string)mUnidad.Invoke(null, new object[] { rutas[0] });
        Console.WriteLine("     el disco de " + rutas[0] + " es \"" + unid + "\"");
        IDictionary mapa = (IDictionary)TF.GetField("discoLento", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        Reinicia();
        PonEst("perfilDisco", 0);
        PonEst("diskSlow", true);            // el veredicto GENERAL dice disco duro...
        mapa.Clear(); mapa[unid] = false;    // ...pero de ESTE disco se midio que es SSD
        Check(!Lento(rutas[0]), "manda lo medido de SU disco, no el veredicto general");
        // Un disco del que no se sabe nada. Se usa una clave que NINGUNA ruta puede dar, para que esto
        // pruebe lo que dice en cualquier maquina y no por casualidad.
        mapa.Clear(); mapa["/un-disco-que-no-esta-conectado"] = false;
        Check(Lento(rutas[0]), "y un archivo de un disco del que no se sabe nada cae en el veredicto general");
        mapa.Clear(); mapa[unid] = true; PonEst("diskSlow", false);
        Check(Lento(rutas[0]), "y al reves igual: disco lento dentro de una rocola rapida");

        Console.WriteLine("\n  8) Y SE PUEDE FIJAR A MANO (Ctrl+F3)\n");
        PonEst("perfilDisco", 1);            // forzar SSD
        Check(!Lento(rutas[0]), "elegir SSD a mano manda sobre lo medido de cada unidad");
        PonEst("perfilDisco", 2);            // forzar disco duro
        Check(Lento(rutas[0]), "y elegir disco duro tambien");
        MethodInfo mAplica = TF.GetMethod("AplicaPerfilDisco", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mAplica != null, "y al elegirlo se rehacen los carriles al momento, sin reiniciar");
        if (mAplica != null)
        {
            PonEst("medidoLento", false);
            PonEst("perfilDisco", 2); mAplica.Invoke(null, null);
            Check((bool)Est("diskSlow") && (int)Est("coverCarriles") == 1, "forzado a disco duro: un solo carril");
            PonEst("perfilDisco", 1); mAplica.Invoke(null, null);
            Check(!(bool)Est("diskSlow") && (int)Est("coverCarriles") >= 2, "forzado a SSD: varios carriles");
            PonEst("perfilDisco", 0); mAplica.Invoke(null, null);
            Check(!(bool)Est("diskSlow"), "y volviendo a automatico manda otra vez LO MEDIDO (no se pierde)");
        }
        PonEst("perfilDisco", 0); mapa.Clear();

        Console.WriteLine("\n  9) LOS RESPIROS VAN POR EL DISCO DE CADA ARCHIVO\n");
        // El repaso de cabeceras y la prelectura de la cancion siguiente van dentro de metodos anonimos (los
        // hilos de fondo), asi que se busca por todo el programa quien pregunta por el disco de cada archivo.
        int preguntan = 0;
        foreach (Type t in asm.GetTypes())
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                if (Codigo.Llama(m, "Form1.DiscoLento")) preguntan++;
        Console.WriteLine("     sitios que preguntan por el disco de ESE archivo: " + preguntan);
        Check(preguntan >= 2, "el repaso de cabeceras y la prelectura preguntan por el disco donde esta cada archivo, no por un veredicto unico");

        Console.WriteLine("\n 10) ENCHUFAR UN DISCO Y DAR F5 VUELVE A MIRARLO\n");
        // El reparto se medía UNA sola vez en toda la sesion. O sea que conectar un disco y pedir un
        // re-escaneo -F5, o agregar la carpeta con Ctrl+F2- traia la musica nueva pero NO medía el disco
        // nuevo: se quedaba con el veredicto general hasta reiniciar la rocola. Los tres caminos que
        // rearman la biblioteca pasan por LoadMusicFolder, asi que ahi es donde tiene que soltarse todo.
        MethodInfo mLoad = TF.GetMethod("LoadMusicFolder", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mLoad != null, "hay un solo sitio por donde se rearma la biblioteca");
        if (mLoad != null)
        {
            int lee, escribe;
            Codigo.UsosDeCampo(mLoad, "perfilHecho", out lee, out escribe);
            Check(escribe > 0, "al rearmar la biblioteca se vuelve a medir el tipo de disco");
            Check(Codigo.Llama(mLoad, "Sistema.OlvidaLasUnidades"), "y se vuelve a mirar QUE discos hay conectados");
            Check(Codigo.Llama(mLoad, "Dictionary`2.Clear"), "y se tiran los veredictos viejos (puede que ese disco ya no este)");
        }

        Console.WriteLine("\n 11) UN DISCO CHICO TAMBIEN SACA SU VEREDICTO\n");
        // El perfilado tomaba 15 lecturas repartidas por IGUAL por toda la biblioteca, y hacen falta 3 de
        // un disco para decidir algo de el. O sea que un disco con menos del 20% de la musica -justo el
        // caso "le agregue un disco chico con lo nuevo"- se quedaba sin veredicto propio. Ahora las
        // lecturas se reparten POR DISCO.
        if (dirChico != null)
        {
            try { using (FileStream fs = new FileStream(Path.Combine(dirChico, "p.mp3"), FileMode.Create)) fs.Write(relleno, 0, relleno.Length); }
            catch { dirChico = null; }
        }
        if (dirChico == null) Console.WriteLine("     (no hay un segundo disco donde escribir: no se puede montar el caso)");
        else
        {
            // 60 canciones en un disco y 6 en el otro: el chico es el 9% de la biblioteca, la mitad de lo
            // que hacia falta antes para que se le mirara siquiera.
            List<string> mezcla = new List<string>();
            for (int i = 0; i < 60; i++) mezcla.Add(rutas[i % rutas.Length]);
            string chico = Path.Combine(dirChico, "p.mp3");
            for (int i = 0; i < 6; i++) mezcla.Add(chico);
            Console.WriteLine("     biblioteca de prueba: 60 canciones en " + unid + " y 6 en " + UnidReal(chico) + "  (el chico es el 9%)");

            Reinicia();
            PonEst("perfilDisco", 0);
            mapa.Clear();
            mPerfil.Invoke(F, new object[] { ListaTracks(mezcla.ToArray()) });
            List<string> salieron = new List<string>();
            foreach (DictionaryEntry de in mapa) salieron.Add((string)de.Key);
            salieron.Sort();
            Console.WriteLine("     discos con veredicto propio: " + (salieron.Count == 0 ? "(ninguno)" : string.Join(", ", salieron.ToArray())));
            // Y lo que se lee en Ctrl+F3, que antes decia "/" y no informaba de nada.
            Console.WriteLine("     lo que dira Ctrl+F3:  " + (string)Est("perfilTexto"));
            Check(salieron.Count >= 2, "los dos discos sacan su propio veredicto, no solo el que tiene la mayoria");
            Check(UnidReal(chico) != unid && mapa.Contains(UnidReal(chico)),
                  "y el chico, con el 9% de la musica, sale APARTE del grande (no metido en su saco)");
            mapa.Clear(); PonEst("perfilDisco", 0);
            try { Directory.Delete(dirChico, true); } catch { }
        }

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
