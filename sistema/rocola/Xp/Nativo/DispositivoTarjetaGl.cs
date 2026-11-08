// EL DISPOSITIVO DE LA TARJETA: LO DE OPENGL ES 2 (port nativo de XP, 8-11-2026).
//
// El dispositivo de XP (DispositivoTarjeta: D3D9Star.cs, PerformanceGpu.cs y TexturasCompatiblesGpu.cs) sigue con su logica tal cual (las
// caches de estado stTss, stBlend, stTex y stDireccion, juntar ordenes, pedidos/envios/cambios, potencias de 2 y borde, la imagen
// intermedia, las teselas, la calibracion UP o bufer, el reintento sin memoria). Donde llamaba a Direct3D 9, cada linea (marcada
// «EN LINUX») llama a OpenGL ES 2 directo o a lo de aqui, que es la parte de OpenGL que necesita los campos del dispositivo:
//   - CreaFondo / RehaceFondo / SueltaFondo: CreateDevice, Reset y Release. El fondo (el back buffer de Direct3D) es una textura con su
//     framebuffer, X8 (el alfa en 1: nunca se escribe), del tamano pedido; se pinta siempre en el (o en la imagen intermedia), nunca directo
//     a la pantalla: asi Mesa usa la regla de arriba a la izquierda de Direct3D 9 sin dar la vuelta. Los tres programas de las etapas de XP
//     (SombreadoresTarjeta) se compilan una vez.
//   - EstadoGl (InitStates): OpenGL es uno para todos (el compositor, el video de libmpv, la flecha, cada dispositivo): lo que Direct3D 9
//     tiene de fabrica y OpenGL no (sin recorte, sin tramado, sin profundidad ni stencil, la suma en la mezcla, el alfa sin escribir), los
//     vertices de XP por la memoria del programa (ARRAY_BUFFER a 0) y el destino.
//   - RestauraGl: si otro toco OpenGL desde el ultimo cuadro (PantallaTarjeta.Generacion), EmpiezaEscena (BeginScene) vuelve a poner en
//     OpenGL lo que el dispositivo tenia puesto (EstadoGl, el programa de stTss y la mezcla de stBlend) SIN tocar las caches de XP: en
//     Direct3D 9 nadie mas tocaba el estado del dispositivo, asi que la logica de XP (y su cuenta de cambios) sigue como en XP.
//   - PonDestino (SetRenderTarget): glBindFramebuffer, glViewport y la medida para el uniforme de los programas.
//   - Triangulos / TriangulosDeBufer / Triangulos2 (DrawPrimitiveUP y DrawPrimitive): fuera de la escena, como Direct3D 9, no se dibuja
//     (D3DERR_INVALIDCALL, y XP da el dispositivo por perdido).
//   - El bufer de vertices (CreateVertexBuffer, Lock/Unlock con DISCARD o NOOVERWRITE): glBufferData / glBufferSubData.
//   - DosTexturas / FinDosTexturas: la pasada de dos texturas (el grano y la vineta) con el programa fijo, la vineta en la unidad 1.
//   - PresentaFondo (Present): el fondo a la pantalla por PantallaTarjeta.
//   - OlvidaTextura: OpenGL repite los nombres de las texturas: una que se borra no puede seguir apuntada como puesta (stTex).
//   - LeeFondo: (pruebas) el fondo, en el orden de Direct3D (B, G, R, A; fila 0 arriba).
#if ROCOLA_GPU
using System;
using System.Collections.Generic;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed unsafe partial class DispositivoTarjeta
    {
        private uint fondoTex, fondoFbo;          // el fondo (el back buffer de Direct3D 9)
        private int fondoAncho, fondoAlto;
        private SombreadoresTarjeta sombreadores;
        private uint destinoFbo;                  // donde se pinta ahora: el fondo o la imagen intermedia (SetRenderTarget)
        private int destAncho, destAlto;
        private int generacionVista;              // la generacion de OpenGL al poner el estado (PantallaTarjeta)
        private static readonly bool erroresGl = Environment.GetEnvironmentVariable("ROCOLA_ERRORES_GL") == "1";   // (pruebas)
        private int cuadrosConError;

        // CreateDevice: el fondo del tamano pedido y los tres programas; en el contexto de OpenGL de este hilo (sin el, no hay tarjeta).
        // Devuelve el framebuffer del fondo (el «dispositivo»: no cambia con Reset), o cero.
        private IntPtr CreaFondo()
        {
            if (GL.GetString(GL.VERSION) == IntPtr.Zero) return IntPtr.Zero;
            fondoTex = TexturasGl.Nueva(bufAncho, bufAlto);
            if (fondoTex == 0) return IntPtr.Zero;
            fondoFbo = TexturasGl.Framebuffer(fondoTex, true);
            sombreadores = fondoFbo != 0 ? SombreadoresTarjeta.Compila() : null;
            if (sombreadores == null)
            {
                if (fondoFbo != 0) TexturasGl.BorraFramebuffer(fondoFbo);
                TexturasGl.Borra(fondoTex); fondoTex = fondoFbo = 0;
                return IntPtr.Zero;
            }
            fondoAncho = bufAncho; fondoAlto = bufAlto;
            destinoFbo = fondoFbo; destAncho = fondoAncho; destAlto = fondoAlto;
            TexturasGl.AlBorrar += OlvidaTextura;
            return (IntPtr)fondoFbo;
        }
        // Reset: el fondo a la medida pedida si cambio (lo de dentro, como Direct3D, solo se conserva si no cambia), el destino otra vez el
        // fondo, y las etapas de fabrica (para la traza). False si no hay memoria (D3DERR_OUTOFVIDEOMEMORY).
        private bool RehaceFondo()
        {
            if (sombreadores != null) sombreadores.EtapasDeFabrica();
            if (fondoAncho != bufAncho || fondoAlto != bufAlto)
            {
                uint nueva = TexturasGl.Nueva(bufAncho, bufAlto);
                if (nueva == 0) return false;
                if (!TexturasGl.Adjunta(fondoFbo, nueva, true)) { TexturasGl.Borra(nueva); return false; }
                TexturasGl.Borra(fondoTex);
                fondoTex = nueva; fondoAncho = bufAncho; fondoAlto = bufAlto;
            }
            destinoFbo = fondoFbo; destAncho = fondoAncho; destAlto = fondoAlto;
            return true;
        }
        // Release del dispositivo: el fondo, su framebuffer y los programas; el control de la ventana ya no tiene fondo que componer.
        private void SueltaFondo()
        {
            TexturasGl.AlBorrar -= OlvidaTextura;
            PantallaTarjeta.Olvida(ventana);
            if (sombreadores != null) { sombreadores.Suelta(); sombreadores = null; }
            if (fondoFbo != 0) TexturasGl.BorraFramebuffer(fondoFbo);
            if (fondoTex != 0) TexturasGl.Borra(fondoTex);
            fondoTex = fondoFbo = destinoFbo = 0;
        }

        // InitStates, la parte de OpenGL (ver arriba).
        private void EstadoGl()
        {
            GL.Disable(GlMas.STENCIL_TEST); GL.Disable(GL.SCISSOR_TEST); GL.Disable(GlMas.DITHER);
            GL.Disable(GlMas.POLYGON_OFFSET_FILL); GL.Disable(GlMas.SAMPLE_ALPHA_TO_COVERAGE); GL.Disable(GlMas.SAMPLE_COVERAGE);
            GlMas.BlendEquation(GlMas.FUNC_ADD);
            GlMas.ColorMask(1, 1, 1, 0);   // el fondo y la imagen intermedia son X8R8G8B8: el alfa no se escribe nunca
            GL.ActiveTexture(GL.TEXTURE0);
            GL.BindBuffer(GL.ARRAY_BUFFER, 0);
            GlMas.DisableVertexAttribArray(3); GlMas.DisableVertexAttribArray(4);
            PonDestino(destinoFbo, destAncho, destAlto);
            sombreadores.Olvida();
            generacionVista = PantallaTarjeta.Generacion;
        }
        // SetRenderTarget: el framebuffer, la vista entera (como Direct3D al cambiar de destino) y la medida para los programas.
        private void PonDestino(uint fbo, int ancho, int alto)
        {
            GL.BindFramebuffer(GL.FRAMEBUFFER, fbo);
            GL.Viewport(0, 0, ancho, alto);
            destinoFbo = fbo; destAncho = ancho; destAlto = alto;
        }
        // BeginScene: lo soltado desde otros hilos se borra, la traza y, si otro toco OpenGL desde el ultimo cuadro, el estado otra vez.
        private bool EmpiezaEscena()
        {
            TexturasGl.BorraPendientes();
            TrazaTarjeta.Cuadro(PantallaTarjeta.AhoraMs);
            if (generacionVista != PantallaTarjeta.Generacion) RestauraGl();
            return true;
        }
        // Lo que el dispositivo tenia puesto, otra vez en OpenGL (las caches de XP no cambian). La textura no hace falta: XP olvida stTex en
        // cada cuadro (BeginFrame) y la vuelve a poner; la envoltura y el filtro van con cada textura y nadie mas los toca.
        private void RestauraGl()
        {
            GL.Disable(GlMas.DEPTH_TEST); GL.Disable(GlMas.CULL_FACE);   // (lo que InitStates pone antes de EstadoGl: ZENABLE 0, CULLMODE NONE)
            EstadoGl();
            if (stTss == 1) sombreadores.Usa(sombreadores.Color);
            else if (stTss == 2) sombreadores.Usa(sombreadores.TexturaPorColor);
            if (stBlend == 1) GL.Disable(GL.BLEND);
            else if (stBlend > 1) { GL.Enable(GL.BLEND); GL.BlendFunc(GL.SRC_ALPHA, stBlend == 2 ? GL.ONE : GL.ONE_MINUS_SRC_ALPHA); }
        }

        // ---- dibujar ----
        // DrawPrimitiveUP (TRIANGLELIST) de los vertices de XP. Fuera de la escena, como Direct3D: no (D3DERR_INVALIDCALL).
        private bool Triangulos(IntPtr datos, int tris)
        {
            if (!inScene) return false;
            sombreadores.Triangulos(datos, tris * 3, destAncho, destAlto, destinoFbo == fondoFbo);
            return true;
        }
        private bool Triangulos2(IntPtr datos, int tris)
        {
            if (!inScene) return false;
            sombreadores.Triangulos2(datos, tris * 3, destAncho, destAlto, destinoFbo == fondoFbo);
            return true;
        }
        // CreateVertexBuffer (DYNAMIC | WRITEONLY): un bufer de OpenGL de ese tamano, o cero si no hay memoria.
        private static IntPtr CreaBuferVertices(int bytes)
        {
            while (GlMas.GetError() != 0) { }
            uint b; GL.GenBuffers(1, &b);
            GL.BindBuffer(GL.ARRAY_BUFFER, b);
            GL.BufferData(GL.ARRAY_BUFFER, (IntPtr)bytes, null, GlMas.DYNAMIC_DRAW);
            GL.BindBuffer(GL.ARRAY_BUFFER, 0);
            if (GlMas.GetError() != 0) { GlMas.DeleteBuffers(1, &b); return IntPtr.Zero; }
            return (IntPtr)b;
        }
        // Lock + copiar + Unlock: los vertices de XP al bufer, en 'desde'. DISCARD (al volver al principio del anillo): se pide memoria nueva
        // (glBufferData sin datos) para no esperar a que la tarjeta acabe con lo de antes; NOOVERWRITE: se escribe detras de lo usado.
        private void SubeVertices(uint b, int desde, int bytes, IntPtr datos, bool descarta)
        {
            GL.BindBuffer(GL.ARRAY_BUFFER, b);
            if (descarta) GL.BufferData(GL.ARRAY_BUFFER, (IntPtr)(verticesCap * Tarjeta.VtxSize), null, GlMas.DYNAMIC_DRAW);
            GlMas.BufferSubData(GL.ARRAY_BUFFER, (IntPtr)desde, (IntPtr)bytes, (void*)datos);
            GL.BindBuffer(GL.ARRAY_BUFFER, 0);
        }
        // SetStreamSource + DrawPrimitive (TRIANGLELIST) desde el vertice 'primero' del bufer.
        private bool TriangulosDeBufer(uint b, int primero, int tris)
        {
            if (!inScene) return false;
            GL.BindBuffer(GL.ARRAY_BUFFER, b);
            sombreadores.TriangulosDeBufer(primero, tris * 3, destAncho, destAlto);
            GL.BindBuffer(GL.ARRAY_BUFFER, 0);
            return true;
        }
        private static void SueltaBuferVertices(uint b) { GlMas.DeleteBuffers(1, &b); }

        // La pasada de dos texturas: las etapas 0 y 1 son el programa fijo «dos texturas»; la vineta en la unidad 1, con CLAMP y LINEAR (los
        // de su creacion); la mezcla ONE / INVSRCALPHA. False si la vineta no es una textura de la tarjeta (SetTexture daria INVALIDCALL).
        private bool DosTexturas(IntPtr t1)
        {
            if (!TexturasGl.Existe((uint)t1)) return false;
            sombreadores.Usa(sombreadores.DosTexturas);
            GL.ActiveTexture(GL.TEXTURE0 + 1);
            GL.BindTexture(GL.TEXTURE_2D, (uint)t1);
            TexturasGl.Envoltura((uint)t1, false);
            GL.ActiveTexture(GL.TEXTURE0);
            GL.Enable(GL.BLEND);
            GL.BlendFunc(GL.ONE, GL.ONE_MINUS_SRC_ALPHA);
            return true;
        }
        // Al acabar: la etapa 1 apagada y sin textura (SetTexture(1, NULL)).
        private void FinDosTexturas()
        {
            GL.ActiveTexture(GL.TEXTURE0 + 1);
            GL.BindTexture(GL.TEXTURE_2D, 0);
            GL.ActiveTexture(GL.TEXTURE0);
            sombreadores.FinDosTexturas();
        }

        // Present: el fondo a la pantalla (PantallaTarjeta); lo soltado desde otros hilos, fuera.
        private void PresentaFondo()
        {
            if (erroresGl) { uint e = GlMas.GetError(); if (e != 0) { cuadrosConError++; Console.WriteLine("tarjeta: error de OpenGL 0x" + e.ToString("x") + " (" + cuadrosConError + " cuadros con error)"); while (GlMas.GetError() != 0) { } } }
            TrazaTarjeta.Presenta();
            PantallaTarjeta.Presenta(ventana, fondoTex, fondoAncho, fondoAlto, presentaSinEspera);
            TexturasGl.BorraPendientes();
        }

        // Una textura que se borra deja de estar puesta (OpenGL puede dar su nombre a otra).
        private void OlvidaTextura(uint n) { if (stTex == (IntPtr)n) stTex = new IntPtr(-1); }

        // (pruebas) lo pintado en el fondo: B, G, R, A por punto, fila 0 arriba (la del Direct3D de XP).
        internal byte[] LeeFondo(out int ancho, out int alto)
        {
            ancho = fondoAncho; alto = fondoAlto;
            if (fondoFbo == 0) return null;
            byte[] px = new byte[ancho * alto * 4];
            GL.BindFramebuffer(GL.FRAMEBUFFER, fondoFbo);
            GL.PixelStorei(GL.PACK_ALIGNMENT, 4);
            fixed (byte* p = px) GL.ReadPixels(0, 0, ancho, alto, GL.RGBA, GL.UNSIGNED_BYTE, p);
            GL.BindFramebuffer(GL.FRAMEBUFFER, destinoFbo);
            return px;
        }
    }
}
#endif
