// Alternativa MP3 flotante administrada para HD, compartiendo el adaptador usado por Modest.
// NLayer 1.12.0, MIT (AVISOS_NLAYER.txt). Mp3FileReader conserva su índice y sus reglas de seek.
using System;
using System.IO;
using System.Reflection;
using NAudio.Wave;

namespace Rocola
{
    internal static class NLayerIncluido
    {
        internal const string Recurso = "Rocola.NLayer.dll";
        private static readonly object cerrojo = new object();
        private static Assembly cargada;
        private static bool enganchado;

        internal static void Engancha()
        {
            lock (cerrojo)
            {
                if (enganchado) return;
                AppDomain.CurrentDomain.AssemblyResolve += Resuelve;
                enganchado = true;
            }
        }

        private static Assembly Resuelve(object quien, ResolveEventArgs e)
        {
            string n = e != null ? e.Name : null;
            if (n == null || !(n == "NLayer" || n.StartsWith("NLayer,", StringComparison.OrdinalIgnoreCase))) return null;
            lock (cerrojo)
            {
                if (cargada != null) return cargada;
                using (Stream s = typeof(NLayerIncluido).Assembly.GetManifestResourceStream(Recurso))
                {
                    if (s == null) return null;
                    byte[] b = new byte[s.Length];
                    int leidos = 0, r;
                    while (leidos < b.Length && (r = s.Read(b, leidos, b.Length - leidos)) > 0) leidos += r;
                    if (leidos != b.Length) throw new EndOfStreamException("NLayer incluido incompleto");
                    cargada = Assembly.Load(b);
                    return cargada;
                }
            }
        }
    }

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
}
