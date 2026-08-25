using System;
using System.Drawing;
using System.Reflection;

// MENOS ESTRELLAS DETRAS DE LAS LETRAS.
//
// La rocola tiene dos franjas de texto que no se mueven: el abecedario de arriba y la barra de
// estado de abajo. El cielo estrellado pasa por detras de las dos, y una estrella justo detras de
// una letra le quita contraste: se lee peor, sobre todo de lejos y sobre todo las estrellas
// brillantes, que son puntos claros del tamano de un acento.
//
// Para eso existe "QuietBands" en el campo de estrellas: poner menos estrellas en esas dos franjas.
// Se enciende al arrancar la rocola... y NO LO LEIA NADIE. Estaba escrito y nunca usado, asi que el
// efecto no ocurria: habia las mismas estrellas detras de las letras que en medio de la pantalla.
//
// Esta bateria cuenta las estrellas de las tres zonas -franja de arriba, centro, franja de abajo- y
// exige que las franjas tengan bastantes menos. Y que el centro NO se toque: si el arreglo quitara
// estrellas de todas partes, el cielo se veria mas pobre y no se habria arreglado nada.
class Franjas
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const int W = 1024, H = 768;

    static Type TS;

    // Cuenta las estrellas por zona: 0 = franja de arriba, 1 = centro, 2 = franja de abajo.
    // 'soloLasQueSeVen' deja fuera el POLVO de un pixel, que no estorba a la lectura.
    static int[] Cuenta(bool callado, out int total) { return Cuenta(callado, true, out total); }
    static int[] Cuenta(bool callado, bool soloLasQueSeVen, out int total)
    {
        object sf = Activator.CreateInstance(TS, true);
        FieldInfo fQ = TS.GetField("QuietBands", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        fQ.SetValue(sf, callado);
        FieldInfo fD = TS.GetField("Density", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fD != null) fD.SetValue(sf, 1f);
        MethodInfo mE = TS.GetMethod("Ensure", BindingFlags.Instance | BindingFlags.NonPublic);
        mE.Invoke(sf, new object[] { W, H });

        Array stars = (Array)TS.GetField("stars", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sf);
        Type TStar = stars.GetType().GetElementType();
        FieldInfo fY = TStar.GetField("Y"), fKind = TStar.GetField("Kind");
        int[] z = new int[3]; total = 0;
        for (int i = 0; i < stars.Length; i++)
        {
            object s = stars.GetValue(i);
            float y = (float)fY.GetValue(s);
            if (soloLasQueSeVen && (int)fKind.GetValue(s) == 0) continue;
            total++;
            if (y < 0.13f) z[0]++;
            else if (y > 0.88f) z[2]++;
            else z[1]++;
        }
        return z;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        TS = TF.GetNestedType("Starfield", BindingFlags.NonPublic | BindingFlags.Public);
        Check(TS != null, "el campo de estrellas esta donde tiene que estar");
        if (TS == null) { Console.WriteLine("\n  1 FALLO(S)\n"); return 1; }

        Console.WriteLine("\n  1) SIN PEDIRLO, EL CIELO ES PAREJO  (si no, esto no probaria nada)\n");
        int totNormal, totCallado;
        int[] normal = Cuenta(false, out totNormal);
        Console.WriteLine("     arriba " + normal[0] + "   centro " + normal[1] + "   abajo " + normal[2]
                          + "   (de " + totNormal + " estrellas que se ven)");
        // Las franjas juntas son la cuarta parte de la pantalla, asi que les toca la cuarta parte.
        double espera = totNormal * 0.25;
        double hayNormal = normal[0] + normal[2];
        Check(hayNormal > espera * 0.6, "sin pedirlo, las franjas tienen su parte de estrellas como cualquier otra zona");

        Console.WriteLine("\n  2) PIDIENDOLO, DETRAS DE LAS LETRAS HAY MENOS\n");
        int[] callado = Cuenta(true, out totCallado);
        Console.WriteLine("     arriba " + callado[0] + "   centro " + callado[1] + "   abajo " + callado[2]
                          + "   (de " + totCallado + " estrellas que se ven)");
        double hayCallado = callado[0] + callado[2];
        Console.WriteLine("     en las dos franjas: " + hayNormal + " -> " + hayCallado
                          + "   (" + (hayNormal > 0 ? (100.0 * hayCallado / hayNormal).ToString("0") : "?") + "% de lo que habia)");
        Check(hayCallado < hayNormal * 0.55, "detras de las letras quedan bastantes menos estrellas");

        // Y QUE NO QUEDE UN AGUJERO. Las que se apartan son las que se VEN -las que le quitan contraste a
        // una letra-; el POLVO de un pixel se queda donde caiga, porque no estorba a la lectura y es lo
        // que evita que la franja se vea como una banda negra pegada al cielo. Asi que aqui se cuenta
        // todo, polvo incluido, que es lo que se ve de verdad.
        int totTodo;
        int[] todo = Cuenta(true, false, out totTodo);
        Console.WriteLine("     contando tambien el polvo:  arriba " + todo[0] + "   abajo " + todo[2]);
        Check(todo[0] > 5 && todo[2] > 5, "las franjas siguen con cielo: un hueco vacio se notaria mas que las estrellas");

        Console.WriteLine("\n  3) Y EL CENTRO NO SE TOCA\n");
        // Si el arreglo quitara estrellas de todas partes, el cielo se veria mas pobre y no se habria
        // arreglado nada: lo que se quiere es moverlas, no perderlas.
        Console.WriteLine("     centro: " + normal[1] + " -> " + callado[1]);
        Check(callado[1] >= normal[1] * 0.9, "en el centro de la pantalla sigue habiendo las mismas estrellas");
        Console.WriteLine("     total de estrellas que se ven: " + totNormal + " -> " + totCallado);
        Check(totCallado >= totNormal * 0.9, "y el cielo entero no pierde estrellas: se mueven, no se pierden");

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}
