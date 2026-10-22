// LOS MP3 DEL REPASO, SIN BASURA (13 de octubre de 2026; las dos ramas).
//
// El repaso mide cada cancion leyendola entera con el lector de MP3 de NAudio 1.9 (Mp3FileReader). Ese lector crea por
// CADA cuadro del archivo (unos 38 por segundo de cancion) un Mp3Frame nuevo, un arreglo de 4 bytes para la cabecera y
// otro con los datos del cuadro, y los tira enseguida. Medido: 5,8 MB por cancion de 200 s a 192 kbps y 9,6 MB por una
// de 215 s a 320 kbps, frente a 64 KB de todo lo demas del repaso. Mas del 99 % de la basura del repaso salia de ahi.
//
// Este lector es el MISMO Mp3FileReader: lo abre NAudio igual que siempre (cabeceras, tabla de cuadros, formato y el
// descompresor de siempre). Lo unico que cambia es la LECTURA de cada cuadro, que aqui reutiliza su memoria:
//   - la cuenta de Read y de Position es la de NAudio 1.9 paso a paso (sobrantes, cuadros dobles, salto con 3 cuadros
//     de calentamiento), con su mismo estado;
//   - la cabecera de cada cuadro la valida y la lee la PROPIA funcion de NAudio (Mp3Frame.IsValidHeader, privada), con
//     la misma busqueda byte a byte de la sincronia;
//   - los cuadros van por turnos en CUATRO objetos, y los datos de cada uno en un arreglo de su tamano exacto guardado
//     por tamano (un MP3 usa pocos). Cuatro porque el descompresor en coma flotante (FlotanteNLayer.cs) recuerda los
//     TRES cuadros anteriores para calentar al otro motor: el que se esta leyendo nunca pisa a esos tres.
// O sea: el descompresor recibe los mismos bytes, del mismo tamano, en el mismo orden y con los mismos Reset; lo que sale
// es identico (la bateria mp3ligero lo compara cuadro a cuadro con NAudio).
//
// EL INDICE DE CUADROS, TAMBIEN SIN BASURA (22-10-2026). Al abrir, Mp3FileReader recorre el archivo entero para apuntar donde empieza cada cuadro
// (CreateTableOfContents): por cada uno crea un Mp3Frame, un arreglo de 4 bytes y un Mp3Index, unos 160 bytes y 1,4 microsegundos, o sea 1,3 MB de basura
// y 10 ms (en Mono) por cancion de 200 s; el repaso abre cada cancion DOS veces (los tramos y la sonoridad entera) y la reproduccion una. LectorMp3Ligero.Abre
// hace lo mismo que el constructor de NAudio paso a paso (la etiqueta ID3, los dos primeros cuadros, la cabecera Xing, la etiqueta ID3v1 del final, el formato
// Mp3WaveFormat con la tasa de bits media, el descompresor), pero el indice lo cuenta ella: lee el archivo en trozos, valida cada cabecera con la funcion de
// NAudio (Mp3Frame.IsValidHeader) y apunta, en dos arreglos de enteros, donde empieza cada cuadro y cuantas muestras llevaban antes. Es el mismo indice (las
// mismas posiciones y las mismas cuentas; Position lo recorre igual), con 0,75 ms y 8 bytes por cuadro. El objeto se crea sin pasar por el constructor de
// NAudio (FormatterServices.GetUninitializedObject) y se le ponen sus campos privados. Si algo no es lo corriente (cambia la frecuencia o los canales a
// mitad, un archivo de mas de 2 GB, cualquier error al leer, NAudio que no es la 1.9), se abre por el constructor de siempre, que contesta lo que contestaba.
//
// Lo usa la medida (LectorMedida) y, desde el 19-10-2026, la reproduccion de un MP3 que se abre desde la memoria
// (CancionEnMemoria.cs, AbreMp3): la bateria reproligera exige el mismo PCM muestra a muestra que con NAudio en cancion
// entera, saltos, reanudar y relevo sin pausa. La reproduccion de un MP3 que se abre del archivo (mas de 32 MB, poca
// memoria) sigue con el lector de NAudio tal cual. Si NAudio no fuera la 1.9 que se espera (no encuentra sus piezas
// privadas), no se usa: se abre el lector de siempre.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using NAudio.Wave;

