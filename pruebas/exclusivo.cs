using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using NAudio.Wave;

// WASAPI EXCLUSIVO (solo HD): Rocola/SalidaExclusiva.cs.
//
//   1) EL FORMATO QUE VE WINDOWS: los 40 bytes de WAVEFORMATEXTENSIBLE, campo a campo, y los mismos que los de NAudio
//      cuando NAudio sabe hacerlos.
//   2) LA NEGOCIACION: primero la frecuencia del aparato (sin cambios de frecuencia entre canciones), luego la de la
//      cancion y las habituales; 24 bits, 32 y coma flotante antes que 16, y 16 solo si nada mas vale en ninguna.
//   3) LA CONVERSION: coma flotante bit a bit; 24 bits (en 4 y en 3 bytes) y 32 enteros redondeados, sin sesgo y
//      recortando a tope; 16 bits con UN dither TPDF.
//   4) EL VOLUMEN DE WINDOWS: con volumen propio del aparato no se toca; sin el, lo aplica la rocola, desde la primera
//      muestra y en rampa; a 1 exacto, bit a bit.
//   5) ABRIR, con un aparato y una salida DE MENTIRA: el formato exacto, mono a dos canales, remuestreo con el
//      limitador detras, el formato que NAudio cambiaria no se usa, un formato rechazado pasa al siguiente, otro
//      programa usando el aparato, Windows que no deja, un aparato sin formatos; y lo que queda apuntado.
//   6) SI FALLA AL ARRANCAR, antes de sonar: se reabre en compartido; ya sonando, o si es el aparato, no.
//   7) EL CABLEADO, leyendo el programa compilado.
//
// Lo que NO se prueba aqui: un Windows y un DAC de verdad. Que el aparato admita el formato, que suene, que el
// volumen de Windows actue en el aparato y que no haya cortes solo se sabe en el equipo del bar.

class JugadorFalso : IWavePlayer, IWavePosition
{
    public static List<JugadorFalso> Hechos = new List<JugadorFalso>();
    public Func<WaveFormat, Exception> AlIniciar;
    public bool CambiaFormato;   // como NAudio cuando no le vale el formato y mete su remuestreador
    public IWaveProvider Proveedor; public WaveFormat Pedido; public int Tocada; public bool Suelta; public int Suscritos; public bool VolumenTocado;
    public PlaybackState Estado = PlaybackState.Stopped;
    public JugadorFalso() { Hechos.Add(this); }
    public void Init(IWaveProvider p)
    {
        Exception e = AlIniciar != null ? AlIniciar(p.WaveFormat) : null;
        if (e != null) throw e;
        Proveedor = p; Pedido = p.WaveFormat;
    }
    public void Play() { Tocada++; Estado = PlaybackState.Playing; }
    public void Stop() { Estado = PlaybackState.Stopped; }
    public void Pause() { Estado = PlaybackState.Paused; }
    public float Volume { get { return 1f; } set { VolumenTocado = true; } }
    public PlaybackState PlaybackState { get { return Estado; } }
    public event EventHandler<StoppedEventArgs> PlaybackStopped { add { Suscritos++; } remove { Suscritos--; } }
    public void Dispose() { Suelta = true; }
    public long GetPosition() { return 0; }
    public WaveFormat OutputWaveFormat { get { return CambiaFormato ? new WaveFormat(48000, 16, 2) : Pedido; } }
}

// El aparato de mentira: que admite, como falla y su volumen.
class AparatoFalso
{
    public string Id = "aparato-1"; public int Hz = 48000;
    public Func<WaveFormat, bool> Admite = delegate { return true; };
    public Func<WaveFormat, Exception> AlIniciar; public bool CambiaFormato;
    public Exception AlPreguntar;
    public int Propio = 3; public float Db = 0f; public bool Silencio;
    public int Preguntas, Abiertos, Soltados;
    public object Real(Type TAp)
    {
        object a = Activator.CreateInstance(TAp, true);
        BindingFlags I = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        TAp.GetField("Id", I).SetValue(a, Id);
        TAp.GetField("Hz", I).SetValue(a, (Func<int>)delegate { return Hz; });
        TAp.GetField("Admite", I).SetValue(a, (Func<WaveFormat, bool>)delegate(WaveFormat f)
        {
            Preguntas++;
            if (AlPreguntar != null) throw AlPreguntar;
            return Admite(f);
        });
        TAp.GetField("Abre", I).SetValue(a, (Func<IWavePlayer>)delegate
        {
            Abiertos++;
            JugadorFalso j = new JugadorFalso(); j.AlIniciar = AlIniciar; j.CambiaFormato = CambiaFormato;
            return j;
        });
        TAp.GetField("Propio", I).SetValue(a, (Func<int>)delegate { return Propio; });
        TAp.GetField("Db", I).SetValue(a, (Func<float>)delegate { return Db; });
        TAp.GetField("Silenciado", I).SetValue(a, (Func<bool>)delegate { return Silencio; });
        TAp.GetField("Suelta", I).SetValue(a, (Action)delegate { Soltados++; });
        return a;
    }
}

class Fuente : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf; private int pos;
    public Fuente(float[] datos, int fs, int ch) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Leidas { get { return pos; } }
    public int Read(float[] b, int o, int c) { int n = Math.Min(c, d.Length - pos); if (n <= 0) return 0; for (int i = 0; i < n; i++) b[o + i] = d[pos + i]; pos += n; return n; }
}

// Un remuestreador de mentira: solo cambia la frecuencia que dice tener (lo que importa aqui es DONDE va).
class RemuestreoFalso : ISampleProvider
{
    private readonly ISampleProvider s; private readonly WaveFormat wf;
    public RemuestreoFalso(ISampleProvider src, int hz) { s = src; wf = WaveFormat.CreateIeeeFloatWaveFormat(hz, src.WaveFormat.Channels); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c) { return s.Read(b, o, c); }
}

