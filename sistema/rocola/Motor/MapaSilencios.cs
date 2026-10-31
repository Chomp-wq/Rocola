// Donde suena una cancion (Rocola/SinSilencios.cs).
using System;
using System.Globalization;

namespace Rocola
{
    // Donde suena algo en una cancion, en segundos. Desde < 0: no suena nada (se deja como esta). HuecoDesde < 0: no
    // hay silencio en medio.
    internal struct MapaSilencios
    {
        internal float Desde, Hasta, HuecoDesde, HuecoHasta, Dura;
        internal static MapaSilencios Mudo(float dura)
        {
            MapaSilencios m; m.Desde = -1f; m.Hasta = -1f; m.HuecoDesde = -1f; m.HuecoHasta = -1f; m.Dura = dura; return m;
        }
        internal bool Suena { get { return Desde >= 0f; } }
        internal bool ConHueco { get { return HuecoDesde >= 0f; } }
        // Coherente: lo que se lee de la tabla tiene que cuadrar o no se usa.
        internal bool Valido
        {
            get
            {
                if (!(Dura > 0f) || Dura > 36000f) return false;
                if (!Suena) return Hasta < 0f && !ConHueco && HuecoHasta < 0f;
                if (!(Hasta > Desde) || Hasta > Dura + 0.2f) return false;
                if (!ConHueco) return HuecoHasta < 0f;
                return HuecoDesde > Desde && HuecoHasta > HuecoDesde && HuecoHasta < Hasta;
            }
        }
        // EL MAPA TIENE QUE SER DE ESTE ARCHIVO. Las medidas se guardan por ruta, y en esa ruta puede haber hoy OTRO
        // archivo (una version mejor con el mismo nombre, un disco rehecho). Con el mapa del de antes se saltaba musica
        // de verdad al principio o en medio, o se cortaba el final de la cancion pagada. Solo se usa si el archivo dura lo
        // que se midio (con un segundo de margen); si no, suena entera, como antes de medirla, hasta que se vuelva a medir.
        internal const double MargenDura = 1.0;
        internal bool DeEsteArchivo(double segundos) { return Valido && Math.Abs(segundos - Dura) <= MargenDura; }
        internal static string Texto(float v) { return v < 0f ? "-" : v.ToString("0.##", CultureInfo.InvariantCulture); }
        internal static bool Lee(string t, out float v)
        {
            v = -1f;
            if (t == "-") return true;
            return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !float.IsNaN(v) && v >= 0f && v <= 36000f;
        }
    }
}
