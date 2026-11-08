// LO QUE EL DISPOSITIVO DE XP LE PREGUNTA A DIRECT3D 9 SOBRE LA TARJETA, CONTESTADO CON OPENGL ES 2 (traduccion de XP, fase 1).
// IDirect3DDevice9::GetDeviceCaps [7] (TexturasCompatiblesGpu.cs) llena D3DCAPS9; la rocola lee solo unos campos (CapsTextura, con sus
// desplazamientos). Lo que se contesta es lo que hace Direct3DaGL.cs.
using System;

namespace Rocola
{
    internal sealed partial class D3D9Device
    {
        private int GetDeviceCaps(IntPtr dispositivo, ref CapsTextura caps)
        {
            if (D3D9.ObjetoDe<D3D9.DispositivoGl>(dispositivo) == null) return D3D9.D3DERR_INVALIDCALL;
            uint max = (uint)D3D9.EnGl.MaxTextura();      // GL_MAX_TEXTURE_SIZE
            caps.W = max; caps.H = max;                   // MaxTextureWidth / MaxTextureHeight
            caps.Intervalos = 0x80000001u;                // PresentationIntervals: IMMEDIATE y ONE
            caps.MezclaOrigen = 0x3FFFu;                  // SrcBlendCaps: de ZERO a BOTHINVSRCALPHA, y BLENDFACTOR (con INVBLENDFACTOR)
            caps.Fvf = 8u;                                // FVFCaps: hasta 8 juegos de coordenadas de textura
            caps.OperacionesTextura = 0x039EFFFFu;        // TextureOpCaps: las de las etapas que hace Direct3DaGL (todas menos PREMODULATE y
                                                          // BUMPENVMAP / BUMPENVMAPLUMINANCE)
            caps.TexturasALaVez = (uint)D3D9.EtapasGl;    // MaxSimultaneousTextures
            return D3D9.D3D_OK;
        }
    }
}