class Exclusivo
{
    const BindingFlags T = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags I = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Assembly asm;
    static Type TF, TFE, TEx, TMu, TSx, TAp, TPlay, TTrack;
    static int fallos, oks;
    static string tmp;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static object Get(object o, string campo) { return o.GetType().GetField(campo, T).GetValue(o); }
    static void Set(object o, string campo, object v) { o.GetType().GetField(campo, T).SetValue(o, v); }
    static object Prop(object o, string p) { return o.GetType().GetProperty(p, T).GetValue(o, null); }
    static object Inv(object o, string m, params object[] a)
    {
        foreach (MethodInfo mi in o.GetType().GetMethods(T))
            if (mi.Name == m && mi.GetParameters().Length == a.Length)
            {
                try { return mi.Invoke(o, a); }
                catch (TargetInvocationException tie) { throw tie.InnerException; }
            }
        throw new MissingMethodException(o.GetType().Name, m);
    }
    static object EstEx(string m, params object[] a)
    {
        try { return TEx.GetMethod(m, T).Invoke(null, a); }
        catch (TargetInvocationException tie) { throw tie.InnerException; }
    }
    static object Mu(string n) { return Enum.Parse(TMu, n); }
    static WaveFormat Formato(int hz, int ch, string tipo) { return (WaveFormat)EstEx("Formato", hz, ch, Mu(tipo)); }
    static string Nombre(WaveFormat f)
    {
        if (f == null) return "(ninguno)";
        return Prop(f, "Descripcion").ToString();
    }
    static Exception Com(uint h) { return new COMException("WASAPI", unchecked((int)h)); }
    static object Nuevo(Type t, params object[] a) { return Activator.CreateInstance(t, I, null, a, null); }
    static MethodBase Met(string n) { return TF.GetMethod(n, T); }
    static byte[] Bytes(object estructura)
    {
        int n = Marshal.SizeOf(estructura);
        IntPtr p = Marshal.AllocHGlobal(n);
        try { Marshal.StructureToPtr(estructura, p, false); byte[] b = new byte[n]; Marshal.Copy(p, b, 0, n); return b; }
        finally { Marshal.FreeHGlobal(p); }
    }
    static bool Iguales(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
    static float[] Musica(int fs, int ch, double seg, int semilla, double amp)
    {
        Random r = new Random(semilla); long n = (long)(seg * fs); float[] x = new float[n * ch];
        for (long i = 0; i < n; i++)
            for (int c = 0; c < ch; c++)
                x[i * ch + c] = (float)(amp * (0.6 * Math.Sin(2 * Math.PI * (220 + 110 * c) * i / fs) + 0.4 * (r.NextDouble() * 2 - 1)));
        return x;
    }
    static int Lee(IWaveProvider p, byte[] b) { int n = 0, r; while (n < b.Length && (r = p.Read(b, n, b.Length - n)) > 0) n += r; return n; }
    static string Apuntes() { string f = Path.Combine(tmp, "rocola_error.txt"); return File.Exists(f) ? File.ReadAllText(f) : ""; }
    static int Veces(string texto, string que) { int n = 0, i = 0; while ((i = texto.IndexOf(que, i, StringComparison.Ordinal)) >= 0) { n++; i += que.Length; } return n; }

    // ================================================================================================
    //  1) EL FORMATO QUE VE WINDOWS
    // ================================================================================================
    static void ElFormato()
    {
        Console.WriteLine("\n  1) EL FORMATO QUE VE WINDOWS (WAVEFORMATEXTENSIBLE)\n");
        WaveFormat f = Formato(48000, 2, "Pcm24en32");
        byte[] b = Bytes(f);
        byte[] pcm = new Guid("00000001-0000-0010-8000-00aa00389b71").ToByteArray();
        byte[] flo = new Guid("00000003-0000-0010-8000-00aa00389b71").ToByteArray();
        bool campos = b.Length == 40 && BitConverter.ToUInt16(b, 0) == 0xFFFE && BitConverter.ToInt16(b, 2) == 2
                      && BitConverter.ToInt32(b, 4) == 48000 && BitConverter.ToInt32(b, 8) == 48000 * 8 && BitConverter.ToInt16(b, 12) == 8
                      && BitConverter.ToInt16(b, 14) == 32 && BitConverter.ToInt16(b, 16) == 22 && BitConverter.ToInt16(b, 18) == 24
                      && BitConverter.ToInt32(b, 20) == 3;
        byte[] sub = new byte[16]; if (b.Length == 40) Array.Copy(b, 24, sub, 0, 16);
        Check(campos && Iguales(sub, pcm),
              "24 bits en 4 bytes a 48 kHz: 40 bytes, extensible, 2 canales, 8 bytes por cuadro, 32 de contenedor, 24 validos, izquierda+derecha, PCM");
        // Donde NAudio sabe hacerlos (24 bits en 3 bytes, coma flotante, 16), tienen que salir los mismos bytes que los suyos.
        bool mismos = Iguales(Bytes(Formato(44100, 2, "Pcm24")), Bytes(new WaveFormatExtensible(44100, 24, 2)))
                      && Iguales(Bytes(Formato(96000, 2, "Flotante32")), Bytes(new WaveFormatExtensible(96000, 32, 2)))
                      && Iguales(Bytes(Formato(48000, 2, "Pcm16")), Bytes(new WaveFormatExtensible(48000, 16, 2)));
        Check(mismos, "24 bits en 3 bytes, coma flotante y 16 bits: byte a byte los mismos que arma NAudio");
        byte[] e32 = Bytes(Formato(48000, 2, "Pcm32"));
        byte[] s32 = new byte[16]; Array.Copy(e32, 24, s32, 0, 16);
        byte[] ef = Bytes(Formato(48000, 2, "Flotante32"));
        byte[] sf = new byte[16]; Array.Copy(ef, 24, sf, 0, 16);
        Check(BitConverter.ToInt16(e32, 18) == 32 && Iguales(s32, pcm) && Iguales(sf, flo),
              "32 bits ENTEROS (32 validos, PCM), que NAudio no sabe pedir: con NAudio 32 bits es siempre coma flotante");
        WaveFormat seis = Formato(48000, 6, "Pcm24en32");
        Check(BitConverter.ToInt32(Bytes(seis), 20) == 0x3F && seis.BlockAlign == 24, "seis canales: los seis altavoces de siempre (0x3F)");
    }

    // ================================================================================================
    //  2) LA NEGOCIACION
    // ================================================================================================
    static WaveFormat Elige(int hzAp, int hzCan, int ch, Func<WaveFormat, bool> admite) { return (WaveFormat)EstEx("Elige", hzAp, hzCan, ch, admite); }
    static bool Es(WaveFormat f, int hz, int ch, int cont, int val, bool flot)
    {
        return f != null && f.SampleRate == hz && f.Channels == ch && f.BitsPerSample == cont && (int)Prop(f, "BitsValidos") == val && (bool)Prop(f, "EsFlotante") == flot;
    }
    static void LaNegociacion()
    {
        Console.WriteLine("\n  2) QUE FORMATO SE LE PIDE AL APARATO\n");
        System.Collections.IList c = (System.Collections.IList)EstEx("Candidatos", 48000, 44100, 2);
        List<string> orden = new List<string>();
        foreach (object o in c) orden.Add(((WaveFormat)o).SampleRate + "/" + ((WaveFormat)o).BitsPerSample + "/" + Prop(o, "BitsValidos") + ((bool)Prop(o, "EsFlotante") ? "f" : ""));
        bool primero = orden.Count == 30 && orden[0] == "48000/32/24" && orden[1] == "48000/24/24" && orden[2] == "48000/32/32" && orden[3] == "48000/32/32f"
                       && orden[4] == "44100/32/24" && orden[24] == "48000/16/16" && orden[25] == "44100/16/16";
        bool dieciseis = true;
        for (int i = 0; i < 24; i++) if (orden[i].Contains("/16/")) dieciseis = false;
        Check(primero && dieciseis,
              "el orden: la frecuencia del aparato (48 kHz) y luego la de la cancion (44,1) y las habituales; en cada una 24 bits (en 4 y en 3 bytes), 32 enteros y coma flotante; 16 bits, solo al final (" + orden.Count + " formatos)");
        Check(Es(Elige(48000, 44100, 2, delegate { return true; }), 48000, 2, 32, 24, false),
              "si lo admite todo: 24 bits a la frecuencia del aparato (la cancion de 44,1 se remuestrea como en compartido y el aparato no cambia de frecuencia)");
        Check(Es(Elige(48000, 44100, 2, delegate(WaveFormat f) { return f.BitsPerSample == 16; }), 48000, 2, 16, 16, false),
              "si solo admite 16 bits: 16 bits a su frecuencia (con dither)");
        Check(Es(Elige(48000, 44100, 2, delegate(WaveFormat f) { return (f.SampleRate == 44100 && f.BitsPerSample == 24) || f.BitsPerSample == 16; }), 44100, 2, 24, 24, false),
              "24 bits a otra frecuencia antes que 16 bits a la del aparato");
        Check(Es(Elige(0, 44100, 2, delegate { return true; }), 44100, 2, 32, 24, false), "si no se sabe a que frecuencia trabaja el aparato, la de la cancion");
        WaveFormat mono = Elige(48000, 48000, 1, delegate { return true; });
        bool todosDos = true;
        foreach (object o in (System.Collections.IList)EstEx("Candidatos", 48000, 48000, 1)) if (((WaveFormat)o).Channels != 2) todosDos = false;
        Check(mono != null && mono.Channels == 2 && todosDos, "una cancion mono se pide siempre en dos canales (sale duplicada por los dos lados)");
        Check(Elige(48000, 44100, 2, delegate { return false; }) == null, "si no admite ninguno: ninguno (y se sigue en compartido)");
    }

    // ================================================================================================
    //  3) LA CONVERSION
    // ================================================================================================
    static IWaveProvider Salida(float[] x, int fs, int ch, WaveFormat f) { return (IWaveProvider)Nuevo(TSx, new Fuente(x, fs, ch), f); }
    static void LaConversion()
    {
        Console.WriteLine("\n  3) LA MUSICA EN EL FORMATO DEL APARATO\n");
        int fs = 48000, ch = 2;
        float[] x = Musica(fs, ch, 0.5, 5, 0.9);
        x[10] = 1.2f; x[11] = -1.3f; x[12] = 1f; x[13] = -1f;   // por encima de tope: se recorta, no da la vuelta

        // Coma flotante: tal cual.
        byte[] b = new byte[x.Length * 4];
        int n = Lee(Salida(x, fs, ch, Formato(fs, ch, "Flotante32")), b);
        bool exacto = n == b.Length;
        for (int i = 0; exacto && i < x.Length; i++) if (BitConverter.ToSingle(b, i * 4) != x[i]) exacto = false;
        Check(exacto, "coma flotante: muestra a muestra igual que la cadena");

        // 24 bits en 4 bytes, en 3, y 32 enteros: redondeados, sin sesgo y recortados a tope.
        string[] tipos = { "Pcm24en32", "Pcm24", "Pcm32" };
        foreach (string tipo in tipos)
        {
            WaveFormat f = Formato(fs, ch, tipo);
            int bpm = f.BitsPerSample / 8;
            b = new byte[x.Length * bpm];
            n = Lee(Salida(x, fs, ch, f), b);
            double escala = tipo == "Pcm32" ? 2147483647.0 : 8388607.0;
            double peor = 0, suma = 0; bool bajoCero = true, tope = true; int m = 0;
            for (int i = 0; i < n / bpm; i++)
            {
                long q;
                if (bpm == 3) { q = b[i * 3] | (b[i * 3 + 1] << 8) | ((sbyte)b[i * 3 + 2] << 16); }
                else
                {
                    int v = BitConverter.ToInt32(b, i * 4);
                    if (tipo == "Pcm24en32") { if ((v & 0xFF) != 0) bajoCero = false; q = v >> 8; }
                    else q = v;
                }
                double ideal = Math.Max(-1.0, Math.Min(1.0, x[i])) * escala;
                if (x[i] > 1f) { if (q != (long)escala) tope = false; continue; }
                if (x[i] < -1f) { if (q < -(long)escala - 1 || q > -(long)escala) tope = false; continue; }
                double d = q - ideal;
                peor = Math.Max(peor, Math.Abs(d)); suma += d; m++;
            }
            Check(n == x.Length * bpm && peor <= 0.5 + 1e-6 && Math.Abs(suma / Math.Max(m, 1)) < 0.05 && bajoCero && tope,
                  Nombre(f) + ": redondeo a medio escalon como mucho (" + F(peor, "0.000") + "), sin sesgo, sin dither" + (tipo == "Pcm24en32" ? ", el byte bajo a cero" : "") + " y lo que pasa de tope se queda en el tope");
        }

        // 16 bits: un dither TPDF, una vez.
        WaveFormat f16 = Formato(fs, ch, "Pcm16");
        b = new byte[x.Length * 2];
        n = Lee(Salida(x, fs, ch, f16), b);
        double p16 = 0, s16 = 0, q16 = 0; int n16 = 0;
        for (int i = 14; i < n / 2; i++)
        {
            double d = BitConverter.ToInt16(b, i * 2) - x[i] * 32767.0;
            p16 = Math.Max(p16, Math.Abs(d)); s16 += d; q16 += d * d; n16++;
        }
        double ruido = Math.Sqrt(q16 / Math.Max(n16, 1));
        Check(n == x.Length * 2 && p16 <= 1.5 && Math.Abs(s16 / n16) < 0.02 && ruido > 0.45 && ruido < 0.55,
              "16 bits: dither TPDF una sola vez (" + F(ruido, "0.000") + " escalones eficaces; dos dithers darian 0,71; ninguno, 0,29)");

        // Lecturas partidas: siempre cuadros enteros, y al acabarse la cancion lo que queda y luego nada.
        IWaveProvider p = Salida(x, fs, ch, Formato(fs, ch, "Pcm24"));
        byte[] trozo = new byte[1001]; int total = 0, r, raros = 0;
        while ((r = p.Read(trozo, 0, trozo.Length)) > 0) { total += r; if (r % 6 != 0) raros++; }
        Check(raros == 0 && total == x.Length * 3 && p.Read(trozo, 0, 600) == 0,
              "pedida a trozos que no son cuadros enteros: devuelve cuadros enteros, toda la cancion y luego nada");
    }

    // ================================================================================================
    //  4) EL VOLUMEN DE WINDOWS
    // ================================================================================================
    static float G(bool vol, bool sil, float db, bool mudo) { return (float)EstEx("GananciaDeWindows", vol, sil, db, mudo); }
    static void ElVolumen()
    {
        Console.WriteLine("\n  4) EL VOLUMEN DE WINDOWS\n");
        Check(G(true, true, -20f, false) == 1f && G(true, true, -20f, true) == 1f,
              "con volumen y silencio propios del aparato, la rocola no toca nada: Windows los mueve en el aparato");
        Check(Math.Abs(G(false, false, -20f, false) - 0.1f) < 1e-6 && Math.Abs(G(false, true, -6f, false) - 0.501187f) < 1e-5 && G(false, false, 0f, false) == 1f,
              "sin volumen propio, la rocola aplica el de Windows: -20 dB es 0,1 y -6 dB es 0,5");
        Check(G(false, false, -20f, true) == 0f && G(true, false, -3f, true) == 0f && G(false, false, -200f, false) == 0f && G(false, false, float.NaN, false) == 1f,
              "el silencio de Windows, si el aparato no lo tiene propio, es silencio; un valor raro no sube nada por encima de 1");

        int fs = 48000, ch = 2;
        float[] x = new float[fs * ch / 5];   // 200 ms a 0,5
        for (int i = 0; i < x.Length; i++) x[i] = 0.5f;
        object sx = Nuevo(TSx, new Fuente(x, fs, ch), Formato(fs, ch, "Flotante32"));
        Inv(sx, "GananciaInicial", 0.1f);
        byte[] b = new byte[x.Length * 4];
        int n = ((IWaveProvider)sx).Read(b, 0, 960 * 8);   // los primeros 20 ms
        bool desdeElPrincipio = n == 960 * 8 && Math.Abs(BitConverter.ToSingle(b, 0) - 0.05f) < 1e-7 && Math.Abs(BitConverter.ToSingle(b, n - 4) - 0.05f) < 1e-7;
        Check(desdeElPrincipio, "la primera muestra ya sale con el volumen de Windows: no arranca a tope para bajar despues");
        TSx.GetProperty("Ganancia", T).SetValue(sx, 1f, null);
        int m = ((IWaveProvider)sx).Read(b, 0, b.Length - n);
        double salto = 0; int llego = -1;
        float antes = 0.05f;
        for (int i = 0; i < m / 8; i++)
        {
            float v = BitConverter.ToSingle(b, i * 8);
            salto = Math.Max(salto, Math.Abs(v - antes)); antes = v;
            if (llego < 0 && v == 0.5f) llego = i;
        }
        Check(llego > 0 && llego <= fs / 20 + 1 && salto <= 0.5 / (0.05 * fs) + 1e-6,
              "al subir el volumen llega en rampa: en " + F(llego * 1000.0 / fs, "0.0") + " ms, sin saltos (el mayor, " + F(salto * 1e6, "0.0") + " millonesimas)");
        bool exacta = true;
        for (int i = llego; i < m / 8; i++) if (BitConverter.ToSingle(b, i * 8) != 0.5f || BitConverter.ToSingle(b, i * 8 + 4) != 0.5f) exacta = false;
        Check(exacta, "y a volumen 1 sale bit a bit como la cadena");
    }

    // ================================================================================================
    //  5) ABRIR CON UN APARATO DE MENTIRA
    // ================================================================================================
    static object Form()
    {
        object f = Codigo.FormSinEncender(TF);
        foreach (FieldInfo fi in TF.GetFields(I))
            if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null)
                fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
        return f;
    }
    static int remuestreos; static int remuestreoHz;
    static void Pon(object f, AparatoFalso ap)
    {
        Set(f, "aparatoExclusivoPrueba", (Func<object>)delegate { return ap.Real(TAp); });
        Set(f, "remuestreoPrueba", (Func<ISampleProvider, int, ISampleProvider>)delegate(ISampleProvider s, int hz) { remuestreos++; remuestreoHz = hz; return new RemuestreoFalso(s, hz); });
    }
    static bool Abre(object f, ISampleProvider s) { return (bool)Inv(f, "AbreExclusiva", s); }
    static void Cierra(object f) { Set(f, "output", null); Set(f, "salidaExacta", null); Set(f, "aparatoExclusivo", null); }

