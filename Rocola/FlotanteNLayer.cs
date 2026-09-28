// DECODIFICAR EN COMA FLOTANTE EN MODEST (con NLayer): lo que el MP3 trae por encima del maximo digital ya no se
// recorta en seco
//
// Los MP3 se descomprimen con el descompresor de ACM de Windows, que entrega enteros de 16 bits. Un MP3 hecho de un
// master fuerte trae picos POR ENCIMA del maximo digital (medido con LAME: de +0,3 dB en un master moderado a +3,5 dB
// en uno recortado a proposito), y el entero de 16 bits los corta en seco ANTES de que la rocola pueda bajar el
// volumen. En HD eso lo resuelve el decodificador de Windows usado como MFT (DecodificaFlotante.cs de esa rama); en
// XP no existe. Aqui lo hace NLayer (packages/NLayer.1.12.0, licencia MIT, la misma que usa la rama de Linux): un
// descompresor de MP3 escrito en C#, que da coma flotante.
//
// Es lo mismo que en HD, con el mismo cuidado:
//   - El lector de NAudio (Mp3FileReader) sigue siendo el mismo -su tabla de cuadros, la duracion, los saltos, la
//     continuidad sin pausas-; solo cambia QUIEN descomprime cada cuadro.
//   - Se COMPARA con el de siempre una vez por sesion y por formato (frecuencia y canales), con la primera cancion que lo
//     necesita: los dos descomprimen los mismos cuadros y NLayer tiene que dar la misma cuenta de muestras y la misma
//     senal (a menos de -50 dB), salvo lo que el de siempre recorto. Si no, esa sesion sigue con el de siempre.
//   - Si falla a media cancion, sigue el de siempre desde ese mismo cuadro; con tres fallos en la sesion ya no se usa.
//   - Si falta NLayer.dll junto a Rocola.exe, se apunta y se sigue con el de siempre.
// Y dos frenos que en HD no hacen falta, porque en XP el sonido se descomprime en el hilo de la pantalla y el equipo es
// modesto:
//   - SOLO EN LAS CANCIONES QUE LO NECESITAN: las que llegan al maximo digital (su pico medido, Sonoridad.cs, es de
//     -0,05 dBFS o mas). Las demas no tienen nada que recortar y siguen con el de siempre, sin gastar nada.
//   - SOLO SI EL EQUIPO LO AGUANTA: al comparar se mide lo que tarda NLayer. Si gasta mas del 3 % de un nucleo (el de
//     siempre gasta alrededor del 1 %), no se usa en esa sesion. Medido en la maquina de pruebas (Mono): 2,2 % en un
//     MP3 de 192 kbit/s estereo; en un procesador de la epoca de XP puede ser varias veces mas, y entonces no se usa.
// Cuando descomprime el de siempre, sale lo mismo que antes, bit a bit: en esas canciones el lector se arma exactamente
// como antes.
//
// NO PROBADO EN XP: esto se escribio y se probo con Mono en Linux (pruebas/flotantexp.cs), con NLayer de verdad y con
// descompresores de mentira que imitan al de Windows. La linea "MP3 en coma flotante" de rocola_error.txt dice en cada
// maquina que decidio y por que.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    // Lo que suelta un descompresor de cuadros MP3 (de NAudio: IMp3FrameDecompressor), en coma flotante: el de
    // siempre da enteros de 16 bits y se pasan a coma flotante con la MISMA cuenta que hace NAudio
    // (Pcm16BitToSampleProvider: entero / 32768f), asi que sale lo mismo que antes, bit a bit; NLayer ya da
    // coma flotante y sale tal cual.
    internal sealed class MotorFlotante : IDisposable
    {
        internal readonly IMp3FrameDecompressor Descompresor;
        private readonly bool enteros;
        private readonly byte[] bytes;
        public MotorFlotante(IMp3FrameDecompressor d, int canales)
        {
            Descompresor = d;
            WaveFormat f = d.OutputFormat;
            enteros = f.Encoding == WaveFormatEncoding.Pcm;
            if (enteros ? f.BitsPerSample != 16 : (f.Encoding != WaveFormatEncoding.IeeeFloat || f.BitsPerSample != 32))
                throw new ArgumentException("formato de salida inesperado: " + f);
            bytes = new byte[1152 * 4 * Math.Max(1, canales) * 8];   // hasta ocho cuadros (el lector nunca pasa de dos)
        }
        public int Decodifica(Mp3Frame cuadro, ref float[] salida, int n)
        {
            int b = Descompresor.DecompressFrame(cuadro, bytes, 0);
            int m = enteros ? b / 2 : b / 4;
            if (salida.Length < n + m) Array.Resize(ref salida, Math.Max(n + m, salida.Length * 2));
            if (enteros) { for (int i = 0, j = 0; i < m; i++, j += 2) salida[n + i] = (short)(bytes[j] | (bytes[j + 1] << 8)) / 32768f; }
            else Buffer.BlockCopy(bytes, 0, salida, n * 4, m * 4);
            return m;
        }
        public void Vacia() { Descompresor.Reset(); }
        public void Dispose() { Descompresor.Dispose(); }
    }

    // NLAYER COMO DESCOMPRESOR DE CUADROS: cada cuadro de NAudio (Mp3Frame) se le da a NLayer tal cual (sus bits, sin la
    // cabecera ni el CRC). Da coma flotante de 32 bits a la misma frecuencia y con los mismos canales.
    internal sealed class MotorNLayer : IMp3FrameDecompressor
    {
        private readonly NLayer.MpegFrameDecoder dec = new NLayer.MpegFrameDecoder();
        private readonly CuadroNLayer cuadro = new CuadroNLayer();
        private readonly WaveFormat formato;
        public MotorNLayer(WaveFormat mp3) { formato = WaveFormat.CreateIeeeFloatWaveFormat(mp3.SampleRate, mp3.Channels); }
        public WaveFormat OutputFormat { get { return formato; } }
        public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
        {
            cuadro.Pon(frame);
            return dec.DecodeFrame(cuadro, dest, destOffset);
        }
        public void Reset() { dec.Reset(); }
        public void Dispose() { }
    }
    // Un cuadro de NAudio visto como lo pide NLayer: la cabecera ya leida y los bits de detras, uno a uno.
    internal sealed class CuadroNLayer : NLayer.IMpegFrame
    {
        private Mp3Frame f;
        private int leido, bits;
        private ulong cubo;
        internal void Pon(Mp3Frame frame) { f = frame; Reset(); }
        public int SampleRate { get { return f.SampleRate; } }
        public int SampleRateIndex { get { return (f.RawData[2] >> 2) & 3; } }
        public int FrameLength { get { return f.FrameLength; } }
        public int BitRate { get { return f.BitRate; } }
        public NLayer.MpegVersion Version
        {
            get { return f.MpegVersion == MpegVersion.Version1 ? NLayer.MpegVersion.Version1 : (f.MpegVersion == MpegVersion.Version2 ? NLayer.MpegVersion.Version2 : NLayer.MpegVersion.Version25); }
        }
        public NLayer.MpegLayer Layer
        {
            get { return f.MpegLayer == MpegLayer.Layer1 ? NLayer.MpegLayer.LayerI : (f.MpegLayer == MpegLayer.Layer2 ? NLayer.MpegLayer.LayerII : NLayer.MpegLayer.LayerIII); }
        }
        public NLayer.MpegChannelMode ChannelMode
        {
            get
            {
                switch (f.ChannelMode)
                {
                    case NAudio.Wave.ChannelMode.Stereo: return NLayer.MpegChannelMode.Stereo;
                    case NAudio.Wave.ChannelMode.JointStereo: return NLayer.MpegChannelMode.JointStereo;
                    case NAudio.Wave.ChannelMode.DualChannel: return NLayer.MpegChannelMode.DualChannel;
                    default: return NLayer.MpegChannelMode.Mono;
                }
            }
        }
        public int ChannelModeExtension { get { return f.ChannelExtension; } }
        public int SampleCount { get { return f.SampleCount; } }
        public int BitRateIndex { get { return (f.RawData[2] >> 4) & 0xF; } }
        public bool IsCopyrighted { get { return f.Copyright; } }
        public bool HasCrc { get { return f.CrcPresent; } }
        public bool IsCorrupted { get { return false; } }
        public void Reset() { leido = 4 + (f.CrcPresent ? 2 : 0); cubo = 0; bits = 0; }
        public int ReadBits(int cuantos)
        {
            if (cuantos < 1 || cuantos > 32) throw new ArgumentOutOfRangeException("cuantos");
            while (bits < cuantos)
            {
                if (leido >= f.FrameLength || leido >= f.RawData.Length) throw new EndOfStreamException();
                cubo = (cubo << 8) | f.RawData[leido++];
                bits += 8;
            }
            int v = (int)((cubo >> (bits - cuantos)) & ((1UL << cuantos) - 1));
            bits -= cuantos;
            return v;
        }
    }
    // EL DESCOMPRESOR QUE USA Mp3FileReader CUANDO NLAYER ESTA COMPROBADO. Entrega SIEMPRE coma flotante (el
    // lector se arma con ese formato y no puede cambiarlo a media cancion): de NLayer cuando se puede, y si no, del
    // de siempre convertido (ver arriba).
    internal sealed class DescompresorFlotante : IMp3FrameDecompressor
    {
        private readonly WaveFormat formato, mp3;
        private readonly int canales;
        private readonly MotorFlotante acm;
        private readonly Func<WaveFormat, IMp3FrameDecompressor> fabricaNl;   // null = solo el de siempre
        private MotorFlotante nl;
        private bool nlVetado;
        private MotorFlotante ultimo;               // el que descomprimio el cuadro anterior
        private readonly Mp3Frame[] historia = new Mp3Frame[3];
        private int nHistoria, hHistoria;           // cuantos hay y donde va el siguiente (circular)
        private float[] tmp, sobra = new float[0], descarte;
        private int nSobra;
        private bool cerrado;
        internal int CuadrosNl, CuadrosAcm, Cambios;   // (las pruebas miran quien descomprimio cada cuadro)

        public DescompresorFlotante(WaveFormat formatoMp3, IMp3FrameDecompressor descompresorAcm, Func<WaveFormat, IMp3FrameDecompressor> fabricaNLayer)
        {
            mp3 = formatoMp3; canales = Math.Max(1, formatoMp3.Channels);
            formato = WaveFormat.CreateIeeeFloatWaveFormat(formatoMp3.SampleRate, canales);
            acm = new MotorFlotante(descompresorAcm, canales);
            fabricaNl = fabricaNLayer;
            tmp = new float[1152 * canales * 2]; descarte = new float[1152 * canales * 2];
        }

        public WaveFormat OutputFormat { get { return formato; } }

        public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
        {
            MotorFlotante m = Elige();
            int n;
            if (m == acm)
            {
                if (ultimo != null && ultimo != acm) Calienta(acm);
                n = acm.Decodifica(frame, ref tmp, 0);
            }
            else
            {
                try
                {
                    if (ultimo != null && ultimo != m) Calienta(m);
                    n = m.Decodifica(frame, ref tmp, 0);
                }
                catch (Exception ex)
                {
                    // NLayer fallo: sigue el de siempre desde este mismo cuadro, calentado con los anteriores.
                    Veta("fallo a media cancion; sigue el de siempre desde ese cuadro", ex);
                    m = acm;
                    Calienta(acm);
                    n = acm.Decodifica(frame, ref tmp, 0);
                }
            }
            if (m == acm) CuadrosAcm++; else CuadrosNl++;
            ultimo = m;
            historia[hHistoria] = frame; hHistoria = (hHistoria + 1) % historia.Length;
            if (nHistoria < historia.Length) nHistoria++;
            return Entrega(n, dest, destOffset);
        }

        // NLayer solo si esta comprobado y no fallo en esta cancion.
        private MotorFlotante Elige()
        {
            if (fabricaNl == null || nlVetado) return acm;
            if (nl == null)
            {
                IMp3FrameDecompressor d = null;
                try { d = fabricaNl(mp3); nl = new MotorFlotante(d, canales); }
                catch (Exception ex)
                {
                    if (d != null && nl == null) { try { d.Dispose(); } catch { } }
                    Veta("no se pudo preparar para esta cancion", ex);
                    return acm;
                }
            }
            return nl;
        }

        // Un motor que entra despues de que descomprimiera el otro: se vacia y se le dan los ultimos cuadros (lo que
        // suelte con ellos se tira), como hace el lector tras un salto. Asi el cuadro siguiente sale entero.
        private void Calienta(MotorFlotante m)
        {
            Cambios++;
            m.Vacia();
            int i0 = (hHistoria - nHistoria + historia.Length) % historia.Length;
            for (int k = 0; k < nHistoria; k++) m.Decodifica(historia[(i0 + k) % historia.Length], ref descarte, 0);
        }

        private void Veta(string motivo, Exception ex)
        {
            nlVetado = true;
            MotorFlotante m = nl; nl = null;
            if (ultimo == m) ultimo = null;
            if (m != null) { try { m.Dispose(); } catch { } }
            DecodificacionFlotante.FalloEnVivo(motivo, ex);
        }

        // Lo que sale, como bytes de coma flotante. Si un motor solto mas de lo que cabe (no deberia), lo que sobra
        // sale la vez siguiente: no se pierde nada.
        private int Entrega(int n, byte[] dest, int off)
        {
            int cabe = (dest.Length - off) / 4;
            cabe -= cabe % canales;
            if (nSobra == 0 && n <= cabe) { Buffer.BlockCopy(tmp, 0, dest, off, n * 4); return n * 4; }
            int total = nSobra + n;
            float[] todo = new float[total];
            Array.Copy(sobra, 0, todo, 0, nSobra);
            Array.Copy(tmp, 0, todo, nSobra, n);
            int k = Math.Min(total, cabe);
            Buffer.BlockCopy(todo, 0, dest, off, k * 4);
            nSobra = total - k;
            if (sobra.Length < nSobra) sobra = new float[nSobra];
            Array.Copy(todo, k, sobra, 0, nSobra);
            return k * 4;
        }

        public void Reset()
        {
            acm.Vacia();
            if (nl != null) { try { nl.Vacia(); } catch (Exception ex) { Veta("fallo al saltar", ex); } }
            nHistoria = 0; nSobra = 0;
        }

        public void Dispose()
        {
            if (cerrado) return;
            cerrado = true;
            MotorFlotante m = nl; nl = null;
            if (m != null) { try { m.Dispose(); } catch { } }
            acm.Dispose();                          // el de siempre se cierra como siempre
        }
    }

    // LA DECISION, UNA VEZ POR SESION Y POR FORMATO.
    internal static class DecodificacionFlotante
    {
        // NLayer es C#: vale en cualquier Windows (y en Mono, para las pruebas).
        internal static bool Posible = true;
        internal static Func<WaveFormat, IMp3FrameDecompressor> FabricaNLayer = delegate(WaveFormat f) { return new MotorNLayer(f); };
        internal static Action<string> Apunte = null;   // las pruebas lo cambian para ver lo que se apuntaria
        internal static int Comprobaciones;         // cuantas veces se comparo con el de siempre en esta sesion
        internal const int MaxCuadros = 600;        // hasta ~15 s de cancion buscando musica que comparar
        internal const double UmbralDb = -50;       // NLayer vale si se aparta menos que esto del de siempre
        internal const double GastoMaximo = 0.03;   // y si descomprime gastando como mucho el 3 % de un nucleo
        internal const int CuadrosMedidos = 100;    // lo que se mide para saberlo (~2,6 s de musica), tras 16 de calentar
        // Que cancion lo necesita (Form1.LlegaAlMaximo: la que llega al maximo digital). null = ninguna.
        internal static Func<string, bool> LaNecesita = null;
        private static readonly object candado = new object();
        private static readonly Dictionary<int, int> veredictos = new Dictionary<int, int>();   // frecuencia*8+canales -> 1 / -1
        private static bool ausente;                // NLayer no se pudo preparar o no contesto: no se reintenta
        private static int fallosEnVivo;            // fallos a media cancion en la sesion
        private static readonly HashSet<string> apuntados = new HashSet<string>();

        // Para AbreMp3: el descompresor de siempre tal cual si esta cancion no lo necesita o en esta sesion no se usa
        // NLayer (el lector queda exactamente como antes), o el de coma flotante que lo lleva dentro. Sin ruta (el MP3
        // reconstruido en memoria, el clip de medir el relleno) siempre el de siempre.
        internal static IMp3FrameDecompressor Envuelve(WaveFormat mp3, IMp3FrameDecompressor acm, string ruta,
                                                       Func<WaveFormat, IMp3FrameDecompressor> acmNuevo)
        {
            try
            {
                Func<string, bool> necesita = LaNecesita;
                if (ruta == null || necesita == null || !necesita(ruta)) return acm;
                if (!Usar(mp3, ruta, acmNuevo)) return acm;
                return new DescompresorFlotante(mp3, acm, FabricaNLayer);
            }
            catch (Exception ex) { Apunta("MP3 en coma flotante: no se pudo armar; sigue el de siempre (" + Resumen(ex) + ")"); return acm; }
        }

        internal static bool Usar(WaveFormat mp3, string ruta, Func<WaveFormat, IMp3FrameDecompressor> acmNuevo)
        {
            if (!Posible || mp3 == null) return false;
            int clave = mp3.SampleRate * 8 + Math.Max(1, mp3.Channels);
            lock (candado)
            {
                if (ausente || Thread.VolatileRead(ref fallosEnVivo) >= 3) return false;
                int v;
                if (veredictos.TryGetValue(clave, out v)) return v > 0;
                if (ruta == null || acmNuevo == null) return false;
                string detalle;
                int r = CompruebaEnOtroHilo(mp3, ruta, acmNuevo, out detalle);
                Comprobaciones++;
                if (r != 0) veredictos[clave] = r;
                string nombre = ""; try { nombre = Path.GetFileName(ruta); } catch { }
                Apunta("MP3 en coma flotante (" + mp3.SampleRate + " Hz, " + mp3.Channels + " canales), comprobado con " + nombre + ": " + detalle);
                return r > 0;
            }
        }

        // La comparacion corre en un hilo propio (asi no para la pantalla) y con tiempo maximo. Si no
        // contesta, no se vuelve a intentar en la sesion.
        private static int CompruebaEnOtroHilo(WaveFormat mp3, string ruta, Func<WaveFormat, IMp3FrameDecompressor> acmNuevo, out string detalle)
        {
            int r = 0; string d = "no contesto a tiempo; se sigue con el de siempre";
            Thread t = new Thread(delegate()
            {
                try
                {
                    using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    { string dd; r = Compara(mp3, fs, acmNuevo, FabricaNLayer, out dd); d = dd; }
                }
                catch (Exception ex) { r = 0; d = "no se pudo leer (" + Resumen(ex) + "); se sigue con el de siempre"; }
            });
            t.IsBackground = true;
            try { t.SetApartmentState(ApartmentState.MTA); } catch { }
            t.Start();
            if (!t.Join(8000)) { ausente = true; detalle = d; return 0; }
            detalle = d;
            return r;
        }

        // Descomprime los mismos cuadros con los dos y los compara. 1 = NLayer da lo mismo; -1 = no, o no se
        // puede preparar; 0 = no se puede saber (poca musica, todo silencio, o el de siempre no se pudo usar).
        internal static int Compara(WaveFormat mp3, Stream datos, Func<WaveFormat, IMp3FrameDecompressor> acmNuevo,
                                    Func<WaveFormat, IMp3FrameDecompressor> nlNuevo, out string detalle)
        {
            MotorFlotante ma = null, mm = null;
            try
            {
                try { ma = Motor(acmNuevo, mp3); }
                catch (Exception ex) { detalle = "no se pudo preparar el de siempre para comparar (" + Resumen(ex) + ")"; return 0; }
                try { mm = Motor(nlNuevo, mp3); }
                catch (Exception ex)
                {
                    ausente = true;
                    detalle = "NLayer no se pudo preparar (" + Resumen(ex) + "); se sigue con el de siempre";
                    return -1;
                }
                try { Id3v2Tag.ReadTag(datos); } catch { }   // saltar la etiqueta, como hace el lector
                int ch = Math.Max(1, mp3.Channels);
                float[] a = new float[1152 * ch * 64], m = new float[1152 * ch * 64];
                int na = 0, nm = 0;
                string juicio = "sin cuadros que comparar";
                System.Diagnostics.Stopwatch reloj = new System.Diagnostics.Stopwatch();
                int medidos = 0; long muestrasMedidas = 0;   // cuadros y muestras de NLayer cronometrados
                for (int k = 0; k < MaxCuadros; k++)
                {
                    Mp3Frame f;
                    try { f = Mp3Frame.LoadFromStream(datos); }
                    catch (EndOfStreamException) { f = null; }
                    if (f == null) break;
                    if (f.SampleRate != mp3.SampleRate || (f.ChannelMode == ChannelMode.Mono ? 1 : 2) != ch) continue;
                    na += ma.Decodifica(f, ref a, na);
                    bool cronometra = k >= 16;   // los primeros, calentando (la primera vez tambien se compila NLayer)
                    try
                    {
                        if (cronometra) reloj.Start();
                        int d = mm.Decodifica(f, ref m, nm);
                        if (cronometra) { reloj.Stop(); medidos++; muestrasMedidas += d; }
                        nm += d;
                    }
                    catch (Exception ex) { detalle = "NLayer fallo al comparar (" + Resumen(ex) + "); se sigue con el de siempre"; return -1; }
                    if ((k & 15) == 15)
                    {
                        int r = Juzga(a, na, m, nm, ch, out juicio);
                        if (r < 0 || (r > 0 && medidos >= CuadrosMedidos)) return Gasto(r, juicio, reloj, muestrasMedidas, ch, mp3.SampleRate, out detalle);
                    }
                }
                int rf = Juzga(a, na, m, nm, ch, out juicio);
                if (rf > 0 && medidos < CuadrosMedidos) rf = 0;   // poca musica para saber lo que gasta: con otra cancion
                return Gasto(rf, juicio, reloj, muestrasMedidas, ch, mp3.SampleRate, out detalle);
            }
            catch (Exception ex) { detalle = "la comparacion fallo (" + Resumen(ex) + "); se sigue con el de siempre"; return 0; }
            finally
            {
                if (mm != null) { try { mm.Dispose(); } catch { } }
                if (ma != null) { try { ma.Dispose(); } catch { } }
            }
        }

        // Un descompresor recien hecho, envuelto; si el envoltorio no lo acepta, se cierra ahi mismo.
        private static MotorFlotante Motor(Func<WaveFormat, IMp3FrameDecompressor> fabrica, WaveFormat mp3)
        {
            IMp3FrameDecompressor d = fabrica(mp3);
            try { return new MotorFlotante(d, mp3.Channels); }
            catch { try { d.Dispose(); } catch { } throw; }
        }

        // Si NLayer da lo mismo pero gasta demasiado en este equipo, tampoco se usa.
        internal static int Gasto(int r, string juicio, System.Diagnostics.Stopwatch reloj, long muestras, int ch, int tasa, out string detalle)
        {
            double musica = (double)muestras / Math.Max(1, ch) / Math.Max(1, tasa);
            double gasto = (musica > 0) ? reloj.Elapsed.TotalSeconds / musica : 0;
            string g = (gasto * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            if (r > 0 && gasto > GastoMaximo)
            {
                detalle = juicio + ", pero gasta el " + g + " % de un nucleo (tope 3 %): NO se usa, sigue el de siempre";
                return -1;
            }
            detalle = Explica(r, juicio) + ((r > 0) ? " (gasta el " + g + " % de un nucleo)" : "");
            return r;
        }

        private static string Explica(int r, string juicio)
        {
            if (r > 0) return juicio + ": en uso";
            if (r < 0) return juicio + ": NO se usa, sigue el de siempre";
            return "no se pudo saber (" + juicio + "); sigue el de siempre y se probara con otra cancion";
        }

        // El juicio. Primero, que lleven la MISMA cuenta de muestras: uno que suelta cada cuadro con uno de retraso da la
        // misma tira seguida, pero con el lector se comeria el ultimo cuadro de cada cancion y cada salto caeria un
        // cuadro antes. Despues, desde el quinto cuadro (cada uno arranca a su manera), la diferencia entre los dos
        // contra lo que suena, sin contar lo que el de siempre recorto (ahi NLayer tiene que pasar de 1,0: es lo
        // que se busca). Un cuadro de desplazamiento sale a ~0 dB; dos decodificadores bien hechos, a mas de 70 dB por
        // debajo.
        internal static int Juzga(float[] a, int na, float[] m, int nm, int ch, out string juicio)
        {
            if (na >= 4 * 1152 * ch && nm != na)
            {
                juicio = "suelta " + Math.Abs(na - nm) / ch + " muestras " + (nm < na ? "por detras" : "por delante") + " del de siempre";
                return -1;
            }
            int desde = 4 * 1152 * ch, hasta = Math.Min(na, nm);
            if (hasta - desde < 20 * 1152 * ch) { juicio = "aun poca musica"; return 0; }
            double sr = 0, sd = 0; long n = 0;
            const float techo = 32767f / 32768f;
            for (int i = desde; i < hasta; i++)
            {
                float x = a[i];
                if (x >= techo || x <= -1f) continue;
                double d = (double)x - m[i];
                sr += (double)x * x; sd += d * d; n++;
            }
            if (n == 0) { juicio = "todo recortado"; return 0; }
            double rr = Math.Sqrt(sr / n), rd = Math.Sqrt(sd / n);
            if (rr < 1e-3) { juicio = "todo silencio hasta ahora"; return 0; }
            double db = 20 * Math.Log10(rd / rr + 1e-12);
            juicio = "se aparta " + db.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " dB del de siempre";
            return db <= UmbralDb ? 1 : -1;
        }

        // Un fallo de NLayer a media cancion. Tres en la sesion y no se vuelve a usar. Sin el candado: esto corre
        // en el hilo del sonido, y el candado puede estar tomado por una comparacion (hasta 8 s).
        internal static void FalloEnVivo(string motivo, Exception ex)
        {
            Interlocked.Increment(ref fallosEnVivo);
            Apunta("MP3 en coma flotante: NLayer " + motivo + " (" + Resumen(ex) + ")");
        }

        private static string Resumen(Exception ex)
        {
            if (ex == null) return "sin detalle";
            string m = ex.Message ?? "";
            if (m.Length > 160) m = m.Substring(0, 160);
            return ex.GetType().Name + ": " + m;
        }

        // Al archivo de errores de la rocola, UNA vez por texto en la sesion (con el mismo sello de version que los
        // demas apuntes: ver Program.LogCrash).
        internal static void Apunta(string texto)
        {
            Action<string> a = Apunte;
            if (a != null) { a(texto); return; }
            lock (apuntados) { if (!apuntados.Add(texto)) return; }
            try
            {
                string f = Path.Combine(Form1.CarpetaDatos, "rocola_error.txt");
                try { FileInfo fi = new FileInfo(f); if (fi.Exists && fi.Length > 262144) fi.Delete(); } catch { }
                string sello = "";
                try { sello = Form1.DeCuandoEs(); } catch { }
                if (sello.Length > 0) sello = "  (rocola del " + sello + ")";
                File.AppendAllText(f, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [Decodificacion]" + sello + "\r\n" + texto + "\r\n\r\n");
            }
            catch { }
        }
    }
}

namespace Rocola
{
    public partial class Form1
    {
        // LA CANCION LLEGA AL MAXIMO DIGITAL: su pico medido (Sonoridad.cs, medida integrada de algoritmo 2) es de
        // -0,05 dBFS o mas. Con el descompresor de siempre esas son las que se recortan; las demas no tienen que.
        internal const float PicoQueRecorta = -0.05f;
        private bool LlegaAlMaximo(string ruta)
        {
            float pico, p99;
            if (!PicosParaNivelar(ruta, out pico, out p99)) return false;
            return PicosBloques.Db(pico) >= PicoQueRecorta;
        }
    }
}
