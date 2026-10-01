using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using NAudio.Wave;

// SIN PAUSA ENTRE CANCIONES (etapa 7 de audio): Rocola/SinPausa.cs y Rocola/Form1.SinPausa.cs.
//
// Antes, al acabar una cancion se cerraba la salida, se abria la siguiente y se volvia a abrir la salida:
// entre las dos quedaba un silencio que no estaba en la musica. Ahora la salida se queda abierta y la
// siguiente, armada de antemano, entra en la misma lectura. Aqui se comprueba:
//
//   1) EL MECANISMO (Encadenado), con cadenas de mentira de contenido conocido: la siguiente entra sin una
//      muestra de mas ni de menos y sin su cebado; el relevo se apunta una vez y se recoge cuando ya se oye;
//      otro formato no entra; retirar a tiempo si, tarde no; un fallo de lectura no revienta la salida y se
//      apunta de quien fue; una vacia; un cuadro a medias; dos hilos a la vez; y leer no reserva memoria.
//   2) LAS CADENAS DE VERDAD (des-recorte, DSEE, nivelado con realce, grave, limitador y fundido): su
//      cebado es silencio y dura justo lo que dicen sus retrasos; encadenadas suenan igual que cada una
//      sola, bit a bit, sin el cebado de la segunda; y el fundido se quita a tiempo o se termina.
//   3) LA ROCOLA (Form1 sin ventana): la medida en vivo pasa a la nueva en su primer bloque; el relevo pasa la
//      cola como PlayNext (la que entra sale de la cola y suena; los creditos no se tocan; el estado guardado
//      dice lo que suena); se suelta el lector de la anterior y se cierra su medida; al parar, la preparada se
//      suelta; "pista siguiente del mismo disco" solo en el mismo disco; el lector preparado solo se mira.
//   4) EL CABLEADO, leyendo el programa compilado.
//   5) LOS NUMEROS: el hueco que quedaba y el que queda, y lo que cuesta.
//
// Lo que NO prueba: la salida de verdad (WaveOut o WASAPI no existen aqui), el descompresor de MP3 de Windows
// ni como se oye. Eso queda para el equipo del bar.

class Cadena : ISampleProvider
{
    // Contenido conocido; los primeros 'cebado' cuadros son silencio, como el de las cadenas de verdad;
    // puede fallar al llegar a la muestra 'fallaEn' (-1 = nunca).
    private readonly float[] d; private readonly WaveFormat wf; private readonly int cebado; private readonly long fallaEn;
    private long pos;
    public int Lecturas;
    public Cadena(float[] datos, int fs, int ch, int cebadoCuadros, long fallaEnMuestra)
    { d = datos; wf = FormatoPrueba.Flotante(fs, ch); cebado = cebadoCuadros * ch; fallaEn = fallaEnMuestra; }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c)
    {
        Lecturas++;
        if (fallaEn >= 0 && pos >= fallaEn) throw new InvalidDataException("archivo roto (de mentira)");
        long total = cebado + d.Length;
        int n = (int)Math.Min(c, total - pos);
        if (fallaEn >= 0) n = (int)Math.Min(n, fallaEn - pos);
        for (int i = 0; i < n; i++) { long p = pos + i; b[o + i] = p < cebado ? 0f : d[p - cebado]; }
        pos += n;
        return n;
    }
}

class FuenteSP : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf; private int pos;
    public FuenteSP(float[] datos, int fs, int ch) { d = datos; wf = FormatoPrueba.Flotante(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c) { int n = Math.Min(c, d.Length - pos); if (n <= 0) return 0; Array.Copy(d, pos, b, o, n); pos += n; return n; }
}

public class Abierto : IDisposable
{
    public string Ruta; public int Sueltas;
    public void Dispose() { Interlocked.Increment(ref Sueltas); }
}

// Una salida de mentira: PreparaSiguienteSinPausa solo pide que haya una abierta.
class SalidaFalsa : IWavePlayer
{
    public void Play() { } public void Stop() { } public void Pause() { } public void Init(IWaveProvider w) { }
    public float Volume { get { return 1f; } set { } }
    public PlaybackState PlaybackState { get { return PlaybackState.Playing; } }
    public event EventHandler<StoppedEventArgs> PlaybackStopped { add { } remove { } }
    public void Dispose() { }
}

static class FormatoPrueba
{
    internal static WaveFormat Flotante(int hz, int ch)
    {
        if (ch != 6) return WaveFormat.CreateIeeeFloatWaveFormat(hz, ch);
        WaveFormatExtensible f = new WaveFormatExtensible(hz, 32, ch);
        typeof(WaveFormatExtensible).GetField("dwChannelMask", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(f, 0x3f);
        return f; // esta fixture de seis canales es FL,FR,FC,LFE,BL,BR, no un conteo sin disposicion
    }
}
class SinPausa
{
    const BindingFlags T = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Assembly asm;
    static Type TF, TEnc, TMedida, TCadena, TLevel, TBajo, TLim, TFade, TDeclip, TSp, TPlay, TTrack, TAlbum, TFile, TIA, TNoche, TMono;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static object Get(object o, string campo) { return o.GetType().GetField(campo, T).GetValue(o); }
    static void Set(object o, string campo, object v) { o.GetType().GetField(campo, T).SetValue(o, v); }
    static object Prop(object o, string p) { return o.GetType().GetProperty(p, T).GetValue(o, null); }
    static object Inv(object o, string m, params object[] a)
    {
        foreach (MethodInfo mi in o.GetType().GetMethods(T))
            if (mi.Name == m && mi.GetParameters().Length == a.Length) return mi.Invoke(o, a);
        throw new MissingMethodException(o.GetType().Name, m);
    }
    // El cierre ahora devuelve el lector al dueno de la salida; esta prueba sin salida asume esa propiedad.
    static void SueltaPreparada(object f)
    {
        object lector = Inv(f, "SueltaSinPausa");
        if (lector != null) ((IDisposable)lector).Dispose();
    }
    // Sin 'Static': un tipo con constructor estatico daria dos constructores sin argumentos.
    static object Nuevo(Type t, params object[] a) { return Activator.CreateInstance(t, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, a, null); }

    // --- Encadenado ---------------------------------------------------------------------------------
    static object Enc(ISampleProvider primera, object ficha) { return Nuevo(TEnc, primera, ficha); }
    static bool Prepara(object e, ISampleProvider c, int recorte, object ficha) { return (bool)Inv(e, "Prepara", c, recorte, ficha); }
    static bool Cancela(object e, object ficha) { return (bool)Inv(e, "Cancela", ficha); }
    static object TomaRelevo(object e, int espera) { return Inv(e, "TomaRelevo", Environment.TickCount, espera); }
    static bool TomaFallo(object e, out object ficha, out Exception error)
    {
        object[] args = new object[2];
        bool r = (bool)TEnc.GetMethod("TomaFallo", T).Invoke(e, args);
        ficha = args[0]; error = (Exception)args[1];
        return r;
    }

