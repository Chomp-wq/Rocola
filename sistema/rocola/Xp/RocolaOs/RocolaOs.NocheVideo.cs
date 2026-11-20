// NV5b, EL MODO NOCHE TAMBIEN EN EL GRAVE DE LOS VIDEOS (20-11-2026; el dueno: «Del modo noche haz lo que me recomiendes»; las medidas y el porque
// en AUDIO_NOCHE_VECINOS_MODEST_2026-11-20.md). Solo Rocola OS: la funcion «noche_video» (encendida de fabrica; «noche_video=0» en rocola_os.txt
// la apaga).
//
// En XP los videos no pasan por la cadena de sonido: el reproductor de Windows Media solo recibe el volumen, y de noche se le baja lo mismo que
// a la musica (NV5a, Form1.Sonido.cs: FactorVideoNoche). En rocola-xp los toca mpv (Nativo/ReproductorVideo.cs), que si deja ponerle filtros de
// libavfilter por su propiedad «af»: de noche, al EMPEZAR cada video (gancho g102, en StartVideoItem justo antes de abrirlo: nunca a media
// reproduccion, que daria un chasquido), el mismo estante de graves de 120 Hz que la musica (los graves de noche de ese momento: lowshelf con
// Q 0,7071, el de SalaLocal) y el mismo corte del grave (dos highpass de Butterworth, Q 0,5412 y 1,3066: el cuarto orden de la curva del
// subgrave), con el avance del momento: el corte a la frecuencia que quita lo mismo que la mezcla de la musica a 0,7 veces el corte
// (Form1.CorteEquivalenteHz). Estos dos son de fase minima (mpv no tiene la fase lineal de la cadena): en un video da igual, no hay limitador
// detras que pague los picos. De dia, sin graves ni corte, «af» vacio: el video suena como siempre. Lo que cambie a media noche (la bajada
// de la hora, la madrugada, «Probar») lo toma el siguiente video; el volumen si sigue la rampa, como en XP. Sin ROCOLA_OS, o con la funcion
// apagada, nada: lo de XP (el filtro de mpv ni se toca).
using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Rocola
{
    public partial class Form1
    {
        internal const string RocolaOsNocheVideoNombre = "noche_video";

        // ================================================================== LA FUNCION «noche_video» (se registra sola)
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraNocheVideo()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsNocheVideoNombre,
                Que = "de noche, a los videos el mismo estante de graves y el mismo corte del grave que a la musica (el filtro de audio de mpv, al empezar cada video)",
                DeFabrica = true,
            });
        }

        // EL GANCHO g102 (Codigo/Form1.Video.cs, StartVideoItem): justo antes de abrir el video, su filtro de audio. Sin Rocola OS, nada.
        private void RocolaOsNocheVideo(ReproductorVideo v)
        {
            if (v == null || !RocolaOs.Activo) return;
            string af = RocolaOs.Encendida(RocolaOsNocheVideoNombre) ? RocolaOsFiltroNoche(GravesNocheAhora, CorteNocheAhoraHz) : "";
            v.FiltroAudio(af);
        }

        // El filtro de libavfilter para mpv: «lavfi=[...]» con el estante (si hay graves de noche) y el corte (si hay corte); "" si no hay nada.
        // (A centesimas de dB y decimas de Hz: lo que no cambia el oido no cambia el texto.)
        internal static string RocolaOsFiltroNoche(float gravesDb, float corteHz)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            string g = gravesDb >= 0.01f
                ? "lowshelf=f=" + SalaLocal.GravesHz.ToString("0.#", ci) + ":t=q:w=" + SalaLocal.TonoQ.ToString("0.####", ci) + ":g=-" + gravesDb.ToString("0.00", ci)
                : "";
            string c = corteHz >= 10f
                ? "highpass=f=" + corteHz.ToString("0.0", ci) + ":t=q:w=0.5412,highpass=f=" + corteHz.ToString("0.0", ci) + ":t=q:w=1.3066"
                : "";
            if (g.Length == 0 && c.Length == 0) return "";
            return "lavfi=[" + g + (g.Length > 0 && c.Length > 0 ? "," : "") + c + "]";
        }
    }
}
