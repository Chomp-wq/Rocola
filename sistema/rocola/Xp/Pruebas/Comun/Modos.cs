// LOS MODOS DEL ESTADO DEL DIBUJO, PARA LOS ARNESES (port nativo, oleada 8b, 13-11-2026). Los arneses guardan el estado con que se llama
// al programa (suavizado, modo de letra, copia, filtro, medio pixel) con estos tipos propios y no con los de la capa de GDI+ (Dibujo2D.cs, que
// se borra en N7). Los nombres y los valores son los de GDI+ a proposito: la referencia congelada los lee por su numero y las lineas que
// escriben los arneses llevan sus nombres (ToString) y tienen que salir iguales en las dos.
namespace Banco
{
    internal enum ModoSuave { Invalid = -1, Default = 0, HighSpeed = 1, HighQuality = 2, None = 3, AntiAlias = 4 }
    internal enum ModoLetra { SystemDefault = 0, SingleBitPerPixelGridFit = 1, SingleBitPerPixel = 2, AntiAliasGridFit = 3, AntiAlias = 4, ClearTypeGridFit = 5 }
    internal enum ModoCopia { SourceOver = 0, SourceCopy = 1 }
    internal enum ModoPixel { Invalid = -1, Default = 0, HighSpeed = 1, HighQuality = 2, None = 3, Half = 4 }
    internal enum ModoFiltro { Invalid = -1, Default = 0, Low = 1, High = 2, Bilinear = 3, Bicubic = 4, NearestNeighbor = 5, HighQualityBilinear = 6, HighQualityBicubic = 7 }
}