    static float[] Senal(int n, int ch, int semilla)
    {
        Random r = new Random(semilla);
        float[] x = new float[n * ch];
        for (int i = 0; i < x.Length; i++) x[i] = (float)(r.NextDouble() * 1.6 - 0.8);
        return x;
    }
    static float[] Junta(params float[][] partes)
    {
        int n = 0; foreach (float[] p in partes) n += p.Length;
        float[] r = new float[n]; int k = 0;
        foreach (float[] p in partes) { Array.Copy(p, 0, r, k, p.Length); k += p.Length; }
        return r;
    }
    static bool Igual(float[] a, float[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    // Lee hasta el final con los tamanos de 'bloques' (en muestras). Tras cada lectura llama a 'tras'.
    static float[] LeeTodo(ISampleProvider p, int[] bloques, Action<long> tras, Random azar)
    {
        List<float> r = new List<float>();
        float[] buf = new float[1 << 17];
        int k = 0; long hechas = 0;
        while (true)
        {
            int pide = bloques != null ? bloques[k++ % bloques.Length] : 1 + azar.Next(9000);
            int n = p.Read(buf, 0, pide);
            if (n <= 0) break;
            for (int i = 0; i < n; i++) r.Add(buf[i]);
            hechas += n;
            if (tras != null) tras(hechas);
            if (r.Count > 50000000) break;
        }
        return r.ToArray();
    }

    // ================================================================================================
    //  1) EL MECANISMO
    // ================================================================================================
    static void ElMecanismo()
    {
        Console.WriteLine("\n  1) EL MECANISMO: LA SIGUIENTE ENTRA EN LA MISMA LECTURA\n");
        int fs = 44100;
        int combos = 0; bool todasIguales = true, relevosBien = true, unaVez = true;
        foreach (int ch in new int[] { 1, 2, 6 })
        {
            float[] a = Senal(57331, ch, 1), b = Senal(39007, ch, 2), c = Senal(8101, ch, 3);
            int[][] tamanos = { new int[] { ch }, new int[] { 7 * ch }, new int[] { 441 * ch }, new int[] { 4096 * ch }, null };
            foreach (int[] bl in tamanos)
            {
                Cadena ca = new Cadena(a, fs, ch, 0, -1), cb = new Cadena(b, fs, ch, 175, -1), cc = new Cadena(c, fs, ch, 211, -1);
                object fa = "A", fb = "B", fcc = "C";
                object e = Enc(ca, fa);
                Prepara(e, cb, 175, fb);
                List<object> relevos = new List<object>();
                Random azar = new Random(combos + 7);
                float[] y = LeeTodo((ISampleProvider)e, bl, delegate(long h)
                {
                    object r = TomaRelevo(e, 0);
                    if (r == null) return;
                    relevos.Add(r);
                    if (Get(r, "Ficha") == fb) Prepara(e, cc, 211, fcc);   // como la pantalla: la siguiente, tras recoger
                }, azar);
                combos++;
                if (!Igual(y, Junta(a, b, c))) todasIguales = false;
                if (relevos.Count != 2 || Get(relevos[0], "Ficha") != fb || Get(relevos[1], "Ficha") != fcc
                    || (long)Get(relevos[0], "Cuadro") != a.Length / ch || (long)Get(relevos[1], "Cuadro") != (a.Length + b.Length) / ch)
                    relevosBien = false;
                if (TomaRelevo(e, 0) != null) unaVez = false;
            }
        }
        Check(todasIguales, combos + " combinaciones (1, 2 y 6 canales; lecturas de una muestra, 7, 441, 4096 cuadros y al azar): lo que sale es la primera, la segunda y la tercera enteras, seguidas, sin una muestra de mas ni de menos y sin el cebado de las que entran");
        Check(relevosBien, "cada relevo apunta que cancion entro y en que cuadro de la salida (justo donde acabo la anterior)");
        Check(unaVez, "y se entrega UNA sola vez");

        // Se recoge cuando ya se oye, no antes
        {
            object e = Enc(new Cadena(Senal(100, 2, 4), fs, 2, 0, -1), "A");
            Prepara(e, new Cadena(Senal(1000, 2, 5), fs, 2, 0, -1), 0, "B");   // B sigue sonando tras la lectura
            float[] buf = new float[1000]; ((ISampleProvider)e).Read(buf, 0, 1000);
            bool temprano = TomaRelevo(e, 60000) == null;
            object r = TomaRelevo(e, 0);
            Check(temprano && r != null && (string)Get(r, "Ficha") == "B" && (bool)Prop(e, "Terminado") == false,
                  "el relevo espera a que pase el colchon de la salida (lo que tarda en oirse); forzado, se recoge ya");
        }

        // Formato
        {
            object e = Enc(new Cadena(Senal(10, 2, 1), fs, 2, 0, -1), "A");
            bool otraFrec = !Prepara(e, new Cadena(Senal(10, 2, 1), 48000, 2, 0, -1), 0, "B");
            bool otrosCanales = !Prepara(e, new Cadena(Senal(10, 1, 1), fs, 1, 0, -1), 0, "B");
            bool buena = Prepara(e, new Cadena(Senal(10, 2, 1), fs, 2, 0, -1), 0, "B");
            bool segunda = !Prepara(e, new Cadena(Senal(10, 2, 1), fs, 2, 0, -1), 0, "C");
            Check(otraFrec && otrosCanales, "otra frecuencia u otros canales NO se preparan: esa ira por el camino de siempre");
            Check(buena && segunda, "una preparada a la vez: la segunda espera");
            float[] buf = new float[100]; while (((ISampleProvider)e).Read(buf, 0, 100) > 0) { }
            Check(!Prepara(e, new Cadena(Senal(10, 2, 1), fs, 2, 0, -1), 0, "D") && (bool)Prop(e, "Terminado"),
                  "terminada la secuencia (no habia siguiente), ya no se prepara nada: se cierra la salida como siempre");
        }

        // Retirar
        {
            float[] a = Senal(3000, 2, 8);
            object e = Enc(new Cadena(a, fs, 2, 0, -1), "A");
            Prepara(e, new Cadena(Senal(3000, 2, 9), fs, 2, 0, -1), 0, "B");
            bool aTiempo = Cancela(e, "B");
            float[] y = LeeTodo((ISampleProvider)e, new int[] { 512 }, null, null);
            Check(aTiempo && Igual(y, a) && TomaRelevo(e, 0) == null, "retirada a tiempo: suena la que estaba y se acaba ahi (la siguiente ira por el camino de siempre)");
            object e2 = Enc(new Cadena(Senal(300, 2, 8), fs, 2, 0, -1), "A");
            Prepara(e2, new Cadena(Senal(3000, 2, 9), fs, 2, 0, -1), 0, "B");
            float[] buf = new float[1000]; ((ISampleProvider)e2).Read(buf, 0, 1000);
            Check(!Cancela(e2, "B") && TomaRelevo(e2, 0) != null, "retirada tarde (ya entro): no se retira, suena, y el relevo queda para la pantalla");
        }

        // Fallos
        {
            float[] a = Senal(5000, 2, 10);
            object e = Enc(new Cadena(a, fs, 2, 0, -1), "A");
            Prepara(e, new Cadena(Senal(5000, 2, 11), fs, 2, 175, 0), 175, "B");   // revienta al primer bloque
            float[] y = null; Exception fuera = null;
            try { y = LeeTodo((ISampleProvider)e, new int[] { 1024 }, null, null); } catch (Exception ex) { fuera = ex; }
            object quien; Exception err;
            bool hay = TomaFallo(e, out quien, out err);
            object r = TomaRelevo(e, 0);
            Check(fuera == null && y != null && Igual(y, a), "la siguiente revienta al empezar: la salida NO revienta; sale la anterior entera y se acaba");
            Check(hay && (string)quien == "B" && err is InvalidDataException && r != null && (string)Get(r, "Ficha") == "B",
                  "queda apuntado que fallo la que entraba (y que entro): la pantalla la atiende UNA vez, con un credito devuelto");
            object q2; Exception e2x;
            Check(!TomaFallo(e, out q2, out e2x), "el fallo se entrega una sola vez");

            float[] a2 = Senal(8000, 2, 12);
            object e3 = Enc(new Cadena(a2, fs, 2, 0, 6000), "A");                  // la que suena revienta a media cancion
            Prepara(e3, new Cadena(Senal(100, 2, 13), fs, 2, 0, -1), 0, "B");
            float[] y3 = LeeTodo((ISampleProvider)e3, new int[] { 1000 }, null, null);
            object q3; Exception e3x;
            bool f3 = TomaFallo(e3, out q3, out e3x);
            bool igual6000 = y3.Length == 6000; for (int i = 0; i < 6000 && igual6000; i++) if (y3[i] != a2[i]) igual6000 = false;
            Check(igual6000 && f3 && (string)q3 == "A" && TomaRelevo(e3, 0) == null,
                  "la que suena revienta a media cancion: sale lo que se leyo, se apunta de quien fue y la preparada NO entra sola (la abrira PlayNext)");

            float[] a4 = Senal(4000, 2, 14);
            object e4 = Enc(new Cadena(a4, fs, 2, 0, -1), "A");
            Prepara(e4, new Cadena(Senal(4000, 2, 15), fs, 2, 300, 200), 300, "B");   // revienta en pleno cebado
            float[] y4 = LeeTodo((ISampleProvider)e4, new int[] { 4096 }, null, null);
            object q4; Exception e4x;
            Check(Igual(y4, a4) && TomaFallo(e4, out q4, out e4x) && (string)q4 == "B", "revienta mientras se descarta su cebado: igual, y el fallo es suyo");
        }

        // Vacia y cuadro a medias
        {
            float[] a = Senal(3000, 2, 16), c = Senal(2000, 2, 17);
            object e = Enc(new Cadena(a, fs, 2, 0, -1), "A");
            Prepara(e, new Cadena(new float[0], fs, 2, 0, -1), 0, "VACIA");
            float[] y = LeeTodo((ISampleProvider)e, new int[] { 700 }, null, null);
            object r = TomaRelevo(e, 0);
            Check(Igual(y, a) && r != null && (string)Get(r, "Ficha") == "VACIA" && (bool)Prop(e, "Terminado"),
                  "una siguiente vacia: entra (la pantalla la da por sonada) y la secuencia termina; nada roto");

            float[] impar = Senal(3001, 1, 18);                                    // 1500,5 cuadros estereo: acaba a medias
            object e2 = Enc(new Cadena(impar, fs, 2, 0, -1), "A");
            Prepara(e2, new Cadena(c, fs, 2, 0, -1), 0, "B");
            float[] y2 = LeeTodo((ISampleProvider)e2, new int[] { 1000 }, null, null);
            bool alineado = y2.Length == 3002 + c.Length && y2[3001] == 0f;
            for (int i = 0; i < c.Length && alineado; i++) if (y2[3002 + i] != c[i]) alineado = false;
            Check(alineado, "si la anterior dejara un cuadro a medias, se cierra con silencio: la siguiente empieza en su canal 0 (los canales no se cruzan)");
        }

        // Dos hilos: el del sonido lee sin parar; la pantalla prepara, recoge y a veces retira. Si el sonido
        // llega al final sin nada preparado, la secuencia termina (es lo correcto: iria por el camino de
        // siempre) y se empieza otra; lo que se exige es que cada secuencia saque exactamente lo que entro.
        {
            int ch = 2, fsx = 8000, porSecuencia = 30;
            Random r = new Random(99), rp = new Random(6);
            int entraronTot = 0, retiradasTot = 0, secuencias = 0, semilla = 100;
            bool ok = true;
            while (entraronTot < 150 && secuencias < 300 && ok)
            {
                secuencias++;
                float[][] canciones = new float[porSecuencia][]; int[] cebados = new int[porSecuencia];
                object[] fichas = new object[porSecuencia];   // cada cancion, UNA ficha (se comparan por referencia)
                for (int i = 0; i < porSecuencia; i++) { canciones[i] = Senal(3000 + r.Next(4000), ch, semilla++); cebados[i] = r.Next(300); fichas[i] = i; }
                object e = Enc(new Cadena(canciones[0], fsx, ch, 0, -1), fichas[0]);
                List<float> salida = new List<float>();
                Exception[] enHilo = new Exception[1];
                Thread sonido = new Thread(delegate()
                {
                    try
                    {
                        float[] buf = new float[4096]; Random rr = new Random(5);
                        while (true)
                        {
                            int k = ((ISampleProvider)e).Read(buf, 0, 2 * (1 + rr.Next(256)));
                            if (k <= 0) break;
                            lock (salida) for (int i = 0; i < k; i++) salida.Add(buf[i]);
                            if (rr.Next(8) == 0) Thread.Sleep(0);
                        }
                    }
                    catch (Exception ex) { enHilo[0] = ex; }
                });
                List<int> entraron = new List<int>(); entraron.Add(0);
                List<int> retiradas = new List<int>();
                int siguiente = 1, preparada = -1;
                sonido.Start();
                while (sonido.IsAlive)
                {
                    object rel = TomaRelevo(e, 0);
                    if (rel != null) { entraron.Add((int)Get(rel, "Ficha")); if ((int)Get(rel, "Ficha") == preparada) preparada = -1; }
                    if (preparada < 0 && siguiente < porSecuencia && !(bool)Prop(e, "HayRelevoPendiente"))
                    {
                        if (Prepara(e, new Cadena(canciones[siguiente], fsx, ch, cebados[siguiente], -1), cebados[siguiente], fichas[siguiente])) { preparada = siguiente; siguiente++; }
                    }
                    else if (preparada >= 0 && rp.Next(4) == 0)
                    {
                        if (Cancela(e, fichas[preparada])) { retiradas.Add(preparada); preparada = -1; }
                    }
                }
                sonido.Join();
                object ult = TomaRelevo(e, 0);
                if (ult != null) entraron.Add((int)Get(ult, "Ficha"));
                List<float> esperado = new List<float>();
                foreach (int i in entraron) esperado.AddRange(canciones[i]);
                bool bien = enHilo[0] == null && salida.Count == esperado.Count;
                for (int i = 0; bien && i < salida.Count; i++) if (salida[i] != esperado[i]) bien = false;
                foreach (int i in retiradas) if (entraron.Contains(i)) bien = false;
                for (int i = 1; i < entraron.Count; i++) if (entraron[i] <= entraron[i - 1]) bien = false;   // en orden, ninguna dos veces
                if (!bien) ok = false;
                entraronTot += entraron.Count; retiradasTot += retiradas.Count;
            }
            Check(ok && entraronTot >= 150 && retiradasTot > 0,
                  "dos hilos a la vez (" + secuencias + " secuencias, " + entraronTot + " canciones entraron, " + retiradasTot + " retiradas a tiempo): cada secuencia saca exactamente lo que entro, en orden; nada de lo retirado; ni una muestra perdida ni repetida");
        }

        // Leer no reserva memoria
        bool sinReservas = true;
        foreach (string m in new string[] { "Read", "Releva", "Falla" })
            foreach (Codigo.Paso p in Codigo.Pasos(TEnc.GetMethod(m, T))) if (p.Op == 0x8D || p.Op == 0x73) sinReservas = false;
        Check(sinReservas, "leer y pasar a la siguiente no reservan memoria (se reserva al preparar)");
    }

    // ================================================================================================
    //  2) LAS CADENAS DE VERDAD
    // ================================================================================================
    static float[] Musica(int fs, int ch, double seg, int semilla)
    {
        long n = (long)(seg * fs); float[] x = new float[n * ch]; Random r = new Random(semilla);
        double[] w = new double[40], fase = new double[40];
        for (int k = 0; k < 40; k++) { w[k] = 2 * Math.PI * (300 + r.NextDouble() * 14000) / fs; fase[k] = r.NextDouble() * 6; }
        for (long i = 0; i < n; i++)
        {
            double v = 0.3 * Math.Sin(2 * Math.PI * 55 * i / fs) + 0.15 * Math.Sin(2 * Math.PI * 440 * i / fs);
            for (int k = 0; k < 40; k++) v += 0.006 * Math.Sin(w[k] * i + fase[k]);
            if ((i % (fs / 2)) < fs / 50) v *= 1.8;   // golpes que el limitador tiene que atender
            for (int c = 0; c < ch; c++) x[i * ch + c] = (float)(c == 1 ? -v * 0.9 : v);
        }
        return x;
    }

    // La misma composicion que ArmaCadena: [des-recorte] -> [DSEE] -> nivelado con realce -> compensacion de noche (de dia)
    // -> grave en mono -> grave sintetico -> limitador -> fundido.
    static ISampleProvider Cadena(object form, float[] x, int fs, int ch, bool completa, out int retardo, out object fundido)
    { return Cadena(form, x, fs, ch, completa, completa, out retardo, out fundido); }
    static ISampleProvider Cadena(object form, float[] x, int fs, int ch, bool completa, bool dsee, out int retardo, out object fundido)
    {
        ISampleProvider s = new FuenteSP(x, fs, ch);
        retardo = 0;
        if (completa)
        {
            object dc = Nuevo(TDeclip, s); retardo += (int)Prop(dc, "DelayFrames"); s = (ISampleProvider)dc;
            ConstructorInfo c2 = TSp.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(ISampleProvider), typeof(int) }, null);
            object sp = c2 != null ? c2.Invoke(new object[] { s, 2048 }) : Nuevo(TSp, s);
            Set(sp, "Active", dsee);
            s = (ISampleProvider)sp;
        }
        object tap = Nuevo(TLevel, s, form);
        Set(tap, "Excite", completa ? 0.3f : 0f);
        Set(tap, "Gain", 1.3f);
        retardo += (int)Prop(tap, "DelayFrames");
        object noche = Nuevo(TNoche, tap);
        object mono = Nuevo(TMono, noche, 120f);
        object bajo = Nuevo(TBajo, mono, completa ? 0.5f : 0f);
        object lim = Nuevo(TLim, bajo, 0.98f, 3.0);
        retardo += (int)Prop(lim, "DelayFrames");
        long total = x.Length / ch + retardo;
        fundido = Nuevo(TFade, lim, total, 0.5);
        return (ISampleProvider)fundido;
    }

