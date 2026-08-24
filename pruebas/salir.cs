using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Windows.Forms;

// COMO SE SALE DE LA ROCOLA CUANDO NO HAY ESCRITORIO DETRAS.
//
// En Windows la rocola se cierra con Alt+F4, y quien manda ese Alt+F4 es el gestor de ventanas -el
// programa que pone los bordes y el boton de cerrar-. En la maquina del bar la rocola arranca sola,
// sin escritorio, y ahi NO HAY NINGUNO: no habia ninguna tecla que la cerrara. Para meter musica
// nueva habria que reiniciar la maquina.
//
// Se sale con Ctrl+Alt+Q pulsado TRES VECES seguidas. Tres y no una porque en un bar la rocola esta
// al alcance de cualquiera: una sola combinacion se aprieta sin querer o por curiosear, y tres
// seguidas dentro de unos segundos no le pasan a nadie por accidente. Las dos primeras solo avisan
// en pantalla ("Salir de la rocola: 1 de 3"), asi que el que lo hizo sin querer ve lo que esta a
// punto de pasar y para.
//
// Y se sale por el camino de siempre, que es importante: cerrar A PROPOSITO borra el estado guardado
// y la proxima vez la rocola arranca LIMPIA. Solo un apagon -que no corre nada- deja el archivo, y
// por eso al volver la luz la rocola reanuda los creditos y la cola.
class Salir
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF;
    static MethodInfo mPiden;
    static object rocola;

    static int Pulsa(Keys k) { return (int)mPiden.Invoke(rocola, new object[] { k }); }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        mPiden = TF.GetMethod("PidenSalir", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mPiden != null, "la rocola tiene una forma de que le pidan salir");
        if (mPiden == null) { Console.WriteLine("\n  1 FALLO(S)\n"); return 1; }

        rocola = FormatterServices.GetUninitializedObject(TF);

        Console.WriteLine("\n  1) UNA SOLA VEZ NO CIERRA NADA\n");
        int a = Pulsa(Keys.Control | Keys.Alt | Keys.Q);
        Console.WriteLine("     Ctrl+Alt+Q una vez -> " + a + " de 3");
        Check(a == 1, "la primera vez cuenta pero NO cierra");

        int b = Pulsa(Keys.Control | Keys.Alt | Keys.Q);
        Console.WriteLine("     dos veces -> " + b + " de 3");
        Check(b == 2, "la segunda tampoco cierra");

        int c = Pulsa(Keys.Control | Keys.Alt | Keys.Q);
        Console.WriteLine("     tres veces -> " + c + " de 3");
        Check(c == 3, "a la tercera si");

        Console.WriteLine("\n  2) CUALQUIER OTRA TECLA NO CUENTA  (si contara, no probaria nada)\n");
        // Las teclas de la botonera del bar son las que mas se pulsan, todo el dia. Ninguna puede
        // acercar a la rocola a cerrarse.
        Keys[] otras = { Keys.Q, Keys.Control | Keys.Q, Keys.Alt | Keys.Q, Keys.Enter, Keys.Space,
                         Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Escape, Keys.F4, Keys.Alt | Keys.F4 };
        bool limpio = true;
        foreach (Keys k in otras) if (Pulsa(k) != 0) { limpio = false; Console.WriteLine("     cuenta " + k); }
        Check(limpio, "ninguna otra tecla cuenta para salir (ni siquiera Q sola, ni Alt+F4)");

        Console.WriteLine("\n  3) LA CUENTA SE OLVIDA SI SE TARDA\n");
        // Dos pulsaciones hoy y una manana no pueden sumar tres. Se retrasa el reloj de la cuenta y se
        // comprueba que vuelve a empezar.
        FieldInfo fUlt = TF.GetField("salirUltima", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(fUlt != null, "la rocola se acuerda de cuando fue la ultima vez");
        if (fUlt != null)
        {
            Pulsa(Keys.Control | Keys.Alt | Keys.Q);
            Pulsa(Keys.Control | Keys.Alt | Keys.Q);
            fUlt.SetValue(rocola, DateTime.Now.AddMinutes(-5));   // como si la ultima hubiera sido hace 5 minutos
            int d = Pulsa(Keys.Control | Keys.Alt | Keys.Q);
            Console.WriteLine("     dos, se deja pasar el tiempo, y otra -> " + d + " de 3");
            Check(d == 1, "pasado el rato la cuenta vuelve a empezar (dos de ayer no suman con una de hoy)");
        }

        Console.WriteLine("\n  4) Y CIERRA POR EL CAMINO DE SIEMPRE\n");
        // Esto es lo que hace que cerrar a proposito NO deje estado que reanudar. Si en vez de Close()
        // se llamara a Application.Exit() o se matara el proceso, el archivo de creditos y cola
        // sobreviviria y manana la rocola arrancaria con la cola de hoy: parecería un apagon.
        MethodBase cmd = TF.GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(cmd != null, "la rocola sigue atendiendo las teclas donde siempre");
        if (cmd != null)
        {
            List<string> ll = Codigo.Llamadas(cmd);
            bool pregunta = Codigo.Llama(ll, "PidenSalir");
            bool cierra = Codigo.Llama(ll, "Close");
            bool mata = Codigo.Llama(ll, "Exit") || Codigo.Llama(ll, "Kill");
            Console.WriteLine("     pregunta si le piden salir: " + (pregunta ? "si" : "NO")
                              + "   cierra con Close(): " + (cierra ? "si" : "NO")
                              + "   mata el proceso: " + (mata ? "SI" : "no"));
            Check(pregunta, "las teclas pasan por la cuenta de salir");
            Check(cierra, "y cierra con Close(), que es el camino que borra el estado guardado");
            Check(!mata, "y NO mata el proceso a lo bruto (eso dejaria el estado y parecería un apagon)");
        }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}
