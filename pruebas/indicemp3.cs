using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// EL INDICE DE CUADROS DE LOS MP3, SIN BASURA (22-10-2026; Mp3Ligero.cs, LectorMp3Ligero.Abre).
//
// Al abrir un MP3, el Mp3FileReader de NAudio recorre el archivo entero para apuntar donde empieza cada cuadro (CreateTableOfContents): un Mp3Frame, un arreglo de
// 4 bytes y un Mp3Index por cuadro, unos 1,3 MB de basura y 10 ms por cancion de 200 s. El repaso abre cada cancion dos veces y la reproduccion una. LectorMp3Ligero.Abre
// lo cuenta aparte, en dos arreglos de enteros (donde empieza cada cuadro y cuantas muestras habia antes), repitiendo paso a paso lo que hace el constructor de
// NAudio; cuando algo no es lo corriente, abre por el constructor de siempre.
//
// Aqui no hay descompresor de Windows: se imita uno que APUNTA todo lo que recibe (como en mp3ligero) y, para cada archivo, NAudio y Abre tienen que dar:
//   - el mismo resultado al abrir (o la misma excepcion con el mismo mensaje): el formato Mp3WaveFormat que se le da al descompresor campo a campo (con su tasa de bits
//     media, que sale del indice), Length, TotalTime, WaveFormat, las etiquetas ID3v2 e ID3v1;
//   - el MISMO INDICE: la misma cantidad de cuadros, y para cada uno donde empieza y cuantas muestras habia antes (el de NAudio, leido por dentro);
//   - los mismos bytes con lecturas de tamanos raros y saltos (al principio, a la mitad, mas alla del final), la misma posicion, y el descompresor recibe lo
//     mismo en el mismo orden (cuadros y Reset).
// Archivos: los MP3 de verdad de esta carpeta (mono, 48 kHz, MPEG-2, VBR, sin etiqueta...) y variantes en memoria: con caratula ID3v2, basura entre cuadros (con y sin
// 0xFF), cortado a mitad de un cuadro, ID3v1 al final, tres veces seguido, DOS FRECUENCIAS (NAudio no lo abre: Abre cae al constructor y contesta lo mismo), archivos
// diminutos, vacios y sin cuadros. Y se mide: abrir una cancion larga con Abre deja menos de la cuarta parte de la basura y tarda menos.
class IndiceMp3
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }

    static string Campos(object o)
    {
        if (o == null) return "null";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(o.GetType().Name + "{");
        foreach (FieldInfo f in o.GetType().GetFields(T)) if (!f.IsStatic) sb.Append(f.Name).Append('=').Append(f.GetValue(o)).Append(';');
        foreach (PropertyInfo p in o.GetType().GetProperties(T)) if (p.GetIndexParameters().Length == 0 && p.CanRead && p.PropertyType.IsPrimitive) sb.Append(p.Name).Append('=').Append(p.GetValue(o, null)).Append(';');
        return sb.Append('}').ToString();
    }

    sealed class Graba : IMp3FrameDecompressor
    {
        readonly WaveFormat f; readonly int ch; readonly bool apunta;
        public readonly List<string> Diario = new List<string>();
        public ulong Suma = 1469598103934665603UL;
        readonly Mp3Frame[] historia = new Mp3Frame[3]; readonly byte[][] copia = new byte[3][]; int h;
        public int Pisados, Llamadas;
        public Graba(WaveFormat mp3, bool apunta) { ch = mp3.Channels; f = new WaveFormat(mp3.SampleRate, 16, ch); this.apunta = apunta; }
        public WaveFormat OutputFormat { get { return f; } }
        public int DecompressFrame(Mp3Frame fr, byte[] dest, int off)
        {
            Llamadas++;
            if (apunta)
                for (int i = 0; i < 3; i++)   // los tres anteriores, intactos
                    if (historia[i] != null)
                    {
                        byte[] r = historia[i].RawData; byte[] c = copia[i];
                        bool igual = r != null && historia[i].FrameLength == c.Length;
                        for (int k = 0; igual && k < c.Length; k++) if (r[k] != c[k]) igual = false;
                        if (!igual) Pisados++;
                    }
            ulong s = Suma;
            int largo = fr.FrameLength;
            for (int k = 0; k < largo; k++) { s ^= fr.RawData[k]; s *= 1099511628211UL; }
            s ^= (ulong)fr.FileOffset * 31 + (ulong)fr.SampleCount * 7 + (ulong)fr.SampleRate + (ulong)fr.ChannelMode + (ulong)fr.BitRate;
            Suma = s;
            if (apunta)
            {
                Diario.Add("C " + fr.FileOffset + " " + largo + " " + fr.SampleCount + " " + fr.RawData.Length + " " + s);
                historia[h] = fr; copia[h] = new byte[largo]; Array.Copy(fr.RawData, copia[h], largo); h = (h + 1) % 3;
            }
            int n = fr.SampleCount * 2 * ch;
            if (Llamadas % 7 == 3 && fr.SampleCount == 1152) n *= 2;   // un cuadro doble de vez en cuando
            for (int i = 0; i < n; i++) dest[off + i] = (byte)(fr.RawData[(i * 13) % largo] ^ i ^ Llamadas);
            return n;
        }
        public void Reset() { if (apunta) Diario.Add("R"); Array.Clear(historia, 0, 3); }
        public void Dispose() { }
    }

    static Type tLigero; static MethodInfo mAbre;
    static string formatoDado;   // el Mp3WaveFormat que recibio el constructor del descompresor, campo a campo
    static Mp3FileReader Abre(int como, byte[] datos, Graba[] g, bool apunta)
    {
        Mp3FileReader.FrameDecompressorBuilder b = delegate(WaveFormat wf) { g[0] = new Graba(wf, apunta); formatoDado = Campos(wf); return g[0]; };
        MemoryStream ms = new MemoryStream(datos, false);
        if (como == 0) return new Mp3FileReader(ms, b);
        try { return (Mp3FileReader)mAbre.Invoke(null, new object[] { ms, b }); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }

    // El indice de un lector: NAudio (tableOfContents) o el propio (dos arreglos), como texto.
    static string Indice(Mp3FileReader r)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        FieldInfo ft = typeof(Mp3FileReader).GetField("tableOfContents", T);
        System.Collections.IList lista = ft.GetValue(r) as System.Collections.IList;
        if (lista != null)
        {
            sb.Append(lista.Count).Append(":");
            foreach (object e in lista)
            {
                Type te = e.GetType();
                long fp = (long)te.GetProperty("FilePosition").GetValue(e, null), sp = (long)te.GetProperty("SamplePosition").GetValue(e, null);
                int sc = (int)te.GetProperty("SampleCount").GetValue(e, null);
                sb.Append(fp).Append(',').Append(sp).Append(',').Append(sc).Append(';');
            }
            return sb.ToString();
        }
        int[] pos = (int[])tLigero.GetField("indicePos", T).GetValue(r), mue = (int[])tLigero.GetField("indiceMuestras", T).GetValue(r);
        int n = (int)tLigero.GetField("indiceN", T).GetValue(r);
        long total = (long)tLigero.GetField("totalMuestras", T).GetValue(r);
        sb.Append(n).Append(":");
        for (int i = 0; i < n; i++)
        {
            long hasta = (i + 1 < n) ? mue[i + 1] : total;
            sb.Append(pos[i]).Append(',').Append(mue[i]).Append(',').Append(hasta - mue[i]).Append(';');
        }
        return sb.ToString();
    }

    // Abre y lo describe todo; lee con tamanos raros y saltos. 'propio' = el indice es el propio (solo con como = 1).
    static string Recorre(int como, byte[] datos, out List<string> diario, out int pisados, out string error, out bool propio, out string indice)
    {
        Graba[] g = new Graba[1]; diario = null; pisados = 0; error = null; propio = false; indice = null; formatoDado = null;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        Mp3FileReader r;
        try { r = Abre(como, datos, g, true); }
        catch (Exception ex)
        {
            error = ex.GetType().Name; return "no abre: " + ex.GetType().FullName + " " + ex.Message;
        }
        using (r)
        {
            if (como == 1) propio = (bool)tLigero.GetProperty("IndiceEsPropio", T).GetValue(r, null);
            indice = Indice(r);
            sb.Append("F ").Append(formatoDado).Append("|");
            sb.Append("M ").Append(Campos(r.Mp3WaveFormat)).Append("|");
            sb.Append("L ").Append(r.Length).Append(" ").Append(r.TotalTime.Ticks).Append(" ").Append(r.WaveFormat).Append(" @").Append(r.Position).Append("|");
            byte[] v1 = r.Id3v1Tag; sb.Append("v1 ").Append(v1 == null ? "null" : v1.Length.ToString()).Append(" v2 ").Append(r.Id3v2Tag == null ? "null" : r.Id3v2Tag.RawData.Length.ToString()).Append("|");
            int[] tam = { 17640, 1, 333, 7777, 4096, 2, 4608, 9216, 65536 };
            byte[] buf = new byte[65536];
            double total = r.TotalTime.TotalSeconds;
            double[] saltos = { 0.5, 0.0, total + 5, total / 2, total * 0.9, 0.013, total * 0.333, -3.0, total };
            int paso = 0, s = 0, vueltas = 0;
            while (vueltas++ < 100000)
            {
                if (paso % 9 == 5 && s < saltos.Length) { r.CurrentTime = TimeSpan.FromSeconds(saltos[s++]); sb.Append("S").Append(r.Position).Append("|"); }
                int n;
                try { n = r.Read(buf, 0, tam[paso % tam.Length]); }
                catch (Exception ex) { sb.Append("E ").Append(ex.GetType().Name).Append("|"); break; }
                paso++;
                ulong h = 14695981039346656037UL;
                for (int i = 0; i < n; i++) { h ^= buf[i]; h *= 1099511628211UL; }
                sb.Append(n).Append(":").Append(h).Append("@").Append(r.Position).Append("|");
                if (n == 0 && s >= saltos.Length) break;
            }
        }
        diario = g[0].Diario; pisados = g[0].Pisados;
        return sb.ToString();
    }

    static byte[] Une(params byte[][] partes)
    {
        int n = 0; foreach (byte[] p in partes) n += p.Length;
        byte[] r = new byte[n]; int o = 0;
        foreach (byte[] p in partes) { Array.Copy(p, 0, r, o, p.Length); o += p.Length; }
        return r;
    }
    static byte[] Trozo(byte[] a, int desde, int n) { byte[] r = new byte[n]; Array.Copy(a, desde, r, 0, n); return r; }

    static readonly MethodInfo mideReserva = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread");
    static long Reservado() { return mideReserva == null ? 0 : (long)mideReserva.Invoke(null, null); }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        tLigero = asm.GetType("Rocola.LectorMp3Ligero");
        Check(tLigero != null && (bool)tLigero.GetField("Posible", T).GetValue(null), "la rocola trae el lector ligero y encuentra las piezas de NAudio 1.9");
        mAbre = tLigero == null ? null : tLigero.GetMethod("Abre", T);
        Check(mAbre != null, "el lector ligero trae Abre (el indice de cuadros propio)");
        if (tLigero == null || mAbre == null) { Console.WriteLine("  indicemp3: " + mal + " FALLOS de " + (ok + mal)); return 1; }
        Check((bool)tLigero.GetField("IndicePosible", T).GetValue(null), "encuentra los campos privados de NAudio que Abre pone");

        // Los archivos: los de verdad y las variantes.
        List<string> nombres = new List<string>(); List<byte[]> datos = new List<byte[]>();
        foreach (string f in Directory.GetFiles(".", "*.mp3")) { nombres.Add(Path.GetFileName(f)); datos.Add(File.ReadAllBytes(f)); }
        if (Directory.Exists("mp3s")) foreach (string f in Directory.GetFiles("mp3s", "*.mp3")) { nombres.Add("mp3s/" + Path.GetFileName(f)); datos.Add(File.ReadAllBytes(f)); }
        byte[] base0 = File.ReadAllBytes("flotante_overs.mp3"), r48 = File.ReadAllBytes("relleno_48k.mp3"), mono = File.ReadAllBytes("relleno_mono.mp3");
        byte[] id3 = new byte[10 + 70000]; id3[0] = (byte)'I'; id3[1] = (byte)'D'; id3[2] = (byte)'3'; id3[3] = 3;
        int t = 70000; id3[6] = (byte)((t >> 21) & 0x7F); id3[7] = (byte)((t >> 14) & 0x7F); id3[8] = (byte)((t >> 7) & 0x7F); id3[9] = (byte)(t & 0x7F);
        Random rnd = new Random(5); for (int i = 10; i < id3.Length; i++) id3[i] = (byte)rnd.Next(256);   // con 0xFF dentro, como una caratula
        nombres.Add("con caratula ID3v2 de 70 KB"); datos.Add(Une(id3, base0));
        byte[] basura = new byte[300]; for (int i = 0; i < basura.Length; i++) basura[i] = (byte)(i % 5 == 0 ? 0xFF : rnd.Next(256));
        int mitad = base0.Length / 2;
        nombres.Add("con basura con 0xFF entre cuadros"); datos.Add(Une(Trozo(base0, 0, mitad), basura, Trozo(base0, mitad, base0.Length - mitad)));
        byte[] basura2 = new byte[301]; for (int i = 0; i < basura2.Length; i++) basura2[i] = (byte)rnd.Next(255);
        nombres.Add("con basura entre cuadros (busca la sincronia byte a byte)"); datos.Add(Une(Trozo(base0, 0, mitad), basura2, Trozo(base0, mitad, base0.Length - mitad)));
        nombres.Add("con basura al principio"); datos.Add(Une(basura2, base0));
        nombres.Add("con basura al final"); datos.Add(Une(base0, basura2));
        nombres.Add("cortado a mitad de un cuadro"); datos.Add(Trozo(base0, 0, base0.Length - 250));
        nombres.Add("cortado a 3 bytes de un cuadro"); datos.Add(Trozo(base0, 0, base0.Length - 1));
        byte[] id31 = new byte[128]; id31[0] = (byte)'T'; id31[1] = (byte)'A'; id31[2] = (byte)'G';
        nombres.Add("con ID3v1 al final"); datos.Add(Une(base0, id31));
        nombres.Add("con ID3v2 y ID3v1"); datos.Add(Une(id3, base0, id31));
        nombres.Add("tres veces seguido"); datos.Add(Une(base0, base0, base0));
        nombres.Add("dos frecuencias (44,1 y luego 48 kHz)"); datos.Add(Une(base0, r48));
        nombres.Add("dos frecuencias (el segundo cuadro ya es de otra)"); datos.Add(Une(Trozo(base0, 0, 418), r48));
        nombres.Add("estereo y luego mono"); datos.Add(Une(base0, mono));
        nombres.Add("48 kHz y luego mono"); datos.Add(Une(r48, mono));
        nombres.Add("vacio"); datos.Add(new byte[0]);
        nombres.Add("3 bytes"); datos.Add(new byte[] { 0xFF, 0xFB, 0x90 });
        nombres.Add("100 ceros"); datos.Add(new byte[100]);
        nombres.Add("127 bytes del principio"); datos.Add(Trozo(base0, 0, 127));
        nombres.Add("128 bytes del principio"); datos.Add(Trozo(base0, 0, 128));
        nombres.Add("solo un ID3v1"); datos.Add(id31);
        nombres.Add("un solo cuadro"); datos.Add(Trozo(base0, 0, 418));
        nombres.Add("dos cuadros"); datos.Add(Trozo(base0, 0, 836));
        nombres.Add("sin cuadros (10 KB al azar sin 0xFF)"); datos.Add(basura2.Length > 0 ? Une(basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2, basura2) : null);
        // CBR de 128 kbps repetido muchas veces (indice largo; 38 cuadros por segundo)
        byte[] repetido = new byte[base0.Length * 40];
        for (int i = 0; i < 40; i++) Array.Copy(base0, 0, repetido, i * base0.Length, base0.Length);
        nombres.Add("40 veces seguido (indice largo)"); datos.Add(repetido);

        // Saltos a muestras exactas: en el borde de un cuadro, una despues, dos, la ultima, y el borde siguiente (el indice se lee por dentro del de NAudio).
        for (int k = 0; k < datos.Count; k++)
        {
            Graba[] g = new Graba[1];
            Mp3FileReader a, b;
            try { a = Abre(0, datos[k], g, false); b = Abre(1, datos[k], g, false); } catch { continue; }
            using (a) using (b)
            {
                System.Collections.IList lista = (System.Collections.IList)typeof(Mp3FileReader).GetField("tableOfContents", T).GetValue(a);
                int bps = a.WaveFormat.BlockAlign;
                List<long> objetivos = new List<long>();
                foreach (int idx in new int[] { 0, 1, 2, 3, lista.Count / 2, lista.Count - 2, lista.Count - 1 })
                {
                    if (idx < 0 || idx >= lista.Count) continue;
                    object e = lista[idx]; Type te = e.GetType();
                    long sp = (long)te.GetProperty("SamplePosition").GetValue(e, null); int sc = (int)te.GetProperty("SampleCount").GetValue(e, null);
                    foreach (long d in new long[] { 0, 1, 2, sc - 1, sc, sc + 1 }) objetivos.Add(sp + d);
                }
                objetivos.Add(a.Length / bps); objetivos.Add(a.Length / bps + 10); objetivos.Add(-5);
                bool igual = true; string primera = null;
                byte[] ba = new byte[5000], bb = new byte[5000];
                foreach (long m in objetivos)
                {
                    a.Position = m * bps; b.Position = m * bps;
                    int na = a.Read(ba, 0, ba.Length), nb = b.Read(bb, 0, bb.Length);
                    bool ig = na == nb && a.Position == b.Position;
                    for (int i = 0; ig && i < na; i++) if (ba[i] != bb[i]) ig = false;
                    if (!ig && primera == null) primera = "muestra " + m;
                    igual &= ig;
                }
                Check(igual, nombres[k] + ": " + objetivos.Count + " saltos a muestras exactas (bordes de cuadro, +1, +2, ultima, final): los mismos bytes y posicion" + (primera != null ? " (primera diferencia en " + primera + ")" : ""));
            }
        }

        int propios = 0, cayo = 0;
        for (int k = 0; k < datos.Count; k++)
        {
            List<string> dA, dB; int pA, pB; string eA, eB; bool propioA, propioB; string iA, iB;
            string a = Recorre(0, datos[k], out dA, out pA, out eA, out propioA, out iA);
            string b = Recorre(1, datos[k], out dB, out pB, out eB, out propioB, out iB);
            bool mismoDiario = (dA == null && dB == null) || (dA != null && dB != null && dA.Count == dB.Count);
            if (mismoDiario && dA != null) for (int i = 0; i < dA.Count; i++)
                {
                    if (dA[i] != dB[i]) { mismoDiario = false; Console.WriteLine("    distinto en " + i + ": " + dA[i] + " / " + dB[i]); break; }
                }
            if (a != b) { int m = 0; while (m < a.Length && m < b.Length && a[m] == b[m]) m++; Console.WriteLine("    primera diferencia en " + m + ": [" + a.Substring(Math.Max(0, m - 60), Math.Min(120, a.Length - Math.Max(0, m - 60))) + "] / [" + b.Substring(Math.Max(0, m - 60), Math.Min(120, b.Length - Math.Max(0, m - 60))) + "]"); }
            Check(a == b, nombres[k] + ": mismo formato para el descompresor, mismas etiquetas, mismo largo, los mismos bytes y la misma posicion tras cada lectura y salto" + (eA != null ? " (los dos: no abre, " + eA + ")" : ""));
            Check(eA == eB, nombres[k] + ": abre o falla igual (" + (eA ?? "abre") + " / " + (eB ?? "abre") + ")");
            Check(mismoDiario, nombres[k] + ": el descompresor recibe lo mismo (" + (dA == null ? 0 : dA.Count) + " cuadros y Reset)");
            Check(pB == 0 && pA == 0, nombres[k] + ": los tres cuadros anteriores siguen intactos mientras se lee el siguiente");
            if (eA == null)
            {
                Check(iA == iB || !propioB, nombres[k] + ": el MISMO indice (" + iA.Split(';').Length + " entradas)");
                if (propioB) propios++; else cayo++;
            }
        }
        Console.WriteLine("    " + propios + " archivos por el indice propio, " + cayo + " por el constructor de NAudio");
        Check(propios >= 15, "la mayoria de los archivos pasan por el indice propio (" + propios + ")");
        // lo que debe caer al constructor de siempre: el cambio de frecuencia a mitad, mas alla de los dos primeros cuadros
        {
            List<string> d; int p; string e; bool propio; string ind;
            Recorre(1, Une(base0, r48), out d, out p, out e, out propio, out ind);
            Check(e == "InvalidOperationException" && !propio, "cambiar de frecuencia a mitad del archivo: NAudio lo rechaza y Abre contesta lo mismo (cae al constructor)");
        }

        // Apagado (las pruebas): con IndicePropio = false, el constructor de siempre.
        {
            FieldInfo fi = tLigero.GetField("IndicePropio", T);
            fi.SetValue(null, false);
            try
            {
                List<string> d; int p; string e; bool propio; string ind;
                string sinPropio = Recorre(1, base0, out d, out p, out e, out propio, out ind);
                string deNaudio = Recorre(0, base0, out d, out p, out e, out propio, out ind);
                Check(!propio && sinPropio == deNaudio, "con IndicePropio apagado, Abre es el constructor de siempre");
            }
            finally { fi.SetValue(null, true); }
        }

        // La basura y el tiempo de abrir: una cancion larga (40 veces el MP3 de prueba, mas de 1.700 cuadros por vuelta... ) con NAudio y con Abre.
        byte[] larga = new byte[base0.Length * 600];
        for (int i = 0; i < 600; i++) Array.Copy(base0, 0, larga, i * base0.Length, base0.Length);
        double[] ms = new double[2]; long[] bytes = new long[2]; int cuadros = 0;
        for (int vuelta = 0; vuelta < 6; vuelta++)
            for (int lig = 0; lig < 2; lig++)
            {
                Graba[] g = new Graba[1];
                long b0 = Reservado(); System.Diagnostics.Stopwatch w = System.Diagnostics.Stopwatch.StartNew();
                Mp3FileReader r = Abre(lig, larga, g, false);
                w.Stop(); long b1 = Reservado();
                if (vuelta >= 1) { ms[lig] += w.Elapsed.TotalMilliseconds; bytes[lig] = b1 - b0; }
                if (lig == 1) cuadros = (int)tLigero.GetField("indiceN", T).GetValue(r);
                r.Dispose();
            }
        Console.WriteLine("    abrir una cancion de " + cuadros + " cuadros: NAudio " + (ms[0] / 5).ToString("0.00") + " ms " + bytes[0] / 1024 + " KB, Abre " + (ms[1] / 5).ToString("0.00") + " ms " + bytes[1] / 1024 + " KB");
        Check(ms[1] < ms[0], "abrir con el indice propio tarda menos que con el de NAudio");
        if (mideReserva != null) Check(bytes[0] > 1024 * 1024 && bytes[1] * 4 < bytes[0], "y deja menos de la cuarta parte de la basura");
        else Console.WriteLine("    OMITIDA solo la medida de reservas: este CLR (.NET 3.5) no trae GetAllocatedBytesForCurrentThread; indice, bytes y cuadros si comprobados.");

        // El enganche: la medida y la reproduccion desde la memoria abren por Abre.
        Type tf = asm.GetType("Rocola.Form1", true);
        Type tl = tf.GetNestedType("LectorMedida", T);
        MethodInfo abreM = tl.GetMethod("AbreMp3Medida", T);
        Check(Codigo.Llama(abreM, "LectorMp3Ligero.Abre") && !Codigo.Llama(abreM, "LectorMp3Ligero..ctor"), "la medida (LectorMedida) abre los MP3 por LectorMp3Ligero.Abre");
        MethodInfo abreP = tf.GetMethod("AbreMp3", T, null, new Type[] { typeof(string), typeof(Stream) }, null);
        Check(abreP != null && Codigo.Llama(abreP, "LectorMp3Ligero.Abre") && !Codigo.Llama(abreP, "LectorMp3Ligero..ctor"), "y la reproduccion desde la memoria (AbreMp3) tambien");

        Console.WriteLine(mal == 0 ? "  OK indicemp3: " + ok + " comprobaciones (el mismo indice, formato, etiquetas y bytes que NAudio, sin su basura)" : "  indicemp3: " + mal + " FALLOS de " + (ok + mal));
        return mal;
    }
}