    static void LasCadenas(object form)
    {
        Console.WriteLine("\n  2) LAS CADENAS DE VERDAD: CEBADO, COLA Y RELEVO\n");
        foreach (int fs in new int[] { 44100, 48000 })
            foreach (bool completa in new bool[] { true, false })
            {
                int ch = 2;
                float[] a = Musica(fs, ch, 2.2, fs + 1), b = Musica(fs, ch, 1.7, fs + 2);
                int ra, rb; object fa, fb;
                float[] soloA = LeeTodo(Cadena(form, a, fs, ch, completa, out ra, out fa), new int[] { 4096 }, null, null);
                float[] soloB = LeeTodo(Cadena(form, b, fs, ch, completa, out rb, out fb), new int[] { 4096 }, null, null);
                string nom = (completa ? "cadena completa" : "cadena minima") + " a " + fs + " Hz";
                // El cebado, sin el DSEE: silencio (el realce 2x deja a lo sumo su pre-oscilacion, unos 2e-6, -114
                // dBFS), y la musica empieza JUSTO en el cuadro que dicen los retrasos: ni antes (se cortaria) ni despues.
                int rs; object fs0;
                float[] sinDsee = LeeTodo(Cadena(form, b, fs, ch, completa, false, out rs, out fs0), new int[] { 4096 }, null, null);
                double cebado = 0; for (int i = 0; i < rs * ch; i++) cebado = Math.Max(cebado, Math.Abs(sinDsee[i]));
                int primera = -1; for (int i = 0; i < sinDsee.Length && primera < 0; i++) if (Math.Abs(sinDsee[i]) > 1e-5) primera = i / ch;
                Check(soloA.Length == a.Length + ra * ch && soloB.Length == b.Length + rb * ch && rs == rb && cebado <= 1e-5 && primera == rb,
                      nom + ": el cebado dura justo lo que suman sus retrasos (" + rb + " cuadros, " + F(1000.0 * rb / fs, "0.0") + " ms), es silencio (" + F(cebado, "0.0e0") + ") y la musica empieza en el cuadro " + primera);
                if (completa)
                {
                    // Con el DSEE detras del des-recorte: la banda que recrea se adelanta un poco (su ventana) sobre
                    // el silencio que mete el des-recorte. Es lo unico que se tira al descartar el cebado.
                    double eco = 0; int desde = -1;
                    for (int i = 0; i < rb * ch; i++) { double v = Math.Abs(soloB[i]); if (v > eco) eco = v; if (v > 1e-6 && desde < 0) desde = i / ch; }
                    Check(eco <= 1e-3, nom + ", con el DSEE: en el cebado solo queda el eco previo de la banda recreada (" + F(20 * Math.Log10(Math.Max(eco, 1e-12)), "0") + " dBFS, "
                          + (desde < 0 ? "ninguno" : F(1000.0 * (rb - desde) / fs, "0.0") + " ms antes") + "); es lo unico que se descarta, y sin des-recorte no existe");
                }
                int rb2; object fa2, fb2; int ra2;
                ISampleProvider ca = Cadena(form, a, fs, ch, completa, out ra2, out fa2);
                ISampleProvider cb = Cadena(form, b, fs, ch, completa, out rb2, out fb2);
                object e = Enc(ca, "A");
                Prepara(e, cb, rb2, "B");
                float[] y = LeeTodo((ISampleProvider)e, new int[] { 2205 * ch, 999 * ch, 4096 * ch }, null, null);
                float[] esperado = new float[soloA.Length + soloB.Length - rb * ch];
                Array.Copy(soloA, esperado, soloA.Length);
                Array.Copy(soloB, rb * ch, esperado, soloA.Length, soloB.Length - rb * ch);
                Check(Igual(y, esperado), nom + ": encadenadas suenan como cada una sola, bit a bit: la primera con su cola entera y la segunda justo detras, sin su cebado");
            }

        // El fundido se quita a tiempo o se termina
        {
            int fs = 44100, ch = 2; float[] x = new float[fs * 3 * ch]; for (int i = 0; i < x.Length; i++) x[i] = 0.5f;
            object f1 = Nuevo(TFade, new FuenteSP(x, fs, ch), (long)(fs * 3), 0.5);
            bool quito = (bool)Inv(f1, "Quita");
            float[] y1 = LeeTodo((ISampleProvider)f1, new int[] { 4096 }, null, null);
            Check(quito && Igual(y1, x), "pista siguiente del mismo disco, quitado a tiempo: la cancion acaba entera, sin fundido (el disco sigue continuo)");
            object f2 = Nuevo(TFade, new FuenteSP(x, fs, ch), (long)(fs * 3), 0.5);
            float[] buf = new float[(int)(fs * 2.7) * ch]; ((ISampleProvider)f2).Read(buf, 0, buf.Length);   // ya entro al fundido
            bool tarde = !(bool)Inv(f2, "Quita");
            float[] resto = LeeTodo((ISampleProvider)f2, new int[] { 4096 }, null, null);
            Check(tarde && resto.Length > 0 && Math.Abs(resto[resto.Length - 1]) < 0.01f && resto[0] < 0.5f,
                  "si el fundido ya empezo, no se quita a media bajada: se termina (sin saltos de volumen)");
        }
    }

