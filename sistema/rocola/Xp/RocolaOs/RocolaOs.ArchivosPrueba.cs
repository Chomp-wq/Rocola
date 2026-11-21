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
            RocolaOsRaiz datos = new RocolaOsRaiz { Nombre = "datos", Ruta = dr, Escribe = true, Tipo = RocolaOsArchivos.TDatos, Clave = dr };
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
            // ---- las descargas y la papelera (21-11-2026) ----
            Papelera(d0, dr, u0, ur, f0, fr, usb);
            Si("Lo de fuera, intacto despues de la papelera", File.ReadAllText(Path.Combine(f0, "secreto.txt")) == "lo de fuera\n" && Directory.GetFileSystemEntries(f0).Length == 1);
            Console.WriteLine("prueba-archivos: " + bien + " bien, " + mal + " mal");
            return mal == 0 ? 0 : 1;
        }
        // LA PAPELERA Y LAS DESCARGAS (la carpeta de datos de la prueba es la de ROCOLA_DATOS, la misma que DATOS)
        private static void Papelera(string d0, string dr, string u0, string ur, string f0, string fr, RocolaOsRaiz usb)
        {
            string err; int fuera;
            Si("La carpeta de datos de la prueba es la de ROCOLA_DATOS (" + Form1.CarpetaDatos + ")", RocolaOsArchivos.Real(Form1.CarpetaDatos) == dr);
            RocolaOsRaiz datos = RocolaOsArchivos.Datos(), desc = RocolaOsArchivos.Descargas(), pap = RocolaOsArchivos.Papelera();
            string pr = pap.Ruta, der = desc.Ruta;
            Si("Descargas y Papelera: carpetas de verdad dentro de los datos, 0700; las descargas se escriben, la papelera no", der == dr + "/descargas" && pr == dr + "/papelera"
                && Modo(der) == "700" && Modo(pr) == "700" && desc.Escribe && !pap.Escribe && desc.Tipo == RocolaOsArchivos.TDescargas && pap.Tipo == RocolaOsArchivos.TPapelera);
            List<RocolaOsEntrada> l = RocolaOsArchivos.Lista(datos, dr, out fuera, out err);
            bool ve = false; foreach (RocolaOsEntrada e in l) if (e.Nombre == "descargas" || e.Nombre == "papelera") ve = true;
            Si("La lista de los datos no ensena ni las descargas ni la papelera (son sus apartados)", !ve && l.Count > 3);
            Directory.CreateDirectory(Path.Combine(dr, "sub", "hondo"));
            File.WriteAllText(Path.Combine(dr, "sub", "hondo", "nada.txt"), "x");
            Si("...pero en una subcarpeta, una que se llame «papelera» si sale", Prepara(Path.Combine(dr, "sub", "papelera")) && Cuenta(RocolaOsArchivos.Lista(datos, Path.Combine(dr, "sub"), out fuera, out err), "papelera") == 1);
            Directory.Delete(Path.Combine(dr, "sub", "papelera"));

            // tirar y devolver
            File.WriteAllText(Path.Combine(dr, "sub", "a.txt"), "primero\n");
            RocolaOsTirado t; string q;
            string r = RocolaOsPapelera.Manda(datos, Path.Combine(dr, "sub", "a.txt"), out t);
            List<RocolaOsTirado> lp = RocolaOsPapelera.Lista();
            Si("Manda: sub/a.txt sale de su sitio y entra a la papelera (de=datos, ruta=sub/a.txt), su carpeta 0700", r == null && !File.Exists(Path.Combine(dr, "sub", "a.txt"))
                && lp.Count == 1 && lp[0].De == "datos" && lp[0].Ruta == "sub/a.txt" && lp[0].Nombre == "a.txt" && File.ReadAllText(lp[0].Cosa) == "primero\n" && Modo(lp[0].Dir) == "700");
            bool otra;
            r = RocolaOsPapelera.Devuelve(lp[0], out q, out otra);
            Si("Devuelve: a sub/a.txt, igual, y la papelera queda vacia", r == null && q == Path.Combine(dr, "sub", "a.txt") && !otra && File.ReadAllText(q) == "primero\n" && RocolaOsPapelera.Lista().Count == 0 && Directory.GetFileSystemEntries(pr).Length == 0);
            // nombre repetido: no se pisa
            RocolaOsPapelera.Manda(datos, Path.Combine(dr, "sub", "a.txt"), out t);
            File.WriteAllText(Path.Combine(dr, "sub", "a.txt"), "segundo\n");
            r = RocolaOsPapelera.Devuelve(RocolaOsPapelera.Lista()[0], out q, out otra);
            Si("Devuelve con el nombre ocupado: «a (devuelto).txt», sin pisar el de ahora", r == null && Path.GetFileName(q) == "a (devuelto).txt" && File.ReadAllText(q) == "primero\n" && File.ReadAllText(Path.Combine(dr, "sub", "a.txt")) == "segundo\n");
            File.WriteAllText(Path.Combine(dr, "sub", "a.txt"), "segundo\n");
            RocolaOsPapelera.Manda(datos, Path.Combine(dr, "sub", "a.txt"), out t);
            File.WriteAllText(Path.Combine(dr, "sub", "a.txt"), "tercero\n");
            r = RocolaOsPapelera.Devuelve(RocolaOsPapelera.Lista()[0], out q, out otra);
            Si("...y otra vez: «a (devuelto 2).txt»; los otros dos, intactos", r == null && Path.GetFileName(q) == "a (devuelto 2).txt" && File.ReadAllText(q) == "segundo\n"
                && File.ReadAllText(Path.Combine(dr, "sub", "a.txt")) == "tercero\n" && File.ReadAllText(Path.Combine(dr, "sub", "a (devuelto).txt")) == "primero\n");
            Si("NombreLibre: sin extension y con punto al principio", Path.GetFileName(RocolaOsPapelera.NombreLibre(Path.Combine(dr, "sub"), "a.txt")) == "a (devuelto 3).txt"
                && Path.GetFileName(RocolaOsPapelera.NombreLibre(dr, "notas.txt")) == "notas.txt");
            File.WriteAllText(Path.Combine(dr, "m1.txt"), "uno\n"); File.WriteAllText(Path.Combine(dr, "m2.txt"), "dos\n");
            bool lanzo = false; try { RocolaOsPapelera.Mueve(Path.Combine(dr, "m1.txt"), Path.Combine(dr, "m2.txt")); } catch (IOException) { lanzo = true; }
            Si("Mueve (renameat2 RENAME_NOREPLACE): a un nombre que ya esta NO, y ninguno cambia", lanzo && File.ReadAllText(Path.Combine(dr, "m1.txt")) == "uno\n" && File.ReadAllText(Path.Combine(dr, "m2.txt")) == "dos\n");
            File.Delete(Path.Combine(dr, "m1.txt")); File.Delete(Path.Combine(dr, "m2.txt"));
            // la carpeta de antes ya no esta: a la raiz de su apartado
            Directory.CreateDirectory(Path.Combine(dr, "sub2")); File.WriteAllText(Path.Combine(dr, "sub2", "x.txt"), "equis\n");
            RocolaOsPapelera.Manda(datos, Path.Combine(dr, "sub2", "x.txt"), out t);
            Directory.Delete(Path.Combine(dr, "sub2"));
            r = RocolaOsPapelera.Devuelve(RocolaOsPapelera.Lista()[0], out q, out otra);
            Si("Devuelve sin su carpeta: a la raiz de los datos, y lo dice", r == null && otra && q == Path.Combine(dr, "x.txt") && File.ReadAllText(q) == "equis\n");
            // una carpeta vacia si; una con cosas no; la raiz, las descargas y la papelera mismas no; en la USB no; en la papelera no
            Directory.CreateDirectory(Path.Combine(dr, "vacia"));
            Si("Manda: una carpeta vacia si (y se devuelve)", RocolaOsPapelera.Manda(datos, Path.Combine(dr, "vacia"), out t) == null && t.Carpeta && !Directory.Exists(Path.Combine(dr, "vacia"))
                && RocolaOsPapelera.Devuelve(t, out q, out otra) == null && Directory.Exists(Path.Combine(dr, "vacia")));
            Si("Manda: una carpeta con cosas NO, la raiz NO, las descargas y la papelera NO", RocolaOsPapelera.Manda(datos, Path.Combine(dr, "sub"), out t) != null && Directory.Exists(Path.Combine(dr, "sub"))
                && RocolaOsPapelera.Manda(datos, dr, out t) != null && RocolaOsPapelera.Manda(datos, der, out t) != null && RocolaOsPapelera.Manda(datos, pr, out t) != null
                && Directory.Exists(der) && Directory.Exists(pr) && RocolaOsPapelera.Lista().Count == 0);
            Si("Manda: en la USB NO y desde la papelera NO", RocolaOsPapelera.Manda(usb, Path.Combine(ur, "leeme.txt"), out t) != null && File.Exists(Path.Combine(u0, "leeme.txt"))
                && RocolaOsPapelera.Manda(pap, Path.Combine(pr, "x"), out t) != null);
            Si("Manda: fuera con «..» NO", RocolaOsPapelera.Manda(datos, Path.Combine(dr, "sub", "..", "..", Path.GetFileName(fr), "secreto.txt"), out t) != null && File.Exists(Path.Combine(f0, "secreto.txt")));
            // las llaves: 0600 tambien en la papelera; por su ruta de antes (soloist/) aunque el nombre no lo diga
            Si("Manda: la llave (rocola_genius.txt, 0600) sigue 0600 en la papelera y se sabe llave", RocolaOsPapelera.Manda(datos, Path.Combine(dr, "rocola_genius.txt"), out t) == null
                && Modo(t.Cosa) == "600" && LlaveEnLista("rocola_genius.txt"));
            Directory.CreateDirectory(Path.Combine(dr, "soloist")); File.WriteAllText(Path.Combine(dr, "soloist", "sesion.json"), "{\"llave\":\"SECRETODEPRUEBA\"}\n");
            RocolaOsArchivos.Permisos(Path.Combine(dr, "soloist", "sesion.json"), true);
            Si("Manda: soloist/sesion.json es llave tambien en la papelera (por la ruta de antes), 0600", RocolaOsPapelera.Manda(datos, Path.Combine(dr, "soloist", "sesion.json"), out t) == null
                && t.Llave && LlaveEnLista("sesion.json") && Modo(t.Cosa) == "600");
            // el enlace que sale: a la papelera va EL ENLACE; vaciar lo borra a el, no a lo de fuera
            Trampa(Path.Combine(dr, "fuga2"), f0);
            string rf = RocolaOsPapelera.Manda(datos, Path.Combine(dr, "fuga2"), out t); if (rf != null) Console.WriteLine("   (" + rf + ")");
            Si("Manda: el enlace que sale va a la papelera el enlace (lo de fuera sigue)", rf == null && !t.Carpeta
                && new FileInfo(t.Cosa).LinkTarget != null && File.Exists(Path.Combine(f0, "secreto.txt")));
            // las descargas: tirar y devolver a las descargas; copiar a los datos
            File.WriteAllText(Path.Combine(der, "bajado.txt"), "de internet\n");
            r = RocolaOsPapelera.Manda(desc, Path.Combine(der, "bajado.txt"), out t);
            Si("Manda desde las descargas: de=descargas", r == null && t.De == "descargas" && !File.Exists(Path.Combine(der, "bajado.txt")));
            r = RocolaOsPapelera.Devuelve(t, out q, out otra);
            Si("Devuelve a las descargas", r == null && q == Path.Combine(der, "bajado.txt") && File.ReadAllText(q) == "de internet\n");
            string dest;
            Si("Copia de las descargas a los datos (y la de las descargas sigue)", RocolaOsArchivos.Copia(desc, Path.Combine(der, "bajado.txt"), datos, dr, false, out dest) == null
                && File.ReadAllText(Path.Combine(dr, "bajado.txt")) == "de internet\n" && File.Exists(Path.Combine(der, "bajado.txt")));
            File.WriteAllText(Path.Combine(der, "mi_api_key.txt"), "client_secret=SECRETODEPRUEBA0987654321\n");
            Si("Copia de las descargas: una llave queda 0600", RocolaOsArchivos.Copia(desc, Path.Combine(der, "mi_api_key.txt"), datos, dr, false, out dest) == null && Modo(dest) == "600");
            Si("Copia: a la papelera NO, a las descargas NO (desde la USB), desde la papelera NO", RocolaOsArchivos.Copia(usb, Path.Combine(ur, "leeme.txt"), datos, pr, false, out dest) != null
                && RocolaOsArchivos.Copia(usb, Path.Combine(ur, "leeme.txt"), datos, der, false, out dest) != null && !File.Exists(Path.Combine(der, "leeme.txt"))
                && RocolaOsArchivos.Copia(pap, RocolaOsPapelera.Lista()[0].Cosa, datos, dr, false, out dest) != null);
            Si("GuardaTexto en las descargas (se editan los chicos)", RocolaOsArchivos.GuardaTexto(desc, Path.Combine(der, "bajado.txt"), "editado", false, false, true, false) == null && File.ReadAllText(Path.Combine(der, "bajado.txt")) == "editado\n");
            // una ruta de antes manipulada («../..») no saca nada de los datos
            File.WriteAllText(Path.Combine(dr, "y.txt"), "ye\n");
            RocolaOsPapelera.Manda(datos, Path.Combine(dr, "y.txt"), out t);
            RocolaOsPapelera.EscribeInfo(t.Dir, "datos", "../../" + Path.GetFileName(fr) + "/y.txt", t.Cuando, t.Bytes, false);
            r = RocolaOsPapelera.Devuelve(RocolaOsPapelera.Lista()[0], out q, out otra);
            Si("Devuelve con la ruta de antes manipulada («../../FUERA/y.txt»): queda en la raiz de los datos, nada fuera", r == null && q == Path.Combine(dr, "y.txt") && !File.Exists(Path.Combine(f0, "y.txt")));

            // borrar ya y vaciar
            int antes = RocolaOsPapelera.Lista().Count;
            Si("BorraYa: una cosa (" + antes + " -> " + (antes - 1) + ")", antes >= 3 && RocolaOsPapelera.BorraYa(RocolaOsPapelera.Lista()[0]) == null && RocolaOsPapelera.Lista().Count == antes - 1);
            int n = RocolaOsPapelera.Vacia(out err);
            Si("Vacia: todo (" + n + "), y lo de fuera sigue (el enlace se borro el)", err == null && n == antes - 1 && RocolaOsPapelera.Lista().Count == 0 && Directory.GetFileSystemEntries(pr).Length == 0 && File.Exists(Path.Combine(f0, "secreto.txt")));

            // vaciarse sola: 30 dias (por cuando se tiro) y el tope (lo mas viejo primero)
            long ahora = RocolaOsPapelera.AhoraMs, dia = RocolaOsPapelera.DiaMs;
            RocolaOsTirado v31 = Tira(datos, dr, "viejo31.txt", 100, ahora - 31 * dia), v29 = Tira(datos, dr, "viejo29.txt", 100, ahora - 29 * dia);
            int vj, pt;
            RocolaOsPapelera.Purga(ahora, 64L << 20, out vj, out pt);
            Si("Purga: lo de hace 31 dias se va, lo de hace 29 se queda (" + vj + " viejas, " + pt + " por el tope)", vj == 1 && pt == 0 && !Directory.Exists(v31.Dir) && Directory.Exists(v29.Dir));
            RocolaOsPapelera.Purga(ahora + 2 * dia, 64L << 20, out vj, out pt);
            Si("Purga: dos dias despues (fecha falsa), la de 29 ya tiene 31: fuera", vj == 1 && !Directory.Exists(v29.Dir));
            RocolaOsTirado a1 = Tira(datos, dr, "t1.bin", 40000, ahora - 3000), a2 = Tira(datos, dr, "t2.bin", 40000, ahora - 2000), a3 = Tira(datos, dr, "t3.bin", 40000, ahora - 1000);
            RocolaOsPapelera.Purga(ahora, 100000, out vj, out pt);
            Si("Purga por el tope (100 000 bytes, tres de 40 000): se va la mas vieja (t1), quedan t2 y t3", vj == 0 && pt == 1 && !Directory.Exists(a1.Dir) && Directory.Exists(a2.Dir) && Directory.Exists(a3.Dir));
            RocolaOsPapelera.Purga(ahora, 30000, out vj, out pt);
            Si("Purga con un tope menor que cada una: se van todas", pt == 2 && RocolaOsPapelera.Lista().Count == 0);
            // lo que quedo a medias (sin info.txt): fuera si tiene mas de un minuto; uno de ahora se queda (otro lo esta tirando)
            string rota = Path.Combine(pr, "20260101000000000-1-1"), nueva = Path.Combine(pr, "20260101000000000-1-2");
            Directory.CreateDirectory(Path.Combine(rota, "c")); Directory.CreateDirectory(Path.Combine(nueva, "c"));
            Directory.SetLastWriteTimeUtc(rota, DateTime.UtcNow.AddMinutes(-5));
            RocolaOsPapelera.Purga(ahora, 64L << 20, out vj, out pt);
            Si("Purga: lo que quedo a medias de hace 5 min se va; el de ahora se queda", !Directory.Exists(rota) && Directory.Exists(nueva));
            Directory.Delete(nueva, true);
            // la papelera que es un enlace (a fuera): no se usa
            Directory.Move(pr, pr + ".bien");
            File.CreateSymbolicLink(pr, f0);
            File.WriteAllText(Path.Combine(dr, "z.txt"), "zeta\n");
            Si("La papelera es un enlace a fuera: no se tira nada (z.txt sigue) y fuera no cambia", RocolaOsPapelera.Manda(datos, Path.Combine(dr, "z.txt"), out t) != null && File.Exists(Path.Combine(dr, "z.txt"))
                && Directory.GetFileSystemEntries(f0).Length == 1 && RocolaOsPapelera.Lista().Count == 0);
            File.Delete(pr); Directory.Move(pr + ".bien", pr);
        }
        private static int Cuenta(List<RocolaOsEntrada> l, string n) { int k = 0; foreach (RocolaOsEntrada e in l) if (e.Nombre == n) k++; return k; }
        private static bool Prepara(string d) { Directory.CreateDirectory(d); return true; }
        private static bool LlaveEnLista(string n) { foreach (RocolaOsTirado x in RocolaOsPapelera.Lista()) if (x.Nombre == n) return x.Llave; return false; }
        private static RocolaOsTirado Tira(RocolaOsRaiz datos, string dr, string n, int bytes, long cuando)
        {
            File.WriteAllBytes(Path.Combine(dr, n), new byte[bytes]);
            RocolaOsTirado t; RocolaOsPapelera.Manda(datos, Path.Combine(dr, n), out t);
            RocolaOsPapelera.EscribeInfo(t.Dir, "datos", n, cuando, bytes, false);
            return t;
        }
        private static void Trampa(string enlace, string a) { try { if (File.Exists(enlace) || Directory.Exists(enlace) || new FileInfo(enlace).LinkTarget != null) File.Delete(enlace); } catch { } File.CreateSymbolicLink(enlace, a); }
        private static bool Prepara640(string r) { File.SetUnixFileMode(r, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead); return Modo(r) == "640"; }
    }
}
