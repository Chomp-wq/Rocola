// LA CARPETA DEL PROGRAMA (Nativo/CarpetaPrograma.cs, el mismo archivo del programa) contra lo que daba la capa de la transicion
// (Plataforma/Ventanas.Form.cs, Application.StartupPath; su cuenta, tal cual, en Capa.cs, que escribe guiones/pruebas_carpeta.sh) y contra la regla
// de WinForms (Application.StartupPath = Path.GetDirectoryName(ExecutablePath): la carpeta del ejecutable, sin la barra del final). Se corre con
// JIT (dotnet PruebasCarpeta.dll: la carpeta es la del .dll) y con NativeAOT (la del ejecutable), desde varias carpetas.
//   PruebasCarpeta CASO [CARPETA_ESPERADA]
using System;
using System.IO;

namespace Rocola
{
    internal static class P
    {
        static int pasa, falla;
        static void Mira(string caso, string nombre, string obtenido, string esperado)
        {
            bool ok = obtenido == esperado;
            if (ok) pasa++; else falla++;
            Console.WriteLine((ok ? "PASA  " : "FALLA ") + caso + ": " + nombre + ": [" + obtenido + "]" + (ok ? "" : " (esperado [" + esperado + "])"));
        }

        static int Main(string[] args)
        {
            string caso = args.Length > 0 ? args[0] : "?";
            string nativa = CarpetaPrograma.Ruta, capa = CapaTransicion.StartupPath;
            bool aot = !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
#pragma warning disable IL3000   // (a proposito: ensena que con NativeAOT sale vacio; NoWarn en el .csproj para el aviso de NativeAOT)
            string ubicacion = typeof(P).Assembly.Location;
#pragma warning restore IL3000
            // La regla de WinForms: la carpeta del ejecutable (con JIT, el programa es el .dll que corre dotnet; con NativeAOT, el ejecutable).
            string ejecutable = aot ? Environment.ProcessPath : ubicacion;
            string winforms = Path.GetDirectoryName(ejecutable);
            Console.WriteLine("       " + caso + ": " + (aot ? "NativeAOT" : "JIT") + "; BaseDirectory [" + AppContext.BaseDirectory + "]; Assembly.Location ["
                + ubicacion + "]; ProcessPath [" + Environment.ProcessPath + "]; actual [" + Environment.CurrentDirectory + "]");
            Mira(caso, "nativa = la de la capa", nativa, capa);
            Mira(caso, "nativa = WinForms (GetDirectoryName del ejecutable)", nativa, winforms);
            Mira(caso, "sin la barra del final", (nativa.Length > 0 && nativa[nativa.Length - 1] != '/').ToString(), "True");
            Mira(caso, "existe", Directory.Exists(nativa).ToString(), "True");
            if (args.Length > 1) Mira(caso, "la carpeta donde se puso", nativa, args[1]);
            if (aot) Mira(caso, "Assembly.Location con NativeAOT (por eso no sirve)", ubicacion, "");
            Console.WriteLine("       " + caso + ": " + pasa + " pasan, " + falla + " fallan");
            return falla == 0 ? 0 : 1;
        }
    }
}