    // ================================================================================================
    //  3) LA ROCOLA
    // ================================================================================================
    static string tmp;
    static string Wav(string nombre, int fs, int ch, double seg, int semilla)
    {
        string p = Path.Combine(tmp, nombre);
        using (WaveFileWriter w = new WaveFileWriter(p, WaveFormat.CreateIeeeFloatWaveFormat(fs, ch)))
        {
            float[] x = Musica(fs, ch, seg, semilla);
            w.WriteSamples(x, 0, x.Length);
        }
        return p;
    }
    static object Lector(string ruta)
    {
        object l = Nuevo(TFile, ruta);
        TIA.GetProperty("Volume").SetValue(l, 1.0f, null);   // FixedVolume, como StartOpenedTrack y el relevo
        return l;
    }
    // El calentador (MiraPorDelante) con la siguiente ya abierta, como la deja cuando termina de abrirla.
    static object MiraCon(string ruta, object lector)
    {
        object mira = Activator.CreateInstance(asm.GetType("Rocola.MiraPorDelante`1").MakeGenericType(TIA), true);
        Set(mira, "para", ruta); Set(mira, "firma", ""); Set(mira, "listo", lector);
        return mira;
    }
    static StoppedEventArgs AlParar(object f, Exception deLaSalida)
    { return (StoppedEventArgs)Inv(f, "RecogeAlParar", new StoppedEventArgs(deLaSalida)); }
    // Suena A, B esta preparada y C espera; 5 creditos. 'falla': quien revienta al leer ("A", "B" o null).
    // 'leer': el hilo del sonido lee la secuencia hasta el final (B entra, salvo que A reviente antes).
    static object Escena(object f, object disco, string[] rutas, string falla, bool leer, out object iA, out object iB, out object iC, out object lB)
    {
        IList q = (IList)Get(f, "queue"); q.Clear();
        object pA = Pista(rutas[0]), pB = Pista(rutas[1]), pC = Pista(rutas[2]);
        iA = Item(pA, disco); iB = Item(pB, disco); iC = Item(pC, disco);
        q.Add(iB); q.Add(iC);
        Set(f, "nowPlaying", iA); Set(f, "credits", 5);
        object lA = Lector(rutas[0]); lB = Lector(rutas[1]);
        Set(f, "reader", lA);
        object cadA = CadenaDe(pA, iA, lA, Tap(f, lA, null)), cadB = CadenaDe(pB, iB, lB, Tap(f, lB, Nuevo(TMedida, null, null)));
        Set(f, "cadenaSonando", cadA); Set(f, "cadenaPreparada", cadB);
        object e = Enc(new Cadena(Senal(3000, 2, 41), 44100, 2, 0, falla == "A" ? 2000 : -1), cadA);
        Prepara(e, new Cadena(Senal(3000, 2, 42), 44100, 2, 0, falla == "B" ? 1000 : -1), 0, cadB);
        Set(f, "secuencia", e);
        if (leer) LeeTodo((ISampleProvider)e, new int[] { 1024 }, null, null);
        return e;
    }
    static bool Cola(object f, params object[] items)
    {
        IList q = (IList)Get(f, "queue");
        if (q.Count != items.Length) return false;
        for (int i = 0; i < items.Length; i++) if (!object.ReferenceEquals(q[i], items[i])) return false;
        return true;
    }
    static bool Suelto(object lector)
    {
        try { ((ISampleProvider)lector).Read(new float[64], 0, 64); return false; } catch { return true; }
    }
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
    static object Tap(object form, object lector, object medida)
    {
        object tap = Nuevo(TLevel, (ISampleProvider)lector, form);
        if (medida != null) Set(tap, "Medida", medida);
        return tap;
    }
    static object CadenaDe(object pista, object item, object lector, object tap)
    {
        object c = Nuevo(TCadena);
        Set(c, "Cancion", pista); Set(c, "Item", item); Set(c, "Lector", lector); Set(c, "Tap", tap);
        Set(c, "Salida", tap); Set(c, "BajoFactor", 1f); Set(c, "BajoBase", 0.3f);
        return c;
    }

    // Las colas de guardado en segundo plano nacen junto a su declaracion: un Form1 sin encender las tiene
    // en nulo (FormSinEncender solo pone cerrojos y colecciones). Se les pone lo que les pondria el constructor.
    static void PonGuardados(object f)
    {
        foreach (FieldInfo fi in TF.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null)
                fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
    }

    // El primer Read de la nueva puede terminar justo cuando UI recoge el relevo.
    // El cerrojo representa una publicacion en curso: el cierre debe esperar y ver
    // su CierreAnterior completo, no entrar en el respaldo que reinicia los campos.
    static void RelevoYPrimerBloque(string ra, string rb)
    {
        object f = Codigo.FormSinEncender(TF); PonGuardados(f);
        Set(f, "startupReadPending", true);
        Set(f, "stateRestored", true); // fixture independiente: no restaurar la cola de otra escena
        int fs = 44100, ch = 2; long muestras = 20L * fs * ch;
        object disco = Nuevo(TAlbum), pA = Pista(ra), pB = Pista(rb);
        object iA = Item(pA, disco), iB = Item(pB, disco);
        object lA = Lector(ra), lB = Lector(rb), mb = Nuevo(TMedida, rb, rb);
        object tapB = Tap(f, lB, mb), cadB = CadenaDe(pB, iB, lB, tapB);
        Set(f, "reader", lA); Set(f, "nowPlaying", iA);
        Set(f, "cadenaSonando", CadenaDe(pA, iA, lA, Tap(f, lA, null)));
        Set(f, "cadenaPreparada", cadB); ((IList)Get(f, "queue")).Add(iB);
        Set(f, "curMeasurePath", ra); Set(f, "curSumSq", muestras * .01); Set(f, "curSamples", muestras);
        Set(f, "kwRate", fs); Set(f, "kwCanales", ch);
        Set(f, "eqMeasPath", ra); Set(f, "eqMeasSum", new double[8]); Set(f, "eqMeasN", 0L);
        object puerta = Get(f, "audioDatosLock"); Exception fallo = null;
        using (ManualResetEvent empezo = new ManualResetEvent(false))
        using (ManualResetEvent termino = new ManualResetEvent(false))
        {
            Thread ui = new Thread(delegate()
            {
                empezo.Set();
                try { Inv(f, "PasaRelevo", cadB); }
                catch (Exception ex) { fallo = ex; }
                finally { termino.Set(); }
            });
            ui.IsBackground = true;
            bool espera, intacta;
            Monitor.Enter(puerta);
            try
            {
                ui.Start(); empezo.WaitOne();
                espera = !termino.WaitOne(200, false);
                intacta = object.ReferenceEquals(Get(f, "reader"), lA) && (long)Get(f, "curSamples") == muestras;
                float[] bloque = new float[512]; for (int i = 0; i < bloque.Length; i++) bloque[i] = .2f;
                Inv(f, "PublicaBloqueDe", tapB, bloque, 0, bloque.Length, fs, ch);
            }
            finally { Monitor.Exit(puerta); }
            bool acabo = ui.Join(5000);
            Check(espera && intacta, "recoger el relevo espera la publicacion en curso; no reinicia ni cierra su medida a medias");
            Check(acabo && fallo == null && (string)Get(f, "curMeasurePath") == rb && (long)Get(f, "curSamples") == 512L
                  && object.ReferenceEquals(Get(f, "tapMedido"), tapB),
                  "el relevo conserva las 512 muestras del primer bloque de la nueva, sin perderlas ni mezclarlas");
            IDictionary loud = (IDictionary)Get(f, "loudCache");
            Check(acabo && loud.Contains(ra) && Math.Abs((float)loud[ra] - .1f) < 1e-4 && !(bool)Get(Get(mb, "CierreAnterior"), "Listo")
                  && Suelto(lA) && !Suelto(lB),
                  "la anterior se guarda una sola vez y su lector se cierra fuera del cerrojo; la nueva sigue abierta");
        }
        Codigo.EsperaGuardado(TF, f);
        ((IDisposable)lA).Dispose(); ((IDisposable)lB).Dispose();
    }