namespace Rocola
{
    internal sealed class LectorMp3Ligero : Mp3FileReader
    {
        private const BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;
        internal const int Turnos = 4;   // cuadros que se reutilizan por turnos (ver arriba)

        // Las piezas privadas de NAudio 1.9, una vez por sesion.
        private static readonly Func<byte[], Mp3Frame, bool> esCabecera;
        private static readonly Action<Mp3Frame, byte[]> ponDatos;
        private static readonly Action<Mp3Frame, long> ponPosicion;
        private static readonly FieldInfo fFlujo, fDescompresor, fBytesPorCuadro, fBuffer, fTabla,
                                          fToc, fDesplaza, fSobrantes, fSalto, fPosicion;
        // Los campos privados que el constructor de NAudio deja puestos (los que pone Abre en un objeto sin constructor).
        private static readonly FieldInfo fFormato, fLargoDatos, fInicioDatos, fFormatoMp3, fXing, fPropio, fMuestras, fBytesPorMuestra, fCandadoNaudio, fId3v2, fId3v1;
        internal static readonly bool Posible;
        // El indice propio (Abre). Las pruebas lo apagan para comparar con el de NAudio.
        internal static bool IndicePropio = true;
        internal static readonly bool IndicePosible;
        [ThreadStatic] private static byte[] trozoIndice;   // 64 KB por hilo que abre canciones (el repaso, la reproduccion): se reutiliza
        private const int TrozoIndiceBytes = 65536;

        static LectorMp3Ligero()
        {
            try
            {
                Type tf = typeof(Mp3Frame), tr = typeof(Mp3FileReader);
                MethodInfo valida = tf.GetMethod("IsValidHeader", BindingFlags.Static | BindingFlags.NonPublic, null,
                                                 new Type[] { typeof(byte[]), tf }, null);
                esCabecera = (Func<byte[], Mp3Frame, bool>)Delegate.CreateDelegate(typeof(Func<byte[], Mp3Frame, bool>), valida);
                ponDatos = (Action<Mp3Frame, byte[]>)Delegate.CreateDelegate(typeof(Action<Mp3Frame, byte[]>),
                                                                             tf.GetProperty("RawData").GetSetMethod(true));
                ponPosicion = (Action<Mp3Frame, long>)Delegate.CreateDelegate(typeof(Action<Mp3Frame, long>),
                                                                              tf.GetProperty("FileOffset").GetSetMethod(true));
                fFlujo = Campo(tr, "mp3Stream", typeof(Stream)); fDescompresor = Campo(tr, "decompressor", typeof(IMp3FrameDecompressor));
                fBytesPorCuadro = Campo(tr, "bytesPerDecodedFrame", typeof(int));
                fBuffer = Campo(tr, "decompressBuffer", typeof(byte[])); fTabla = tr.GetField("tableOfContents", Privado);
                fToc = Campo(tr, "tocIndex", typeof(int)); fDesplaza = Campo(tr, "decompressBufferOffset", typeof(int));
                fSobrantes = Campo(tr, "decompressLeftovers", typeof(int)); fSalto = Campo(tr, "repositionedFlag", typeof(bool));
                fPosicion = Campo(tr, "position", typeof(long));
                Posible = fTabla != null && typeof(IList).IsAssignableFrom(fTabla.FieldType) && tf.GetConstructor(Privado, null, Type.EmptyTypes, null) != null;
            }
            catch { Posible = false; }
            try
            {
                Type tr = typeof(Mp3FileReader);
                fFormato = Campo(tr, "waveFormat", typeof(WaveFormat)); fLargoDatos = Campo(tr, "mp3DataLength", typeof(long)); fInicioDatos = Campo(tr, "dataStartPosition", typeof(long));
                fFormatoMp3 = Campo(tr, "<Mp3WaveFormat>k__BackingField", typeof(Mp3WaveFormat)); fXing = Campo(tr, "xingHeader", typeof(XingHeader)); fPropio = Campo(tr, "ownInputStream", typeof(bool));
                fMuestras = Campo(tr, "totalSamples", typeof(long)); fBytesPorMuestra = Campo(tr, "bytesPerSample", typeof(int)); fCandadoNaudio = Campo(tr, "repositionLock", typeof(object));
                fId3v2 = Campo(tr, "<Id3v2Tag>k__BackingField", typeof(Id3v2Tag)); fId3v1 = Campo(tr, "<Id3v1Tag>k__BackingField", typeof(byte[]));
                IndicePosible = Posible;
            }
            catch { IndicePosible = false; }
        }
        private static FieldInfo Campo(Type t, string nombre, Type tipo)
        {
            FieldInfo f = t.GetField(nombre, Privado);
            if (f == null || f.FieldType != tipo) throw new MissingFieldException(t.Name, nombre);
            return f;
        }

