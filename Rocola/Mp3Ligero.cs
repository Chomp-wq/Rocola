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
// Solo lo usa la medida (LectorMedida). La reproduccion sigue con el lector de NAudio tal cual. Si NAudio no fuera la
// 1.9 que se espera (no encuentra sus piezas privadas), no se usa: la medida abre el lector de siempre.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
        internal static readonly bool Posible;

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
        }
        private static FieldInfo Campo(Type t, string nombre, Type tipo)
        {
            FieldInfo f = t.GetField(nombre, Privado);
            if (f == null || f.FieldType != tipo) throw new MissingFieldException(t.Name, nombre);
            return f;
        }

        private readonly Stream flujo;
        private readonly IMp3FrameDecompressor descompresor;
        private readonly int bytesPorCuadro;
        private readonly byte[] buffer;
        private readonly IList tabla;
        private readonly object candado = new object();
        // El estado de lectura de Mp3FileReader, aqui (el de NAudio queda quieto: Read y Position son estos).
        private int tocIndex, desplaza, sobrantes;
        private bool salto;
        private long posicion;
        // Los cuadros por turnos y los datos de cada turno por tamano.
        private readonly Mp3Frame[] cuadros = new Mp3Frame[Turnos];
        private readonly Dictionary<int, byte[]>[] datos = new Dictionary<int, byte[]>[Turnos];
        private int turno;
        private readonly byte[] cabecera = new byte[4];
        internal long CuadrosLeidos;   // las pruebas

        internal LectorMp3Ligero(Stream entrada, FrameDecompressorBuilder armador) : base(entrada, armador)
        {
            if (!Posible) throw new NotSupportedException("NAudio no es la 1.9 esperada");
            flujo = (Stream)fFlujo.GetValue(this);
            descompresor = (IMp3FrameDecompressor)fDescompresor.GetValue(this);
            bytesPorCuadro = (int)fBytesPorCuadro.GetValue(this);
            buffer = (byte[])fBuffer.GetValue(this);
            tabla = (IList)fTabla.GetValue(this);
            Recoge();   // recien abierto: tocIndex 0, en el principio de los datos
            for (int i = 0; i < Turnos; i++)
            {
                cuadros[i] = (Mp3Frame)Activator.CreateInstance(typeof(Mp3Frame), true);
                datos[i] = new Dictionary<int, byte[]>();
            }
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

        // tableOfContents[i].FilePosition (Mp3Index es privado de NAudio): solo tras un salto.
        private long PosicionEnTabla(int i)
        {
            object e = tabla[i];
            return (long)e.GetType().GetProperty("FilePosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(e, null);
        }
    }
}