    static void LaRocola()
    {
        Console.WriteLine("\n  3) LA ROCOLA: LA MEDIDA, LA COLA Y LO QUE SE SUELTA\n");
        object f = Codigo.FormSinEncender(TF);
        PonGuardados(f);
        int fs = 44100, ch = 2;
        string ra = Wav("a.wav", fs, ch, 1.0, 21), rb = Wav("b.wav", fs, ch, 1.0, 22), rc = Wav("c.wav", fs, ch, 1.0, 23);

        // a) LA MEDIDA EN VIVO pasa a la nueva en SU primer bloque
        {
            object ma = Nuevo(TMedida, "a.mp3", "a.mp3"), mb = Nuevo(TMedida, "b.mp3", "b.mp3");
            object tapA = Tap(f, Lector(ra), ma), tapB = Tap(f, Lector(rb), mb), tapSuelto = Tap(f, Lector(rc), null);
            Set(f, "curMeasurePath", "a.mp3"); Set(f, "eqMeasPath", "a.mp3"); Set(f, "curSumSq", 0.0); Set(f, "curSamples", 0L);
            Set(f, "eqMeasSum", new double[8]); Set(f, "eqMeasN", 0L); Set(f, "kwRate", 0); Set(f, "eqMeasRate", 0);
            Set(f, "tapMedido", tapA);
            float[] buf = new float[4410 * ch];
            for (int i = 0; i < 5; i++) ((ISampleProvider)tapA).Read(buf, 0, buf.Length);
            double sumA = (double)Get(f, "curSumSq"); long nA = (long)Get(f, "curSamples"); long eqA = (long)Get(f, "eqMeasN");
            double[] sumasA = (double[])Get(f, "eqMeasSum");
            object cierre = Get(mb, "CierreAnterior");
            bool antes = !(bool)Get(cierre, "Listo");
            ((ISampleProvider)tapB).Read(buf, 0, buf.Length);   // primer bloque de la nueva
            bool guardo = (bool)Get(cierre, "Listo") && (string)Get(cierre, "Ruta") == "a.mp3" && (double)Get(cierre, "SumSq") == sumA
                          && (long)Get(cierre, "Muestras") == nA && (long)Get(cierre, "EqN") == eqA && object.ReferenceEquals(Get(cierre, "EqSumas"), sumasA);
            bool estreno = (string)Get(f, "curMeasurePath") == "b.mp3" && (long)Get(f, "curSamples") == buf.Length
                           && object.ReferenceEquals(Get(f, "eqMeasSum"), Get(mb, "EqSumas")) && object.ReferenceEquals(Get(f, "tapMedido"), tapB);
            Check(antes && guardo, "en el primer bloque de la que entra, lo medido de la anterior se guarda tal cual (" + nA + " muestras), sin mezclarse");
            Check(estreno, "y la medida en vivo pasa a ser de la nueva desde ese bloque (su sonoridad empieza de cero, con su memoria ya reservada)");
            ((ISampleProvider)tapSuelto).Read(buf, 0, buf.Length);
            Check(object.ReferenceEquals(Get(f, "tapMedido"), tapB), "un LevelTap sin medida propia (como los de las pruebas) mide como siempre: no cambia de dueno");
        }

        // b) EL RELEVO pasa la cola como PlayNext
        {
            Environment.SetEnvironmentVariable("ROCOLA_DATOS", tmp);
            IList q = (IList)Get(f, "queue"); q.Clear();
            object pA = Pista(ra), pB = Pista(rb), pC = Pista(rc);
            object disco = Nuevo(TAlbum); Set(disco, "Folder", tmp); Set(disco, "Name", "Disco"); Set(disco, "Artist", "Prueba");
            IList pistas = (IList)Get(disco, "Tracks"); pistas.Add(pA); pistas.Add(pB); pistas.Add(pC);
            object iA = Item(pA, disco), iB = Item(pB, disco), iC = Item(pC, disco);
            q.Add(iB); q.Add(iC);
            Set(f, "nowPlaying", iA); Set(f, "credits", 5); Set(f, "playFailStreak", 2);
            object lA = Lector(ra), lB = Lector(rb);
            Set(f, "reader", lA);
            object mb = Nuevo(TMedida, rb, rb);
            object tapB = Tap(f, lB, mb);
            object cadA = CadenaDe(pA, iA, lA, Tap(f, lA, null)), cadB = CadenaDe(pB, iB, lB, tapB);
            Set(f, "cadenaSonando", cadA); Set(f, "cadenaPreparada", cadB);
            // Lo que el hilo del sonido guardo de la A: 20 s oidos, de sobra para aprender su sonoridad.
            object cierre = Get(mb, "CierreAnterior");
            long muestras = 20L * fs * ch;
            Set(cierre, "Ruta", ra); Set(cierre, "SumSq", muestras * 0.01); Set(cierre, "Muestras", muestras);
            Set(cierre, "Tasa", fs); Set(cierre, "Canales", ch); Set(cierre, "Listo", true);
            IDictionary loud = (IDictionary)Get(f, "loudCache");
            loud.Remove(ra);
            Inv(f, "PasaRelevo", cadB);
            Check(object.ReferenceEquals(Get(f, "nowPlaying"), iB) && q.Count == 1 && object.ReferenceEquals(q[0], iC),
                  "la que entra sale de la cola y pasa a sonar; la que venia detras queda primera (como PlayNext)");
            Check((int)Get(f, "credits") == 5, "los creditos no se tocan: se cobraron al encolar (ni uno de mas ni de menos)");
            Check(object.ReferenceEquals(Get(f, "reader"), lB) && Suelto(lA) && !Suelto(lB),
                  "la barra de progreso sigue a la nueva y el lector de la anterior se suelta (el archivo queda libre)");
            Check(object.ReferenceEquals(Get(f, "curTap"), tapB) && (int)Get(f, "playFailStreak") == 0,
                  "los controles en vivo de Ctrl+F3 pasan a la nueva, y la anterior cuenta como bien terminada");
            Check(loud.Contains(ra) && Math.Abs((float)loud[ra] - 0.1f) < 1e-4 && !(bool)Get(cierre, "Listo"),
                  "la sonoridad oida de la anterior se guarda (" + (loud.Contains(ra) ? F((float)loud[ra], "0.000") : "nada") + ") una sola vez");
            Codigo.EsperaGuardado(TF, f);
            string estado = File.Exists(Path.Combine(tmp, "rocola_estado.txt")) ? File.ReadAllText(Path.Combine(tmp, "rocola_estado.txt")) : "";
            Check(estado.Contains("sonando=S|" + rb) && estado.Contains("cola=S|" + rc) && !estado.Contains("cola=S|" + rb) && estado.Contains("creditos=5"),
                  "el estado guardado para un corte de luz dice lo que de verdad suena y la cola que queda");

            // c) Al parar (saltar antes de que se oyera, cerrar): la preparada se suelta y la medida pendiente se cierra
            object iD = Item(Pista(rc), disco); q.Add(iD);
            object lD = Lector(rc);
            object md = Nuevo(TMedida, rc, rc);
            object cadD = CadenaDe(Get(iD, "Song"), iD, lD, Tap(f, lD, md));
            object cierreD = Get(md, "CierreAnterior");
            Set(cierreD, "Ruta", rb); Set(cierreD, "SumSq", muestras * 0.04); Set(cierreD, "Muestras", muestras);
            Set(cierreD, "Tasa", fs); Set(cierreD, "Canales", ch); Set(cierreD, "Listo", true);
            Set(f, "cadenaPreparada", cadD);
            loud.Remove(rb);
            SueltaPreparada(f);
            Check(Suelto(lD) && Get(f, "cadenaPreparada") == null && Get(f, "secuencia") == null && loud.Contains(rb),
                  "al parar la salida, la siguiente preparada se suelta, y si ya habia entrado sin oirse, se guarda lo medido de la anterior");
            Check(q.Count == 2 && object.ReferenceEquals(q[1], iD), "y la cola no se toca: PlayNext la abrira desde el principio por el camino de siempre");
        }

        // d) Pista siguiente del MISMO disco
        {
            MethodInfo sd = TF.GetMethod("SiguienteDelDisco", T);
            object d1 = Nuevo(TAlbum), d2 = Nuevo(TAlbum), top = Nuevo(TAlbum);
            Set(top, "IsTop", true);
            object t1 = Pista("1.mp3"), t2 = Pista("2.mp3"), t3 = Pista("3.mp3");
            foreach (object d in new object[] { d1, top }) { IList l = (IList)Get(d, "Tracks"); l.Add(t1); l.Add(t2); l.Add(t3); }
            ((IList)Get(d2, "Tracks")).Add(t2);
            object video = Item(t2, d1); Set(video, "IsVideo", true);
            bool s12 = (bool)sd.Invoke(null, new object[] { Item(t1, d1), Item(t2, d1) });
            bool s13 = (bool)sd.Invoke(null, new object[] { Item(t1, d1), Item(t3, d1) });
            bool s21 = (bool)sd.Invoke(null, new object[] { Item(t2, d1), Item(t1, d1) });
            bool otro = (bool)sd.Invoke(null, new object[] { Item(t1, d1), Item(t2, d2) });
            bool enTop = (bool)sd.Invoke(null, new object[] { Item(t1, top), Item(t2, top) });
            bool vid = (bool)sd.Invoke(null, new object[] { Item(t1, d1), video });
            Check(s12 && !s13 && !s21 && !otro && !enTop && !vid,
                  "solo la pista que SIGUE en el mismo disco quita el fundido; saltandose una, al reves, de otro disco, en un TOP o un video, fundido de siempre");
        }

        // e) El lector preparado solo se MIRA (formato) antes de llevarselo
        {
            Type tm = asm.GetType("Rocola.MiraPorDelante`1").MakeGenericType(typeof(Abierto));
            object mira = Activator.CreateInstance(tm, true);
            Abierto ab = new Abierto(); ab.Ruta = "x.mp3";
            Func<Abierto> abre = delegate { return ab; };
            Action<Action> post = delegate(Action a) { a(); };
            Inv(mira, "Pide", "x.mp3", abre, post);
            for (int i = 0; i < 200 && !(bool)Prop(mira, "Listo"); i++) Thread.Sleep(10);
            Func<Abierto, bool> si = delegate(Abierto a) { return true; }, no = delegate(Abierto a) { return false; };
            bool mira1 = (bool)Inv(mira, "Cumple", "x.mp3", si), mira2 = !(bool)Inv(mira, "Cumple", "y.mp3", si), mira3 = !(bool)Inv(mira, "Cumple", "x.mp3", no);
            bool sigue = (bool)Prop(mira, "Listo") && ab.Sueltas == 0;
            object tomado = Inv(mira, "Toma", "x.mp3");
            Check(mira1 && mira2 && mira3 && sigue && object.ReferenceEquals(tomado, ab) && !(bool)Inv(mira, "Cumple", "x.mp3", si),
                  "mirar el lector ya abierto de la siguiente no lo toma ni lo suelta: si no sirve (otro formato), queda para el camino de siempre");
        }

        // f) HD: el reconstructor y el realce de la siguiente, con SU estado de Apollo
        MethodInfo dp = TF.GetMethod("DseeActivoPara", T);
        if (dp == null) Info("(Modest: sin IA en vivo, el reconstructor lo decide el interruptor para todas)");
        else
        {
            bool igual = true;
            foreach (bool permitido in new bool[] { false, true })
                foreach (bool vivo in new bool[] { false, true })
                    foreach (bool on in new bool[] { false, true })
                        foreach (bool mejorada in new bool[] { false, true })
                        {
                            Set(f, "sbrAllowed", permitido); Set(f, "iaVivo", vivo); Set(f, "sbrOn", on); Set(f, "iaNowEnhanced", mejorada);
                            Inv(f, "RevisaRespaldoDsee");
                            if ((bool)Inv(f, "DseeActivo") != (bool)dp.Invoke(f, new object[] { mejorada })) igual = false;
                        }
            Check(igual, "HD: el reconstructor de la siguiente se decide con SU version de Apollo; con la que suena da lo mismo que antes (16 casos)");
            Set(f, "sbrAllowed", false); Set(f, "iaVivo", false); Set(f, "sbrOn", false); Set(f, "iaNowEnhanced", false);
        }

        // g) AL PARAR LA SALIDA (RecogeAlParar): que se da por sonado y que aviso se atiende. Cada caso con
        //    B ya preparada y C detras; los creditos los devuelve despues Output_PlaybackStopped, una vez por aviso.
        {
            object disco = Nuevo(TAlbum); Set(disco, "Folder", tmp); Set(disco, "Name", "Disco"); Set(disco, "Artist", "Prueba");
            string[] rutas = { ra, rb, rc };
            object iA, iB, iC, lB; StoppedEventArgs r;

            Escena(f, disco, rutas, null, true, out iA, out iB, out iC, out lB);
            r = AlParar(f, null);
            Check(r.Exception == null && object.ReferenceEquals(Get(f, "nowPlaying"), iB) && Cola(f, iC) && (int)Get(f, "credits") == 5,
                  "acabo bien con B ya dentro (B corta, o la ultima): B cuenta como sonada y sale de la cola; C queda para PlayNext; sin aviso ni creditos");

            Escena(f, disco, rutas, null, true, out iA, out iB, out iC, out lB);
            r = AlParar(f, new IOException("DAC desenchufado"));
            bool sinDar = r.Exception is IOException && object.ReferenceEquals(Get(f, "nowPlaying"), iA) && Cola(f, iB, iC) && (int)Get(f, "credits") == 5;
            SueltaPreparada(f);
            Check(sinDar && Suelto(lB) && Get(f, "cadenaPreparada") == null,
                  "falla la SALIDA con B dentro pero sin oirse: B NO se da por sonada; sigue primera en la cola y PlayNext la empieza desde el principio (como antes)");

            Escena(f, disco, rutas, null, false, out iA, out iB, out iC, out lB);
            r = AlParar(f, new IOException("DAC desenchufado"));
            Check(r.Exception is IOException && object.ReferenceEquals(Get(f, "nowPlaying"), iA) && Cola(f, iB, iC),
                  "falla la salida antes de que B entre: el aviso es de A, como siempre; la cola intacta");
            SueltaPreparada(f);

            Escena(f, disco, rutas, "B", true, out iA, out iB, out iC, out lB);
            r = AlParar(f, null);
            bool deB = r.Exception is InvalidDataException && object.ReferenceEquals(Get(f, "nowPlaying"), iB) && Cola(f, iC) && (int)Get(f, "credits") == 5;
            StoppedEventArgs otra = AlParar(f, null);
            Check(deB && otra.Exception == null && object.ReferenceEquals(Get(f, "nowPlaying"), iB) && Cola(f, iC),
                  "B revienta al leer despues de entrar: el fallo es de B (se le devuelve SU credito, una vez); C sigue en la cola; B no se repite ni se salta sin aviso");

            Escena(f, disco, rutas, "A", true, out iA, out iB, out iC, out lB);
            r = AlParar(f, null);
            Check(r.Exception is InvalidDataException && object.ReferenceEquals(Get(f, "nowPlaying"), iA) && Cola(f, iB, iC),
                  "A revienta a media cancion: el fallo es de A; B no llego a entrar y PlayNext la abrira desde el principio");
            SueltaPreparada(f);
        }

        // h) DE PUNTA A PUNTA, con la ArmaCadena de verdad (des-recorte, DSEE, nivelado con realce, grave,
        //    limitador y fundido): PreparaSiguienteSinPausa, la lectura y el relevo.
        {
            Set(f, "crossfadeSec", 1.5f); Set(f, "declipOn", true); Set(f, "normalizeOn", true);
            Set(f, "sbrAllowed", true); Set(f, "sbrOn", true);
            Set(f, "videoOn", false); Set(f, "audioOpeningForMeasurement", false);
            Set(f, "output", new SalidaFalsa());
            string pa = Wav("pa.wav", fs, ch, 4.0, 31), pb = Wav("pb.wav", fs, ch, 3.0, 32), pc = Wav("pc.wav", 48000, ch, 3.0, 33);
            string larga = Wav("larga.wav", fs, ch, 13.0, 34);
            object tA = Pista(pa), tB = Pista(pb), tC = Pista(pc), tL = Pista(larga);
            object disco = Nuevo(TAlbum), otro = Nuevo(TAlbum);
            IList pistas = (IList)Get(disco, "Tracks"); pistas.Add(tA); pistas.Add(tB); pistas.Add(tC);
            ((IList)Get(otro, "Tracks")).Add(tL);
            IList q = (IList)Get(f, "queue");

            // Suena 'tp' (del disco 'dp'); en la cola, 'siguientes'; el calentador tiene abierta 'abierta'.
            object[] sonando = new object[3];   // cadena, secuencia, lector del calentador
            Action<object, object, object[], string> monta = delegate(object tp, object dq, object[] siguientes, string abierta)
            {
                q.Clear(); foreach (object it in siguientes) q.Add(it);
                object it0 = Item(tp, dq);
                Set(f, "nowPlaying", it0);
                object l = Lector((string)Get(tp, "Path"));
                object cad = Inv(f, "ArmaCadena", tp, l, false, 0.0);
                Set(cad, "Item", it0);
                Set(f, "reader", l); Set(f, "cadenaSonando", cad); Set(f, "cadenaPreparada", null);
                object sec = Enc((ISampleProvider)Get(cad, "Salida"), cad);
                Set(f, "secuencia", sec);
                Inv(f, "InstalaMedida", cad);   // la medida en vivo es de la que suena, como la deja StartOpenedTrack
                object la = abierta != null ? Lector(abierta) : null;
                Set(f, "lookahead", abierta != null ? MiraCon(abierta, la) : null);
                sonando[0] = cad; sonando[1] = sec; sonando[2] = la;
            };

            object iB = Item(tB, disco), iC = Item(tC, disco);
            monta(tA, disco, new object[] { iB, iC }, pb);
            object cadA = sonando[0], sec1 = sonando[1], lB = sonando[2];
            Inv(f, "PreparaSiguienteSinPausa");
            object cadB = Get(f, "cadenaPreparada");
            bool preparo = cadB != null && object.ReferenceEquals(Get(cadB, "Item"), iB) && object.ReferenceEquals(Get(cadB, "Lector"), lB)
                           && (bool)Prop(sec1, "HayPreparada") && !(bool)Prop(Get(f, "lookahead"), "Listo") && Cola(f, iB, iC);
            bool quitado = Get(cadA, "Fundido") != null && (int)Get(Get(cadA, "Fundido"), "estadoFundido") == 1;
            Check(preparo && quitado, "a 4 s del final, la siguiente del mismo disco se prepara con el lector que ya abrio el calentador, y a la que suena se le quita el fundido; la cola aun no cambia");
            Check(object.ReferenceEquals(Get(Get(cadB, "Mono"), "anterior"), Get(cadA, "Mono")) && object.ReferenceEquals(Get(Get(cadB, "Noche"), "anterior"), Get(cadA, "Noche")),
                  "y como A acaba entera, B seguira con los filtros de A (grave en mono y compensacion de noche): el cambio no se oye");
            float[] y = LeeTodo((ISampleProvider)sec1, new int[] { 2205 * ch, 999 * ch, 4096 * ch }, null, null);
            object medidaB = Get(Get(cadB, "Tap"), "Medida");
            bool midio = object.ReferenceEquals(Get(f, "tapMedido"), Get(cadB, "Tap")) && (bool)Get(Get(medidaB, "CierreAnterior"), "Listo")
                         && (string)Get(Get(medidaB, "CierreAnterior"), "Ruta") == pa && (string)Get(f, "curMeasurePath") == pb;
            // Lo mismo, cada una por su lado (A sin su fundido, como queda en un disco continuo), con B siguiendo con los
            // filtros de A, que es lo que hace el relevo en un disco continuo (SigueA).
            object sA = Inv(f, "ArmaCadena", tA, Lector(pa), false, 0.0); Inv(Get(sA, "Fundido"), "Quita");
            object sB = Inv(f, "ArmaCadena", tB, Lector(pb), false, 0.0);
            float[] soloA = LeeTodo((ISampleProvider)Get(sA, "Salida"), new int[] { 4096 }, null, null);
            Inv(f, "SigueA", sB, sA);
            float[] soloB = LeeTodo((ISampleProvider)Get(sB, "Salida"), new int[] { 4096 }, null, null);
            int rbx = (int)Get(sB, "Retardo");
            float[] esperado = new float[soloA.Length + soloB.Length - rbx * ch];
            Array.Copy(soloA, esperado, soloA.Length);
            Array.Copy(soloB, rbx * ch, esperado, soloA.Length, soloB.Length - rbx * ch);
            Check(Igual(y, esperado) && (int)Get(cadB, "Retardo") == rbx,
                  "lo que sale son las dos canciones seguidas bit a bit: A entera con su cola, y B sin sus " + rbx + " cuadros de cebado (" + F(1000.0 * rbx / fs, "0.0") + " ms), siguiendo con los filtros de A");
            Check(midio, "la medida en vivo paso a B en su primer bloque; lo medido de A quedo guardado aparte para cerrarlo");
            StoppedEventArgs fin = AlParar(f, null);
            Check(fin.Exception == null && object.ReferenceEquals(Get(f, "nowPlaying"), iB) && Cola(f, iC) && object.ReferenceEquals(Get(f, "reader"), lB)
                  && object.ReferenceEquals(Get(f, "cadenaSonando"), cadB) && Get(f, "cadenaPreparada") == null,
                  "y el relevo deja a B sonando, con su lector en la barra de progreso, y a C primera de la cola");

            // Otra tasa, mismo layout: espera la tabla sin tomar el lector ni bloquear la pantalla.
            monta(tA, disco, new object[] { iC }, pc);
            object lC = sonando[2];
            TimeSpan inicioC=(TimeSpan)TIA.GetProperty("CurrentTime").GetValue(lC,null);
            Inv(f, "PreparaSiguienteSinPausa");
            Check(Get(f, "cadenaPreparada") == null && (bool)Prop(Get(f, "lookahead"), "Listo")
                  && (TimeSpan)TIA.GetProperty("CurrentTime").GetValue(lC,null)==inicioC,
                  "la siguiente a 48 kHz tras44,1 espera tabla: no toma ni suelta el lector mientras se prepara");
            Stopwatch esperaTabla = Stopwatch.StartNew();
            while (Get(f, "cadenaPreparada") == null && esperaTabla.ElapsedMilliseconds < 3000)
            { Thread.Sleep(10); Inv(f, "PreparaSiguienteSinPausa"); }
            object distinta = Get(f, "cadenaPreparada");
            bool tasaLista = distinta != null && object.ReferenceEquals(Get(distinta, "Lector"), lC)
                && ((ISampleProvider)Get(distinta, "Salida")).WaveFormat.SampleRate == fs
                && Get(Get(distinta, "Mono"), "anterior") == null && Get(Get(distinta, "Noche"), "anterior") == null;
            Check(tasaLista, "misma disposicion y tasa distinta mantiene salida estable sin transferir estado de filtros de otra tasa");
            if (tasaLista)
            {
                float[] mixtas = LeeTodo((ISampleProvider)sonando[1], new int[] { 997 * ch, 4096 * ch }, null, null);
                Info("relevo tasas mixtas: " + mixtas.Length + " muestras; esperado " + ((7L * fs + (int)Get(sonando[0], "Retardo")) * ch));
                Check(mixtas.Length == (7L * fs + (int)Get(sonando[0], "Retardo")) * ch,
                    "cadenas reales44,1+48k duran7s y cola inicial: sin doble cebado ni padding de conversion");
                AlParar(f, null);
            }
            else SueltaPreparada(f);
            object video = Item(tB, disco); Set(video, "IsVideo", true);
            monta(tA, disco, new object[] { video }, pb);
            Inv(f, "PreparaSiguienteSinPausa");
            bool noVideo = Get(f, "cadenaPreparada") == null && (bool)Prop(Get(f, "lookahead"), "Listo");
            monta(tL, otro, new object[] { Item(tB, disco) }, pb);
            Inv(f, "PreparaSiguienteSinPausa");
            bool noLejos = Get(f, "cadenaPreparada") == null && (bool)Prop(Get(f, "lookahead"), "Listo");
            monta(tA, disco, new object[] { Item(tB, disco) }, pb);
            Set(f, "audioOpeningForMeasurement", true);
            Inv(f, "PreparaSiguienteSinPausa");
            bool noMedida = Get(f, "cadenaPreparada") == null;
            Set(f, "audioOpeningForMeasurement", false);
            Check(noVideo && noLejos && noMedida, "ni un video, ni a 13 s del final (espera a los 12), ni durante la medida de Ctrl+F3");
            monta(tA, disco, new object[] { Item(tL, otro) }, larga);
            Inv(f, "PreparaSiguienteSinPausa");
            object cadL = Get(f, "cadenaPreparada");
            Check(cadL != null && (int)Get(Get(sonando[0], "Fundido"), "estadoFundido") == 0,
                  "la siguiente de OTRO disco si entra sin pausa, pero la que suena conserva su fundido de salida");
            Check(cadL != null && Get(Get(cadL, "Mono"), "anterior") == null && Get(Get(cadL, "Noche"), "anterior") == null,
                  "y como la que suena acaba fundiendo a silencio, la siguiente empieza sus filtros de cero (no arrastra la memoria de una musica que ya no se oye)");
            SueltaPreparada(f);

            // EL INTERRUPTOR DE SALTAR SILENCIOS (Ctrl+F3) vale desde la siguiente. Los silencios se deciden al abrir, y la
            // siguiente ya estaba abierta (calentador) o armada (relevo) con el ajuste de antes: se retira lo que aun no
            // suena y se vuelve a pedir. Lo que ya entro, no.
            {
                object iB2 = Item(tB, disco), iC2 = Item(tC, disco);
                monta(tA, disco, new object[] { iB2, iC2 }, pb);
                object cadA2 = sonando[0], sec2 = sonando[1], lB2 = sonando[2];
                Inv(f, "PreparaSiguienteSinPausa");
                object armada = Get(f, "cadenaPreparada");
                Inv(f, "SiguienteConElInterruptor");
                object mira = Get(f, "lookahead");
                Check(armada != null && Get(f, "cadenaPreparada") == null && !(bool)Prop(sec2, "HayPreparada") && Suelto(lB2)
                      && (string)Prop(mira, "Para") == pb && Cola(f, iB2, iC2),
                      "armada para entrar sin pausa y aun sin sonar: se retira, su lector se cierra y la siguiente se vuelve a abrir; la cola no cambia");
                object lB3 = Lector(pb);
                Set(f, "lookahead", MiraCon(pb, lB3));   // como la deja el calentador al terminar de abrirla otra vez
                Inv(f, "PreparaSiguienteSinPausa");
                object otra = Get(f, "cadenaPreparada");
                Check(otra != null && object.ReferenceEquals(Get(otra, "Lector"), lB3) && (bool)Prop(sec2, "HayPreparada")
                      && object.ReferenceEquals(Get(Get(otra, "Mono"), "anterior"), Get(cadA2, "Mono")),
                      "y se vuelve a armar con la recien abierta; la que suena sigue sin fundido y la siguiente del disco sigue con sus filtros");
                SueltaPreparada(f);

                monta(tL, otro, new object[] { Item(tB, disco) }, pb);   // a 13 s del final: abierta, aun sin armar
                object lAbierta = sonando[2];
                Inv(f, "SiguienteConElInterruptor");
                mira = Get(f, "lookahead");
                Check(Suelto(lAbierta) && !(bool)Prop(mira, "Listo") && (string)Prop(mira, "Para") == pb,
                      "abierta por el calentador: se suelta y se pide otra vez (se abre con el ajuste nuevo)");

                object iB4 = Item(tB, disco);
                monta(tA, disco, new object[] { iB4, Item(tC, disco) }, pb);
                object sec4 = sonando[1], lB4 = sonando[2];
                Inv(f, "PreparaSiguienteSinPausa");
                object entro = Get(f, "cadenaPreparada");
                LeeTodo((ISampleProvider)sec4, new int[] { 4096 }, null, null);   // el hilo del sonido ya paso a ella
                Inv(f, "SiguienteConElInterruptor");
                Check(entro != null && object.ReferenceEquals(Get(f, "cadenaPreparada"), entro) && !Suelto(lB4) && Prop(Get(f, "lookahead"), "Para") == null,
                      "si ya entro por el relevo, ya es la que suena: no se toca");
                AlParar(f, null);
                SueltaPreparada(f);

                monta(tA, disco, new object[] { Item(tB, disco) }, null);
                Inv(f, "SiguienteConElInterruptor");
                Check(Get(f, "lookahead") == null && Get(f, "cadenaPreparada") == null,
                      "si el calentador aun no la pidio, no se adelanta: la pedira el a su hora, ya con el ajuste nuevo");
            }
            Set(f, "sbrAllowed", false); Set(f, "sbrOn", false); Set(f, "lookahead", null); Set(f, "output", null);
        }
        Codigo.EsperaGuardado(TF, f);
        RelevoYPrimerBloque(ra, rb); // al final: SaveState no altera los escenarios que verifican su contenido
    }

