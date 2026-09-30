using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

// DECODIFICAR EN COMA FLOTANTE (rama HD; ver Rocola/DecodificaFlotante.cs)
//
// Los MP3 se descomprimian con el ACM de Windows, en enteros de 16 bits: lo que el MP3 trae por encima del maximo
// digital se cortaba en seco antes de que la rocola pudiera bajar el volumen. En HD, cuando el decodificador de MP3 de
// Windows usado como MFT (coma flotante) queda comprobado contra el de siempre, descomprime el; si no, el de siempre.
//
// El decodificador de Windows solo existe en Windows. Aqui (Mono en Linux) se prueba TODA la logica con dos
// decodificadores de mentira que hacen lo que hacen los de Windows: uno "de siempre" que da enteros de 16 bits
// recortados (y arranca como el ACM de verdad: el primer cuadro no suelta nada y el segundo suelta dos), y uno "de
// Windows" que da la misma senal en coma flotante, sin recortar. Cada cuadro de los MP3 de prueba lleva su numero
// dentro, asi que los dos saben que cuadro les toca y la bateria sabe que tiene que salir:
//
//   1) el de siempre, convertido, da lo mismo que NAudio, bit a bit
//   2) el descompresor entrega coma flotante, y por encima de 1,0 cuando la senal lo trae
//   3) si el de Windows falla a media cancion, sigue el de siempre en ese mismo cuadro, sin saltarse ni repetir nada
//   4) en un hilo STA descomprime el de siempre, y al volver a otro hilo el de Windows entra calentado
//   5) lo que no cabe en una entrega sale en la siguiente; un salto lo olvida
//   6) se cierra lo que se abre, una vez
//   7) por el lector de NAudio de verdad (Mp3FileReader): misma duracion, los cuadros en orden y los saltos
//   8) la comparacion con el de siempre: acepta al bueno y rechaza al desplazado, al distinto y al que no arranca
//   9) una comparacion por sesion y formato, en un hilo que no es STA; y con tres fallos en vivo, se deja
//  10) AAC, WMA y M4A por Media Foundation en coma flotante (aqui no hay Media Foundation: se comprueba la regla)
//  11) el programa lo usa donde debe (leido del codigo compilado)
//  12) EN WINDOWS, con el decodificador de verdad y pruebas/flotante_overs.mp3 (una mezcla sintetica masterizada a lo
//      bruto: +8 dB y recortada, pasada a MP3 con LAME; ver genera_flotante_overs.py): la comparacion, cuanto pasa
//      de 1,0 y lo que cuesta SI supera la comparacion. Si el codec instalado no la supera, se prueba el camino real
//      Envuelve y se exige PCM/duracion identicos al ACM, no se fuerza a usar un codec desplazado. Fuera de Windows
//      se dice y no se da por comprobado.

// Cuadros MP3 de prueba: MPEG-1 capa III, 128 kbps, 44,1 kHz, estereo. El numero del cuadro va en los bytes 4-7.
static class Mp3Prueba
{
    public const int Largo = 417;                   // 144 * 128000 / 44100, sin relleno
    public static byte[] Hace(int cuadros)
    {
        byte[] d = new byte[Largo * cuadros];
        for (int k = 0; k < cuadros; k++)
        {
            int o = k * Largo;
            d[o] = 0xFF; d[o + 1] = 0xFB; d[o + 2] = 0x90; d[o + 3] = 0x00;
            byte[] n = BitConverter.GetBytes(k);
            Array.Copy(n, 0, d, o + 4, 4);
        }
        return d;
    }
    public static int Numero(Mp3Frame f) { return BitConverter.ToInt32(f.RawData, 4); }
    // La senal "de verdad" de cada cuadro: un tono que llega a 1,25 (pasa del maximo digital).
    public static float Senal(int cuadro, int j, int c)
    {
        double t = (cuadro * 1152.0 + j) / 44100.0;
        return (float)(1.25 * Math.Sin(2 * Math.PI * 440.0 * t + c * 0.7));
    }
    public static short Entero(float v)
    {
        double x = Math.Round(v * 32768.0);
        if (x > 32767) x = 32767; else if (x < -32768) x = -32768;
        return (short)x;
    }
}

