// EL ARRANQUE DEL MOTOR (en XP, Form1.cs: OnShown y SigueArranque, la parte del sonido): los requisitos de la maquina, la configuracion,
// las IA y las tablas de lo aprendido (volumen, sonoridad, EQ...), en el mismo orden que XP. Las funciones que leen cada tabla son las
// de XP tal cual (DeXP.cs).
using System;
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
            ProbeAudioTier();    // requisitos de maquina para el NIVEL 2 de audio (antes de leer dsee= de config)
            LeeConfiguracion();
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
            LoadDurFast();
            startupReadPending = false;
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
    }
}
