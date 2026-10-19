using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
// LA REPRODUCCION DESDE LA MEMORIA, CON EL LECTOR LIGERO (19-10-2026): CancionEnMemoria.cs (AbreMp3 de memoria) y Mp3Ligero.cs.
//
// NAudio 1.9 crea por cada cuadro de MP3 (unos 38 por segundo) un Mp3Frame y dos arreglos y los tira: 5,8 a 9,6 MB de basura por cancion. El repaso ya leia
// con el lector ligero (la bateria mp3ligero lo compara cuadro a cuadro con NAudio); ahora tambien la reproduccion desde la memoria. Lo que aqui se exige es
// lo que oye el bar: con el descompresor de VERDAD de la rocola para los MP3 que llegan al maximo (NLayer, en coma flotante, el de FlotanteNLayer.cs) y
// leyendo como lee la reproduccion (SampleChannel, bloques de 4.096 muestras), el PCM que sale tiene que ser el MISMO, muestra a muestra, que con el lector de NAudio:
//   1) una cancion entera (cada MP3 de pruebas que NLayer abre), incluido el ultimo bloque corto;
//   2) un salto a mitad de cancion y otro hacia atras (lo que hace saltar un silencio o un relevo) y seguir leyendo;
//   3) reanudar: leer un rato, anotar donde se va, cerrar, abrir otro lector, saltar ahi y seguir (como al volver a una cancion);
//   4) el relevo sin pausa: una cancion entera y, sin parar, la siguiente en otro lector (el PCM concatenado, y que cerrar el primero no toca el segundo);
//   5) la pantalla pregunta mientras la musica lee: TotalTime, Length y Position desde otro hilo no cambian lo que sale ni dan error;
//   6) el enganche: AbreMp3 de memoria abre el ligero si NAudio es el esperado y el de siempre si no, y el que se abre es un Mp3FileReader (lo que espera todo lo demas).
class ReproLigera
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, TLigero, TDecodificacion;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }

    static Mp3FileReader Abre(bool ligero, byte[] datos)
    {
        Func<WaveFormat, IMp3FrameDecompressor> fabrica = (Func<WaveFormat, IMp3FrameDecompressor>)TDecodificacion.GetField("FabricaNLayer", T).GetValue(null);
        Mp3FileReader.FrameDecompressorBuilder b = delegate(WaveFormat wf) { return fabrica(wf); };
        MemoryStream ms = new MemoryStream(datos, 0, datos.Length, false, true);
        if (!ligero) return new Mp3FileReader(ms, b);
        return (Mp3FileReader)Activator.CreateInstance(TLigero, T, null, new object[] { ms, b }, null);
    }
    // Lee como la reproduccion: SampleChannel (el que arma FileAudioSource) en bloques; devuelve el hash corrido de las muestras como bits.
    sealed class Resultado
    {
        public ulong Hash = 14695981039346656037UL; public long Muestras; public string Notas = "";
        public void Suma(float[] b, int n)
        {
            for (int i = 0; i < n; i++) { Hash ^= (uint)BitConverter.ToInt32(BitConverter.GetBytes(b[i]), 0); Hash *= 1099511628211UL; }
            Muestras += n;
        }
    }
    static void Lee(SampleChannel c, Resultado r, int bloques)   // bloques < 0: hasta el final
    {
        float[] b = new float[4096];
        for (int k = 0; bloques < 0 || k < bloques; k++)
        {
            int n;
            try { n = c.Read(b, 0, b.Length); }
            catch (Exception e) { r.Notas += "E:" + e.GetType().Name + ";"; break; }   // un MP3 roto falla igual con los dos lectores
            if (n <= 0) break;
            r.Suma(b, n);
        }
    }
    static string Completa(bool ligero, byte[] d)
    {
        using (Mp3FileReader m = Abre(ligero, d))
        {
            SampleChannel c = new SampleChannel(m, false); Resultado r = new Resultado();
            Lee(c, r, -1);
            return r.Hash + ":" + r.Muestras + ":" + m.Position + ":" + m.TotalTime.Ticks + ":" + m.Length + ":" + r.Notas;
        }
    }
    static string Saltos(bool ligero, byte[] d)
    {
        using (Mp3FileReader m = Abre(ligero, d))
        {
            SampleChannel c = new SampleChannel(m, false); Resultado r = new Resultado();
            int ba = m.WaveFormat.BlockAlign;
            long total = m.Length;
            Lee(c, r, 20);
            m.Position = (total / 2 / ba) * ba;                       // a mitad de cancion
            r.Notas += "S" + m.Position + ";"; Lee(c, r, 15);
            m.Position = ((total / 5) / ba) * ba;                     // hacia atras
            r.Notas += "S" + m.Position + ";"; Lee(c, r, 10);
            m.Position = 0; r.Notas += "S0;"; Lee(c, r, 5);          // al principio
            m.Position = ((total - 7 * ba) / ba) * ba;                // casi al final
            r.Notas += "S" + m.Position + ";"; Lee(c, r, -1);
            m.CurrentTime = TimeSpan.FromSeconds(0.77); r.Notas += "T" + m.Position + ";"; Lee(c, r, 3);   // por tiempo, como Salta
            return r.Hash + ":" + r.Muestras + ":" + r.Notas + m.Position;
        }
    }
    static string Reanuda(bool ligero, byte[] d)
    {
        long donde; Resultado r = new Resultado();
        using (Mp3FileReader m = Abre(ligero, d))
        {
            SampleChannel c = new SampleChannel(m, false);
            Lee(c, r, 40);
            donde = m.Position;
        }
        using (Mp3FileReader m = Abre(ligero, d))
        {
            SampleChannel c = new SampleChannel(m, false);
            m.Position = donde;
            Lee(c, r, -1);
            return r.Hash + ":" + r.Muestras + ":" + donde + ":" + m.Position + ":" + r.Notas;
        }
    }
    static string Relevo(bool ligero, byte[] a, byte[] b)
    {
        Resultado r = new Resultado();
        Mp3FileReader ma = Abre(ligero, a), mb = null;
        SampleChannel ca = new SampleChannel(ma, false), cb = null;
        Lee(ca, r, 30);
        mb = Abre(ligero, b); cb = new SampleChannel(mb, false);   // la siguiente, ya lista mientras suena la primera
        Lee(ca, r, -1);
        ma.Dispose();                                                // la primera acaba y se cierra
        Lee(cb, r, -1);
        string fin = r.Hash + ":" + r.Muestras + ":" + mb.Position + ":" + r.Notas;
        mb.Dispose();
        return fin;
    }
    static string ConPreguntas(bool ligero, byte[] d, out int errores)
    {
        int err = 0;
        using (Mp3FileReader m = Abre(ligero, d))
        {
            SampleChannel c = new SampleChannel(m, false); Resultado r = new Resultado();
            bool fin = false;
            Thread pregunta = new Thread(delegate()
            {
                while (!fin)
                {
                    try { long p = m.Position; TimeSpan t = m.TotalTime; long l = m.Length; if (p < 0 || l <= 0 || t.Ticks <= 0) Interlocked.Increment(ref err); }
                    catch { Interlocked.Increment(ref err); }
                    Thread.Sleep(0);
                }
            });
            pregunta.Start();
            try { Lee(c, r, -1); } finally { fin = true; pregunta.Join(); }
            errores = err;
            return r.Hash + ":" + r.Muestras + ":" + r.Notas;
        }
    }
    static long Reservado()
    {
        MethodInfo mi = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread");
        return mi == null ? -1 : (long)mi.Invoke(null, null);
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true); TLigero = asm.GetType("Rocola.LectorMp3Ligero"); TDecodificacion = asm.GetType("Rocola.DecodificacionFlotante");
        Check(TLigero != null && TDecodificacion != null && (bool)TLigero.GetField("Posible", T).GetValue(null), "la rocola trae el lector ligero y NAudio es el esperado");
        List<string> archivos = new List<string>();
        foreach (string f in Directory.GetFiles(".", "*.mp3")) archivos.Add(f);
        if (Directory.Exists("mp3s")) foreach (string f in Directory.GetFiles("mp3s", "*.mp3")) archivos.Add(f);
        archivos.Sort(StringComparer.Ordinal);
        List<byte[]> buenos = new List<byte[]>(); List<string> nombres = new List<string>();
        foreach (string f in archivos)
        {
            byte[] d = File.ReadAllBytes(f);
            try { using (Mp3FileReader m = Abre(false, d)) { if (m.Length <= 0) continue; } } catch { continue; }   // los que NAudio+NLayer no abren, tampoco aqui
            buenos.Add(d); nombres.Add(Path.GetFileName(f));
        }
        Console.WriteLine("  (" + buenos.Count + " MP3 de pruebas que se abren con NLayer: " + string.Join(", ", nombres.ToArray()) + ")");
        Check(buenos.Count >= 4, "hay MP3 de pruebas con los que comparar");

        int malosA = 0, malosB = 0, malosC = 0, malosD = 0, malosE = 0; long muestras = 0;
        for (int k = 0; k < buenos.Count; k++)
        {
            string a = Completa(false, buenos[k]), b = Completa(true, buenos[k]);
            if (a != b) { malosA++; Console.WriteLine("    entera distinta: " + nombres[k]); }
            muestras += long.Parse(a.Split(':')[1]);
            if (Saltos(false, buenos[k]) != Saltos(true, buenos[k])) { malosB++; Console.WriteLine("    saltos distintos: " + nombres[k]); }
            if (Reanuda(false, buenos[k]) != Reanuda(true, buenos[k])) { malosC++; Console.WriteLine("    reanudar distinto: " + nombres[k]); }
            byte[] sig = buenos[(k + 1) % buenos.Count];
            if (Relevo(false, buenos[k], sig) != Relevo(true, buenos[k], sig)) { malosD++; Console.WriteLine("    relevo distinto: " + nombres[k]); }
            int e1, e2;
            string pa = ConPreguntas(false, buenos[k], out e1), pb = ConPreguntas(true, buenos[k], out e2);
            if (pa != pb || e1 != 0 || e2 != 0) { malosE++; Console.WriteLine("    con preguntas distinto: " + nombres[k] + " errores " + e1 + "/" + e2); }
        }
        Check(malosA == 0, "1) cancion entera: el mismo PCM en coma flotante, muestra a muestra (" + muestras + " muestras en " + buenos.Count + " MP3), la misma posicion, duracion y largo");
        Check(malosB == 0, "2) saltos a mitad, hacia atras, al principio, casi al final y por tiempo, y seguir leyendo: el mismo PCM y las mismas posiciones");
        Check(malosC == 0, "3) reanudar (leer, anotar donde se va, abrir otro lector, saltar ahi, seguir): el mismo PCM");
        Check(malosD == 0, "4) relevo sin pausa (una cancion y, sin parar, la siguiente en otro lector; cerrar la primera no toca la segunda): el mismo PCM concatenado");
        Check(malosE == 0, "5) la pantalla pregunta Position, TotalTime y Length desde otro hilo mientras la musica lee: sin errores y el mismo PCM");

        // la basura
        byte[] larga = buenos[0];
        long basuraN = 0, basuraL = 0;
        if (Reservado() >= 0)
        {
            for (int v = 0; v < 2; v++)
                for (int lig = 0; lig < 2; lig++)
                {
                    using (Mp3FileReader m = Abre(lig == 1, larga))
                    {
                        SampleChannel c = new SampleChannel(m, false); Resultado r = new Resultado();
                        Lee(c, r, 2);
                        long b0 = Reservado();
                        Lee(c, r, -1);
                        long gasto = Reservado() - b0;
                        if (lig == 1) basuraL = gasto; else basuraN = gasto;
                    }
                }
            Console.WriteLine("     " + nombres[0] + " leido entero como la reproduccion (incluye SampleChannel y NLayer): NAudio " + basuraN / 1024 + " KB asignados, ligero " + basuraL / 1024 + " KB (Mono, sintetico)");
            Check(basuraL < basuraN, "el lector ligero asigna menos que el de NAudio al leer la cancion entera");
        }
        else Console.WriteLine("     OMITIDA solo la medida de reservas: este CLR no trae GetAllocatedBytesForCurrentThread.");

        // el enganche
        MethodInfo abre = TF.GetMethod("AbreMp3", T, null, new Type[] { typeof(string), typeof(Stream) }, null);
        int lee, escribe;
        if (abre != null) Codigo.UsosDeCampo(abre, "Posible", out lee, out escribe); else { lee = 0; }
        Check(abre != null && abre.ReturnType == typeof(Mp3FileReader) && Codigo.Llama(abre, "LectorMp3Ligero..ctor") && Codigo.Llama(abre, "Mp3FileReader..ctor") && lee > 0,
              "6) AbreMp3 de memoria devuelve un Mp3FileReader: el ligero si Posible, el de siempre si no (el armado del descompresor y la comprobacion de la coma flotante, como siempre: lo vigila enmemoria)");
        Type fas = TF.GetNestedType("FileAudioSource", T);
        ConstructorInfo ctor = fas.GetConstructor(T, null, new Type[] { typeof(string) }, null);
        Check(Codigo.Llama(ctor, "CancionEnMemoria") && Codigo.Llama(ctor, "AbreMp3"), "y la fuente de audio de un MP3 lo abre por ahi, con caida al archivo si la memoria no pudo");
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