// EL DE SIEMPRE, de mentira: enteros de 16 bits recortados. Como el ACM de verdad, tras crearse o tras un Reset el
// primer cuadro no suelta nada y el segundo suelta dos.
class AcmFalso : IMp3FrameDecompressor
{
    private readonly WaveFormat wf = new WaveFormat(44100, 16, 2);
    public List<int> Recibidos = new List<int>();
    public int Resets, Cierres;
    public bool ComoAcm = true;
    public Func<int, int, int, short> Valor;       // null = la senal recortada
    private int pendiente = -1; private bool arrancado;
    public WaveFormat OutputFormat { get { return wf; } }
    public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
    {
        int k = Mp3Prueba.Numero(frame); Recibidos.Add(k);
        int n = 0;
        if (ComoAcm && !arrancado) { arrancado = true; pendiente = k; return 0; }
        if (pendiente >= 0) { n += Escribe(pendiente, dest, destOffset); pendiente = -1; }
        n += Escribe(k, dest, destOffset + n);
        return n;
    }
    private int Escribe(int k, byte[] dest, int off)
    {
        for (int j = 0; j < 1152; j++)
            for (int c = 0; c < 2; c++)
            {
                short s = Valor != null ? Valor(k, j, c) : Mp3Prueba.Entero(Mp3Prueba.Senal(k, j, c));
                dest[off + (j * 2 + c) * 2] = (byte)(s & 0xFF); dest[off + (j * 2 + c) * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
        return 1152 * 4;
    }
    public void Reset() { Resets++; arrancado = false; pendiente = -1; }
    public void Dispose() { Cierres++; }
}

// EL DE WINDOWS, de mentira: la misma senal en coma flotante, sin recortar. Un cuadro entra, un cuadro sale.
class MftFalso : IMp3FrameDecompressor
{
    private readonly WaveFormat wf = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    public List<int> Recibidos = new List<int>();
    public int Resets, Cierres;
    public int FallaEn = -1;                         // numero de cuadro con el que lanza un error
    public bool Retrasado;                           // suelta cada cuadro con uno de retraso (un decodificador mal alineado)
    public bool Desplazado;                          // misma cuenta de muestras, pero la senal corrida medio cuadro
    public int RafagaEn = -1;                        // en ese cuadro suelta tres de golpe (los dos anteriores se los guarda)
    public ApartmentState Hilo;
    private int previo = -1; private readonly List<int> guardados = new List<int>();
    public MftFalso() { Hilo = Thread.CurrentThread.GetApartmentState(); }
    public WaveFormat OutputFormat { get { return wf; } }
    public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
    {
        int k = Mp3Prueba.Numero(frame); Recibidos.Add(k);
        if (k == FallaEn) throw new InvalidOperationException("fallo de prueba en el cuadro " + k);
        if (RafagaEn >= 0 && (k == RafagaEn - 2 || k == RafagaEn - 1)) { guardados.Add(k); return 0; }
        if (k == RafagaEn) { guardados.Add(k); int n = 0; foreach (int g in guardados) n += Escribe(g, dest, destOffset + n); guardados.Clear(); return n; }
        if (Retrasado) { int p = previo; previo = k; return p < 0 ? 0 : Escribe(p, dest, destOffset); }
        return Escribe(k, dest, destOffset);
    }
    private int Escribe(int k, byte[] dest, int off)
    {
        for (int j = 0; j < 1152; j++)
            for (int c = 0; c < 2; c++)
            {
                byte[] b = BitConverter.GetBytes(Desplazado ? Mp3Prueba.Senal(k, j + 576, c) : Mp3Prueba.Senal(k, j, c));
                Array.Copy(b, 0, dest, off + (j * 2 + c) * 4, 4);
            }
        return 1152 * 8;
    }
    public void Reset() { Resets++; previo = -1; guardados.Clear(); }
    public void Dispose() { Cierres++; }
}

class Flotante
{
    static int fallos = 0, oks = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (ok) oks++; else fallos++; }
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static Assembly asm;
    static Type TDesc, TDec, TMotor;
    static readonly WaveFormat Mp3Fmt = new Mp3WaveFormat(44100, 2, Mp3Prueba.Largo, 128000);

    static object Estatico(string campo) { return TDec.GetField(campo, TODO).GetValue(null); }
    static void PonEstatico(string campo, object v) { TDec.GetField(campo, TODO).SetValue(null, v); }
    static object Llama(string metodo, params object[] args) { return TDec.GetMethod(metodo, TODO).Invoke(null, args); }
    static int Campo(object o, string n) { return (int)o.GetType().GetField(n, TODO).GetValue(o); }

    // Deja la decision de la sesion como recien arrancada.
    static void Olvida()
    {
        ((IDictionary)Estatico("veredictos")).Clear();
        PonEstatico("ausente", false); PonEstatico("comparacionAtascada", false); PonEstatico("fallosEnVivo", 0); PonEstatico("Comprobaciones", 0);
    }

    static IMp3FrameDecompressor Desc(IMp3FrameDecompressor acm, Func<WaveFormat, IMp3FrameDecompressor> fabrica)
    {
        return (IMp3FrameDecompressor)Activator.CreateInstance(TDesc, TODO, null, new object[] { Mp3Fmt, acm, fabrica }, null);
    }

    static Mp3Frame[] Cuadros(int n)
    {
        MemoryStream ms = new MemoryStream(Mp3Prueba.Hace(n));
        List<Mp3Frame> l = new List<Mp3Frame>();
        Mp3Frame f; while ((f = Mp3Frame.LoadFromStream(ms)) != null) l.Add(f);
        return l.ToArray();
    }

    // Corre algo en un hilo con ese apartamento y espera.
    static void EnHilo(ApartmentState ap, ThreadStart a)
    {
        Exception err = null;
        Thread t = new Thread(delegate() { try { a(); } catch (Exception e) { err = e; } });
        t.SetApartmentState(ap); t.Start(); t.Join();
        if (err != null) throw new Exception("en el hilo " + ap, err);
    }

    // Lo que tendria que salir en coma flotante de un cuadro con cada decodificador.
    static float DeWindows(int k, int j, int c) { return Mp3Prueba.Senal(k, j, c); }
    static float DeSiempre(int k, int j, int c) { return Mp3Prueba.Entero(Mp3Prueba.Senal(k, j, c)) / 32768f; }

    // Descomprime cuadros con un descompresor y devuelve las muestras en coma flotante (lo que vera el lector).
    static List<float> Saca(IMp3FrameDecompressor d, Mp3Frame[] cuadros, int desde, int hasta, int capacidad)
    {
        List<float> r = new List<float>();
        byte[] buf = new byte[capacidad];
        for (int i = desde; i < hasta; i++)
        {
            int n = d.DecompressFrame(cuadros[i], buf, 0);
            for (int b = 0; b < n; b += 4) r.Add(BitConverter.ToSingle(buf, b));
        }
        return r;
    }

    // ¿Las muestras son exactamente la sucesion de cuadros [k0, k0+n) segun 'cual'?
    static int Distintas(List<float> m, int desde, int k0, int n, Func<int, int, int, float> cual)
    {
        int malas = 0;
        for (int k = 0; k < n; k++)
            for (int j = 0; j < 1152; j++)
                for (int c = 0; c < 2; c++)
                {
                    int i = desde + (k * 1152 + j) * 2 + c;
                    if (i >= m.Count || m[i] != cual(k0 + k, j, c)) malas++;
                }
        return malas;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        TDesc = asm.GetType("Rocola.DescompresorFlotante");
        TDec = asm.GetType("Rocola.DecodificacionFlotante");
        TMotor = asm.GetType("Rocola.MotorFlotante");
        if (TDesc == null || TDec == null || TMotor == null)
        {
            // Solo existe en la rama HD. En la de XP (sin coma flotante del decodificador de Windows) no hay que mirar.
            Console.WriteLine("  esta version no decodifica en coma flotante (solo la rama HD): no hay nada que mirar");
            Console.WriteLine("OK sin decodificacion en coma flotante en esta rama");
            return 0;
        }
        List<string> apuntes = new List<string>();
        Action<string> apunta = delegate(string s) { lock (apuntes) apuntes.Add(s); };
        PonEstatico("Apunte", apunta);
        Mp3Frame[] cuadros = Cuadros(40);
        Check(cuadros.Length == 40, "los MP3 de prueba se leen cuadro a cuadro (" + cuadros.Length + " de 40)");

        // ============================================================================================
        Console.WriteLine("\n  1) EL DE SIEMPRE, CONVERTIDO, DA LO MISMO QUE NAUDIO, BIT A BIT\n");
        // Todos los enteros de 16 bits posibles pasan por la conversion y se comparan con la de NAudio
        // (Pcm16BitToSampleProvider: entero / 32768f).
        {
            AcmFalso todos = new AcmFalso(); todos.ComoAcm = false;
            todos.Valor = delegate(int k, int j, int c) { return (short)(((k * 1152 + j) * 2 + c) % 65536 - 32768); };
            object motor = Activator.CreateInstance(TMotor, TODO, null, new object[] { todos, 2 }, null);
            MethodInfo dec = TMotor.GetMethod("Decodifica", TODO);
            float[] sal = new float[16]; int total = 0, malas = 0;
            Mp3Frame[] muchos = Cuadros(29);
            for (int k = 0; k < 29; k++)   // 29 cuadros de 2304 muestras: pasan los 65.536 enteros de 16 bits
            {
                object[] a = new object[] { muchos[k], sal, 0 };
                int n = (int)dec.Invoke(motor, a); sal = (float[])a[1];
                for (int i = 0; i < n; i++)
                {
                    int ent = ((k * 1152 * 2 + i) % 65536) - 32768;
                    short s = (short)ent;
                    byte[] b = BitConverter.GetBytes(s);
                    float naudio = BitConverter.ToInt16(b, 0) / 32768f;
                    if (sal[i] != naudio) malas++;
                }
                total += n;
            }
            Console.WriteLine("     " + total + " enteros convertidos; distintos de la cuenta de NAudio: " + malas);
            Check(total >= 65536 && malas == 0, "cada uno de los 65.536 enteros de 16 bits sale como lo pasa NAudio a coma flotante, bit a bit");
            Check(TMotor.GetConstructor(TODO, null, new Type[] { typeof(IMp3FrameDecompressor), typeof(int) }, null) != null,
                  "y el envoltorio acepta cualquier descompresor de NAudio (el de siempre o el de Windows)");
            bool rechaza = false;
            try { Activator.CreateInstance(TMotor, TODO, null, new object[] { new FormatoRaro(), 2 }, null); }
            catch (TargetInvocationException) { rechaza = true; }
            Check(rechaza, "un descompresor que no da ni enteros de 16 bits ni coma flotante de 32 no se acepta");
        }

        // ============================================================================================
        Console.WriteLine("\n  2) EL DESCOMPRESOR ENTREGA COMA FLOTANTE\n");
        {
            AcmFalso acm = new AcmFalso(); MftFalso mft = null;
            IMp3FrameDecompressor d = null; List<float> m = null;
            EnHilo(ApartmentState.MTA, delegate
            {
                d = Desc(acm, delegate(WaveFormat f) { mft = new MftFalso(); return mft; });
                m = Saca(d, cuadros, 0, 20, 1152 * 8 * 2);
            });
            WaveFormat of = d.OutputFormat;
            Check(of.Encoding == WaveFormatEncoding.IeeeFloat && of.BitsPerSample == 32 && of.SampleRate == 44100 && of.Channels == 2,
                  "entrega coma flotante de 32 bits, a la frecuencia y con los canales del MP3");
            int malas = Distintas(m, 0, 0, 20, DeWindows);
            float pico = 0; foreach (float v in m) pico = Math.Max(pico, Math.Abs(v));
            Console.WriteLine("     20 cuadros por el de Windows: " + m.Count + " muestras, pico " + pico.ToString("0.000") + ", distintas de lo esperado: " + malas);
            Check(malas == 0 && m.Count == 20 * 2304, "descomprime el de Windows, cuadro a cuadro y sin perder ni una muestra");
            Check(pico > 1.2f, "y lo que la senal trae por encima de 1,0 sale tal cual (antes se cortaba en seco)");
            Check(mft != null && Campo(d, "CuadrosMft") == 20 && Campo(d, "CuadrosAcm") == 0, "el de siempre no descomprimio ninguno");

            // Sin el de Windows (no comprobado en la sesion): lo del de siempre, bit a bit.
            AcmFalso acm2 = new AcmFalso();
            IMp3FrameDecompressor d2 = Desc(acm2, null);
            List<float> m2 = null;
            EnHilo(ApartmentState.MTA, delegate { m2 = Saca(d2, cuadros, 0, 20, 1152 * 8 * 2); });
            int malas2 = Distintas(m2, 0, 0, 20, DeSiempre);
            Check(malas2 == 0 && m2.Count == 20 * 2304, "sin el de Windows, sale lo del de siempre bit a bit (con su arranque de 0 y 2 cuadros)");
        }

        // ============================================================================================
        Console.WriteLine("\n  3) SI EL DE WINDOWS FALLA A MEDIA CANCION, SIGUE EL DE SIEMPRE EN ESE MISMO CUADRO\n");
        {
            apuntes.Clear(); Olvida();
            AcmFalso acm = new AcmFalso(); MftFalso mft = null;
            IMp3FrameDecompressor d = null; List<float> m = null;
            EnHilo(ApartmentState.MTA, delegate
            {
                d = Desc(acm, delegate(WaveFormat f) { mft = new MftFalso(); mft.FallaEn = 10; return mft; });
                m = Saca(d, cuadros, 0, 20, 1152 * 8 * 2);
            });
            int malasW = Distintas(m, 0, 0, 10, DeWindows);
            int malasS = Distintas(m, 10 * 2304, 10, 10, DeSiempre);
            Console.WriteLine("     cuadros 0-9 del de Windows, 10-19 del de siempre: distintas " + malasW + " + " + malasS + " de " + m.Count
                              + "; el de siempre recibio: " + string.Join(",", acm.Recibidos.ConvertAll(x => x.ToString()).ToArray()));
            Check(m.Count == 20 * 2304 && malasW == 0 && malasS == 0, "la cancion sigue sin saltarse ni repetir nada: el cuadro 10 sale entero del de siempre");
            Check(acm.Resets == 1 && acm.Recibidos.Count >= 4 && acm.Recibidos[0] == 7 && acm.Recibidos[1] == 8 && acm.Recibidos[2] == 9 && acm.Recibidos[3] == 10,
                  "el de siempre entra vaciado y calentado con los tres cuadros anteriores (7, 8, 9), como tras un salto");
            Check(mft != null && mft.Cierres == 1, "el de Windows que fallo se cierra ahi mismo");
            Check(apuntes.Exists(s => s.IndexOf("fallo a media cancion") >= 0), "y queda apuntado (rocola_error.txt)");
            Check((int)Estatico("fallosEnVivo") == 1, "y cuenta como un fallo de la sesion");

            // Y si en esta cancion ni se puede preparar: toda por el de siempre, apuntado.
            apuntes.Clear();
            AcmFalso acm2 = new AcmFalso();
            IMp3FrameDecompressor d2 = null; List<float> m2 = null;
            EnHilo(ApartmentState.MTA, delegate
            {
                d2 = Desc(acm2, delegate(WaveFormat f) { throw new InvalidOperationException("no se deja crear"); });
                m2 = Saca(d2, cuadros, 0, 10, 1152 * 8 * 2);
            });
            Check(m2.Count == 10 * 2304 && Distintas(m2, 0, 0, 10, DeSiempre) == 0 && Campo(d2, "CuadrosAcm") == 10,
                  "si el de Windows ni se puede preparar para una cancion, suena entera por el de siempre");
            Check(apuntes.Exists(s => s.IndexOf("no se pudo preparar para esta cancion") >= 0), "y queda apuntado");
        }

        // ============================================================================================
        Console.WriteLine("\n  4) EN UN HILO STA DESCOMPRIME EL DE SIEMPRE; AL VOLVER, EL DE WINDOWS ENTRA CALENTADO\n");
        {
            AcmFalso acm = new AcmFalso(); MftFalso mft = null;
            IMp3FrameDecompressor d = Desc(acm, delegate(WaveFormat f) { mft = new MftFalso(); return mft; });
            List<float> m = new List<float>();
            EnHilo(ApartmentState.STA, delegate { m.AddRange(Saca(d, cuadros, 0, 6, 1152 * 8 * 2)); });
            Check(mft == null, "en un hilo STA (la salida clasica, que se rellena desde la pantalla) el de Windows ni se crea");
            EnHilo(ApartmentState.MTA, delegate { m.AddRange(Saca(d, cuadros, 6, 12, 1152 * 8 * 2)); });
            int malasS = Distintas(m, 0, 0, 6, DeSiempre), malasW = Distintas(m, 6 * 2304, 6, 6, DeWindows);
            Console.WriteLine("     cuadros 0-5 del de siempre, 6-11 del de Windows: distintas " + malasS + " + " + malasW
                              + "; el de Windows recibio: " + (mft == null ? "-" : string.Join(",", mft.Recibidos.ConvertAll(x => x.ToString()).ToArray())));
            Check(m.Count == 12 * 2304 && malasS == 0 && malasW == 0, "la cancion sigue entera al cambiar de hilo");
            Check(mft != null && mft.Hilo != ApartmentState.STA, "el de Windows se crea en el hilo que no es STA");
            Check(mft != null && mft.Resets == 1 && mft.Recibidos.Count >= 4 && mft.Recibidos[0] == 3 && mft.Recibidos[1] == 4 && mft.Recibidos[2] == 5,
                  "y entra vaciado y calentado con los tres cuadros anteriores (3, 4, 5)");
            EnHilo(ApartmentState.STA, delegate { m.AddRange(Saca(d, cuadros, 12, 15, 1152 * 8 * 2)); });
            Check(Distintas(m, 12 * 2304, 12, 3, DeSiempre) == 0 && acm.Recibidos.Contains(9) && acm.Recibidos.Contains(10) && acm.Recibidos.Contains(11),
                  "y al volver a un STA, el de siempre entra calentado igual (9, 10, 11)");
        }

        // ============================================================================================
        Console.WriteLine("\n  5) LO QUE NO CABE SALE EN LA ENTREGA SIGUIENTE; UN SALTO LO OLVIDA\n");
        {
            AcmFalso acm = new AcmFalso(); MftFalso mft = null;
            IMp3FrameDecompressor d = null; List<float> m = null; int maxEntrega = 0;
            EnHilo(ApartmentState.MTA, delegate
            {
                d = Desc(acm, delegate(WaveFormat f) { mft = new MftFalso(); mft.RafagaEn = 6; return mft; });
                m = new List<float>(); byte[] buf = new byte[1152 * 8 * 2];   // lo que da el lector: dos cuadros
                for (int i = 0; i < 12; i++)
                {
                    int n = d.DecompressFrame(cuadros[i], buf, 0); maxEntrega = Math.Max(maxEntrega, n);
                    for (int b = 0; b < n; b += 4) m.Add(BitConverter.ToSingle(buf, b));
                }
            });
            Console.WriteLine("     el de Windows solto tres cuadros de golpe en el 6; la entrega mas grande: " + maxEntrega + " bytes (caben " + (1152 * 8 * 2) + ")");
            Check(maxEntrega <= 1152 * 8 * 2, "nunca escribe mas de lo que cabe en el buffer del lector");
            Check(m.Count == 12 * 2304 && Distintas(m, 0, 0, 12, DeWindows) == 0, "y no se pierde ni se desordena nada: lo que sobro salio despues");
            // Un salto a media rafaga: lo pendiente se olvida (el lector recoloca y calienta por su cuenta).
            MftFalso mft2 = null; AcmFalso acm2 = new AcmFalso();
            IMp3FrameDecompressor d2 = null; int trasSalto = -1;
            EnHilo(ApartmentState.MTA, delegate
            {
                d2 = Desc(acm2, delegate(WaveFormat f) { mft2 = new MftFalso(); mft2.RafagaEn = 3; return mft2; });
                byte[] buf = new byte[1152 * 8 * 2];
                for (int i = 0; i < 4; i++) d2.DecompressFrame(cuadros[i], buf, 0);   // queda un cuadro pendiente
                d2.Reset();
                trasSalto = d2.DecompressFrame(cuadros[20], buf, 0);
            });
            Check(trasSalto == 1152 * 8 && mft2.Resets == 1 && acm2.Resets == 1, "tras un salto (Reset) no sale nada de antes del salto, y los dos se vacian");
        }

        // ============================================================================================
        Console.WriteLine("\n  6) SE CIERRA LO QUE SE ABRE, UNA VEZ\n");
        {
            AcmFalso acm = new AcmFalso(); MftFalso mft = null;
            IMp3FrameDecompressor d = null;
            EnHilo(ApartmentState.MTA, delegate
            {
                d = Desc(acm, delegate(WaveFormat f) { mft = new MftFalso(); return mft; });
                Saca(d, cuadros, 0, 3, 1152 * 8 * 2);
            });
            d.Dispose(); d.Dispose();
            Check(acm.Cierres == 1 && mft.Cierres == 1, "al cerrarlo se cierran el de siempre y el de Windows, una sola vez aunque se cierre dos");
            AcmFalso acm2 = new AcmFalso();
            IMp3FrameDecompressor d2 = Desc(acm2, delegate(WaveFormat f) { return new MftFalso(); });
            d2.Dispose();
            Check(acm2.Cierres == 1, "y si nunca llego a descomprimir, se cierra el de siempre igual");
        }

        // ============================================================================================
        Console.WriteLine("\n  7) POR EL LECTOR DE NAUDIO DE VERDAD (Mp3FileReader)\n");
        {
            byte[] mp3 = Mp3Prueba.Hace(100);
            AcmFalso acm0 = new AcmFalso();
            TimeSpan antes;
            using (Mp3FileReader r0 = new Mp3FileReader(new MemoryStream(mp3), delegate(WaveFormat f) { return acm0; })) antes = r0.TotalTime;
            AcmFalso acm = new AcmFalso(); MftFalso mft = null;
            List<float> todo = new List<float>(), trasSalto = new List<float>();
            TimeSpan ahora = TimeSpan.Zero; WaveFormat wf = null;
            EnHilo(ApartmentState.MTA, delegate
            {
                using (Mp3FileReader r = new Mp3FileReader(new MemoryStream(mp3), delegate(WaveFormat f) { return Desc(acm, delegate(WaveFormat g) { mft = new MftFalso(); return mft; }); }))
                {
                    ahora = r.TotalTime; wf = r.WaveFormat;
                    SampleChannel sc = new SampleChannel(r, false);
                    float[] b = new float[3000]; int n;
                    while ((n = sc.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i++) todo.Add(b[i]);
                    r.Position = 50L * 1152 * 8;   // al principio del cuadro 50 (en bytes: un TimeSpan redondea al milisegundo)
                    n = sc.Read(b, 0, 2304); for (int i = 0; i < n; i++) trasSalto.Add(b[i]);
                }
            });
            Console.WriteLine("     duracion con el de siempre " + antes.TotalMilliseconds.ToString("0.0") + " ms, en coma flotante " + ahora.TotalMilliseconds.ToString("0.0") + " ms; muestras " + todo.Count);
            Check(wf != null && wf.Encoding == WaveFormatEncoding.IeeeFloat, "el lector queda en coma flotante");
            Check(antes == ahora && antes.TotalMilliseconds > 2500, "con la misma duracion exacta que antes");
            Check(todo.Count == 100 * 2304 && Distintas(todo, 0, 0, 100, DeWindows) == 0, "y los 100 cuadros salen enteros y en orden");
            Check(trasSalto.Count == 2304 && Distintas(trasSalto, 0, 50, 1, DeWindows) == 0,
                  "un salto (lo que usa 'retomar' tras un fallo del aparato) cae justo en el cuadro pedido");
        }

        // ============================================================================================
        Console.WriteLine("\n  8) LA COMPARACION CON EL DE SIEMPRE\n");
        {
            string d;
            MethodInfo mCompara = TDec.GetMethod("Compara", TODO);
            Func<Func<WaveFormat, IMp3FrameDecompressor>, int> compara = delegate(Func<WaveFormat, IMp3FrameDecompressor> w)
            {
                object[] a = new object[] { Mp3Fmt, new MemoryStream(Mp3Prueba.Hace(120)), (Func<WaveFormat, IMp3FrameDecompressor>)delegate(WaveFormat f) { return new AcmFalso(); }, w, null };
                int r = (int)mCompara.Invoke(null, a); d = (string)a[4]; Console.WriteLine("       -> " + d); return r;
            };
            Olvida();
            Check(compara(delegate(WaveFormat f) { return new MftFalso(); }) == 1,
                  "el que da lo mismo (salvo lo que el de siempre recorto) se acepta");
            Check(compara(delegate(WaveFormat f) { MftFalso x = new MftFalso(); x.Retrasado = true; return x; }) == -1,
                  "el que suelta cada cuadro con uno de retraso se rechaza (con el lector perderia el ultimo cuadro y los saltos caerian uno antes)");
            Check(compara(delegate(WaveFormat f) { MftFalso x = new MftFalso(); x.Desplazado = true; return x; }) == -1,
                  "y el que va desplazado (medio cuadro) tambien");
            Check(compara(delegate(WaveFormat f) { MftFalso x = new MftFalso(); x.FallaEn = 30; return x; }) == -1,
                  "el que falla al descomprimir se rechaza");
            Check(compara(delegate(WaveFormat f) { throw new InvalidOperationException("no esta registrado"); }) == -1 && (bool)Estatico("ausente"),
                  "y el que no se puede preparar se rechaza para toda la sesion");
            Olvida();
            // El juicio mismo, con numeros: silencio, diferencias chicas y grandes, y lo recortado.
            MethodInfo mJ = TDec.GetMethod("Juzga", TODO);
            int nn = 60 * 2304; float[] a0 = new float[nn], m0 = new float[nn];
            Random rnd = new Random(1);
            Func<double, int> juzga = delegate(double ruidoDb)
            {
                for (int i = 0; i < nn; i++)
                {
                    float x = (float)(0.5 * Math.Sin(i * 0.03)); a0[i] = x;
                    m0[i] = x + (float)(0.5 * Math.Pow(10, ruidoDb / 20) * (rnd.NextDouble() * 2 - 1) * 1.2247);
                }
                object[] ar = new object[] { a0, nn, m0, nn, 2, null };
                int r = (int)mJ.Invoke(null, ar); Console.WriteLine("       ruido a " + ruidoDb + " dB -> " + ar[5]); return r;
            };
            Check(juzga(-70) == 1, "dos decodificadores que se apartan 70 dB (lo normal entre dos bien hechos) dan lo mismo");
            Check(juzga(-30) == -1, "uno que se aparta 30 dB no");
            for (int i = 0; i < nn; i++) { a0[i] = 0f; m0[i] = 0f; }
            object[] sil = new object[] { a0, nn, m0, nn, 2, null };
            Check((int)mJ.Invoke(null, sil) == 0, "con todo en silencio no se decide nada (se probara con otra cancion)");
            for (int i = 0; i < nn; i++) { float x = (float)(1.3 * Math.Sin(i * 0.03)); m0[i] = x; a0[i] = Math.Max(-1f, Math.Min(32767f / 32768f, x)); }
            object[] rec = new object[] { a0, nn, m0, nn, 2, null };
            int rr = (int)mJ.Invoke(null, rec);
            Console.WriteLine("       senal recortada por el de siempre -> " + rec[5]);
            Check(rr == 1, "lo que el de siempre recorto no cuenta: ahi el de Windows TIENE que pasar de 1,0");
        }

        // ============================================================================================
        Console.WriteLine("\n  9) UNA COMPARACION POR SESION Y FORMATO\n");
        {
            string tmp = Path.Combine(Path.GetTempPath(), "rocola_flotante_" + Guid.NewGuid().ToString("N") + ".mp3");
            File.WriteAllBytes(tmp, Mp3Prueba.Hace(120));
            object posibleAntes = Estatico("Posible"), fabricaAntes = Estatico("FabricaMft");
            try
            {
                Olvida(); apuntes.Clear();
                PonEstatico("Posible", true);
                List<MftFalso> creados = new List<MftFalso>();
                Func<WaveFormat, IMp3FrameDecompressor> fab = delegate(WaveFormat f) { MftFalso x = new MftFalso(); lock (creados) creados.Add(x); return x; };
                PonEstatico("FabricaMft", fab);
                Func<WaveFormat, IMp3FrameDecompressor> acmNuevo = delegate(WaveFormat f) { return new AcmFalso(); };
                MethodInfo mUsar = TDec.GetMethod("Usar", TODO), mEnv = TDec.GetMethod("Envuelve", TODO);
                bool u1 = false, u2 = false, u3 = false, u4 = false;
                EnHilo(ApartmentState.STA, delegate   // se abre desde un STA, como AperturaAudio
                {
                    u1 = (bool)mUsar.Invoke(null, new object[] { Mp3Fmt, tmp, acmNuevo });
                    u2 = (bool)mUsar.Invoke(null, new object[] { Mp3Fmt, tmp, acmNuevo });
                    u3 = (bool)mUsar.Invoke(null, new object[] { Mp3Fmt, null, null });
                });
                int comp = (int)Estatico("Comprobaciones");
                Console.WriteLine("     tres aperturas del mismo formato: " + comp + " comparacion(es); apunte: " + (apuntes.Count > 0 ? apuntes[0] : "-"));
                Check(u1 && u2 && u3 && comp == 1, "se compara una vez y lo decidido vale para toda la sesion, tambien para el MP3 en memoria");
                Check(creados.Count > 0 && creados[0].Hilo != ApartmentState.STA, "la comparacion crea el de Windows en un hilo propio, que no es STA, aunque se abra desde uno");
                Check(apuntes.Exists(s => s.IndexOf("en uso") >= 0 && s.IndexOf("44100 Hz, 2 canales") >= 0), "y apunta que lo decidio y por que");
                WaveFormat mono = new Mp3WaveFormat(44100, 1, Mp3Prueba.Largo, 128000);
                u4 = (bool)mUsar.Invoke(null, new object[] { mono, null, null });
                Check(!u4, "otro formato sin comparar, desde un MP3 en memoria: el de siempre (no hay archivo que mirar)");
                AcmFalso a1 = new AcmFalso();
                object env = mEnv.Invoke(null, new object[] { Mp3Fmt, a1, tmp, acmNuevo });
                Check(env != null && env.GetType() == TDesc, "comprobado: AbreMp3 recibe el descompresor de coma flotante, con el de siempre dentro");
                PonEstatico("fallosEnVivo", 3);
                AcmFalso a2 = new AcmFalso();
                object env2 = mEnv.Invoke(null, new object[] { Mp3Fmt, a2, tmp, acmNuevo });
                Check(object.ReferenceEquals(env2, a2), "con tres fallos en vivo en la sesion ya no se usa: el de siempre, TAL CUAL, como antes");
                Olvida(); PonEstatico("Posible", false);
                AcmFalso a3 = new AcmFalso();
                object env3 = mEnv.Invoke(null, new object[] { Mp3Fmt, a3, tmp, acmNuevo });
                Check(object.ReferenceEquals(env3, a3) && (int)Estatico("Comprobaciones") == 0,
                      "fuera de Windows 7 o posterior ni se compara: el de siempre tal cual");
                object nlAntes=Estatico("FabricaNLayer");
                try {
                    Olvida();PonEstatico("Posible",true);
                    PonEstatico("FabricaMft",(Func<WaveFormat,IMp3FrameDecompressor>)delegate(WaveFormat f){throw new NotSupportedException("MFT rechazado");});
                    Func<WaveFormat,IMp3FrameDecompressor> nlFab=delegate(WaveFormat f){return new MftFalso();};
                    PonEstatico("FabricaNLayer",nlFab);
                    IMp3FrameDecompressor e=(IMp3FrameDecompressor)mEnv.Invoke(null,new object[]{Mp3Fmt,new AcmFalso(),tmp,acmNuevo});
                    Check(e.GetType()==TDesc,"Sin MFT, NLayer alineado/costo acotado se selecciona");
                    List<float> nlPcm=null;
                    EnHilo(ApartmentState.STA,delegate{nlPcm=Saca(e,cuadros,0,20,1152*8*2);});
                    Check(nlPcm.Count==20*2304&&Distintas(nlPcm,0,0,20,DeWindows)==0,"Alternativa administrada funciona en STA y conserva overs");
                    Check((int)Estatico("Comprobaciones")==1,"MFT y NLayer comparten una sola comprobacion por formato");
                    e.Dispose();
                } finally {PonEstatico("FabricaNLayer",nlAntes);}
            }
            finally
            {
                PonEstatico("Posible", posibleAntes); PonEstatico("FabricaMft", fabricaAntes); Olvida();
                try { File.Delete(tmp); } catch { }
            }
        }

        // ============================================================================================
        Console.WriteLine("\n  10) AAC, WMA Y M4A: MEDIA FOUNDATION EN COMA FLOTANTE\n");
        {
            MethodInfo mOtro = TDec.GetMethod("AbreOtro", TODO);
            object posibleAntes = Estatico("Posible");
            try
            {
                PonEstatico("Posible", true);
                apuntes.Clear();
                bool propios = true;
                foreach (string e in new string[] { "a.mp3", "b.WAV", "c.aif", "d.aiff" })
                    if (mOtro.Invoke(null, new object[] { e }) != null) propios = false;
                Check(propios && apuntes.Count == 0, "MP3, WAV y AIFF siguen por su camino sin intentarlo siquiera (el MP3 por AbreMp3; WAV y AIFF no se recortan)");
                apuntes.Clear();
                string raro = Path.Combine(Path.GetTempPath(), "rocola_flotante_" + Guid.NewGuid().ToString("N") + ".m4a");
                File.WriteAllText(raro, "esto no es sonido");
                object r = null; bool lanzo = false;
                try { r = mOtro.Invoke(null, new object[] { raro }); } catch { lanzo = true; }
                try { File.Delete(raro); } catch { }
                Check(!lanzo && r == null, "si Media Foundation no lo abre en coma flotante, no revienta: por el camino de siempre (AudioFileReader)");
                Check(apuntes.Exists(s => s.StartsWith("Coma flotante en .m4a")), "y lo apunta una vez");
                PonEstatico("Posible", false);
                Check(mOtro.Invoke(null, new object[] { "e.m4a" }) == null, "fuera de Windows 7 o posterior, el camino de siempre sin intentarlo");
            }
            finally { PonEstatico("Posible", posibleAntes); }
            List<string> l = Codigo.Llamadas(mOtro);
            Check(Codigo.Llama(l, "set_RequestFloatOutput"), "a Media Foundation se le pide coma flotante (RequestFloatOutput)");
            Check(Codigo.Llama(l, "get_Encoding"), "y se mira que de verdad la dio antes de usarlo");
        }

        // ============================================================================================
        Console.WriteLine("\n  11) EL PROGRAMA LO USA DONDE DEBE\n");
        {
            List<string> ab = Codigo.ConAyudantes(TF, "AbreMp3");
            Check(Codigo.Llama(ab, "DecodificacionFlotante.Envuelve"), "los MP3 (los que suenan, los que se miden y los de memoria) pasan por la decision");
            foreach (string cual in new string[] { "FileAudioSource", "LectorMedida" })
            {
                Type t = TF.GetNestedType(cual, BindingFlags.NonPublic | BindingFlags.Public);
                List<string> l = new List<string>();
                if (t != null) foreach (ConstructorInfo c in t.GetConstructors(TODO)) l.AddRange(Codigo.Llamadas(c));
                Check(t != null && Codigo.Llama(l, "DecodificacionFlotante.AbreOtro"), cual + " abre lo que no es MP3 en coma flotante si se puede");
                MethodInfo dis = t != null ? t.GetMethod("Dispose", TODO, null, Type.EmptyTypes, null) : null;
                List<MethodBase> uno = new List<MethodBase>(); if (dis != null) uno.Add(dis);
                Check(dis != null && Codigo.LeeCampo(uno, "flotante"), "   ...y lo cierra al terminar");
            }
            MethodInfo mUsar = TDec.GetMethod("Usar", TODO);
            Check(Codigo.Llama(Codigo.Llamadas(mUsar), "Monitor.Enter"), "la decision se toma bajo candado (dos hilos abriendo a la vez no comparan dos veces)");
            MethodInfo mFallo = TDec.GetMethod("FalloEnVivo", TODO);
            Check(!Codigo.Llama(Codigo.Llamadas(mFallo), "Monitor.Enter"), "pero un fallo en vivo se cuenta SIN candado: corre en el hilo del sonido y no puede esperar a una comparacion");
        }

        // ============================================================================================
        Console.WriteLine("\n  12) EN WINDOWS, EL DECODIFICADOR DE VERDAD\n");
        {
            string fix = Path.GetFullPath("flotante_overs.mp3");
            Check(File.Exists(fix), "esta el MP3 de prueba masterizado a lo bruto (pruebas/flotante_overs.mp3)");
            bool enWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            if (!enWindows || !File.Exists(fix))
            {
                Console.WriteLine("     NO SE PUEDE MEDIR AQUI: el decodificador de MP3 de Windows (mp3dmod.dll, como MFT) solo existe en Windows.");
                Console.WriteLine("     Esta misma bateria lo compara y lo mide en Windows: validar-audio-mejoras.ps1 la corre.");
            }
            else PruebaEnWindows(fix, apuntes);
        }

        Console.WriteLine("\n" + (fallos == 0 ? "OK decodificacion en coma flotante: " + oks + " comprobaciones" : "FALLOS: " + fallos));
        return fallos;
    }

    // Con el de Windows de verdad y el ACM de verdad, sobre el MP3 de prueba.
    static void PruebaEnWindows(string fix, List<string> apuntes)
    {
        Func<WaveFormat, IMp3FrameDecompressor> acmNuevo = delegate(WaveFormat f) { return Mp3FileReader.CreateAcmFrameDecompressor(f); };
        Func<WaveFormat, IMp3FrameDecompressor> mftNuevo = (Func<WaveFormat, IMp3FrameDecompressor>)Estatico("FabricaMft");
        WaveFormat fmt;
        using (Mp3FileReader r = new Mp3FileReader(fix)) fmt = r.Mp3WaveFormat;
        int veredicto = 0; string detalle = null;
        EnHilo(ApartmentState.MTA, delegate
        {
            using (FileStream fs = File.OpenRead(fix))
            {
                object[] a = new object[] { fmt, fs, acmNuevo, mftNuevo, null };
                veredicto = (int)TDec.GetMethod("Compara", TODO).Invoke(null, a); detalle = (string)a[4];
            }
        });
        Console.WriteLine("     comparacion con el de siempre: " + detalle);
        if (veredicto == -1)
        {
            Check(!String.IsNullOrEmpty(detalle), "el rechazo del codec instalado explica su motivo");
            Olvida();
            apuntes.Clear();
            bool mismoMotor = true, mismoPcm = true, mismaDuracion = false;
            long muestras = 0, muestrasOriginal = 0; float pico = 0;
            EnHilo(ApartmentState.MTA, delegate
            {
                using (Mp3FileReader seguro = new Mp3FileReader(fix, delegate(WaveFormat f)
                {
                    IMp3FrameDecompressor acm = acmNuevo(f);
                    IMp3FrameDecompressor elegido = (IMp3FrameDecompressor)Llama("Envuelve", f, acm, fix, acmNuevo);
                    mismoMotor = mismoMotor && Object.ReferenceEquals(acm, elegido);
                    return elegido;
                }))
                using (Mp3FileReader original = new Mp3FileReader(fix))
                {
                    mismaDuracion = seguro.TotalTime == original.TotalTime && seguro.WaveFormat.SampleRate == original.WaveFormat.SampleRate
                        && seguro.WaveFormat.Channels == original.WaveFormat.Channels;
                    SampleChannel sc=new SampleChannel(seguro,false), oc=new SampleChannel(original,false);
                    float[] a = new float[2048], b = new float[2048];
                    int na, nb;
                    do
                    {
                        na = sc.Read(a, 0, a.Length); nb = oc.Read(b, 0, b.Length);
                        if (na != nb) mismoPcm = false;
                        for (int i = 0; i < Math.Min(na, nb); i++) {
                            if (mismoMotor ? a[i]!=b[i] : (Math.Abs(b[i])<.999f && Math.Abs(a[i]-b[i])>.001f)) mismoPcm=false;
                            pico=Math.Max(pico,Math.Abs(a[i]));
                        }
                        muestras += na; muestrasOriginal += nb;
                    } while (na > 0 || nb > 0);
                }
            });
            Check((int)Estatico("Comprobaciones") == 1,
                  "Envuelve compara MFT y alternativa NLayer en un solo trabajador/plazo");
            foreach(string apunte in apuntes) Console.WriteLine("     decision completa: "+apunte);
            Check(apuntes.Exists(s => s.Contains("NO se usa") || s.Contains("se sigue con el de siempre")),
                  "la decision real registra que no habilito el codec incompatible");
            Check(mismaDuracion && muestras > 0 && muestras==muestrasOriginal, "el camino seguro conserva cuadros, canales y duracion del ACM");
            Check(mismoPcm, mismoMotor ? "ACM rechazado NLayer: PCM bit-identico" : "NLayer real: PCM alineado en archivo entero salvo picos recortados por ACM");
            Check(mismoMotor || pico>1.2f,"NLayer aceptado conserva picos flotantes por encima del maximo");
            Console.WriteLine(mismoMotor ? "     ACM exacto: ambos flotantes rechazados en este equipo." : "     NLayer flotante REAL habilitado tras rechazo de MFT: pico="+pico);
            return;
        }
        Check(veredicto == 1, "el decodificador de Windows da lo mismo que el de siempre, sin desplazarse");
        // Descomprimir entero con cada uno y contar lo que pasa de 1,0.
        long sobreW = 0, topeS = 0, nW = 0, nS = 0; double picoW = 0, msW = 0, msS = 0;
        EnHilo(ApartmentState.MTA, delegate
        {
            Stopwatch sw = Stopwatch.StartNew();
            using (Mp3FileReader r = new Mp3FileReader(fix, delegate(WaveFormat f) { return Desc(Mp3FileReader.CreateAcmFrameDecompressor(f), mftNuevo); }))
            {
                SampleChannel sc = new SampleChannel(r, false); float[] b = new float[8192]; int n;
                while ((n = sc.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i++) { double v = Math.Abs(b[i]); if (v > 1.0) sobreW++; if (v > picoW) picoW = v; nW++; }
            }
            msW = sw.Elapsed.TotalMilliseconds; sw = Stopwatch.StartNew();
            using (Mp3FileReader r = new Mp3FileReader(fix))
            {
                SampleChannel sc = new SampleChannel(r, false); float[] b = new float[8192]; int n;
                while ((n = sc.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i++) { if (b[i] >= 32767f / 32768f || b[i] <= -1f) topeS++; nS++; }
            }
            msS = sw.Elapsed.TotalMilliseconds;
        });
        double seg = nS / 2.0 / 44100.0;
        Console.WriteLine("     de Windows: " + sobreW + " muestras por encima de 1,0 (pico " + (20 * Math.Log10(picoW)).ToString("0.00") + " dBFS); de siempre: "
                          + topeS + " pegadas al tope");
        Console.WriteLine("     coste por segundo de musica: de Windows " + (msW / seg).ToString("0.00") + " ms, de siempre " + (msS / seg).ToString("0.00") + " ms");
        Check(nW == nS, "las dos descompresiones dan el mismo numero de muestras");
        Check(sobreW > 0 && picoW > 1.0, "y el de Windows deja pasar lo que el MP3 trae por encima de 1,0");
    }
}

// Un descompresor con una salida que no es ni enteros de 16 bits ni coma flotante de 32.
class FormatoRaro : IMp3FrameDecompressor
{
    public WaveFormat OutputFormat { get { return new WaveFormat(44100, 24, 2); } }
    public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset) { return 0; }
    public void Reset() { }
    public void Dispose() { }
}
