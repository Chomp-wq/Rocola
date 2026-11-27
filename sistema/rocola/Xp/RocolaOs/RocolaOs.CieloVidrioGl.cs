// EL CIELO VIVO DE ROCOLA OS, VIDRIO: LA PRIMITIVA DE «VER LO DE DETRAS» (VI1, VI2, VI4, VI6; FASE11_cielo-vidrio.md).
//
// Las piezas de vidrio desenfocan lo que ya esta pintado detras de ellas. El fondo de la tarjeta (el back buffer de Direct3D 9 de XP) es una
// textura con su framebuffer (Nativo/DispositivoTarjetaGl.cs), asi que NO hace falta copiarlo: se muestrea directo mientras se pinta en OTRO
// framebuffer (muestrear la textura donde se pinta no se hace: aqui nunca se hace). Para cada pieza, el recorte de lo de detras (la zona de la
// pieza):
//   1. se reduce a la mitad  (fondo -> mitad: un estirado bilineal que promedia 2x2),
//   2. y otra vez a la mitad (mitad -> S0: 1/4 de cada lado, 1/16 de los puntos; promedia 4x4 en total, sin aliasing),
//   3. se desenfoca en el 1/4 con 9 tomas separables (horizontal S0 -> S1, vertical S1 -> S0) de 5 lecturas cada una (el truco de las lecturas
//      bilineales entre dos puntos: pesos 0,2270 en el centro, 0,3162 a 1,3846 y 0,0703 a 3,2308 puntos de cada lado), con los tres programas fijos de
//      XP: la primera lectura sin mezcla y las demas con la mezcla normal y el alfa w/(suma hasta ahi), que da el promedio ponderado exacto,
//   4. y S0 queda como la textura de «lo de detras desenfocado» de esa pieza (la ranura), que el pintor estira de vuelta sobre la zona.
// Con sigma de unos 2 puntos del 1/4 son ~8 px de la pantalla: vidrio esmerilado, no niebla. Lo que cuesta, por pieza: 2 estirados + 10 lecturas +
// 1 dibujo, todos de menos de 1/4 de la pantalla (la ficha de 480x300 son 120x75 puntos), con 4 cambios de destino. CACHE: la ranura recuerda su
// zona y el reloj de su ultimo desenfoque; mientras la zona sea la misma y no hayan pasado `cadaMs` (66 ms: 15 desenfoques por segundo; lo de
// detras se mueve despacio), se reusa tal cual (0 dibujos). Cada ranura es de una pieza (su textura S0 y S1 son exactas del tamano del 1/4 de su
// zona: asi las lecturas del borde se quedan en el borde, CLAMP, y no leen basura).
// Nada de esto corre sin ROCOLA_OS ni con vidrio apagado (nadie lo llama). En la pantalla, dentro del cuadro (inScene).
#if ROCOLA_GPU
using System;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class DispositivoTarjeta
    {
        private sealed class RocolaOsRanura
        {
            internal uint T0, T1, F0, F1;            // S0 y S1 (y sus framebuffers): el 1/4 de la zona
            internal int W, H;                       // su medida
            internal int ZX, ZY, ZW, ZH;             // la zona de la pantalla que tiene desenfocada
            internal double Ms = -1e9;               // cuando se hizo
            internal bool Hecha;
        }
        private const int RocolaOsRanuras = 8;
        private RocolaOsRanura[] rocolaOsRanuras;
        private uint rocolaOsMitadT, rocolaOsMitadF; private int rocolaOsMitadW, rocolaOsMitadH;
        private bool rocolaOsVidrioSinSoporte;
        internal int RocolaOsVidrioHechos, RocolaOsVidrioReusados;   // (pruebas: cuantos desenfoques se hicieron y cuantos se reusaron)
        internal bool RocolaOsVidrioNoPuede { get { return rocolaOsVidrioSinSoporte; } }

        // el dibujo de un cuadrado con la textura puesta (X, Y y U, V a los cuatro vertices; el centro del punto en el entero: -0,5 a los bordes)
        private readonly Tarjeta.Vtx[] rocolaOsQuad = new Tarjeta.Vtx[6];
        private void RocolaOsCuadro(float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1, uint c)
        {
            Tarjeta.Vtx[] q = rocolaOsQuad;
            RocolaOsPonV(ref q[0], x0, y0, u0, v0, c); RocolaOsPonV(ref q[1], x1, y0, u1, v0, c); RocolaOsPonV(ref q[2], x1, y1, u1, v1, c);
            RocolaOsPonV(ref q[3], x0, y0, u0, v0, c); RocolaOsPonV(ref q[4], x1, y1, u1, v1, c); RocolaOsPonV(ref q[5], x0, y1, u0, v1, c);
        }
        private static void RocolaOsPonV(ref Tarjeta.Vtx v, float x, float y, float u, float vv, uint c) { v.X = x - 0.5f; v.Y = y - 0.5f; v.Z = 0f; v.Rhw = 1f; v.Color = c; v.U = u; v.V = vv; }

        // pintar en otro framebuffer: lo pendiente va antes al destino de antes
        private void RocolaOsDestino(uint fbo, int w, int h) { Junta(); PonDestino(fbo, w, h); }
        private bool RocolaOsTextura(ref uint tex, ref uint fbo, int w, int h)
        {
            if (tex != 0 && fbo != 0) return true;
            tex = TexturasGl.Nueva(w, h);
            if (tex == 0) return false;
            fbo = TexturasGl.Framebuffer(tex, true);
            if (fbo == 0) { TexturasGl.Borra(tex); tex = 0; return false; }
            return true;
        }
        private void RocolaOsSueltaPar(ref uint tex, ref uint fbo)
        {
            if (fbo != 0) { TexturasGl.BorraFramebuffer(fbo); fbo = 0; }
            if (tex != 0) { if (stTex == (IntPtr)tex) stTex = new IntPtr(-1); TexturasGl.Borra(tex); tex = 0; }
        }

        // Una ranura con sus dos texturas del tamano pedido (se rehacen si cambia); null si no hay memoria.
        private RocolaOsRanura RocolaOsDaRanura(int n, int w, int h)
        {
            if (rocolaOsRanuras == null) rocolaOsRanuras = new RocolaOsRanura[RocolaOsRanuras];
            if (n < 0 || n >= rocolaOsRanuras.Length) return null;
            RocolaOsRanura r = rocolaOsRanuras[n];
            if (r == null) rocolaOsRanuras[n] = r = new RocolaOsRanura();
            if (r.W != w || r.H != h || r.T0 == 0 || r.T1 == 0)
            {
                RocolaOsSueltaPar(ref r.T0, ref r.F0); RocolaOsSueltaPar(ref r.T1, ref r.F1);
                r.Hecha = false; r.W = r.H = 0;
                if (!RocolaOsTextura(ref r.T0, ref r.F0, w, h) || !RocolaOsTextura(ref r.T1, ref r.F1, w, h)) { RocolaOsSueltaPar(ref r.T0, ref r.F0); RocolaOsSueltaPar(ref r.T1, ref r.F1); return null; }
                r.W = w; r.H = h;
            }
            return r;
        }

        // LAS 9 TOMAS (5 lecturas) en una direccion: de 'src' a 'dst' (framebuffer F), del tamano w x h; horizontal o vertical.
        private static readonly float[] RocolaOsDesv = { 0f, 1.3846153846f, -1.3846153846f, 3.2307692308f, -3.2307692308f };
        private static readonly float[] RocolaOsPeso = { 0.2270270270f, 0.3162162162f, 0.3162162162f, 0.0702702703f, 0.0702702703f };
        private void RocolaOsToma(uint src, uint dstF, int w, int h, bool horizontal)
        {
            RocolaOsDestino(dstF, w, h);
            float cum = 0f;
            for (int i = 0; i < 5; i++)
            {
                cum += RocolaOsPeso[i];
                float a = RocolaOsPeso[i] / cum;
                float du = horizontal ? RocolaOsDesv[i] / w : 0f, dv = horizontal ? 0f : RocolaOsDesv[i] / h;
                uint c = ((uint)(a * 255f + 0.5f) << 24) | 0x00FFFFFFu;
                RocolaOsCuadro(0, 0, w, h, du, dv, 1f + du, 1f + dv, c);
                Tex((IntPtr)src); TssModulate(); Blend(i == 0 ? 1 : 3);
                Draw(rocolaOsQuad, 2);
            }
        }

        // DESENFOCA la zona (x, y, w, h) de lo ya pintado en el fondo, en la ranura n. Devuelve la textura desenfocada (cuadrada exacta al 1/4 de la
        // zona; U, V de 0 a 1 son la zona entera), o false si no se puede (sin tarjeta, fuera de la escena, sin memoria: el pintor no pinta el vidrio de
        // detras y la pieza queda como la de XP). Reusa lo hecho si la zona es la misma y hace menos de cadaMs.
        internal bool RocolaOsVidrioDesenfoca(int n, int x, int y, int w, int h, double ahoraMs, double cadaMs, out IntPtr tex)
        {
            tex = IntPtr.Zero;
            if (rocolaOsVidrioSinSoporte) return false;
            if (CintaTarjeta.Graba && CintaTarjeta.Fuera) return false;   // (pruebas: ROCOLA_CINTA: la cinta no sabe de esto)
            if (dev == IntPtr.Zero || lost || !inScene || enMedia || fondoFbo == 0 || sombreadores == null || VinetaPuntos) return false;
            if (x < 0) { w += x; x = 0; }
            if (y < 0) { h += y; y = 0; }
            if (x + w > fondoAncho) w = fondoAncho - x;
            if (y + h > fondoAlto) h = fondoAlto - y;
            if (w < 16 || h < 16) return false;
            int hw = (w + 1) / 2, hh = (h + 1) / 2, qw = (hw + 1) / 2, qh = (hh + 1) / 2;
            RocolaOsRanura r = RocolaOsDaRanura(n, qw, qh);
            if (r == null) { rocolaOsVidrioSinSoporte = true; return false; }
            if (r.Hecha && r.ZX == x && r.ZY == y && r.ZW == w && r.ZH == h && ahoraMs - r.Ms >= 0 && ahoraMs - r.Ms < cadaMs)
            { RocolaOsVidrioReusados++; tex = (IntPtr)r.T0; return true; }
            // la mitad (compartida; se rehace si hace falta mas grande)
            if (rocolaOsMitadT == 0 || rocolaOsMitadW < hw || rocolaOsMitadH < hh)
            {
                RocolaOsSueltaPar(ref rocolaOsMitadT, ref rocolaOsMitadF);
                int mw = Math.Max(hw, (fondoAncho + 1) / 2), mh = Math.Max(hh, (fondoAlto + 1) / 2);
                if (!RocolaOsTextura(ref rocolaOsMitadT, ref rocolaOsMitadF, mw, mh)) { rocolaOsVidrioSinSoporte = true; return false; }
                rocolaOsMitadW = mw; rocolaOsMitadH = mh;
            }
            // 1: fondo -> mitad. La zona entera al (0,0)-(hw,hh) de la mitad.
            float fw = fondoAncho, fh = fondoAlto;
            RocolaOsDestino(rocolaOsMitadF, rocolaOsMitadW, rocolaOsMitadH);
            RocolaOsCuadro(0, 0, hw, hh, x / fw, y / fh, (x + w) / fw, (y + h) / fh, 0xFFFFFFFFu);
            Tex((IntPtr)fondoTex); TssModulate(); Blend(1); Draw(rocolaOsQuad, 2);
            // 2: mitad -> S0 (el 1/4)
            RocolaOsDestino(r.F0, qw, qh);
            RocolaOsCuadro(0, 0, qw, qh, 0f, 0f, hw / (float)rocolaOsMitadW, hh / (float)rocolaOsMitadH, 0xFFFFFFFFu);
            Tex((IntPtr)rocolaOsMitadT); TssModulate(); Blend(1); Draw(rocolaOsQuad, 2);
            // 3: el desenfoque separable: S0 -> S1 (horizontal), S1 -> S0 (vertical)
            RocolaOsToma(r.T0, r.F1, qw, qh, true);
            RocolaOsToma(r.T1, r.F0, qw, qh, false);
            // de vuelta al fondo, con lo pendiente mandado
            Junta();
            PonDestino(fondoFbo, fondoAncho, fondoAlto);
            r.ZX = x; r.ZY = y; r.ZW = w; r.ZH = h; r.Ms = ahoraMs; r.Hecha = true;
            RocolaOsVidrioHechos++;
            tex = (IntPtr)r.T0;
            return true;
        }

        // (al soltarse el dispositivo) las texturas del vidrio
        internal void RocolaOsVidrioSuelta()
        {
            if (rocolaOsRanuras != null)
                for (int i = 0; i < rocolaOsRanuras.Length; i++)
                {
                    RocolaOsRanura r = rocolaOsRanuras[i];
                    if (r == null) continue;
                    RocolaOsSueltaPar(ref r.T0, ref r.F0); RocolaOsSueltaPar(ref r.T1, ref r.F1); r.Hecha = false;
                }
            RocolaOsSueltaPar(ref rocolaOsMitadT, ref rocolaOsMitadF);
        }
        // (pruebas) lo que cuesta cada desenfoque
        internal bool RocolaOsVidrioHecha(int n) { return rocolaOsRanuras != null && n >= 0 && n < rocolaOsRanuras.Length && rocolaOsRanuras[n] != null && rocolaOsRanuras[n].Hecha; }
    }

    internal sealed partial class GpuStarfield
    {
        // (el dispositivo es de GpuStarfield: los pintores hablan con el)
        internal bool RocolaOsVidrioDesenfoca(int n, int x, int y, int w, int h, double ahoraMs, double cadaMs, out IntPtr tex)
        {
            tex = IntPtr.Zero;
            return Ok && gpu.RocolaOsVidrioDesenfoca(n, x, y, w, h, ahoraMs, cadaMs, out tex);
        }
        internal void RocolaOsVidrioSuelta() { if (gpu != null) gpu.RocolaOsVidrioSuelta(); }
        internal int RocolaOsVidrioHechos { get { return gpu != null ? gpu.RocolaOsVidrioHechos : 0; } }
        internal int RocolaOsVidrioReusados { get { return gpu != null ? gpu.RocolaOsVidrioReusados : 0; } }
        internal bool RocolaOsVidrioNoPuede { get { return gpu != null && gpu.RocolaOsVidrioNoPuede; } }
    }
}
#endif
