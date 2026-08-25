using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

// LO QUE LA ROCOLA LE PIDE PRESTADO AL SISTEMA, Y SI ESTA.
//
// La rocola no lo hace todo ella: para sonar le habla a ALSA, para dibujar le habla a X11 y a OpenGL,
// para saber cuanto refresca la pantalla le habla a RandR, y para dibujar letras y fotos Mono le
// habla a libgdiplus. Si en la maquina del bar falta UNA de esas, la rocola se muere al arrancar y
// lo unico que se ve es la pantalla en negro. No hay mensaje que valga: el cliente ve una rocola
// apagada y el dueno no sabe por que.
//
// Esta prueba saca la lista DEL PROPIO PROGRAMA YA COMPILADO -no de una lista escrita a mano, que se
// quedaria vieja en cuanto alguien anada una llamada nueva- e intenta cargar cada una. Dice cuales
// estan y cuales faltan.
//
// Y por eso ademas SIRVE SOLA: se copia este archivo a la maquina de Puppy, se corre, y en diez
// segundos se sabe si esa maquina puede con la rocola, antes de mover nada mas.
//
//     mono bibliotecas.exe rocola.exe
class Bibliotecas
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    [DllImport("libdl.so.2", EntryPoint = "dlopen")] static extern IntPtr dlopen2(string n, int f);
    [DllImport("libdl.so.2", EntryPoint = "dlerror")] static extern IntPtr dlerror2();
    [DllImport("libc", EntryPoint = "dlopen")] static extern IntPtr dlopenC(string n, int f);
    [DllImport("libc", EntryPoint = "dlerror")] static extern IntPtr dlerrorC();
    const int RTLD_NOW = 2;

    // dlopen vive en libdl en los sistemas viejos y dentro de libc en los nuevos. Se prueban los dos.
    static bool usaLibc;
    static IntPtr Abre(string n, out string porque)
    {
        porque = "";
        IntPtr h = IntPtr.Zero;
        try { h = usaLibc ? dlopenC(n, RTLD_NOW) : dlopen2(n, RTLD_NOW); }
        catch (Exception ex) { porque = ex.Message; return IntPtr.Zero; }
        if (h == IntPtr.Zero)
        {
            try
            {
                IntPtr e = usaLibc ? dlerrorC() : dlerror2();
                if (e != IntPtr.Zero) porque = Marshal.PtrToStringAnsi(e);
            }
            catch { }
        }
        return h;
    }

    // Todas las bibliotecas que nombra el programa, sacadas de sus propias llamadas al sistema.
    static List<string> LasQuePide(Assembly asm)
    {
        Dictionary<string, bool> vistas = new Dictionary<string, bool>(StringComparer.Ordinal);
        List<string> r = new List<string>();
        BindingFlags bf = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (Type t in asm.GetTypes())
            foreach (MethodInfo m in t.GetMethods(bf))
            {
                if ((m.Attributes & MethodAttributes.PinvokeImpl) == 0) continue;
                object[] at;
                try { at = m.GetCustomAttributes(typeof(DllImportAttribute), false); } catch { continue; }
                foreach (DllImportAttribute d in at)
                {
                    string n = d.Value;
                    if (n == null || n.Length == 0 || vistas.ContainsKey(n)) continue;
                    vistas[n] = true; r.Add(n);
                }
            }
        r.Sort(StringComparer.Ordinal);
        return r;
    }

    // Lo que hace falta ademas del programa: quien dibuja las letras y las fotos. Mono no lo pide con
    // una llamada al sistema -lo carga el solo al usar System.Drawing-, asi que no sale en la lista de
    // arriba y hay que nombrarlo aparte. Sin esto la rocola arranca y muere al pintar la primera letra.
    static readonly string[] DeMono = { "libgdiplus.so.0" };

    // EL NOMBRE QUE PIDE EL PROGRAMA NO SIEMPRE ES EL DEL ARCHIVO. La rocola pide "libc" a secas, y
    // archivo llamado "libc" no hay ninguno: el de verdad se llama "libc.so.6". Quien traduce es Mono,
    // con una tabla suya (/etc/mono/config, renglones "dllmap"). Aqui se hace lo mismo -primero la tabla
    // de Mono, y si no esta, los remates de siempre- para no dar por perdida una biblioteca que en
    // realidad si esta. Si se mirara solo el nombre a secas, esta prueba diria que falta libc en una
    // maquina donde la rocola funciona perfectamente, y no habria por donde empezar a buscar.
    static Dictionary<string, string> tabla;
    static Dictionary<string, string> TablaDeMono()
    {
        if (tabla != null) return tabla;
        tabla = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string cfg in new string[] { "/etc/mono/config", "/usr/etc/mono/config", "/usr/local/etc/mono/config" })
        {
            try
            {
                if (!File.Exists(cfg)) continue;
                foreach (string l in File.ReadAllLines(cfg))
                {
                    int i = l.IndexOf("dllmap dll=\"", StringComparison.Ordinal);
                    if (i < 0) continue;
                    i += 12;
                    int j = l.IndexOf('"', i); if (j < 0) continue;
                    string de = l.Substring(i, j - i);
                    int k = l.IndexOf("target=\"", j, StringComparison.Ordinal); if (k < 0) continue;
                    k += 8;
                    int m2 = l.IndexOf('"', k); if (m2 < 0) continue;
                    string a2 = l.Substring(k, m2 - k);
                    if (de.Length > 0 && a2.Length > 0 && !tabla.ContainsKey(de)) tabla[de] = a2;
                }
                break;
            }
            catch { }
        }
        return tabla;
    }

    // Los nombres de archivo que puede tener esa biblioteca, en el orden en que Mono los probaria.
    static List<string> Candidatos(string n)
    {
        List<string> r = new List<string>();
        string t;
        if (TablaDeMono().TryGetValue(n, out t)) r.Add(t);
        r.Add(n);
        if (n.IndexOf(".so", StringComparison.Ordinal) < 0)
        {
            r.Add(n + ".so");
            r.Add(n + ".so.6");
            if (!n.StartsWith("lib", StringComparison.Ordinal)) { r.Add("lib" + n + ".so"); r.Add("lib" + n + ".so.6"); }
        }
        return r;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";

        // Primero, saber por donde se piden las bibliotecas en ESTA maquina.
        string porque;
        usaLibc = false;
        if (Abre("libc.so.6", out porque) == IntPtr.Zero) usaLibc = true;

        Console.WriteLine("\n  1) LA LISTA SALE DEL PROPIO PROGRAMA\n");
        Assembly asm;
        try { asm = Assembly.LoadFrom(exe); }
        catch (Exception ex) { Console.WriteLine("  FALLO  no se pudo leer " + exe + ": " + ex.Message); return 1; }
        List<string> pide = LasQuePide(asm);
        Console.WriteLine("     " + Path.GetFileName(exe) + " le pide al sistema " + pide.Count + " biblioteca(s):");
        foreach (string n in pide) Console.WriteLine("       " + n);
        Check(pide.Count >= 3, "la lista se saco del programa y no esta vacia (si lo estuviera, esto no probaria nada)");

        Console.WriteLine("\n  2) Y ESTAN TODAS EN ESTA MAQUINA\n");
        List<string> faltan = new List<string>();
        foreach (string n in Todas(pide))
        {
            // Las de Windows (d3d9.dll, kernel32.dll) no tienen por que estar aqui: en Linux no se compilan.
            if (n.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) { Console.WriteLine("       -  " + n + "   (de Windows: aqui no hace falta)"); continue; }
            bool hay = false; string comoSeLlama = "", ultimo = "";
            foreach (string cand in Candidatos(n))
            {
                if (Abre(cand, out porque) != IntPtr.Zero) { hay = true; comoSeLlama = cand; break; }
                ultimo = porque;
            }
            Console.WriteLine("       " + (hay ? "si" : "NO") + " " + n
                              + (hay ? (comoSeLlama == n ? "" : "   (el archivo se llama " + comoSeLlama + ")")
                                     : "   <- " + ultimo));
            if (!hay) faltan.Add(n);
        }
        if (faltan.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("     FALTAN " + faltan.Count + ": " + string.Join(", ", faltan.ToArray()));
            Console.WriteLine("     Sin ellas la rocola se muere al arrancar y no se ve nada en la pantalla.");
        }
        Check(faltan.Count == 0, "todas las bibliotecas que la rocola necesita estan en esta maquina");

        Console.WriteLine("\n  3) Y LOS PROGRAMAS DE LOS VIDEOS  (sin ellos hay rocola, pero sin videos)\n");
        // Estos dos NO son obligatorios: si faltan, la rocola no ensena la seccion de videos y funciona
        // igual. Pero mas vale saberlo aqui que descubrirlo en el bar cuando alguien pida un video.
        int faltanVideo = 0;
        foreach (string prog in new string[] { "mpv", "ffmpeg", "ffprobe" })
        {
            bool hay = Programa(prog);
            Console.WriteLine("       " + (hay ? "si" : "no") + " " + prog + (hay ? "" : "   (sin el, no hay videos)"));
            if (!hay) faltanVideo++;
        }
        if (faltanVideo == 0) Console.WriteLine("     -> esta maquina puede con los videos.");
        else Console.WriteLine("     -> esta maquina NO pone videos. La rocola funciona igual, sin esa seccion.");

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }

    // ¿Esta ese programa en la maquina? Se le pregunta la version, que es lo que hace cualquiera.
    static bool Programa(string nombre)
    {
        try
        {
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(nombre, "--version");
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
            p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
            p.WaitForExit(4000);
            return true;
        }
        catch { return false; }
    }

    static List<string> Todas(List<string> pide)
    {
        List<string> r = new List<string>(pide);
        foreach (string n in DeMono) if (!r.Contains(n)) r.Add(n);
        return r;
    }
}
