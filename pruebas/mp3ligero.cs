using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// LOS MP3 DEL REPASO, SIN BASURA (13-10-2026; Mp3Ligero.cs).
//
// El repaso lee los MP3 con LectorMp3Ligero: el Mp3FileReader de NAudio de siempre, con la lectura de cada cuadro
// reutilizando su memoria. Aqui no hay descompresor de Windows, asi que se imita uno que APUNTA todo lo que recibe
// (cada cuadro byte a byte, sus datos de cabecera y cada Reset) y suelta bytes que dependen del cuadro (y, a veces, un
// cuadro doble, para pasar por ese camino del lector). Con el mismo archivo, NAudio y el ligero tienen que:
//   - darle al descompresor exactamente lo mismo, en el mismo orden;
//   - soltar los mismos bytes con lecturas de tamanos raros y saltos (al principio, a la mitad, mas alla del final);
//   - marcar la misma posicion despues de cada paso.
// Archivos: los MP3 de verdad de esta carpeta (mono, 48 kHz, MPEG-2, VBR, sin etiqueta, ...) y variantes en memoria:
// con una caratula ID3v2 delante, con basura entre cuadros (la busqueda byte a byte), cortado a mitad de un cuadro y
// con ID3v1 al final. El descompresor recuerda los tres cuadros anteriores, como el de coma flotante, y mira que nadie
// se los cambie. Y se mide la basura: leer una cancion larga entera con el ligero deja menos del 2 % que con NAudio.
class Mp3LigeroPrueba
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }

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

    static Type tLigero;
    static Mp3FileReader Abre(bool ligero, byte[] datos, Graba[] g, bool apunta)
    {
        Mp3FileReader.FrameDecompressorBuilder b = delegate(WaveFormat wf) { g[0] = new Graba(wf, apunta); return g[0]; };
        MemoryStream ms = new MemoryStream(datos, false);
        if (!ligero) return new Mp3FileReader(ms, b);
        return (Mp3FileReader)Activator.CreateInstance(tLigero, T, null, new object[] { ms, b }, null);
    }

    // Lee todo con tamanos raros y saltos; devuelve lo que salio y lo que se apunto.
    static string Recorre(bool ligero, byte[] datos, out List<string> diario, out int pisados, out string error)
    {
        Graba[] g = new Graba[1]; diario = null; pisados = 0; error = null;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        Mp3FileReader r;
        try { r = Abre(ligero, datos, g, true); }
        catch (Exception ex)
        {
            if (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
            error = ex.GetType().Name; return "no abre: " + ex.GetType().Name + " " + ex.Message;
        }
        using (r)
        {
            sb.Append("L ").Append(r.Length).Append(" ").Append(r.TotalTime.Ticks).Append(" ").Append(r.WaveFormat).Append("|");
            int[] tam = { 17640, 1, 333, 7777, 4096, 2, 4608, 9216, 65536 };
            byte[] buf = new byte[65536];
            double total = r.TotalTime.TotalSeconds;
            double[] saltos = { 0.5, 0.0, total + 5, total / 2, total * 0.9, 0.013 };
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
        if (tLigero == null) { Console.WriteLine("  mp3ligero: " + mal + " FALLOS de " + (ok + mal)); return 1; }

        // Los archivos: los de verdad y las variantes.
        List<string> nombres = new List<string>(); List<byte[]> datos = new List<byte[]>();
        foreach (string f in Directory.GetFiles(".", "*.mp3")) { nombres.Add(Path.GetFileName(f)); datos.Add(File.ReadAllBytes(f)); }
        byte[] base0 = File.ReadAllBytes("flotante_overs.mp3");
        byte[] id3 = new byte[10 + 70000]; id3[0] = (byte)'I'; id3[1] = (byte)'D'; id3[2] = (byte)'3'; id3[3] = 3;
        int t = 70000; id3[6] = (byte)((t >> 21) & 0x7F); id3[7] = (byte)((t >> 14) & 0x7F); id3[8] = (byte)((t >> 7) & 0x7F); id3[9] = (byte)(t & 0x7F);
        Random rnd = new Random(5); for (int i = 10; i < id3.Length; i++) id3[i] = (byte)rnd.Next(256);   // con 0xFF dentro, como una caratula
        nombres.Add("con caratula ID3v2 de 70 KB"); datos.Add(Une(id3, base0));
        byte[] basura = new byte[300]; for (int i = 0; i < basura.Length; i++) basura[i] = (byte)(i % 5 == 0 ? 0xFF : rnd.Next(256));
        int mitad = base0.Length / 2;
        // Con 0xFF sueltos en la basura, NAudio cree ver un cuadro de otra frecuencia y no lo abre: el ligero, igual.
        nombres.Add("con basura con 0xFF entre cuadros"); datos.Add(Une(Trozo(base0, 0, mitad), basura, Trozo(base0, mitad, base0.Length - mitad)));
        byte[] basura2 = new byte[301]; for (int i = 0; i < basura2.Length; i++) basura2[i] = (byte)rnd.Next(255);
        nombres.Add("con basura entre cuadros (busca la sincronia byte a byte)"); datos.Add(Une(Trozo(base0, 0, mitad), basura2, Trozo(base0, mitad, base0.Length - mitad)));
        nombres.Add("cortado a mitad de un cuadro"); datos.Add(Trozo(base0, 0, base0.Length - 250));
        byte[] id31 = new byte[128]; id31[0] = (byte)'T'; id31[1] = (byte)'A'; id31[2] = (byte)'G';
        nombres.Add("con ID3v1 al final"); datos.Add(Une(base0, id31));
        nombres.Add("tres veces seguido"); datos.Add(Une(base0, base0, base0));

        for (int k = 0; k < datos.Count; k++)
        {
            List<string> dA, dB; int pA, pB; string eA, eB;
            string a = Recorre(false, datos[k], out dA, out pA, out eA);
            string b = Recorre(true, datos[k], out dB, out pB, out eB);
            bool mismoDiario = (dA == null && dB == null) || (dA != null && dB != null && dA.Count == dB.Count);
            if (mismoDiario && dA != null) for (int i = 0; i < dA.Count; i++)
                {
                    // El tamano de RawData es el del cuadro en los dos (el ligero guarda un arreglo de su tamano exacto).
                    if (dA[i] != dB[i]) { mismoDiario = false; Console.WriteLine("    distinto en " + i + ": " + dA[i] + " / " + dB[i]); break; }
                }
            Check(a == b, nombres[k] + ": los mismos bytes y la misma posicion tras cada lectura y salto" + (eA != null ? " (los dos: no abre, " + eA + ")" : ""));
            Check(mismoDiario, nombres[k] + ": el descompresor recibe lo mismo (" + (dA == null ? 0 : dA.Count) + " cuadros y Reset)");
            Check(pB == 0 && pA == 0, nombres[k] + ": los tres cuadros anteriores siguen intactos mientras se lee el siguiente");
        }

        // La basura: una cancion larga (el MP3 de prueba, 60 veces seguido) leida entera en trozos de 100 ms.
        byte[] larga = new byte[base0.Length * 60];
        for (int i = 0; i < 60; i++) Array.Copy(base0, 0, larga, i * base0.Length, base0.Length);
        long[] basuraDe = new long[2]; ulong[] suma = new ulong[2]; double seg = 0;
        for (int vuelta = 0; vuelta < 2; vuelta++)
            for (int lig = 0; lig < 2; lig++)
            {
                Graba[] g = new Graba[1];
                using (Mp3FileReader r = Abre(lig == 1, larga, g, false))
                {
                    byte[] buf = new byte[17640];
                    r.Read(buf, 0, buf.Length);   // calentar
                    long b0 = Reservado();
                    long n = 0; int m;
                    while ((m = r.Read(buf, 0, buf.Length)) > 0) n += m;
                    basuraDe[lig] = Reservado() - b0;
                    seg = n / (double)r.WaveFormat.AverageBytesPerSecond;
                    suma[lig] = g[0].Suma;
                }
            }
        Check(suma[0] == suma[1], "la cancion larga: el descompresor recibe lo mismo con los dos");
        if (mideReserva != null)
        {
            Console.WriteLine("    cancion de " + seg.ToString("0") + " s leida entera: NAudio " + basuraDe[0] / 1024 + " KB de basura, ligero " + basuraDe[1] / 1024 + " KB");
            Check(basuraDe[0] > 1024 * 1024 && basuraDe[1] * 50 < basuraDe[0], "la cancion larga deja menos del 2 % de la basura de NAudio");
        }
        else Console.WriteLine("    OMITIDA solo la medida de reservas: este CLR (.NET 3.5) no trae GetAllocatedBytesForCurrentThread; bytes y cuadros si comprobados.");

        // La medida abre el ligero, con ID3 y sin el.
        Type tf = asm.GetType("Rocola.Form1", true);
        Type tl = tf.GetNestedType("LectorMedida", T);
        MethodInfo abre = tl.GetMethod("AbreMp3Medida", T);
        Check(Codigo.Llama(abre, "LectorMp3Ligero..ctor"), "la medida (LectorMedida) abre los MP3 con el lector ligero");
        Check(!Codigo.Llama(abre, "Form1.AbreMp3"), "sin ID3 tambien: ya no pasa por el lector de la reproduccion");

        Console.WriteLine(mal == 0 ? "  OK mp3ligero: " + ok + " comprobaciones (los mismos cuadros y bytes que NAudio, sin su basura)" : "  mp3ligero: " + mal + " FALLOS de " + (ok + mal));
        return mal;
    }
}