    static void Abrir()
    {
        Console.WriteLine("\n  5) ABRIR LA CANCION EN EXCLUSIVO (aparato y salida de mentira)\n");
        int fs = 48000, ch = 2;
        float[] x = Musica(fs, ch, 0.3, 9, 0.8);

        // a) Todo bien.
        object f = Form(); AparatoFalso ap = new AparatoFalso(); Pon(f, ap);
        JugadorFalso.Hechos.Clear();
        bool ok = Abre(f, new Fuente(x, fs, ch));
        JugadorFalso j = JugadorFalso.Hechos.Count == 1 ? JugadorFalso.Hechos[0] : null;
        bool puesto = ok && j != null && object.ReferenceEquals(Get(f, "output"), j) && j.Suscritos == 1 && j.Tocada == 1 && !j.Suelta
                      && Es(j.Pedido, 48000, 2, 32, 24, false) && object.ReferenceEquals(j.Pedido, Prop(Get(f, "salidaExacta"), "Formato"))
                      && Get(f, "aparatoExclusivo") != null && ((string)Get(f, "formatoSalida")).StartsWith("WASAPI exclusivo, 24 bits en 4 bytes, 48000 Hz");
        bool suena = false;
        if (puesto)
        {
            byte[] b = new byte[x.Length * 4]; int n = Lee(j.Proveedor, b);
            suena = n == b.Length;
            for (int i = 0; suena && i < x.Length; i++)
                if ((BitConverter.ToInt32(b, i * 4) >> 8) != (int)Math.Floor(x[i] * 8388607.0 + 0.5)) suena = false;
        }
        Check(puesto && suena, "abre en el formato exacto que eligio (24 bits a 48 kHz), con su aviso de parada, y lo que le llega es la cancion misma en 24 bits");
        Check(!j.VolumenTocado, "no le toca el volumen a la salida");
        string ap1 = Apuntes();
        Check(ap1.Contains("WASAPI exclusivo: en marcha: 24 bits en 4 bytes, 48000 Hz, 2 canales"), "queda apuntado que sale en exclusivo y en que formato");
        Cierra(f);

        // b) Una cancion mono sale igual por los dos lados.
        float[] mono = Musica(fs, 1, 0.1, 4, 0.7);
        JugadorFalso.Hechos.Clear();
        ok = Abre(f, new Fuente(mono, fs, 1));
        j = JugadorFalso.Hechos.Count == 1 ? JugadorFalso.Hechos[0] : null;
        bool dos = ok && j != null && j.Pedido.Channels == 2;
        if (dos)
        {
            byte[] b = new byte[mono.Length * 8]; int n = Lee(j.Proveedor, b);
            dos = n == b.Length;
            for (int i = 0; dos && i < mono.Length; i++)
            {
                int l = BitConverter.ToInt32(b, i * 8), r = BitConverter.ToInt32(b, i * 8 + 4);
                if (l != r || (l >> 8) != (int)Math.Floor(mono[i] * 8388607.0 + 0.5)) dos = false;
            }
        }
        Check(dos, "una cancion mono sale duplicada, exacta, por los dos lados");
        Cierra(f);

        // c) Cancion de 44,1 en un aparato a 48: remuestreo y, detras, el limitador de pico real.
        remuestreos = 0; remuestreoHz = 0; JugadorFalso.Hechos.Clear();
        ok = Abre(f, new Fuente(Musica(44100, 2, 0.1, 3, 0.5), 44100, 2));
        object sx = Get(f, "salidaExacta");
        Check(ok && remuestreos == 1 && remuestreoHz == 48000 && sx != null && Get(sx, "src").GetType().Name == "LimitadorPicoReal"
              && JugadorFalso.Hechos[0].Pedido.SampleRate == 48000,
              "una cancion de 44,1 kHz en un aparato a 48: se remuestrea a 48 y el ultimo paso es el limitador de pico real, como en compartido");
        Cierra(f);

        // d) El aparato dice que si y al iniciar rechaza el formato: se prueba el siguiente, sin dejar nada colgando.
        f = Form(); ap = new AparatoFalso(); Pon(f, ap);
        ap.AlIniciar = delegate(WaveFormat w) { return w.BitsPerSample == 32 ? Com(0x88890008) : null; };
        JugadorFalso.Hechos.Clear();
        ok = Abre(f, new Fuente(x, fs, ch));
        bool siguiente = ok && JugadorFalso.Hechos.Count == 2 && JugadorFalso.Hechos[0].Suelta && JugadorFalso.Hechos[0].Suscritos == 0
                         && object.ReferenceEquals(Get(f, "output"), JugadorFalso.Hechos[1]) && Es(JugadorFalso.Hechos[1].Pedido, 48000, 2, 24, 24, false);
        Check(siguiente, "si al iniciar rechaza el formato, esa salida se suelta sin avisos colgando y se prueba el siguiente (24 bits en 3 bytes)");
        Cierra(f);

        // e) NAudio cambiaria el formato (meteria su remuestreador detras del limitador): esa no se usa.
        f = Form(); ap = new AparatoFalso(); Pon(f, ap); ap.CambiaFormato = true;
        JugadorFalso.Hechos.Clear();
        ok = Abre(f, new Fuente(x, fs, ch));
        bool ninguna = !ok && JugadorFalso.Hechos.Count > 0 && Get(f, "output") == null;
        foreach (JugadorFalso k in JugadorFalso.Hechos) if (!k.Suelta || k.Suscritos != 0 || k.Tocada != 0) ninguna = false;
        Check(ninguna, "si NAudio cambiara el formato por su cuenta, esa salida no suena: se suelta y, sin otra, sigue en compartido");
        Cierra(f);

        // f) Otro programa esta usando el aparato: esta vez compartido; con la siguiente cancion se vuelve a probar.
        f = Form(); ap = new AparatoFalso(); Pon(f, ap);
        ap.AlIniciar = delegate { return Com(0x8889000A); };
        JugadorFalso.Hechos.Clear();
        bool r1 = Abre(f, new Fuente(x, fs, ch));
        int abiertos1 = ap.Abiertos;
        ap.AlIniciar = null;
        bool r2 = Abre(f, new Fuente(x, fs, ch));
        Check(!r1 && abiertos1 == 1 && JugadorFalso.Hechos[0].Suelta && r2 && ap.Abiertos == 2 && Apuntes().Contains("otro programa estaba usando el aparato"),
              "otro programa usando el aparato: esa cancion va en compartido (sin probar mas formatos), queda dicho asi, y la siguiente vuelve a intentarlo, y entra");
        Cierra(f);

        // g) Windows no permite el exclusivo en ese aparato: compartido, y en esta sesion ya no se pregunta.
        f = Form(); ap = new AparatoFalso(); Pon(f, ap);
        ap.AlIniciar = delegate { return Com(0x8889000E); };
        r1 = Abre(f, new Fuente(x, fs, ch));
        int preguntas = ap.Preguntas, abiertos = ap.Abiertos;
        r2 = Abre(f, new Fuente(x, fs, ch));
        AparatoFalso otro = new AparatoFalso(); otro.Id = "aparato-2"; Pon(f, otro);
        bool r3 = Abre(f, new Fuente(x, fs, ch));
        Check(!r1 && !r2 && ap.Preguntas == preguntas && ap.Abiertos == abiertos && ap.Soltados >= 2 && r3,
              "Windows no deja el modo exclusivo en ese aparato: compartido, y en esta sesion no se le vuelve a preguntar (a otro aparato, si)");
        Cierra(f);

        // h) El aparato no admite ningun formato: compartido, una sola nota.
        f = Form(); ap = new AparatoFalso(); ap.Id = "aparato-sin-formatos"; Pon(f, ap);
        ap.Admite = delegate { return false; };
        r1 = Abre(f, new Fuente(x, fs, ch)); r2 = Abre(f, new Fuente(x, fs, ch));
        string apuntes = Apuntes();
        Check(!r1 && !r2 && ap.Abiertos == 0 && Veces(apuntes, "no admite en exclusivo ningun formato") == 1,
              "si no admite ningun formato: compartido, apuntado una sola vez y sin volver a preguntar");

        // i) El aparato falla al preguntarle (desenchufado): compartido, que lo atiende la espera de siempre; no se da por prohibido.
        f = Form(); ap = new AparatoFalso(); ap.Id = "aparato-desenchufado"; Pon(f, ap);
        ap.AlPreguntar = Com(0x88890004);
        r1 = Abre(f, new Fuente(x, fs, ch));
        ap.AlPreguntar = null;
        r2 = Abre(f, new Fuente(x, fs, ch));
        Check(!r1 && r2, "si el aparato falla al preguntarle (desenchufado): esta vez compartido, y en cuanto vuelve, exclusivo");
        Cierra(f);

        // j) Sin volumen propio: la rocola aplica el de Windows desde la primera muestra y lo sigue.
        f = Form(); ap = new AparatoFalso(); Pon(f, ap);
        ap.Propio = 0; ap.Db = -20f;
        float[] medio = new float[fs * ch / 10];
        for (int i = 0; i < medio.Length; i++) medio[i] = 0.5f;
        ap.Admite = delegate(WaveFormat w) { return (bool)Prop(w, "EsFlotante"); };
        JugadorFalso.Hechos.Clear();
        ok = Abre(f, new Fuente(medio, fs, ch));
        j = JugadorFalso.Hechos.Count == 1 ? JugadorFalso.Hechos[0] : null;
        byte[] bb = new byte[64];
        bool primera = ok && j != null && j.Proveedor.Read(bb, 0, 64) == 64 && Math.Abs(BitConverter.ToSingle(bb, 0) - 0.05f) < 1e-7;
        ap.Db = -6f; Inv(f, "SigueVolumenWindows");
        float g1 = (float)Prop(Get(f, "salidaExacta"), "Ganancia");
        ap.Silencio = true; Inv(f, "SigueVolumenWindows");
        float g2 = (float)Prop(Get(f, "salidaExacta"), "Ganancia");
        ap.Silencio = false; ap.Propio = 3; ap.Db = -40f; Inv(f, "SigueVolumenWindows");
        float g3 = (float)Prop(Get(f, "salidaExacta"), "Ganancia");
        Check(primera && Math.Abs(g1 - 0.501187f) < 1e-5 && g2 == 0f && g3 == 1f,
              "aparato sin volumen propio a -20 dB: la primera muestra ya sale a 0,1; si se sube a -6 dB la rocola sigue (0,50), el silencio calla y con volumen propio no se toca");
        Cierra(f);

        // k) Una cancion de seis canales en un aparato que en exclusivo solo admite dos: esa va en compartido, pero el
        //    aparato NO se da por perdido: la siguiente, estereo, entra en exclusivo.
        f = Form(); ap = new AparatoFalso(); ap.Id = "aparato-estereo"; Pon(f, ap);
        ap.Admite = delegate(WaveFormat w) { return w.Channels == 2; };
        r1 = Abre(f, new Fuente(Musica(fs, 6, 0.1, 8, 0.3), fs, 6));
        r2 = Abre(f, new Fuente(x, fs, ch));
        Check(!r1 && r2 && Apuntes().Contains("una cancion de 6 canales no cabe en exclusivo"),
              "una cancion de seis canales que el aparato no admite va en compartido, sin quitarle el exclusivo a las demas");
        Cierra(f);

        // l) Si la salida exclusiva se queda muda (el vigia), la cancion se retoma como siempre, pero ya en compartido.
        f = Form(); ap = new AparatoFalso(); ap.Id = "aparato-mudo"; Pon(f, ap);
        r1 = Abre(f, new Fuente(x, fs, ch));
        Inv(f, "MudoEnExclusivo");
        Cierra(f);
        int abiertosAntes = ap.Abiertos;
        r2 = Abre(f, new Fuente(x, fs, ch));
        Check(r1 && !r2 && ap.Abiertos == abiertosAntes && Apuntes().Contains("la salida exclusiva se quedo muda"),
              "si la salida exclusiva se queda muda, ese aparato sigue en compartido el resto de la sesion (hay controladores que se atascan en exclusivo)");
        Cierra(f);
    }