        // (Sin readonly: Abre crea el objeto sin pasar por el constructor y los pone ella.)
        private Stream flujo;
        private IMp3FrameDecompressor descompresor;
        private int bytesPorCuadro;
        private byte[] buffer;
        private IList tabla;                       // el indice de NAudio (null si el indice es el propio)
        private object candado = new object();
        // El estado de lectura de Mp3FileReader, aqui (el de NAudio queda quieto: Read y Position son estos).
        private int tocIndex, desplaza, sobrantes;
        private bool salto;
        private long posicion;
        // El indice propio (Abre): donde empieza cada cuadro (indicePos) y cuantas muestras habia antes (indiceMuestras), en enteros; sus cuentas.
        private bool propio;
        private int[] indicePos, indiceMuestras;
        private int indiceN, bytesPorMuestra;
        private long totalMuestras, largoDatos, inicioDatos;
        // Los cuadros por turnos y los datos de cada turno por tamano.
        private Mp3Frame[] cuadros = new Mp3Frame[Turnos];
        private Dictionary<int, byte[]>[] datos = new Dictionary<int, byte[]>[Turnos];
        private int turno;
        private byte[] cabecera = new byte[4];
        internal long CuadrosLeidos;   // las pruebas
        internal bool IndiceEsPropio { get { return propio; } }   // las pruebas

        internal LectorMp3Ligero(Stream entrada, FrameDecompressorBuilder armador) : base(entrada, armador)
        {
            if (!Posible) throw new NotSupportedException("NAudio no es la 1.9 esperada");
            flujo = (Stream)fFlujo.GetValue(this);
            descompresor = (IMp3FrameDecompressor)fDescompresor.GetValue(this);
            bytesPorCuadro = (int)fBytesPorCuadro.GetValue(this);
            buffer = (byte[])fBuffer.GetValue(this);
            tabla = (IList)fTabla.GetValue(this);
            Recoge();   // recien abierto: tocIndex 0, en el principio de los datos
            Prepara();
        }

        // Los cuatro cuadros por turnos, con sus datos por tamano.
        private void Prepara()
        {
            for (int i = 0; i < Turnos; i++)
            {
                cuadros[i] = (Mp3Frame)Activator.CreateInstance(typeof(Mp3Frame), true);
                datos[i] = new Dictionary<int, byte[]>();
            }
        }

