// LA CADENA VIEJA BORRADA (28-11-2026; el dueno: «Elimina la cadena de sonido anterior y tambien el modo billar anterior, yo se que esta cadena
// de sonido nueva sera mejor» y, el mismo dia, «Sobre las cadenas de audio viejas elimina todo de ellas, que no quede rastro»; el inventario y el
// plan, CADENA_NUEVA.md, «La cadena vieja borrada»). Lo de AQUI es la FASE 1 (lo que no choca con los agentes que trabajan en la cadena nueva):
// al arrancar con la cadena nueva (la llama el Arranca de «cadena_nueva», que va siempre con ROCOLA_OS=1), en su hilo:
//   1. LOS DATOS DE LA CADENA VIEJA QUE YA NADIE LEE, A LA PAPELERA de Archivos de Ctrl+F4 (RocolaOs.Papelera.cs: se vacia sola a los 30 dias;
//      se pueden devolver si algo faltara). Una vez por lista: rocola_cadena_vieja.txt («papelera=N», la version de la lista que ya se mando).
//      La lista 1 (fase 1): el volumen parejo y la bitacora del billar viejo, la huella de Spotify del EQ automatico y el realce (spotify_eq y
//      spotify_brillo), TODOS los modelos de la IA (IAS/ y sueltos: el del DSEE, el del EQ, los de genero, la candidata, las .anterior-N) y
//      rocola_ia_hechas.txt; la carpeta IAS si queda vacia. NO van (la cadena nueva aun los lee hasta la fase 2): rocola_volumen.txt,
//      rocola_eq.txt, rocola_sonoridad.txt, rocola_fuentes.txt, rocola_afondo.txt, rocola_calidad.txt y rocola_spotify_niveles.txt.
//   2. EL MATERIAL DE LA IA (la cosecha de XP: rocola_ia_datos.bin, .idx y .generos.tsv, hasta 24 GB en el disco de la rocola, ROCOLA_IA_DATOS)
//      no cabe en la papelera de los datos (datos.img de 512 MB, tope de 64 MB) y es derivado: se BORRA (decision (4) del 28-11-2026: «elimina
//      todo de la IA»). Las miniaturas y el respaldo de esa misma carpeta no se tocan. La cosecha ya no se hace con Rocola OS (gancho g550).
//   3. La tabla de los niveles de Spotify de la cadena vieja se carga SOLO PARA LEER (RocolaOsSpotifyNivela.CargaParaLeer): la cadena nueva la
//      usa de respaldo para la ganancia fija estimada de Spotify la 1.a vez, hasta tener 5 medidas suyas.
// Nada de esto corre sin ROCOLA_OS (la rocola de XP tal cual) ni con una lista que no nombre «cadena_nueva».
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Rocola
{
    internal static class RocolaOsCadenaVieja
    {
        internal const string Archivo = "rocola_cadena_vieja.txt";
        internal const int ListaPapelera = 1;   // la version de la lista de abajo (la fase 2 la sube con las tablas de XP)
        internal const string CarpetaIa = "papelera-cadena-vieja";
        internal const int DiasIa = 30;
        // LISTA 1 (fase 1): de la carpeta de datos de rocola-xp, lo que la cadena nueva ya no lee (los patrones con «*» van dentro de su carpeta)
        internal static readonly string[] Lista1 =
        {
            "rocola_billar_nivel.txt", "rocola_billar_nivel.antes-2026-11-18.txt",       // el volumen parejo aprendido del billar (VolumenParejo.cs)
            "rocola_billar_bitacora.txt", "rocola_billar_bitacora.txt.1",                // la bitacora del billar (BitacoraBillar.cs)
            "rocola_spotify_eq.txt",                                                     // spotify_eq y spotify_brillo (RocolaOs.SpotifyEq.cs)
            // TODA LA IA (28-11-2026, decision (4): «elimina todo de la IA, al igual que lo de recolectar datos para entrenar IA»): los modelos
            // (el del DSEE, el del EQ, los de genero, la candidata y las .anterior-N), en IAS/ y sueltos, y lo que apuntaba la cosecha. Sin
            // rocola_ia.bin el DSEE de «agudos_mp3» usa su curva fija (EnvolventeClasica, Codigo/Form1.Sonido.cs) desde el siguiente arranque.
            "IAS/rocola_*.bin", "IAS/rocola_*.bin.anterior-*", "rocola_ia.bin", "rocola_eq_ia.bin", "rocola_ia_genero_*.bin", "rocola_*.bin.anterior-*",
            "rocola_ia_hechas.txt",
            "IAS",                                                                       // (la carpeta, si quedo vacia)
        };
        internal static readonly string[] MaterialIa = { "rocola_ia_datos.bin", "rocola_ia_datos.idx", "rocola_ia_datos.generos.tsv" };

        internal static long Mandadas, Apartadas;
        private static int arrancada;

        internal static void Arranca()
        {
            if (System.Threading.Interlocked.Exchange(ref arrancada, 1) != 0) return;
            RocolaOs.Hilo("cadena_vieja", delegate
            {
                try { RocolaOsSpotifyNivela.CargaParaLeer(); } catch (Exception ex) { RocolaOs.Falla(RocolaOsCadenasNuevas.Nombre, "leer los niveles de Spotify de antes", ex); }
                try { ALaPapelera(); } catch (Exception ex) { RocolaOs.Falla(RocolaOsCadenasNuevas.Nombre, "los datos de la cadena vieja a la papelera", ex); }
                try { ApartaMaterialIa(); } catch (Exception ex) { RocolaOs.Falla(RocolaOsCadenasNuevas.Nombre, "apartar el material de la IA", ex); }
            });
        }

        // ---------------------------------------------------------------- 1. a la papelera de Ctrl+F4
        private static int VersionHecha()
        {
            try
            {
                string f = RocolaOs.Datos(Archivo);
                if (!File.Exists(f)) return 0;
                foreach (string l in File.ReadAllLines(f))
                    if (l.StartsWith("papelera=", StringComparison.Ordinal) && int.TryParse(l.Substring(9).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
            }
            catch { }
            return 0;
        }
        private static void ALaPapelera()
        {
            if (VersionHecha() >= ListaPapelera) return;
            string datos = Form1.CarpetaDatos;
            RocolaOsRaiz raiz = RocolaOsArchivos.Datos();
            List<string> mandadas = new List<string>(), fallas = new List<string>();
            // (XP aparta rocola_billar_nivel.txt a .antes-2026-11-18.txt al arrancar si le falta parejo_v2: se le da un momento para no cruzarse)
            System.Threading.Thread.Sleep(3000);
            foreach (string patron in Lista1)
            {
                foreach (string ruta in Busca(datos, patron))
                {
                    RocolaOsTirado t;
                    string mal = raiz != null ? RocolaOsPapelera.Manda(raiz, ruta, out t) : "sin la carpeta de datos";
                    if (mal == null) { mandadas.Add(Path.GetRelativePath(datos, ruta)); System.Threading.Interlocked.Increment(ref Mandadas); }
                    else if (File.Exists(ruta) || Directory.Exists(ruta)) fallas.Add(Path.GetRelativePath(datos, ruta) + " (" + mal + ")");   // (si ya no esta, se fue sola: p. ej. XP aparta rocola_billar_nivel.txt al arrancar con parejo_v2; su copia sigue en la lista)
                }
            }
            if (fallas.Count > 0)
            {   // (se vuelve a intentar en el siguiente arranque: la version no se apunta)
                RocolaOs.Apunta(RocolaOsCadenasNuevas.Nombre, "cadena vieja: no se pudieron mandar a la papelera " + string.Join(", ", fallas) + (mandadas.Count > 0 ? "; si: " + string.Join(", ", mandadas) : ""));
                return;
            }
            Form1.RocolaOsEscribeSeguro(RocolaOs.Datos(Archivo), "# los datos de la cadena de sonido vieja ya mandados a la papelera de Ctrl+F4 (RocolaOs/RocolaOs.CadenaVieja.cs)\npapelera="
                + ListaPapelera.ToString(CultureInfo.InvariantCulture) + "\n");
            RocolaOs.Apunta(RocolaOsCadenasNuevas.Nombre, "cadena vieja: " + (mandadas.Count == 0 ? "nada que mandar a la papelera" : "a la papelera de Ctrl+F4 (se vacia sola a los 30 dias): " + string.Join(", ", mandadas)));
            if (mandadas.Count > 0) RocolaOsPapelera.PurgaDeFondo("los datos de la cadena vieja");
        }
        // las rutas de un patron («carpeta/nombre», con «*» solo en el nombre), que existan y sean archivos de verdad (no enlaces)
        internal static List<string> Busca(string raiz, string patron)
        {
            List<string> l = new List<string>();
            string dir = Path.Combine(raiz, Path.GetDirectoryName(patron) ?? ""), nombre = Path.GetFileName(patron);
            try
            {
                if (!Directory.Exists(dir)) return l;
                string una = Path.Combine(dir, nombre);
                if (nombre.IndexOf('*') < 0 && Directory.Exists(una))
                {   // una carpeta: solo si quedo vacia (la papelera solo se lleva carpetas vacias)
                    if ((File.GetAttributes(una) & FileAttributes.ReparsePoint) == 0 && !Directory.EnumerateFileSystemEntries(una).GetEnumerator().MoveNext()) l.Add(una);
                    return l;
                }
                IEnumerable<string> cuales = nombre.IndexOf('*') >= 0 ? Directory.EnumerateFiles(dir, nombre) : new[] { una };
                foreach (string f in cuales)
                {
                    try
                    {
                        if (!File.Exists(f)) continue;
                        if ((File.GetAttributes(f) & FileAttributes.ReparsePoint) != 0) continue;
                        l.Add(f);
                    }
                    catch { }
                }
            }
            catch { }
            l.Sort(StringComparer.Ordinal);
            return l;
        }

        // ---------------------------------------------------------------- 2. el material de la IA, BORRADO
        // (28-11-2026, decision (4) del dueno: «elimina todo de la IA»): el material para entrenar (rocola_ia_datos.bin, .idx, .generos.tsv; hasta 24
        // GB) es derivado de la musica y no cabe en la papelera de datos.img: se BORRA al primer arranque. Solo esos tres archivos (y lo que la
        // version de antes de esta fase hubiera apartado en papelera-cadena-vieja/): las miniaturas y el respaldo, que viven en la misma carpeta
        // (ROCOLA_IA_DATOS, Nativo/DatosGrandes.cs), NO se tocan. Nunca la carpeta de la rocola de XP del disco de Windows.
        private static void ApartaMaterialIa()
        {
            string uno = DatosGrandes.Ruta(MaterialIa[0]);
            string dir = Path.GetDirectoryName(uno);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            long bytes = 0;
            List<string> hechas = new List<string>();
            foreach (string n in MaterialIa)
            {
                string f = DatosGrandes.Ruta(n);
                try
                {
                    if (!File.Exists(f) || (File.GetAttributes(f) & FileAttributes.ReparsePoint) != 0 || Path.GetDirectoryName(f) != dir) continue;
                    long b = new FileInfo(f).Length;
                    File.Delete(f);
                    bytes += b; hechas.Add(n); System.Threading.Interlocked.Increment(ref Apartadas);
                }
                catch (Exception ex) { RocolaOs.Falla(RocolaOsCadenasNuevas.Nombre, "borrar " + f, ex); }
            }
            string pap = Path.Combine(dir, CarpetaIa);
            try { if (Directory.Exists(pap) && (File.GetAttributes(pap) & FileAttributes.ReparsePoint) == 0) { Directory.Delete(pap, true); hechas.Add(CarpetaIa + "/"); } }
            catch (Exception ex) { RocolaOs.Falla(RocolaOsCadenasNuevas.Nombre, "borrar " + pap, ex); }
            if (hechas.Count > 0)
                RocolaOs.Apunta(RocolaOsCadenasNuevas.Nombre, "cadena vieja: BORRADO el material de la IA (" + string.Join(", ", hechas) + ", " + (bytes / 1048576).ToString(CultureInfo.InvariantCulture)
                    + " MB) de " + dir + " (la cosecha ya no se hace; las miniaturas y el respaldo se quedan)");
        }
    }

    public partial class Form1
    {
        // g550 (Codigo/Form1.Sonido.cs, MeasureFile): con Rocola OS no se junta material para la IA (la cosecha era de la cadena vieja: el DSEE de
        // «agudos_mp3» usa el modelo que ya hay y no se entrena aqui). Sin ROCOLA_OS, el interruptor de XP tal cual.
        private static bool RocolaOsCosechaIa(bool deXp) { return deXp && !RocolaOs.Activo; }
    }
}
