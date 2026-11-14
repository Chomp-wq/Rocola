// TRAER LO QUE LA ROCOLA DE XP YA APRENDIO (Rocola OS; no es de XP): `rocola-xp --importa-xp CARPETA_XP [DISCO...]`.
//
// Decision del dueno del 14-11-2026: rocola-xp es el unico programa de Rocola OS y empieza en su carpeta propia de datos (/rocola/datos/xp)
// con lo aprendido por la rocola de XP. Como rocola-xp ES la rocola de XP, sus tablas se usan tal cual: solo cambian las rutas de Windows
// («C:\Rocola\musica\...») por las de Rocola OS («/rocola/disco/Rocola/musica/...»), buscando cada archivo en los discos montados sin
// distinguir mayusculas (como Motor/ImportaXP.cs del programa anterior, SinMayusculas); lo que ya no esta se queda fuera. La carpeta de XP
// solo se lee. El estado del dia (creditos y cola) no se trae.
//
// EL CONTRATO CON EL LANZADOR (/usr/bin/rocola), en cada arranque del programa y antes de abrirlo:
//     ROCOLA_DATOS=/rocola/datos/xp /usr/lib/rocola-xp/rocola-xp --importa-xp /run/rocola-datos-xp
//  - CARPETA_XP: la carpeta de datos de la rocola de XP, o un archivo cuya primera linea la dice (/run/rocola-datos-xp, de rocola-discos).
//  - DISCO...: donde buscar la musica. Sin ellos, los de siempre: /rocola/discos/* (los que monta rocola-discos, en solo lectura) y
//    /rocola/disco (el disco donde va instalado Rocola OS), como el programa anterior (Motor/Arranque.cs).
//  - ROCOLA_DATOS: la carpeta propia de rocola-xp, donde se importa (obligatoria: nunca se importa a la carpeta del programa).
//  - Sale con 0 si importo, o si no habia nada que hacer: rocola_importado_xp.txt ya dice esa carpeta (sale enseguida, sin tocar nada) o
//    la carpeta de destino es la misma de XP (con «datos=xp»). Si fallo, sale con otro numero y una linea en la salida de errores (2: mal
//    llamado; 3: no esta la carpeta de XP; 4: no se pudo leer, escribir, o no se encontro en los discos ni una cancion de las tablas).
//  - Nunca deja un archivo a medias: todo se arma en una carpeta de paso (.importa-xp.tmp) y solo al final cada archivo cambia de nombre a
//    su sitio; rocola_importado_xp.txt va el ultimo (sin el, la siguiente vez se vuelve a intentar).
// Se contesta ANTES de entrar al Main de XP (como Nativo/VersionRocolaOs.cs, al cargarse el programa): sin la instancia unica, sin
// pantalla ni sonido, y en la carpeta de datos no toca nada fuera de lo suyo.
//
// QUE SE TRAE (lo lee el codigo de XP tal cual; quien lo lee, entre parentesis):
//  - lo aprendido y medido, con sus rutas traducidas: rocola_sonoridad.txt (Sonoridad.cs), rocola_volumen.txt (Form1.Sonido.cs),
//    rocola_eq.txt y rocola_stats.txt (Form1.Datos.cs), rocola_fuentes.txt (FuentesMedidas.cs), rocola_duracion.txt, rocola_afondo.txt y
//    rocola_ia_hechas.txt (Form1.Biblioteca.cs), rocola_billar_nivel.txt (VolumenParejo.cs) y rocola_billar_bitacora.txt (BitacoraBillar.cs:
//    solo lleva el nombre del archivo, va tal cual). Sin el archivo, su copia .bak (como lo recupera XP: RecuperaGuardadosCortados).
//  - EL SELLO DE CADA CANCION (rocola_fuentes.txt: largo|creada|escrita|...): rocola-xp comprueba con el que la medida es del archivo que
//    hay hoy (CompruebaFuenteMedida); si no cuadra, la adopta pero la vuelve a medir. «escrita» y el largo valen igual en Linux (NTFS guarda
//    la fecha en UTC, en cientos de nanosegundos, y ntfs3 la da tal cual), pero «creada» no: en Linux .NET no lee la fecha de creacion y da
//    la mas vieja de las de cambio (comprobado aqui: FileInfo.CreationTimeUtc = la de escritura en ext4). Por eso, cuando el largo y la
//    escrita son los mismos (es el mismo archivo, sin tocar), se pone la «creada» que rocola-xp vera en Linux; si no, se deja la de XP (otro
//    archivo: rocola-xp la volvera a medir, como XP).
//  - los botones (rocola_teclas.txt, tal cual: los numeros de las teclas son los de Windows en los dos).
//  - la configuracion (rocola_config.txt): las lineas «dir=» de Ctrl+F2 con la carpeta traducida, o quitadas si no esta; «respaldo_dir=»
//    quitada (en Rocola OS los otros discos van en solo lectura y el respaldo fallaria cada dia; en el disco de XP podaria los respaldos de
//    la rocola de XP a los tres mas nuevos, PruneBackups); lo demas tal cual. Junto con la que ya hubiera (ver JuntaConfig).
//  - los modelos de IA, tal cual: IAS/ a IAS/, y los sueltos de la carpeta a la carpeta (al arrancar, rocola-xp los pasa a IAS/ como XP:
//    OrdenaIas). Sin el material para entrenar (rocola_ia_datos.*: no es un modelo, puede pesar gigas y no lleva rutas; se queda en la
//    carpeta de XP. Con rocola_ia_hechas.txt traido, rocola-xp no vuelve a sacar material de lo que XP ya cosecho).
// QUE NO (y por que):
//  - rocola_estado.txt (creditos y cola: el dia) y rocola_noche.txt (lo pedido esta noche: a las 8 de la manana empieza de cero).
//  - rocola_error.txt, rocola_diagnostico*.txt, rocola_calidad.txt (informes de XP; rocola-xp escribe los suyos) y rocola_incompatibles.txt
//    (lo que el descompresor de XP no pudo abrir: solo sirve para no apuntarlo dos veces, y en Linux el descompresor es otro).
//  - las caches que XP rehace solas y cuya clave depende de la ruta: rocola_biblioteca.txt y rocola_letras.txt (valen solo con la firma de
//    las carpetas de musica, que en Linux es otra), rocola_miniaturas/ (cada una se llama por la ruta de su portada).
//  - respaldo/ (copias de lo de XP con sus rutas de Windows) y rocola_ia_datos.* (arriba).
//
// LA RE-IMPORTACION (regla del 7-11-2026 del programa anterior): si la carpeta de XP no es la que dice rocola_importado_xp.txt, se importa
// TODO otra vez de la buena; lo de antes queda en .antes-xp y la configuracion se junta con las claves que solo tenia la de antes
// (JuntaConfigDeAntes). Una carpeta que cambio solo de nombre de disco (un disco que hoy es sdc1 y ayer sdb1: la vieja ya no esta, la ruta
// dentro del disco y el Rocola.exe son los mismos) es la misma: no se re-importa.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Rocola
{
    internal static class ImportaXp
    {
        internal const string Marca = "rocola_importado_xp.txt";
        private const string Paso = ".importa-xp.tmp";             // la carpeta de paso, dentro del destino
        private const string EnCurso = ".importa-xp.en-curso";     // mientras se cambian los nombres: de que carpeta (ver Pon)
        private const string SufijoAntes = ".antes-xp";

        // Las tablas con rutas (el formato de cada una: quien la lee en Codigo/). Las cuatro primeras se leen como XP lee las que se
        // guardan anadiendo al final: solo lineas enteras y sin ceros (DiarioTablas.cs, LeeLineasSanas); las demas, tal cual.
        private static readonly string[] Tablas = {
            "rocola_sonoridad.txt", "rocola_volumen.txt", "rocola_eq.txt", "rocola_fuentes.txt",
            "rocola_duracion.txt", "rocola_afondo.txt", "rocola_ia_hechas.txt", "rocola_stats.txt",
            "rocola_billar_nivel.txt", "rocola_billar_bitacora.txt" };
        private const int TablasSanas = 4;
        private static readonly string[] NoSeTraen = {
            "rocola_estado.txt", "rocola_noche.txt", "rocola_error.txt", "rocola_calidad.txt", "rocola_incompatibles.txt",
            "rocola_biblioteca.txt", "rocola_letras.txt", "rocola_diagnostico.txt" };
        // Una ruta de Windows: con su letra de unidad, o de la red (esas no se pueden traducir). Hasta el siguiente campo: en las tablas la
        // ruta va siempre al final de la linea, tras la ultima «|» (y en Windows ninguna ruta lleva «|»).
        private static readonly Regex RutaWindows = new Regex(@"(?:[A-Za-z]:\\|\\\\)[^|\t\r\n]*", RegexOptions.CultureInvariant);

#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Antes()
        {
            string[] a = Environment.GetCommandLineArgs();
            if (a.Length < 2 || a[1] != "--importa-xp") return;
            int r;
            try { r = Corre(a); }
            catch (Exception ex) { r = Falla(4, "fallo inesperado (" + ex.GetType().Name + ": " + ex.Message + ")"); }
            try { Console.Out.Flush(); Console.Error.Flush(); } catch { }
            Environment.Exit(r);
        }

        private static int Falla(int codigo, string que)
        {
            try { Console.Error.WriteLine("rocola-xp --importa-xp: " + que); } catch { }
            return codigo;
        }
        private static void Dice(string que) { try { Console.Out.WriteLine("rocola-xp --importa-xp: " + que); } catch { } }

        private static int Corre(string[] a)
        {
            if (a.Length < 3 || a[2].Trim().Length == 0) return Falla(2, "uso: rocola-xp --importa-xp CARPETA_XP [DISCO...], con ROCOLA_DATOS = la carpeta de destino");
            string destino = (Environment.GetEnvironmentVariable("ROCOLA_DATOS") ?? "").Trim();
            if (destino.Length == 0) return Falla(2, "falta ROCOLA_DATOS (la carpeta de datos de rocola-xp, donde se importa)");
            destino = Completa(destino);
            string xp = CarpetaXpDe(a[2]);
            if (xp == null) return Falla(3, "no esta la carpeta de datos de la rocola de XP (" + a[2].Trim() + ")");
            if (xp == destino) { Dice("la carpeta de datos es la de la rocola de XP: nada que importar"); return 0; }

            List<string> discos = new List<string>();
            if (a.Length > 3) { for (int i = 3; i < a.Length; i++) if (a[i].Trim().Length > 0 && Directory.Exists(a[i].Trim())) discos.Add(Completa(a[i].Trim())); }
            else
            {
                try { if (Directory.Exists("/rocola/discos")) { string[] ds = Directory.GetDirectories("/rocola/discos"); Array.Sort(ds, StringComparer.Ordinal); discos.AddRange(ds); } } catch { }
                if (Directory.Exists("/rocola/disco")) discos.Add("/rocola/disco");
            }
            string relativa = Relativa(xp, discos), programa = Programa(xp);

            // ¿Ya importado de esta carpeta?
            string marca = Path.Combine(destino, Marca);
            string[] ant = LeeMarca(marca);   // carpeta, relativa, programa (null: no hay)
            bool reimporta = false;
            if (ant != null)
            {
                if (ant[0] == xp) { Dice("ya importado de " + xp + ": nada que hacer"); return 0; }
                if (!Directory.Exists(ant[0]) && ant[1].Length > 0 && ant[1] == relativa && ant[2] != "-" && ant[2] == programa)
                {
                    // la misma carpeta en un disco que cambio de nombre: solo se apunta donde esta ahora
                    List<string> l = new List<string>(File.ReadAllLines(marca));
                    l[0] = Cabecera(xp); bool hay = false;
                    for (int i = 1; i < l.Count; i++) if (l[i].StartsWith("carpeta=", StringComparison.Ordinal)) { l[i] = "carpeta=" + xp; hay = true; }
                    if (!hay) l.Insert(1, "carpeta=" + xp);
                    l.Add("(" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ": la misma carpeta, antes en " + ant[0] + ")");
                    Directory.CreateDirectory(destino);
                    EscribeTexto(marca + ".tmp", string.Join("\r\n", l) + "\r\n"); File.Move(marca + ".tmp", marca, true);
                    Dice("la misma carpeta de XP, ahora en " + xp + " (antes " + ant[0] + "): nada que importar");
                    return 0;
                }
                reimporta = true;
            }

            string paso = Path.Combine(destino, Paso);
            try
            {
                Directory.CreateDirectory(destino);
                if (Directory.Exists(paso)) Directory.Delete(paso, true);
                Directory.CreateDirectory(paso);
            }
            catch (Exception ex) { return Falla(4, "no se puede escribir en " + destino + " (" + ex.Message + ")"); }
            try
            {
                int r = Importa(xp, destino, paso, discos, relativa, programa, reimporta, ant);
                return r;
            }
            finally { try { if (Directory.Exists(paso)) Directory.Delete(paso, true); } catch { } }
        }

        private static int Importa(string xp, string destino, string paso, List<string> discos, string relativa, string programa, bool reimporta, string[] ant)
        {
            // Lo que hay en la carpeta de XP, sin distinguir mayusculas (Windows no las distingue).
            Dictionary<string, string> enXp = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string iasXp = null;
            try
            {
                string[] fs = Directory.GetFiles(xp); Array.Sort(fs, StringComparer.Ordinal);
                foreach (string f in fs) { string n = Path.GetFileName(f); if (!enXp.ContainsKey(n)) enXp[n] = f; }
                string[] ds = Directory.GetDirectories(xp); Array.Sort(ds, StringComparer.Ordinal);
                foreach (string d in ds) if (string.Equals(Path.GetFileName(d), Form1.NombreCarpetaIas, StringComparison.OrdinalIgnoreCase)) { iasXp = d; break; }
            }
            catch (Exception ex) { return Falla(4, "no se pudo leer la carpeta de XP " + xp + " (" + ex.Message + ")"); }

            // 1. Leer las tablas (y la configuracion) y juntar sus rutas de Windows.
            List<string> nombres = new List<string>(); List<List<string>> contenidos = new List<List<string>>(); List<string> deDonde = new List<string>();
            List<string> rutas = new List<string>();
            for (int t = 0; t < Tablas.Length; t++)
            {
                string f = DeXp(enXp, Tablas[t]);
                if (f == null) continue;
                List<string> ls;
                try { ls = Lineas(LeeTexto(f), t < TablasSanas); }
                catch (Exception ex) { return Falla(4, "no se pudo leer " + f + " (" + ex.Message + ")"); }
                nombres.Add(Tablas[t]); contenidos.Add(ls); deDonde.Add(Path.GetFileName(f));
                foreach (string l in ls) foreach (Match m in RutaWindows.Matches(l)) rutas.Add(m.Value.TrimEnd());
            }
            List<string> cfgXp = null; string fcfg = DeXp(enXp, "rocola_config.txt");
            if (fcfg != null)
            {
                try { cfgXp = Lineas(LeeTexto(fcfg), false); }
                catch (Exception ex) { return Falla(4, "no se pudo leer " + fcfg + " (" + ex.Message + ")"); }
                foreach (string l in cfgXp) { string s = l.Trim(); if (s.StartsWith("dir=", StringComparison.Ordinal) && EsRutaWindows(s.Substring(4).Trim())) rutas.Add(s.Substring(4).Trim()); }
            }
            Traductor tr = new Traductor(discos, xp);
            tr.Vota(rutas);

            // 2. Cada tabla, traducida, a la carpeta de paso.
            StringBuilder informe = new StringBuilder();
            List<string> hechos = new List<string>();   // rutas relativas de lo que hay en la carpeta de paso
            int totTraducidas = 0, totFuera = 0, totRutas = 0;
            for (int t = 0; t < nombres.Count; t++)
            {
                bool fuentes = nombres[t] == "rocola_fuentes.txt";
                int lineas = 0, traducidas = 0, fuera = 0, sellos = 0;
                StringBuilder sb = new StringBuilder();
                foreach (string linea in contenidos[t])
                {
                    lineas++;
                    bool quitar = false; int n = 0;
                    string l = RutaWindows.Replace(linea, delegate (Match m)
                    {
                        string w = m.Value.TrimEnd();
                        string r = tr.Traduce(w, false);
                        if (r == null) { quitar = true; return m.Value; }
                        n++; return r;
                    });
                    if (quitar) { fuera++; continue; }   // una cancion que ya no esta (o en un disco que no se ve)
                    if (n > 0) traducidas++;
                    if (fuentes && n > 0) l = SelloDeLinux(l, ref sellos);
                    sb.Append(l).Append("\r\n");
                }
                totTraducidas += traducidas; totFuera += fuera; totRutas += traducidas + fuera;
                try { EscribeTexto(Path.Combine(paso, nombres[t]), sb.ToString()); }
                catch (Exception ex) { return Falla(4, "no se pudo escribir " + nombres[t] + " (" + ex.Message + ")"); }
                hechos.Add(nombres[t]);
                informe.Append(nombres[t]).Append(": ").Append(lineas).Append(" lineas, ").Append(traducidas).Append(" con la ruta traducida, ")
                       .Append(fuera).Append(" fuera (ya no estan)");
                if (fuentes) informe.Append("; ").Append(sellos).Append(" con la fecha de creacion de Linux");
                if (!string.Equals(deDonde[t], nombres[t], StringComparison.Ordinal)) informe.Append(" (de ").Append(deDonde[t]).Append(")");
                informe.Append("\r\n");
            }
            // Si habia canciones y no se encontro NINGUNA, los discos no estan (o no son estos): no se importa nada, y se vuelve a intentar.
            if (totRutas > 0 && totTraducidas == 0)
                return Falla(4, "no esta en los discos ninguna cancion de las " + totRutas + " lineas con ruta de las tablas de XP (" + (discos.Count == 0 ? "no hay discos" : string.Join(" ", discos)) + "): no se importa; se intentara otra vez");

            // 3. Los botones, tal cual.
            string fteclas = DeXp(enXp, "rocola_teclas.txt");
            if (fteclas != null)
            {
                try { CopiaArchivo(fteclas, Path.Combine(paso, "rocola_teclas.txt")); }
                catch (Exception ex) { return Falla(4, "no se pudo copiar " + fteclas + " (" + ex.Message + ")"); }
                hechos.Add("rocola_teclas.txt");
                informe.Append("rocola_teclas.txt: tal cual\r\n");
            }

            // 4. La configuracion: las carpetas de Ctrl+F2 traducidas; junto con la que ya hubiera.
            HashSet<string> delLanzador = CopiasDelLanzador(destino);
            string cfgDestino = Path.Combine(destino, "rocola_config.txt");
            if (cfgXp != null)
            {
                List<string> cfg = new List<string>(); int dirT = 0, dirF = 0, resp = 0;
                foreach (string linea in cfgXp)
                {
                    string s = linea.Trim();
                    if (s.StartsWith("dir=", StringComparison.Ordinal))
                    {
                        string v = s.Substring(4).Trim();
                        if (EsRutaWindows(v))
                        {
                            string r = tr.Traduce(v, true);
                            if (r == null) { dirF++; continue; }
                            dirT++;
                            if (!cfg.Contains("dir=" + r)) cfg.Add("dir=" + r);
                            continue;
                        }
                    }
                    else if (s.StartsWith("respaldo_dir=", StringComparison.Ordinal)) { resp++; continue; }
                    cfg.Add(linea);
                }
                List<string> final = cfg; string como = "";
                if (File.Exists(cfgDestino))
                {
                    List<string> ya;
                    try { ya = Lineas(LeeTexto(cfgDestino), false); }
                    catch (Exception ex) { return Falla(4, "no se pudo leer " + cfgDestino + " (" + ex.Message + ")"); }
                    if (reimporta || delLanzador.Contains("rocola_config.txt")) { final = JuntaConfig(cfg, ya); como = "; con las claves que solo tenia la de antes"; }
                    else { final = JuntaConfig(ya, cfg); como = "; junto a la que ya tenia rocola-xp (manda la suya)"; }
                }
                try { EscribeTexto(Path.Combine(paso, "rocola_config.txt"), string.Join("\r\n", final) + (final.Count > 0 ? "\r\n" : "")); }
                catch (Exception ex) { return Falla(4, "no se pudo escribir rocola_config.txt (" + ex.Message + ")"); }
                hechos.Add("rocola_config.txt");
                informe.Append("rocola_config.txt: ").Append(dirT).Append(" carpetas de musica (dir=) traducidas, ").Append(dirF).Append(" fuera");
                if (resp > 0) informe.Append("; respaldo_dir= quitada");
                informe.Append(como).Append("\r\n");
            }

            // 5. Los modelos de IA, tal cual (sin el material para entrenar).
            int enIas = 0, sueltos = 0;
            try
            {
                if (iasXp != null)
                {
                    string[] fs = Directory.GetFiles(iasXp); Array.Sort(fs, StringComparer.Ordinal);
                    foreach (string f in fs)
                    {
                        string n = NombreModelo(Path.GetFileName(f), true);
                        if (n == null) continue;
                        Directory.CreateDirectory(Path.Combine(paso, Form1.NombreCarpetaIas));
                        string rel = Form1.NombreCarpetaIas + "/" + n;
                        if (hechos.Contains(rel)) continue;
                        CopiaArchivo(f, Path.Combine(paso, rel)); hechos.Add(rel); enIas++;
                    }
                }
                foreach (KeyValuePair<string, string> kv in enXp)
                {
                    string n = NombreModelo(kv.Key, false);
                    if (n == null || hechos.Contains(n)) continue;
                    CopiaArchivo(kv.Value, Path.Combine(paso, n)); hechos.Add(n); sueltos++;
                }
            }
            catch (Exception ex) { return Falla(4, "no se pudieron copiar los modelos de IA (" + ex.Message + ")"); }
            if (enIas + sueltos > 0) informe.Append("modelos de IA: ").Append(enIas).Append(" en IAS/, ").Append(sueltos).Append(" sueltos (rocola-xp los pasa a IAS/ al arrancar)\r\n");

            // 6. Lo que no se trae, para que conste.
            List<string> no = new List<string>();
            foreach (string n in NoSeTraen) if (enXp.ContainsKey(n)) no.Add(n);
            foreach (string n in enXp.Keys)
            {
                string m = n.ToLowerInvariant();
                if ((m.StartsWith("rocola_diagnostico_", StringComparison.Ordinal) || m.StartsWith("rocola_ia_datos", StringComparison.Ordinal)) && !no.Contains(n)) no.Add(n);
            }
            try { foreach (string d in Directory.GetDirectories(xp)) { string m = Path.GetFileName(d).ToLowerInvariant(); if (m == "rocola_miniaturas" || m.StartsWith("respaldo", StringComparison.Ordinal)) no.Add(Path.GetFileName(d) + "/"); } } catch { }
            if (no.Count > 0) informe.Append("no se traen: ").Append(string.Join(", ", no)).Append("\r\n");
            foreach (string u in tr.Unidades()) informe.Append(u).Append("\r\n");

            // 7. A su sitio. Primero lo de antes, copiado a .antes-xp (si se corta aqui, lo de antes sigue entero en su sitio); despues cada
            // archivo nuevo cambia de nombre al suyo (de un tiron), y la marca va la ultima. Si se cortara entre los cambios de nombre, la
            // vez siguiente (la marca sigue diciendo la carpeta de antes) no vuelve a copiar a .antes-xp lo que ya es de la importacion
            // nueva: lo dice .importa-xp.en-curso.
            string enCurso = Path.Combine(destino, EnCurso);
            bool sigue = false;
            try { sigue = File.Exists(enCurso) && File.ReadAllText(enCurso).Trim() == xp; } catch { }
            List<string> igual = new List<string>(), noSeTocan = new List<string>(), alAntes = new List<string>(), vacias = new List<string>();
            List<KeyValuePair<string, string>> mueve = new List<KeyValuePair<string, string>>();
            try
            {
                foreach (string rel in hechos)
                {
                    string nuevo = Path.Combine(paso, rel), dst = Path.Combine(destino, rel);
                    if (File.Exists(dst))
                    {
                        if (MismosBytes(nuevo, dst)) { igual.Add(rel); continue; }
                        // (Revision del 14-11-2026) Antes de la primera importacion (sin marca), una tabla de rocola-xp SIN NINGUNA CANCION no es
                        // «lo que ya tenia rocola-xp»: la deja asi rocola-xp si se abrio antes sin la musica (la importacion salio con 4 porque no
                        // estaba el disco y el lanzador abre la rocola igual): rocola_volumen.txt vacio. Sin esto, la de XP no entraba nunca.
                        bool sinCanciones = ant == null && Array.IndexOf(Tablas, rel) >= 0 && SinCanciones(dst);
                        bool reemplaza = reimporta || rel == "rocola_config.txt" || delLanzador.Contains(rel) || sinCanciones;
                        if (!reemplaza) { noSeTocan.Add(rel); continue; }
                        if (sinCanciones && new FileInfo(dst).Length == 0) vacias.Add(rel);   // vacia: nada que guardar en .antes-xp
                        else
                        {
                            if (!sigue) CopiaArchivo(dst, dst + SufijoAntes);
                            alAntes.Add(rel);
                        }
                    }
                    mueve.Add(new KeyValuePair<string, string>(nuevo, dst));
                }
                EscribeTexto(enCurso, xp + "\n");
                foreach (KeyValuePair<string, string> kv in mueve)
                {
                    string dir = Path.GetDirectoryName(kv.Value);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.Move(kv.Key, kv.Value, true);
                }
            }
            catch (Exception ex) { return Falla(4, "no se pudo poner lo importado en " + destino + " (" + ex.Message + ")"); }
            if (igual.Count > 0) informe.Append("igual que lo que ya habia: ").Append(string.Join(", ", igual)).Append("\r\n");
            if (noSeTocan.Count > 0) informe.Append("ya habia de rocola-xp, no se tocan: ").Append(string.Join(", ", noSeTocan)).Append("\r\n");
            if (vacias.Count > 0) informe.Append("vacias (de rocola-xp, sin ninguna cancion), reemplazadas: ").Append(string.Join(", ", vacias)).Append("\r\n");
            if (alAntes.Count > 0) informe.Append("lo de antes, en .antes-xp: ").Append(string.Join(", ", alAntes)).Append("\r\n");
            if (reimporta && ant != null) informe.Append("importado otra vez: antes era de ").Append(ant[0]).Append("\r\n");

            StringBuilder m0 = new StringBuilder();
            m0.Append(Cabecera(xp)).Append("\r\n");
            m0.Append("carpeta=").Append(xp).Append("\r\n").Append("relativa=").Append(relativa).Append("\r\n").Append("programa=").Append(programa).Append("\r\n");
            m0.Append(informe);
            try
            {
                string marca = Path.Combine(destino, Marca);
                EscribeTexto(marca + ".tmp", m0.ToString()); File.Move(marca + ".tmp", marca, true);
                File.Delete(enCurso);
            }
            catch (Exception ex) { return Falla(4, "no se pudo escribir " + Marca + " (" + ex.Message + ")"); }
            Dice((reimporta ? "importado otra vez" : "importado") + " de " + xp + " a " + destino + ": " + nombres.Count + " tablas, " + totTraducidas
                 + " lineas con la ruta traducida, " + totFuera + " fuera; detalle en " + Marca);
            return 0;
        }

        // La primera linea de la marca (la misma que la del programa anterior, Motor/ImportaXP.cs).
        private static string Cabecera(string xp)
        { return "Importado de la rocola de XP (" + xp + ") el " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture); }

        // De que carpeta se importo: { carpeta, relativa, programa }, o null si no hay marca.
        private static string[] LeeMarca(string marca)
        {
            if (!File.Exists(marca)) return null;
            string[] r = { "", "", "-" };
            string[] ls = File.ReadAllLines(marca);
            foreach (string l in ls)
            {
                if (l.StartsWith("carpeta=", StringComparison.Ordinal)) r[0] = l.Substring(8).Trim();
                else if (l.StartsWith("relativa=", StringComparison.Ordinal)) r[1] = l.Substring(9).Trim();
                else if (l.StartsWith("programa=", StringComparison.Ordinal)) r[2] = l.Substring(9).Trim();
            }
            if (r[0].Length == 0 && ls.Length > 0)
            {
                // solo la primera linea: «Importado de la rocola de XP (CARPETA) el FECHA»
                int i = ls[0].IndexOf(" XP (", StringComparison.Ordinal), j = ls[0].LastIndexOf(") el ", StringComparison.Ordinal);
                if (i >= 0 && j > i + 5) r[0] = ls[0].Substring(i + 5, j - i - 5).Trim();
            }
            if (r[0].Length > 0) r[0] = Completa(r[0]);
            return r;
        }

        // La carpeta de XP: la que dan, o la que dice la primera linea del archivo que dan (/run/rocola-datos-xp). null si no esta.
        private static string CarpetaXpDe(string arg)
        {
            string c = arg.Trim();
            try
            {
                if (!Directory.Exists(c) && File.Exists(c))
                {
                    string[] ls = File.ReadAllLines(c);
                    c = "";
                    foreach (string l in ls) if (l.Trim().Length > 0) { c = l.Trim(); break; }
                }
                return c.Length > 0 && Directory.Exists(c) ? Completa(c) : null;
            }
            catch { return null; }
        }

        private static string Completa(string p)
        {
            string r = Path.GetFullPath(p);
            return r.Length > 1 ? r.TrimEnd('/') : r;
        }

        // Donde esta la carpeta de XP dentro de su disco (lo que no cambia si el disco cambia de nombre), y el Rocola.exe de esa carpeta.
        private static string Relativa(string carpeta, List<string> discos)
        {
            string mejor = null;
            foreach (string d in discos)
            {
                string dd = d.Length > 1 ? d.TrimEnd('/') : d;
                if ((carpeta == dd || carpeta.StartsWith(dd == "/" ? "/" : dd + "/", StringComparison.Ordinal)) && (mejor == null || dd.Length > mejor.Length)) mejor = dd;
            }
            return mejor == null ? "" : carpeta.Substring(mejor.Length).Trim('/');
        }
        private static string Programa(string carpeta)
        {
            try
            {
                foreach (string f in Directory.GetFiles(carpeta))
                    if (string.Equals(Path.GetFileName(f), "Rocola.exe", StringComparison.OrdinalIgnoreCase))
                    { FileInfo i = new FileInfo(f); return i.Length.ToString(CultureInfo.InvariantCulture) + "|" + i.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture); }
            }
            catch { }
            return "-";
        }

        // Un archivo de la carpeta de XP por su nombre (sin mayusculas), o su copia .bak si el falta (como lo recupera XP al arrancar).
        private static string DeXp(Dictionary<string, string> enXp, string nombre)
        {
            string f;
            if (enXp.TryGetValue(nombre, out f)) return f;
            return enXp.TryGetValue(nombre + ".bak", out f) ? f : null;
        }

        // Un modelo de IA (con su nombre en minusculas: en Windows «Rocola_IA.bin» tambien es rocola_ia.bin), o null. En IAS/ cualquier
        // rocola_*.bin y sus copias .anterior-N; sueltos, los que OrdenaIas pasa a IAS/ (Form1.Datos.cs). Nunca el material.
        private static string NombreModelo(string n, bool enIas)
        {
            string m = n.ToLowerInvariant();
            if (!m.StartsWith("rocola_", StringComparison.Ordinal) || m.StartsWith("rocola_ia_datos", StringComparison.Ordinal)) return null;
            bool bin = m.EndsWith(".bin", StringComparison.Ordinal), anterior = Regex.IsMatch(m, @"\.bin\.anterior-[0-9]+$", RegexOptions.CultureInvariant);
            if (enIas) return bin || anterior ? m : null;
            if (m == "rocola_ia.bin" || m == "rocola_eq_ia.bin") return m;
            if (m.StartsWith(Form1.PrefijoDseeGenero, StringComparison.Ordinal) && (bin || anterior)) return m;
            return null;
        }

        private static bool EsRutaWindows(string v)
        { return v.Length >= 3 && ((char.IsLetter(v[0]) && v[1] == ':' && v[2] == '\\') || (v[0] == '\\' && v[1] == '\\')); }

        // El sello de rocola_fuentes.txt («largo|creada|escrita|huella|huella|ruta») con la fecha de creacion que vera rocola-xp en Linux,
        // si el archivo es el mismo (mismo largo y misma fecha de escritura). Si no, tal cual.
        private static string SelloDeLinux(string l, ref int sellos)
        {
            string[] p = l.Split(new char[] { '|' }, 6);
            long n, c, e;
            if (p.Length != 6 || !long.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out n)
                || !long.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out c)
                || !long.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out e)) return l;
            try
            {
                FileInfo f = new FileInfo(p[5]);
                if (!f.Exists || f.Length != n || f.LastWriteTimeUtc.Ticks != e) return l;
                long cl = f.CreationTimeUtc.Ticks;
                if (cl == c || cl <= 0) return l;
                sellos++;
                p[1] = cl.ToString(CultureInfo.InvariantCulture);
                return string.Join("|", p);
            }
            catch { return l; }
        }

        // JUNTAR DOS CONFIGURACIONES (JuntaConfigDeAntes del programa anterior, Motor/Arranque.cs): todas las lineas de la que manda y, de
        // la otra, las claves que la que manda no tiene. Las carpetas de Ctrl+F2 («dir=») no son una clave sino una lista: se suman las que
        // falten. De la otra no pasa ninguna linea con una ruta de Windows (si es una copia de la de XP sin traducir, la del lanzador).
        private static List<string> JuntaConfig(List<string> manda, List<string> otra)
        {
            List<string> r = new List<string>();
            HashSet<string> claves = new HashSet<string>(StringComparer.Ordinal), dirs = new HashSet<string>(StringComparer.Ordinal);
            foreach (string l in manda)
            {
                r.Add(l);
                string s = l.Trim(); int e = s.IndexOf('=');
                if (s.StartsWith("dir=", StringComparison.Ordinal)) dirs.Add(s.Substring(4).Trim());
                else if (e > 0) claves.Add(s.Substring(0, e).Trim());
            }
            foreach (string l in otra)
            {
                string s = l.Trim(); int e = s.IndexOf('=');
                if (e <= 0 || RutaWindows.IsMatch(s)) continue;
                if (s.StartsWith("dir=", StringComparison.Ordinal))
                {
                    string v = s.Substring(4).Trim();
                    if (v.Length > 0 && dirs.Add(v)) r.Add("dir=" + v);
                    continue;
                }
                if (claves.Add(s.Substring(0, e).Trim())) r.Add(l);
            }
            return r;
        }

        // Los archivos que el lanzador de antes copio tal cual de la carpeta de XP (rocola_de_xp.txt: «NOMBRE de CARPETA»): son de XP, no
        // de rocola-xp, y la importacion los pone ya traducidos.
        private static HashSet<string> CopiasDelLanzador(string destino)
        {
            HashSet<string> r = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                string f = Path.Combine(destino, "rocola_de_xp.txt");
                if (File.Exists(f))
                    foreach (string l in File.ReadAllLines(f)) { int i = l.IndexOf(" de ", StringComparison.Ordinal); if (i > 0) r.Add(l.Substring(0, i).Trim()); }
            }
            catch { }
            return r;
        }

        // El texto de un archivo de XP: en UTF-8 (lo que escribe la rocola); si no lo es, en Windows-1252 (uno escrito a mano con el Bloc de
        // notas). Como Motor/ImportaXP.cs.
        private static string LeeTexto(string ruta)
        {
            byte[] b = File.ReadAllBytes(ruta);
            string t;
            try { t = new UTF8Encoding(false, true).GetString(b); }
            catch (DecoderFallbackException)
            {
                const string altos = "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008DŽ\u008F\u0090‘’“”•–—˜™š›œ\u009DžŸ";
                char[] c = new char[b.Length];
                for (int i = 0; i < b.Length; i++) c[i] = b[i] >= 0x80 && b[i] < 0xA0 ? altos[b[i] - 0x80] : (char)b[i];
                t = new string(c);
            }
            if (t.Length > 0 && t[0] == '\uFEFF') t = t.Substring(1);
            return t;
        }

        // Las lineas (sin sus saltos). Sanas: como XP lee las tablas que se guardan anadiendo al final (LeeLineasSanas): si el archivo no acaba
        // en salto de linea la ultima esta a medias y no cuenta, y no cuenta ninguna con ceros.
        private static List<string> Lineas(string t, bool sanas)
        {
            List<string> r = new List<string>();
            if (t.Length == 0) return r;
            bool cortada = t[t.Length - 1] != '\n';
            string[] ls = t.Split('\n');
            int n = ls.Length - (cortada ? (sanas ? 1 : 0) : 1);
            for (int i = 0; i < n; i++)
            {
                string l = ls[i].EndsWith("\r", StringComparison.Ordinal) ? ls[i].Substring(0, ls[i].Length - 1) : ls[i];
                if (sanas && l.IndexOf('\0') >= 0) continue;
                r.Add(l);
            }
            return r;
        }

        // Escribir (UTF-8 sin marca, como XP) y bajarlo al disco.
        private static void EscribeTexto(string ruta, string texto)
        {
            byte[] b = new UTF8Encoding(false).GetBytes(texto);
            using (FileStream f = new FileStream(ruta, FileMode.Create, FileAccess.Write, FileShare.None))
            { f.Write(b, 0, b.Length); f.Flush(true); }
        }
        private static void CopiaArchivo(string de, string a)
        {
            using (FileStream o = new FileStream(de, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (FileStream d = new FileStream(a, FileMode.Create, FileAccess.Write, FileShare.None))
            { o.CopyTo(d); d.Flush(true); }
        }
        // Una tabla sin ninguna cancion: ninguna linea con una ruta de Linux (al final de la linea tras «|», o la linea entera: rocola_afondo.txt).
        // Vacia, o solo su cabecera y sus totales. (La bitacora del billar no lleva rutas: siempre cuenta como sin canciones.)
        private static bool SinCanciones(string ruta)
        {
            try
            {
                foreach (string l in File.ReadLines(ruta)) if (l.StartsWith("/", StringComparison.Ordinal) || l.IndexOf("|/", StringComparison.Ordinal) >= 0) return false;
                return true;
            }
            catch { return false; }
        }
        private static bool MismosBytes(string a, string b)
        {
            try
            {
                FileInfo x = new FileInfo(a), y = new FileInfo(b);
                if (x.Length != y.Length) return false;
                byte[] p = File.ReadAllBytes(a), q = File.ReadAllBytes(b);
                return p.AsSpan().SequenceEqual(q);
            }
            catch { return false; }
        }

        // DE UNA RUTA DE WINDOWS A LA DE ROCOLA OS. Cada tramo se busca en la carpeta de encima: tal cual y, si no esta, sin distinguir
        // mayusculas (Windows no las distingue y la rocola de XP guarda las rutas como se escribieron en su configuracion: SinMayusculas del
        // programa anterior). Lo que hay en cada carpeta se lee una vez. Cada letra de unidad va al disco donde estan mas de sus canciones
        // (se prueba una muestra de cada letra en cada disco): si dos discos tienen la misma musica -una copia-, la letra no se confunde por
        // el orden. Una cancion que no esta en el disco de su letra se busca en los demas.
        private sealed class Traductor
        {
            private sealed class Entrada { public string Ruta; public bool Carpeta; }
            private sealed class Listado { public Dictionary<string, Entrada> Exacto, SinMay; }
            private readonly List<string> discos;
            private readonly string xp;
            private readonly Dictionary<char, string> unidad = new Dictionary<char, string>();
            private readonly Dictionary<char, string> votos = new Dictionary<char, string>();
            private readonly Dictionary<string, string> hechas = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, Listado> listados = new Dictionary<string, Listado>(StringComparer.Ordinal);
            internal Traductor(List<string> discos, string xp) { this.discos = discos; this.xp = xp; }

            private Listado Lista(string carpeta)
            {
                Listado l;
                if (listados.TryGetValue(carpeta, out l)) return l;
                l = new Listado { Exacto = new Dictionary<string, Entrada>(StringComparer.Ordinal), SinMay = new Dictionary<string, Entrada>(StringComparer.OrdinalIgnoreCase) };
                try
                {
                    List<Entrada> es = new List<Entrada>();
                    foreach (string d in Directory.GetDirectories(carpeta)) es.Add(new Entrada { Ruta = d, Carpeta = true });
                    foreach (string f in Directory.GetFiles(carpeta)) es.Add(new Entrada { Ruta = f, Carpeta = false });
                    es.Sort((x, y) => string.CompareOrdinal(x.Ruta, y.Ruta));
                    foreach (Entrada e in es)
                    {
                        string n = Path.GetFileName(e.Ruta);
                        if (!l.Exacto.ContainsKey(n)) l.Exacto[n] = e;
                        if (!l.SinMay.ContainsKey(n)) l.SinMay[n] = e;
                    }
                }
                catch { }
                listados[carpeta] = l;
                return l;
            }

            // La ruta en ese disco, o null. carpeta: que sea una carpeta (si no, un archivo).
            private string Busca(string disco, string[] tramos, bool carpeta)
            {
                string actual = disco; bool esCarpeta = true;
                for (int i = 0; i < tramos.Length; i++)
                {
                    if (!esCarpeta) return null;
                    Listado l = Lista(actual);
                    Entrada e;
                    if (!l.Exacto.TryGetValue(tramos[i], out e) && !l.SinMay.TryGetValue(tramos[i], out e)) return null;
                    actual = e.Ruta; esCarpeta = e.Carpeta;
                }
                return esCarpeta == carpeta ? actual : null;
            }

            private static string[] Tramos(string w)
            {
                List<string> t = new List<string>();
                foreach (string s in w.Substring(3).Split('\\'))
                {
                    if (s.Length == 0 || s == ".") continue;
                    if (s == "..") return null;
                    t.Add(s);
                }
                return t.ToArray();
            }

            // A que disco va cada letra: el que tiene mas de una muestra de sus rutas (a igualdad, el de la carpeta de XP y despues el orden).
            internal void Vota(List<string> rutas)
            {
                Dictionary<char, List<string>> porLetra = new Dictionary<char, List<string>>();
                HashSet<string> vistas = new HashSet<string>(StringComparer.Ordinal);
                foreach (string w in rutas)
                {
                    if (!EsRutaWindows(w) || w[0] == '\\' || !vistas.Add(w)) continue;
                    char u = char.ToUpperInvariant(w[0]);
                    List<string> l; if (!porLetra.TryGetValue(u, out l)) porLetra[u] = l = new List<string>();
                    l.Add(w);
                }
                foreach (KeyValuePair<char, List<string>> kv in porLetra)
                {
                    List<string> l = kv.Value;
                    int muestra = Math.Min(40, l.Count);
                    string mejor = null; int mejorN = 0;
                    foreach (string d in DiscosEnOrden())
                    {
                        int n = 0;
                        for (int i = 0; i < muestra; i++)
                        {
                            string w = l[(int)((long)i * l.Count / muestra)];
                            string[] t = Tramos(w);
                            if (t != null && (Busca(d, t, false) != null || Busca(d, t, true) != null)) n++;
                        }
                        if (n > mejorN) { mejor = d; mejorN = n; }
                    }
                    if (mejor != null)
                    {
                        unidad[kv.Key] = mejor;
                        votos[kv.Key] = mejor + " (" + mejorN + " de " + muestra + " de la muestra; " + l.Count + " rutas)";
                    }
                    else votos[kv.Key] = "(ninguno: " + l.Count + " rutas de esa unidad no estan en los discos)";
                }
            }
            // Primero el disco donde esta la carpeta de XP (su musica suele ir al lado), despues los demas en su orden.
            private List<string> DiscosEnOrden()
            {
                List<string> r = new List<string>();
                foreach (string d in discos) if (xp == d || xp.StartsWith(d.TrimEnd('/') + "/", StringComparison.Ordinal)) { r.Add(d); break; }
                foreach (string d in discos) if (!r.Contains(d)) r.Add(d);
                return r;
            }

            internal string Traduce(string w, bool carpeta)
            {
                string clave = (carpeta ? "d" : "f") + w, r;
                if (hechas.TryGetValue(clave, out r)) return r;
                r = null;
                if (EsRutaWindows(w) && w[0] != '\\')
                {
                    string[] t = Tramos(w);
                    if (t != null)
                    {
                        string d;
                        if (unidad.TryGetValue(char.ToUpperInvariant(w[0]), out d)) r = Busca(d, t, carpeta);
                        if (r == null) foreach (string x in DiscosEnOrden()) { if (x == d) continue; r = Busca(x, t, carpeta); if (r != null) break; }
                    }
                }
                hechas[clave] = r;
                return r;
            }

            internal List<string> Unidades()
            {
                List<string> r = new List<string>();
                List<char> ks = new List<char>(votos.Keys); ks.Sort();
                foreach (char k in ks) r.Add("unidad " + k + ": -> " + votos[k]);
                return r;
            }
        }
    }
}