        // El constructor de siempre o el rapido (ver arriba): abre el lector ligero de un MP3. Devuelve lo mismo que daria
        // new LectorMp3Ligero(entrada, armador), y con el mismo estado de lectura; solo el indice de cuadros se cuenta aparte.
        internal static Mp3FileReader Abre(Stream entrada, FrameDecompressorBuilder armador)
        {
            if (!Posible) return new Mp3FileReader(entrada, armador);
            if (IndicePosible && IndicePropio)
            {
                long inicio = -1;
                try { inicio = entrada.Position; } catch { }
                if (inicio >= 0)
                {
                    LectorMp3Ligero r = null;
                    try { r = AbreRapido(entrada, armador, inicio); }
                    catch (NoRapido) { r = null; }
                    if (r != null) return r;
                    try { entrada.Position = inicio; } catch { }   // lo que haya leido el intento, deshecho
                }
            }
            return new LectorMp3Ligero(entrada, armador);
        }

        // Lo corriente se sale del caso rapido sin ruido: el constructor de NAudio vuelve a empezar y dira lo que haya que decir.
        private sealed class NoRapido : Exception { }

        private static LectorMp3Ligero AbreRapido(Stream entrada, FrameDecompressorBuilder armador, long inicio)
        {
            Mp3WaveFormat formato;
            long inicioDatos, largoDatos;
            Id3v2Tag etiqueta2; byte[] etiqueta1 = null; XingHeader xing;
            int[] pos, mue; int n; long total;
            Mp3Frame cuadroIndice = (Mp3Frame)Activator.CreateInstance(typeof(Mp3Frame), true);
            try
            {
                // El constructor de NAudio 1.9, tal cual, hasta el formato que se le da al descompresor.
                etiqueta2 = Id3v2Tag.ReadTag(entrada);
                inicioDatos = entrada.Position;
                Mp3Frame primero = Mp3Frame.LoadFromStream(entrada);
                if (primero == null) throw new NoRapido();
                double tasaBits = primero.BitRate;
                xing = XingHeader.LoadXingHeader(primero);
                if (xing != null) inicioDatos = entrada.Position;
                Mp3Frame segundo = Mp3Frame.LoadFromStream(entrada);
                if (segundo != null && (segundo.SampleRate != primero.SampleRate || segundo.ChannelMode != primero.ChannelMode))
                { inicioDatos = segundo.FileOffset; primero = segundo; }
                long largo = entrada.Length;
                if (largo > int.MaxValue) throw new NoRapido();
                largoDatos = largo - inicioDatos;
                entrada.Position = largo - 128;
                byte[] id3v1 = new byte[128];
                entrada.Read(id3v1, 0, 128);
                if (id3v1[0] == 0x54 && id3v1[1] == 0x41 && id3v1[2] == 0x47) { etiqueta1 = id3v1; largoDatos -= 128; }
                entrada.Position = inicioDatos;
                int canales = primero.ChannelMode == ChannelMode.Mono ? 1 : 2;
                formato = new Mp3WaveFormat(primero.SampleRate, canales, primero.FrameLength, (int)tasaBits);
                // CreateTableOfContents, con arreglos de enteros.
                int capacidad = (int)Math.Min(int.MaxValue / 2, largo / Math.Max(1, primero.FrameLength) + 64);
                pos = new int[capacidad]; mue = new int[capacidad];
                EscaneaIndice(entrada, inicioDatos, largo, cuadroIndice, formato.SampleRate, formato.Channels, ref pos, ref mue, out n, out total);
                tasaBits = largoDatos * 8.0 / ((double)total / formato.SampleRate);
                entrada.Position = inicioDatos;
                formato = new Mp3WaveFormat(primero.SampleRate, canales, primero.FrameLength, (int)tasaBits);
            }
            catch (NoRapido) { throw; }
            catch { throw new NoRapido(); }

            // El descompresor y lo que sigue: si esto falla, falla como en NAudio (que a estas alturas tampoco cerraba el flujo: no es suyo).
            IMp3FrameDecompressor descompresor = armador(formato);
            WaveFormat salida = descompresor.OutputFormat;
            int bytesPorMuestra = (salida.BitsPerSample / 8) * salida.Channels;
            int bytesPorCuadroDecodificado = 1152 * bytesPorMuestra;
            byte[] bufferDecodificado = new byte[bytesPorCuadroDecodificado * 2];

            LectorMp3Ligero r;
            try { r = PonObjeto(entrada, descompresor, formato, salida, etiqueta2, etiqueta1, xing, inicioDatos, largoDatos, total, pos, mue, n, bytesPorMuestra, bytesPorCuadroDecodificado, bufferDecodificado); }
            catch { try { descompresor.Dispose(); } catch { } throw new NoRapido(); }   // no deberia pasar: que lo abra NAudio
            return r;
        }

