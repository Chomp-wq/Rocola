// LA ORDEN DE PRUEBA DEL RELLENO DE LOS HUECOS (R1; Pruebas/RocolaOs/huecos/prueba.sh; FASE11_relleno-huecos.md). No hace nada en la
// rocola: `rocola-xp --prueba-huecos ...` corre aqui y acaba el programa (RocolaOs.Ordenes.cs).
//   mide ARCHIVO...            la medida del repaso (MideArchivo: tres tramos) y la del archivo entero, con lo que tardan
//   decodifica ARCHIVO BASE    descomprime el MP3 por el lector de XP (Mp3FileReader con el descompresor de siempre) de cuatro formas y deja
//                              cada una en BASE.<forma>.f32 (coma flotante de 32 bits, intercalado, sin cabecera):
//                                sin      el descompresor de XP tal cual (lo de hoy)
//                                con      envuelto con el relleno (RocolaOsRellenoHuecos), sumando
//                                pasa     envuelto pero sin sumar (como con «Escuchar sin mejoras»): debe salir IGUAL bit a bit que «sin»
//                                propio   la sintesis del lector de los huecos con la cancion ENTERA (no el relleno): debe decir lo mismo que
//                                         libmpg123, en el mismo momento (desfase 0) y a la misma escala
//                              y dice cuanto tardo cada una (ms por segundo de cancion), cuantas bandas relleno y cuanto se parecen.
//   costo ARCHIVO              lo que cuesta, bien medido: 7 vueltas de cada forma (sin, con, y solo leer los huecos), la mejor de cada una
//   salta ARCHIVO              lee con un salto a la mitad (Reset del descompresor) envuelto y sin envolver: que no truene ni se desfase
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using NAudio.Wave;

namespace Rocola
{
    internal static class RocolaOsHuecosPrueba
    {
        private static string N(double v, string f = "0.000") { return v.ToString(f, CultureInfo.InvariantCulture); }

        internal static int Corre(string[] a)
        {
            if (a.Length >= 2 && a[0] == "mide") { for (int i = 1; i < a.Length; i++) MideUno(a[i]); return 0; }
            if (a.Length == 3 && a[0] == "decodifica") return Decodifica(a[1], a[2]);
            if (a.Length == 2 && a[0] == "salta") return Salta(a[1]);
            if (a.Length == 2 && a[0] == "costo") return Costo(a[1]);
            Console.Error.WriteLine("uso: rocola-xp --prueba-huecos mide ARCHIVO... | decodifica ARCHIVO BASE | salta ARCHIVO");
            return 2;
        }

        private static void MideUno(string ruta)
        {
            Stopwatch sw = Stopwatch.StartNew();
            RocolaOsHuecosMp3 m = RocolaOsHuecos.MideArchivo(ruta);
            double ms = sw.Elapsed.TotalMilliseconds;
            // el archivo entero (para comparar con los tres tramos)
            sw.Restart();
            RocolaOsHuecosMp3 e = new RocolaOsHuecosMp3(1);
            long cuadros = 0;
            using (Mp3FileReader r = new Mp3FileReader(ruta))
            {
                Mp3Frame f;
                while ((f = r.ReadNextFrame()) != null) { e.Cuadro(f.RawData, f.FrameLength, null, false); cuadros++; }
            }
            double msE = sw.Elapsed.TotalMilliseconds;
            double seg = cuadros * 1152.0 / 44100.0;
            Console.WriteLine("MIDE " + Path.GetFileName(ruta) + " tramos: cuadros=" + (m.Cuadros - m.CuadrosRaros) + " candidatas=" + m.Candidatas + " huecos=" + m.Huecos
                + " pct=" + N(m.Candidatas > 0 ? m.Huecos * 100.0 / m.Candidatas : 0) + " trae=" + (RocolaOsHuecos.Veredicto(m.Candidatas, RocolaOsHuecos.Decimas(m.RellenoDb) / 10.0) ? "si" : "no")
                + " ms=" + N(ms, "0.0") + " | entero: cuadros=" + (e.Cuadros - e.CuadrosRaros) + " raros=" + e.CuadrosRaros + " largos=" + e.GranulosLargos + "/" + e.Granulos
                + " candidatas=" + e.Candidatas + " huecos=" + e.Huecos + " pct=" + N(e.Candidatas > 0 ? e.Huecos * 100.0 / e.Candidatas : 0)
                + " trae=" + (RocolaOsHuecos.Veredicto(e.Candidatas, RocolaOsHuecos.Decimas(e.RellenoDb) / 10.0) ? "si" : "no") + " relleno_db=" + N(e.RellenoDb, "0.0") + " tramos_relleno_db=" + N(m.RellenoDb, "0.0") + " ms=" + N(msE, "0.0") + " ms_por_s=" + N(seg > 0 ? msE / seg : 0, "0.000"));
        }

