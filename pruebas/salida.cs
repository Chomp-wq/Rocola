using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using NAudio.Wave;

// LA SALIDA AL DISPOSITIVO (etapa 8 de audio): Rocola/SalidaSonido.cs.
//
//   1) EL FORMATO. En Vista o posterior la salida clasica recibe coma flotante tal cual sale de la cadena; si el
//      sistema la rechaza, 16 bits con dither TPDF (lo de siempre, y lo de XP). El dither va una sola vez.
//   2) EL APARATO O LA CANCION: que fallos son del dispositivo (desenchufado, tomado por otro programa, sin
//      servicio de audio) y cuales de la cancion (formato, archivo).
//   3) LA ESPERA: un fallo del aparato no devuelve credito, no salta la cancion ni la apunta como incompatible:
//      se espera y se retoma la MISMA desde donde iba. A media cancion, a la sexta caida se rinde como antes.
//   4) RETOMAR: el lector salta al sitio, el fundido acaba donde acaba la cancion y de un trozo no se aprende.
//   5) EL VIGIA: 10 s con la salida en marcha sin pedir musica cuentan como fallo del aparato.
//   6) CAMBIO DE APARATO: con otro aparato predeterminado no se encadena la siguiente.
//   7) EL CABLEADO, leyendo el programa compilado.
//   8) LA CANCION DE LA MEDIDA de rendimiento: callada al final de la cadena, sin tocar ningun volumen de Windows.
//
// Todo con una salida DE MENTIRA que acepta o rechaza formatos y falla como un aparato de verdad. Lo que NO se
// prueba aqui: un Windows de verdad aceptando coma flotante, un DAC desenchufado de verdad, ni como suena. Eso
// es del equipo del bar (y la medida fisica del DAC, del DAC).

class SalidaFalsa : IWavePlayer
{
    public static List<SalidaFalsa> Hechas = new List<SalidaFalsa>();
    public static Func<WaveFormat, Exception> Rechaza;   // para las que se fabriquen: que lanzar al iniciar (null = acepta)
    public Func<WaveFormat, Exception> AlIniciar;
    public WaveFormat Formato; public IWaveProvider Proveedor; public bool Suelta; public int Suscritos; public int Tocada;
    public PlaybackState Estado = PlaybackState.Stopped;
    public SalidaFalsa() { AlIniciar = Rechaza; Hechas.Add(this); }
    public void Init(IWaveProvider p)
    {
        Exception e = AlIniciar != null ? AlIniciar(p.WaveFormat) : null;
        if (e != null) throw e;
        Proveedor = p; Formato = p.WaveFormat;
    }
    public void Play() { Tocada++; Estado = PlaybackState.Playing; }
    public void Stop() { Estado = PlaybackState.Stopped; }
    public void Pause() { Estado = PlaybackState.Paused; }
    public float Volume { get { return 1f; } set { } }
    public PlaybackState PlaybackState { get { return Estado; } }
    public event EventHandler<StoppedEventArgs> PlaybackStopped { add { Suscritos++; } remove { Suscritos--; } }
    public void Dispose() { Suelta = true; }
}

class Fuente : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf; private int pos;
    public Fuente(float[] datos, int fs, int ch) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    // Muestra a muestra, no con Array.Copy: 'b' puede ser la vista flotante que NAudio pone sobre su byte[].
    public int Read(float[] b, int o, int c) { int n = Math.Min(c, d.Length - pos); if (n <= 0) return 0; for (int i = 0; i < n; i++) b[o + i] = d[pos + i]; pos += n; return n; }
}

class Salida
{
    const BindingFlags T = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags I = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Assembly asm;
    static Type TF, TPlay, TTrack, TAlbum, TFile, TIA, TEnc, TMuda;
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
    static object Est(string m, params object[] a)
    {
        try { return TF.GetMethod(m, T).Invoke(null, a); }
        catch (TargetInvocationException tie) { throw tie.InnerException; }
    }
    static object Nuevo(Type t, params object[] a) { return Activator.CreateInstance(t, I, null, a, null); }
    static MethodBase Met(string n) { return TF.GetMethod(n, T); }

