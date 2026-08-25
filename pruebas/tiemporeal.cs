using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

// QUE LA MUSICA NO SE CORTE, Y QUE SE SEPA SI SE CORTA.
//
// Cuando la tarjeta de sonido se queda sin muestras -la maquina se distrajo leyendo la biblioteca,
// decodificando una portada- el sonido da un tropiezo: un instante de silencio o un chasquido. La
// rocola se recupera y sigue, que esta bien. Pero hasta ahora NADIE LOS CONTABA: la rocola podia
// llevar meses cortandose en el bar y no habia forma de saberlo, ni de saber si algo lo mejoro.
//
// Y el hilo que le da de comer a la tarjeta pedia "prioridad alta" con las palabras de Windows
// (ThreadPriority.AboveNormal), que en Linux se traducen a "nice" y para sonido no sirven de nada: el
// planificador puede dejarlo esperando mientras el dibujado o el disco trabajan.
//
// En Linux se puede pedir de verdad: SCHED_FIFO, que quiere decir "a este hilo se le atiende ANTES
// que a los demas". Normalmente hace falta permiso especial; en Puppy la rocola corre como root, asi
// que se puede. Es la mejora mas valiosa para un bar: un corte de sonido es lo unico que de verdad
// se nota.
//
// Aqui se comprueban las dos cosas: que el contador esta enchufado donde se recupera de un tropiezo,
// y que la prioridad de tiempo real se pide DE VERDAD y el sistema la concede.
class TiempoReal
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    // Lo que usa la rocola por dentro, para poder preguntarle al sistema que quedo puesto.
    [StructLayout(LayoutKind.Sequential)] struct SchedParam { public int prioridad; }
    [DllImport("libc", SetLastError = true)] static extern IntPtr pthread_self();
    [DllImport("libc", SetLastError = true)] static extern int pthread_getschedparam(IntPtr hilo, out int politica, out SchedParam p);
    const int SCHED_OTHER = 0, SCHED_FIFO = 1;

    static Type TSal;

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TSal = asm.GetType("Rocola.SalidaAlsa", false);
        Check(TSal != null, "la salida de sonido de Linux esta donde tiene que estar");
        if (TSal == null) { Console.WriteLine("\n  1 FALLO(S)\n"); return 1; }

        Console.WriteLine("\n  1) LOS TROPIEZOS SE CUENTAN\n");
        PropertyInfo pTrop = TSal.GetProperty("Tropiezos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Check(pTrop != null, "la salida lleva la cuenta de los tropiezos");

        // Y esta enchufado DONDE SE RECUPERA. Si el contador existiera pero nadie lo subiera, marcaria
        // cero para siempre y seria peor que no tenerlo: diria que todo va bien pase lo que pase.
        MethodBase quien = QuienSeRecupera();
        Check(quien != null, "hay un sitio que se recupera cuando la tarjeta se queda seca");
        if (quien != null)
        {
            int lee, escribe;
            Codigo.UsosDeCampo(quien, "tropiezos", out lee, out escribe);
            Console.WriteLine("     se recupera en " + quien.Name + ", y ahi el contador se toca " + escribe + " vez/veces");
            Check(escribe > 0, "y ahi mismo sube el contador (si no, marcaria cero para siempre)");
        }

        Console.WriteLine("\n  2) Y SE PUEDEN VER  (un numero escondido no sirve de nada)\n");
        // Se busca quien LEE el contador, no una palabra concreta del texto: asi cambiar como esta
        // redactado el aviso no rompe la prueba, pero quitar el aviso si.
        Type TF = asm.GetType("Rocola.Form1", true);
        string loEnsena = null;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            if (Codigo.Llama(m, "get_Tropiezos")) { loEnsena = m.DeclaringType.Name + "." + m.Name; break; }
        if (loEnsena != null) Console.WriteLine("     los lee " + loEnsena + " para ensenarlos");
        Check(loEnsena != null, "la rocola lee el contador para ensenarlo (Ctrl+F3)");

        // Y tambien si la prioridad de tiempo real quedo puesta: prometerla y no decir cuando falla
        // seria peor que no prometerla.
        string diceRt = null;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            if (Codigo.Llama(m, "get_EnTiempoReal")) { diceRt = m.DeclaringType.Name + "." + m.Name; break; }
        Check(diceRt != null, "y dice si el sonido consiguio la prioridad de tiempo real o no");

        Console.WriteLine("\n  3) LA PRIORIDAD DE TIEMPO REAL SE PIDE DE VERDAD\n");
        MethodInfo mPide = TSal.GetMethod("PideTiempoReal", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Check(mPide != null, "la salida sabe pedir prioridad de tiempo real");
        if (mPide == null) { Console.WriteLine("\n  " + fallos + " FALLO(S)\n"); return fallos; }

        // Se pide en ESTE hilo y se le pregunta al sistema que quedo puesto. Esto no es leer el codigo:
        // es el sistema operativo contestando.
        int pol0; SchedParam p0;
        pthread_getschedparam(pthread_self(), out pol0, out p0);
        Console.WriteLine("     antes de pedir:  politica " + Nombre(pol0) + ", prioridad " + p0.prioridad);
        bool concedido = (bool)mPide.Invoke(null, null);
        int pol1; SchedParam p1;
        pthread_getschedparam(pthread_self(), out pol1, out p1);
        Console.WriteLine("     despues:         politica " + Nombre(pol1) + ", prioridad " + p1.prioridad);
        Console.WriteLine("     la rocola dice que " + (concedido ? "SI se la concedieron" : "NO se la concedieron"));

        Check(pol0 == SCHED_OTHER, "se parte de la prioridad normal (si no, esto no probaria nada)");
        Check(concedido == (pol1 == SCHED_FIFO), "la rocola dice la VERDAD sobre si se la concedieron o no");
        if (concedido)
        {
            Check(pol1 == SCHED_FIFO, "el sistema puso el hilo en tiempo real de verdad");
            Check(p1.prioridad > 0 && p1.prioridad <= 50,
                  "con prioridad moderada (" + p1.prioridad + "): bastante para el sonido, no tanta como para dejar el equipo tieso");
        }
        else Console.WriteLine("     (esta maquina no la concede; la rocola sigue como antes y lo dice)");

        Console.WriteLine("\n  4) Y SE PIDE EN EL HILO QUE DA DE COMER A LA TARJETA\n");
        // Pedirla en el hilo equivocado no serviria de nada: la prioridad es de cada hilo, no del programa.
        MethodBase bombea = TSal.GetMethod("Bombea", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(bombea != null, "el hilo que da de comer a la tarjeta sigue donde estaba");
        if (bombea != null)
        {
            bool laPide = Codigo.Llama(bombea, "PideTiempoReal");
            Console.WriteLine("     " + bombea.Name + " la pide: " + (laPide ? "si" : "NO"));
            Check(laPide, "la pide el hilo del sonido, que es el que la necesita");
        }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }

    static string Nombre(int p) { return (p == SCHED_FIFO) ? "tiempo real (FIFO)" : (p == SCHED_OTHER ? "normal" : p.ToString()); }

    // Quien se recupera: el metodo que llama a snd_pcm_recover.
    static MethodBase QuienSeRecupera()
    {
        foreach (MethodBase m in Codigo.TodosLosMetodos(TSal))
            if (Codigo.Llama(m, "snd_pcm_recover")) return m;
        return null;
    }
}
