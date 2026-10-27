// LA BITACORA DEL BILLAR (27 de octubre de 2026, quinta tanda del modo billar). Todo lo de la cadena del billar se ha medido con canciones
// de prueba en Linux; nada en el billar. Para ajustarla con datos de verdad, con el modo billar la rocola apunta una linea por cada cancion
// que sono al menos 30 s (no una por cada salto) en rocola_billar_bitacora.txt, junto a los demas datos: separada por tabuladores, para
// abrirla en una hoja de calculo. Solo escribe (en segundo plano); no cambia el sonido. Al pasar de 4 MB la de antes se guarda como .1.
//
// Columnas: la fecha y hora, el archivo, si sono entera, los segundos medidos, el volumen parejo que llevo y el que aprendio (dB; "-" si nada),
// los picos por bloque frente al techo (P90, P99 y el mayor, dB), que parte del tiempo el limitador bajo mas de 1 y mas de 3 dB, lo que
// decidio el modo (presencia, claridad, partes suaves, cuerpo, eses, agudos asperos), si iba afinada, el factor del bajo, el corte del
// subgrave y el EQ de la cancion en sus ocho bandas (dB).
using System;
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
