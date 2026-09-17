using System;
using System.Collections.Generic;
using System.Reflection;

// "TU CANCION ES LA 3a, EN ~11 MINUTOS".
//
// La pastilla de la cola dice cuantas canciones van pedidas. Eso es un dato del LOCAL, no del cliente:
// al que acaba de meter su moneda no le sirve saber que hay cuatro, le sirve saber CUAL ES LA SUYA y
// cuanto falta. Y eso hoy no esta en ninguna parte de la pantalla.
//
// No es un detalle bonito: es la causa de un problema de verdad del bar. Alguien pide su cancion, no
// pasa nada visible que le diga que entro, y vuelve a picarle al boton -a veces gastando otro credito-
// creyendo que la maquina no le hizo caso.
//
// Y LO MEJOR ES QUE LA ROCOLA YA LO SABE. RefrescaCola mantiene 'colaPos' -que puesto ocupa cada
// cancion- y el pre-analisis dejo apuntada la duracion de cada archivo. Solo hay que sumar y decirlo.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE LA CUENTA SE PUEDA COMPROBAR. La suma vive en una funcion PURA -recibe numeros y devuelve un
//      numero, sin tocar pantalla ni cola-, igual que ProponeEscalones en el medidor. Asi se le pueden
//      dar casos a mano y ver si contesta bien, que con algo metido dentro de la pantalla no se podria.
//
//   2) QUE NO INVENTE. Si de alguna cancion de la cola no se sabe la duracion -aun no la ha analizado-
//      la cuenta NO vale, y entonces se dice el puesto Y SE CALLA EL TIEMPO. Un "en ~11 min" que luego
//      son veinte es peor que no decir nada: la proxima vez ya nadie se lo cree.
//
//   3) QUE EL TIEMPO SALGA DE LO YA MEDIDO Y NO DEL ARCHIVO. Preguntarle la duracion al descompresor
//      obliga a RECORRER EL MP3 ENTERO cuando no trae cabecera Xing: 3.87 ms de procesador en una
//      cancion de 4 minutos, y la lectura de sus 9 MB del disco. Multiplicado por toda la cola, en el
//      momento en que alguien acaba de pulsar un boton. La duracion ya esta apuntada: se lee de ahi.
class Turno
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF;

    static MethodBase Met(string n)
    {
        if (TF == null) return null;
        foreach (MethodInfo m in TF.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }

    static double Espera(double[] antes, double restan)
    {
        MethodInfo m = (MethodInfo)Met("EsperaSegundos");
        return Convert.ToDouble(m.Invoke(null, new object[] { antes, restan }));
    }
    static string Texto(int puesto, double segs)
    {
        MethodInfo m = (MethodInfo)Met("TextoTurno");
        return (string)m.Invoke(null, new object[] { puesto, segs });
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes()) if (t.Name == "Form1") TF = t;
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }

        Console.WriteLine("\n  1) LAS PIEZAS, Y QUE SE PUEDAN COMPROBAR\n");
        MethodBase esp = Met("EsperaSegundos"), txt = Met("TextoTurno"), segs = Met("SegundosDe");
        Check(esp != null, "la cuenta de cuanto falta esta APARTE y se le pueden dar numeros (EsperaSegundos)");
        Check(txt != null, "y el letrero que lo dice, tambien (TextoTurno)");
        Check(segs != null, "hay un solo sitio que sabe cuanto dura una cancion (SegundosDe)");
        if (esp == null || txt == null || segs == null)
        {
            Console.WriteLine("\n  " + fallos + " FALLO(S)");
            return fallos;
        }
        Check(esp.IsStatic && txt.IsStatic, "las dos son puras: no tocan la pantalla ni la cola, asi que se pueden probar");

        Console.WriteLine("\n  2) LA CUENTA\n");
        // Tres canciones por delante y a la que suena le quedan 45 s.
        double e = Espera(new double[] { 180, 200, 190 }, 45);
        Console.WriteLine("     3 canciones (180+200+190) + 45 s de la que suena  ->  " + e.ToString("0") + " s");
        Check(Math.Abs(e - 615) < 0.5, "suma lo que va por delante mas lo que le queda a la que suena");
        Check(Math.Abs(Espera(new double[0], 45) - 45) < 0.5, "si eres el siguiente, falta lo que le queda a la que suena");
        Check(Math.Abs(Espera(new double[0], 0) - 0) < 0.5, "y con la rocola callada, no falta nada");

        Console.WriteLine("\n  3) Y NO INVENTA\n");
        // Una cancion recien copiada al disco todavia no esta analizada: no se sabe cuanto dura. La
        // cuenta entera deja de valer, y hay que DECIR QUE NO SE SABE en vez de dar un numero flojo.
        Check(Espera(new double[] { 180, 0, 190 }, 45) < 0, "si de alguna no se sabe la duracion, la cuenta NO vale (devuelve -1)");
        Check(Espera(new double[] { 180, -3, 190 }, 45) < 0, "ni con una duracion absurda");
        Check(Espera(new double[] { 180, 200 }, -1) < 0, "ni si no se sabe cuanto le queda a la que suena");

        Console.WriteLine("\n  4) EL LETRERO\n");
        string t3 = Texto(3, 615), t1 = Texto(1, 45), tNo = Texto(3, -1), tYa = Texto(1, 20);
        Console.WriteLine("     puesto 3, faltan 615 s   ->  \"" + t3 + "\"");
        Console.WriteLine("     puesto 1, faltan 45 s    ->  \"" + t1 + "\"");
        Console.WriteLine("     puesto 3, no se sabe     ->  \"" + tNo + "\"");
        Console.WriteLine("     puesto 1, faltan 20 s    ->  \"" + tYa + "\"");
        Check(t3.Contains("3"), "dice QUE PUESTO ocupa la tuya");
        Check(t3.Contains("10") || t3.Contains("11"), "y cuantos minutos faltan (615 s son unos 10)");
        Check(t1.IndexOf("siguiente", StringComparison.OrdinalIgnoreCase) >= 0,
              "si eres el proximo lo dice con palabras, no con un \"1o\"");
        // ESTA ES LA IMPORTANTE. Un tiempo inventado que luego no se cumple hace que nadie vuelva a
        // creerle a la maquina; decir solo el puesto es honesto y sigue sirviendo.
        Check(!tNo.Contains("min"), "sin saber la duracion NO dice minutos: dice el puesto y se calla");
        Check(tNo.Contains("3"), "pero el puesto si lo dice");
        Check(!tYa.Contains("0 min"), "menos de un minuto no se dice \"0 min\"");
        Check(Texto(0, 100) == "", "y sin puesto no dice nada");

        Console.WriteLine("\n  5) EL TIEMPO SALE DE LO YA MEDIDO, NO DEL ARCHIVO\n");
        int lee, esc;
        Codigo.UsosDeCampo(segs, "durCache", out lee, out esc);
        Check(lee > 0, "la duracion se lee de lo que el pre-analisis dejo apuntado (durCache)");
        Codigo.UsosDeCampo(segs, "durFast", out lee, out esc);
        Check(lee > 0, "y si aun no esta, de la del barrido rapido (durFast)");
        // Preguntarle al descompresor recorre el MP3 entero cuando no trae cabecera Xing. Multiplicado
        // por toda la cola, justo cuando alguien acaba de pulsar un boton.
        Check(!Codigo.Llama(segs, "get_TotalTime"), "y NUNCA se le pregunta al archivo (recorreria el MP3 entero)");

        Console.WriteLine("\n  6) Y AL PONER LA CANCION, TAMPOCO SE LE PREGUNTA AL ARCHIVO\n");
        // Al arrancar cada cancion la rocola necesita saber cuanto dura, para armar el fundido de salida.
        // Se lo preguntaba AL ARCHIVO, y muchos MP3 no lo traen escrito: contestar obliga a recorrerlo
        // entero -3.87 ms y nueve megas de disco en una cancion de cuatro minutos-, y eso pasa en el hilo
        // de la pantalla, en el instante exacto en que el cliente acaba de soltar el boton. Y la duracion
        // ya esta apuntada desde el pre-analisis: se pregunta ahi.
        MethodBase pone = Met("StartOpenedTrack");
        Check(pone != null, "hay un sitio por donde arranca una cancion ya abierta (StartOpenedTrack)");
        if (pone != null)
        {
            Check(Codigo.Llama(pone, "SegundosDe"), "al arrancar una cancion, la duracion sale de lo ya apuntado");
            // El camino largo TIENE que seguir estando: una cancion recien copiada al disco aun no esta
            // medida y de algun lado hay que sacar su duracion. Lo que se comprueba es el ORDEN: que se
            // pregunte a lo apuntado ANTES que al archivo, para que el archivo sea el ultimo recurso y no
            // el primero.
            List<string> pasos = Codigo.Llamadas(pone);
            int iApunte = -1, iArchivo = -1;
            for (int i = 0; i < pasos.Count; i++)
            {
                string q = pasos[i];
                int pt = q.LastIndexOf('.');
                if (pt >= 0) q = q.Substring(pt + 1);
                if (iApunte < 0 && q == "SegundosDe") iApunte = i;
                if (iArchivo < 0 && q == "get_TotalTime") iArchivo = i;
            }
            Check(iArchivo >= 0, "y sigue habiendo camino largo para una cancion que aun no se ha medido");
            Check(iApunte >= 0 && (iArchivo < 0 || iApunte < iArchivo),
                  "pero primero se pregunta a lo apuntado, y al archivo solo si eso no sabe");
        }

        Console.WriteLine("\n  7) Y EL AVISO LO USA\n");
        MethodBase aviso = Met("ShowSongAddedToast");
        Check(aviso != null, "hay un aviso de cancion agregada");
        if (aviso != null)
            Check(Codigo.Llama(aviso, "TextoTurno"), "y dice el turno, no solo \"Cancion agregada\"");

        // LA CUENTA SE HACE AL METER LA CANCION, NO AL ENSEÑAR EL AVISO, y esto no es un detalle de
        // gusto: unas lineas despues de meterla, si no habia nada sonando, PlayNext saca la primera de
        // la cola y todos los puestos se corren. Contando en el aviso, la cuenta saldria de una cola que
        // ya no es la que el cliente acaba de ver.
        MethodBase mete = Met("AddItem");
        Check(mete != null, "hay un sitio por donde entra una cancion a la cola (AddItem)");
        if (mete != null)
        {
            Check(Codigo.Llama(mete, "EsperaSegundos"), "y la cuenta se hace ALLI, con la cola tal como quedo");
            Check(Codigo.Llama(mete, "SegundosDe"), "sumando lo que dura cada una de las que van por delante");
        }

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
