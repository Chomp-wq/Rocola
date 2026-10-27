// LA BITACORA DEL BILLAR (27 de octubre de 2026, quinta tanda del modo billar). Todo lo de la cadena del billar se ha medido con canciones
// de prueba en Linux; nada en el billar. Para ajustarla con datos de verdad, con el modo billar la rocola apunta una linea por cada cancion
// que sono al menos 30 s (no una por cada salto) en rocola_billar_bitacora.txt, junto a los demas datos: separada por tabuladores, para
// abrirla en una hoja de calculo. Solo escribe (en segundo plano); no cambia el sonido. Al pasar de 4 MB la de antes se guarda como .1.
//
// Columnas: la fecha y hora, el archivo, si sono entera, los segundos medidos, el volumen parejo que llevo y el que aprendio (dB; "-" si nada),
// los picos por bloque frente al techo (P90, P99 y el mayor, dB), que parte del tiempo el limitador bajo mas de 1 y mas de 3 dB, lo que
// decidio el modo (presencia, claridad, partes suaves, cuerpo, eses, agudos asperos), si iba afinada, el factor del bajo, el corte del
// subgrave y el EQ de la cancion en sus ocho bandas (dB).
//
// EL RESUMEN (sexta tanda, 27-10-2026). En la tarjeta del billar (pagina de Sonido), dos lineas: cuantas canciones lleva apuntadas (y
// cuantas en las ultimas 12 h, una noche de bar), que parte del tiempo el limitador bajo mas de 3 dB y la cancion que mas apreto. Se
// arma en memoria: al arrancar se lee el archivo en segundo plano y cada linea nueva se suma al apuntarla. Solo se muestra.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
        internal const long BitacoraBillarMaxBytes = 4L * 1024 * 1024;
        internal const string BitacoraBillarCabecera = "fecha\tarchivo\tentera\tsegundos\tparejo_llevaba\tparejo_aprendio\tp90\tp99\tmayor\tlimita_1dB\tlimita_3dB\tpresencia\tclaridad\tsuaves\tcuerpo\teses\tasperos\tafinada\tbajo\tsubgrave_hz\teq50\teq158\teq400\teq1k\teq2k5\teq5k6\teq10k\teq15k8";
        private static readonly object bitacoraCerrojo = new object();
        private string BitacoraBillarFile() { return RutaDatos("rocola_billar_bitacora.txt"); }

        // ---- EL RESUMEN ----
        internal const double ResumenBitacoraHoras = 12.0;
        internal sealed class ResumenBitacora
        {
            internal int Total;
            internal double SumaLim3;
            internal readonly List<DateTime> Cuando = new List<DateTime>();
            internal readonly List<string> Archivo = new List<string>();
            internal readonly List<float> Lim3 = new List<float>();
        }
        private volatile ResumenBitacora bitacoraResumen;
        // Suma una linea de la bitacora al resumen (la cabecera y lo que no se entiende, no). Guarda el detalle de las de las ultimas 12 h
        // (contando desde 'ahora'). Pura.
        internal static bool AcumulaBitacora(ResumenBitacora r, string linea, DateTime ahora)
        {
            if (r == null || string.IsNullOrEmpty(linea)) return false;
            string[] c = linea.Split('\t');
            if (c.Length < 11) return false;
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            DateTime cuando; float lim3;
            if (!DateTime.TryParseExact(c[0], "yyyy-MM-dd HH:mm:ss", ci, System.Globalization.DateTimeStyles.None, out cuando)) return false;
            if (!float.TryParse(c[10], System.Globalization.NumberStyles.Float, ci, out lim3) || lim3 < 0f || lim3 > 100f) return false;
            r.Total++; r.SumaLim3 += lim3;
            if ((ahora - cuando).TotalHours <= ResumenBitacoraHoras)
            {
                r.Cuando.Add(cuando); r.Archivo.Add(c[1]); r.Lim3.Add(lim3);
                if (r.Cuando.Count > 2000) { r.Cuando.RemoveAt(0); r.Archivo.RemoveAt(0); r.Lim3.RemoveAt(0); }
            }
            return true;
        }
        // Las dos lineas de la tarjeta. Pura.
        internal static string TextoResumenBitacora(ResumenBitacora r, DateTime ahora)
        {
            if (r == null || r.Total == 0)
                return "Bitacora: aun vacia. Apunta cada cancion que suene 30 s o\nmas con el modo encendido (rocola_billar_bitacora.txt).";
            int n = 0; double s = 0; float peor = -1f; string cual = "";
            for (int i = 0; i < r.Cuando.Count; i++)
            {
                if ((ahora - r.Cuando[i]).TotalHours > ResumenBitacoraHoras) continue;
                n++; s += r.Lim3[i];
                if (r.Lim3[i] > peor) { peor = r.Lim3[i]; cual = r.Archivo[i]; }
            }
            string t = "Bitacora: " + r.Total + (r.Total == 1 ? " cancion" : " canciones") + "; " + n + " en las ultimas 12 h.\n";
            if (n == 0) return t + "El limitador bajo mas de 3 dB el " + (r.SumaLim3 / r.Total).ToString("0.0") + " % del tiempo.";
            cual = Path.GetFileNameWithoutExtension(cual);
            if (cual.Length > 22) cual = cual.Substring(0, 20) + "...";
            return t + "Limitador >3 dB: " + (s / n).ToString("0.0") + " %; la que mas: " + cual + " (" + peor.ToString("0") + " %).";
        }
        // Al arrancar, en segundo plano: el resumen de lo que ya hay en el archivo.
        private void CargaResumenBitacora()
        {
            ResumenBitacora r = new ResumenBitacora();
            try
            {
                string archivo = BitacoraBillarFile();
                if (File.Exists(archivo))
                {
                    DateTime ahora = DateTime.Now;
                    lock (bitacoraCerrojo)
                        using (StreamReader sr = new StreamReader(archivo))
                        {
                            string l;
                            while ((l = sr.ReadLine()) != null) AcumulaBitacora(r, l, ahora);
                        }
                }
            }
            catch (Exception ex) { LogError("Bitacora del billar", ex); }
            if (bitacoraResumen == null) bitacoraResumen = r;
        }
        internal string TextoBitacoraTarjeta()
        {
            ResumenBitacora r = bitacoraResumen;
            if (r == null) return "Bitacora: leyendo...\n";
            lock (r) return TextoResumenBitacora(r, DateTime.Now);
        }

        // La linea de una cancion (pura salvo la hora: la pasa quien llama).
        internal static string LineaBitacoraBillar(DateTime cuando, string archivo, bool entera, double segundos, float llevaba, float aprendio,
            float p90, float p99, float max, float lim1, float lim3, AjusteLocal lo, bool afinada, float bajo, float subHz, float[] eq)
        {
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            sb.Append(cuando.ToString("yyyy-MM-dd HH:mm:ss", ci)).Append('\t').Append((archivo ?? "").Replace('\t', ' ')).Append('\t');
            sb.Append(entera ? "si" : "no").Append('\t').Append(segundos.ToString("0", ci)).Append('\t');
            sb.Append(llevaba.ToString("0.00", ci)).Append('\t').Append(float.IsNaN(aprendio) ? "-" : aprendio.ToString("0.00", ci)).Append('\t');
            sb.Append(p90.ToString("0.0", ci)).Append('\t').Append(p99.ToString("0.0", ci)).Append('\t').Append(max.ToString("0.0", ci)).Append('\t');
            sb.Append((lim1 * 100f).ToString("0.0", ci)).Append('\t').Append((lim3 * 100f).ToString("0.0", ci)).Append('\t');
            sb.Append(lo.Presencia ? 1 : 0).Append('\t').Append(lo.Claridad ? 1 : 0).Append('\t').Append(lo.Suaves ? 1 : 0).Append('\t').Append(lo.Cuerpo ? 1 : 0).Append('\t');
            sb.Append(lo.Eses ? 1 : 0).Append('\t').Append(lo.Aspereza ? 1 : 0).Append('\t').Append(afinada ? 1 : 0).Append('\t');
            sb.Append(bajo.ToString("0.00", ci)).Append('\t').Append(subHz.ToString("0", ci));
            for (int b = 0; b < 8; b++) sb.Append('\t').Append(eq != null && eq.Length > b ? eq[b].ToString("0.0", ci) : "0");
            return sb.ToString();
        }

        private void ApuntaBitacoraBillar(CadenaCancion c, MideMargen m, float p90, float p99, float max, float aprendio)
        {
            try
            {
                string linea = LineaBitacoraBillar(DateTime.Now, Path.GetFileName(c.Cancion.Path), m.Entera && !m.Descarta, m.Bloques / 10.0,
                    m.Aplicado, aprendio, p90, p99, max, m.FraccionSobre(1f), m.FraccionSobre(3f), LocalDe(c), AfinadaActiva, BajoMedido, SubgraveEf,
                    c.Tap != null ? c.Tap.EqDb : null);
                string archivo = BitacoraBillarFile();
                ResumenBitacora r = bitacoraResumen;
                if (r != null) lock (r) AcumulaBitacora(r, linea, DateTime.Now);
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        lock (bitacoraCerrojo)
                        {
                            FileInfo fi = new FileInfo(archivo);
                            if (fi.Exists && fi.Length > BitacoraBillarMaxBytes) { File.Copy(archivo, archivo + ".1", true); File.Delete(archivo); }
                            bool nueva = !File.Exists(archivo);
                            File.AppendAllText(archivo, (nueva ? BitacoraBillarCabecera + "\r\n" : "") + linea + "\r\n");
                        }
                    }
                    catch (Exception ex) { LogError("Bitacora del billar", ex); }
                });
            }
            catch (Exception ex) { LogError("Bitacora del billar", ex); }
        }
    }
}
