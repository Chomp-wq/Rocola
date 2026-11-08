// EL PUENTE DEL GRUPO «ACABADO-ESTILO», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 2 del dibujo, 8-11-2026). GuiaRojoGpu.cs (la guia
// del rojo mantenido) ya dibuja con SkiaSharp directo y las funciones nativas (Xp/Nativo); donde la llama codigo que todavia dibuja con la capa
// que imita a GDI+ (Dibujo2D.cs), pasa por aqui. La regla: el puente va del lado de lo que no esta traducido, y los que llaman no cambian. Nada
// de esto queda: cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al final, con Dibujo2D.cs y Puente.cs
// (PORT_acabado-estilo.md dice cual es cual).
//   - La firma de XP de lo que llama con un Graphics Form1.Video.cs (EnsureUiOverlay: DrawGuiaRojo). Es delgada: pasa a la traducida el lienzo
//     del Graphics (con su matriz y su recorte) y su estado (PuenteDibujo.Estado). La traducida no le cambia el estado al Graphics, asi que no
//     hay nada que devolverle.
#if ROCOLA_GPU
using System;
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        // ---- LA FIRMA DE XP PARA Form1.Video.cs (EnsureUiOverlay: la imagen de la interfaz), de GuiaRojoGpu.cs
        private void DrawGuiaRojo(Graphics g)
        { EstadoLienzo e = PuenteDibujo.Estado(g); DrawGuiaRojo(PuenteDibujo.Lienzo(g), e.Suave, e.Copia, e.Medio); }
    }
}
#endif
