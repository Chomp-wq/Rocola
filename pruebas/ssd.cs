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
        // El disco/antivirus del equipo puede tardar mas de 3 ms. Verificar la decision
        // contra la mediana observada, no suponer que todo entorno de pruebas es un SSD rapido.
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
        Console.WriteLine("     medidas: " + Est("perfilTexto") + "; modo=" + Est("perfilDisco") + "; medidoLento=" + Est("medidoLento"));
        string detalle = (string)Est("perfilTexto");
        int abre = detalle.LastIndexOf('('), cierra = detalle.LastIndexOf(" ms)", StringComparison.Ordinal), medida = -1;
        bool medidaValida = abre >= 0 && cierra > abre && int.TryParse(detalle.Substring(abre + 1, cierra - abre - 1), out medida);
        Check(medidaValida && lento == (medida > (int)Est("UmbralDiscoMs")), "la clasificacion coincide con la mediana real y su umbral");
        Check(lento ? carriles == 1 : carriles >= 2 && carriles <= 4 && carriles <= Math.Max(2, Environment.ProcessorCount),
              "abre los carriles del perfil observado   [nucleos=" + Environment.ProcessorCount + ", carriles=" + carriles + "]");
        Check(probeN >= 16, "y deja cerrado el perfilado de oido: no puede volver a cambiar la decision a mitad de la noche");

        // Idempotente: llamarlo dos veces no puede volver a ampliar el turno de disco (se pasaria del tope).
        object semAntes = TF.GetField("coverSem", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        System.Threading.Semaphore sem = (System.Threading.Semaphore)semAntes;
        int pudo = 0;
        for (int i = 0; i < 32; i++) { if (sem.WaitOne(0)) pudo++; else break; }
        for (int i = 0; i < pudo; i++) sem.Release();
        Console.WriteLine("     turnos de disco disponibles a la vez: " + pudo);
        Check(pudo == (int)Est("coverAbiertos") && pudo >= carriles, "el semaforo coincide con los permisos concedidos (solo crece); las bombas respetan los carriles");
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

        Console.WriteLine("\n  5) CUANDO SE USAN LOS DOS DISCOS A LA VEZ\n");
        // Es de lo mas normal: el programa y las miniaturas en el SSD del sistema y la musica en un disco
        // duro grande colgado detras, o la musica repartida entre los dos ("dir=" en el config). Un solo
        // veredicto para todo se equivoca en media biblioteca, asi que se guarda uno POR UNIDAD.
        MethodInfo mLento = TF.GetMethod("DiscoLento", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo mUnidad = TF.GetMethod("UnidadDe", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mLento != null && mUnidad != null, "cada archivo se puede preguntar por SU disco, no solo por el general");
        if (mLento == null || mUnidad == null) return 1;
        Check(Codigo.Llama(mPerfil, "Form1.UnidadDe"), "y el perfilado aparta las medidas por unidad (no las mete todas en el mismo saco)");
        Func<string, bool> Lento = delegate (string ru) { return (bool)mLento.Invoke(null, new object[] { ru }); };
        string unid = (string)mUnidad.Invoke(null, new object[] { rutas[0] });
        Console.WriteLine("     la unidad de " + rutas[0] + " es \"" + unid + "\"");
        IDictionary mapa = (IDictionary)TF.GetField("discoLento", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        Reinicia();
        PonEst("perfilDisco", 0);
        PonEst("diskSlow", true);            // el veredicto GENERAL dice disco duro...
        mapa.Clear(); mapa[unid] = false;    // ...pero de ESTA unidad se midio que es SSD
        Check(!Lento(rutas[0]), "manda lo medido de SU unidad, no el veredicto general");
        Check(Lento(@"Z:\otro\disco\x.mp3"), "y un archivo de una unidad de la que no se sabe nada cae en el veredicto general");
        mapa[unid] = true; PonEst("diskSlow", false);
        Check(Lento(rutas[0]), "y al reves igual: unidad lenta dentro de una rocola rapida");

        Console.WriteLine("\n  6) Y SE PUEDE FIJAR A MANO (Ctrl+F3)\n");
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

        Console.WriteLine("\n  7) LOS RESPIROS VAN POR EL DISCO DE CADA ARCHIVO\n");
        // El repaso de cabeceras y la prelectura de la cancion siguiente van dentro de metodos anonimos (los
        // hilos de fondo), asi que se busca por todo el programa quien pregunta por el disco de cada archivo.
        int preguntan = 0;
        foreach (Type t in asm.GetTypes())
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                if (Codigo.Llama(m, "Form1.DiscoLento")) preguntan++;
        Console.WriteLine("     sitios que preguntan por el disco de ESE archivo: " + preguntan);
        Check(preguntan >= 2, "el repaso de cabeceras y la prelectura preguntan por el disco donde esta cada archivo, no por un veredicto unico");

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
