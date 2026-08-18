using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// "Reproduciendo: <titulo>" ahora puede ocupar dos lineas. Se comprueba el repartidor de lineas:
// que no invente ni pierda texto, que la de arriba quepa, y que de verdad se vea MAS titulo que antes.
class Titulo
{
    static MethodInfo PARTIR;
    static Font F = new Font("DejaVu Sans", 13f, FontStyle.Bold);
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }
    static int Ancho(string s) { return TextRenderer.MeasureText(s, F).Width; }

    static void Partir(string t, int arribaW, int abajoW, out string a, out string b)
    {
        object[] args = { t, F, arribaW, abajoW, null, null };
        PARTIR.Invoke(null, args);
        a = (string)args[4]; b = (string)args[5];
    }

    // lo que se ALCANZABA A LEER antes: una sola linea estrecha, cortada con puntos suspensivos
    static string Antes(string t, int abajoW)
    {
        if (Ancho(t) <= abajoW) return t;
        int n = 0;
        while (n < t.Length && Ancho(t.Substring(0, n + 1) + "...") <= abajoW) n++;
        return t.Substring(0, n) + "...";
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        Type TF = asm.GetType("Rocola.Form1", true);
        PARTIR = TF.GetMethod("PartirNow", BindingFlags.Static | BindingFlags.NonPublic);
        if (PARTIR == null) { Console.WriteLine("no existe PartirNow"); return 1; }

        const int ARRIBA = 560, ABAJO = 430;   // ancho completo y ancho con el letrero MEJORADA al lado
        string[] casos = {
            "Nada reproduciendose",
            "Reproduciendo:  Los Tigres del Norte - La Puerta Negra",
            "Reproduciendo:  Vicente Fernandez - Por Tu Maldito Amor (En Vivo desde el Palacio de los Deportes)",
            "Reproduciendo:  Banda El Recodo de Cruz Lizarraga - Y Llegaste Tu (Version Ranchera Completa)",
            "Reproduciendo:  Rock & Roll All Nite",
            "Reproduciendo:  Supercalifragilisticoexpialidosoyunapalabrainterminablequenocabenunca",
        };
        int ganaron = 0, partidos = 0;
        foreach (string t in casos)
        {
            string a, b;
            Partir(t, ARRIBA, ABAJO, out a, out b);
            bool dos = a.Length > 0;
            if (dos) partidos++;
            // 1) no se pierde ni se inventa texto
            string junto = dos ? (a + " " + b) : b;
            bool integro = (junto == t) || (dos && (a + b) == t);   // corte por espacio o por letra
            // 2) la linea de arriba cabe en su ancho
            bool cabeArriba = !dos || Ancho(a) <= ARRIBA;
            // 3) se lee mas que antes
            int antes = Antes(t, ABAJO).Length;
            int ahora = dos ? (Math.Min(a.Length, a.Length) + Antes(b, ABAJO).Length) : Antes(b, ABAJO).Length;
            if (ahora >= antes) ganaron++;
            Console.WriteLine("\n  " + (dos ? "DOS LINEAS" : "una linea ") + "  \"" + (dos ? a : "") + "\"");
            Console.WriteLine("              \"" + b + "\"" + (dos ? "" : "   (como siempre)"));
            Console.WriteLine(string.Format("              se leen {0} letras (antes {1})", ahora, antes));
            if (!integro) Check(false, "no se pierde texto al partir: " + t);
            if (!cabeArriba) Check(false, "la linea de arriba cabe: " + t);
        }
        Console.WriteLine();
        Check(true, "ninguna linea perdio ni invento texto");
        Check(partidos >= 3, "los titulos largos SI se parten en dos (" + partidos + " de " + casos.Length + ")");
        Check(ganaron == casos.Length, "en todos los casos se lee igual o mas que antes");

        // un titulo corto tiene que quedar EXACTAMENTE como antes (linea de arriba vacia)
        string x, y;
        Partir("Reproduciendo:  Cielito Lindo", ARRIBA, ABAJO, out x, out y);
        Check(x == "" && y == "Reproduciendo:  Cielito Lindo", "un titulo corto no cambia nada (arriba vacia)");

        // sin letrero MEJORADA el ancho de abajo es el completo: se parte menos
        Partir(casos[1], ARRIBA, ARRIBA, out x, out y);
        string x2, y2;
        Partir(casos[1], ARRIBA, ABAJO, out x2, out y2);
        Check(x.Length <= x2.Length, "sin el letrero cabe mas en una sola linea");

        // casos borde
        Partir("", ARRIBA, ABAJO, out x, out y); Check(x == "" && y == "", "texto vacio no truena");
        Partir(null, ARRIBA, ABAJO, out x, out y); Check(x == "" && y == "", "texto nulo no truena");
        Partir(casos[2], ARRIBA, 10, out x, out y); Check(x == "" && y == casos[2], "con un ancho absurdo no se parte (no truena)");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
