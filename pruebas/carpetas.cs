using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;

// DECIRLE A LA ROCOLA DONDE ESTA LA MUSICA, INCLUIDO "EN ESTE DISCO ENTERO".
//
// La rocola escanea la carpeta "musica" que tiene al lado, y con Ctrl+F2 se le pueden agregar otras, que
// pueden estar en otros discos. Al agregar una se comprueba que no se solape con las que ya estan, y el
// motivo es bueno: una carpeta dentro de otra se escanearia dos veces y cada cancion saldria duplicada
// en las listas, el buscador y las estadisticas.
//
// PERO ESA COMPROBACION IMPEDIA LO MAS NORMAL: elegir el disco entero. El disco SIEMPRE contiene a la
// carpeta "musica" del programa -el programa esta en ese disco-, asi que siempre contestaba "esa carpeta
// ya esta incluida"... incluso cuando la carpeta "musica" NO EXISTE, que es justo el caso de una rocola
// recien puesta. Y la regla estaba al reves: cuando la carpeta nueva CONTIENE a otra que ya estaba, lo
// que hay que hacer no es rechazarla, es quedarse con la de fuera y soltar la de dentro.
//
// Y HABIA ALGO PEOR DETRAS. Apuntar al disco entero colgaba la rocola para siempre: el recorrido se
// metia en las carpetas de pega del sistema (/proc, /sys, /dev...), que no tienen musica y son la
// inmensa mayoria del trabajo, y sobre todo seguia ATAJOS QUE DAN VUELTAS -/proc/<numero>/root lleva de
// vuelta a la raiz, /usr/bin/X11 lleva a si mismo- y no salia nunca. Medido antes del arreglo: 40.000
// carpetas y 401.023 archivos en 19 segundos SIN TERMINAR.
class Carpetas
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF;
    static object F;
    static object Campo(string n) { return TF.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(F); }
    static void PonCampo(string n, object v) { TF.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(F, v); }

    static void Cancion(string dir, string nombre)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, nombre), new byte[2048]);
    }
    // Un atajo de carpeta (en Linux, un enlace simbolico).
    static bool Atajo(string desde, string hacia)
    {
        try
        {
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("ln", "-sfn \"" + hacia + "\" \"" + desde + "\"");
            psi.UseShellExecute = false; psi.RedirectStandardError = true;
            System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
            p.WaitForExit(5000);
            return Directory.Exists(desde);
        }
        catch { return false; }
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        F = FormatterServices.GetUninitializedObject(TF);

        MethodInfo mPorQue = TF.GetMethod("PorQueNoSePuedeAgregar", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo mRaices = TF.GetMethod("AllMusicRoots", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo mScan = TF.GetMethod("ScanFolders", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fCarpeta = TF.GetField("carpetaDatos", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mRaices != null && mScan != null && fCarpeta != null, "las piezas de las carpetas de musica estan donde tienen que estar");
        // Sin esto la decision vive dentro del boton de la ventana, donde ninguna prueba la alcanza.
        Check(mPorQue != null, "el motivo por el que una carpeta no se puede agregar se puede preguntar aparte de la ventana");
        if (mRaices == null || mScan == null || fCarpeta == null) { Console.WriteLine("\n  " + Math.Max(1, fallos) + " FALLO(S)\n"); return Math.Max(1, fallos); }

        string casa = Path.Combine(Path.GetTempPath(), "rocola_carp_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(casa);
        fCarpeta.SetValue(null, null);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", casa);
        List<string> extras = new List<string>();
        PonCampo("extraMusicDirs", extras);

        Func<string, string> PorQue = delegate (string ru)
        { return (mPorQue == null) ? "(no existe)" : (string)mPorQue.Invoke(F, new object[] { ru }); };
        Func<List<string>> Raices = delegate ()
        {
            List<string> r = new List<string>();
            foreach (string x in (System.Collections.IEnumerable)mRaices.Invoke(F, null)) r.Add(x);
            return r;
        };

        try
        {
            string discoEntero = casa;                       // hace de "disco principal": contiene al programa
            string musica = Path.Combine(casa, "musica");    // la carpeta fija del programa

            Console.WriteLine("\n  1) EL DISCO ENTERO SE PUEDE ELEGIR (no hay carpeta 'musica')\n");
            extras.Clear();
            Console.WriteLine("     la carpeta 'musica' del programa seria " + musica + "  ->  " + (Directory.Exists(musica) ? "existe" : "NO existe"));
            string motivo = PorQue(discoEntero);
            Console.WriteLine("     elegir " + discoEntero + "  ->  " + (motivo == null ? "se puede" : "\"" + motivo + "\""));
            Check(motivo == null, "se puede elegir el disco donde esta el programa cuando no hay carpeta 'musica' (no hay nada con que chocar)");

            Console.WriteLine("\n  2) Y TAMBIEN CON LA CARPETA 'musica' PUESTA: MANDA LA DE FUERA\n");
            Cancion(Path.Combine(musica, "Disco 1"), "a.mp3");
            extras.Clear();
            motivo = PorQue(discoEntero);
            Console.WriteLine("     elegir " + discoEntero + " teniendo 'musica' dentro  ->  " + (motivo == null ? "se puede" : "\"" + motivo + "\""));
            Check(motivo == null, "elegir una carpeta que CONTIENE a otra que ya estaba se puede: la de fuera ya la cubre");
            extras.Clear(); extras.Add(discoEntero);
            List<string> r2 = Raices();
            Console.WriteLine("     lo que se escanea: " + string.Join(", ", r2.ToArray()));
            Check(r2.Count == 1 && r2[0] == discoEntero, "y se escanea SOLO la de fuera: la de dentro se suelta (si no, cada cancion saldria dos veces)");

            Console.WriteLine("\n  3) LO QUE SI SE SIGUE RECHAZANDO\n");
            extras.Clear();
            Check(PorQue(musica) != null, "la carpeta 'musica' otra vez: ya esta");
            Check(PorQue(Path.Combine(musica, "Disco 1")) != null, "y una carpeta METIDA DENTRO de otra que ya esta");
            extras.Clear(); extras.Add(Path.Combine(casa, "otra"));
            Directory.CreateDirectory(Path.Combine(casa, "otra"));
            Check(PorQue(Path.Combine(casa, "otra")) != null, "y una que ya se habia agregado antes");

            Console.WriteLine("\n  4) EL BARRIDO NO ENTRA EN LAS CARPETAS DE PEGA DEL SISTEMA\n");
            // /proc, /sys, /dev y compania no son discos: son inventos del nucleo. No hay que adivinar
            // nombres, el propio sistema dice de que tipo es cada sitio montado (tercera columna de
            // /proc/mounts). OJO: la raiz de Puppy es de tipo "aufs", que SI es un sitio de verdad; por eso
            // la lista es de tipos concretos y nunca al reves.
            // Cada sistema lo sabe a su manera -Linux dice de que TIPO es cada sitio montado; Windows va por
            // el nombre de las cuatro carpetas de siempre-, asi que la rocola lo pregunta en un solo sitio y
            // esta prueba comprueba las dos formas, cada una donde aplica.
            MethodInfo mEsSistema = TF.GetMethod("EsCarpetaDelSistema", BindingFlags.Static | BindingFlags.NonPublic);
            Check(mEsSistema != null, "la rocola sabe que carpetas son del sistema y no hay que mirar");
            Type TS = asm.GetType("Rocola.Sistema");
            MethodInfo mMentiraDe = (TS != null) ? TS.GetMethod("SistemasDeMentira", new Type[] { typeof(string) }) : null;
            MethodInfo mEsSistemaTabla = (TS != null) ? TS.GetMethod("EsCarpetaDelSistema", new Type[] { typeof(string), typeof(List<string>) }) : null;
            if (mMentiraDe != null && mEsSistemaTabla != null)
            {
                string tabla =
                    "unionfs / aufs rw 0 0\n" +
                    "proc /proc proc rw 0 0\n" +
                    "sysfs /sys sysfs rw 0 0\n" +
                    "devtmpfs /dev devtmpfs rw 0 0\n" +
                    "tmpfs /tmp tmpfs rw 0 0\n" +
                    "/dev/sdb1 /mnt/sdb1 vfat rw 0 0\n";
                List<string> mentira = (List<string>)mMentiraDe.Invoke(null, new object[] { tabla });
                Console.WriteLine("     en una maquina de Puppy de mentira, no son discos: " + string.Join(", ", mentira.ToArray()));
                Func<string, bool> EsSis = delegate (string ru) { return (bool)mEsSistemaTabla.Invoke(null, new object[] { ru, mentira }); };
                Check(EsSis("/proc/123/root"), "  /proc es del sistema");
                Check(EsSis("/sys/devices/x"), "  /sys tambien");
                Check(EsSis("/dev/shm/x"), "  /dev tambien");
                Check(!EsSis("/mnt/sdb1/musica/Disco 1"), "  el disco de la musica NO");
                Check(!EsSis("/mnt/home/rocola/musica"), "  ni la carpeta de la rocola");
                Check(!EsSis("/"), "  ni la raiz de Puppy, que es de tipo 'aufs' y ahi vive todo");
            }
            else
            {
                // La rocola de Windows. Aqui no hay tabla que preguntar: son las carpetas de siempre.
                Console.WriteLine("     esta rocola es de Windows: se va por el nombre de las carpetas de siempre");
                Check(mEsSistema != null && (bool)mEsSistema.Invoke(null, new object[] { @"C:\Windows" }), "  la carpeta de Windows es del sistema");
                Check(mEsSistema != null && (bool)mEsSistema.Invoke(null, new object[] { @"C:\Program Files" }), "  la de los programas tambien");
                Check(mEsSistema != null && (bool)mEsSistema.Invoke(null, new object[] { @"D:\$Recycle.Bin" }), "  y la papelera");
                Check(mEsSistema != null && !(bool)mEsSistema.Invoke(null, new object[] { @"D:\Musica\Disco 1" }), "  y una carpeta de musica NO");
            }
            if (mEsSistema != null)
            {
                Console.WriteLine("     y con las carpetas de verdad de esta maquina:");
                Check(!(bool)mEsSistema.Invoke(null, new object[] { casa }), "  la carpeta de la prueba no es del sistema");
                Check(!(bool)mEsSistema.Invoke(null, new object[] { Path.Combine(casa, "musica") }), "  ni la carpeta de musica");
            }

            Console.WriteLine("\n  5) Y NO SE QUEDA DANDO VUELTAS EN LOS ATAJOS\n");
            // Los dos casos que colgaron el recorrido de verdad: un atajo a si mismo (/usr/bin/X11) y uno
            // que lleva a la carpeta de arriba (/proc/<numero>/root -> /).
            string enredo = Path.Combine(casa, "enredo");
            Cancion(Path.Combine(enredo, "musica", "Disco 1"), "a.mp3");
            Cancion(Path.Combine(enredo, "musica", "Disco 2"), "b.mp3");
            bool hayAtajos = Atajo(Path.Combine(enredo, "vueltas"), enredo)
                           & Atajo(Path.Combine(enredo, "musica", "arriba"), enredo);
            if (!hayAtajos) Console.WriteLine("     (no se pudieron fabricar los atajos: se salta)");
            else
            {
                List<string> audio = new List<string>(), video = new List<string>();
                Dictionary<string, string> covers = new Dictionary<string, string>();
                Exception murio = null;
                Thread t = new Thread(delegate ()
                {
                    try { mScan.Invoke(F, new object[] { enredo, audio, video, covers }); }
                    catch (Exception e) { murio = e; }
                });
                t.IsBackground = true;   // si se queda dando vueltas, que no impida salir de la prueba
                t.Start();
                bool termino = t.Join(20000);
                Console.WriteLine("     con dos atajos que dan vueltas, el barrido " + (termino ? "TERMINO" : "SE QUEDO DANDO VUELTAS")
                                  + "   (encontro " + audio.Count + " canciones)");
                Check(termino, "el barrido termina aunque haya atajos que dan vueltas");
                if (termino)
                {
                    Check(murio == null, "y no revienta por el camino");
                    Check(audio.Count == 2, "y encuentra cada cancion UNA sola vez (ni de menos por saltarse cosas, ni de mas por dar vueltas)");
                }
            }

            Console.WriteLine("\n  6) SIN MUSICA, LA VENTANA SE ABRE SOLA\n");
            // Hoy solo se escribe un letrero que habla de un ".exe" que en Linux no existe y de una
            // combinacion de teclas que hay que saberse. Configurar la musica es mantenimiento -con teclado
            // y raton-, pero no puede hacer falta adivinar nada para llegar ahi.
            MethodInfo mLoad = TF.GetMethod("LoadMusicFolder", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo mPide = TF.GetMethod("PideElegirMusica", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(mLoad != null && mPide != null, "hay un sitio que pide elegir la musica cuando no hay ninguna");
            if (mLoad != null && mPide != null)
            {
                Check(Codigo.Llama(mLoad, "Form1.PideElegirMusica"), "y al no encontrar musica, se pide");
                // EL SEGURO CONTRA EL BUCLE: la ventana, al cerrarse, vuelve a cargar la musica. Si sigue sin
                // haber, no puede volver a abrirse sola o no se saldria nunca de ahi.
                FieldInfo fAbierto = TF.GetField("gestorAbierto", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo fOfrecido = TF.GetField("gestorOfrecido", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(fAbierto != null && fOfrecido != null, "y hay con que evitar que se abra una y otra vez");
                if (fAbierto != null && fOfrecido != null)
                {
                    fAbierto.SetValue(F, true); fOfrecido.SetValue(F, false);
                    mPide.Invoke(F, null);
                    Check(!(bool)fOfrecido.GetValue(F), "con la ventana ABIERTA no se pide otra vez (si no, se abriria sobre si misma)");
                    fAbierto.SetValue(F, false); fOfrecido.SetValue(F, false);
                    mPide.Invoke(F, null);
                    Check((bool)fOfrecido.GetValue(F), "sin musica y sin ventana abierta, se pide");
                    mPide.Invoke(F, null);
                    Check((bool)fOfrecido.GetValue(F), "y si ya se pidio una vez, no se insiste: se cierra una vez y se cierra");
                }
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROCOLA_DATOS", null);
            fCarpeta.SetValue(null, null);
            try { Directory.Delete(casa, true); } catch { }
        }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}