    // ================================================================================================
    //  4) EL CABLEADO
    // ================================================================================================
    static MethodBase Met(Type t, string n) { return t.GetMethod(n, T); }
    static void ElCableado()
    {
        Console.WriteLine("\n  4) EL CABLEADO (leyendo el programa compilado)\n");
        List<string> arranca = Codigo.Llamadas(Met(TF, "StartOpenedTrack"));
        Check(Codigo.Llama(arranca, "ArmaCadena") && Codigo.Llama(arranca, "Encadenado..ctor"),
              "la cancion que arranca por el camino de siempre arma su cadena con ArmaCadena y la salida lee de la secuencia");
        Check(Codigo.EscribeCampo(new List<MethodBase> { Met(TF, "StartOpenedTrack") }, "tapMedido"), "y su medida en vivo es suya desde el principio");
        List<string> arma = Codigo.Llamadas(Met(TF, "ArmaCadena"));
        Check(Codigo.Llama(arma, "LevelTap..ctor") && Codigo.Llama(arma, "DeepBassProvider..ctor") && Codigo.Llama(arma, "LimitadorPicoReal..ctor")
              && Codigo.Llama(arma, "FadeProvider..ctor") && Codigo.Llama(arma, "MedidaCancion..ctor") && Codigo.Llama(arma, "get_DelayFrames"),
              "ArmaCadena arma la cadena entera (nivelado, grave, limitador de pico real, fundido) y cuenta sus retrasos");
        List<string> prepara = Codigo.Llamadas(Met(TF, "PreparaSiguienteSinPausa"));
        Check(Codigo.Llama(prepara, "ArmaCadena"), "la siguiente se arma con la MISMA ArmaCadena: suena igual que si empezara por el camino de siempre");
        Check(Codigo.Llama(prepara, "Cumple") && Codigo.Llama(prepara, "Toma") && Codigo.Llama(prepara, "Prepara") && Codigo.Llama(prepara, "RutaQueSonaria"),
              "usa el lector que ya abrio el calentador, solo si es del mismo formato, y el mismo archivo que sonaria (Apollo incluido)");
        Check(Codigo.Llama(prepara, "SiguienteDelDisco") && Codigo.Llama(prepara, "Quita"), "y solo quita el fundido con la pista siguiente del mismo disco");
        List<string> guardas = Codigo.Nombres(Met(TF, "PreparaSiguienteSinPausa"));
        Check(Codigo.LeeCampo(new List<MethodBase> { Met(TF, "PreparaSiguienteSinPausa") }, "videoOn")
              && Codigo.LeeCampo(new List<MethodBase> { Met(TF, "PreparaSiguienteSinPausa") }, "audioOpeningForMeasurement")
              && guardas.Contains("get_IsVideo") || Codigo.LeeCampo(new List<MethodBase> { Met(TF, "PreparaSiguienteSinPausa") }, "IsVideo"),
              "no prepara con un video de por medio ni durante la medida de Ctrl+F3");
        List<string> lee = Codigo.Nombres(Met(TF, "PublicaBloqueDe"));
        int iM = lee.IndexOf("MedidaDe"), iR = lee.IndexOf("ReportAudioBlock");
        Check(Codigo.Llama(TLevel.GetMethod("Read", T), "PublicaBloqueDe") && iM >= 0 && iR > iM,
              "cada bloque pasa por MedidaDe ANTES de medirse, bajo el guardian de generacion: nunca se mide en la cuenta de otra cancion");
        List<string> para = Codigo.Nombres(Met(TF, "Output_PlaybackStopped"));
        int iRel = para.IndexOf("RecogeAlParar"), iDev = para.IndexOf("RefundCredit");
        int devoluciones = 0; foreach (string n in para) if (n == "RefundCredit") devoluciones++;
        Check(iRel >= 0 && iDev > iRel && devoluciones == 1,
              "al parar la salida: primero se recoge la que ya entro (RecogeAlParar) y despues se atiende el aviso, con UN credito devuelto");
        List<string> recoge = Codigo.Nombres(Met(TF, "RecogeAlParar"));
        Check(recoge.Contains("TomaFallo") && recoge.Contains("TomaRelevo") && recoge.Contains("PasaRelevo") && !recoge.Contains("RefundCredit")
              && !recoge.Contains("PlayNext") && !Codigo.EscribeCampo(new List<MethodBase> { Met(TF, "RecogeAlParar") }, "credits"),
              "RecogeAlParar no cobra, no devuelve y no salta: solo decide que sono y que aviso queda");
        Check(Codigo.Llama(Met(TF, "DisposePlaybackNow"), "SueltaSinPausa"), "saltar y cerrar sueltan la preparada");
        Check(Codigo.Llama(Met(TF, "ProgressTimer_Tick"), "PreparaSiguienteSinPausa") && Codigo.Llama(Met(TF, "AnimTimer_Tick"), "RevisaRelevo"),
              "se prepara con la barra de progreso y se recoge con la animacion (cada 42 ms)");
        Check(Codigo.Llama(Met(TF, "ApplyNormNow"), "AplicaAPreparada"), "los ajustes en vivo tambien le llegan a la preparada");
        Check(Codigo.Llama(Met(TF, "PlayNext"), "DisposePlaybackNow"), "PlayNext sigue parando todo (saltar = empezar la siguiente desde el principio)");
        List<string> pasa = Codigo.Nombres(Met(TF, "PasaRelevo"));
        Check(pasa.Contains("SaveState") && pasa.Contains("RefrescaCola") && !pasa.Contains("RefundCredit") && !Codigo.EscribeCampo(new List<MethodBase> { Met(TF, "PasaRelevo") }, "credits"),
              "el relevo guarda el estado y rehace la cola, y no toca creditos");
        List<string> refresca = Codigo.Nombres(Met(TF, "RefrescaTrasRelevo"));
        Check(refresca.Contains("UpdateNowPlayingUI") && refresca.Contains("ApplyGenreBackground") && refresca.Contains("PrecalientaSiguiente")
              && refresca.Contains("ApuntaGeneroTrasRelevo") && Codigo.Llama(Met(TF, "ApuntaGeneroTrasRelevo"), "EnsureTags")
              && Codigo.Nombres(Met(TF, "RecogeAlParar")).Contains("ApuntaGeneroTrasRelevo"),
              "y la pantalla se entera como al empezar una cancion: letrero, color y genero (tambien si se recoge al parar), y el calentador de la que viene detras");
    }

