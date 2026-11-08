// EL PUENTE DE LA FRONTERA CON LA TARJETA, SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 2, grupo «frontera», 8-11-2026).
// La subida a la tarjeta (Codigo/D3D9Star.cs y Codigo/TexturasCompatiblesGpu.cs) ya recibe SKBitmap: los puntos se leen sin premultiplicar
// con Pixeles.CopiaSinPremultiplicar (lo que en XP era LockBits Format32bppArgb). Los archivos que todavia no estan traducidos (y los de otros
// grupos de esta tanda) le siguen pasando un Bitmap de la capa (Plataforma/Dibujo2D.cs): estas sobrecargas, con la firma de XP, toman sus
// puntos con PuenteDibujo.Mapa (los MISMOS, sin copia y sin hacerse dueno) y llaman a la traducida. La regla del puente: va del lado de lo
// que no esta traducido; nada de esto queda. Cada una se borra cuando ya nadie la llame (al traducir a los que la usan) y todas al final,
// con Dibujo2D.cs y Puente.cs.
//   - GpuStarfield: CreateOverlay, CreateCoverShared, UpdateOverlay y UpdateOverlayRegion (los llaman BuscadorLigeroGpu.cs,
//     BuscadorPortadasGpu.cs, CaratulasAdaptativas.cs, CaratulasSeparadasGpu.cs, FranjaLigeraGpu.cs y RespuestaGpu.cs);
//   - GpuPanel: Upload (lo llaman AsistenteEstrellaGpu.cs, AtraccionGpu.cs, AyudaCasillaGpu.cs, DiscoVivoGpu.cs, FranjaLigeraGpu.cs,
//     PantallaVivaGpu.cs, RespuestaGpu.cs, SuenaVivoGpu.cs y TextoMovilGpu.cs).
// Un Bitmap ya soltado (Dispose) no tiene puntos: PuenteDibujo.Mapa da null y la traducida hace lo que hacia con un null (lo dice
// PORT_frontera.md, «Costuras»).
#if ROCOLA_GPU
using System;
using System.Drawing;

namespace Rocola
{
    internal sealed partial class GpuStarfield
    {
        public IntPtr CreateOverlay(Rocola.Dibujo2D.Bitmap bmp, out float uMax, out float vMax) { return CreateOverlay(PuenteDibujo.Mapa(bmp), out uMax, out vMax); }
        public IntPtr CreateCoverShared(Rocola.Dibujo2D.Bitmap bmp, out float uMax, out float vMax) { return CreateCoverShared(PuenteDibujo.Mapa(bmp), out uMax, out vMax); }
        public bool UpdateOverlay(IntPtr tex, Rocola.Dibujo2D.Bitmap bmp, out float uMax, out float vMax) { return UpdateOverlay(tex, PuenteDibujo.Mapa(bmp), out uMax, out vMax); }
        public bool UpdateOverlayRegion(IntPtr tex, Rocola.Dibujo2D.Bitmap bmp, Rectangle area) { return UpdateOverlayRegion(tex, PuenteDibujo.Mapa(bmp), area); }
    }

    internal sealed partial class GpuPanel
    {
        private IntPtr Upload(IntPtr tex, ref int bw, ref int bh, Rocola.Dibujo2D.Bitmap bmp, out float u, out float v) { return Upload(tex, ref bw, ref bh, PuenteDibujo.Mapa(bmp), out u, out v); }
    }
}
#endif
