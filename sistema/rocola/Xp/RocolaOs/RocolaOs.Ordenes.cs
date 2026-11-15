// LAS ORDENES DE PRUEBA DE ROCOLA OS EN LA LINEA DE MANDATOS (fase 11, grupo andamio): `rocola-xp --prueba-algo ARGUMENTOS...`. Como
// Nativo/VersionRocolaOs.cs y Nativo/ImportaXp.cs, se contestan al cargarse el programa, con un [ModuleInitializer] del archivo de cada
// grupo, y el programa acaba ahi (sin el Main de XP: sin la instancia unica, sin Form1, sin sonido). El patron, en el archivo de la funcion:
//
//     #pragma warning disable CA2255
//         [ModuleInitializer]
//     #pragma warning restore CA2255
//         internal static void RocolaOsRegistraRed()
//         {
//             RocolaOs.Registra(new RocolaOsFuncion { Nombre = "red", ... });   // (el registro de la funcion: lo de siempre)
//             RocolaOsOrdenes.Si("--prueba-red", PruebaRed);                      // (y su orden de prueba, si tiene)
//         }
//         private static int PruebaRed(string[] argumentos) { ...; return 0; }   // lo que devuelve es el codigo de salida
//
// Si la linea de mandatos no es esa orden, Si no hace nada (y no cuesta nada). Si lo es: la corre (con los argumentos de despues), atrapa lo
// que lance (sale con 70 y lo dice por la salida de errores), vacia la salida y acaba el programa con lo que devolvio.
//
// LO QUE HAY QUE SABER (medido el 14-11-2026, comprobado con `dotnet msbuild -getItem:Compile`):
//   - LOS INICIALIZADORES CORREN EN EL ORDEN DE LOS ARCHIVOS (por orden alfabetico, sin distinguir mayusculas: Codigo/, Nativo/,
//     Plataforma/ y despues RocolaOs/). Los de RocolaOs/ van DESPUES del de Plataforma/PruebasLinux.cs, que abre la pantalla
//     (Anfitrion.Arranca: KMS, o fuera de pantalla con ROCOLA_PANTALLA=fuera): una orden de RocolaOs/ corre con la pantalla ya abierta. En
//     las pruebas, ROCOLA_PANTALLA=fuera:64x64; una orden para la consola de mantenimiento, igual (si no, se queda con la pantalla). Para
//     correr ANTES de la pantalla haria falta un gancho en Plataforma/PruebasLinux.cs (antes de Anfitrion.Arranca): se pide al coordinador.
//   - DENTRO DE UN INICIALIZADOR NO HAY OTROS HILOS: uno que entre al programa espera a que acaben los inicializadores (con JIT: medido, un
//     hilo que llama a un metodo del programa no acaba en 3 s; lo mismo dice Plataforma/PruebasLinux.cs del video). Una orden que necesita
//     hilos (la salida de sonido de XP, procesos con lectura de fondo) no puede correr aqui: o lo hace todo en este hilo, o pide el gancho
//     de despues del inicio (Plataforma/PruebasLinux.cs, DespuesDelInicio).
//   - AL CORRER UNA ORDEN, SOLO ESTAN REGISTRADAS LAS FUNCIONES DE LOS ARCHIVOS QUE VAN ANTES (el orden de arriba): una orden registra lo que
//     necesita antes de correr (la de PruebaAndamio.cs registra primero su funcion de prueba).
//   - No se pregunta RocolaOs.Encendida en un inicializador (el registro aun no esta completo), salvo en una orden que ya lo sabe.
using System;

namespace Rocola
{
    internal static class RocolaOsOrdenes
    {
        // SI LA LINEA DE MANDATOS ES «rocola-xp ORDEN ...»: corre la orden y acaba el programa con lo que devuelva
        internal static void Si(string orden, Func<string[], int> corre)
        {
            string[] a;
            try { a = Environment.GetCommandLineArgs(); } catch { return; }
            if (a == null || a.Length < 2 || a[1] != orden || corre == null) return;
            int r;
            try
            {
                string[] resto = new string[a.Length - 2];
                Array.Copy(a, 2, resto, 0, resto.Length);
                r = corre(resto);
            }
            catch (Exception ex)
            {
                try { Console.Error.WriteLine("rocola-xp " + orden + ": fallo inesperado (" + ex.GetType().Name + ": " + ex.Message + ")\n" + ex.StackTrace); } catch { }
                r = 70;
            }
            try { Console.Out.Flush(); Console.Error.Flush(); } catch { }
            Environment.Exit(r);
        }
    }
}
