using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

// QUE NINGUN ARCHIVO DE LA ROCOLA SE QUEDE JUNTO AL PROGRAMA.
//
// La rocola escribe una veintena de archivos: los creditos y la cola -para sobrevivir a un apagon-,
// el volumen medido de cada cancion, la huella del EQ, lo mas sonado, el indice de la biblioteca,
// los ajustes y el mapa de botones. Todos iban a la carpeta del PROGRAMA.
//
// En Windows eso funciona, porque el programa vive en una carpeta normal donde se puede escribir.
// Pero deja al operador sin poder decir "los datos y la musica van en el otro disco", y en la rocola
// de Linux es peor: alli el programa va dentro de un archivo de SOLO LECTURA, y lo que se escriba
// "por encima" se queda en la memoria hasta que el sistema guarde -o sea que un apagon se llevaria
// los creditos de la noche-.
//
// Ahora hay UN solo sitio donde se decide: la variable de entorno ROCOLA_DATOS si esta puesta y se
// puede escribir en ella, y si no la carpeta del programa, COMO SIEMPRE. Sin ponerla, nada cambia.
//
// LO QUE COMPRUEBA ESTA BATERIA, Y POR QUE ASI. No mira una lista de archivos escrita a mano: esa se
// queda vieja el dia que alguien anada uno nuevo, y justo ese seria el que se pierde. Lee el programa
// YA COMPILADO, busca cada sitio que nombra un archivo de la rocola, y exige que ninguno pregunte por
// la carpeta del programa. Si manana alguien anade "rocola_loquesea.txt" por el camino viejo, esto lo
// caza solo.
class Carpeta
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    // Los nombres que delatan que un metodo esta armando la ruta de algo de la rocola.
    static bool EsCosaDeLaRocola(string t)
    {
        if (t == null) return false;
        return t.StartsWith("rocola_", StringComparison.OrdinalIgnoreCase)
            || t == "musica" || t == "respaldo" || t == "ia_cache";
    }

    // Los dos unicos sitios donde preguntar por la carpeta del programa esta BIEN: son los que
    // deciden -uno da la carpeta de datos, y el otro busca los modelos de IA de fabrica, que se leen
    // pero no se escriben-. Si la lista creciera sin querer, esta bateria dejaria de servir.
    static readonly string[] Permitidos = { "get_CarpetaDatos", "RutaLectura" };

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);

        Console.WriteLine("\n  1) HAY UN SOLO SITIO DONDE SE DECIDE\n");
        PropertyInfo pCarpeta = TF.GetProperty("CarpetaDatos", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        MethodInfo mRuta = TF.GetMethod("RutaDatos", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        FieldInfo fCache = TF.GetField("carpetaDatos", BindingFlags.Static | BindingFlags.NonPublic);
        Check(pCarpeta != null, "la rocola tiene un sitio que dice donde viven sus datos");
        Check(mRuta != null, "y una forma de armar la ruta de cada archivo suyo");
        if (pCarpeta == null || mRuta == null || fCache == null) { Console.WriteLine("\n  " + Math.Max(1, fallos) + " FALLO(S)\n"); return Math.Max(1, fallos); }

        Console.WriteLine("\n  2) SIN PEDIR NADA, TODO SIGUE DONDE SIEMPRE\n");
        // Esto es lo que dice que en la rocola de Windows no cambia absolutamente nada.
        string dondeElPrograma = Path.GetDirectoryName(Path.GetFullPath(exe));
        fCache.SetValue(null, null);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", null);
        string sinNada = (string)pCarpeta.GetValue(null, null);
        Console.WriteLine("     sin ROCOLA_DATOS -> " + sinNada);
        Check(Igual(sinNada, dondeElPrograma), "sin pedir nada, los datos van junto al programa (como toda la vida)");

        Console.WriteLine("\n  3) Y PIDIENDOLO, VAN DONDE SE DIGA\n");
        string aparte = Path.Combine(Path.GetTempPath(), "rocola_carpeta_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        fCache.SetValue(null, null);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", aparte);
        string conVar = (string)pCarpeta.GetValue(null, null);
        Console.WriteLine("     con ROCOLA_DATOS -> " + conVar);
        Check(Igual(conVar, aparte), "poniendo ROCOLA_DATOS, los datos se van a esa carpeta");

        string unArchivo = (string)mRuta.Invoke(null, new object[] { "rocola_volumen.txt" });
        Check(unArchivo != null && unArchivo.StartsWith(aparte, StringComparison.Ordinal),
              "y cada archivo suyo se arma dentro de ella");

        Console.WriteLine("\n  4) SI NO SE PUEDE ESCRIBIR ALLI, NO SE FIA\n");
        // Un disco que aun no esta montado deja la ruta existiendo pero vacia. Escribir ahi seria
        // escribir en algo que va a desaparecer, asi que se vuelve a la carpeta del programa.
        string imposible = Path.Combine(Path.GetTempPath(), "rocola_no_existe_" + Guid.NewGuid().ToString("N").Substring(0, 8), "muy", "adentro");
        string bloqueado = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(imposible));
            File.WriteAllText(imposible, "esto es un archivo, no una carpeta");   // no se puede crear carpeta encima
            fCache.SetValue(null, null);
            Environment.SetEnvironmentVariable("ROCOLA_DATOS", imposible);
            bloqueado = (string)pCarpeta.GetValue(null, null);
        }
        catch { }
        Console.WriteLine("     apuntando a algo donde no se puede escribir -> " + (bloqueado ?? "(no se pudo probar)"));
        Check(bloqueado != null && Igual(bloqueado, dondeElPrograma),
              "si no se puede escribir donde se pidio, vuelve junto al programa en vez de perder los datos");

        Console.WriteLine("\n  5) Y NADIE SE SALTA EL CAMINO  (esto es lo que de verdad importa)\n");
        // Se recorre el programa entero buscando sitios que nombren un archivo de la rocola. Cada uno
        // tiene que pasar por la carpeta de datos y NO preguntar por la del programa.
        //
        // NI POR LA CARPETA TEMPORAL DEL SISTEMA, que es el otro atajo y el que mas caro salio: las
        // miniaturas de las portadas se guardaban ahi, y en la rocola del bar -Puppy Linux- la carpeta
        // temporal esta EN LA MEMORIA y se borra al apagar. O sea que cada noche habia que volver a
        // decodificar la portada de cada disco. Un archivo de la rocola no es tirable por definicion:
        // si lo fuera, no valdria la pena escribirlo.
        List<string> malos = new List<string>();
        int mirados = 0;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            string comoSeLlama = m.Name;
            bool permitido = false;
            foreach (string p in Permitidos) if (comoSeLlama == p) { permitido = true; break; }
            if (permitido) continue;

            bool nombraArchivo = false;
            foreach (string t in Codigo.Textos(m)) if (EsCosaDeLaRocola(t)) { nombraArchivo = true; break; }
            if (!nombraArchivo) continue;
            mirados++;
            if (Codigo.Llama(m, "get_StartupPath") || Codigo.Llama(m, "GetTempPath"))
                malos.Add(m.DeclaringType.Name + "." + comoSeLlama);
        }
        Console.WriteLine("     sitios que nombran un archivo de la rocola: " + mirados);
        Check(mirados >= 10, "se encontraron los sitios (si no, esta prueba no probaria nada)");
        if (malos.Count > 0)
        {
            Console.WriteLine("     SE SALTAN EL CAMINO (escriben junto al programa o en la carpeta temporal):");
            foreach (string s in malos) Console.WriteLine("       " + s);
        }
        Check(malos.Count == 0, "ninguno arma su ruta con la carpeta del programa ni con la temporal por su cuenta");

        Environment.SetEnvironmentVariable("ROCOLA_DATOS", null);
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }

    static bool Igual(string a, string b)
    {
        if (a == null || b == null) return false;
        return Path.GetFullPath(a).TrimEnd('/', '\\') == Path.GetFullPath(b).TrimEnd('/', '\\');
    }
}