        // una lectura entera por Mp3FileReader con el descompresor que de «arma»
        // (27-11-2026) EL COSTO SE MIDE EN TIEMPO DE PROCESADOR DEL HILO (CLOCK_THREAD_CPUTIME_ID, resolucion de ns; la decodificacion corre en el hilo que
        // llama), no de reloj: con la maquina cargada (otras pruebas, otros procesos) el tiempo de reloj de una decodificacion se infla y la resta
        // «con menos sin» salia 15-17 ms por segundo de cancion contra 3-4 sin carga
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Timespec { public long Seg; public long Nseg; }
        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "clock_gettime")]
        private static extern int ClockGettime(int reloj, out Timespec t);
        private static double CpuMs()
        {
            Timespec t;
            if (ClockGettime(3, out t) == 0) return t.Seg * 1000.0 + t.Nseg / 1e6;   // 3 = CLOCK_THREAD_CPUTIME_ID (Linux)
            return Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;
        }
        private static float[] Lee(string ruta, Mp3FileReader.FrameDecompressorBuilder arma, out int canales, out int tasa, out double ms)
        {
            double c0 = CpuMs();
            using (Mp3FileReader r = new Mp3FileReader(ruta, arma))
            {
                canales = r.WaveFormat.Channels; tasa = r.WaveFormat.SampleRate;
                bool flt = r.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat;
                MemoryStream ms0 = new MemoryStream();
                byte[] b = new byte[65536]; int n;
                while ((n = r.Read(b, 0, b.Length)) > 0) ms0.Write(b, 0, n);
                ms = CpuMs() - c0;
                byte[] t = ms0.ToArray();
                float[] s = new float[t.Length / (flt ? 4 : 2)];
                for (int i = 0; i < s.Length; i++) s[i] = flt ? BitConverter.ToSingle(t, 4 * i) : (short)(t[2 * i] | t[2 * i + 1] << 8) / 32768f;
                return s;
            }
        }
        private static void Escribe(string ruta, float[] s)
        {
            byte[] b = new byte[s.Length * 4];
            Buffer.BlockCopy(s, 0, b, 0, b.Length);
            File.WriteAllBytes(ruta, b);
        }

        private static int Decodifica(string ruta, string baseSalida)
        {
            int ch, tasa; double msSin, msCon, msPasa;
            float[] sin = Lee(ruta, wf => DescompresorMp3.Crea(wf), out ch, out tasa, out msSin);
            RocolaOsRellenoHuecos env = null;
            float[] con = Lee(ruta, wf => env = new RocolaOsRellenoHuecos(DescompresorMp3.Crea(wf), 12345), out ch, out tasa, out msCon);
            RocolaOsRellenoHuecos envPasa = null;
            float[] pasa = Lee(ruta, wf => { envPasa = new RocolaOsRellenoHuecos(DescompresorMp3.Crea(wf), 12345); envPasa.Pasa = true; return envPasa; }, out ch, out tasa, out msPasa);
            // EL DOBLE: envuelto, pero el lector sintetiza la cancion ENTERA con el signo cambiado. Si su lectura, su sintesis, su escala y su
            // momento son los de libmpg123, lo que sale es casi silencio (lo que queda es el redondeo a 16 bits)
            RocolaOsRellenoHuecos envAnula = null; double msAnula;
            float[] anula = Lee(ruta, wf => { envAnula = new RocolaOsRellenoHuecos(DescompresorMp3.Crea(wf), 1); envAnula.Lector.Propio = true; envAnula.Lector.PropioEscala = -1f; return envAnula; }, out ch, out tasa, out msAnula);
            // y lo mismo con el descompresor en coma flotante (NLayer, el que XP pone a las canciones que llegan al maximo: FlotanteNLayer.cs)
            RocolaOsRellenoHuecos envFlt = null; double msFlt;
            float[] sinFlt = Lee(ruta, wf => new DescompresorFlotante(wf, DescompresorMp3.Crea(wf), DecodificacionFlotante.FabricaNLayer), out ch, out tasa, out msFlt);
            float[] anulaFlt = Lee(ruta, wf => { envFlt = new RocolaOsRellenoHuecos(new DescompresorFlotante(wf, DescompresorMp3.Crea(wf), DecodificacionFlotante.FabricaNLayer), 1); envFlt.Lector.Propio = true; envFlt.Lector.PropioEscala = -1f; return envFlt; }, out ch, out tasa, out msFlt);
            double sf = 0, df = 0;
            for (int i = 0; i < Math.Min(sinFlt.Length, anulaFlt.Length); i++) { sf += (double)sinFlt[i] * sinFlt[i]; df += (double)anulaFlt[i] * anulaFlt[i]; }
            Escribe(baseSalida + ".sin.f32", sin); Escribe(baseSalida + ".con.f32", con); Escribe(baseSalida + ".pasa.f32", pasa);
            // pasa == sin, bit a bit
            bool iguales = pasa.Length == sin.Length;
            for (int i = 0; iguales && i < sin.Length; i++) if (BitConverter.SingleToInt32Bits(pasa[i]) != BitConverter.SingleToInt32Bits(sin[i])) iguales = false;
            double se = 0, sd = 0, maxd = 0;
            for (int i = 0; i < Math.Min(sin.Length, anula.Length); i++) { se += (double)sin[i] * sin[i]; sd += (double)anula[i] * anula[i]; if (Math.Abs(anula[i]) > maxd) maxd = Math.Abs(anula[i]); }
            double sumado = 0, total = 0;
            for (int i = 0; i < Math.Min(con.Length, sin.Length); i++) { double d = con[i] - sin[i]; sumado += d * d; total += (double)sin[i] * sin[i]; }
            double seg = sin.Length / (double)ch / tasa;
            Console.WriteLine("DECODIFICA " + Path.GetFileName(ruta) + " canales=" + ch + " tasa=" + tasa + " seg=" + N(seg, "0.00")
                + " muestras_sin=" + sin.Length + " muestras_con=" + con.Length + " pasa_igual=" + (iguales ? "si" : "no")
                + " anula_db=" + N(sd > 0 ? 10 * Math.Log10(sd / se) : -999, "0.0") + " anula_max=" + N(maxd, "0.000000") + " anula_desfases=" + envAnula.Desfases + " anula_tirados=" + envAnula.Tirados
                + " anula_flotante_db=" + N(df > 0 ? 10 * Math.Log10(df / sf) : -999, "0.0") + " flotante_desfases=" + envFlt.Desfases + " flotante_tirados=" + envFlt.Tirados
                + " rellenadas=" + (env != null ? env.Lector.Rellenos : -1) + " huecos=" + (env != null ? env.Lector.Huecos : -1) + " candidatas=" + (env != null ? env.Lector.Candidatas : -1)
                + " sumadas=" + (env != null ? env.Sumadas : -1) + " desfases=" + (env != null ? env.Desfases : -1) + " pasa_sumadas=" + (envPasa != null ? envPasa.Sumadas : -1)
                + " relleno_db=" + N(total > 0 && sumado > 0 ? 10 * Math.Log10(sumado / total) : -999, "0.0")
                + " relleno_medido_db=" + N(env != null ? env.Lector.RellenoDb : -99, "0.0") + " ms_por_s_sin=" + N(msSin / seg) + " ms_por_s_con=" + N(msCon / seg) + " ms_por_s_pasa=" + N(msPasa / seg) + " ms_por_s_anula=" + N(msAnula / seg)
                + " raros=" + envAnula.Lector.CuadrosRaros + " primeros_n=" + (env != null ? string.Join(",", env.PrimerosN) : ""));
            return 0;
        }

        // CON SALTOS (el Reset del descompresor, como al retomar una cancion o al saltar los silencios largos): el mismo recorrido sin envolver y
        // con el doble que anula; tras cada salto lo que sale debe seguir siendo casi silencio (el lector empieza de cero como libmpg123)
        private static int Salta(string ruta)
        {
            float[][] sale = new float[2][];
            RocolaOsRellenoHuecos env = null;
            for (int v = 0; v < 2; v++)
            {
                bool envolver = v == 1;
                MemoryStream acum = new MemoryStream();
                using (Mp3FileReader r = new Mp3FileReader(ruta, wf => envolver ? (IMp3FrameDecompressor)(env = Anulador(wf)) : DescompresorMp3.Crea(wf)))
                {
                    byte[] b = new byte[16384]; int n;
                    for (int k = 0; k < 20 && (n = r.Read(b, 0, b.Length)) > 0; k++) acum.Write(b, 0, n);
                    r.Position = (r.Length / 2) & ~3L;
                    for (int k = 0; k < 40 && (n = r.Read(b, 0, b.Length)) > 0; k++) acum.Write(b, 0, n);
                    r.Position = (r.Length / 5) & ~3L;
                    for (int k = 0; k < 40 && (n = r.Read(b, 0, b.Length)) > 0; k++) acum.Write(b, 0, n);
                }
                byte[] t = acum.ToArray(); float[] s = new float[t.Length / 2];
                for (int i = 0; i < s.Length; i++) s[i] = (short)(t[2 * i] | t[2 * i + 1] << 8) / 32768f;
                sale[v] = s;
            }
            double se = 0, sd = 0;
            for (int i = 0; i < Math.Min(sale[0].Length, sale[1].Length); i++) { se += (double)sale[0][i] * sale[0][i]; sd += (double)sale[1][i] * sale[1][i]; }
            Console.WriteLine("SALTA " + Path.GetFileName(ruta) + " muestras=" + sale[0].Length + "/" + sale[1].Length + " anula_db=" + N(sd > 0 ? 10 * Math.Log10(sd / se) : -999, "0.0")
                + " desfases=" + env.Desfases + " tirados=" + env.Tirados);
            return 0;
        }
        private static int Costo(string ruta)
        {
            double mejorSin = double.MaxValue, mejorCon = double.MaxValue, mejorLee = double.MaxValue;
            int ch = 2, tasa = 44100; float[] s0 = null;
            for (int v = 0; v < 7; v++)
            {
                double ms;
                s0 = Lee(ruta, wf => DescompresorMp3.Crea(wf), out ch, out tasa, out ms); mejorSin = Math.Min(mejorSin, ms);
                Lee(ruta, wf => new RocolaOsRellenoHuecos(DescompresorMp3.Crea(wf), 3), out ch, out tasa, out ms); mejorCon = Math.Min(mejorCon, ms);
                double c1 = CpuMs();
                RocolaOsHuecosMp3 e = new RocolaOsHuecosMp3(1);
                using (Mp3FileReader r = new Mp3FileReader(ruta)) { Mp3Frame f; while ((f = r.ReadNextFrame()) != null) e.Cuadro(f.RawData, f.FrameLength, null, false); }
                mejorLee = Math.Min(mejorLee, CpuMs() - c1);
            }
            double seg = s0.Length / (double)ch / tasa;
            Console.WriteLine("COSTO " + Path.GetFileName(ruta) + " seg=" + N(seg, "0.00") + " ms_por_s_sin=" + N(mejorSin / seg) + " ms_por_s_con=" + N(mejorCon / seg)
                + " ms_por_s_relleno=" + N((mejorCon - mejorSin) / seg) + " ms_por_s_solo_leer=" + N(mejorLee / seg)
                + " nucleo_pct=" + N((mejorCon - mejorSin) / seg / 10.0, "0.00") + " vector=" + System.Numerics.Vector<float>.Count);
            return 0;
        }
        private static RocolaOsRellenoHuecos Anulador(WaveFormat wf)
        {
            RocolaOsRellenoHuecos e = new RocolaOsRellenoHuecos(DescompresorMp3.Crea(wf), 1);
            e.Lector.Propio = true; e.Lector.PropioEscala = -1f;
            return e;
        }
    }
}
