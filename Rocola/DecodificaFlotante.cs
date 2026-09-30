// DECODIFICAR EN COMA FLOTANTE (solo HD): lo que el MP3 trae por encima del maximo digital ya no se recorta en seco
//
// Los MP3 se descomprimian con el descompresor de ACM de Windows, que entrega enteros de 16 bits. Un MP3 hecho de un
// master fuerte trae picos POR ENCIMA del maximo digital (medido con LAME: de +0,3 dB en un master moderado a +3,5 dB
// en uno recortado a proposito, con hasta el 2,8 % de las muestras por encima), y el entero de 16 bits no los
// admite: se cortaban en seco ANTES de que la rocola pudiera bajar el volumen, y eso es distorsion que el nivelado
// ya no podia quitar. En AAC, WMA y M4A pasaba lo mismo: Media Foundation los entregaba tambien en 16 bits.
//
// Ahora, en HD:
// 1) MP3. El lector de NAudio (Mp3FileReader) sigue siendo el mismo -su tabla de cuadros, la duracion exacta, los
//    saltos, el camino tolerante, la continuidad sin pausas-; solo cambia QUIEN descomprime cada cuadro: el
//    decodificador de MP3 de Windows (mp3dmod.dll) usado como MFT, que es la forma en que Microsoft documenta que da
//    coma flotante (como DMO solo da enteros). Pero no se usa a ciegas:
//      - Se COMPARA con el de siempre una vez por sesion y por formato (frecuencia y canales), con la primera cancion
//        que se abre: los dos descomprimen los mismos cuadros y lo del de Windows tiene que coincidir con lo del de
//        siempre -muestra a muestra y sin desplazarse ni un cuadro- salvo lo que el de siempre recorto. Si no
//        coincide, o no se puede preparar, esa sesion sigue con el de siempre.
//      - Solo trabaja en hilos que no son STA (el de la salida WASAPI lo es de sobra). En un STA -la salida clasica
//        por WaveOut, que se rellena desde la pantalla- descomprime el de siempre: un objeto de Windows creado en un
//        STA solo se puede usar desde ese hilo, y aqui la cancion puede cambiar de hilo si cambia la salida.
//      - Si falla a media cancion, sigue el de siempre desde ese mismo cuadro, calentado con los tres anteriores
//        (lo mismo que hace el lector tras un salto): la cancion no se corta ni se repite nada. Queda apuntado en
//        rocola_error.txt; con tres fallos en la sesion ya no se vuelve a usar.
//    Cuando descomprime el de siempre, se pasa a coma flotante con la MISMA cuenta que hacia NAudio (entero / 32768):
//    sale lo mismo que antes, bit a bit. Y si en esta sesion no se usa el de Windows, el lector se arma exactamente
//    como antes, con el descompresor de siempre y nada mas.
// 2) AAC, M4A, WMA y el sonido de los videos (lo que no es MP3, WAV ni AIFF): Media Foundation pidiendo coma
//    flotante al decodificador. Es el mismo decodificador de antes con otra salida, asi que el tiempo no cambia. Si
//    no la da, por el camino de siempre (AudioFileReader).
// Lo que el MP3 trae por encima de 1,0 sigue por la cadena en coma flotante: el nivelado lo baja y el limitador de
// pico real del final no deja pasar nada del techo.
//
// PRUEBAS: pruebas/flotante.cs cubre la logica con decodificadores sinteticos y, en Windows, compara el codec real.
// No todos los codecs instalados entregan muestras alineadas con el ACM: si se rechaza, la prueba exige que la
// alternativa real conserve su PCM y duracion exactos; no fuerza a habilitarlo. La linea "MP3 en coma flotante"
// de rocola_error.txt dice en cada maquina que decidio y por que. Un PASS de la alternativa no acredita una mejora
// flotante real en esa maquina.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace Rocola
{
    // Lo que suelta un descompresor de cuadros MP3 (de NAudio: IMp3FrameDecompressor), en coma flotante: el de
    // siempre da enteros de 16 bits y se pasan a coma flotante con la MISMA cuenta que hace NAudio
    // (Pcm16BitToSampleProvider: entero / 32768f), asi que sale lo mismo que antes, bit a bit; el de Windows ya da
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

    // EL DE WINDOWS, EN COMA FLOTANTE: el decodificador de MP3 de Windows (CLSID_CMP3DecMediaObject, mp3dmod.dll).
    // Microsoft documenta que se porta como DMO (solo enteros) salvo que se le pida su interfaz de MFT, y que como MFT
    // da coma flotante (WAVE_FORMAT_IEEE_FLOAT). Se le pide eso y nada mas: entrada el MP3 tal cual lo describe el
    // lector, salida coma flotante de 32 bits a la misma frecuencia y con los mismos canales. Un cuadro entra, se saca
    // todo lo que suelte; si no cabe en lo que le dan, lo que sobra sale la vez siguiente.
    internal sealed class MotorMft : IMp3FrameDecompressor
    {
        private static readonly Guid ClsidMp3 = new Guid("bbeea841-0a63-4f52-a7ab-a9b3a84ed38a");
        private object com;                         // el objeto de Windows (se suelta una vez)
        private IMFTransform mft;
        private readonly WaveFormat formato;
        private readonly bool ponMuestras;          // el MFT pone sus propias muestras de salida
        private readonly int tamSalida;             // bytes de la muestra de salida que se le da
        private long tiempo;                        // marca de tiempo del cuadro que entra (100 ns)
        private byte[] sobra = new byte[0];         // lo que solto y no cupo
        private int nSobra;

        public MotorMft(WaveFormat mp3)
        {
            formato = WaveFormat.CreateIeeeFloatWaveFormat(mp3.SampleRate, mp3.Channels);
            MediaFoundationApi.Startup();
            com = Activator.CreateInstance(Type.GetTypeFromCLSID(ClsidMp3, true));
            try
            {
                mft = (IMFTransform)com;            // pedirle esta interfaz es lo que lo hace portarse como MFT
                IMFMediaType ent = MediaFoundationApi.CreateMediaTypeFromWaveFormat(mp3);
                try { mft.SetInputType(0, ent, _MFT_SET_TYPE_FLAGS.None); } finally { Marshal.ReleaseComObject(ent); }
                IMFMediaType sal = MediaFoundationApi.CreateMediaTypeFromWaveFormat(formato);
                try { mft.SetOutputType(0, sal, _MFT_SET_TYPE_FLAGS.None); } finally { Marshal.ReleaseComObject(sal); }
                MFT_OUTPUT_STREAM_INFO info;
                mft.GetOutputStreamInfo(0, out info);
                ponMuestras = (info.dwFlags & _MFT_OUTPUT_STREAM_INFO_FLAGS.MFT_OUTPUT_STREAM_PROVIDES_SAMPLES) != 0;
                tamSalida = Math.Max(info.cbSize, 1152 * 4 * Math.Max(1, mp3.Channels) * 2);
                mft.ProcessMessage(MFT_MESSAGE_TYPE.MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, IntPtr.Zero);
                mft.ProcessMessage(MFT_MESSAGE_TYPE.MFT_MESSAGE_NOTIFY_START_OF_STREAM, IntPtr.Zero);
            }
            catch { Suelta(); throw; }
        }

        public WaveFormat OutputFormat { get { return formato; } }

        public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
        {
            int largo = frame.FrameLength;
            IMFMediaBuffer b = MediaFoundationApi.CreateMemoryBuffer(largo);
            IMFSample s = null;
            try
            {
                IntPtr p; int max, cur;
                b.Lock(out p, out max, out cur);
                try { Marshal.Copy(frame.RawData, 0, p, largo); }
                finally { b.Unlock(); }
                b.SetCurrentLength(largo);
                s = MediaFoundationApi.CreateSample();
                s.AddBuffer(b);
                long dur = 10000000L * frame.SampleCount / Math.Max(1, frame.SampleRate);
                s.SetSampleTime(tiempo); s.SetSampleDuration(dur); tiempo += dur;
                mft.ProcessInput(0, s, 0);
            }
            finally
            {
                if (s != null) Marshal.ReleaseComObject(s);
                Marshal.ReleaseComObject(b);
            }
            for (int vueltas = 0; vueltas < 16; vueltas++)   // todo lo que suelte (un cuadro da una o dos salidas)
                if (!UnaSalida()) break;
            int dar = Math.Min(nSobra, dest.Length - destOffset);
            dar -= dar % formato.BlockAlign;
            Buffer.BlockCopy(sobra, 0, dest, destOffset, dar);
            nSobra -= dar;
            if (nSobra > 0) Buffer.BlockCopy(sobra, dar, sobra, 0, nSobra);
            return dar;
        }

        // Una salida del MFT, a la cola de lo que se entrega. false si pide mas entrada.
        private bool UnaSalida()
        {
            MFT_OUTPUT_DATA_BUFFER[] ob = new MFT_OUTPUT_DATA_BUFFER[1];
            IMFSample mia = null; IMFMediaBuffer buf = null;
            if (!ponMuestras)
            {
                mia = MediaFoundationApi.CreateSample();
                buf = MediaFoundationApi.CreateMemoryBuffer(tamSalida);
                mia.AddBuffer(buf);
                ob[0].pSample = mia;
            }
            try
            {
                _MFT_PROCESS_OUTPUT_STATUS st;
                int hr = mft.ProcessOutput(_MFT_PROCESS_OUTPUT_FLAGS.None, 1, ob, out st);
                if (hr == MediaFoundationErrors.MF_E_TRANSFORM_NEED_MORE_INPUT) return false;
                if (hr != 0) Marshal.ThrowExceptionForHR(hr);   // incluido un cambio de formato: no se renegocia, se deja
                IMFSample res = ob[0].pSample;
                if (res == null) return true;
                IMFMediaBuffer cont;
                res.ConvertToContiguousBuffer(out cont);
                try
                {
                    IntPtr p; int max, cur;
                    cont.Lock(out p, out max, out cur);
                    try
                    {
                        if (sobra.Length < nSobra + cur) Array.Resize(ref sobra, Math.Max(nSobra + cur, sobra.Length * 2));
                        Marshal.Copy(p, sobra, nSobra, cur);
                        nSobra += cur;
                        return true;
                    }
                    finally { cont.Unlock(); }
                }
                finally
                {
                    Marshal.ReleaseComObject(cont);
                    if (ponMuestras) Marshal.ReleaseComObject(res);
                }
            }
            finally
            {
                if (ob[0].pEvents != null) { try { Marshal.ReleaseComObject(ob[0].pEvents); } catch { } }
                if (mia != null) { try { mia.RemoveAllBuffers(); } catch { } Marshal.ReleaseComObject(mia); }
                if (buf != null) Marshal.ReleaseComObject(buf);
            }
        }

        public void Reset()
        {
            mft.ProcessMessage(MFT_MESSAGE_TYPE.MFT_MESSAGE_COMMAND_FLUSH, IntPtr.Zero);
            tiempo = 0; nSobra = 0;
        }

        public void Dispose()
        {
            if (mft != null) { try { mft.ProcessMessage(MFT_MESSAGE_TYPE.MFT_MESSAGE_NOTIFY_END_STREAMING, IntPtr.Zero); } catch { } }
            Suelta();
        }
        private void Suelta()
        {
            object c = com; com = null; mft = null;
            if (c != null) { try { Marshal.ReleaseComObject(c); } catch { } }
        }
    }

    // EL DESCOMPRESOR QUE USA Mp3FileReader CUANDO EL DE WINDOWS ESTA COMPROBADO. Entrega SIEMPRE coma flotante (el
    // lector se arma con ese formato y no puede cambiarlo a media cancion): del de Windows cuando se puede, y si no, del
    // de siempre convertido (ver arriba).
    internal sealed class DescompresorFlotante : IMp3FrameDecompressor
    {
        private readonly WaveFormat formato, mp3;
        private readonly int canales;
        private readonly MotorFlotante acm;
        private readonly Func<WaveFormat, IMp3FrameDecompressor> fabricaMft;   // null = solo el de siempre
        private readonly bool administrado;
        private MotorFlotante mft;
        private bool mftVetado;
        private MotorFlotante ultimo;               // el que descomprimio el cuadro anterior
        private readonly Mp3Frame[] historia = new Mp3Frame[3];
        private int nHistoria, hHistoria;           // cuantos hay y donde va el siguiente (circular)
        private float[] tmp, sobra = new float[0], descarte;
        private int nSobra;
        private bool cerrado;
        internal int CuadrosMft, CuadrosAcm, Cambios;   // (las pruebas miran quien descomprimio cada cuadro)

        public DescompresorFlotante(WaveFormat formatoMp3, IMp3FrameDecompressor descompresorAcm, Func<WaveFormat, IMp3FrameDecompressor> fabricaWindows)
        {
            mp3 = formatoMp3; canales = Math.Max(1, formatoMp3.Channels);
            formato = WaveFormat.CreateIeeeFloatWaveFormat(formatoMp3.SampleRate, canales);
            acm = new MotorFlotante(descompresorAcm, canales);
            fabricaMft = fabricaWindows;
            administrado = fabricaWindows == DecodificacionFlotante.FabricaNLayer;
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
                    // El de Windows fallo: sigue el de siempre desde este mismo cuadro, calentado con los anteriores.
                    Veta("fallo a media cancion; sigue el de siempre desde ese cuadro", ex);
                    m = acm;
                    Calienta(acm);
                    n = acm.Decodifica(frame, ref tmp, 0);
                }
            }
            if (m == acm) CuadrosAcm++; else CuadrosMft++;
            ultimo = m;
            historia[hHistoria] = frame; hHistoria = (hHistoria + 1) % historia.Length;
            if (nHistoria < historia.Length) nHistoria++;
            return Entrega(n, dest, destOffset);
        }

        // El de Windows solo si esta comprobado, no fallo en esta cancion y el hilo no es un STA (ver arriba).
        private MotorFlotante Elige()
        {
            if (fabricaMft == null || mftVetado) return acm;
            if (!administrado && Thread.CurrentThread.GetApartmentState() == ApartmentState.STA) return acm;
            if (mft == null)
            {
                IMp3FrameDecompressor d = null;
                try { d = fabricaMft(mp3); mft = new MotorFlotante(d, canales); }
                catch (Exception ex)
                {
                    if (d != null && mft == null) { try { d.Dispose(); } catch { } }
                    Veta("no se pudo preparar para esta cancion", ex);
                    return acm;
                }
            }
            return mft;
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
            mftVetado = true;
            MotorFlotante m = mft; mft = null;
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
            if (mft != null) { try { mft.Vacia(); } catch (Exception ex) { Veta("fallo al saltar", ex); } }
            nHistoria = 0; nSobra = 0;
        }

        public void Dispose()
        {
            if (cerrado) return;
            cerrado = true;
            MotorFlotante m = mft; mft = null;
            if (m != null) { try { m.Dispose(); } catch { } }
            acm.Dispose();                          // el de siempre se cierra como siempre
        }
    }

    // LA DECISION, UNA VEZ POR SESION Y POR FORMATO, y el lector de coma flotante para lo que no es MP3.
    internal static class DecodificacionFlotante
    {
        // Solo en Windows 7 o posterior: la rama HD pide .NET 4.8, que no corre en nada anterior.
        internal static bool Posible = Environment.OSVersion.Platform == PlatformID.Win32NT
                                       && Environment.OSVersion.Version >= new Version(6, 1);
        internal static Func<WaveFormat, IMp3FrameDecompressor> FabricaMft = delegate(WaveFormat f) { return new MotorMft(f); };
        internal static Func<WaveFormat, IMp3FrameDecompressor> FabricaNLayer = delegate(WaveFormat f) { return new MotorNLayer(f); };
        internal static Action<string> Apunte = null;   // las pruebas lo cambian para ver lo que se apuntaria
        internal static int Comprobaciones;         // cuantas veces se comparo con el de siempre en esta sesion
        internal const int MaxCuadros = 600;        // hasta ~15 s de cancion buscando musica que comparar
        internal const double UmbralDb = -50;       // el de Windows vale si se aparta menos que esto del de siempre
        private static readonly object candado = new object();
        private static readonly Dictionary<int, int> veredictos = new Dictionary<int, int>();   // frecuencia*8+canales -> 1 MFT / 2 NLayer / -1 rechazo
        private static bool ausente;                // el de Windows no se pudo preparar o no contesto: no se reintenta
        private static bool comparacionAtascada;
        private static int fallosEnVivo;            // fallos a media cancion en la sesion
        private static readonly HashSet<string> apuntados = new HashSet<string>();

        // Para AbreMp3: el descompresor de siempre tal cual si en esta sesion no se usa el de Windows (el lector queda
        // exactamente como antes), o el de coma flotante que lo lleva dentro. 'ruta' es null para el MP3 reconstruido
        // en memoria: ese usa lo ya decidido, sin comprobar nada.
        internal static IMp3FrameDecompressor Envuelve(WaveFormat mp3, IMp3FrameDecompressor acm, string ruta,
                                                       Func<WaveFormat, IMp3FrameDecompressor> acmNuevo)
        {
            try
            {
                if (Usar(mp3, ruta, acmNuevo)) return new DescompresorFlotante(mp3, acm, FabricaMft);
                int v;
                lock (candado)
                    if (Posible && !comparacionAtascada && Thread.VolatileRead(ref fallosEnVivo) < 3 && mp3 != null && veredictos.TryGetValue(mp3.SampleRate * 8 + Math.Max(1, mp3.Channels), out v) && v == 2)
                        return new DescompresorFlotante(mp3, acm, FabricaNLayer);
                return acm;
            }
            catch (Exception ex) { Apunta("MP3 en coma flotante: no se pudo armar; sigue el de siempre (" + Resumen(ex) + ")"); return acm; }
        }

        internal static bool Usar(WaveFormat mp3, string ruta, Func<WaveFormat, IMp3FrameDecompressor> acmNuevo)
        {
            if (!Posible || mp3 == null) return false;
            int clave = mp3.SampleRate * 8 + Math.Max(1, mp3.Channels);
            lock (candado)
            {
                if (comparacionAtascada || Thread.VolatileRead(ref fallosEnVivo) >= 3) return false;
                int v;
                if (veredictos.TryGetValue(clave, out v)) return v == 1;
                if (ruta == null || acmNuevo == null) return false;
                string detalle;
                int r = CompruebaEnOtroHilo(mp3, ruta, acmNuevo, out detalle);
                Comprobaciones++;
                if (r != 0) veredictos[clave] = r;
                string nombre = ""; try { nombre = Path.GetFileName(ruta); } catch { }
                Apunta("MP3 en coma flotante (" + mp3.SampleRate + " Hz, " + mp3.Channels + " canales), comprobado con " + nombre + ": " + detalle);
                return r == 1;
            }
        }

        // La comparacion corre en un hilo propio (no STA: ahi se puede crear el de Windows) y con tiempo maximo. Si no
        // contesta, no se vuelve a intentar en la sesion.
        private static int CompruebaEnOtroHilo(WaveFormat mp3, string ruta, Func<WaveFormat, IMp3FrameDecompressor> acmNuevo, out string detalle)
        {
            int r = 0; string d = "no contesto a tiempo; se sigue con el de siempre";
            Thread t = new Thread(delegate()
            {
                try
                {
                    using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        string dd = "MFT no disponible";
                        r = ausente ? -1 : Compara(mp3, fs, acmNuevo, FabricaMft, out dd); d = dd;
                        if (r < 0)
                        {
                            fs.Position = 0;
                            int nl = Compara(mp3, fs, acmNuevo, FabricaNLayer, out dd);
                            r = nl > 0 ? 2 : nl;
                            d += "; alternativa NLayer: " + dd;
                        }
                    }
                }
                catch (Exception ex) { r = 0; d = "no se pudo leer (" + Resumen(ex) + "); se sigue con el de siempre"; }
            });
            t.IsBackground = true;
            try { t.SetApartmentState(ApartmentState.MTA); } catch { }
            t.Start();
            if (!t.Join(8000)) { comparacionAtascada = true; detalle = d; return 0; }
            detalle = d;
            return r;
        }

        // Descomprime los mismos cuadros con los dos y los compara. 1 = el de Windows da lo mismo; -1 = no, o no se
        // puede preparar; 0 = no se puede saber (poca musica, todo silencio, o el de siempre no se pudo usar).
        internal static int Compara(WaveFormat mp3, Stream datos, Func<WaveFormat, IMp3FrameDecompressor> acmNuevo,
                                    Func<WaveFormat, IMp3FrameDecompressor> mftNuevo, out string detalle)
        {
            MotorFlotante ma = null, mm = null;
            try
            {
                try { ma = Motor(acmNuevo, mp3); }
                catch (Exception ex) { detalle = "no se pudo preparar el de siempre para comparar (" + Resumen(ex) + ")"; return 0; }
                try { mm = Motor(mftNuevo, mp3); }
                catch (Exception ex)
                {
                    if (mftNuevo != FabricaNLayer) ausente = true;
                    detalle = "el decodificador " + (mftNuevo == FabricaNLayer ? "NLayer" : "de Windows") + " no se pudo preparar (" + Resumen(ex) + "); se sigue con el de siempre";
                    return -1;
                }
                try { Id3v2Tag.ReadTag(datos); } catch { }   // saltar la etiqueta, como hace el lector
                int ch = Math.Max(1, mp3.Channels);
                float[] a = new float[1152 * ch * 64], m = new float[1152 * ch * 64];
                int na = 0, nm = 0;
                bool mideGasto = mftNuevo == FabricaNLayer;
                long ticksGasto = 0, muestrasGasto = 0;
                int cuadrosGasto = 0;
                string juicio = "sin cuadros que comparar";
                for (int k = 0; k < MaxCuadros; k++)
                {
                    Mp3Frame f;
                    try { f = Mp3Frame.LoadFromStream(datos); }
                    catch (EndOfStreamException) { f = null; }
                    if (f == null) break;
                    if (f.SampleRate != mp3.SampleRate || (f.ChannelMode == ChannelMode.Mono ? 1 : 2) != ch) continue;
                    na += ma.Decodifica(f, ref a, na);
                    try
                    {
                        long antes = mideGasto && k >= 16 ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                        int n = mm.Decodifica(f, ref m, nm); nm += n;
                        if (antes != 0) { ticksGasto += System.Diagnostics.Stopwatch.GetTimestamp() - antes; muestrasGasto += n; cuadrosGasto++; }
                    }
                    catch (Exception ex) { detalle = (mideGasto ? "NLayer" : "el de Windows") + " fallo al comparar (" + Resumen(ex) + "); se sigue con el de siempre"; return -1; }
                    if ((k & 15) == 15)
                    {
                        int r = Juzga(a, na, m, nm, ch, out juicio);
                        if (r < 0 || (r > 0 && (!mideGasto || cuadrosGasto >= 100)))
                        { detalle = Explica(r, juicio); return RevisaGasto(r, mideGasto, ticksGasto, muestrasGasto, ch, mp3.SampleRate, ref detalle); }
                    }
                }
                int rf = Juzga(a, na, m, nm, ch, out juicio);
                if (mideGasto && rf > 0 && cuadrosGasto < 100) rf = 0;
                detalle = Explica(rf, juicio);
                return RevisaGasto(rf, mideGasto, ticksGasto, muestrasGasto, ch, mp3.SampleRate, ref detalle);
            }
            catch (Exception ex) { detalle = "la comparacion fallo (" + Resumen(ex) + "); se sigue con el de siempre"; return 0; }
            finally
            {
                if (mm != null) { try { mm.Dispose(); } catch { } }
                if (ma != null) { try { ma.Dispose(); } catch { } }
            }
        }

        private static int RevisaGasto(int resultado, bool medir, long ticks, long muestras, int ch, int hz, ref string detalle)
        {
            if (!medir || resultado <= 0) return resultado;
            double gasto = muestras > 0 ? ticks / (double)System.Diagnostics.Stopwatch.Frequency / (muestras / (double)ch / hz) : double.PositiveInfinity;
            detalle += "; costo NLayer " + (100 * gasto).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "% de un nucleo";
            if (gasto <= .03) return resultado;
            detalle += ": supera presupuesto 3%, sigue ACM"; return -1;
        }

        // Un descompresor recien hecho, envuelto; si el envoltorio no lo acepta, se cierra ahi mismo.
        private static MotorFlotante Motor(Func<WaveFormat, IMp3FrameDecompressor> fabrica, WaveFormat mp3)
        {
            IMp3FrameDecompressor d = fabrica(mp3);
            try { return new MotorFlotante(d, mp3.Channels); }
            catch { try { d.Dispose(); } catch { } throw; }
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
        // contra lo que suena, sin contar lo que el de siempre recorto (ahi el de Windows tiene que pasar de 1,0: es lo
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
                if (float.IsNaN(m[i]) || float.IsInfinity(m[i]) || Math.Abs(m[i]) > 8f)
                { juicio = "muestras flotantes invalidas"; return -1; }
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

        // Un fallo del de Windows a media cancion. Tres en la sesion y no se vuelve a usar. Sin el candado: esto corre
        // en el hilo del sonido, y el candado puede estar tomado por una comparacion (hasta 8 s).
        internal static void FalloEnVivo(string motivo, Exception ex)
        {
            Interlocked.Increment(ref fallosEnVivo);
            Apunta("MP3 en coma flotante: el decodificador alternativo " + motivo + " (" + Resumen(ex) + ")");
        }

        // LO QUE NO ES MP3 NI WAV/AIFF (AAC, M4A, WMA, el sonido de los videos): Media Foundation pidiendo coma
        // flotante. null = por el camino de siempre (AudioFileReader), que es tambien Media Foundation pero en 16 bits.
        internal static WaveStream AbreOtro(string ruta)
        {
            if (!Posible || ruta == null) return null;
            string ext = "";
            try { ext = Path.GetExtension(ruta).ToLowerInvariant(); } catch { }
            if (ext == ".mp3" || ext == ".wav" || ext == ".aif" || ext == ".aiff") return null;
            MediaFoundationReader r = null;
            try
            {
                MediaFoundationReader.MediaFoundationReaderSettings ajustes = new MediaFoundationReader.MediaFoundationReaderSettings();
                ajustes.RequestFloatOutput = true;
                r = new MediaFoundationReader(ruta, ajustes);
                WaveFormat f = r.WaveFormat;
                if (f.Encoding != WaveFormatEncoding.IeeeFloat || f.BitsPerSample != 32)
                {
                    Apunta("Coma flotante en " + ext + ": el decodificador no la dio (" + f + "); por el camino de siempre");
                    return null;   // (el finally lo cierra)
                }
                MediaFoundationReader listo = r; r = null;
                return listo;
            }
            catch (Exception ex)
            {
                Apunta("Coma flotante en " + ext + ": no se pudo abrir asi (" + Resumen(ex) + "); por el camino de siempre");
                return null;
            }
            finally { if (r != null) { try { r.Dispose(); } catch { } } }
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
                Bitacora.ApuntaConSello(Path.Combine(Form1.CarpetaDatos, "rocola_error.txt"), 262144,   // detras (MedidasBitacora.cs)
                                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [Decodificacion]", "\r\n" + texto + "\r\n\r\n");
            }
            catch { }
        }
    }
}
