// EL RECORTE DEL LIENZO COMO EN GDI+, NATIVO (port nativo, el dibujo; 8-11-2026). XP pone recortes con SetClip (que REEMPLAZA el de antes),
// los combina (Intersect, Exclude) y lee sus limites (ClipBounds, VisibleClipBounds). En Skia un recorte solo se quita volviendo a un Save:
// el codigo traducido hace c.Save() y ClipRect, y para reemplazarlo vuelve con Quita (que conserva la matriz) y pone el nuevo. Los
// algoritmos son los del Dibujo2D.cs de la capa de la transicion (ya borrada; sigue en la referencia), movidos aqui.
using System.Drawing;
using SkiaSharp;

namespace Rocola.Nativo
{
    public static class RecorteLienzo
    {
        // LOS LIMITES DEL RECORTE, EXACTOS (en las coordenadas del dibujo). LocalClipBounds de Skia los agranda un pixel por cada lado (por si
        // el recorte fuera suavizado); GDI+ no. Con ese pixel de mas, el renglon de al lado de un recorte "tocaba" el recorte: la lista del
        // disco, al pintar el renglon elegido, se quedaba tambien con la cinta del renglon de abajo (TextoMovilGpu.cs) y ese renglon salia
        // escrito dos veces, encimado.
        public static RectangleF Limites(SKCanvas c)
        {
            SKRectI d = c.DeviceClipBounds; SKMatrix inv;
            if (d.IsEmpty || !c.TotalMatrix.TryInvert(out inv)) return RectangleF.Empty;
            SKRect b = inv.MapRect(new SKRect(d.Left, d.Top, d.Right, d.Bottom));
            return RectangleF.FromLTRB(b.Left, b.Top, b.Right, b.Bottom);
        }
        // quita los recortes puestos despues del Save que dio 'cuenta' (ResetClip, o SetClip que reemplaza): el recorte se va, la posicion
        // (la matriz de ahora) se queda
        public static void Quita(SKCanvas c, int cuenta)
        {
            SKMatrix m = c.TotalMatrix;
            c.RestoreToCount(cuenta); c.SetMatrix(m);
        }
    }
}