    static string Wav(string nombre, int fs, int ch, double seg, Func<long, float> muestra)
    {
        string p = Path.Combine(tmp, nombre);
        using (WaveFileWriter w = new WaveFileWriter(p, WaveFormat.CreateIeeeFloatWaveFormat(fs, ch)))
        {
            long n = (long)(seg * fs); float[] x = new float[n * ch];
            for (long i = 0; i < n; i++) for (int c = 0; c < ch; c++) x[i * ch + c] = muestra(i);
            w.WriteSamples(x, 0, x.Length);
        }
        return p;
    }
    static object Lector(string ruta) { object l = Nuevo(TFile, ruta); TIA.GetProperty("Volume").SetValue(l, 1.0f, null); return l; }
    static object Pista(string ruta)
    {
        object t = Nuevo(TTrack);
        Set(t, "Path", ruta); Set(t, "Title", Path.GetFileNameWithoutExtension(ruta)); Set(t, "Artist", "Prueba");
        return t;
    }
    static object Item(object pista, object disco)
    {
        object it = Nuevo(TPlay);
        Set(it, "Song", pista); Set(it, "Disc", disco); Set(it, "IsVideo", false);
        return it;
    }
    static object MiraCon(string ruta, object lector)
    {
        object mira = Activator.CreateInstance(asm.GetType("Rocola.MiraPorDelante`1").MakeGenericType(TIA), true);
        Set(mira, "para", ruta); Set(mira, "firma", ""); Set(mira, "listo", lector);
        return mira;
    }
    static void PonGuardados(object f)
    {
        foreach (FieldInfo fi in TF.GetFields(I))
            if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null)
                fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
    }
    static Exception Mm(NAudio.MmResult r) { return new NAudio.MmException(r, "waveOutOpen"); }
    static Exception Com(uint h) { return new System.Runtime.InteropServices.COMException("WASAPI", unchecked((int)h)); }

    // ================================================================================================
    //  1) EL FORMATO
    // ================================================================================================
    static void ElFormato(object f)
    {
        Console.WriteLine("\n  1) EL FORMATO QUE RECIBE LA SALIDA CLASICA\n");
        Set(f, "fabricaSalidaClasica", (Func<IWavePlayer>)delegate { return new SalidaFalsa(); });
        int fs = 44100, ch = 2;
        Random rnd = new Random(3);
        float[] x = new float[fs * ch];
        for (int i = 0; i < x.Length; i++) x[i] = (float)(rnd.NextDouble() * 1.8 - 0.9);

        // a) Vista o posterior, y el sistema la acepta: coma flotante tal cual, sin redondeo ni dither.
        SalidaFalsa.Hechas.Clear(); SalidaFalsa.Rechaza = null; Set(f, "flotanteRechazado", false);
        Inv(f, "AbreSalidaClasica", new Fuente(x, fs, ch), true);
        SalidaFalsa s1 = SalidaFalsa.Hechas.Count == 1 ? SalidaFalsa.Hechas[0] : null;
        bool flotante = s1 != null && object.ReferenceEquals(Get(f, "output"), s1) && s1.Suscritos == 1
                        && s1.Formato.Encoding == WaveFormatEncoding.IeeeFloat && s1.Formato.BitsPerSample == 32
                        && s1.Formato.SampleRate == fs && s1.Formato.Channels == ch && (string)Get(f, "formatoSalida") == "float32";
        bool exacto = false;
        if (flotante)
        {
            byte[] b = new byte[x.Length * 4]; int n = s1.Proveedor.Read(b, 0, b.Length);
            exacto = n == b.Length;
            for (int i = 0; exacto && i < x.Length; i++) if (BitConverter.ToSingle(b, i * 4) != x[i]) exacto = false;
        }
        Check(flotante && exacto, "en Vista o posterior la salida recibe coma flotante de 32 bits, muestra a muestra igual que la cadena (sin redondeo ni dither)");

        // b) El sistema rechaza la coma flotante: 16 bits con dither, y no se vuelve a probar en la sesion.
        SalidaFalsa.Hechas.Clear();
        SalidaFalsa.Rechaza = delegate(WaveFormat w) { return w.Encoding == WaveFormatEncoding.IeeeFloat ? Mm(NAudio.MmResult.WaveBadFormat) : null; };
        Inv(f, "AbreSalidaClasica", new Fuente(x, fs, ch), true);
        bool dos = SalidaFalsa.Hechas.Count == 2;
        SalidaFalsa r1 = dos ? SalidaFalsa.Hechas[0] : null, r2 = dos ? SalidaFalsa.Hechas[1] : null;
        bool cae16 = dos && r1.Suelta && r1.Suscritos == 0 && object.ReferenceEquals(Get(f, "output"), r2) && r2.Suscritos == 1
                     && r2.Formato.Encoding == WaveFormatEncoding.Pcm && r2.Formato.BitsPerSample == 16
                     && (string)Get(f, "formatoSalida") == "PCM16+TPDF" && (bool)Get(f, "flotanteRechazado");
        string err = Path.Combine(tmp, "rocola_error.txt");
        bool apunto = File.Exists(err) && File.ReadAllText(err).Contains("no admite coma flotante");
        Check(cae16 && apunto, "si el sistema la rechaza, se suelta esa salida (sin avisos colgando) y se abre en 16 bits con dither; queda apuntado");
        SalidaFalsa.Hechas.Clear(); SalidaFalsa.Rechaza = null;
        Inv(f, "AbreSalidaClasica", new Fuente(x, fs, ch), true);
        Check(SalidaFalsa.Hechas.Count == 1 && SalidaFalsa.Hechas[0].Formato.BitsPerSample == 16,
              "rechazada una vez, en esa sesion ya no se vuelve a probar: directo a 16 bits");

        // c) XP (o sin Vista): 16 bits con dither, como siempre.
        Set(f, "flotanteRechazado", false);
        SalidaFalsa.Hechas.Clear();
        Inv(f, "AbreSalidaClasica", new Fuente(x, fs, ch), false);
        SalidaFalsa q = SalidaFalsa.Hechas.Count == 1 ? SalidaFalsa.Hechas[0] : null;
        bool bien16 = q != null && q.Formato.BitsPerSample == 16 && q.Formato.Encoding == WaveFormatEncoding.Pcm;
        // El dither: una sola vez, triangular de +-1 escalon, sin sesgo.
        double peor = 0, suma = 0, suma2 = 0; int nq = 0;
        if (bien16)
        {
            byte[] b = new byte[x.Length * 2]; int n = q.Proveedor.Read(b, 0, b.Length);
            for (int i = 0; i < n / 2; i++)
            {
                double d = BitConverter.ToInt16(b, i * 2) - x[i] * 32767.0;
                peor = Math.Max(peor, Math.Abs(d)); suma += d; suma2 += d * d; nq++;
            }
        }
        double sesgo = nq > 0 ? suma / nq : 99, ruido = nq > 0 ? Math.Sqrt(suma2 / nq) : 0;
        // Redondeo simple: 0,29 escalones eficaces (truncando, 0,58). Con el dither triangular de +-1 escalon: 0,50.
        Check(bien16 && peor <= 1.5 && Math.Abs(sesgo) < 0.02 && ruido > 0.45 && ruido < 0.55,
              "en XP, 16 bits con dither TPDF como siempre: a menos de 1,5 escalones (" + F(peor, "0.00") + "), sin sesgo (" + F(sesgo, "0.0000")
              + ") y con el ruido justo del dither triangular (" + F(ruido, "0.000") + " escalones eficaces; con redondeo simple serian 0,29)");

        // d) Un fallo del APARATO al probar la coma flotante no se toma por rechazo de formato.
        Set(f, "flotanteRechazado", false);
        SalidaFalsa.Hechas.Clear();
        SalidaFalsa.Rechaza = delegate(WaveFormat w) { return Mm(NAudio.MmResult.BadDeviceId); };
        Exception lanzo = null;
        try { Inv(f, "AbreSalidaClasica", new Fuente(x, fs, ch), true); } catch (Exception ex) { lanzo = ex; }
        Check(lanzo is NAudio.MmException && SalidaFalsa.Hechas.Count == 1 && SalidaFalsa.Hechas[0].Suelta && SalidaFalsa.Hechas[0].Suscritos == 0
              && Get(f, "output") == null && !(bool)Get(f, "flotanteRechazado"),
              "si lo que falla es el aparato (desenchufado), no se prueba 16 bits ni se da la coma flotante por rechazada: se avisa del fallo tal cual");

        // e) Rechaza las dos: se avisa, sin salidas colgando.
        SalidaFalsa.Hechas.Clear();
        SalidaFalsa.Rechaza = delegate(WaveFormat w) { return Mm(NAudio.MmResult.WaveBadFormat); };
        lanzo = null;
        try { Inv(f, "AbreSalidaClasica", new Fuente(x, fs, ch), true); } catch (Exception ex) { lanzo = ex; }
        bool limpias = SalidaFalsa.Hechas.Count == 2;
        foreach (SalidaFalsa s in SalidaFalsa.Hechas) if (!s.Suelta || s.Suscritos != 0) limpias = false;
        Check(lanzo != null && limpias && Get(f, "output") == null && !(bool)Get(f, "flotanteRechazado"),
              "si rechaza los dos formatos (la cancion no le sirve), se avisa como fallo de la cancion y no queda ninguna salida abierta");
        SalidaFalsa.Rechaza = null; Set(f, "flotanteRechazado", false);
        Set(f, "fabricaSalidaClasica", null);
        LaVistaFlotante(f);
    }

    // f) En coma flotante NAudio no le da a la cadena un float[] propio: le da una VISTA flotante sobre su byte[]
    //    (SampleToWaveProvider). Escribir muestra a muestra funciona; Array.Copy revienta y Array.Clear borra bytes en
    //    otro sitio. La cadena ENTERA -des-recorte, DSEE, nivelado con realce 2x, grave, limitador, fundido- y el relevo
    //    sin pausa entre dos canciones, leidos por esa vista, tienen que salir iguales, muestra a muestra, que leidos a
    //    un float[] de verdad. (Esto destapo que el vaciado del realce al final de cada cancion usaba Array.Clear.)
    static ISampleProvider CadenaEntera(object f, float[] x, int fs, int ch, out int retardo)
    {
        Type TDeclip = TF.GetNestedType("DeclipProvider", T), TSp = TF.GetNestedType("SpectralTap", T), TLevel = TF.GetNestedType("LevelTap", T);
        Type TBajo = TF.GetNestedType("DeepBassProvider", T), TLim = TF.GetNestedType("LimitadorPicoReal", T), TFade = TF.GetNestedType("FadeProvider", T);
        ISampleProvider s = new Fuente(x, fs, ch);
        object dc = Nuevo(TDeclip, s); retardo = (int)Prop(dc, "DelayFrames"); s = (ISampleProvider)dc;
        System.Reflection.ConstructorInfo c2 = TSp.GetConstructor(I, null, new Type[] { typeof(ISampleProvider), typeof(int) }, null);
        object sp = c2 != null ? c2.Invoke(new object[] { s, 2048 }) : Nuevo(TSp, s);
        Set(sp, "Active", true); s = (ISampleProvider)sp;
        object tap = Nuevo(TLevel, s, f); Set(tap, "Excite", 0.3f); Set(tap, "Gain", 1.3f);
        retardo += (int)Prop(tap, "DelayFrames");
        object bajo = Nuevo(TBajo, tap, 0.5f);
        object lim = Nuevo(TLim, bajo, 0.98f, 3.0); retardo += (int)Prop(lim, "DelayFrames");
        return (ISampleProvider)Nuevo(TFade, lim, (long)(x.Length / ch + retardo), 0.5);
    }
    static float[] Musica(int fs, int ch, double seg, int semilla)
    {
        Random r = new Random(semilla); long n = (long)(seg * fs); float[] x = new float[n * ch];
        for (long i = 0; i < n; i++)
        {
            double v = 0.3 * Math.Sin(2 * Math.PI * 110 * i / fs) + 0.1 * Math.Sin(2 * Math.PI * 3000 * i / fs) + (r.NextDouble() - 0.5) * 0.1;
            if ((i % (fs / 2)) < fs / 50) v *= 2.2;   // golpes que el limitador tiene que atender
            for (int c = 0; c < ch; c++) x[i * ch + c] = (float)(c == 1 ? -v * 0.8 : v);
        }
        return x;
    }
    static object Secuencia(object f, float[] a, float[] b, int fs, int ch)
    {
        int ra, rb;
        object e = Activator.CreateInstance(TEnc, I, null, new object[] { CadenaEntera(f, a, fs, ch, out ra), "A" }, null);
        ISampleProvider cb = CadenaEntera(f, b, fs, ch, out rb);
        Inv(e, "Prepara", cb, rb, "B");
        return e;
    }
    static void LaVistaFlotante(object f)
    {
        int fs = 44100, ch = 2;
        float[] a = Musica(fs, ch, 1.5, 11), b = Musica(fs, ch, 1.2, 12);
        List<float> ya = new List<float>(); float[] fb = new float[4096]; int n;
        ISampleProvider pa = (ISampleProvider)Secuencia(f, a, b, fs, ch);
        while ((n = pa.Read(fb, 0, fb.Length)) > 0) for (int i = 0; i < n; i++) ya.Add(fb[i]);
        List<float> yb = new List<float>(); object eb = Secuencia(f, a, b, fs, ch);
        IWaveProvider pb = new NAudio.Wave.SampleProviders.SampleToWaveProvider((ISampleProvider)eb);
        byte[] bb = new byte[4096 * 4];
        for (int k = 0; k < bb.Length; k++) bb[k] = 0x7F;   // restos de la vuelta anterior
        while ((n = pb.Read(bb, 0, bb.Length)) > 0)
        {
            for (int i = 0; i < n / 4; i++) yb.Add(BitConverter.ToSingle(bb, i * 4));
            for (int k = 0; k < bb.Length; k++) bb[k] = 0x7F;
        }
        int dif = 0; for (int i = 0; i < Math.Min(ya.Count, yb.Count); i++) if (ya[i] != yb[i]) dif++;
        object[] fa = new object[2];
        bool sinFallo = !(bool)TEnc.GetMethod("TomaFallo", I).Invoke(eb, fa);
        Check(ya.Count == yb.Count && dif == 0 && sinFallo && ya.Count > (a.Length + b.Length) * 9 / 10,
              "la cadena entera y el relevo sin pausa, leidos por la vista flotante de NAudio, salen iguales muestra a muestra ("
              + ya.Count + " muestras; distintas: " + dif + "), sin fallos: la coma flotante no rompe nada");
    }

    // ================================================================================================
    //  2) EL APARATO O LA CANCION
    // ================================================================================================
    static bool Disp(Exception e) { return (bool)Est("EsFalloDelDispositivo", e); }
    static void AparatoOCancion()
    {
        Console.WriteLine("\n  2) FALLO DEL APARATO O FALLO DE LA CANCION\n");
        bool aparato = true, cancion = true;
        foreach (NAudio.MmResult r in new NAudio.MmResult[] { NAudio.MmResult.BadDeviceId, NAudio.MmResult.NotEnabled, NAudio.MmResult.AlreadyAllocated,
                                                             NAudio.MmResult.InvalidHandle, NAudio.MmResult.NoDriver, NAudio.MmResult.NoDriverCallback })
            if (!Disp(Mm(r))) aparato = false;
        foreach (uint h in new uint[] { 0x88890004u, 0x8889000Au, 0x8889000Fu, 0x88890010u, 0x88890026u, 0x80070490u })
            if (!Disp(Com(h))) aparato = false;
        if (!Disp((Exception)Activator.CreateInstance(TMuda, true))) aparato = false;
        if (!Disp(new Exception("envuelto", Mm(NAudio.MmResult.NoDriver)))) aparato = false;
        foreach (NAudio.MmResult r in new NAudio.MmResult[] { NAudio.MmResult.WaveBadFormat, NAudio.MmResult.UnspecifiedError, NAudio.MmResult.InvalidParameter,
                                                             NAudio.MmResult.AcmNotPossible, NAudio.MmResult.MemoryAllocationError })
            if (Disp(Mm(r))) cancion = false;
        foreach (uint h in new uint[] { 0x88890008u, 0x80004005u })
            if (Disp(Com(h))) cancion = false;
        foreach (Exception e in new Exception[] { new IOException("disco"), new InvalidDataException("mp3 roto"), new InvalidCastException("x"), new NotSupportedException("formato") })
            if (Disp(e)) cancion = false;
        Check(aparato, "del aparato: desenchufado, sin controlador, tomado por otro programa, sin servicio de audio, sin aparato predeterminado, o mudo (14 casos)");
        Check(cancion, "de la cancion: formato rechazado, archivo roto, descompresor, y lo que no se sabe (11 casos): como siempre, credito devuelto y a la siguiente");
    }

    // ================================================================================================
    //  3) LA ESPERA
    // ================================================================================================
    static void LaEspera(object f)
    {
        Console.WriteLine("\n  3) SI FALLA EL APARATO: SE ESPERA Y SE RETOMA LA MISMA\n");
        bool esperas = (int)Est("EsperaReintento", 0) == 500 && (int)Est("EsperaReintento", 1) == 2000 && (int)Est("EsperaReintento", 2) == 2000
                       && (int)Est("EsperaReintento", 3) == 10000 && (int)Est("EsperaReintento", 40) == 10000;
        Check(esperas, "reintentos: el primero a medio segundo (un tropiezo), luego 2 s y desde el tercero cada 10 s, sin fin");
        bool pos = Math.Abs((double)Est("PosicionOida", 95.0, 180.0, 850) - 94.15) < 1e-9 && (double)Est("PosicionOida", 0.3, 180.0, 850) == 0
                   && (double)Est("PosicionOida", 200.0, 180.0, 240) == 180 && (double)Est("PosicionOida", double.NaN, 180.0, 240) == 0;
        Check(pos, "se retoma desde lo que ya se oyo: lo leido menos el colchon de la salida (nunca antes del principio ni despues del final)");

        int fs = 44100, ch = 2;
        string ra = Wav("a.wav", fs, ch, 3.0, delegate(long i) { return 0.2f; });
        object disco = Nuevo(TAlbum);
        object tA = Pista(ra), tB = Pista(Wav("b.wav", fs, ch, 1.0, delegate(long i) { return 0.1f; }));
        object iA = Item(tA, disco), iB = Item(tB, disco);
        IList q = (IList)Get(f, "queue"); q.Clear(); q.Add(iB);
        Set(f, "nowPlaying", iA); Set(f, "credits", 5);
        Set(f, "progressTimer", new System.Windows.Forms.Timer());
        Set(f, "lblStatus", new System.Windows.Forms.Label());
        object lA = Lector(ra);
        Set(f, "reader", lA);
        SalidaFalsa sal = new SalidaFalsa(); sal.Estado = PlaybackState.Playing;
        Set(f, "output", sal);
        Set(f, "reintentos", 0);
        string incompat = Path.Combine(tmp, "rocola_incompatibles.txt");
        bool r1 = (bool)Inv(f, "SalidaCaida", tA, 12.5, Mm(NAudio.MmResult.BadDeviceId), false);
        System.Windows.Forms.Timer tm = (System.Windows.Forms.Timer)Get(f, "reintentoSalida");
        bool quieto = r1 && (int)Get(f, "credits") == 5 && q.Count == 1 && object.ReferenceEquals(q[0], iB) && object.ReferenceEquals(Get(f, "nowPlaying"), iA)
                      && !File.Exists(incompat);
        bool espera = object.ReferenceEquals(Get(f, "reintentoCancion"), tA) && (double)Get(f, "reintentoDesde") == 12.5 && (int)Get(f, "reintentos") == 1
                      && tm != null && tm.Enabled && tm.Interval == 500 && Get(f, "output") == null && Get(f, "reader") == null;
        string est = ((System.Windows.Forms.Label)Get(f, "lblStatus")).Text;
        Check(quieto, "al fallar el aparato: ni credito devuelto, ni cancion saltada, ni apuntada como incompatible; la cola queda como estaba");
        Check(espera && est.Contains("Sin salida"), "se suelta la salida y se espera para retomar la MISMA cancion en el mismo sitio (12,5 s); el letrero lo dice");
        Inv(f, "SalidaCaida", tA, 12.5, Mm(NAudio.MmResult.BadDeviceId), false);
        Check((int)Get(f, "reintentos") == 2 && tm.Interval == 2000, "si al reintentar sigue sin aparato, se vuelve a esperar (2 s), sin tocar nada mas");
        Inv(f, "DisposePlaybackNow");
        Check(Get(f, "reintentoCancion") == null && !tm.Enabled, "saltar (o cerrar) cancela la espera: PlayNext empieza la siguiente como siempre");

        // Si mientras tanto la cancion ya no es la que toca (se salto), el reintento no hace nada.
        Set(f, "reintentoCancion", tA); Set(f, "nowPlaying", iB); Set(f, "reanudarEn", 0.0);
        Inv(f, "ReintentoSalida_Tick", null, EventArgs.Empty);
        Check(Get(f, "reintentoCancion") == null && (double)Get(f, "reanudarEn") == 0, "si al llegar el reintento ya suena otra cosa, no se toca nada");

        // A media cancion: a la sexta caida del mismo elemento se rinde (credito devuelto como antes); otra cancion empieza de cero.
        Set(f, "nowPlaying", iA); Set(f, "caidaItem", null);
        bool cinco = true;
        for (int i = 0; i < 5; i++)
        {
            Set(f, "reintentos", 0); Set(f, "output", new SalidaFalsa());
            if (!(bool)Inv(f, "SalidaCaida", tA, 30.0, Com(0x88890004u), true)) cinco = false;
        }
        SalidaFalsa ultima = new SalidaFalsa(); Set(f, "output", ultima); Set(f, "reintentos", 0);
        bool sexta = !(bool)Inv(f, "SalidaCaida", tA, 30.0, Com(0x88890004u), true);
        bool intacta = object.ReferenceEquals(Get(f, "output"), ultima) && !ultima.Suelta;   // se rinde sin tocar nada: sigue el camino de siempre
        Set(f, "nowPlaying", iB);
        bool otra = (bool)Inv(f, "SalidaCaida", tB, 0.0, Com(0x88890004u), true);
        bool alAbrir = true; Set(f, "nowPlaying", iA);
        for (int i = 0; i < 20; i++) { Set(f, "reintentos", 0); if (!(bool)Inv(f, "SalidaCaida", tA, 0.0, Mm(NAudio.MmResult.NoDriver), false)) alAbrir = false; }
        Check(cinco && sexta && intacta,
              "si se cae una y otra vez A MEDIA cancion, a la sexta se da por fallida como antes (sin bucle): esa vez no se toca nada y sigue el camino de siempre");
        Check(otra, "otra cancion empieza su cuenta de cero");
        Check(alAbrir, "si el aparato no esta al ABRIR, se espera sin limite: la cola aguarda hasta que vuelva");
        Inv(f, "DisposePlaybackNow");

        // Lo que toma StartOpenedTrack: una sola vez.
        Set(f, "reanudarEn", 42.5);
        double d1 = (double)Inv(f, "TomaReanudar"), d2 = (double)Inv(f, "TomaReanudar");
        Check(d1 == 42.5 && d2 == 0 && (double)Get(f, "inicioSeg") == 0, "el punto de retomada se usa una vez (el siguiente arranque ya es desde el principio)");
    }

    // ================================================================================================
    //  4) RETOMAR
    // ================================================================================================
    static void Retomar(object f)
    {
        Console.WriteLine("\n  4) RETOMAR A MEDIA CANCION\n");
        int fs = 44100, ch = 2;
        string rr = Wav("rampa.wav", fs, ch, 5.0, delegate(long i) { return (float)(i / 250000.0); });   // cada cuadro, su valor
        object l = Lector(rr);
        TIA.GetMethod("Salta").Invoke(l, new object[] { TimeSpan.FromSeconds(2.5) });
        float[] b = new float[2]; ((ISampleProvider)l).Read(b, 0, 2);
        float esperado = (float)(110250 / 250000.0);
        TimeSpan donde = (TimeSpan)TIA.GetProperty("CurrentTime").GetValue(l, null);
        Check(b[0] == esperado && b[1] == esperado && Math.Abs(donde.TotalSeconds - 2.5) < 0.01, "el lector salta al sitio: lo primero que sale es el cuadro de los 2,5 s (el 110.250)");
        ((IDisposable)l).Dispose();

        // La cadena de una retomada: el fundido acaba donde acaba la cancion, y de un trozo no se aprende nada.
        string rs = Wav("seno.wav", fs, ch, 5.0, delegate(long i) { return (float)(0.3 * Math.Sin(2 * Math.PI * 440 * i / fs)); });
        object t = Pista(rs);
        Set(f, "crossfadeSec", 0.5f); Set(f, "normalizeOn", true);
        object lr = Lector(rs);
        TIA.GetMethod("Salta").Invoke(lr, new object[] { TimeSpan.FromSeconds(2.0) });
        object cad = Inv(f, "ArmaCadena", t, lr, false, 2.0);
        List<float> y = new List<float>(); float[] buf = new float[4096]; int n;
        while ((n = ((ISampleProvider)Get(cad, "Salida")).Read(buf, 0, buf.Length)) > 0) for (int i = 0; i < n; i++) y.Add(buf[i]);
        int frames = y.Count / ch;
        double rmsMedio = Rms(y, ch, frames / 2 - 2205, frames / 2 + 2205), rmsFinal = Rms(y, ch, frames - 441, frames);
        double rmsAntes = Rms(y, ch, frames - (int)(0.9 * fs), frames - (int)(0.7 * fs));
        bool funde = rmsMedio > 0.15 && rmsAntes > rmsMedio * 0.9 && rmsFinal < rmsMedio * 0.05;
        bool sinAprender = Get(cad, "MedidaRuta") == null && Get(cad, "EqRuta") == null;
        object lEntera = Lector(rs);
        object cadEntera = Inv(f, "ArmaCadena", t, lEntera, false, 0.0);
        bool enteraAprende = (string)Get(cadEntera, "MedidaRuta") == rs;
        Check(Math.Abs(frames - 3.0 * fs) < 0.01 * fs && funde,
              "retomada a los 2 s de 5: suenan los 3 s que faltan y el fundido de salida acaba justo al final (" + F(rmsFinal / Math.Max(rmsMedio, 1e-9), "0.000") + " del nivel en los ultimos 10 ms)");
        Check(sinAprender && enteraAprende, "de una cancion retomada no se aprende sonoridad ni huella (seria la de un trozo); entera, si");
        Set(f, "crossfadeSec", 0f);
    }
    static double Rms(List<float> y, int ch, int desde, int hasta)
    {
        double s = 0; int n = 0;
        for (int i = Math.Max(0, desde); i < hasta && i * ch < y.Count; i++) { double v = y[i * ch]; s += v * v; n++; }
        return n > 0 ? Math.Sqrt(s / n) : 0;
    }

    // ================================================================================================
    //  5) EL VIGIA
    // ================================================================================================
    static void ElVigia(object f)
    {
        Console.WriteLine("\n  5) SI LA SALIDA SE QUEDA MUDA SIN AVISAR\n");
        int fs = 44100, ch = 2;
        object l = Lector(Wav("vigia.wav", fs, ch, 30.0, delegate(long i) { return 0.1f; }));
        SalidaFalsa s = new SalidaFalsa(); s.Estado = PlaybackState.Playing;
        Set(f, "reader", l); Set(f, "output", s); Set(f, "videoOn", false); Set(f, "audioOpeningForMeasurement", false);
        Set(f, "vigiaQuietos", 0); Set(f, "vigiaLeido", -1.0);
        int primera = -1;
        for (int i = 1; i <= 25 && primera < 0; i++) if ((bool)Inv(f, "SalidaMuda")) primera = i;
        bool reinicia = true; for (int i = 0; i < 19; i++) if ((bool)Inv(f, "SalidaMuda")) reinicia = false;
        // primera = 21: la primera llamada solo apunta donde esta; despues, 20 sin moverse (10 s).
        Check(primera == 21 && reinicia, "10 s (20 vistazos de la barra) con la salida en marcha sin pedir musica: se da por muda; y la cuenta vuelve a empezar");
        float[] buf = new float[4410 * ch]; bool nunca = true;
        for (int i = 0; i < 60; i++) { ((ISampleProvider)l).Read(buf, 0, buf.Length); if ((bool)Inv(f, "SalidaMuda")) nunca = false; }
        Check(nunca, "mientras pide musica, nunca");
        s.Estado = PlaybackState.Paused; bool pausa = true;
        for (int i = 0; i < 40; i++) if ((bool)Inv(f, "SalidaMuda")) pausa = false;
        s.Estado = PlaybackState.Playing; Set(f, "videoOn", true); bool video = true;
        for (int i = 0; i < 40; i++) if ((bool)Inv(f, "SalidaMuda")) video = false;
        Set(f, "videoOn", false); Set(f, "audioOpeningForMeasurement", true); bool medida = true;
        for (int i = 0; i < 40; i++) if ((bool)Inv(f, "SalidaMuda")) medida = false;
        Set(f, "audioOpeningForMeasurement", false);
        Check(pausa && video && medida, "ni con la salida parada, ni con un video, ni durante la medida de Ctrl+F3");
        bool relevo = true; Set(f, "vigiaQuietos", 0); Set(f, "vigiaLeido", -1.0);
        for (int k = 0; k < 10; k++)
        {
            for (int i = 0; i < 3; i++) if ((bool)Inv(f, "SalidaMuda")) relevo = false;   // 1,5 s quieto (el relevo tarda <1 s en recogerse)
            ((ISampleProvider)l).Read(buf, 0, buf.Length);
        }
        Check(relevo, "un rato corto sin avanzar (el cambio de cancion sin pausa, un atoron) no cuenta");
        ((IDisposable)l).Dispose(); Set(f, "reader", null); Set(f, "output", null);
    }

    // ================================================================================================
    //  6) CAMBIO DE APARATO
    // ================================================================================================
    static void CambioDeAparato(object f)
    {
        Console.WriteLine("\n  6) CAMBIO DE APARATO PREDETERMINADO\n");
        string actual = "{0.0.0.00000000}.{AAA}";
        Set(f, "idSalidaActual", (Func<string>)delegate { return actual; });
        Set(f, "idSalidaAbierta", null);
        bool sinSaber = (bool)Inv(f, "MismaSalida");
        Inv(f, "SalidaArranco", "{0.0.0.00000000}.{aaa}");
        bool mismo = (bool)Inv(f, "MismaSalida") && (int)Get(f, "reintentos") == 0;
        actual = "{0.0.0.00000000}.{BBB}";
        bool otro = !(bool)Inv(f, "MismaSalida");
        actual = null;
        bool ninguno = (bool)Inv(f, "MismaSalida");
        Check(sinSaber && mismo && otro && ninguno,
              "se sabe en que aparato se abrio la salida: el mismo (sin distinguir mayusculas) sigue; otro no; si no se sabe o no hay ninguno, no se estorba");

        // De punta a punta: con otro aparato predeterminado, PreparaSiguienteSinPausa no encadena.
        int fs = 44100, ch = 2;
        string pa = Wav("pa.wav", fs, ch, 4.0, delegate(long i) { return (float)(0.2 * Math.Sin(i * 0.05)); });
        string pb = Wav("pb.wav", fs, ch, 3.0, delegate(long i) { return (float)(0.2 * Math.Sin(i * 0.07)); });
        object disco = Nuevo(TAlbum);
        object tA = Pista(pa), tB = Pista(pb);
        IList pistas = (IList)Get(disco, "Tracks"); pistas.Add(tA); pistas.Add(tB);
        object iA = Item(tA, disco), iB = Item(tB, disco);
        bool[] preparo = new bool[2];
        string[] aparatos = { "{BBB}", "{AAA}" };
        for (int k = 0; k < 2; k++)
        {
            IList q = (IList)Get(f, "queue"); q.Clear(); q.Add(iB);
            Set(f, "nowPlaying", iA);
            object l = Lector(pa);
            object cad = Inv(f, "ArmaCadena", tA, l, false, 0.0);
            Set(cad, "Item", iA);
            Set(f, "reader", l); Set(f, "cadenaSonando", cad); Set(f, "cadenaPreparada", null);
            Set(f, "secuencia", Activator.CreateInstance(TEnc, I, null, new object[] { Get(cad, "Salida"), cad }, null));
            SalidaFalsa s = new SalidaFalsa(); s.Estado = PlaybackState.Playing; Set(f, "output", s);
            Set(f, "lookahead", MiraCon(pb, Lector(pb)));
            Set(f, "idSalidaAbierta", "{AAA}");
            string ap = aparatos[k];
            Set(f, "idSalidaActual", (Func<string>)delegate { return ap; });
            Inv(f, "PreparaSiguienteSinPausa");
            preparo[k] = Get(f, "cadenaPreparada") != null;
            Inv(f, "SueltaSinPausa");
            ((IDisposable)l).Dispose();
        }
        Check(!preparo[0] && preparo[1], "con otro aparato predeterminado la siguiente no se encadena (se abrira en el nuevo al acabar esta); con el mismo, si");
        Set(f, "idSalidaActual", null); Set(f, "idSalidaAbierta", null); Set(f, "reader", null); Set(f, "output", null); Set(f, "lookahead", null);
    }

    // ================================================================================================
    //  7) EL CABLEADO
    // ================================================================================================
    static void ElCableado()
    {
        Console.WriteLine("\n  7) EL CABLEADO (leyendo el programa compilado)\n");
        List<string> arranca = Codigo.Llamadas(Met("StartOpenedTrack"));
        Check(Codigo.Llama(arranca, "AbreSalidaClasica") && Codigo.Llama(arranca, "FlotanteAdmitido") && !Codigo.Llama(arranca, "WaveOut..ctor")
              && !Codigo.Llama(arranca, "DitherWaveProvider16..ctor"),
              "la cancion abre la salida clasica por AbreSalidaClasica (coma flotante si hay Vista o posterior), no con un WaveOut suelto");
        Check(Codigo.Llama(arranca, "EsFalloDelDispositivo") && Codigo.Llama(arranca, "SalidaCaida") && Codigo.Llama(arranca, "SalidaArranco")
              && Codigo.Llama(arranca, "TomaReanudar") && Codigo.Llama(arranca, "Salta"),
              "y si el aparato no abre, espera en vez de dar la cancion por rota; si abre, apunta en que aparato; y retoma desde donde iba");
        int dithers = 0; string donde = "";
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            foreach (string s in Codigo.Llamadas(m)) if (s.EndsWith("DitherWaveProvider16..ctor")) { dithers++; donde = m.Name; }
        Check(dithers == 1 && donde == "AbreSalidaClasica", "el dither se pone en UN solo sitio (" + donde + "), al final y solo en 16 bits: nunca dos veces en la cadena");
        List<string> para = Codigo.Nombres(Met("Output_PlaybackStopped"));
        int iCaida = para.IndexOf("SalidaCaida"), iDev = para.IndexOf("RefundCredit");
        Check(para.Contains("EsFalloDelDispositivo") && iCaida >= 0 && iDev > iCaida,
              "al pararse la salida con un fallo del aparato, se retoma antes de pensar en devolver creditos");
        List<string> caida = Codigo.Nombres(Met("SalidaCaida"));
        Check(caida.Contains("DisposePlaybackNow") && !caida.Contains("RefundCredit") && !caida.Contains("NoteIncompatible") && !caida.Contains("PlayNext")
              && !caida.Contains("RemoveAt") && !caida.Contains("Remove") && !caida.Contains("Clear")
              && !Codigo.EscribeCampo(new List<MethodBase> { Met("SalidaCaida") }, "credits"),
              "la espera no devuelve, no cobra, no apunta incompatibles, no salta y no toca la cola");
        List<string> tick = Codigo.Nombres(Met("ReintentoSalida_Tick"));
        Check(tick.Contains("PlayTrack") && Codigo.EscribeCampo(new List<MethodBase> { Met("ReintentoSalida_Tick") }, "reanudarEn"),
              "el reintento vuelve a abrir la misma cancion desde el punto guardado");
        Check(Codigo.Llama(Met("DisposePlaybackNow"), "CancelaReintentoSalida"), "saltar o cerrar cancelan la espera");
        List<string> barra = Codigo.Nombres(Met("ProgressTimer_Tick"));
        Check(barra.Contains("SalidaMuda") && barra.Contains("Output_PlaybackStopped"), "la barra de progreso vigila la salida muda y la trata como un aviso de la salida");
        Check(Codigo.Llama(Met("PreparaSiguienteSinPausa"), "MismaSalida"), "la continuidad sin pausas mira que el aparato siga siendo el mismo");
        MethodBase re = Met("ReintentaWaveOut");
        if (re != null) Check(Codigo.EscribeCampo(new List<MethodBase> { re }, "reanudarEn"), "HD: si WASAPI no arranca y se reintenta por WaveOut, se retoma en el mismo sitio");
        Check(Codigo.Llama(Met("AudioStartFailed"), "RefundCredit") && Codigo.Llama(Met("AudioStartFailed"), "NoteIncompatible"),
              "un fallo de la CANCION sigue como siempre: credito devuelto y apuntada");
    }

    // ================================================================================================
    //  8) LA CANCION DE LA MEDIDA, CALLADA SIN TOCAR NINGUN VOLUMEN DE WINDOWS
    // ================================================================================================
    // Antes se callaba bajandole el volumen a la salida. En WASAPI eso es el volumen GENERAL de Windows y en WaveOut
    // (Vista o posterior) el de la rocola en el mezclador; ninguno se devolvia al acabar la medida.
    static List<string> VolumenesDeSalida(Type tf)
    {
        List<string> r = new List<string>();
        foreach (MethodBase m in Codigo.TodosLosMetodos(tf))
            foreach (string s in Codigo.Llamadas(m))
                if (s.EndsWith(".set_Volume") && (s.StartsWith("IWavePlayer.") || s.StartsWith("WasapiOut.") || s.StartsWith("WaveOut.")
                    || s.StartsWith("WaveOutEvent.") || s.StartsWith("DirectSoundOut.") || s.StartsWith("AsioOut.")))
                    r.Add(m.Name);
        return r;
    }
    static void LaMedidaCallada(object f, string exe)
    {
        Console.WriteLine("\n  8) LA CANCION DE LA MEDIDA, CALLADA SIN TOCAR VOLUMENES DE WINDOWS\n");
        int fs = 44100, ch = 2;
        float[] x = Musica(fs, ch, 1.0, 7);
        Set(f, "audioOpeningForMeasurement", true);
        Fuente src = new Fuente(x, fs, ch);
        ISampleProvider c = (ISampleProvider)Inv(f, "CallaSiEsMedida", src);
        float[] y = new float[x.Length + 100];
        for (int i = 0; i < y.Length; i++) y[i] = 1f;
        int n = c.Read(y, 0, y.Length);
        bool ceros = n == x.Length && c.WaveFormat.SampleRate == fs && c.WaveFormat.Channels == ch;
        for (int i = 0; ceros && i < y.Length; i++) if (y[i] != (i < n ? 0f : 1f)) ceros = false;
        // Por la vista flotante que NAudio pone sobre su byte[] (coma flotante): tambien ceros, y en su sitio.
        IWaveProvider w = new NAudio.Wave.SampleProviders.SampleToWaveProvider((ISampleProvider)Inv(f, "CallaSiEsMedida", new Fuente(x, fs, ch)));
        byte[] b = new byte[x.Length * 4 + 64];
        for (int i = 0; i < b.Length; i++) b[i] = 0x55;
        int nb = w.Read(b, 0, x.Length * 4);
        bool vista = nb == x.Length * 4;
        for (int i = 0; vista && i < b.Length; i++) if (b[i] != (i < nb ? (byte)0 : (byte)0x55)) vista = false;
        Set(f, "audioOpeningForMeasurement", false);
        Fuente fuera = new Fuente(x, fs, ch);
        bool pasa = object.ReferenceEquals(Inv(f, "CallaSiEsMedida", fuera), fuera);
        Check(ceros && vista && pasa, "la cancion de la medida se calla al final de la cadena: se lee entera (el mismo trabajo) y a la salida solo le llegan ceros, tambien por la vista flotante de NAudio; fuera de la medida la cadena pasa tal cual");
        List<string> arranca = Codigo.Nombres(Met("StartOpenedTrack"));
        int iCalla = arranca.IndexOf("CallaSiEsMedida"), iAbre = arranca.IndexOf("AbreSalidaClasica");
        Check(iCalla >= 0 && iAbre > iCalla, "la cancion calla su cadena ANTES de darsela a la salida");
        // Ningun sitio le cambia el volumen a la salida: ni en este programa ni en el de tarjeta, donde esta la medida.
        List<string> revisados = new List<string>(), tocan = new List<string>(); bool conTarjeta = false;
        string dir = Path.GetDirectoryName(Path.GetFullPath(exe));
        foreach (string nombre in new string[] { Path.GetFullPath(exe), Path.Combine(dir, "rocola_cpu.exe"), Path.Combine(dir, "rocola_gpu.exe"),
                                                 Path.Combine(dir, "Rocola-cpu.exe"), Path.Combine(dir, "Rocola-gpu.exe") })
        {
            if (!File.Exists(nombre) || revisados.Contains(Path.GetFileName(nombre))) continue;
            Type tf = Assembly.Load(File.ReadAllBytes(nombre)).GetType("Rocola.Form1", false);
            if (tf == null) continue;
            revisados.Add(Path.GetFileName(nombre));
            foreach (string m in VolumenesDeSalida(tf)) tocan.Add(Path.GetFileName(nombre) + ": " + m);
            MethodBase mide = tf.GetMethod("MideRendimiento", T);
            if (mide != null && Codigo.Llama(mide, "PlayTrackForMeasurement")) conTarjeta = true;
        }
        Check(conTarjeta, "se repaso tambien el programa con tarjeta, donde esta la medida de rendimiento (" + string.Join(", ", revisados.ToArray()) + ")");
        Check(revisados.Count > 0 && tocan.Count == 0, "ningun sitio del programa le cambia el volumen a la salida (en WASAPI seria el volumen general de Windows; en WaveOut, el de la rocola en el mezclador)"
              + (tocan.Count > 0 ? ": " + string.Join(", ", tocan.ToArray()) : ""));
    }

    static int Main(string[] args)
    {
        asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TPlay = TF.GetNestedType("PlayItem", T); TTrack = TF.GetNestedType("Track", T); TAlbum = TF.GetNestedType("Album", T);
        TFile = TF.GetNestedType("FileAudioSource", T); TIA = TF.GetNestedType("IAudioSource", T);
        TMuda = TF.GetNestedType("SalidaMudaException", T);
        TEnc = asm.GetType("Rocola.Encadenado");
        if (Met("AbreSalidaClasica") == null || TMuda == null) { Check(false, "no esta la salida nueva (SalidaSonido.cs)"); return 1; }
        tmp = Path.Combine(Path.GetTempPath(), "rocola_salida_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", tmp);
        try
        {
            object f = Codigo.FormSinEncender(TF);
            PonGuardados(f);
            ElFormato(f);
            AparatoOCancion();
            LaEspera(f);
            Retomar(f);
            ElVigia(f);
            CambioDeAparato(f);
            ElCableado();
            LaMedidaCallada(f, args.Length > 0 ? args[0] : "rocola.exe");
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
        Console.WriteLine(fallos == 0 ? ("\n  TODO BIEN (" + oks + " comprobaciones)") : ("\n  FALLOS: " + fallos));
        return fallos;
    }
}