        // El objeto sin constructor, con los campos de NAudio y los del ligero como los deja el constructor de siempre.
        private static LectorMp3Ligero PonObjeto(Stream entrada, IMp3FrameDecompressor descompresor, Mp3WaveFormat formato, WaveFormat salida, Id3v2Tag etiqueta2, byte[] etiqueta1,
                                                 XingHeader xing, long inicioDatos, long largoDatos, long total, int[] pos, int[] mue, int n, int bytesPorMuestra,
                                                 int bytesPorCuadroDecodificado, byte[] bufferDecodificado)
        {
            LectorMp3Ligero r = (LectorMp3Ligero)FormatterServices.GetUninitializedObject(typeof(LectorMp3Ligero));
            fPropio.SetValue(r, false); fCandadoNaudio.SetValue(r, new object());
            fFlujo.SetValue(r, entrada); fId3v2.SetValue(r, etiqueta2); fId3v1.SetValue(r, etiqueta1); fXing.SetValue(r, xing);
            fInicioDatos.SetValue(r, inicioDatos); fLargoDatos.SetValue(r, largoDatos); fFormatoMp3.SetValue(r, formato);
            fMuestras.SetValue(r, total); fToc.SetValue(r, 0); fDescompresor.SetValue(r, descompresor); fFormato.SetValue(r, salida);
            fBytesPorMuestra.SetValue(r, bytesPorMuestra); fBytesPorCuadro.SetValue(r, bytesPorCuadroDecodificado); fBuffer.SetValue(r, bufferDecodificado);
            fDesplaza.SetValue(r, 0); fSobrantes.SetValue(r, 0); fSalto.SetValue(r, false); fPosicion.SetValue(r, 0L);
            // y lo del objeto ligero (sus inicializadores de campo no corrieron)
            r.candado = new object(); r.cuadros = new Mp3Frame[Turnos]; r.datos = new Dictionary<int, byte[]>[Turnos]; r.cabecera = new byte[4];
            r.flujo = entrada; r.descompresor = descompresor; r.bytesPorCuadro = bytesPorCuadroDecodificado; r.buffer = bufferDecodificado; r.tabla = null;
            r.propio = true; r.indicePos = pos; r.indiceMuestras = mue; r.indiceN = n; r.bytesPorMuestra = bytesPorMuestra;
            r.totalMuestras = total; r.largoDatos = largoDatos; r.inicioDatos = inicioDatos;
            r.tocIndex = 0; r.desplaza = 0; r.sobrantes = 0; r.salto = false; r.posicion = 0;
            r.Prepara();
            return r;
        }