    // ================================================================================================
    //  5) LOS NUMEROS
    // ================================================================================================
    static void LosNumeros(object form)
    {
        Console.WriteLine("\n  5) LOS NUMEROS\n");
        int fs = 44100, ch = 2;
        int rb; object fb;
        LeeTodo(Cadena(form, Musica(fs, ch, 0.3, 5), fs, ch, true, out rb, out fb), new int[] { 4096 }, null, null);
        Info("hueco entre canciones ANTES: vaciar la salida, abrir la siguiente, volver a abrir la salida y su colchon,");
        Info("mas el cebado de la cadena nueva (" + rb + " cuadros = " + F(1000.0 * rb / fs, "0.0") + " ms de silencio que SI se mide aqui); abrir la salida");
        Info("de Windows no se puede medir en esta maquina. AHORA, con la siguiente preparada: 0 muestras (medido arriba).");
        // Lo que cuesta armar la siguiente (fuera del camino del sonido, 12 s antes) y leer a traves de la secuencia.
        Stopwatch sw = new Stopwatch(); sw.Start(); int veces = 30;
        for (int i = 0; i < veces; i++) { int r; object fx; Cadena(form, new float[4410 * ch], fs, ch, true, out r, out fx); }
        double msArmar = sw.Elapsed.TotalMilliseconds / veces;
        float[] a = Senal(fs * 20, ch, 1);
        float[] buf = new float[2205 * ch];
        double sin = double.MaxValue, con = double.MaxValue;   // el mejor de cinco: lo que cuesta, sin el ruido de la maquina
        for (int k = 0; k < 5; k++)
        {
            Cadena cruda = new Cadena(a, fs, ch, 0, -1);
            sw.Reset(); sw.Start(); while (cruda.Read(buf, 0, buf.Length) > 0) { } sin = Math.Min(sin, sw.Elapsed.TotalMilliseconds);
            object e = Enc(new Cadena(a, fs, ch, 0, -1), "A");
            sw.Reset(); sw.Start(); while (((ISampleProvider)e).Read(buf, 0, buf.Length) > 0) { } con = Math.Min(con, sw.Elapsed.TotalMilliseconds);
        }
        double ns = Math.Max(0, con - sin) * 1e6 / a.Length;
        Info("armar la cadena de la siguiente: " + F(msArmar, "0.00") + " ms (en el hilo de la pantalla, doce segundos antes del final, no en el cambio)");
        Info("leer a traves de la secuencia: " + F(ns, "0.00") + " ns por muestra de mas (Mono, esta maquina; no es un CPU antiguo)");
        Check(ns < 5.0, "la secuencia no encarece la lectura (menos de 5 ns por muestra)");
    }