    // ================================================================================================
    //  6) SI FALLA AL ARRANCAR
    // ================================================================================================
    static void AlArrancar()
    {
        Console.WriteLine("\n  6) SI LA SALIDA EXCLUSIVA FALLA AL ARRANCAR\n");
        int fs = 48000, ch = 2;
        object f = Form(); AparatoFalso ap = new AparatoFalso(); Pon(f, ap);
        JugadorFalso.Hechos.Clear();
        Abre(f, new Fuente(Musica(fs, ch, 1.0, 2, 0.5), fs, ch));
        JugadorFalso j = JugadorFalso.Hechos[0];
        object pista = Activator.CreateInstance(TTrack, true); Set(pista, "Path", "a.mp3");
        object it = Activator.CreateInstance(TPlay, true); Set(it, "Song", pista); Set(it, "IsVideo", false);
        Set(f, "nowPlaying", it);
        byte[] b = new byte[4800 * 8];
        j.Proveedor.Read(b, 0, b.Length);   // el relleno antes de arrancar
        Exception raro = Com(0x80004005), aparato = Com(0x88890004);
        bool antes = (bool)Inv(f, "FalloAlArrancarExclusivo", raro);
        bool delAparato = (bool)Inv(f, "FalloAlArrancarExclusivo", aparato);
        bool sinNada = (bool)Inv(f, "FalloAlArrancarExclusivo", (Exception)null);
        bool noEsDeWindows = (bool)Inv(f, "FalloAlArrancarExclusivo", (Exception)new InvalidOperationException("otra cosa"));
        j.Proveedor.Read(b, 0, b.Length);   // ya pidio musica despues de arrancar
        bool despues = (bool)Inv(f, "FalloAlArrancarExclusivo", raro);
        Check(antes && !delAparato && !sinNada && !despues && !noEsDeWindows,
              "un error de Windows en la salida exclusiva antes de sonar se reabre en compartido; uno del aparato (desenchufado) va a la espera de siempre; ya sonando, o si no es de Windows, como cualquier fallo");
        Set(it, "IsVideo", true);
        Set(f, "salidaExacta", Nuevo(TSx, new Fuente(new float[10], fs, ch), Formato(fs, ch, "Pcm16")));
        Check(!(bool)Inv(f, "FalloAlArrancarExclusivo", raro), "un video nunca (los videos no van por aqui)");
    }