        // CreateTableOfContents de NAudio: desde 'desde', cuadro a cuadro (con la misma busqueda byte a byte de la sincronia cuando la cabecera no vale), apunta
        // donde empieza la busqueda de cada cuadro (la posicion que NAudio guardaba en su Mp3Index.FilePosition) y las muestras que habia antes; la cuenta de
        // muestras es la suma de SampleCount de cada cuadro. Termina donde NAudio: cuando no quedan 4 bytes para una cabecera. El cuadro de la ultima posicion, aunque
        // el archivo lo corte, entra igual (NAudio solo avanzaba la posicion). Si cambia la frecuencia o los canales, NAudio lanza InvalidOperationException: aqui, NoRapido.
        private static void EscaneaIndice(Stream s, long desde, long largo, Mp3Frame f, int tasa, int canales, ref int[] pos, ref int[] mue, out int n, out long total)
        {
            byte[] buf = trozoIndice;
            if (buf == null) buf = trozoIndice = new byte[TrozoIndiceBytes];
            long bufIni = 0; int bufN = 0;
            byte[] h = new byte[4];
            long p = desde;
            n = 0; total = 0;
            while (true)
            {
                if (!Asegura(s, p, largo, buf, ref bufIni, ref bufN)) break;
                int o = (int)(p - bufIni);
                h[0] = buf[o]; h[1] = buf[o + 1]; h[2] = buf[o + 2]; h[3] = buf[o + 3];
                long q = p;
                bool fin = false;
                while (!esCabecera(h, f))
                {
                    q++;
                    if (!Asegura(s, q, largo, buf, ref bufIni, ref bufN)) { fin = true; break; }
                    o = (int)(q - bufIni);
                    h[0] = buf[o]; h[1] = buf[o + 1]; h[2] = buf[o + 2]; h[3] = buf[o + 3];
                }
                if (fin) break;
                if (f.SampleRate != tasa || (f.ChannelMode == ChannelMode.Mono ? 1 : 2) != canales) throw new NoRapido();
                if (n == pos.Length)
                {
                    if (n >= int.MaxValue / 4) throw new NoRapido();
                    Array.Resize(ref pos, n + n / 2 + 16); Array.Resize(ref mue, pos.Length);
                }
                if (total > int.MaxValue - 4608) throw new NoRapido();
                pos[n] = (int)p; mue[n] = (int)total; n++;
                total += f.SampleCount;
                p = q + f.FrameLength;
            }
        }

        // Hay 4 bytes legibles desde 'p' en el trozo (leyendolo del flujo si hace falta)? Falso = ya no queda una cabecera (el fin del archivo).
        private static bool Asegura(Stream s, long p, long largo, byte[] buf, ref long bufIni, ref int bufN)
        {
            if (p + 4 > largo) return false;
            if (p >= bufIni && p + 4 <= bufIni + bufN) return true;
            s.Position = p;
            int tiene = 0;
            while (tiene < buf.Length)
            {
                int r = s.Read(buf, tiene, buf.Length - tiene);
                if (r <= 0) break;
                tiene += r;
            }
            bufIni = p; bufN = tiene;
            if (tiene < 4) throw new NoRapido();   // el flujo dijo tener mas de lo que da: que lo cuente NAudio
            return true;
        }

        // El estado de NAudio, a este lector y de vuelta (solo al saltar: un salto es raro).
        private void Recoge()
        {
            tocIndex = (int)fToc.GetValue(this); desplaza = (int)fDesplaza.GetValue(this); sobrantes = (int)fSobrantes.GetValue(this);
            salto = (bool)fSalto.GetValue(this); posicion = (long)fPosicion.GetValue(this);
        }

        public override long Position
        {
            get { return posicion; }
            set
            {
                // El salto es el de NAudio, con el cuadro en que va ESTE lector: si el destino cae fuera de la tabla, NAudio
                // conserva su tocIndex y el Read siguiente vuelve 3 cuadros desde ahi.
                lock (candado)
                {
                    if (propio) { SaltaPropio(value); return; }
                    fToc.SetValue(this, tocIndex);
                    base.Position = value;
                    Recoge();
                }
            }
        }

        // Mp3Frame.LoadFromStream(flujo, true), con la memoria del turno.
        private Mp3Frame CargaCuadro()
        {
            Mp3Frame f = cuadros[turno];
            ponPosicion(f, flujo.Position);
            byte[] h = cabecera;
            if (flujo.Read(h, 0, 4) < 4) return null;
            long donde = f.FileOffset;
            while (!esCabecera(h, f))
            {
                h[0] = h[1]; h[1] = h[2]; h[2] = h[3];
                if (flujo.Read(h, 3, 1) < 1) return null;
                donde++; ponPosicion(f, donde);
            }
            int largo = f.FrameLength, falta = largo - 4;
            byte[] d;
            Dictionary<int, byte[]> porLargo = datos[turno];
            if (!porLargo.TryGetValue(largo, out d)) { d = new byte[largo]; porLargo[largo] = d; }
            ponDatos(f, d);
            Array.Copy(h, d, 4);
            if (flujo.Read(d, 4, falta) < falta) throw new EndOfStreamException("Unexpected end of stream before frame complete");
            turno = (turno + 1) % Turnos;
            CuadrosLeidos++;
            return f;
        }

