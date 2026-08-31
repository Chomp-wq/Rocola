using System;
using System.Diagnostics;
using System.IO;
using System.Text;

// EL INSTALADOR DE LINUX.
//
// Poner la rocola en Linux era una batalla: instalar Mono, libgdiplus, mpv y ffmpeg a mano adivinando
// los nombres de los paquetes, compilar, armar un .sfs, entrar al BootManager de Puppy a marcar un
// archivo, reiniciar, y recien ahi correr otro guion. Cinco pasos, y dos de ellos hay que saber lo que
// se esta haciendo. Ahora es UNO, y sirve para Puppy y para Pop!_OS.
//
// COMO SE PRUEBA UN INSTALADOR SIN INSTALAR NADA. El guion tiene un modo --probar que dice lo que HARIA
// y no toca ni un archivo, y unas variables para fingirle el sistema. Asi esta bateria puede meterlo en
// una BookwormPup de mentira sin tener una delante, que es la unica manera de que la parte de Puppy
// este probada en algo. (Y de paso le sirve al usuario: puede ver que va a pasar ANTES de que pase.)
//
// LO QUE VIGILA, que es donde un instalador hace dano:
//
//  1) QUE ELIJA LA CARPETA DE DATOS SEGUN EL SISTEMA. En Puppy tiene que ir a un disco de verdad
//     (/mnt/home): en una instalacion frugal la carpeta del usuario vive DENTRO del archivo de guardado,
//     que no baja al disco hasta que Puppy guarda, asi que un apagon se llevaria los creditos y la cola
//     de la noche. Y al reves: en Pop!_OS /mnt/home no existe y mandar la musica ahi seria mandarla a
//     ninguna parte. Es el mismo guion decidiendo distinto, y si se equivoca no se nota hasta el apagon.
//
//  2) QUE --probar NO TOQUE NADA. Si tocara, ni esta bateria podria correr ni el usuario podria mirar
//     antes de decidir.
//
//  3) QUE LO OPCIONAL NO BLOQUEE Y LO NECESARIO SI. Sin mpv y ffmpeg la rocola funciona igual, solo que
//     sin seccion de videos -asi se comporta ya-. Sin libasound no suena: ahi hay que plantarse. Un
//     instalador que se planta por lo que no hace falta es tan malo como uno que sigue sin lo que si.
//
//  4) QUE DESINSTALAR NO SE LLEVE LA MUSICA. Es lo unico verdaderamente irrecuperable de esa maquina.
class Instala
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static string guion;

    // Corre el instalador en modo prueba con el sistema fingido, y devuelve lo que escribe.
    static string Corre(string args, params string[] finge)
    {
        ProcessStartInfo p = new ProcessStartInfo("sh", "\"" + guion + "\" " + args);
        p.UseShellExecute = false; p.RedirectStandardOutput = true; p.RedirectStandardError = true;
        p.WorkingDirectory = Path.GetDirectoryName(guion);
        for (int i = 0; i + 1 < finge.Length; i += 2) p.EnvironmentVariables[finge[i]] = finge[i + 1];
        StringBuilder sb = new StringBuilder();
        using (Process pr = Process.Start(p))
        {
            sb.Append(pr.StandardOutput.ReadToEnd());
            sb.Append(pr.StandardError.ReadToEnd());
            pr.WaitForExit(60000);
        }
        return sb.ToString();
    }

    // El valor de una linea "clave: valor" del plan.
    static string Dato(string salida, string clave)
    {
        foreach (string l in salida.Split('\n'))
        {
            string t = l.Trim();
            if (t.StartsWith(clave + ":", StringComparison.Ordinal))
                return t.Substring(clave.Length + 1).Trim();
        }
        return null;
    }

    static int Main()
    {
        // La bateria corre desde pruebas/; el instalador vive en linux/.
        guion = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../linux/instalar.sh"));

        Console.WriteLine("\n  1) EL INSTALADOR EXISTE Y SE PUEDE MIRAR ANTES DE USARLO\n");
        Check(File.Exists(guion), "hay un instalador para Linux (linux/instalar.sh)");
        if (!File.Exists(guion))
        {
            Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
            return fallos;
        }
        string pop = Corre("--probar", "ROCOLA_FINGE_SISTEMA", "debian", "ROCOLA_FINGE_ROOT", "0",
                           "ROCOLA_FINGE_HOME", "/home/pepe");
        Check(Dato(pop, "sistema") != null, "el modo --probar dice lo que haria, en lineas de clave: valor");

        Console.WriteLine("\n  2) CADA SISTEMA, SU CARPETA DE DATOS\n");
        string pup = Corre("--probar", "ROCOLA_FINGE_SISTEMA", "puppy", "ROCOLA_FINGE_ROOT", "1",
                           "ROCOLA_FINGE_HOME", "/root");
        string dPup = Dato(pup, "datos"), dPop = Dato(pop, "datos");
        Console.WriteLine("     puppy   -> " + dPup);
        Console.WriteLine("     pop!_os -> " + dPop);
        Check(Dato(pup, "sistema") == "puppy", "reconoce Puppy");
        Check(Dato(pop, "sistema") == "debian", "y reconoce la familia de Pop!_OS");
        // EN PUPPY, A UN DISCO DE VERDAD. En una instalacion frugal la carpeta del usuario vive dentro del
        // archivo de guardado y no baja al disco hasta que Puppy guarda: un apagon se llevaria los creditos.
        Check(dPup != null && dPup.StartsWith("/mnt/", StringComparison.Ordinal),
              "en Puppy los datos van a un disco de verdad, no a la carpeta del usuario");
        // Y EN POP!_OS AL REVES: /mnt/home no existe, y mandar ahi la musica seria mandarla a ninguna parte.
        Check(dPop != null && !dPop.StartsWith("/mnt/", StringComparison.Ordinal),
              "y en Pop!_OS a la carpeta del usuario, no a /mnt (que ahi no existe)");
        Check(dPup != dPop, "o sea que el mismo guion decide DISTINTO segun donde este");
        Check(Dato(pop, "musica") != null && Dato(pop, "musica").StartsWith(dPop, StringComparison.Ordinal),
              "y la musica va dentro de la carpeta de datos");

        Console.WriteLine("\n  3) SIN ROOT NO SE METE EN /opt\n");
        // Un usuario normal de Pop!_OS no tiene por que dar su contrasena para probar la rocola.
        string prog = Dato(pop, "programa");
        Console.WriteLine("     sin root -> " + prog);
        Check(prog != null && !prog.StartsWith("/opt", StringComparison.Ordinal),
              "sin root, el programa va a la carpeta del usuario");
        string popRoot = Corre("--probar", "ROCOLA_FINGE_SISTEMA", "debian", "ROCOLA_FINGE_ROOT", "1",
                               "ROCOLA_FINGE_HOME", "/root");
        Check(Dato(popRoot, "programa") != null && Dato(popRoot, "programa").StartsWith("/opt", StringComparison.Ordinal),
              "y con root, a /opt, que es donde va lo instalado para todos");

        Console.WriteLine("\n  4) LO OPCIONAL NO BLOQUEA; LO NECESARIO SI\n");
        // Sin mpv ni ffmpeg la rocola funciona igual, solo que sin seccion de videos: es como se comporta
        // ya. Plantarse por eso seria dejar sin rocola a quien no quiere videos.
        string sinVideo = Corre("--probar", "ROCOLA_FINGE_SISTEMA", "debian", "ROCOLA_FINGE_ROOT", "1",
                                "ROCOLA_FINGE_HOME", "/root", "ROCOLA_FINGE_FALTA", "mpv ffmpeg");
        Check(Dato(sinVideo, "veredicto") == "se puede", "sin mpv ni ffmpeg SIGUE: la rocola funciona, solo que sin videos");
        Check((Dato(sinVideo, "sinvideos") ?? "") == "si", "y lo avisa, para que sea una decision y no una sorpresa");
        // Sin ALSA no suena. Una rocola que no suena no es una rocola.
        string sinSon = Corre("--probar", "ROCOLA_FINGE_SISTEMA", "debian", "ROCOLA_FINGE_ROOT", "1",
                              "ROCOLA_FINGE_HOME", "/root", "ROCOLA_FINGE_FALTA", "libasound.so.2",
                              "ROCOLA_FINGE_SIN_APT", "1");
        Check(Dato(sinSon, "veredicto") != "se puede", "sin ALSA y sin poder instalarla, se planta");
        Check(sinSon.Contains("libasound"), "y dice cual falta, con su nombre");

        Console.WriteLine("\n  5) SIN GESTOR DE PAQUETES, DICE QUE INSTALAR A MANO\n");
        // Un Puppy que no traiga apt. Antes que dejar la maquina a medias, se planta y escribe los nombres.
        string sinApt = Corre("--probar", "ROCOLA_FINGE_SISTEMA", "puppy", "ROCOLA_FINGE_ROOT", "1",
                              "ROCOLA_FINGE_HOME", "/root", "ROCOLA_FINGE_FALTA", "mono",
                              "ROCOLA_FINGE_SIN_APT", "1");
        Check(Dato(sinApt, "veredicto") != "se puede", "sin gestor de paquetes no sigue a ciegas");
        Check(sinApt.Contains("mono"), "y escribe el nombre de lo que hay que instalar a mano");

        Console.WriteLine("\n  6) MIRAR NO TOCA NADA\n");
        // Si --probar tocara algo, ni esta bateria podria correr ni el usuario podria mirar antes de
        // decidir. Se le da una carpeta vacia como casa y se comprueba que sigue vacia.
        string casa = Path.Combine(Path.GetTempPath(), "rocola_probar_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(casa);
        Corre("--probar", "ROCOLA_FINGE_SISTEMA", "debian", "ROCOLA_FINGE_ROOT", "0", "ROCOLA_FINGE_HOME", casa);
        int dentro = Directory.GetFileSystemEntries(casa).Length;
        Check(dentro == 0, "en modo --probar no se crea ni un archivo (habia " + dentro + ")");
        try { Directory.Delete(casa, true); } catch { }

        Console.WriteLine("\n  7) DESINSTALAR NO SE LLEVA LA MUSICA\n");
        // Es lo unico verdaderamente irrecuperable de esa maquina: el programa se vuelve a instalar en un
        // minuto, la musica y lo que la rocola aprendio en anos no.
        string quita = Corre("--quitar --probar", "ROCOLA_FINGE_SISTEMA", "debian", "ROCOLA_FINGE_ROOT", "1",
                             "ROCOLA_FINGE_HOME", "/root");
        string borra = Dato(quita, "borra") ?? "";
        string conserva = Dato(quita, "conserva") ?? "";
        Console.WriteLine("     borra:    " + borra);
        Console.WriteLine("     conserva: " + conserva);
        Check(!borra.Contains("musica") && !borra.Contains(Dato(popRoot, "datos") ?? " "),
              "al desinstalar NO se borra la carpeta de datos ni la musica");
        Check(conserva.Length > 0, "y lo dice: se conserva la musica y lo aprendido");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
