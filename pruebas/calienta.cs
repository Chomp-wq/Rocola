using System;
using System.Collections.Generic;
using System.Reflection;

// EL CALENTADOR YA LEE LA CANCION ENTERA: QUE LA MIDA.
//
// Cuando alguien pide una cancion, la rocola lee ese archivo COMPLETO en segundo plano para dejarlo en
// la memoria del sistema y que abrirlo luego sea instantaneo. Lo lee de principio a fin, a trozos,
// cediendo el disco si hacen falta caratulas o hay un video sonando... y tira todo lo que lee.
//
// Y la parte cara de MEDIR una cancion son dos cosas: leerla del disco y descomprimirla. La lectura ya
// esta pagada ahi. Descomprimir lo que acaba de pasar por delante cuesta 550 ms por minuto de cancion
// -medido-, o sea unos 2.2 segundos por una de cuatro minutos, repartidos en los minutos que esa
// cancion va a estar esperando en la cola y en un hilo que va en la prioridad mas baja que hay.
//
// A cambio, la cancion llega a sonar YA MEDIDA: nivelada de volumen a la primera en vez de a la
// segunda, con su duracion apuntada, y con el turno pudiendo decir los minutos aunque sea la primera
// vez que se pone.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE MIDA. Si no, el archivo se sigue leyendo entero para nada.
//   2) QUE NO MIDA LO QUE YA ESTA MEDIDO. Serian 2.2 segundos de procesador tirados en cada cancion
//      de las que mas se ponen, que son justo las que ya estan medidas.
//   3) QUE LA PREGUNTA SE HAGA EN EL HILO DE LA PANTALLA, que es de quien son las tablas. Las tablas
//      de la rocola no llevan candado -a proposito, para no pagarlo en cada consulta-, asi que leerlas
//      desde el hilo del calentador podria reventar justo mientras el pre-analisis escribe en ellas.
//   4) QUE LO MEDIDO SE APUNTE POR UN SOLO SITIO, el mismo que usa el pre-analisis. Dos sitios que
//      escriben las mismas tres tablas se desincronizan en cuanto alguien toca uno y se olvida del otro.
//   5) QUE SIGA CEDIENDO EL DISCO. Este hilo existe para NO estorbar; si por medir se pone a competir
//      con las caratulas o con un video, hace mas daño del que arregla.
class Calienta
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

    // Lo que hace el calentador de verdad vive en el delegado que se lanza como hilo, y el compilador lo
    // saca aparte con el nombre entre picos. Sin mirar ahi no se ve nada de lo que hace.
    static List<MethodBase> Ayudantes(string metodo)
    {
        List<MethodBase> r = new List<MethodBase>();
        string picos = "<" + metodo + ">";
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            string n = m.Name;
            string t = (m.DeclaringType != null) ? m.DeclaringType.Name : "";
            if (n == metodo) continue;   // el propio, no: aqui se busca lo que se le saco aparte
            if (n.IndexOf(picos, StringComparison.Ordinal) >= 0 || t.IndexOf(picos, StringComparison.Ordinal) >= 0) r.Add(m);
        }
        return r;
    }
    static bool AlgunAyudante(string metodo, string llamaA)
    {
        foreach (MethodBase m in Ayudantes(metodo)) if (Codigo.Llama(m, llamaA)) return true;
        return false;
    }
    static bool AyudanteLee(string metodo, string campo)
    {
        foreach (MethodBase m in Ayudantes(metodo))
        {
            int lee, esc; Codigo.UsosDeCampo(m, campo, out lee, out esc);
            if (lee > 0) return true;
        }
        return false;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes()) if (t.Name == "Form1") TF = t;
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }

        Console.WriteLine("\n  1) LAS PIEZAS\n");
        MethodBase cal = Met("PrecalientaSiguiente"), mide = Met("MeasureFile"), pub = Met("PublicaMedida");
        Check(cal != null, "sigue habiendo un calentador de la siguiente cancion (PrecalientaSiguiente)");
        Check(mide != null, "y un solo sitio que mide un archivo (MeasureFile)");
        Check(pub != null, "y un solo sitio que apunta lo medido (PublicaMedida)");
        if (cal == null || mide == null || pub == null) { Console.WriteLine("\n  " + fallos + " FALLO(S)"); return Math.Max(fallos, 1); }

        Console.WriteLine("\n  2) YA QUE LEE EL ARCHIVO ENTERO, LO MIDE\n");
        Check(AlgunAyudante("PrecalientaSiguiente", "MeasureFile"),
              "el calentador aprovecha lo que acaba de leer y mide la cancion");
        Check(AlgunAyudante("PrecalientaSiguiente", "PublicaMedida"),
              "y apunta lo medido donde lo apunta el pre-analisis");

        Console.WriteLine("\n  3) PERO NO MIDE LO QUE YA ESTA MEDIDO\n");
        int lee, esc;
        Codigo.UsosDeCampo(cal, "durCache", out lee, out esc);
        Check(lee > 0, "antes de gastar nada, mira si esa cancion ya esta medida");
        // Y esa consulta va en el hilo de la PANTALLA, no en el del calentador: las tablas no llevan
        // candado y el pre-analisis escribe en ellas desde otro lado.
        Check(!AyudanteLee("PrecalientaSiguiente", "durCache"),
              "y lo mira en el hilo de la pantalla, que es de quien son las tablas");

        Console.WriteLine("\n  4) LO MEDIDO SE APUNTA POR UN SOLO SITIO\n");
        // Las tres tablas -duracion, sonoridad y huella tonal- se escriben SOLO ahi. Con dos sitios, el
        // dia que alguien arregle uno se olvidara del otro.
        string[] tabla = { "durCache", "loudCache", "eqCache" };
        for (int i = 0; i < tabla.Length; i++)
        {
            Codigo.UsosDeCampo(pub, tabla[i], out lee, out esc);
            Check(lee > 0, "el sitio que apunta escribe tambien " + tabla[i]);
        }
        MethodBase prescan = Met("StartPreScan");
        bool prescanPub = (prescan != null) && Codigo.Llama(Codigo.ConAyudantes(TF, "StartPreScan"), "PublicaMedida");
        Check(prescanPub, "y el pre-analisis apunta por ese mismo sitio, no por su cuenta");

        Console.WriteLine("\n  5) Y SIGUE CEDIENDO EL DISCO\n");
        // Este hilo existe para NO estorbar. Si por ponerse a medir dejara de cederle el disco a las
        // caratulas o a un video, haria mas daño del que arregla.
        Check(AyudanteLee("PrecalientaSiguiente", "videoOn"), "si hay un video sonando, se aparta");
        Check(AyudanteLee("PrecalientaSiguiente", "warmSongGen"), "y si la cola cambia, se cancela solo");
        Check(AlgunAyudante("PrecalientaSiguiente", "BeginInvoke"),
              "y lo que apunta lo manda al hilo de la pantalla, no lo escribe desde aqui");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
