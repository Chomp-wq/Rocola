// LO GRANDE DE rocola-xp, FUERA DE LO APRENDIDO (Rocola OS; no es de XP). En Rocola OS la carpeta de datos de rocola-xp
// (/rocola/datos/xp) va dentro de lo aprendido: un ext4 de 512 MB guardado en un archivo (datos.img), que comparte con lo del programa
// anterior. XP guarda en su carpeta de datos tres cosas que no caben ahi (o que no deben ir ahi):
//  - el material para entrenar la IA (rocola_ia_datos.bin, .idx y .generos.tsv; Codigo/Form1.Ia.cs): la cosecha esta encendida de
//    fabrica (iaHarvestOn) y su tope es de 24 GB (iaMaxBytes);
//  - las miniaturas de las portadas (rocola_miniaturas/, con el indice de previas previews32-v1.bin; Codigo/Form1.Portadas.cs,
//    CarpetaMiniaturas): una PNG de 256x256 por portada (unos 142 KB, lo medido en el comentario de las caratulas en JPEG de
//    Form1.Portadas.cs), y el calentador de miniaturas (Form1.cs) las hace de toda la biblioteca; el barrido solo empieza pasados 200 MB
//    y solo quita las de mas de un ano (MiniaturasTopeMb, MiniaturasDiasMax): con 2 000 discos, unos 280 MB.
//  - el respaldo de lo aprendido (respaldo/, Codigo/Form1.Datos.cs, BackupBaseDir): cada dia, tres copias de quince tablas
//    (PruneBackups(root, 3)): con 20 000 canciones de 84 a 150 MB, y una copia dentro de datos.img se perderia con el (XP respalda
//    porque «el disco puede morir»).
// Con eso lleno, la rocola ya no podria guardar ni su estado. Lo que el dueno decidio para Rocola OS el 3-11-2026 (Motor/MaterialIa.cs
// del programa anterior):
//  - lo grande va a la carpeta que diga ROCOLA_IA_DATOS (el lanzador la pone en el disco de la rocola, /rocola/disco/rocola-os/ia-xp:
//    el material, rocola_miniaturas/ y respaldo/);
//    sin la variable, o si no se puede usar, a la carpeta de datos, como en XP. Se decide UNA vez (como Form1.CarpetaDatos: si cambiara
//    a media sesion, la mitad de las cosas acabarian en un sitio y la otra mitad en otro);
//  - la cosecha se para si a ese disco le quedan menos de 256 MB libres (se mira como mucho cada 30 s): lo demas siempre puede guardarse.
// Lo que ya hubiera en la carpeta de datos se queda donde esta (no se muda): con «datos=xp» esa carpeta es la de la rocola de XP, y su
// material sigue siendo suyo. rocola_ia_hechas.txt (de que canciones ya se saco material) sigue en la carpeta de datos, como en XP.
using System;
using System.IO;

namespace Rocola
{
    internal static class DatosGrandes
    {
        internal const long ReservaBytes = 256L * 1024 * 1024;
        private static readonly object cerrojo = new object();
        private static bool decidida;
        private static string carpeta;   // null: la carpeta de datos (como XP)

        // La carpeta de lo grande, o null si es la carpeta de datos.
        private static string Carpeta
        {
            get
            {
                lock (cerrojo)
                {
                    if (decidida) return carpeta;
                    decidida = true;
                    string d = null;
                    try { d = Environment.GetEnvironmentVariable("ROCOLA_IA_DATOS"); } catch { }
                    if (string.IsNullOrEmpty(d) || d.Trim().Length == 0) return carpeta = null;
                    d = d.Trim();
                    try
                    {
                        Directory.CreateDirectory(d);
                        // Que de verdad se pueda escribir (un disco montado en solo lectura tiene la carpeta, pero no deja crear nada): como
                        // la prueba de Form1.CarpetaDatos.
                        string prueba = Path.Combine(d, "rocola_escribible_" + Guid.NewGuid().ToString("N") + ".tmp");
                        using (FileStream sonda = new FileStream(prueba, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) sonda.WriteByte(1);
                        return carpeta = d;
                    }
                    catch (Exception ex)
                    {
                        // a la salida de errores: el lanzador la apunta en su registro
                        try { Console.Error.WriteLine("rocola-xp: no se puede usar ROCOLA_IA_DATOS=" + d + " (" + ex.Message + "): el material de la IA, las miniaturas y el respaldo van a la carpeta de datos"); } catch { }
                        return carpeta = null;
                    }
                }
            }
        }

        // La ruta de un archivo (o carpeta) de lo grande: en ROCOLA_IA_DATOS, o en la carpeta de datos (lo mismo que RutaDatos de XP).
        internal static string Ruta(string nombre)
        {
            string c = Carpeta;
            return Path.Combine(c ?? Form1.CarpetaDatos, nombre);
        }

        // Poco sitio donde va el material: la cosecha se para (Form1.Ia.cs, IaBudgetFull). Se mira como mucho cada 30 s.
        private static int mirado = int.MinValue / 2;
        private static bool sinSitio;
        internal static bool SinSitio()
        {
            int ahora = Environment.TickCount;
            if (unchecked(ahora - mirado) < 30000) return sinSitio;
            mirado = ahora;
            try { sinSitio = new DriveInfo(Carpeta ?? Form1.CarpetaDatos).AvailableFreeSpace < ReservaBytes; }
            catch { sinSitio = false; }
            return sinSitio;
        }
    }
}
