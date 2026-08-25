using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// QUE SABE HACER DE VERDAD LA TARJETA DE SONIDO DE ESTA MAQUINA.
//
//     mono formatos.exe [tarjeta]        (por defecto "default"; tambien "hw:0", "plughw:1"...)
//
// Hay dos cosas que conviene saber de la maquina del bar y que no se pueden adivinar desde fuera:
//
//   CUANTOS BITS ACEPTA. La rocola le ofrece 32, luego 24, luego 16, y se queda con el mejor que le
//   acepten. Con 16 hay que anadir un ruido diminuto a proposito -el dither- para que el redondeo no
//   se oiga; con mas bits eso sobra. Saber cual va a tocar dice si ese ruido esta ahi o no.
//
//   SI SABE 44.1 kHz. Casi toda la musica esta a 44100 muestras por segundo. Si la tarjeta solo sabe
//   48000 -que en audio integrado es de lo mas comun- alguien tiene que convertir, y hoy lo hace
//   ALSA con el convertidor que trae de serie, que es rapido y mediocre. Si sale que esta maquina es
//   de esas, vale la pena que lo hagamos nosotros, que lo hariamos mejor. Y si sabe 44.1, no hay nada
//   que arreglar y no se toca nada.
//
// Se pregunta abriendo la tarjeta y probando de verdad, que es la unica forma de saberlo: lo que diga
// un archivo de configuracion puede no ser lo que la tarjeta hace.
class Formatos
{
    const string LIB = "libasound.so.2";
    const int STREAM_PLAYBACK = 0, ACCESS_RW_INTERLEAVED = 3;

    [DllImport(LIB)] static extern int snd_pcm_open(out IntPtr pcm, string nombre, int flujo, int modo);
    [DllImport(LIB)] static extern int snd_pcm_close(IntPtr pcm);
    [DllImport(LIB)] static extern int snd_pcm_set_params(IntPtr pcm, int formato, int acceso,
                                                          uint canales, uint frecuencia, int remuestreo, uint latenciaUs);
    [DllImport(LIB)] static extern int snd_pcm_get_params(IntPtr pcm, out ulong buffer, out ulong periodo);
    [DllImport(LIB)] static extern IntPtr snd_strerror(int err);

    static string Error(int e)
    {
        try { IntPtr p = snd_strerror(e); return (p == IntPtr.Zero) ? e.ToString() : Marshal.PtrToStringAnsi(p); }
        catch { return e.ToString(); }
    }

    // Prueba un formato/frecuencia SIN dejar que ALSA convierta por su cuenta (remuestreo = 0): asi lo
    // que conteste es lo que la tarjeta sabe hacer, no lo que ALSA sabe disimular.
    static bool Prueba(string tarjeta, int formato, int canales, int frecuencia, bool dejarConvertir, out string porque)
    {
        porque = ""; IntPtr pcm;
        int r = snd_pcm_open(out pcm, tarjeta, STREAM_PLAYBACK, 0);
        if (r < 0) { porque = Error(r); return false; }
        try
        {
            r = snd_pcm_set_params(pcm, formato, ACCESS_RW_INTERLEAVED, (uint)canales, (uint)frecuencia,
                                   dejarConvertir ? 1 : 0, 200000);
            if (r < 0) { porque = Error(r); return false; }
            return true;
        }
        finally { try { snd_pcm_close(pcm); } catch { } }
    }

