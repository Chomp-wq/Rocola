// EL ARRANQUE DEL MOTOR (en XP, Form1.cs: OnShown y SigueArranque, la parte del sonido): los requisitos de la maquina, la configuracion,
// las IA y las tablas de lo aprendido (volumen, sonoridad, EQ...), en el mismo orden que XP. Las funciones que leen cada tabla son las
// de XP tal cual (DeXP.cs).
using System;
using System.Collections.Generic;
using System.IO;

namespace Rocola
{
    public partial class Form1
    {
        private string sbrReason = "";

        // Los requisitos del NIVEL 2 (el DSEE), como ProbeAudioTier de XP: 2 nucleos (o 2200 MHz) y 900 MB. En Linux, de /proc.
        private void ProbeAudioTier()
        {
            int cores = 1, mhz = 0; ulong ramMb = 0;
            try { cores = Environment.ProcessorCount; } catch { }
            try
            {
                foreach (string l in File.ReadLines("/proc/cpuinfo"))
                    if (l.StartsWith("cpu MHz")) { double v; if (double.TryParse(l.Substring(l.IndexOf(':') + 1).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) mhz = (int)v; break; }
            }
            catch { }
            try
            {
                foreach (string l in File.ReadLines("/proc/meminfo"))
                    if (l.StartsWith("MemTotal:")) { ulong kb; if (ulong.TryParse(l.Substring(9).Trim().Split(' ')[0], out kb)) ramMb = kb / 1024UL; break; }
            }
            catch { }
            bool cpuOk = (cores >= 2) || (mhz >= 2200);
            bool ramOk = (ramMb == 0) || (ramMb >= 900);
            sbrAllowed = cpuOk && ramOk;
            if (!sbrAllowed)
                sbrReason = "CPU de " + cores + " nucleo(s) a " + mhz + " MHz" + (ramOk ? "" : (", RAM de " + ramMb + " MB"))
                          + "  (pide 2 nucleos o 2200 MHz, y 900 MB)";
        }

        // Todo lo del arranque que toca al sonido. Se llama una vez, antes de la primera cancion.
        public void Arranca()
        {
            ImportaDeXPSiHay();  // lo que la rocola de XP ya aprendio, la primera vez (ImportaXP.cs)
            ProbeAudioTier();    // requisitos de maquina para el NIVEL 2 de audio (antes de leer dsee= de config)
            CapturaFabrica();    // los valores de fabrica de Ctrl+F3, antes de leer los guardados («Dejar como de fabrica»)
            LeeConfiguracion();
            SumaMusicaDeOtrosDiscos();
            CargaParejo();       // el volumen parejo aprendido del modo billar (VolumenParejo.cs)
            Diag("rocola del " + DeCuandoEs());
            int iasOrdenadas = OrdenaIas();   // los modelos sueltos de la carpeta de datos, a IAS/
            if (iasOrdenadas > 0) Diag("IAS: " + iasOrdenadas + " modelo(s) pasados a la carpeta IAS");
            NeuralEnv.TryLoad(RutaLectura("rocola_ia.bin"));   // IA propia (el modelo entrenado: en IAS, o el de fabrica)
            EqNet.TryLoad(RutaLectura("rocola_eq_ia.bin"));    // IA del EQ automatico (objetivo por cancion)
            CargaModelosPorGenero();   // I3: los del DSEE por genero
            LoadStats();         // lo mas pedido (rocola_stats.txt)
            LoadLoudCache();
            LoadIntegCache();
            LoadEqCache();
            LoadDeepDone();
            LoadIaDone();        // de que canciones ya se junto material para la IA (rocola_ia_hechas.txt)
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { try { MudaMaterialIa(); } catch { } try { IaCountPairs(); } catch { } });   // el material a su sitio (MaterialIa.cs) y cuantos ejemplos hay   // cuantos ejemplos hay ya (solo para ensenarlo)
            LoadDurFast();
            startupReadPending = false;
            // Las tablas del convertidor para el relevo entre 44100 y 48000 (las frecuencias de casi todo), en segundo plano: cuando haga
            // falta ya estan (en XP, CalientaFrecuenciaSalida).
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    NAudio.Wave.WaveFormat a = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(44100, 2), b = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
                    for (int i = 0; i < 40 && (RemuestreoContinuo.CapturaTabla(a, b) == null || RemuestreoContinuo.CapturaTabla(b, a) == null); i++) System.Threading.Thread.Sleep(250);
                }
                catch { }
            });
        }

        // Lo que se sabe, para la consola.
        public string Resumen()
        {
            return "datos en " + CarpetaDatos + "; " + (integCache != null ? integCache.Count : 0) + " canciones medidas, "
                + (loudCache != null ? loudCache.Count : 0) + " con volumen, " + (eqCache != null ? eqCache.Count : 0) + " con EQ; modo billar "
                + (localAuto ? "ENCENDIDO" : "apagado") + "; DSEE " + (sbrOn ? "encendido" : "apagado") + (sbrAllowed ? "" : " (" + sbrReason + ")");
        }

        // rocola_teclas.txt, el de los botones (el mismo de XP).
        public string ArchivoBotones { get { return RutaDatos("rocola_teclas.txt"); } }
        // LA COPIA DE LOS BOTONES FUERA DE LO APRENDIDO (4-11-2026): en el disco de la musica (ROCOLA_COPIAS, la pone /usr/bin/rocola).
        // Si datos.img se pierde, la rocola arrancaba pidiendo configurar los botones y los clientes no podian usarla; con la copia
        // los botones siguen como estaban. null sin ese disco.
        public string CopiaBotones
        {
            get
            {
                string d = Environment.GetEnvironmentVariable("ROCOLA_COPIAS");
                if (string.IsNullOrEmpty(d)) return null;
                try { Directory.CreateDirectory(d); } catch { return null; }
                return Path.Combine(d, "rocola_teclas.txt");
            }
        }

        // La musica de los otros discos (Rocola OS: rocola-discos los monta en solo lectura y deja sus carpetas "musica" en
        // /run/rocola-musica, una por linea; ROCOLA_MUSICA_LISTA puede dar otro archivo). Se suman a las de "dir=", como en XP.
        private void SumaMusicaDeOtrosDiscos()
        {
            string lista = Environment.GetEnvironmentVariable("ROCOLA_MUSICA_LISTA");
            if (string.IsNullOrEmpty(lista)) lista = "/run/rocola-musica";
            try
            {
                if (!File.Exists(lista)) return;
                foreach (string l in File.ReadAllLines(lista))
                {
                    string d = l.Trim();
                    if (d.Length > 0 && !extraMusicDirs.Contains(d)) extraMusicDirs.Add(d);
                }
            }
            catch { }
        }

        // La carpeta de datos de XP que encontro rocola-discos (/run/rocola-datos-xp; ROCOLA_DATOS_XP puede dar otra) y los discos montados.
        private void ImportaDeXPSiHay()
        {
            try
            {
                string xp = Environment.GetEnvironmentVariable("ROCOLA_DATOS_XP");
                if (string.IsNullOrEmpty(xp) && File.Exists("/run/rocola-datos-xp")) xp = File.ReadAllText("/run/rocola-datos-xp").Trim();
                if (string.IsNullOrEmpty(xp)) return;
                string discos = Environment.GetEnvironmentVariable("ROCOLA_DISCOS");
                if (string.IsNullOrEmpty(discos)) discos = "/rocola/discos";
                List<string> raices = new List<string>();
                if (Directory.Exists(discos)) raices.AddRange(Directory.GetDirectories(discos));
                if (Directory.Exists("/rocola/disco")) raices.Add("/rocola/disco");   // instalado: el disco de la rocola (rocola-instalar)
                string informe = ImportaDeXP(xp, raices);
                if (informe != null) Diag(informe);
            }
            catch { }
        }
    }
}