    // ================================================================================================
    //  7) EL CABLEADO
    // ================================================================================================
    // ¿Crea un WasapiOut con este modo (0 compartido, 1 exclusivo), sin eventos y 300 ms?
    static bool CreaWasapi(List<MethodBase> ms, int modo)
    {
        foreach (MethodBase m in ms)
        {
            List<Codigo.Paso> p = Codigo.Pasos(m);
            for (int i = 3; i < p.Count; i++)
            {
                if (p[i].Op != 0x73) continue;
                MethodBase c = null;
                try { c = m.Module.ResolveMethod(p[i].Numero); } catch { }
                if (c == null || c.DeclaringType.Name != "WasapiOut" || c.GetParameters().Length != 4) continue;
                if (p[i - 3].Op == (modo == 1 ? 0x17 : 0x16) && p[i - 2].Op == 0x16 && p[i - 1].Op == 0x20 && p[i - 1].Numero == 300) return true;
            }
        }
        return false;
    }
    static void ElCableado()
    {
        Console.WriteLine("\n  7) EL CABLEADO (leyendo el programa compilado)\n");
        List<string> arranca = Codigo.Nombres(Met("StartOpenedTrack"));
        int iCalla = arranca.IndexOf("CallaSiEsMedida"), iExc = arranca.IndexOf("AbreExclusiva"), iComp = arranca.IndexOf("PreparaSalidaWasapi");
        Check(iCalla >= 0 && iExc > iCalla && iComp > iExc, "la cancion prueba primero el exclusivo y despues el compartido, con la cadena ya callada si es la de la medida");
        Check(CreaWasapi(Codigo.MetodosConAyudantes(TF, "AparatoPredeterminado"), 1) && CreaWasapi(new List<MethodBase> { Met("StartOpenedTrack") }, 0),
              "el exclusivo abre WASAPI EXCLUSIVO (sin eventos, 300 ms) y el compartido sigue siendo compartido");
        List<string> abre = Codigo.Llamadas(Met("AbreExclusiva"));
        int iRem = -1, iLim = -1, iSx = -1;
        for (int i = 0; i < abre.Count; i++)
        {
            if (abre[i].EndsWith("NuevoRemuestreo") && iRem < 0) iRem = i;
            if (abre[i] == "LimitadorPicoReal..ctor" && iLim < 0) iLim = i;
            if (abre[i] == "SalidaExacta..ctor" && iSx < 0) iSx = i;
        }
        Check(iRem >= 0 && iLim > iRem && iSx > iLim && !Codigo.Llama(abre, "DitherWaveProvider16..ctor") && Codigo.Llama(abre, "IWavePosition.get_OutputWaveFormat"),
              "en exclusivo: remuestreo, limitador y la conversion al formato, en ese orden; sin otro dither; y se mira que NAudio no cambie el formato");
        List<string> para = Codigo.Nombres(Met("Output_PlaybackStopped"));
        int iFallo = para.IndexOf("FalloAlArrancarExclusivo"), iRe = para.IndexOf("ReintentaCompartido"), iWo = para.IndexOf("ReintentaWaveOut"), iDev = para.IndexOf("RefundCredit");
        Check(iFallo >= 0 && iRe > iFallo && iWo > iRe && iDev > iRe, "si falla al arrancar se reabre en compartido antes de pensar en WaveOut o en devolver creditos");
        List<MethodBase> re = new List<MethodBase> { Met("ReintentaCompartido") };
        Check(Codigo.EscribeCampo(re, "reintentandoCompartido") && Codigo.EscribeCampo(re, "reanudarEn") && Codigo.Llama(Met("ReintentaCompartido"), "PlayTrack")
              && Codigo.Llama(Met("ReintentaCompartido"), "NoExclusivo") && Codigo.LeeCampo(Codigo.MetodosConAyudantes(TF, "PlayTrack"), "reintentandoCompartido"),
              "y se reabre la misma cancion desde donde iba a empezar, sin exclusivo en ese aparato");
        List<string> barra = Codigo.Nombres(Met("ProgressTimer_Tick"));
        int iMudo = barra.IndexOf("MudoEnExclusivo"), iAviso = barra.IndexOf("Output_PlaybackStopped");
        Check(barra.Contains("SigueVolumenWindows") && iMudo >= 0 && iAviso > iMudo && Codigo.Llama(Met("DisposePlaybackNow"), "SueltaExclusivo"),
              "la barra de progreso sigue el volumen de Windows y, si el vigia ve la salida muda, la aparta del exclusivo antes de retomar; parar suelta el exclusivo");
        bool leeConfig = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            List<string> tx = Codigo.Textos(m);
            if (tx.Contains("salida=") && tx.Contains("exclusivo") && tx.Contains("wasapi") && tx.Contains("waveout")) leeConfig = true;
        }
        Check(leeConfig, "la configuracion entiende salida=exclusivo (y siguen salida=wasapi para el compartido de antes y salida=waveout)");
        // El volumen de Windows solo se LEE: nadie lo escribe, ni el general ni el de un canal.
        List<string> escriben = new List<string>();
        foreach (Type t in asm.GetTypes())
        {
            BindingFlags bf = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            List<MethodBase> ms = new List<MethodBase>();
            foreach (MethodInfo m in t.GetMethods(bf)) ms.Add(m);
            foreach (ConstructorInfo c in t.GetConstructors(bf)) ms.Add(c);
            foreach (MethodBase m in ms)
                foreach (string s in Codigo.Llamadas(m))
                    if (s == "AudioEndpointVolume.set_MasterVolumeLevel" || s == "AudioEndpointVolume.set_MasterVolumeLevelScalar" || s == "AudioEndpointVolume.set_Mute"
                        || s == "AudioEndpointVolume.VolumeStepUp" || s == "AudioEndpointVolume.VolumeStepDown"
                        || s == "AudioEndpointVolumeChannel.set_VolumeLevel" || s == "AudioEndpointVolumeChannel.set_VolumeLevelScalar")
                        escriben.Add(t.Name + "." + m.Name);
        }
        Check(escriben.Count == 0, "el volumen de Windows solo se lee: ningun sitio del programa lo cambia" + (escriben.Count > 0 ? ": " + string.Join(", ", escriben.ToArray()) : ""));
    }

    static int Main(string[] args)
    {
        asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TFE = asm.GetType("Rocola.FormatoExacto"); TEx = asm.GetType("Rocola.Exclusivo"); TMu = asm.GetType("Rocola.MuestraExclusiva");
        TSx = asm.GetType("Rocola.SalidaExacta"); TAp = asm.GetType("Rocola.AparatoExclusivo");
        TPlay = TF.GetNestedType("PlayItem", T); TTrack = TF.GetNestedType("Track", T);
        if (TFE == null || TEx == null || TSx == null || TAp == null || Met("AbreExclusiva") == null)
        { Check(false, "no esta la salida exclusiva (SalidaExclusiva.cs; solo en HD)"); return 1; }
        tmp = Path.Combine(Path.GetTempPath(), "rocola_exclusivo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", tmp);
        try
        {
            ElFormato();
            LaNegociacion();
            LaConversion();
            ElVolumen();
            Abrir();
            AlArrancar();
            ElCableado();
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
        Console.WriteLine(fallos == 0 ? ("\n  TODO BIEN (" + oks + " comprobaciones)") : ("\n  FALLOS: " + fallos));
        return fallos;
    }
}
