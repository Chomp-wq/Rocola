// EN LINUX (port nativo, 8-11-2026): la parte de la salida con hilo propio de XP (Codigo/SalidaPropia.cs, WaveOutPropio) que en XP
// contestaba Windows y en Linux se calcula. Va aparte para que SalidaPropia.cs siga linea por linea con el de XP (la regla de la
// traduccion: cada archivo conserva su numero de lineas).
//
// La tarjeta es la de ALSA y esta salida le habla directo (Nativo/SalidaAlsa.cs), sin la interfaz con
// forma de winmm (ISistemaWaveOut), sin las WAVEHDR (CabeceraWave y sus banderas) y sin los MMSYSERR: los mismos bloques de 50 ms, en el
// mismo orden, el mismo hilo, la misma espera de 100 ms como mucho, el mismo final, el mismo reloj en bytes y la misma parada. Un bloque
// «ya sono» (lo que en XP era la bandera WHDR_DONE que pone Windows) cuando lo sonado en la tarjeta (SalidaAlsa.Sonado) pasa de donde acaba.
// Los errores son ErrorAlsa: Form1 decide con ellos lo mismo que con los de winmm (SalidaSonido.cs).
using System;
using NAudio.Wave;

namespace Rocola
{
    internal sealed partial class WaveOutPropio
    {
        // EN LINUX: lo que en XP contesta Windows sobre los bloques (WHDR_DONE, el aviso al acabar cada uno) se calcula con lo sonado.
        // Lo sonado en la tarjeta, en cuadros desde el ultimo Vacia (si ALSA falla, ErrorAlsa: lo atiende el hilo, como un fallo de winmm)
        private long Sonado() { lock (cerrojo) return salida != null && !parar ? salida.Sonado() : long.MaxValue; }
        // Cuanto cabe en la tarjeta ahora, en cuadros
        private int Libres() { lock (cerrojo) return salida != null && !parar ? salida.Libres() : 0; }
        // Cuanto esperar al aviso: hasta que acabe de sonar el bloque escrito mas viejo que aun suena (en XP, Windows senala el evento en ese
        // momento), como mucho 100 ms (la espera de XP) y al menos 1 ms
        private int EsperaMs(bool[] escrito)
        {
            if (estado != PlaybackState.Playing) return 100;
            long ya, primero = long.MaxValue;
            try { ya = Sonado(); } catch { return 1; }   // (el hilo vera el fallo enseguida, al pedirlo otra vez)
            for (int i = 0; i < escrito.Length; i++) if (escrito[i] && finDe[i] > ya && finDe[i] < primero) primero = finDe[i];
            if (primero == long.MaxValue) return fin ? 100 : 1;   // nada sonando: si aun hay que escribir, enseguida
            long ms = (primero - ya) * 1000 / Math.Max(1, formato.SampleRate) + 1;
            return (int)Math.Max(1, Math.Min(100, ms));
        }
        // EN LINUX: (waveOutReset) lo que no sono se tira (snd_pcm_drop), la cuenta vuelve a 0 y todos los bloques quedan sonados. Con el cerrojo.
        private void Reinicia()
        {
            salida.Vacia();
            if (finDe != null) for (int i = 0; i < finDe.Length; i++) finDe[i] = 0;
        }
    }
}