    static int Main(string[] argv)
    {
        string tarjeta = (argv.Length > 0) ? argv[0] : "default";
        Console.WriteLine();
        Console.WriteLine("=== QUE SABE HACER LA TARJETA \"" + tarjeta + "\" ===");
        Console.WriteLine();

        IntPtr pcm;
        int abre = snd_pcm_open(out pcm, tarjeta, STREAM_PLAYBACK, 0);
        if (abre < 0)
        {
            Console.WriteLine("  No se pudo abrir: " + Error(abre));
            Console.WriteLine();
            Console.WriteLine("  Si dice que esta ocupada, es que otro programa la tiene cogida.");
            Console.WriteLine("  Si dice que no existe, prueba con: hw:0, hw:1, plughw:0");
            Console.WriteLine();
            return 1;
        }
        snd_pcm_close(pcm);

        // ---------- los bits ----------
        Console.WriteLine("  CUANTOS BITS ACEPTA  (la rocola los prueba en este orden y se queda con el primero)");
        int[] fmt = { 10, 6, 2 };
        string[] nom = { "32 bits", "24 bits", "16 bits" };
        string elegido = null;
        string por;
        foreach (int i in new int[] { 0, 1, 2 })
        {
            bool ok = Prueba(tarjeta, fmt[i], 2, 44100, false, out por);
            Console.WriteLine("     " + (ok ? "si" : "NO") + "  " + nom[i] + (ok ? "" : "   (" + por + ")"));
            if (ok && elegido == null) elegido = nom[i];
        }
        Console.WriteLine();
        if (elegido == null) Console.WriteLine("     -> ninguno directo; la rocola tendra que dejar que ALSA convierta.");
        else
        {
            Console.WriteLine("     -> la rocola le mandara " + elegido + ".");
            if (elegido == "16 bits")
                Console.WriteLine("        Con 16 bits la rocola anade el dither, que es lo correcto ahi.");
            else
                Console.WriteLine("        Con " + elegido + " no hace falta dither: se manda la cuenta tal cual.");
        }

        // ---------- las frecuencias ----------
        Console.WriteLine();
        Console.WriteLine("  QUE FRECUENCIAS ACEPTA  (44100 es la de casi toda la musica)");
        int[] hz = { 44100, 48000, 88200, 96000, 22050 };
        bool sabe441 = false;
        List<int> sabidas = new List<int>();
        foreach (int f in hz)
        {
            bool ok = Prueba(tarjeta, 2, 2, f, false, out por);
            Console.WriteLine("     " + (ok ? "si" : "NO") + "  " + f + " Hz" + (ok ? "" : "   (" + por + ")"));
            if (ok) { sabidas.Add(f); if (f == 44100) sabe441 = true; }
        }

        Console.WriteLine();
        if (sabe441)
        {
            Console.WriteLine("     -> SABE 44100. No hay nada que arreglar: la musica le llega tal cual,");
            Console.WriteLine("        sin que nadie la convierta por el camino.");
        }
        else if (sabidas.Count > 0)
        {
            Console.WriteLine("     -> NO SABE 44100, que es la frecuencia de casi toda la musica.");
            Console.WriteLine("        Hoy la convierte ALSA con el convertidor que trae de serie, que es");
            Console.WriteLine("        rapido y mediocre, y por ahi pasa TODA la musica del bar.");
            Console.WriteLine("        En esta maquina vale la pena que la convierta la rocola.");
        }
        else Console.WriteLine("     -> no acepto ninguna directa; todo pasa por el convertidor de ALSA.");

        // ---------- el colchon ----------
        Console.WriteLine();
        Console.WriteLine("  EL COLCHON  (lo que la tarjeta tiene guardado por delante: mas colchon, menos");
        Console.WriteLine("  riesgo de que la musica se corte; menos colchon, la imagen mas pegada al sonido)");
        if (snd_pcm_open(out pcm, tarjeta, STREAM_PLAYBACK, 0) >= 0)
        {
            try
            {
                if (snd_pcm_set_params(pcm, elegido == null ? 2 : fmt[Array.IndexOf(nom, elegido)],
                                       ACCESS_RW_INTERLEAVED, 2, 44100, 1, 300000) >= 0)
                {
                    ulong buf, per;
                    if (snd_pcm_get_params(pcm, out buf, out per) >= 0)
                        Console.WriteLine("     pidiendo 300 ms, la tarjeta dio " + (buf * 1000UL / 44100UL) + " ms");
                }
            }
            finally { try { snd_pcm_close(pcm); } catch { } }
        }
        Console.WriteLine();
        return 0;
    }
}
