// EL FORMATO QUE TENIA UN MAPA EN XP (port nativo, grupo «pared», 11-11-2026). Aqui todos los mapas son SKBitmap en BGRA premultiplicado
// (lo que en GDI+ era Format32bppPArgb), pero en XP cada Bitmap llevaba su PixelFormat (un JPEG leido es de 24 bits sin alfa; uno hecho
// con new Bitmap(w, h), de 32 con alfa sin premultiplicar...) y hay codigo de XP que lee los puntos segun ese formato: la paleta de una
// portada (PaletaCaratulaGpu.cs, MuestrasPaleta) toma los bytes tal cual en PArgb, sin premultiplicar en Argb, con el alfa opaco en los
// Rgb, y en cualquier otro punto por punto (GetPixel). Para que salga lo mismo, quien tiene el mapa dice que formato tenia.
// Solo es el dato: aqui no hay cuentas (las de leer los puntos en cada formato estan en Pixeles.cs).
namespace Rocola.Nativo
{
    public enum FormatoMapa { PArgb32 = 0, Argb32 = 1, Rgb32 = 2, Rgb24 = 3, Otro = 4 }
}
