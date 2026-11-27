// ROCOLA OS (fase 11): LA ORDEN DE PRUEBA DE LA HOJA «IMAGEN» (FASE11_hoja-imagen.md; la usa Pruebas/RocolaOs/imagen/prueba.sh).
//   rocola-xp --prueba-imagen
// Las fechas propias de rocola_fechas.txt (RocolaOs.VentanaImagenFechas.cs: leer, agregar, quitar, guardar) y las tablas de la hoja: que cada
// clave de sus interruptores exista y sea de si/no (declarada por el cielo, la letra o el andamio) y que cada idea nombre una funcion de la hoja.
// Escribe un renglon «BIEN ...» o «MAL ...» por comprobacion y sale con 0 si todas estan bien. Solo escribe en la carpeta de datos de la prueba
// (ROCOLA_DATOS).
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace Rocola
{
    internal static class RocolaOsImagenPrueba
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra() { RocolaOsOrdenes.Si("--prueba-imagen", Corre); }

        private static int bien, mal;
        private static void Si(string que, bool ok) { Console.WriteLine((ok ? "BIEN  " : "MAL   ") + que); if (ok) bien++; else mal++; }

        private static int Corre(string[] a)
        {
            string base0 = "# mis fechas\r\n\r\n31-10;mensaje;Pasen a bailar\r\nbasura sin forma\r\n02-08;aniversario;El Billar\r\n";
            // ---- leer ----
            List<RocolaOsImagenFechas.Entrada> e = RocolaOsImagenFechas.Entradas(base0);
            Si("Entradas: los 3 renglones (sin el comentario ni el vacio), cada uno con su sitio en el archivo", e.Count == 3 && e[0].Renglon == 2 && e[1].Renglon == 3 && e[2].Renglon == 4);
            Si("Entradas: «31-10 · mensaje · Pasen a bailar» y «02-08 · aniversario · El Billar»", e[0].Texto == "31-10 · mensaje · Pasen a bailar" && e[2].Texto == "02-08 · aniversario · El Billar");
            Si("Entradas: el que no vale sale tal cual con «(no vale)» y marcado", !e[1].Vale && e[1].Texto == "basura sin forma (no vale)" && e[0].Vale && e[2].Vale);
            Si("Entradas: un texto vacio o null no da ninguno", RocolaOsImagenFechas.Entradas("").Count == 0 && RocolaOsImagenFechas.Entradas(null).Count == 0);
            Si("Entradas: la marca de UTF-8 al principio no estorba", RocolaOsImagenFechas.Entradas("﻿15-03;cumple;Ana\n").Count == 1);
            Si("Entradas: «5/3» se ve como «05-03»", RocolaOsImagenFechas.Entradas("5/3;cumple;Ana\n")[0].Texto == "05-03 · cumpleaños · Ana");
            // ---- agregar ----
            string n1 = RocolaOsImagenFechas.Agrega(base0, "cumple", "15-03", "Juan");
            Si("Agrega: al final, con su salto de linea, y lo de antes sigue", n1 != null && n1.EndsWith("\n15-03;cumple;Juan\n", StringComparison.Ordinal) && n1.Contains("# mis fechas") && n1.Contains("El Billar"));
            string n2 = RocolaOsImagenFechas.Agrega("31-10;mensaje;Hola", "aniversario", "2-8", "El Billar");
            Si("Agrega: sin salto al final del archivo: lo pone, y «2-8» queda «02-08»", n2 == "31-10;mensaje;Hola\n02-08;aniversario;El Billar\n");
            Si("Agrega: del archivo que no existe (null) hace el primero", RocolaOsImagenFechas.Agrega(null, "cumple", "01-01", "Ana") == "01-01;cumple;Ana\n");
            Si("Agrega: un tipo que la hoja no agrega (fiesta) no vale", RocolaOsImagenFechas.Agrega("", "fiesta", "01-01", "Ana") == null);
            Si("Agrega: una fecha mala (31-02, 32-01, 0-5, texto) no vale", RocolaOsImagenFechas.Agrega("", "cumple", "31-02", "Ana") == null && RocolaOsImagenFechas.Agrega("", "cumple", "32-01", "Ana") == null
                && RocolaOsImagenFechas.Agrega("", "cumple", "0-5", "Ana") == null && RocolaOsImagenFechas.Agrega("", "cumple", "hoy", "Ana") == null);
            Si("Agrega: el 29-02 vale (ano bisiesto)", RocolaOsImagenFechas.Agrega("", "cumple", "29-02", "Ana") == "29-02;cumple;Ana\n");
            Si("Agrega: un nombre vacio, con «;», «|», «#» o de mas de 60 letras no vale",
                RocolaOsImagenFechas.Agrega("", "cumple", "01-01", "  ") == null && RocolaOsImagenFechas.Agrega("", "cumple", "01-01", "A;B") == null
                && RocolaOsImagenFechas.Agrega("", "cumple", "01-01", "A|B") == null && RocolaOsImagenFechas.Agrega("", "cumple", "01-01", "#A") == null
                && RocolaOsImagenFechas.Agrega("", "cumple", "01-01", new string('x', 61)) == null && RocolaOsImagenFechas.Agrega("", "cumple", "01-01", new string('x', 60)) != null);
            Si("Agrega: el nombre con acentos y espacios se guarda recortado: «  María José » -> «María José»", RocolaOsImagenFechas.Agrega("", "cumple", "01-01", "  María José ") == "01-01;cumple;María José\n");
            // lo que se agrega lo lee el cielo (el mismo lector de rocola_fechas.txt)
            RocolaOsFechasArchivo f = RocolaOsFechasArchivo.Lee(n1);
            bool esta = false; foreach (RocolaOsFechasArchivo.Renglon r in f.Renglones) if (r.Dia == 15 && r.Mes == 3 && r.Tipo == "cumple" && r.Texto == "Juan") esta = true;
            Si("Lo agregado lo lee el cielo (RocolaOsFechasArchivo.Lee) como un cumple del 15-03 de Juan, sin renglones malos nuevos", esta && f.Malos.Length == 1);
            RocolaOsCalendarioHoy hoy = RocolaOsCalendario.De(new DateTime(2026, 3, 15, 12, 0, 0), f);
            Si("Y ese dia el calendario trae «¡Feliz cumpleaños, Juan!»", hoy.Propias.Length == 1 && hoy.Propias[0].Mensaje == "¡Feliz cumpleaños, Juan!");
            // ---- quitar ----
            string q = RocolaOsImagenFechas.Quita(base0, e[2].Renglon);
            Si("Quita: el renglon pedido (el del Billar) y nada mas; lo de antes igual salvo el fin de linea", q == "# mis fechas\n\n31-10;mensaje;Pasen a bailar\nbasura sin forma\n");
            Si("Quita: el que no vale tambien se puede quitar", RocolaOsImagenFechas.Quita(base0, e[1].Renglon) == "# mis fechas\n\n31-10;mensaje;Pasen a bailar\n02-08;aniversario;El Billar\n");
            Si("Quita: un renglon que no hay (-1, 99) da null", RocolaOsImagenFechas.Quita(base0, -1) == null && RocolaOsImagenFechas.Quita(base0, 99) == null);
            // ---- guardar y leer (el archivo de verdad, en ROCOLA_DATOS) ----
            string dir = Environment.GetEnvironmentVariable("ROCOLA_DATOS");
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                string ruta = Path.Combine(dir, RocolaOsCielo.ArchivoFechas);
                try { File.Delete(ruta); } catch { }
                Si("Lee: sin archivo es null", RocolaOsImagenFechas.Lee() == null);
                Si("Guarda: escribe el archivo entero, sin marca de UTF-8, y no deja el temporal", RocolaOsImagenFechas.Guarda(n1) && File.ReadAllText(ruta) == n1 && !File.Exists(ruta + ".nuevo")
                    && File.ReadAllBytes(ruta)[0] == (byte)'#');
                Si("Lee: lo guardado", RocolaOsImagenFechas.Lee() == n1);
                Si("Guarda: otra vez sobre el de antes (cambia el archivo)", RocolaOsImagenFechas.Guarda(q) && RocolaOsImagenFechas.Lee() == q);
            }
            else Si("ROCOLA_DATOS puesto (sin el no se prueba el archivo)", false);
            // ---- las tablas de la hoja ----
            ImagenTablas(new[] { Form1.ImagenDia, Form1.ImagenFiestas, Form1.ImagenClima, Form1.ImagenVidrio });
            Console.WriteLine();
            Console.WriteLine((mal == 0 ? "TODO BIEN: " : "FALLAN: ") + bien + " bien, " + mal + " mal");
            return mal == 0 ? 0 : 1;
        }

        // cada fila: su clave esta declarada y es de si/no; la primera es su funcion (nombre de una funcion registrada), las demas dependen de ella
        private static void ImagenTablas(Form1.ImagenFila[][] tablas)
        {
            int filas = 0; bool declaradas = true, madres = true, ideas = true, repetidas = false;
            HashSet<string> vistas = new HashSet<string>();
            foreach (Form1.ImagenFila[] t in tablas)
            {
                madres &= t[0].Funcion == null && RocolaOs.Busca(t[0].Clave) != null;
                for (int i = 0; i < t.Length; i++)
                {
                    filas++;
                    RocolaOsClave c = RocolaOs.ClaveDe(t[i].Clave);
                    declaradas &= c != null && c.Vale("1") && c.Vale("0") && !c.Vale("2");
                    if (!vistas.Add(t[i].Clave)) repetidas = true;
                    if (i > 0) ideas &= t[i].Funcion == t[0].Clave;
                }
            }
            Si("Tablas: " + filas + " interruptores, todas sus claves declaradas y de si o no (1 o 0)", declaradas && filas == 7 + 7 + 5 + 7);
            Si("Tablas: la primera de cada grupo es una funcion registrada (cielo, fiestas, clima, vidrio) y las demas dependen de ella", madres && ideas);
            Si("Tablas: ninguna clave repetida", !repetidas);
            Si("Tablas: la letra en el cielo (letra_cielo) es una funcion registrada de si o no", RocolaOs.Busca(Form1.ImagenLetra.Clave) != null && RocolaOs.ClaveDe(Form1.ImagenLetra.Clave) != null);
            // las 6+6+4+6 ideas del plan, todas en la hoja: las claves de las cuatro funciones menos la propia y menos ciudad_bar
            foreach (string func in new[] { "cielo", "fiestas", "clima", "vidrio" })
            {
                RocolaOsFuncion fu = RocolaOs.Busca(func);
                int n = 0;
                foreach (RocolaOsClave c in fu.Claves) if (c.Nombre != "ciudad_bar") n++;
                int enHoja = 0;
                foreach (Form1.ImagenFila[] t in tablas) if (t[0].Clave == func) enHoja = t.Length - 1;
                Si("Tablas: «" + func + "»: las " + n + " ideas de su funcion estan todas en la hoja (" + enHoja + ")", n == enHoja);
            }
        }
    }
}
