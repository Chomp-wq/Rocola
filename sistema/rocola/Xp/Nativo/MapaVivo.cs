// UN MAPA YA SOLTADO, COMO LO VE GDI+ (port nativo, grupo «cache-portadas», 12-11-2026). En XP las portadas de la cache son Image de GDI+:
// leer la medida de una ya soltada (Dispose) lanza ArgumentException («Parameter is not valid.»: GdipGetImageWidth con la imagen nula
// contesta InvalidParameter), y donde XP atrapa esa excepcion la rocola sigue (CreateTexturePow2 y UpdateTexturePow2 de D3D9Star.cs dan
// IntPtr.Zero o false; el trabajador de portadas de Form1.Portadas.cs se queda sin los tonos). Aqui la cache guarda SKBitmap, y un SKBitmap
// soltado no avisa: su Handle queda en cero y leer su medida llama a Skia con un puntero nulo (violacion de segmento: se cierra la rocola).
// Por eso, donde XP leia la medida de una portada que puede venir de la cache y atrapaba lo que lanzara, se lee con esto: comprueba ANTES
// y lanza lo mismo que GDI+. Un mapa nulo da NullReferenceException, como b.Width en XP.
using System;
using SkiaSharp;

namespace Rocola.Nativo
{
    internal static class MapaVivo
    {
        // ¿ya se solto? (Dispose de SkiaSharp deja el Handle en cero)
        public static bool Soltado(SKBitmap b) { return b != null && b.Handle == IntPtr.Zero; }

        // Image.Width e Image.Height de GDI+: la medida, o ArgumentException si la imagen ya se solto
        public static int Ancho(SKBitmap b) { if (Soltado(b)) throw new ArgumentException("Parameter is not valid."); return b.Width; }
        public static int Alto(SKBitmap b) { if (Soltado(b)) throw new ArgumentException("Parameter is not valid."); return b.Height; }
    }
}
