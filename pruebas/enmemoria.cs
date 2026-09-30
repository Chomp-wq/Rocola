using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NAudio.Wave;
// LA CANCION, EN MEMORIA (CancionEnMemoria.cs).
//
// Un MP3 se abre desde la memoria: la lectura por adelantado deja los bytes de la siguiente, y si una cancion llega sin
// ellos se lee entera de una vez al abrirla. Aqui se mira:
//   1) que se oye lo mismo: el lector de NAudio entrega exactamente lo mismo desde la memoria que desde el archivo (con
//      un descompresor de prueba que convierte cada cuadro en muestras, porque el de Windows no existe aqui), tambien al
//      saltar a mitad de cancion; y que el AbreMp3 de memoria es el de siempre letra por letra;
//   2) el cargador: MP3 entero y con los mismos bytes; lo de la lectura por adelantado se usa una sola vez, y no si el
//      archivo cambio o es otro; ni lo que no es MP3 ni lo que pasa del tope; y si la cola cambia, se suelta;
//   3) que mientras suena no depende del disco: con el archivo vaciado a mitad de cancion sigue entera (del archivo, se
//      cortaba); y cuantas lecturas le cuesta al disco cada forma;
//   4) el enganche: la fuente de audio abre desde la memoria y cae al archivo si no; la lectura por adelantado guarda los
//      bytes sin dejar de hacer sus pausas; soltar la cola suelta tambien esto; al cerrar la fuente, suelta la memoria.
class EnMemoria
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static object Call(string n, params object[] a) { return TF.GetMethod(n, T, null, TiposDe(a), null).Invoke(null, a); }
    static Type[] TiposDe(object[] a) { Type[] t = new Type[a.Length]; for (int i = 0; i < a.Length; i++) t[i] = a[i] == null ? typeof(string) : a[i].GetType(); return t; }
    static int Contador(string n) { return (int)TF.GetField(n, T).GetValue(null); }

    // ---- un MP3 de mentira: cuadros MPEG-1 capa III de 128 kbps a 44,1 kHz con relleno distinto en cada uno ----
    static byte[] Mp3(int cuadros, int semilla)
    {
        MemoryStream ms = new MemoryStream();
        byte[] id3 = { (byte)'I', (byte)'D', (byte)'3', 3, 0, 0, 0, 0, 0, 20 };   // una etiqueta ID3v2 de 20 bytes delante
        ms.Write(id3, 0, id3.Length); ms.Write(new byte[20], 0, 20);
        Random rnd = new Random(semilla);
        for (int c = 0; c < cuadros; c++)
        {
            byte[] f = new byte[417];
            f[0] = 0xFF; f[1] = 0xFB; f[2] = 0x90; f[3] = 0x64;
            for (int i = 4; i < f.Length; i++) f[i] = (byte)(rnd.Next(0, 0xFF));   // nunca 0xFF: ninguna sincronia falsa
            ms.Write(f, 0, f.Length);
        }
        return ms.ToArray();
    }
    // Descompresor de prueba: cada cuadro -> 1152 muestras estereo de 16 bits sacadas de sus bytes.
    sealed class DePrueba : IMp3FrameDecompressor
    {
        public WaveFormat OutputFormat { get { return new WaveFormat(44100, 16, 2); } }
        public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
        {
            byte[] r = frame.RawData;
            for (int i = 0; i < 4608; i++) dest[destOffset + i] = (byte)(r[(i * 7) % r.Length] ^ (i & 0xFF));
            return 4608;
        }
        public void Reset() { }
        public void Dispose() { }
    }
    static Mp3FileReader Lector(Stream s) { return new Mp3FileReader(s, delegate(WaveFormat wf) { return new DePrueba(); }); }
    static byte[] LeeTodo(WaveStream w) { MemoryStream o = new MemoryStream(); byte[] b = new byte[5000]; int n; while ((n = w.Read(b, 0, b.Length)) > 0) o.Write(b, 0, n); return o.ToArray(); }
    static bool Iguales(byte[] a, byte[] b) { if (a == null || b == null || a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }

    sealed class Contando : Stream
    {
        readonly Stream s; public int Lecturas, Saltos;
        public Contando(Stream s) { this.s = s; }
        public override bool CanRead { get { return true; } } public override bool CanSeek { get { return true; } } public override bool CanWrite { get { return false; } }
        public override long Length { get { return s.Length; } }
        public override long Position { get { return s.Position; } set { Saltos++; s.Position = value; } }
        public override int Read(byte[] b, int o, int c) { Lecturas++; return s.Read(b, o, c); }
        public override long Seek(long o, SeekOrigin k) { Saltos++; return s.Seek(o, k); }
        public override void Flush() { } public override void SetLength(long v) { throw new NotSupportedException(); }
        public override void Write(byte[] b, int o, int c) { throw new NotSupportedException(); }
        protected override void Dispose(bool d) { if (d) s.Dispose(); base.Dispose(d); }
    }

    static void Igual(string ruta)
    {
        Console.WriteLine("\n  1) SE OYE LO MISMO\n");
        byte[] a, b, a2, b2; TimeSpan ta, tb;
        using (Mp3FileReader r1 = Lector(File.OpenRead(ruta))) { ta = r1.TotalTime; a = LeeTodo(r1); r1.CurrentTime = TimeSpan.FromSeconds(3.3); a2 = LeeTodo(r1); }
        using (Mp3FileReader r2 = Lector(new MemoryStream(File.ReadAllBytes(ruta)))) { tb = r2.TotalTime; b = LeeTodo(r2); r2.CurrentTime = TimeSpan.FromSeconds(3.3); b2 = LeeTodo(r2); }
        Check(ta == tb && a.Length > 400000 && Iguales(a, b), "desde la memoria, las mismas muestras que desde el archivo (" + a.Length / 4 + " muestras por canal) y la misma duracion");
        Check(a2.Length > 0 && Iguales(a2, b2), "y saltando a mitad de cancion (como al saltar un silencio), lo mismo desde el mismo sitio");

        string dir = Path.Combine("..", "Rocola");
        string fuente = File.Exists(Path.Combine(dir, "Form1.Sonido.cs")) ? File.ReadAllText(Path.Combine(dir, "Form1.Sonido.cs")) : null;
        string nueva = File.Exists(Path.Combine(dir, "CancionEnMemoria.cs")) ? File.ReadAllText(Path.Combine(dir, "CancionEnMemoria.cs")) : null;
        string deArchivo = Cuerpo(fuente, "private static Mp3FileReader AbreMp3(string ruta)");
        string deMemoria = Cuerpo(nueva, "private static Mp3FileReader AbreMp3(string ruta, Stream enMemoria)");
        bool igual = deArchivo != null && deMemoria != null && deArchivo == deMemoria.Replace("new Mp3FileReader(enMemoria, armador)", "new Mp3FileReader(ruta, armador)");
        MethodInfo mA = TF.GetMethod("AbreMp3", T, null, new Type[] { typeof(string) }, null);
        MethodInfo mM = TF.GetMethod("AbreMp3", T, null, new Type[] { typeof(string), typeof(Stream) }, null);
        List<string> cA = Cierres(mA), cM = Cierres(mM);
        bool mismos = cA.Count > 0 && cA.Count == cM.Count;
        for (int i = 0; mismos && i < cA.Count; i++) if (cA[i] != cM[i]) mismos = false;
        Check(mismos, "y compilado igual: el armado del descompresor de los dos AbreMp3 es, instruccion por instruccion, el mismo");
        Check(igual, "el AbreMp3 de memoria es el de siempre letra por letra (mismo descompresor, misma comprobacion de la coma flotante con el nombre del archivo, mismo cierre si falla); solo cambia de donde lee");
    }
    // Lo que el compilador saca aparte de un metodo (sus delegados), como secuencia de instrucciones.
    static List<string> Cierres(MethodInfo m)
    {
        List<string> r = new List<string>();
        if (m == null) return r;
        foreach (Codigo.Paso paso in Codigo.Pasos(m))
        {
            // ldftn/ldvirtftn: el delegado real, sin depender del nombre de su clase (distinto en Mono y csc).
            if (paso.Op != 0xFE || (paso.Op2 != 0x06 && paso.Op2 != 0x07)) continue;
            MethodBase delegado = m.Module.ResolveMethod(paso.Numero);
            StringBuilder sb = new StringBuilder();
            foreach (Codigo.Paso p in Codigo.Pasos(delegado)) sb.Append(p.Op).Append('.').Append(p.Op2).Append(' ');
            r.Add(sb.ToString());
        }
        return r;
    }
    // El cuerpo de un metodo en la fuente, sin comentarios ni espacios de mas.
    static string Cuerpo(string fuente, string firma)
    {
        if (fuente == null) return null;
        int i = fuente.IndexOf(firma, StringComparison.Ordinal);
        if (i < 0) return null;
        int a = fuente.IndexOf('{', i), n = 0, j = a;
        for (; j < fuente.Length; j++) { if (fuente[j] == '{') n++; else if (fuente[j] == '}' && --n == 0) break; }
        string c = fuente.Substring(a, j - a + 1);
        c = Regex.Replace(c, "//[^\n]*", "");
        return Regex.Replace(c, "\\s+", " ").Trim();
    }

    static void Cargador(string dir, string ruta, byte[] bytes)
    {
        Console.WriteLine("\n  2) EL CARGADOR\n");
        int leidas = Contador("EnMemoriaLeidas"), adelanto = Contador("EnMemoriaDelAdelanto");
        MemoryStream ms = (MemoryStream)Call("CancionEnMemoria", ruta);
        Check(ms != null && Iguales(ms.ToArray(), bytes) && !ms.CanWrite && Contador("EnMemoriaLeidas") == leidas + 1,
              "un MP3 que cabe: entero, con los mismos bytes, de solo lectura (leido al abrir)");

        byte[] mios = (byte[])bytes.Clone();
        TF.GetMethod("DejaEnMemoria", T).Invoke(null, new object[] { ruta, Call("AudioFileSignature", ruta), mios });
        ms = (MemoryStream)Call("CancionEnMemoria", ruta);
        Check(ms != null && object.ReferenceEquals(ms.GetBuffer(), mios) && Contador("EnMemoriaDelAdelanto") == adelanto + 1,
              "lo que dejo la lectura por adelantado se usa tal cual: no se vuelve a leer ni se copia");
        ms = (MemoryStream)Call("CancionEnMemoria", ruta);
        Check(ms != null && !object.ReferenceEquals(ms.GetBuffer(), mios) && Contador("EnMemoriaLeidas") == leidas + 2,
              "y se entrega una sola vez (la segunda, se lee): nunca dos lectores sobre los mismos bytes");

        TF.GetMethod("DejaEnMemoria", T).Invoke(null, new object[] { ruta, Call("AudioFileSignature", ruta), (byte[])bytes.Clone() });
        File.SetLastWriteTimeUtc(ruta, DateTime.UtcNow.AddMinutes(-5));   // el archivo cambio desde que se leyo
        ms = (MemoryStream)Call("CancionEnMemoria", ruta);
        Check(ms != null && Contador("EnMemoriaLeidas") == leidas + 3 && Contador("EnMemoriaDelAdelanto") == adelanto + 1,
              "si el archivo cambio desde la lectura por adelantado, no se usa: se lee el de ahora");

        string otra = Path.Combine(dir, "otra.mp3"); File.WriteAllBytes(otra, bytes);
        TF.GetMethod("DejaEnMemoria", T).Invoke(null, new object[] { otra, Call("AudioFileSignature", otra), (byte[])bytes.Clone() });
        ms = (MemoryStream)Call("CancionEnMemoria", ruta);
        Check(ms != null && Contador("EnMemoriaDelAdelanto") == adelanto + 1 && TF.GetField("adelantoDatos", T).GetValue(null) != null,
              "la de otra cancion no se usa para esta, ni se pierde (sigue esperando a la suya)");
        object f = Codigo.FormSinEncender(TF);
        TF.GetMethod("SueltaLookahead", T).Invoke(f, null);
        Check(TF.GetField("adelantoDatos", T).GetValue(null) == null, "y si la cola cambia (se suelta lo que se miraba por delante), se suelta tambien: como mucho hay una guardada");

        string wav = Path.Combine(dir, "a.wav"); File.WriteAllBytes(wav, bytes);
        string grande = Path.Combine(dir, "grande.mp3");
        using (FileStream g = new FileStream(grande, FileMode.Create)) g.SetLength((long)TF.GetField("TopeEnMemoria", T).GetValue(null) + 1);
        Check(Call("CancionEnMemoria", wav) == null && Call("CancionEnMemoria", grande) == null && Call("CancionEnMemoria", Path.Combine(dir, "no_esta.mp3")) == null,
              "lo que no es MP3, lo que pasa del tope (32 MB) y lo que no se puede leer: null (del disco, como siempre)");
        MethodInfo reserva = TF.GetMethod("ReservaEnMemoria", T);
        Check(reserva.Invoke(null, new object[] { grande, (long)TF.GetField("TopeEnMemoria", T).GetValue(null) + 1 }) == null
              && reserva.Invoke(null, new object[] { wav, 1000L }) == null
              && ((byte[])reserva.Invoke(null, new object[] { ruta, 1000L })).Length == 1000,
              "la lectura por adelantado solo pide sitio para un MP3 que cabe");
    }

    static void SinDisco(string dir, byte[] bytes)
    {
        Console.WriteLine("\n  3) MIENTRAS SUENA, EL DISCO NO SE TOCA\n");
        string ruta = Path.Combine(dir, "se_va.mp3");
        byte[] referencia;
        using (Mp3FileReader r = Lector(new MemoryStream(bytes))) referencia = LeeTodo(r);

        File.WriteAllBytes(ruta, bytes);
        byte[] delArchivo;
        using (Mp3FileReader r = Lector(new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)))
        {
            byte[] b = new byte[referencia.Length / 3]; int n = r.Read(b, 0, b.Length);
            File.WriteAllBytes(ruta, new byte[0]);   // el disco "se va" a mitad de cancion
            MemoryStream o = new MemoryStream(); o.Write(b, 0, n);
            try { byte[] resto = LeeTodo(r); o.Write(resto, 0, resto.Length); } catch { }
            delArchivo = o.ToArray();
        }
        File.WriteAllBytes(ruta, bytes);
        byte[] deMemoria;
        MemoryStream ms = (MemoryStream)Call("CancionEnMemoria", ruta);
        using (Mp3FileReader r = Lector(ms))
        {
            byte[] b = new byte[referencia.Length / 3]; int n = r.Read(b, 0, b.Length);
            File.WriteAllBytes(ruta, new byte[0]);
            File.Delete(ruta);
            MemoryStream o = new MemoryStream(); o.Write(b, 0, n);
            byte[] resto = LeeTodo(r); o.Write(resto, 0, resto.Length);
            deMemoria = o.ToArray();
        }
        Console.WriteLine("     leyendo del archivo, al vaciarse a mitad: " + (delArchivo.Length * 100L / referencia.Length) + " % de la cancion; desde la memoria: "
                          + (deMemoria.Length * 100L / referencia.Length) + " %");
        Check(Iguales(deMemoria, referencia) && delArchivo.Length < referencia.Length,
              "con el archivo vaciado y borrado a mitad de cancion, desde la memoria sigue ENTERA e identica (leyendo del archivo, se cortaba)");

        File.WriteAllBytes(ruta, bytes);
        Contando c = new Contando(new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096));
        int alAbrir, alSonar;
        using (Mp3FileReader r = Lector(c)) { alAbrir = c.Lecturas + c.Saltos; LeeTodo(r); alSonar = c.Lecturas + c.Saltos - alAbrir; }
        int trozos = (bytes.Length + 256 * 1024 - 1) / (256 * 1024);
        Console.WriteLine("     MP3 de " + (bytes.Length / 1024) + " KB leido por NAudio del archivo: " + alAbrir + " lecturas y saltos al abrir y " + alSonar
                          + " mientras suena; desde la memoria: " + trozos + " lecturas de 256 KB al abrir y 0 mientras suena");
        Check(alSonar > 0, "(del archivo, NAudio sigue pidiendo al disco mientras suena; desde la memoria, nada)");
    }

    static void Enganche()
    {
        Console.WriteLine("\n  4) EL ENGANCHE\n");
        Type fas = TF.GetNestedType("FileAudioSource", T);
        ConstructorInfo ctor = fas == null ? null : fas.GetConstructor(T, null, new Type[] { typeof(string) }, null);
        List<string> ll = ctor == null ? new List<string>() : Codigo.Llamadas(ctor);
        int abre = 0; foreach (string s in ll) if (s.EndsWith(".AbreMp3")) abre++;
        Check(Codigo.Llama(ll, "CancionEnMemoria") && abre >= 2 && ll.IndexOf("Form1.CancionEnMemoria") < ll.IndexOf("Form1.AbreMp3"),
              "la fuente de audio de un MP3 lo pide a la memoria primero y, si no, lo abre del archivo como siempre");
        int lee, escribe;
        Codigo.UsosDeCampo(fas.GetMethod("Dispose", T), "enMemoria", out lee, out escribe);
        Check(escribe > 0, "al cerrar la fuente, suelta la cancion (aunque alguien se quede con el objeto)");
        List<string> cal = Codigo.ConAyudantes(TF, "PrecalientaSiguiente");
        Check(Codigo.Llama(cal, "ReservaEnMemoria") && Codigo.Llama(cal, "DejaEnMemoria") && Codigo.Llama(cal, "Sleep") && Codigo.Llama(cal, "EsperaPortadas"),
              "la lectura por adelantado guarda los bytes sin dejar de ceder el disco ni de hacer sus pausas");
        Check(Codigo.Llama(TF.GetMethod("SueltaLookahead", T), "SueltaEnMemoria"), "soltar lo que se miraba por delante suelta tambien los bytes guardados");
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_enmemoria_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        try
        {
            byte[] bytes = Mp3(1200, 5);   // ~31 s, 500 KB
            string ruta = Path.Combine(dir, "cancion.mp3");
            File.WriteAllBytes(ruta, bytes);
            Igual(ruta);
            Cargador(dir, ruta, bytes);
            SinDisco(dir, bytes);
            Enganche();
        }
        catch (Exception e) { Check(false, "sin excepciones: " + (e.InnerException ?? e)); }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
