using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Collections.Generic;

// EL TAMANO DE VENTANA DEL RECONSTRUCTOR DE AGUDOS, A MANO.
//
// El DSEE normal trabaja por trozos ("ventanas") de 2048 o 4096 muestras, y hasta ahora lo decidia
// sola la rocola: 4096 si el equipo tiene 4 nucleos y 3.5 GB, 2048 si no. El equipo del bar cae
// JUSTO en ese borde, asi que la rocola podia elegir una cosa u otra segun el dia, y no habia forma
// de fijarlo sin recompilar.
//
// Lo que costaba de verdad, medido en esta maquina con pruebas/dseeperf.exe:
//
//     ventana 2048 -> 8.76 ms de trabajo por cada segundo de musica
//     ventana 4096 -> 9.42 ms                     (0.66 ms mas, siete centesimas del 1% de un nucleo)
//
// O sea: practicamente lo mismo. Y no es casualidad. Al doblar la ventana cada FFT cuesta poco mas
// del doble, pero caben la mitad por segundo, asi que el trabajo total solo sube 12/11 = 9%. La
// ventana grande afina el doble en frecuencia y deja la costura mas limpia, y sale casi gratis.
//
// Esta bateria comprueba que el renglon "dsee_ventana=" de la configuracion MANDA sobre lo que la
// rocola habria elegido sola, en los dos sentidos, y que un valor con el que no se puede trabajar
// deja las cosas como estaban en vez de armar una ventana rota.
class Ventana
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF, TSbr;
    static FieldInfo fTier;
    static MethodInfo mLoad, mVent;

    // Una rocola sin estrenar: se fabrica el objeto SIN pasar por su constructor (que abriria ventanas
    // y leeria la biblioteca entera). Aqui solo interesan tres campos y dos metodos.
    static object RocolaCruda()
    {
        return FormatterServices.GetUninitializedObject(TF);
    }

    static string Config { get { return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "rocola_config.txt"); } }

    // Deja la configuracion con estos renglones, lee con ella y devuelve la ventana que sale.
    static int ConLaConfig(string texto, bool tierAlto)
    {
        object r = RocolaCruda();
        fTier.SetValue(r, tierAlto);
        File.WriteAllText(Config, texto);
        mLoad.Invoke(r, null);
        return (int)mVent.Invoke(r, null);
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        TSbr = TF.GetNestedType("SpectralTap", BindingFlags.NonPublic | BindingFlags.Public);
        fTier = TF.GetField("tierHd", BindingFlags.Instance | BindingFlags.NonPublic);
        mLoad = TF.GetMethod("LoadConfigPrefs", BindingFlags.Instance | BindingFlags.NonPublic);
        mVent = TF.GetMethod("VentanaDsee", BindingFlags.Instance | BindingFlags.NonPublic);

        Check(TSbr != null, "el reconstructor de agudos esta donde tiene que estar");
        Check(fTier != null && mLoad != null, "la rocola sigue leyendo su configuracion y midiendo el equipo");
        Check(mVent != null, "la rocola tiene UN sitio donde decide el tamano de ventana");
        if (TSbr == null || fTier == null || mLoad == null || mVent == null) return 1;

        string previo = File.Exists(Config) ? File.ReadAllText(Config) : null;
        try
        {
            Console.WriteLine("\n  1) SIN EL RENGLON, DECIDE LA ROCOLA  (si esto no pasa, el resto no prueba nada)\n");
            int autoAlto = ConLaConfig("normalizar=1\n", true);
            int autoBajo = ConLaConfig("normalizar=1\n", false);
            Console.WriteLine("     equipo potente -> " + autoAlto + "     equipo modesto -> " + autoBajo);
            Check(autoAlto == 4096, "en un equipo potente elige la ventana fina (4096)");
            Check(autoBajo == 2048, "en un equipo modesto elige la ventana normal (2048)");

            Console.WriteLine("\n  2) CON EL RENGLON, MANDA EL RENGLON  (en los dos sentidos)\n");
            int mandaBaja = ConLaConfig("dsee_ventana=2048\n", true);
            int mandaAlta = ConLaConfig("dsee_ventana=4096\n", false);
            Console.WriteLine("     equipo potente pidiendo 2048 -> " + mandaBaja);
            Console.WriteLine("     equipo modesto pidiendo 4096 -> " + mandaAlta);
            Check(mandaBaja == 2048, "pidiendo 2048 en un equipo potente, se respeta el 2048");
            Check(mandaAlta == 4096, "pidiendo 4096 en un equipo modesto, se respeta el 4096");

            Console.WriteLine("\n  3) UN VALOR CON EL QUE NO SE PUEDE TRABAJAR NO ROMPE NADA\n");
            int basura = ConLaConfig("dsee_ventana=pepino\n", true);
            int chico = ConLaConfig("dsee_ventana=333\n", false);
            Console.WriteLine("     \"pepino\" -> " + basura + "      333 -> " + chico);
            Check(basura == 4096, "un valor que no es un numero devuelve la decision a la rocola");
            Check(chico == 2048, "un numero que no sirve devuelve la decision a la rocola");

            Console.WriteLine("\n  4) ESE RENGLON NO ENCIENDE EL DSEE POR SU CUENTA\n");
            // "dsee_ventana=" empieza igual que "dsee=". Si el que lee la configuracion se confundiera,
            // poner el tamano de ventana encenderia el reconstructor sin que nadie lo pidiera.
            object r4 = RocolaCruda();
            fTier.SetValue(r4, true);
            FieldInfo fOn = TF.GetField("sbrOn", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo fAllow = TF.GetField("sbrAllowed", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(fOn != null && fAllow != null, "el interruptor del DSEE sigue donde estaba");
            if (fOn != null && fAllow != null)
            {
                fAllow.SetValue(r4, true);
                fOn.SetValue(r4, false);
                File.WriteAllText(Config, "dsee_ventana=4096\n");
                mLoad.Invoke(r4, null);
                Check((bool)fOn.GetValue(r4) == false, "poner el tamano de ventana NO enciende el reconstructor");
            }

            Console.WriteLine("\n  5) EL NUMERO LLEGA AL RECONSTRUCTOR DE VERDAD\n");
            // Que el numero salga bien no sirve de nada si luego nadie lo usa. Dos comprobaciones:
            // que un reconstructor armado con ese numero SALE con ese tamano, y que el sitio donde la
            // rocola lo arma para reproducir pasa por aqui y no por una cuenta suelta escrita aparte.
            foreach (int n in new int[] { 2048, 4096 })
            {
                object tap = Tap(n);
                int len = (tap == null) ? -1 : (int)TSbr.GetProperty("FrameLen").GetValue(tap, null);
                Console.WriteLine("     pidiendo " + n + " sale un reconstructor de " + len + " puntos");
                Check(len == n, "un reconstructor pedido de " + n + " sale de " + n);
            }

            MethodBase quien = QuienArmaElTap();
            Check(quien != null, "hay un sitio en la rocola que arma el reconstructor para reproducir");
            if (quien != null)
            {
                List<string> llamadas = Codigo.Llamadas(quien);
                Console.WriteLine("     lo arma " + quien.Name + ", y para el tamano llama a: "
                                  + (Codigo.Llama(llamadas, "VentanaDsee") ? "VentanaDsee" : "(a nadie: cuenta suelta)"));
                Check(Codigo.Llama(llamadas, "VentanaDsee"),
                      "ese sitio pregunta el tamano en el unico sitio donde se decide, no lo calcula aparte");
            }
        }
        finally
        {
            try { if (previo != null) File.WriteAllText(Config, previo); else File.Delete(Config); } catch { }
        }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }

    // Arma un reconstructor de n puntos con una fuente cualquiera, por el constructor que usa la rocola.
    static object Tap(int n)
    {
        foreach (ConstructorInfo ci in TSbr.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            ParameterInfo[] ps = ci.GetParameters();
            if (ps.Length != 2 || ps[1].ParameterType != typeof(int)) continue;
            if (ps[0].ParameterType == typeof(int)) continue;         // ese es el de solo analisis
            try { return ci.Invoke(new object[] { new FuenteV(), n }); } catch { }
        }
        return null;
    }

    // Quien arma el reconstructor para REPRODUCIR. Se busca por lo que HACE, no por como se llama, para
    // que cambiarle el nombre al metodo no deje esta prueba mirando al vacio y diciendo que todo va bien:
    // es el unico que arma un reconstructor Y ADEMAS guarda el que armo en "curSbr", que es el que suena.
    // (Hay otro sitio que arma reconstructores -el que mide las canciones de la biblioteca- y ese no vale:
    // no reproduce nada y usa el constructor de solo analisis, que ni siquiera lleva tamano.)
    static MethodBase QuienArmaElTap()
    {
        // La cadena de cada cancion se arma en ArmaCadena (la usan el arranque de siempre y el relevo sin
        // pausa): el reconstructor que suena se construye ahi y se guarda en la cadena, no en curSbr.
        MethodBase armaCadena = TF.GetMethod("ArmaCadena", BindingFlags.Instance | BindingFlags.NonPublic);
        if (armaCadena != null)
            foreach (string s in Codigo.Llamadas(armaCadena))
                if (s.IndexOf("SpectralTap", StringComparison.Ordinal) >= 0 && s.EndsWith(".ctor")) return armaCadena;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            if (m.DeclaringType == TSbr) continue;                    // el propio reconstructor no cuenta
            bool arma = false;
            foreach (string s in Codigo.Llamadas(m))
                if (s.IndexOf("SpectralTap", StringComparison.Ordinal) >= 0 && s.EndsWith(".ctor")) { arma = true; break; }
            if (!arma) continue;
            int lee, escribe;
            Codigo.UsosDeCampo(m, "curSbr", out lee, out escribe);
            if (escribe > 0) return m;
        }
        return null;
    }
}

// Una fuente de sonido de mentira: el reconstructor solo le pregunta el formato al construirse.
class FuenteV : NAudio.Wave.ISampleProvider
{
    private readonly NAudio.Wave.WaveFormat wf = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    public NAudio.Wave.WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c) { for (int i = 0; i < c; i++) b[o + i] = 0f; return c; }
}
