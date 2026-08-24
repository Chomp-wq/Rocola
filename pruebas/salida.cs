using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Collections.Generic;

// ELEGIR POR QUE TARJETA SUENA LA ROCOLA.
//
// Cuando la rocola encuentra un servidor de sonido metido entre ella y la tarjeta -PipeWire,
// PulseAudio, JACK-, lo avisa en Ctrl+F3 y dice que hacer:
//
//     "Pon salida=hw:0 en los ajustes para hablarle DIRECTO a la tarjeta."
//
// Y no se podia. Quien leia la configuracion solo aceptaba tres palabras -auto, wasapi, waveout, que
// son de la rocola de Windows- y tiraba cualquier otra cosa a la basura, asi que ese renglon no hacia
// NADA. El sitio que lo usa si esperaba un nombre de tarjeta, y su comentario lo decia; simplemente
// nunca le llegaba. O sea: la rocola daba un consejo que ella misma no dejaba seguir.
//
// Y ademas lo pasaba a minusculas, que en ALSA rompe los nombres de verdad: "hw:CARD=Generic" tiene
// mayusculas y sin ellas no existe.
//
// Aqui se comprueba que el renglon llega hasta el final, tal cual se escribio.
class Salida
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF;
    static MethodInfo mLoad, mTarjeta;
    static string Config { get { return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "rocola_config.txt"); } }

    // Deja la configuracion con estos renglones, lee con ella y devuelve la tarjeta que sale elegida.
    static string ConLaConfig(string texto)
    {
        object r = FormatterServices.GetUninitializedObject(TF);
        TF.GetField("outPref", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(r, "auto");
        File.WriteAllText(Config, texto);
        mLoad.Invoke(r, null);
        return (string)mTarjeta.Invoke(r, null);
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        mLoad = TF.GetMethod("LoadConfigPrefs", BindingFlags.Instance | BindingFlags.NonPublic);
        mTarjeta = TF.GetMethod("TarjetaDeSonido", BindingFlags.Instance | BindingFlags.NonPublic);

        Check(mLoad != null, "la rocola sigue leyendo su configuracion");
        Check(mTarjeta != null, "y tiene UN sitio donde decide por que tarjeta suena");
        if (mLoad == null || mTarjeta == null) { Console.WriteLine("\n  " + (fallos) + " FALLO(S)\n"); return Math.Max(1, fallos); }

        string previo = File.Exists(Config) ? File.ReadAllText(Config) : null;
        try
        {
            Console.WriteLine("\n  1) SIN PEDIR NADA, LA QUE USE EL SISTEMA  (si esto no pasa, el resto no prueba nada)\n");
            string sinNada = ConLaConfig("normalizar=1\n");
            Console.WriteLine("     sin renglon -> \"" + sinNada + "\"");
            Check(sinNada == "default", "sin pedir nada suena por la tarjeta que use el sistema");

            // Las tres palabras de la rocola de Windows siguen queriendo decir "la del sistema": una
            // configuracion traida de alli no manda la musica a una tarjeta que no existe.
            foreach (string v in new string[] { "auto", "wasapi", "waveout" })
            {
                string t = ConLaConfig("salida=" + v + "\n");
                Check(t == "default", "\"salida=" + v + "\" (de la rocola de Windows) sigue siendo la del sistema");
            }

            Console.WriteLine("\n  2) EL CONSEJO QUE DA LA ROCOLA SE PUEDE SEGUIR\n");
            // Esto es literal lo que la rocola escribe en pantalla cuando hay un servidor de sonido en medio.
            string hw0 = ConLaConfig("salida=hw:0\n");
            Console.WriteLine("     \"salida=hw:0\"  ->  \"" + hw0 + "\"");
            Check(hw0 == "hw:0", "poniendo salida=hw:0 la rocola le habla DIRECTO a esa tarjeta");

            string ph1 = ConLaConfig("salida=plughw:1\n");
            Console.WriteLine("     \"salida=plughw:1\"  ->  \"" + ph1 + "\"");
            Check(ph1 == "plughw:1", "y con dos tarjetas se puede elegir la que va al amplificador");

            Console.WriteLine("\n  3) EL NOMBRE LLEGA TAL CUAL, CON SUS MAYUSCULAS\n");
            // En ALSA las mayusculas cuentan: "hw:CARD=Generic" existe y "hw:card=generic" no. La rocola
            // pasaba el renglon a minusculas, asi que un nombre de esos se rompia por el camino.
            string may = ConLaConfig("salida=hw:CARD=Generic,DEV=0\n");
            Console.WriteLine("     \"salida=hw:CARD=Generic,DEV=0\"  ->  \"" + may + "\"");
            Check(may == "hw:CARD=Generic,DEV=0", "un nombre con mayusculas llega entero (en ALSA las mayusculas cuentan)");

            Console.WriteLine("\n  4) UN RENGLON VACIO NO DEJA A LA ROCOLA MUDA\n");
            string vacio = ConLaConfig("salida=\n");
            Console.WriteLine("     \"salida=\" (vacio)  ->  \"" + vacio + "\"");
            Check(vacio == "default", "un renglon vacio vuelve a la tarjeta del sistema, no a un nombre vacio");

            Console.WriteLine("\n  5) Y ESO ES LO QUE SE USA AL SONAR\n");
            // Que la decision salga bien no sirve de nada si luego quien abre la tarjeta hace su propia
            // cuenta aparte. Se busca por lo que HACE -el metodo que arma la salida de sonido- y se exige
            // que pregunte en el unico sitio donde se decide.
            MethodBase quien = QuienAbreLaTarjeta();
            Check(quien != null, "hay un sitio en la rocola que abre la tarjeta de sonido");
            if (quien != null)
            {
                List<string> ll = Codigo.Llamadas(quien);
                Console.WriteLine("     la abre " + quien.Name + ", y la tarjeta se la pregunta a: "
                                  + (Codigo.Llama(ll, "TarjetaDeSonido") ? "TarjetaDeSonido" : "(a nadie: cuenta suelta)"));
                Check(Codigo.Llama(ll, "TarjetaDeSonido"),
                      "quien abre la tarjeta pregunta en el unico sitio donde se decide, no lo calcula aparte");
            }
        }
        finally
        {
            try { if (previo != null) File.WriteAllText(Config, previo); else File.Delete(Config); } catch { }
        }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }

    // Quien abre la tarjeta de sonido: el metodo que construye la salida de ALSA.
    static MethodBase QuienAbreLaTarjeta()
    {
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            foreach (string s in Codigo.Llamadas(m))
                if (s.IndexOf("SalidaAlsa", StringComparison.Ordinal) >= 0 && s.EndsWith(".ctor")) return m;
        return null;
    }
}
