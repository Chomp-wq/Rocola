// ROCOLA OS (fase 11): LA ORDEN DE PRUEBA DEL GESTOR DE ARCHIVOS (FASE11_navegador-archivos.md; la usa Pruebas/RocolaOs/navegador-archivos/prueba.sh).
//   rocola-xp --prueba-archivos DATOS USB FUERA
// DATOS hace de carpeta de datos de la rocola (escribible), USB de memoria USB (de solo lectura para el gestor) y FUERA de lo que esta fuera
// de las dos (a donde apuntan los enlaces trampa). La prueba arma los archivos, intenta salir de las carpetas de todas las maneras y escribe
// un renglon «BIEN ...» o «MAL ...» por comprobacion; sale con 0 si todas estan bien. No toca nada fuera de esas tres carpetas.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Rocola
{
    internal static class RocolaOsArchivosPrueba
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra() { RocolaOsOrdenes.Si("--prueba-archivos", Corre); }

        private static int bien, mal;
        private static void Si(string que, bool ok) { Console.WriteLine((ok ? "BIEN  " : "MAL   ") + que); if (ok) bien++; else mal++; }
        private static string Modo(string r) { try { return Convert.ToString((int)File.GetUnixFileMode(r), 8); } catch { return "?"; } }

        private static int Corre(string[] a)
        {
            if (a.Length < 3) { Console.Error.WriteLine("uso: rocola-xp --prueba-archivos DATOS USB FUERA"); return 2; }
            string d0 = a[0], u0 = a[1], f0 = a[2];
            foreach (string c in new[] { d0, u0, f0 }) Directory.CreateDirectory(c);
            File.WriteAllText(Path.Combine(f0, "secreto.txt"), "lo de fuera\n");
            Directory.CreateDirectory(Path.Combine(d0, "sub"));
            File.WriteAllText(Path.Combine(d0, "notas.txt"), "hola\r\nmundo\r\n");
            Directory.CreateDirectory(Path.Combine(u0, "carpeta"));
            File.WriteAllText(Path.Combine(u0, "rocola_spotify_api.txt"), "client_id=IDDEPRUEBA1234567890\nclient_secret=SECRETODEPRUEBA0987654321\n");
            File.WriteAllText(Path.Combine(u0, "rocola_spotify.txt"), "LLAVEDESOLOISTDEPRUEBA\n");
            File.WriteAllText(Path.Combine(u0, "leeme.txt"), "texto\n");
            // los enlaces trampa: en los datos y en la USB, a FUERA (una carpeta y un archivo), y uno con «..» relativo
            Trampa(Path.Combine(d0, "fuga"), f0); Trampa(Path.Combine(d0, "fuga.txt"), Path.Combine(f0, "secreto.txt"));
            Trampa(Path.Combine(d0, "sub", "arriba"), "../../" + Path.GetFileName(f0.TrimEnd('/')));
            Trampa(Path.Combine(u0, "fuga"), f0); Trampa(Path.Combine(u0, "fuga.txt"), Path.Combine(f0, "secreto.txt"));
            Trampa(Path.Combine(d0, "dentro.txt"), Path.Combine(d0, "notas.txt"));   // (uno que no sale: si se lista)

            string dr = RocolaOsArchivos.Real(d0), ur = RocolaOsArchivos.Real(u0), fr = RocolaOsArchivos.Real(f0);
            RocolaOsRaiz datos = new RocolaOsRaiz { Nombre = "datos", Ruta = dr, Escribe = true, Clave = dr };
            RocolaOsRaiz usb = new RocolaOsRaiz { Nombre = "usb", Ruta = ur, Clave = ur };

            // ---- la regla: nunca fuera ----
            Si("Dentro: la raiz misma", RocolaOsArchivos.Dentro(dr, d0));
            Si("Dentro: una subcarpeta", RocolaOsArchivos.Dentro(dr, Path.Combine(d0, "sub")));
            Si("Dentro: «..» que sale (datos/sub/../..) NO", !RocolaOsArchivos.Dentro(dr, Path.Combine(d0, "sub", "..", "..")));
            Si("Dentro: el enlace a una carpeta de fuera NO", !RocolaOsArchivos.Dentro(dr, Path.Combine(d0, "fuga")));
            Si("Dentro: el enlace a un archivo de fuera NO", !RocolaOsArchivos.Dentro(dr, Path.Combine(d0, "fuga.txt")));
            Si("Dentro: el enlace relativo con «..» (sub/arriba) NO", !RocolaOsArchivos.Dentro(dr, Path.Combine(d0, "sub", "arriba")));
            Si("Dentro: un archivo de dentro a traves del enlace de fuera NO", !RocolaOsArchivos.Dentro(dr, Path.Combine(d0, "fuga", "secreto.txt")));
            Si("Dentro: una raiz que empieza igual (datos2) NO", !RocolaOsArchivos.Dentro(dr, dr + "2"));
            Si("Dentro: el enlace que se queda dentro (dentro.txt) SI", RocolaOsArchivos.Dentro(dr, Path.Combine(d0, "dentro.txt")));
            int fuera; string err;
            List<RocolaOsEntrada> l = RocolaOsArchivos.Lista(datos, dr, out fuera, out err);
            HashSet<string> n = new HashSet<string>(); foreach (RocolaOsEntrada e in l) n.Add(e.Nombre);
            Si("Lista de los datos: sin los dos enlaces que salen (fuga, fuga.txt), contados: " + fuera, !n.Contains("fuga") && !n.Contains("fuga.txt") && fuera == 2 && n.Contains("dentro.txt") && n.Contains("notas.txt"));
            Si("Lista de los datos en la raiz: sin «..»", !n.Contains(".."));
            l = RocolaOsArchivos.Lista(datos, Path.Combine(dr, "sub"), out fuera, out err);
            Si("Lista de sub: con «..» y sin el enlace relativo que sale (arriba)", l.Count == 1 && l[0].Subir && fuera == 1);
            l = RocolaOsArchivos.Lista(datos, Path.Combine(d0, "fuga"), out fuera, out err);
            Si("Lista por el enlace de fuera: no se lee nada y lo dice", l.Count == 0 && err != null);
            l = RocolaOsArchivos.Lista(datos, fr, out fuera, out err);
            Si("Lista de una carpeta de fuera: no", l.Count == 0 && err != null);
            l = RocolaOsArchivos.Lista(usb, ur, out fuera, out err);
            n.Clear(); foreach (RocolaOsEntrada e in l) n.Add(e.Nombre);
            Si("Lista de la USB: carpetas primero, sin los enlaces que salen", l.Count == 4 && l[0].Nombre == "carpeta" && l[0].Carpeta && !n.Contains("fuga") && fuera == 2);

            // ---- leer: solo dentro ----
            Si("LeeTexto por el enlace de fuera: la pagina no lo abre (Dentro lo para antes)", !RocolaOsArchivos.Dentro(dr, Path.Combine(d0, "fuga.txt")));
            RocolaOsArchivos.Texto t = RocolaOsArchivos.LeeTexto(Path.Combine(dr, "notas.txt"), out err);
            Si("LeeTexto: CRLF visto, final con salto, sin CR dentro", t != null && t.Crlf && t.FinalConSalto && t.Contenido == "hola\nmundo");
            File.WriteAllBytes(Path.Combine(d0, "grande.txt"), Encoding.ASCII.GetBytes(new string('x', 70000)));
            Si("LeeTexto: mas de 64 KB no se abre", RocolaOsArchivos.LeeTexto(Path.Combine(dr, "grande.txt"), out err) == null && err.Contains("64"));
            File.WriteAllBytes(Path.Combine(d0, "bin.dat"), new byte[] { 1, 2, 0, 3 });
            Si("LeeTexto: con un byte 0 no es texto", RocolaOsArchivos.LeeTexto(Path.Combine(dr, "bin.dat"), out err) == null);

            // ---- guardar ----
            Si("GuardaTexto: conserva CRLF", RocolaOsArchivos.GuardaTexto(datos, Path.Combine(dr, "notas.txt"), "hola\nmundo2", true, false, true, false) == null
                && File.ReadAllText(Path.Combine(dr, "notas.txt")) == "hola\r\nmundo2\r\n");
            Si("GuardaTexto: una llave nueva queda 0600 (" + 0 + ")", RocolaOsArchivos.GuardaTexto(datos, Path.Combine(dr, "rocola_genius.txt"), "ABC123xyz", false, false, true, true) == null
                && Modo(Path.Combine(dr, "rocola_genius.txt")) == "600" && File.ReadAllText(Path.Combine(dr, "rocola_genius.txt")) == "ABC123xyz\n");
            Si("GuardaTexto: por un enlace (fuga.txt) NO, y lo de fuera sigue igual", RocolaOsArchivos.GuardaTexto(datos, Path.Combine(dr, "fuga.txt"), "pisado", false, false, true, false) != null
                && File.ReadAllText(Path.Combine(f0, "secreto.txt")) == "lo de fuera\n");
            Si("GuardaTexto: dentro de una carpeta de fuera por el enlace (fuga/x.txt) NO", RocolaOsArchivos.GuardaTexto(datos, Path.Combine(dr, "fuga", "x.txt"), "x", false, false, true, false) != null
                && !File.Exists(Path.Combine(f0, "x.txt")));
            Si("GuardaTexto: con «..» en la ruta (datos/sub/../../FUERA/y.txt) NO", RocolaOsArchivos.GuardaTexto(datos, Path.Combine(dr, "sub", "..", "..", Path.GetFileName(fr), "y.txt"), "y", false, false, true, false) != null
                && !File.Exists(Path.Combine(f0, "y.txt")));
            Si("GuardaTexto: en la USB NO (solo lectura)", RocolaOsArchivos.GuardaTexto(usb, Path.Combine(ur, "leeme.txt"), "pisado", false, false, true, false) != null
                && File.ReadAllText(Path.Combine(u0, "leeme.txt")) == "texto\n");
            Si("GuardaTexto: un archivo que no es llave conserva sus permisos (640)", Prepara640(Path.Combine(dr, "notas.txt"))
                && RocolaOsArchivos.GuardaTexto(datos, Path.Combine(dr, "notas.txt"), "a", true, false, true, false) == null && Modo(Path.Combine(dr, "notas.txt")) == "640");

            // ---- copiar de la USB ----
            string dest;
            string r1 = RocolaOsArchivos.Copia(usb, Path.Combine(ur, "rocola_spotify_api.txt"), datos, dr, false, out dest);
            Si("Copia: la llave de la API web, 0600 y el mismo contenido", r1 == null && Modo(dest) == "600" && File.ReadAllText(dest) == File.ReadAllText(Path.Combine(u0, "rocola_spotify_api.txt")));
            string r2 = RocolaOsArchivos.Copia(usb, Path.Combine(ur, "rocola_spotify.txt"), datos, dr, false, out dest);
            Si("Copia: rocola_spotify.txt de la USB queda como rocola_spotify_llave.txt (0600)", r2 == null && Path.GetFileName(dest) == "rocola_spotify_llave.txt" && Modo(dest) == "600" && !File.Exists(Path.Combine(dr, "rocola_spotify.txt")));
            Si("Copia: si ya esta, pregunta (YA_HAY) y no la pisa", RocolaOsArchivos.Copia(usb, Path.Combine(ur, "rocola_spotify_api.txt"), datos, dr, false, out dest) == "YA_HAY");
            Si("Copia: por el enlace de la USB a fuera NO", RocolaOsArchivos.Copia(usb, Path.Combine(ur, "fuga.txt"), datos, dr, false, out dest) != null && !File.Exists(Path.Combine(dr, "fuga.txt.copia")));
            Si("Copia: a una carpeta de fuera NO", RocolaOsArchivos.Copia(usb, Path.Combine(ur, "leeme.txt"), datos, fr, false, out dest) != null && !File.Exists(Path.Combine(f0, "leeme.txt")));
            Si("Copia: a traves del enlace de los datos (fuga/) NO", RocolaOsArchivos.Copia(usb, Path.Combine(ur, "leeme.txt"), datos, Path.Combine(dr, "fuga"), false, out dest) != null && !File.Exists(Path.Combine(f0, "leeme.txt")));
            Si("Copia: una carpeta NO (solo archivos)", RocolaOsArchivos.Copia(usb, Path.Combine(ur, "carpeta"), datos, dr, false, out dest) != null);
            Si("Copia: desde fuera de la USB NO", RocolaOsArchivos.Copia(usb, Path.Combine(fr, "secreto.txt"), datos, dr, false, out dest) != null && !File.Exists(Path.Combine(dr, "secreto.txt")));

            // ---- renombrar y borrar ----
            Si("Renombra: con «/» NO", RocolaOsArchivos.Renombra(datos, Path.Combine(dr, "notas.txt"), "../notas.txt") != null && File.Exists(Path.Combine(dr, "notas.txt")));
            Si("Renombra: «..» NO", RocolaOsArchivos.Renombra(datos, Path.Combine(dr, "notas.txt"), "..") != null);
            Si("Renombra: a un nombre que ya esta NO", RocolaOsArchivos.Renombra(datos, Path.Combine(dr, "notas.txt"), "rocola_genius.txt") != null);
            Si("Renombra: en la USB NO", RocolaOsArchivos.Renombra(usb, Path.Combine(ur, "leeme.txt"), "otro.txt") != null && File.Exists(Path.Combine(u0, "leeme.txt")));
            Si("Renombra: a un nombre de llave, queda 0600", RocolaOsArchivos.Renombra(datos, Path.Combine(dr, "notas.txt"), "mi_token.txt") == null && Modo(Path.Combine(dr, "mi_token.txt")) == "600");
            Si("Borra: el enlace que sale se borra EL ENLACE, lo de fuera sigue", RocolaOsArchivos.Borra(datos, Path.Combine(dr, "fuga.txt")) == null && File.Exists(Path.Combine(f0, "secreto.txt")) && !File.Exists(Path.Combine(d0, "fuga.txt")));
            Si("Borra: la carpeta de fuera a traves del enlace NO (borra el enlace, no lo de dentro)", RocolaOsArchivos.Borra(datos, Path.Combine(dr, "fuga")) == null && File.Exists(Path.Combine(f0, "secreto.txt")));
            Si("Borra: una carpeta con cosas NO", RocolaOsArchivos.Borra(datos, Path.Combine(dr, "sub")) != null && Directory.Exists(Path.Combine(dr, "sub")));
            Si("Borra: la raiz de los datos NO", RocolaOsArchivos.Borra(datos, dr) != null && Directory.Exists(dr));
            Si("Borra: en la USB NO", RocolaOsArchivos.Borra(usb, Path.Combine(ur, "leeme.txt")) != null && File.Exists(Path.Combine(u0, "leeme.txt")));
            Si("Borra: fuera con «..» NO", RocolaOsArchivos.Borra(datos, Path.Combine(dr, "sub", "..", "..", Path.GetFileName(fr), "secreto.txt")) != null && File.Exists(Path.Combine(f0, "secreto.txt")));

            // ---- las llaves y lo tapado ----
            Si("EsLlave: las conocidas y por su nombre", RocolaOsArchivos.EsLlave(dr + "/rocola_github.txt", dr) && RocolaOsArchivos.EsLlave(dr + "/rocola_buzon.txt", dr)
                && RocolaOsArchivos.EsLlave(dr + "/MI_API_KEY.txt", dr) && RocolaOsArchivos.EsLlave(dr + "/claude_token", dr) && RocolaOsArchivos.EsLlave(dr + "/soloist/sesion.json", dr));
            Si("EsLlave: rocola_config.txt y rocola_spotify.txt (la bitacora) no", !RocolaOsArchivos.EsLlave(dr + "/rocola_config.txt", dr) && !RocolaOsArchivos.EsLlave(dr + "/rocola_spotify.txt", dr));
            Si("Tapa: «client_secret=SECRETO123» -> «cli•••••••••••••••••123» (misma medida)", RocolaOsArchivos.Tapa("client_secret=SECRETO123") == "cli" + new string('•', 18) + "123");
            Si("Tapa: menos de 16, todo tapado (una contrasena de 8 no deja ver 6)", RocolaOsArchivos.Tapa("abcdef") == "••••••" && RocolaOsArchivos.Tapa("") == ""
                && RocolaOsArchivos.Tapa("pass1234") == new string('•', 8) && RocolaOsArchivos.Tapa("abcdefghijklmno") == new string('•', 15)
                && RocolaOsArchivos.Tapa("abcdefghijklmnop") == "abc" + new string('•', 10) + "nop");
            // una tuberia (FIFO) en la USB: ni se abre ni se copia (abrirla dejaba colgada la pantalla esperando a quien escriba)
            string fifo = Path.Combine(u0, "tuberia.txt");
            bool hayFifo = false;
            try { using (System.Diagnostics.Process p = System.Diagnostics.Process.Start("mkfifo", fifo)) { p.WaitForExit(5000); hayFifo = p.ExitCode == 0; } } catch { }
            if (hayFifo)
            {
                Si("EsArchivoNormal: un archivo si, una tuberia, una carpeta y uno que no esta NO", RocolaOsArchivos.EsArchivoNormal(Path.Combine(ur, "leeme.txt"))
                    && !RocolaOsArchivos.EsArchivoNormal(fifo) && !RocolaOsArchivos.EsArchivoNormal(ur) && !RocolaOsArchivos.EsArchivoNormal(Path.Combine(ur, "no-esta")));
                Si("LeeTexto: una tuberia no se abre (y no se queda esperando)", RocolaOsArchivos.LeeTexto(fifo, out err) == null && err != null);
                Si("Copia: una tuberia NO", RocolaOsArchivos.Copia(usb, fifo, datos, dr, false, out dest) != null && !File.Exists(Path.Combine(dr, "tuberia.txt")));
                try { File.Delete(fifo); } catch { }
            }
            else Si("mkfifo para probar la tuberia", false);
            Si("NombreNoVale: los que no valen", RocolaOsArchivos.NombreNoVale("a/b") != null && RocolaOsArchivos.NombreNoVale(".") != null && RocolaOsArchivos.NombreNoVale(" x") != null
                && RocolaOsArchivos.NombreNoVale("x\ty") != null && RocolaOsArchivos.NombreNoVale(new string('a', 256)) != null && RocolaOsArchivos.NombreNoVale("rocola_genius.txt") == null);
            Si("Lo de fuera, intacto al final", File.ReadAllText(Path.Combine(f0, "secreto.txt")) == "lo de fuera\n" && Directory.GetFileSystemEntries(f0).Length == 1);
            // el teclado del editor (distribucion de EE. UU.)
            Si("CaracterUs: a A 5 % - _ = + / ? [ { ; : ' \" , < . > ` ~ \\ |", RocolaOsEditor.CaracterUs(Tecla.A, false) == 'a' && RocolaOsEditor.CaracterUs(Tecla.A, true) == 'A'
                && RocolaOsEditor.CaracterUs(Tecla.D5, false) == '5' && RocolaOsEditor.CaracterUs(Tecla.D5, true) == '%' && RocolaOsEditor.CaracterUs(Tecla.OemMinus, true) == '_'
                && RocolaOsEditor.CaracterUs(Tecla.Oemplus, false) == '=' && RocolaOsEditor.CaracterUs(Tecla.OemQuestion, false) == '/' && RocolaOsEditor.CaracterUs(Tecla.OemQuestion, true) == '?'
                && RocolaOsEditor.CaracterUs(Tecla.OemOpenBrackets, true) == '{' && RocolaOsEditor.CaracterUs(Tecla.OemSemicolon, true) == ':' && RocolaOsEditor.CaracterUs(Tecla.OemQuotes, true) == '"'
                && RocolaOsEditor.CaracterUs(Tecla.Oemcomma, true) == '<' && RocolaOsEditor.CaracterUs(Tecla.Oemtilde, true) == '~' && RocolaOsEditor.CaracterUs(Tecla.OemPipe, false) == '\\');
            Console.WriteLine("prueba-archivos: " + bien + " bien, " + mal + " mal");
            return mal == 0 ? 0 : 1;
        }
        private static void Trampa(string enlace, string a) { try { if (File.Exists(enlace) || Directory.Exists(enlace) || new FileInfo(enlace).LinkTarget != null) File.Delete(enlace); } catch { } File.CreateSymbolicLink(enlace, a); }
        private static bool Prepara640(string r) { File.SetUnixFileMode(r, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead); return Modo(r) == "640"; }
    }
}
