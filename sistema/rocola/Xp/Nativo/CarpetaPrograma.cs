// LA CARPETA DEL PROGRAMA, EN LINUX (port nativo de XP, lo-suelto W1, 12-11-2026). En XP el codigo la pide a WinForms (Application.StartupPath:
// Path.GetDirectoryName(ExecutablePath), la carpeta de Rocola.exe, sin la barra del final) en tres sitios: la carpeta de datos cuando no se puede usar
// otra (Form1.Datos.cs, CarpetaDatos), donde se buscan las IA de fabrica (Form1.Datos.cs, CarpetasIas) y el aviso de la carpeta de datos
// (Program.cs, AvisaCarpetaDatos). Aqui la da .NET: AppContext.BaseDirectory es la carpeta del programa (la del ejecutable de NativeAOT, o la de
// rocola-xp.dll con JIT) y lleva siempre la barra del final, que se quita como en XP. No sirve Assembly.Location: con NativeAOT sale vacio.
// Es lo mismo que daba la capa de la transicion (Plataforma/Ventanas.Form.cs, Application.StartupPath), con la misma cuenta; lo comprueba
// Pruebas/Ventanas/Carpeta (guiones/pruebas_carpeta.sh), en JIT y en NativeAOT.
using System;

namespace Rocola
{
    internal static class CarpetaPrograma
    {
        // La carpeta donde esta el programa, sin la barra del final (Application.StartupPath de WinForms).
        internal static string Ruta { get { return AppContext.BaseDirectory.TrimEnd('/', '\\'); } }
    }
}
