// LO QUE EL MOTOR NECESITA DE ALREDEDOR (Rocola OS). En XP, el nivelado avisa a la ventana de cada bloque para el pulso de las estrellas
// (PublicaBloqueDe, publicacionAudio); en el motor esos avisos quedan aqui, vacios, hasta que exista la pantalla de Linux.
// Y dos piezas sacadas tal cual: FormatoAudio (Rocola/AudioControles.cs) y TanhRapida (Rocola/Form1.Sonido.cs).
using System;

namespace Rocola
{
    // NAudio 1.9 expone SubFormat, pero no dwChannelMask. Solo se consulta/fija
    // al construir una etapa; nunca por muestra. No se inventa layout multicanal.
    internal static class FormatoAudio
    {
        private static readonly System.Reflection.FieldInfo campoMascara =
            typeof(NAudio.Wave.WaveFormatExtensible).GetField("dwChannelMask",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        internal static bool Valida(int mascara, int canales)
        {
            if (mascara == 0 || (mascara & ~0x3FFFF) != 0) return false;
            int n = 0; for (int m = mascara; m != 0; m &= m - 1) n++;
            return n == canales;
        }
        internal static int Mascara(NAudio.Wave.WaveFormat formato)
        {
            if (formato == null) return 0;
            NAudio.Wave.WaveFormatExtensible ex = formato as NAudio.Wave.WaveFormatExtensible;
            if (ex != null && campoMascara != null)
            {
                int m = (int)campoMascara.GetValue(ex);
                return Valida(m, formato.Channels) ? m : 0;
            }
            // Los formatos exactos de salida tambien exponen su mascara.
            System.Reflection.PropertyInfo p = formato.GetType().GetProperty("Mascara",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (p != null) { int m = (int)p.GetValue(formato, null); return Valida(m, formato.Channels) ? m : 0; }
            return formato.Channels == 1 ? 4 : (formato.Channels == 2 ? 3 : 0);
        }
        internal static NAudio.Wave.WaveFormat Flotante(int hz, int canales, int mascara)
        {
            if ((canales == 1 && mascara == 4) || (canales == 2 && mascara == 3))
                return NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(hz, canales);
            NAudio.Wave.WaveFormatExtensible f = new NAudio.Wave.WaveFormatExtensible(hz, 32, canales);
            if (campoMascara == null) throw new NotSupportedException("NAudio no conserva la mascara multicanal");
            campoMascara.SetValue(f, Valida(mascara, canales) ? mascara : 0);
            return f;
        }
        internal static NAudio.Wave.WaveFormat Pcm(int hz, int bits, int canales, int mascara)
        {
            if ((canales == 1 && mascara == 4) || (canales == 2 && mascara == 3))
                return new NAudio.Wave.WaveFormat(hz, bits, canales);
            NAudio.Wave.WaveFormatExtensible f = new NAudio.Wave.WaveFormatExtensible(hz, bits, canales);
            if (campoMascara == null) throw new NotSupportedException("NAudio no conserva la mascara multicanal");
            campoMascara.SetValue(f, Valida(mascara, canales) ? mascara : 0);
            return f;
        }
        internal static NAudio.Wave.WaveFormat DeArchivo(NAudio.Wave.WaveFormat f, string ruta)
        {
            int mascara = Mascara(f);
            if (ruta != null && ruta.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                int tipo, canales, m;
                if (DisposicionBs1770.LeeFmtWav(ruta, out tipo, out canales, out m) && canales == f.Channels)
                    mascara = Valida(m, canales) ? m : (tipo == 0xFFFE ? 0 : (canales <= 2 ? Mascara(f) : 0));
            }
            return Flotante(f.SampleRate, f.Channels, mascara);
        }
        internal static bool[] Musicales(NAudio.Wave.WaveFormat f)
        {
            int mascara = Mascara(f), ch = Math.Max(1, f.Channels);
            bool[] p = new bool[ch];
            if (mascara == 0) { for (int c = 0; c < ch; c++) p[c] = true; return p; }
            int i = 0;
            for (int bit = 1; i < ch && bit <= 0x20000; bit <<= 1)
                if ((mascara & bit) != 0) p[i++] = bit != DisposicionBs1770.LFE;
            return p;
        }
    }

    public partial class Form1
    {
        internal int publicacionAudio;
        private sealed class GuardadoReciente { public Action Pending; public bool Running; }   // (Rocola/Form1.Datos.cs)
        private void PublicaBloqueDe(LevelTap tap, float[] buffer, int offset, int n, int rate, int ch) { }
        // ==========  RENDER DEL CUBO (compartido)  ==========
        // TANGENTE HIPERBOLICA aproximada (Pade 3/2), para la saturacion suave del realce de agudos. La de la
        // libreria matematica se llamaba UNA VEZ POR MUESTRA Y CANAL -88,200 veces por segundo- y era de lo mas
        // caro que hacia la cadena de audio. Esta cuesta 5.6 veces menos (medido: del 0.23% de un nucleo al
        // 0.04%) y dibuja la misma curva: se aparta como mucho 0.024, y como el realce entra al 7% en el peor
        // caso, en la salida eso queda 56 dB por debajo de la senal. Inaudible.
        // OJO: la red neuronal del DSEE usa la tangente DE VERDAD y ahi no se toca, porque el modelo se entreno
        // con esa; ademas alli se llama unas 2,600 veces por segundo, o sea nada.
        internal static float TanhRapida(float x)
        {
            if (!(x > -3f)) return -1f;   // atrapa tambien NaN: ninguna comparacion con NaN es cierta
            if (x > 3f) return 1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }
    }
}