        // Mp3FileReader.ReadNextFrame(true).
        private Mp3Frame SiguienteCuadro()
        {
            Mp3Frame f = null;
            try { f = CargaCuadro(); if (f != null) tocIndex++; }
            catch (EndOfStreamException) { }
            return f;
        }

        // Mp3FileReader.Read, la misma cuenta.
        public override int Read(byte[] sampleBuffer, int offset, int numBytes)
        {
            int leidos = 0;
            lock (candado)
            {
                if (sobrantes != 0)
                {
                    int k = Math.Min(sobrantes, numBytes);
                    Array.Copy(buffer, desplaza, sampleBuffer, offset, k);
                    sobrantes -= k;
                    if (sobrantes == 0) desplaza = 0; else desplaza += k;
                    leidos += k;
                    offset += k;
                }
                int tocAntes = tocIndex;
                if (salto)
                {
                    descompresor.Reset();
                    tocIndex = Math.Max(0, tocIndex - 3);
                    flujo.Position = PosicionEnTabla(tocIndex);
                    salto = false;
                }
                while (leidos < numBytes)
                {
                    Mp3Frame f = SiguienteCuadro();
                    if (f == null) break;
                    int n = descompresor.DecompressFrame(f, buffer, 0);
                    if (tocIndex <= tocAntes || n == 0) continue;
                    if (tocIndex == tocAntes + 1 && n == bytesPorCuadro * 2)
                    {
                        Array.Copy(buffer, bytesPorCuadro, buffer, 0, bytesPorCuadro);
                        n = bytesPorCuadro;
                    }
                    int k = Math.Min(n - desplaza, numBytes - leidos);
                    Array.Copy(buffer, desplaza, sampleBuffer, offset, k);
                    if (k + desplaza < n) { desplaza += k; sobrantes = n - desplaza; }
                    else { desplaza = 0; sobrantes = 0; }
                    offset += k;
                    leidos += k;
                }
            }
            posicion += leidos;
            return leidos;
        }

        // Mp3FileReader.Position = value, con el indice propio. El cuadro es el primero cuyas muestras (las de antes mas las suyas) pasan de la pedida; si no hay
        // ninguno (el final), se queda el tocIndex de antes y se va al final de los datos.
        private void SaltaPropio(long value)
        {
            value = Math.Max(Math.Min(value, totalMuestras * bytesPorMuestra), 0L);
            long muestra = value / bytesPorMuestra;
            int hallado = -1;
            for (int i = 0; i < indiceN; i++)
            {
                long hasta = (i + 1 < indiceN) ? indiceMuestras[i + 1] : totalMuestras;
                if (hasta > muestra) { hallado = i; tocIndex = i; break; }
            }
            desplaza = 0; sobrantes = 0; salto = true;
            if (hallado >= 0)
            {
                flujo.Position = indicePos[hallado];
                long dentro = muestra - indiceMuestras[hallado];
                if (dentro > 0) desplaza = (int)dentro * bytesPorMuestra;
            }
            else flujo.Position = largoDatos + inicioDatos;
            posicion = value;
        }

        // tableOfContents[i].FilePosition (Mp3Index es privado de NAudio): solo tras un salto.
        private long PosicionEnTabla(int i)
        {
            if (propio)
            {
                if ((uint)i >= (uint)indiceN) throw new ArgumentOutOfRangeException("i");
                return indicePos[i];
            }
            object e = tabla[i];
            return (long)e.GetType().GetProperty("FilePosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(e, null);
        }
    }
}