    static int Main(string[] args)
    {
        asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TEnc = asm.GetType("Rocola.Encadenado");
        TMedida = asm.GetType("Rocola.MedidaCancion");
        TCadena = TF.GetNestedType("CadenaCancion", T);
        TLevel = TF.GetNestedType("LevelTap", T); TBajo = TF.GetNestedType("DeepBassProvider", T);
        TLim = TF.GetNestedType("LimitadorPicoReal", T); TFade = TF.GetNestedType("FadeProvider", T);
        TDeclip = TF.GetNestedType("DeclipProvider", T); TSp = TF.GetNestedType("SpectralTap", T);
        TNoche = TF.GetNestedType("CompensaNoche", T); TMono = TF.GetNestedType("GraveMono", T);
        TPlay = TF.GetNestedType("PlayItem", T); TTrack = TF.GetNestedType("Track", T); TAlbum = TF.GetNestedType("Album", T);
        TFile = TF.GetNestedType("FileAudioSource", T); TIA = TF.GetNestedType("IAudioSource", T);
        if (TEnc == null || TMedida == null || TCadena == null) { Check(false, "no esta el relevo sin pausa (SinPausa.cs)"); return 1; }
        tmp = Path.Combine(Path.GetTempPath(), "rocola_sinpausa_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", tmp);
        try
        {
            object form = Codigo.FormSinEncender(TF);
            ElMecanismo();
            LasCadenas(form);
            LaRocola();
            ElCableado();
            LosNumeros(form);
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
        Console.WriteLine(fallos == 0 ? ("\n  TODO BIEN (" + oks + " comprobaciones)") : ("\n  FALLOS: " + fallos));
        return fallos;
    }
}
