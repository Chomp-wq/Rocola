// DONDE VIVE EL MATERIAL PARA ENTRENAR LA IA (Rocola OS, 3-11-2026).
// Lo aprendido de Rocola OS vive en datos.img, de 512 MB (rocola-instalar), y ahi iba tambien rocola_ia_datos.bin, que la cosecha deja
// crecer hasta 24 GB (ia_datos_mb=): con unas 300 canciones sanas repasadas a fondo llenaba datos.img, y desde ese momento la rocola ya
// no podia guardar nada mas (ni la configuracion, ni las tablas). Ahora:
//  - el material va al disco de la rocola, junto a la musica (ROCOLA_IA_DATOS: /rocola/disco/rocola-os/ia, lo pone el lanzador /usr/bin/
//    rocola cuando esta instalada); sin ese disco (la memoria USB), en la carpeta de datos como antes. Lo que ya hubiera en la carpeta
//    de datos se muda alli al arrancar (en segundo plano, con el candado de la cosecha), entero y una sola vez;
//  - la cosecha se para si al sitio donde escribe le quedan menos de 256 MB libres (se mira cada 30 s): lo demas siempre puede guardarse;
//  - el material que junto la rocola de XP (rocola_ia_datos.bin y .idx en su carpeta de datos, mismo formato y mismo extractor) tambien
//    lo usa el entrenador de Rocola OS (IA1, IaNueva.cs), sin copiarlo ni tocarlo: Rocola OS no vuelve a repasar a fondo lo que XP ya
//    repaso (rocola_afondo.txt se importa), asi que sin el de XP apenas tendria material de la biblioteca de siempre.
using System;
using System.Collections.Generic;
using System.IO;

namespace Rocola
{
    public partial class Form1
    {
        internal const long IaReservaBytes = 256L * 1024 * 1024;
        private static string CarpetaMaterialIa
        {
            get
            {
                string d = Environment.GetEnvironmentVariable("ROCOLA_IA_DATOS");
                if (!string.IsNullOrEmpty(d)) { try { Directory.CreateDirectory(d); return d; } catch { } }
                return CarpetaDatos;
            }
        }
        private static string RutaMaterialIa(string nombre) { return Path.Combine(CarpetaMaterialIa, nombre); }
        private static readonly string[] ArchivosMaterialIa = { "rocola_ia_datos.bin", "rocola_ia_datos.idx", "rocola_ia_datos.generos.tsv" };

        // Al arrancar, en segundo plano: lo de la carpeta de datos, a su sitio nuevo (solo si alli no hay nada todavia).
        private void MudaMaterialIa()
        {
            string nuevo = CarpetaMaterialIa;
            if (string.Equals(Path.GetFullPath(nuevo).TrimEnd('/'), Path.GetFullPath(CarpetaDatos).TrimEnd('/'), StringComparison.Ordinal)) return;
            lock (iaFileLock)
            {
                if (File.Exists(Path.Combine(nuevo, ArchivosMaterialIa[0])) || !File.Exists(RutaDatos(ArchivosMaterialIa[0]))) return;
                try
                {
                    foreach (string n in ArchivosMaterialIa)
                    {
                        string de = RutaDatos(n), a = Path.Combine(nuevo, n);
                        if (!File.Exists(de)) continue;
                        File.Copy(de, a + ".mudando", true);            // entero (nunca disperso: copia de verdad) y despues el nombre
                        File.Move(a + ".mudando", a, true);
                    }
                    foreach (string n in ArchivosMaterialIa) { try { File.Delete(RutaDatos(n)); } catch { } }
                    Diag("IA: el material para entrenar paso de la carpeta de datos a " + nuevo);
                }
                catch (Exception ex)
                {
                    foreach (string n in ArchivosMaterialIa) { try { File.Delete(Path.Combine(nuevo, n)); File.Delete(Path.Combine(nuevo, n) + ".mudando"); } catch { } }
                    Diag("IA: no se pudo mudar el material (" + ex.Message + "): sigue en la carpeta de datos");
                }
            }
        }

        // Poco sitio donde escribe la cosecha (se mira como mucho cada 30 s).
        private int iaSitioMirado = int.MinValue / 2; private bool iaSinSitio;
        private bool IaSinSitio()
        {
            int ahora = Environment.TickCount;
            if (unchecked(ahora - iaSitioMirado) < 30000) return iaSinSitio;
            iaSitioMirado = ahora;
            try { iaSinSitio = new DriveInfo(CarpetaMaterialIa).AvailableFreeSpace < IaReservaBytes; } catch { iaSinSitio = false; }
            return iaSinSitio;
        }

        // Los materiales para entrenar: el de esta rocola y, si se encuentra y es otro, el de la rocola de XP. Pares (datos, indice).
        private List<string[]> MaterialesIa()
        {
            List<string[]> r = new List<string[]>();
            string d = IaDataFile(), i = IaIndexFile();
            if (File.Exists(d) && File.Exists(i)) r.Add(new[] { d, i });
            try
            {
                string xp = Environment.GetEnvironmentVariable("ROCOLA_DATOS_XP");
                if (string.IsNullOrEmpty(xp) && File.Exists("/run/rocola-datos-xp")) xp = File.ReadAllText("/run/rocola-datos-xp").Trim();
                if (!string.IsNullOrEmpty(xp))
                {
                    string xd = Path.Combine(xp, "rocola_ia_datos.bin"), xi = Path.Combine(xp, "rocola_ia_datos.idx");
                    if (File.Exists(xd) && File.Exists(xi) && !string.Equals(Path.GetFullPath(xd), Path.GetFullPath(d), StringComparison.Ordinal)) r.Add(new[] { xd, xi });
                }
            }
            catch { }
            return r;
        }
    }
}
