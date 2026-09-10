using System;
using System.Collections.Generic;
using System.Reflection;

// LA PAUSA DEL VIGIA DE PANTALLA ES ENCENDIDA O APAGADA, SIN RELOJ.
//
// La rocola tiene que estar SIEMPRE al frente: si cualquier ventana le roba el foco, un vigia se lo
// devuelve cada segundo y medio. F10 pausa ese vigia para poder dar mantenimiento con teclado y raton
// sin que la rocola te devuelva a la fuerza.
//
// Esa pausa se reactivaba SOLA a la media hora, "por si se olvida". Y eso mordia justo cuando hacia
// falta: en mitad de un mantenimiento largo la rocola volvia a robar el foco sin avisar, con las manos
// dentro de otra ventana. Ahora la pausa se queda como se dejo hasta que se vuelva a pulsar F10.
//
// Quitar un limite de tiempo tiene su precio, asi que aqui se vigilan las dos mitades:
//   1. que NADIE devuelva el vigia por su cuenta -ni el reloj del propio vigia-, y
//   2. que la RED siga puesta: arrancar la rocola siempre lo enciende, para que una pausa olvidada no
//      sobreviva a un reinicio. Sin esa segunda mitad, esto no seria un mando: seria una trampa.
class Vigia
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos;
    static void Bien(string q) { Console.WriteLine("  OK    " + q); }
    static void Mal(string q) { Console.WriteLine("  FALLO " + q); fallos++; }
    static void Exige(bool c, string q) { if (c) Bien(q); else Mal(q); }

    static void Usos(Type tf, string metodo, string campo, out int lee, out int escribe)
    {
        lee = 0; escribe = 0;
        foreach (MethodBase m in Codigo.MetodosConAyudantes(tf, metodo))
        {
            int l, e; Codigo.UsosDeCampo(m, campo, out l, out e);
            lee += l; escribe += e;
        }
    }

    static int Main(string[] argv)
    {
        Assembly a = Assembly.LoadFrom((argv.Length > 0) ? argv[0] : "rocola.exe");
        Type tf = a.GetType("Rocola.Form1");
        if (tf == null) { Mal("no encuentro Rocola.Form1"); return 1; }

        // --- 1) NO QUEDA NINGUN RELOJ DE CADUCIDAD ---
        // Mientras exista el campo, alguien puede volver a colgarle una fecha sin querer.
        bool hayReloj = false;
        foreach (FieldInfo f in tf.GetFields(TODO))
            if (f.Name == "maintUntil") hayReloj = true;
        Exige(!hayReloj, "no queda el campo de 'hasta cuando' de la pausa: ya no hay media hora que valga");

        FieldInfo maint = tf.GetField("maintMode", TODO);
        Exige(maint != null, "sigue existiendo el interruptor de la pausa");
        if (maint == null) return fallos;
        Exige(maint.FieldType == typeof(bool), "y es un si/no, no una fecha");

        // --- 2) EL VIGIA NO SE DEVUELVE A SI MISMO ---
        // Su reloj corre cada segundo y medio. Si escribiera el interruptor, la pausa tendria limite otra
        // vez, se llame como se llame el limite. Solo puede LEERLO para saber si le toca callarse.
        int lee, escribe;
        Usos(tf, "FocusTimer_Tick", "maintMode", out lee, out escribe);
        Exige(lee > 0, "el vigia mira la pausa antes de robar el foco (" + lee + " lecturas)");
        Exige(escribe == 0, "y NO la apaga por su cuenta: la pausa no caduca sola (" + escribe + " escrituras)");

        // --- 3) F10 SIGUE SIENDO EL INTERRUPTOR ---
        Usos(tf, "ProcessCmdKey", "maintMode", out lee, out escribe);
        Exige(lee > 0 && escribe > 0, "F10 sigue encendiendo y apagando la pausa");

        // --- 4) LA RED: ARRANCAR SIEMPRE ENCIENDE EL VIGIA ---
        // Es lo unico que impide que una pausa olvidada se quede puesta para siempre.
        Usos(tf, "OnShown", "maintMode", out lee, out escribe);
        Exige(escribe > 0, "al arrancar, la rocola enciende el vigia pase lo que pase");

        // --- 5) LOS DOS DIALOGOS DE MANTENIMIENTO DEVUELVEN EL VIGIA AL CERRARSE ---
        // Se pausan solos para poder quedarse al frente, y tienen que dejarlo como estaba. Antes se
        // apoyaban en el reloj de diez minutos; sin reloj, devolverlo es obligatorio.
        string[] dialogos = { "ShowMusicDirManager", "VentanaAjustes" };
        foreach (string d in dialogos)
        {
            Usos(tf, d, "maintMode", out lee, out escribe);
            Exige(lee > 0 && escribe > 0, d + " se pausa el vigia y lo deja como estaba al cerrarse");
        }

        // --- 6) EL AVISO NO PROMETE UNA MEDIA HORA QUE YA NO EXISTE ---
        // Un cartel que miente es peor que no tener cartel: el del bar se iria pensando que se arregla solo.
        bool prometeMinutos = false, diceComoVolver = false;
        foreach (MethodBase m in Codigo.MetodosConAyudantes(tf, "ProcessCmdKey"))
            foreach (string t in Codigo.Textos(m))
            {
                if (t.IndexOf("vigia pausado", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (t.IndexOf("min", StringComparison.OrdinalIgnoreCase) >= 0) prometeMinutos = true;
                if (t.IndexOf("F10", StringComparison.OrdinalIgnoreCase) >= 0) diceComoVolver = true;
            }
        Exige(!prometeMinutos, "el aviso ya no promete unos minutos que no van a pasar");
        Exige(diceComoVolver, "y dice con que tecla se devuelve, que ahora es la unica forma");

        Console.WriteLine(fallos == 0 ? "  todo bien" : ("  " + fallos + " fallos"));
        return fallos;
    }
}
