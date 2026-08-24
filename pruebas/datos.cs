using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

// DONDE VIVE LO QUE LA ROCOLA GUARDA.
//
// La rocola escribe una veintena de archivos: los creditos y la cola (para sobrevivir a un apagon),
// el volumen medido de cada cancion, la huella del EQ, lo mas sonado, el indice de la biblioteca,
// los ajustes, el mapa de botones. Todos iban a la carpeta del PROGRAMA.
//
// En Windows eso esta bien: el programa vive en una carpeta normal donde se puede escribir. En la
// maquina del bar la rocola va a ir dentro de un ".sfs" de Puppy, que es un archivo de SOLO LECTURA
// que se carga al arrancar. Puppy deja escribir "por encima", pero lo escrito se queda en la memoria
// o en el archivo de guardado y NO BAJA AL DISCO hasta que Puppy guarda. O sea: un apagon se llevaria
// los creditos y la cola -justo lo que el guardado seguro de la rocola existe para evitar-.
//
// Asi que ahora hay un solo sitio donde se decide donde vive todo eso: la variable de entorno
// ROCOLA_DATOS si esta puesta, y si no la carpeta del programa, como siempre (o sea: en Windows no
// cambia nada). En Puppy el arranque la apunta a una carpeta del disco, fuera del .sfs, y ahi el
// fsync que la rocola ya hace llega al disco de verdad.
//
// Esta bateria enciende la rocola DE VERDAD con la variable puesta y va a mirar donde acabaron los
// archivos. Y comprueba lo otro que no se puede romper: que las carpetas de musica de mas -las que
// se agregan con Ctrl+F2, que en el archivo son renglones "dir="- siguen funcionando.
class Datos
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static string[] Wavs()
    {
        if (!Directory.Exists("wav")) return new string[0];
        string[] w = Directory.GetFiles("wav", "*.wav"); Array.Sort(w); return w;
    }

    static void Disco(string raiz, string nombre, int cuantas)
    {
        string d = Path.Combine(raiz, nombre);
        Directory.CreateDirectory(d);
        string[] w = Wavs();
        for (int k = 0; k < cuantas && k < w.Length; k++)
            File.Copy(w[k], Path.Combine(d, "Cancion " + (k + 1) + ".wav"), true);
    }

    static int Main(string[] argv)
    {
        string exe = Path.GetFullPath((argv.Length > 0) ? argv[0] : "rocola.exe");
        string baseDir = Path.Combine(Path.GetTempPath(), "rocola_datos");
        try { if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true); } catch { }

        // Tres carpetas separadas a proposito, para que no haya forma de confundirlas:
        //   programa/  el .exe (hara de ".sfs de solo lectura")
        //   datos/     donde TIENE que escribir
        //   masmusica/ un segundo disco de musica, agregado con un renglon "dir="
        string dirPrograma = Path.Combine(baseDir, "programa");
        string dirDatos = Path.Combine(baseDir, "datos");
        string dirMas = Path.Combine(baseDir, "masmusica");
        Directory.CreateDirectory(dirPrograma); Directory.CreateDirectory(dirDatos); Directory.CreateDirectory(dirMas);

        File.Copy(exe, Path.Combine(dirPrograma, "rocola.exe"));
        foreach (string d in Directory.GetFiles(".", "*.dll")) File.Copy(d, Path.Combine(dirPrograma, Path.GetFileName(d)), true);

        Disco(Path.Combine(dirDatos, "musica"), "Los Tigres del Norte", 2);
        Disco(dirMas, "Banda El Recodo", 2);
        File.WriteAllText(Path.Combine(dirDatos, "rocola_teclas.txt"),
            "arriba=38\r\nabajo=40\r\nizquierda=37\r\nderecha=39\r\nponer=13\r\nregresar=8\r\nmoneda=32\r\nsaltar=9\r\n");
        File.WriteAllText(Path.Combine(dirDatos, "rocola_config.txt"),
            "diagnostico=1\r\ndir=" + dirMas + "\r\n");

        Console.WriteLine("\n  1) LA ROCOLA ENCIENDE CON LA CARPETA DE DATOS APARTE\n");
        Console.WriteLine("     programa en " + dirPrograma);
        Console.WriteLine("     datos en    " + dirDatos);
        Process p = null;
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("mono", "\"" + Path.Combine(dirPrograma, "rocola.exe") + "\"");
            psi.WorkingDirectory = baseDir;        // ni la carpeta del programa ni la de datos: no puede colarse por aqui
            psi.EnvironmentVariables["ROCOLA_DATOS"] = dirDatos;
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            p = Process.Start(psi);
        }
        catch (Exception ex) { Console.WriteLine("  FALLO  no se pudo lanzar la rocola: " + ex.Message); return 1; }

        // Se espera a que haya escrito el indice de la biblioteca, que es de lo ultimo que hace al arrancar.
        string indice = Path.Combine(dirDatos, "rocola_biblioteca.txt");
        for (int i = 0; i < 40 && !p.HasExited; i++)
        {
            System.Threading.Thread.Sleep(700);
            if (File.Exists(indice) && new FileInfo(indice).Length > 0) { System.Threading.Thread.Sleep(1500); break; }
        }
        bool viva = !p.HasExited;
        if (!viva)
        {
            string sal = "";
            try { sal = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); } catch { }
            foreach (string l in sal.Replace("\r", "").Split('\n')) if (l.Trim().Length > 0) Console.WriteLine("       " + l);
        }
        Check(viva, "la rocola arranca y sigue en pie con los datos en otra carpeta");

        Console.WriteLine("\n  2) Y ESCRIBE ALLI, NO JUNTO AL PROGRAMA\n");
        List<string> enDatos = new List<string>(), enPrograma = new List<string>();
        foreach (string f in Directory.GetFiles(dirDatos)) if (Path.GetFileName(f).StartsWith("rocola_")) enDatos.Add(Path.GetFileName(f));
        foreach (string f in Directory.GetFiles(dirPrograma)) if (Path.GetFileName(f).StartsWith("rocola_")) enPrograma.Add(Path.GetFileName(f));
        enDatos.Sort(); enPrograma.Sort();
        Console.WriteLine("     en la carpeta de datos: " + enDatos.Count + "  (" + string.Join(", ", enDatos.ToArray()) + ")");
        Console.WriteLine("     junto al programa:      " + enPrograma.Count
                          + (enPrograma.Count > 0 ? "  (" + string.Join(", ", enPrograma.ToArray()) + ")" : ""));
        Check(enDatos.Count >= 3, "los archivos de la rocola aparecen en la carpeta de datos");
        Check(enPrograma.Count == 0, "y NI UNO junto al programa (que en Puppy es de solo lectura)");

        // El indice tiene que estar, y con contenido: es la prueba de que de verdad leyo la musica desde alli.
        bool hayIndice = File.Exists(indice) && new FileInfo(indice).Length > 0;
        Check(hayIndice, "el indice de la biblioteca se guardo en la carpeta de datos");

        Console.WriteLine("\n  3) Y LAS CARPETAS DE MUSICA DE MAS SIGUEN FUNCIONANDO\n");
        // La musica principal esta dentro de la carpeta de datos; la de mas, en otro sitio, puesta con un
        // renglon "dir=". Las dos tienen que aparecer en el indice: si mover los datos rompiera esto, se
        // perderia la mitad de la biblioteca del bar sin que nadie lo note hasta que falte una cancion.
        string lib = hayIndice ? File.ReadAllText(indice) : "";
        bool principal = lib.IndexOf("Los Tigres del Norte", StringComparison.OrdinalIgnoreCase) >= 0;
        bool extra = lib.IndexOf("Banda El Recodo", StringComparison.OrdinalIgnoreCase) >= 0;
        Console.WriteLine("     musica de la carpeta de datos: " + (principal ? "encontrada" : "NO"));
        Console.WriteLine("     musica del otro disco (dir=):  " + (extra ? "encontrada" : "NO"));
        Check(principal, "la carpeta \"musica\" de al lado de los datos se lee");
        Check(extra, "y un disco de musica agregado con dir= tambien (Ctrl+F2 sigue sirviendo)");

        try { if (viva) { p.Kill(); p.WaitForExit(4000); } } catch { }
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}
