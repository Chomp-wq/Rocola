// LA VERSION DEL PROGRAMA EN ROCOLA OS (`rocola-xp --version`). No es de XP (alli la version era la fecha del ejecutable, Form1.DeCuandoEs,
// que sigue igual): es lo que Rocola OS le pregunta a su programa. /usr/bin/rocola, rocola-actualiza y rocola-buzon leen del programa de
// ahora UNA linea, `rocola-consola programa N (COMMIT) compilado SEGUNDOS`, con `s/.*programa \([0-9]*\).*/\1/`,
// `s/.* compilado \([0-9]*\).*/\1/` y `grep -E "programa N( |$)"`; aqui la misma con «rocola-xp». NO CAMBIAR EL FORMATO sin cambiar esos
// tres guiones (regla del 7-11-2026).
//
// N y COMMIT los pone quien compila (-p:RocolaVersion=N -p:RocolaCommit=abc1234; sin ellos, 0 y «local») y SEGUNDOS es cuando se compilo
// (segundos desde 1970, UTC): RocolaXp.csproj, con las mismas propiedades que Consola.csproj.
//
// Se contesta ANTES de entrar al Main de XP (Codigo/Program.cs, que no se toca): antes de la instancia unica, de mirar o crear la carpeta
// de datos y de abrir la pantalla y el sonido; el programa acaba ahi con 0. Como las pruebas de Plataforma (PruebasLinux, PruebaAot): al
// cargarse el programa.
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Rocola
{
    internal static class VersionRocolaOs
    {
        private static string Info
        {
            get
            {
                AssemblyInformationalVersionAttribute a = typeof(VersionRocolaOs).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                return a != null && !string.IsNullOrEmpty(a.InformationalVersion) ? a.InformationalVersion : "0+local";
            }
        }
        internal static string Numero { get { string i = Info; int p = i.IndexOf('+'); return p > 0 ? i.Substring(0, p) : i; } }
        internal static string Commit { get { string i = Info; int p = i.IndexOf('+'); return p > 0 ? i.Substring(p + 1) : "local"; } }
        // cuando se compilo (0 si no se sabe: el lanzador lo toma entonces por «no lo dice»)
        internal static long Fecha
        {
            get
            {
                foreach (AssemblyMetadataAttribute a in typeof(VersionRocolaOs).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
                {
                    long f;
                    if (a.Key == "RocolaFecha" && long.TryParse(a.Value, out f)) return f;
                }
                return 0;
            }
        }
        internal static string Linea { get { return "rocola-xp programa " + Numero + " (" + Commit + ") compilado " + Fecha; } }

#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Antes()
        {
            string[] a = Environment.GetCommandLineArgs();
            if (a.Length < 2 || a[1] != "--version") return;
            Console.Out.WriteLine(Linea);
            Console.Out.Flush();
            Environment.Exit(0);
        }
    }
}
