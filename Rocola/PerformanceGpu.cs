// ====================================================================================================
//  PERFORMANCE (13-10-2026): MENOS TRABAJO PARA LA TARJETA CON LA PANTALLA CASI IGUAL
// ----------------------------------------------------------------------------------------------------
//  Peticion del usuario: «Haz las 4 cosas, pon los nuevos settings graficos como un preset llamado performance en la
//  ventana de imagen, y el benchmark renuevalo para que sea mas inteligente y mejor». Las cuatro cosas:
//
//   1) MEDIR DONDE SE VA EL CUADRO. Cada cuadro cuenta cuantas ordenes de dibujo se piden, cuantas se mandan de
//      verdad, cuantos cambios de estado hay y -mientras mide el medidor de Ctrl+F3- cuantas pantallas se pintan. Y
//      el medidor puede pedir presentar SIN ESPERAR AL MONITOR: asi ve lo que cuesta el cuadro de verdad (procesador o
//      tarjeta), y no los saltos de 60 a 30 que impone la espera.
//   2) EL CIELO A MEDIA RESOLUCION (interruptor, apagado de fabrica). El degradado, las nebulosas y la via lactea se
//      pintan en una imagen intermedia de la mitad de ancho y de alto, y esa imagen se estira UNA vez a la pantalla.
//      Las nebulosas y la via lactea ya vienen de imagenes de un cuarto de la pantalla: a media resolucion les sobra
//      detalle. Casi identico; la tarjeta pinta un cuarto de esos pixeles.
//   3) GRANO Y VINETA EN UNA PASADA (interruptor, apagado de fabrica). Dos texturas a la vez: el grano y la vineta
//      se mezclan en la propia tarjeta y la pantalla se lee y escribe una vez en vez de dos. El grano pasa a ir donde
//      iba la vineta, encima de las estrellas: un punto de mas o de menos en algun pixel de estrella.
//   4) JUNTAR ORDENES. Lo chico seguido y con el mismo estado (misma textura, misma mezcla) se manda en una sola
//      orden. Es exactamente lo mismo -los triangulos van en el mismo orden con el mismo estado-, asi que es
//      automatico y sin interruptor (AGENTS.md).
//
//  Solo con tarjeta grafica. El dibujo por procesador no se toca. Si la tarjeta no puede algo (imagen intermedia,
//  dos texturas, presentar sin esperar), se dibuja como siempre.
// ====================================================================================================
#if ROCOLA_GPU
using System;
using System.Runtime.InteropServices;

namespace Rocola
{
    internal static partial class D3D9
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int GetPtrDel(IntPtr self, uint a, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int ValidaDel(IntPtr self, out uint passes);
        [StructLayout(LayoutKind.Sequential)]
        public struct Vtx2 { public float X, Y, Z, Rhw; public uint Color; public float U, V, U2, V2; }
        public const uint FVF2 = 0x004 | 0x040 | 0x200;   // XYZRHW | DIFFUSE | TEX2
        public const int Vtx2Size = 36;
        public const uint USAGE_RENDERTARGET = 1, POOL_DEFAULT = 0, FMT_X8R8G8B8 = 22;
        public const uint PRESENT_INTERVAL_IMMEDIATE = 0x80000000u;
        public const uint TSS_TEXCOORDINDEX = 11;
        public const uint TOP_DISABLE = 1, TOP_ADDSMOOTH = 11;
        public const uint TA_CURRENT = 1, TA_COMPLEMENT = 0x10, TA_ALPHAREPLICATE = 0x20;
        // Los huecos de la tabla del dispositivo y de la textura que se usan aqui (IDirect3DDevice9 / IDirect3DTexture9).
        public const int SlotSetRenderTarget = 37, SlotGetRenderTarget = 38, SlotValidateDevice = 70, SlotGetSurfaceLevel = 18;
    }

    internal sealed partial class D3D9Device
    {
        // ---------------- LO QUE PUEDE LA TARJETA ----------------
        internal bool CapsDosTexturas, CapsSinEspera;
        private void LeeCapacidades(uint intervalos, uint mezclaOrigen, uint fvf, uint operaciones, uint texturasALaVez)
        {
            CapsSinEspera = (intervalos & D3D9.PRESENT_INTERVAL_IMMEDIATE) != 0;
            // Dos texturas a la vez, dos juegos de coordenadas, mezcla con "uno" de origen, y las operaciones que usa la
            // pasada unida: modular, elegir y la suma suave (a + b - a*b).
            CapsDosTexturas = texturasALaVez >= 2 && (fvf & 0xFFFF) >= 2 && (mezclaOrigen & 0x2) != 0
                && (operaciones & 0x8) != 0 && (operaciones & 0x2) != 0 && (operaciones & 0x400) != 0;
        }

        // ---------------- CONTAR (1) ----------------
        // Pedidos: ordenes que pide el programa. Envios: las que llegan al controlador (menos, si se juntan). Cambios:
        // cambios de estado de verdad (textura, mezcla, modo). AreaPx: pixeles pintados, solo si se pide (el medidor).
        internal int Pedidos, Envios, Cambios;
        internal double AreaPx;
        internal bool CuentaArea;
        internal void EmpiezaCuenta(bool area) { Pedidos = Envios = Cambios = 0; AreaPx = 0; CuentaArea = area; }
        private static float Recorta(float v, float max) { return v < 0f ? 0f : (v > max ? max : v); }
        // El area de los triangulos dentro del destino, con la regla de centros de pixel. Para lo recto (casi todo) es
        // exacta; para lo girado (cubos, halos) es una aproximacion buena.
        private double AreaDe(D3D9.Vtx[] v, int tris)
        {
            float w = enMedia ? mediaW : (float)pp.BackBufferWidth, h = enMedia ? mediaH : (float)pp.BackBufferHeight;
            double a = 0;
            for (int i = 0, o = 0; i < tris; i++, o += 3)
            {
                float x0 = Recorta(v[o].X + .5f, w), y0 = Recorta(v[o].Y + .5f, h);
                float x1 = Recorta(v[o + 1].X + .5f, w), y1 = Recorta(v[o + 1].Y + .5f, h);
                float x2 = Recorta(v[o + 2].X + .5f, w), y2 = Recorta(v[o + 2].Y + .5f, h);
                a += Math.Abs((double)(x1 - x0) * (y2 - y0) - (double)(x2 - x0) * (y1 - y0)) * 0.5;
            }
            return a;
        }

        // ---------------- JUNTAR ORDENES (4) ----------------
        // Una orden chica (menos de JuntaMaxTris triangulos) no se manda en el acto: se copia a un arreglo y se manda
        // junto con las que vengan detras CON EL MISMO ESTADO. Cualquier cambio de estado (textura, mezcla, modo de la
        // textura), lo grande, la imagen intermedia, la pasada doble y el final de la escena mandan antes lo pendiente.
        // Los triangulos llegan en el mismo orden y con el mismo estado: la tarjeta pinta exactamente lo mismo.
        internal const int JuntaMaxTris = 64;
        private const int JuntaCap = 6144;   // vertices pendientes como mucho (2048 triangulos)
        private bool juntar;   // lo enciende el constructor; los dispositivos imitados de las pruebas, solo si lo piden
        private D3D9.Vtx[] junta;
        private GCHandle juntaPin;
        private IntPtr juntaDir;
        private int juntaN;
        internal bool Juntar { get { return juntar; } set { if (!value) Junta(); juntar = value; } }
        private bool Encola(D3D9.Vtx[] verts, int tris)
        {
            if (!juntar) return false;
            int n = tris * 3;
            if (n > JuntaCap) return false;
            if (junta == null)
            {
                junta = new D3D9.Vtx[JuntaCap];
                juntaPin = GCHandle.Alloc(junta, GCHandleType.Pinned);
                juntaDir = juntaPin.AddrOfPinnedObject();
            }
            if (juntaN + n > JuntaCap) { Junta(); if (lost) return true; }
            Array.Copy(verts, 0, junta, juntaN, n);
            juntaN += n;
            return true;
        }
        // Manda lo pendiente. No lanza nunca.
        private void Junta()
        {
            int n = juntaN;
            if (n == 0) return;
            juntaN = 0;
            if (dev == IntPtr.Zero || lost) return;
            try
            {
                if (drawUp(dev, D3D9.PT_TRIANGLELIST, (uint)(n / 3), juntaDir, D3D9.VtxSize) < 0) EstadoFallido();
                else Envios++;
            }
            catch { EstadoFallido(); }
        }
        private void SueltaJunta()
        {
            juntaN = 0;
            if (juntaPin.IsAllocated) juntaPin.Free();
            junta = null; juntaDir = IntPtr.Zero;
        }

        // ---------------- LA IMAGEN INTERMEDIA A MEDIA RESOLUCION (2) ----------------
        // Una textura de la tarjeta (POOL_DEFAULT: se suelta antes de cada reinicio y se rehace sola) donde se pinta la
        // mitad de ancho y de alto, mas una fila y una columna de sobra para que el estirado bilineal del borde lea lo
        // que habria justo fuera de la pantalla, y no basura. Mientras dura, cada vertice pasa de pixeles de pantalla a
        // pixeles de la imagen: el centro del pixel p de la pantalla cae en (p + 0.5) / 2 - 0.5.
        private IntPtr mediaTex, mediaSup, mediaFondo;
        private int mediaTW, mediaTH, mediaW, mediaH;
        // G1 (14-10-2026): cuantas veces se creo la imagen intermedia. Lo que se pinto en ella vale mientras no cambie: al
        // reiniciar el dispositivo se suelta y la nueva empieza vacia (otro numero), asi que hay que volver a pintarla.
        private int mediaGen;
        internal int MediaGen { get { return mediaGen; } }
        private bool mediaSinSoporte, enMedia;
        // J2 (14-10-2026): la imagen va a la mitad (2) o a un cuarto (4) de la pantalla en cada lado.
        private int mediaDiv = 2;
        internal int MediaDiv { get { return mediaDiv; } }
        private D3D9.Vtx[] mediaVerts;
        internal bool EnMedia { get { return enMedia; } }
        internal int MediaTW { get { return mediaTW; } }
        internal int MediaTH { get { return mediaTH; } }
        internal bool MediaSinSoporte { get { return mediaSinSoporte; } }
        internal static int MediaLado(int lado) { return MediaLado(lado, 2); }
        internal static int MediaLado(int lado, int div) { return (lado + div - 1) / div + 2; }

        internal bool EmpiezaMedia(int w, int h) { return EmpiezaMedia(w, h, 2); }
        internal bool EmpiezaMedia(int w, int h, int div)
        {
            if (div != 4) div = 2;
            if (dev == IntPtr.Zero || lost || !inScene || enMedia || mediaSinSoporte || w < 8 * div || h < 8 * div) return false;
            int mw = MediaLado(w, div), mh = MediaLado(h, div);
            if (mediaTex == IntPtr.Zero || mediaTW < mw || mediaTH < mh)
            {
                LiberaMedia();
                if (!CreaMedia(Pow2(mw), Pow2(mh))) { mediaSinSoporte = true; return false; }
            }
            Junta();
            PonTexturaFisica(IntPtr.Zero);   // la imagen no puede estar puesta como textura mientras se pinta en ella
            if (lost) return false;
            IntPtr fondo = IntPtr.Zero;
            try
            {
                if (D3D9.Fn<D3D9.GetPtrDel>(dev, D3D9.SlotGetRenderTarget)(dev, 0, out fondo) < 0 || fondo == IntPtr.Zero)
                { if (fondo != IntPtr.Zero) D3D9.Release(fondo); mediaSinSoporte = true; return false; }
                if (D3D9.Fn<D3D9.SetTexDel>(dev, D3D9.SlotSetRenderTarget)(dev, 0, mediaSup) < 0)
                { D3D9.Release(fondo); mediaSinSoporte = true; return false; }
            }
            catch { if (fondo != IntPtr.Zero) D3D9.Release(fondo); mediaSinSoporte = true; return false; }
            mediaFondo = fondo; mediaW = mw; mediaH = mh; mediaDiv = div; enMedia = true; Cambios++;
            return true;
        }
        private bool CreaMedia(int tw, int th)
        {
            if (tw > LimiteTexturaW || th > LimiteTexturaH) return false;
            IntPtr t = IntPtr.Zero;
            try
            {
                foreach (uint formato in new uint[] { D3D9.FMT_X8R8G8B8, D3D9.FMT_A8R8G8B8 })
                {
                    t = IntPtr.Zero;
                    if (createTex(dev, (uint)tw, (uint)th, 1, D3D9.USAGE_RENDERTARGET, formato, D3D9.POOL_DEFAULT, out t, IntPtr.Zero) >= 0 && t != IntPtr.Zero) break;
                    if (t != IntPtr.Zero) D3D9.Release(t);
                    t = IntPtr.Zero;
                }
                if (t == IntPtr.Zero) return false;
                IntPtr sup;
                if (D3D9.Fn<D3D9.GetPtrDel>(t, D3D9.SlotGetSurfaceLevel)(t, 0, out sup) < 0 || sup == IntPtr.Zero)
                { D3D9.Release(t); return false; }
                mediaTex = t; mediaSup = sup; mediaTW = tw; mediaTH = th; mediaGen++;
                return true;
            }
            catch { if (t != IntPtr.Zero) D3D9.Release(t); return false; }
        }
        // Vuelve a la pantalla. Si no se puede, el dispositivo se reinicia en el proximo cuadro (eso devuelve la pantalla
        // como destino) y la imagen intermedia no se vuelve a intentar.
        internal bool TerminaMedia()
        {
            if (!enMedia) return false;
            Junta();
            enMedia = false;
            IntPtr fondo = mediaFondo; mediaFondo = IntPtr.Zero;
            bool ok;
            try { ok = D3D9.Fn<D3D9.SetTexDel>(dev, D3D9.SlotSetRenderTarget)(dev, 0, fondo) >= 0; }
            catch { ok = false; }
            D3D9.Release(fondo);
            Cambios++;
            if (!ok) { EstadoFallido(); resetPending = true; mediaSinSoporte = true; }
            return ok;
        }
        // La imagen estirada a la pantalla, opaca y sin mezcla: hace de cielo (el primero que tapa cada pixel).
        internal void DibujaMedia(D3D9.Vtx[] quad, int tris)
        {
            if (dev == IntPtr.Zero || lost || mediaTex == IntPtr.Zero || enMedia) return;
            Tex(mediaTex); TssModulate(); Blend(1);
            Draw(quad, tris);
        }
        private D3D9.Vtx[] EscalaMedia(D3D9.Vtx[] v, int tris)
        {
            if (v == null || tris <= 0 || tris > v.Length / 3) return null;
            int n = tris * 3;
            if (mediaVerts == null || mediaVerts.Length < n) mediaVerts = new D3D9.Vtx[Math.Max(n, 64)];
            float e = 1f / mediaDiv;
            for (int i = 0; i < n; i++)
            {
                D3D9.Vtx x = v[i];
                x.X = (x.X + 0.5f) * e - 0.5f; x.Y = (x.Y + 0.5f) * e - 0.5f;
                mediaVerts[i] = x;
            }
            return mediaVerts;
        }
        private void LiberaMedia()
        {
            mediaGen++;   // lo que tenia pintado ya no vale (G1)
            if (enMedia) { enMedia = false; if (mediaFondo != IntPtr.Zero) D3D9.Release(mediaFondo); mediaFondo = IntPtr.Zero; }
            IntPtr s = mediaSup, t = mediaTex;
            mediaSup = mediaTex = IntPtr.Zero; mediaTW = mediaTH = 0;
            if (t != IntPtr.Zero && stTex == t) stTex = new IntPtr(-1);
            if (s != IntPtr.Zero) D3D9.Release(s);
            if (t != IntPtr.Zero) D3D9.Release(t);
        }
        // ---------------- G2: LA VINETA, PUNTO POR PUNTO (14-10-2026) ----------------
        // Con «Fondo en una sola capa» la vineta va dentro de la imagen intermedia, DEBAJO de la aurora, las estrellas, la
        // luna y las fugaces. Para que en los bordes sigan oscureciendose como antes, mientras VinetaPuntos esta encendido
        // cada vertice se oscurece lo que la vineta oscureceria en su sitio: su color por (1 - alfa de la vineta). Con la
        // mezcla normal o la sumada, y el color del vertice multiplicando (las dos maneras que usa el fondo), eso es lo mismo
        // que la vineta encima, en el centro de cada punto; entre vertice y vertice la tarjeta lo degrada. La cuenta es la de
        // BakeVineta (GpuPanel.VinetaAlfa), sin la textura: en el centro de la pantalla no hace nada y se copia tal cual.
        internal bool VinetaPuntos;
        private D3D9.Vtx[] vinetaVerts;
        private D3D9.Vtx[] Vinetea(D3D9.Vtx[] v, int tris)
        {
            if (v == null || tris <= 0 || tris > v.Length / 3) return null;
            int n = tris * 3;
            if (vinetaVerts == null || vinetaVerts.Length < n) vinetaVerts = new D3D9.Vtx[Math.Max(n, 64)];
            float w = Math.Max(1f, (float)pp.BackBufferWidth), h = Math.Max(1f, (float)pp.BackBufferHeight);
            Array.Copy(v, 0, vinetaVerts, 0, n);   // de golpe; luego solo se toca el color de los que caen en la vineta
            D3D9.Vtx[] o = vinetaVerts;
            // Por tabla de enteros sobre la pantalla (GpuPanel.VinetaRejilla): se hace con cada vertice y cada cuadro, y
            // con decimales costaba seis veces lo que estirar los vertices a la imagen intermedia.
            int[] tab = GpuPanel.VinetaRejilla;
            int lado = GpuPanel.VinetaRejillaLado;
            float ex = lado / w, ey = lado / h;
            for (int i = 0; i < n; i++)
            {
                int ix = (int)((o[i].X + 0.5f) * ex), iy = (int)((o[i].Y + 0.5f) * ey);
                if (ix < 0) ix = 0; else if (ix > lado) ix = lado;
                if (iy < 0) iy = 0; else if (iy > lado) iy = lado;
                int k = tab[iy * (lado + 1) + ix];
                if (k >= 256) continue;   // el centro: la vineta no hace nada
                uint c = o[i].Color, kk = (uint)k;
                o[i].Color = (c & 0xFF000000u) | ((((c >> 16) & 255u) * kk + 128u) >> 8 << 16)
                           | ((((c >> 8) & 255u) * kk + 128u) >> 8 << 8) | (((c & 255u) * kk + 128u) >> 8);
            }
            return o;
        }

        public void DrawSkyN(D3D9.Vtx[] sky, int tris)
        {
            if (dev == IntPtr.Zero || lost) return;
            Tex(IntPtr.Zero); TssDiffuse(); Blend(1);
            Draw(sky, tris);
        }

        // ---------------- DOS TEXTURAS EN UNA PASADA (3) ----------------
        // Grano (etapa 0, repetido) y vineta (etapa 1, estirada). Las dos capas son mezcla normal; la vineta es negra:
        //   antes:  d' = d (1 - a1) + c1 a1      y despues      d'' = d' (1 - a2)
        //   juntas: d'' = d (1 - A) + C          con A = a1 + a2 - a1 a2 y C = c1 a1 (1 - a2)
        // Etapa 0: color = c1 * a1 (la textura por su propio alfa), alfa = a1.
        // Etapa 1: color = lo anterior * (1 - a2), alfa = suma suave (a1 + a2 - a1 a2).
        // Mezcla: origen UNO, destino 1 - alfa. Lo unico que cambia es el redondeo a 8 bits entre pasos.
        private bool dosTexturasFallo, dosTexturasValidado;
        internal bool PuedeDosTexturas { get { return CapsDosTexturas && !dosTexturasFallo && dev != IntPtr.Zero; } }
        internal bool DibujaDosTexturas(IntPtr t0, IntPtr t1, D3D9.Vtx2[] v, int tris)
        {
            if (!PuedeDosTexturas || lost || !inScene || enMedia || t0 == IntPtr.Zero || t1 == IntPtr.Zero
                || v == null || tris <= 0 || tris > v.Length / 3) return false;
            if (TexturasDivididasGpu.Busca(t0) != null || TexturasDivididasGpu.Busca(t1) != null) return false;
            Junta();
            Direccion(true); PonTexturaFisica(t0); texturaLogica = t0;
            if (lost) return false;
            bool dibujado = false, fvfCambiado = false, segunda = false;
            GCHandle gh = new GCHandle();
            stTss = 0; stBlend = 0;   // desde aqui, lo de la etapa 0 y la mezcla queda "sin saber": lo siguiente lo repone
            try
            {
                Cambios += 3;
                segunda = true;
                bool ok =
                    setTSS(dev, 0, D3D9.TSS_COLOROP, D3D9.TOP_MODULATE) >= 0 &&
                    setTSS(dev, 0, D3D9.TSS_COLORARG1, D3D9.TA_TEXTURE) >= 0 &&
                    setTSS(dev, 0, D3D9.TSS_COLORARG2, D3D9.TA_TEXTURE | D3D9.TA_ALPHAREPLICATE) >= 0 &&
                    setTSS(dev, 0, D3D9.TSS_ALPHAOP, D3D9.TOP_SELECTARG1) >= 0 &&
                    setTSS(dev, 0, D3D9.TSS_ALPHAARG1, D3D9.TA_TEXTURE) >= 0 &&
                    setTex(dev, 1, t1) >= 0 &&
                    setSamp(dev, 1, D3D9.SAMP_ADDRESSU, D3D9.ADDRESS_CLAMP) >= 0 &&
                    setSamp(dev, 1, D3D9.SAMP_ADDRESSV, D3D9.ADDRESS_CLAMP) >= 0 &&
                    setSamp(dev, 1, D3D9.SAMP_MAGFILTER, D3D9.TEXF_LINEAR) >= 0 &&
                    setSamp(dev, 1, D3D9.SAMP_MINFILTER, D3D9.TEXF_LINEAR) >= 0 &&
                    setTSS(dev, 1, D3D9.TSS_TEXCOORDINDEX, 1) >= 0 &&
                    setTSS(dev, 1, D3D9.TSS_COLOROP, D3D9.TOP_MODULATE) >= 0 &&
                    setTSS(dev, 1, D3D9.TSS_COLORARG1, D3D9.TA_TEXTURE | D3D9.TA_ALPHAREPLICATE | D3D9.TA_COMPLEMENT) >= 0 &&
                    setTSS(dev, 1, D3D9.TSS_COLORARG2, D3D9.TA_CURRENT) >= 0 &&
                    setTSS(dev, 1, D3D9.TSS_ALPHAOP, D3D9.TOP_ADDSMOOTH) >= 0 &&
                    setTSS(dev, 1, D3D9.TSS_ALPHAARG1, D3D9.TA_TEXTURE) >= 0 &&
                    setTSS(dev, 1, D3D9.TSS_ALPHAARG2, D3D9.TA_CURRENT) >= 0 &&
                    setRS(dev, D3D9.RS_ALPHABLENDENABLE, 1) >= 0 &&
                    setRS(dev, D3D9.RS_SRCBLEND, D3D9.BLEND_ONE) >= 0 &&
                    setRS(dev, D3D9.RS_DESTBLEND, D3D9.BLEND_INVSRCALPHA) >= 0;
                // La primera vez se le pregunta al controlador si esta combinacion la hace en una pasada.
                if (ok && !dosTexturasValidado)
                {
                    uint pasadas;
                    ok = D3D9.Fn<D3D9.ValidaDel>(dev, D3D9.SlotValidateDevice)(dev, out pasadas) >= 0 && pasadas <= 1;
                    dosTexturasValidado = ok;
                }
                if (ok) { fvfCambiado = true; ok = setFVF(dev, D3D9.FVF2) >= 0; }
                if (ok)
                {
                    gh = GCHandle.Alloc(v, GCHandleType.Pinned);
                    ok = drawUp(dev, D3D9.PT_TRIANGLELIST, (uint)tris, gh.AddrOfPinnedObject(), D3D9.Vtx2Size) >= 0;
                    if (ok)
                    {
                        dibujado = true; Envios++; Pedidos++;
                        if (CuentaArea) AreaPx += AreaDe2(v, tris);
                    }
                }
                if (!ok) dosTexturasFallo = true;   // no se vuelve a intentar con este dispositivo: se dibuja como siempre
            }
            catch { dosTexturasFallo = true; }
            finally
            {
                if (gh.IsAllocated) gh.Free();
                try
                {
                    if (fvfCambiado && setFVF(dev, D3D9.FVF) < 0) EstadoFallido();
                    if (segunda)
                    {
                        if (setTSS(dev, 1, D3D9.TSS_COLOROP, D3D9.TOP_DISABLE) < 0 || setTSS(dev, 1, D3D9.TSS_ALPHAOP, D3D9.TOP_DISABLE) < 0
                            || setTex(dev, 1, IntPtr.Zero) < 0) EstadoFallido();
                    }
                }
                catch { EstadoFallido(); }
                stTss = 0; stBlend = 0;
            }
            return dibujado;
        }
        private double AreaDe2(D3D9.Vtx2[] v, int tris)
        {
            float w = pp.BackBufferWidth, h = pp.BackBufferHeight;
            double a = 0;
            for (int i = 0, o = 0; i < tris; i++, o += 3)
            {
                float x0 = Recorta(v[o].X + .5f, w), y0 = Recorta(v[o].Y + .5f, h);
                float x1 = Recorta(v[o + 1].X + .5f, w), y1 = Recorta(v[o + 1].Y + .5f, h);
                float x2 = Recorta(v[o + 2].X + .5f, w), y2 = Recorta(v[o + 2].Y + .5f, h);
                a += Math.Abs((double)(x1 - x0) * (y2 - y0) - (double)(x2 - x0) * (y1 - y0)) * 0.5;
            }
            return a;
        }

        // ---------------- PRESENTAR SIN ESPERAR AL MONITOR (para medir) ----------------
        // Con la espera, un cuadro de 17 ms se ensena a los 33: el numero salta de 60 a 30 y no dice cuanto falta. Sin
        // ella, el ritmo es el que dan el procesador y la tarjeta. Cambiarlo pide reiniciar el dispositivo (lo hace el
        // proximo BeginFrame); las texturas de siempre sobreviven (POOL_MANAGED). Solo lo usa el medidor, y lo devuelve.
        internal bool SinEspera { get { return pp.PresentationInterval == D3D9.PRESENT_INTERVAL_IMMEDIATE; } }
        internal bool PideSinEspera(bool sinEspera)
        {
            if (dev == IntPtr.Zero) return false;
            if (sinEspera && !CapsSinEspera) return false;
            uint quiere = sinEspera ? D3D9.PRESENT_INTERVAL_IMMEDIATE : 0u;
            if (pp.PresentationInterval == quiere) return true;
            pp.PresentationInterval = quiere;
            resetPending = true;
            return true;
        }
    }

    internal sealed partial class GpuStarfield
    {
        // El cielo de abajo de todo. Si devuelve true, ya se pinto (con las nebulosas, en la imagen intermedia).
        internal delegate bool DibujaBase(int w, int h, float midY, uint top, uint mid, uint bot);
        public DibujaBase CieloBase;
        private D3D9.Vtx[] skyAmplio;
        // El mismo degradado de tres paradas, mas ancho y mas alto que la pantalla: en la imagen intermedia, la columna y
        // la fila de sobra tienen que tener cielo (el color no cambia de lado a lado, y abajo sigue el de abajo).
        internal void CieloAmplio(int w, int h, float midY, uint top, uint mid, uint bot)
        {
            if (gpu == null) return;
            if (CieloSuave) { DibujaCieloSuave(ArmaCieloSuave(w + 8f, h, midY, top, mid, bot, 8f)); return; }   // C1 (PulidoGpu.cs)
            if (skyAmplio == null) skyAmplio = new D3D9.Vtx[18];
            float ancho = w + 8f;
            SetQuad(skyAmplio, 0, 0, 0, ancho, midY, top, top, mid, mid);
            SetQuad(skyAmplio, 6, 0, midY, ancho, h, mid, mid, bot, bot);
            SetQuad(skyAmplio, 12, 0, h, ancho, h + 8f, bot, bot, bot, bot);
            gpu.DrawSkyN(skyAmplio, 6);
        }
        internal bool EmpiezaMedia(int w, int h) { return Ok && gpu.EmpiezaMedia(w, h); }
        internal bool EmpiezaMedia(int w, int h, int div) { return Ok && gpu.EmpiezaMedia(w, h, div); }
        internal int MediaDiv { get { return gpu != null ? gpu.MediaDiv : 2; } }
        internal int MediaGen { get { return gpu != null ? gpu.MediaGen : 0; } }
        internal bool VinetaPuntos { get { return gpu != null && gpu.VinetaPuntos; } set { if (gpu != null) gpu.VinetaPuntos = value; } }
        internal bool TerminaMedia() { return Ok && gpu.TerminaMedia(); }
        internal void DibujaMedia(D3D9.Vtx[] quad, int tris) { if (Ok) gpu.DibujaMedia(quad, tris); }
        internal int MediaTW { get { return gpu != null ? gpu.MediaTW : 0; } }
        internal int MediaTH { get { return gpu != null ? gpu.MediaTH : 0; } }
        internal bool PuedeMedia { get { return Ok && !gpu.MediaSinSoporte; } }
        internal bool PuedeDosTexturas { get { return Ok && gpu.PuedeDosTexturas; } }
        internal bool DibujaDosTexturas(IntPtr t0, IntPtr t1, D3D9.Vtx2[] v, int tris) { return Ok && gpu.DibujaDosTexturas(t0, t1, v, tris); }
        internal void EmpiezaCuenta(bool area) { if (gpu != null) gpu.EmpiezaCuenta(area); }
        internal int Pedidos { get { return gpu != null ? gpu.Pedidos : 0; } }
        internal int Envios { get { return gpu != null ? gpu.Envios : 0; } }
        internal int Cambios { get { return gpu != null ? gpu.Cambios : 0; } }
        internal double AreaPx { get { return gpu != null ? gpu.AreaPx : 0; } }
        internal bool PuedeSinEspera { get { return Ok && gpu.CapsSinEspera; } }
        internal bool SinEspera { get { return Ok && gpu.SinEspera; } }
        internal bool PideSinEspera(bool sinEspera) { return Ok && gpu.PideSinEspera(sinEspera); }
    }

    internal sealed partial class GpuPanel
    {
        // Ctrl+F3 -> Imagen -> Performance. Los tres son de aspecto (casi identico), asi que llevan interruptor y vienen
        // apagados de fabrica; el preset "Performance" los enciende.
        public bool CieloMedia, GranoVineta, FondoUnaCapa, CieloCuarto;
        // El medidor pide contar tambien los pixeles pintados (cuesta un poco de procesador; solo mientras mide).
        public bool CuentaArea;
        private bool cieloMedioEsteCuadro, granoUnidoEsteCuadro, capaUnicaEsteCuadro;
        private readonly D3D9.Vtx[] mediaQuad = new D3D9.Vtx[24];
        private D3D9.Vtx2[] granoVineta2;

        // Antes de abrir la escena: que va a ir en una pasada este cuadro.
        private void DecideAhorros()
        {
            cieloMedioEsteCuadro = false; capaUnicaEsteCuadro = false;
            if (stars != null) stars.VinetaPuntos = false;
            granoUnidoEsteCuadro = GranoVineta && VinetaSuave && vinetaTex != IntPtr.Zero && ruidoTex != IntPtr.Zero
                && stars != null && stars.PuedeDosTexturas;
        }

        // ---------------- G1: NO REPETIR EL CIELO CUANDO NO CAMBIO (14-10-2026; automatico) ----------------
        // Las nebulosas y la via lactea se mueven con el reloj de la animacion (24 veces por segundo, Form1.AnimTimer_Tick),
        // pero la imagen intermedia se pintaba en cada cuadro: 60 veces por segundo, y 36 de ellas salia lo mismo. Ahora se
        // apunta CON QUE se pinto (la firma) y solo se vuelve a pintar si algo de eso cambio, o si la imagen se rehizo (un
        // reinicio del dispositivo la deja vacia: MediaGen). Si no, se estira la que ya estaba: es la misma imagen, pixel por
        // pixel, asi que va siempre activo y sin interruptor (AGENTS.md). Solo con la imagen intermedia: a resolucion
        // completa, el cuadro que si cambia costaria una pantalla mas que hoy, y eso empeora justo los peores cuadros.
        private sealed class FirmaCielo
        {
            internal int W, H, Nieblas, Ver, Gen, Div;
            internal float MidY, NebFase, HazFase, NebEmpuje, HazEmpuje;
            internal uint Top, Mid, Bot, Neb1, Neb2;
            internal bool Cine, Capa, Suave, Prof;
            internal IntPtr NebTex, HazTex, RuidoTex, VinetaTex;
            internal bool Igual(FirmaCielo o)
            {
                return W == o.W && H == o.H && Div == o.Div && Nieblas == o.Nieblas && Ver == o.Ver && MidY == o.MidY && NebFase == o.NebFase
                    && HazFase == o.HazFase && NebEmpuje == o.NebEmpuje && HazEmpuje == o.HazEmpuje && Top == o.Top && Mid == o.Mid
                    && Bot == o.Bot && Neb1 == o.Neb1 && Neb2 == o.Neb2 && Cine == o.Cine && Capa == o.Capa && Suave == o.Suave && Prof == o.Prof && NebTex == o.NebTex
                    && HazTex == o.HazTex && RuidoTex == o.RuidoTex && VinetaTex == o.VinetaTex;
            }
            internal void Copia(FirmaCielo o)
            {
                W = o.W; H = o.H; Div = o.Div; Nieblas = o.Nieblas; Ver = o.Ver; Gen = o.Gen; MidY = o.MidY; NebFase = o.NebFase; HazFase = o.HazFase;
                NebEmpuje = o.NebEmpuje; HazEmpuje = o.HazEmpuje; Top = o.Top; Mid = o.Mid; Bot = o.Bot; Neb1 = o.Neb1; Neb2 = o.Neb2;
                Cine = o.Cine; Capa = o.Capa; Suave = o.Suave; Prof = o.Prof; NebTex = o.NebTex; HazTex = o.HazTex; RuidoTex = o.RuidoTex; VinetaTex = o.VinetaTex;
            }
        }
        private FirmaCielo firmaHecha, firmaAhora;   // al primer uso (un panel armado a mano en las pruebas tambien los tiene)
        private bool firmaValida;
        private int nieblasVer;                      // sube cada vez que se sube una imagen del cielo (EnsureNieblas)
        internal int CieloRepintes, CieloReusos;     // cuantas veces se pinto y cuantas se reuso (pruebas y medidor)
        internal void OlvidaCielo() { firmaValida = false; }

        // EL CIELO A MEDIA RESOLUCION (lo llama GpuStarfield.Begin en lugar del degradado). El degradado, las nebulosas y la
        // via lactea van a la imagen intermedia, enteras (sin los huecos de las portadas: el estirado leeria su borde), y
        // la imagen se estira una vez, opaca, a toda la pantalla. Si algo falla, el cielo de siempre.
        // Con «Fondo en una sola capa» (G2) van tambien el grano y la vineta. Y solo se pinta si cambio algo (G1).
        private bool CieloMedioBase(int w, int h, float midY, uint top, uint mid, uint bot)
        {
            cieloMedioEsteCuadro = false; capaUnicaEsteCuadro = false;
            if (stars != null) stars.VinetaPuntos = false;
            if (!CieloMedia || Nieblas == GpuStarfield.Bajo || (nebTex == IntPtr.Zero && hazTex == IntPtr.Zero)) return false;
            if (stars == null) return false;
            bool capa = FondoUnaCapa;
            int div = CieloCuarto ? 4 : 2;   // J2: a un cuarto en cada lado
            IntPtr vineta = capa && VinetaSuave ? vinetaTex : IntPtr.Zero, ruido = capa ? ruidoTex : IntPtr.Zero;
            if (firmaHecha == null) { firmaHecha = new FirmaCielo(); firmaAhora = new FirmaCielo(); }
            FirmaCielo f = firmaAhora;
            f.W = w; f.H = h; f.Div = div; f.Nieblas = Nieblas; f.Ver = nieblasVer; f.MidY = midY; f.NebFase = NebFase; f.HazFase = HazFase;
            f.NebEmpuje = nebEmpuje; f.HazEmpuje = hazEmpuje; f.Top = top; f.Mid = mid; f.Bot = bot; f.Neb1 = NebColor; f.Neb2 = NebColor2;
            f.Cine = NebulosasCine; f.Capa = capa; f.Suave = stars.CieloSuave; f.Prof = stars.ColoresProfundos; f.NebTex = nebTex; f.HazTex = hazTex; f.RuidoTex = ruido; f.VinetaTex = vineta;
            bool repinta = !firmaValida || !f.Igual(firmaHecha) || firmaHecha.Gen != stars.MediaGen || stars.MediaTW <= 0;
            if (repinta)
            {
                firmaValida = false;
                if (!stars.EmpiezaMedia(w, h, div)) return false;
                int huecosGuardados = nHuecos;
                bool ok = false;
                try
                {
                    nHuecos = 0;
                    stars.CieloAmplio(w, h, midY, top, mid, bot);
                    DibujaNieblas();
                    if (capa)
                    {
                        // G2: el grano (un granito por pixel de la imagen: 2x2 en la pantalla) y la vineta, aqui dentro.
                        if (ruido != IntPtr.Zero) { D3D9.Vtx[] malla; int n = MallaRuidoMedia(div, out malla); if (n > 0) stars.DrawTexturedBatchWrap(ruido, malla, n / 3); }
                        if (vineta != IntPtr.Zero)
                            DibujaTeselasFondo(vineta, vinetaTeselas, 0, 0, Width, Height, vinetaU, vinetaV, 0xFFFFFFFFu, adornoQuad);
                    }
                    ok = true;
                }
                finally
                {
                    nHuecos = huecosGuardados;
                    if (!stars.TerminaMedia()) ok = false;
                }
                if (!ok) return false;
                f.Gen = stars.MediaGen;
                firmaHecha.Copia(f);
                firmaValida = true;
                CieloRepintes++;
            }
            else CieloReusos++;
            int tw = stars.MediaTW, th = stars.MediaTH;
            if (tw <= 0 || th <= 0) { firmaValida = false; return false; }
            if (nHuecos > 0)
            {
                // J1 (14-10-2026; automatico): el estirado no se pinta debajo de las portadas opacas. Los huecos son su
                // interior (DibujoLigeroGpu.cs, HuecosDePortadas), que la portada tapa con alfa 255: se ve igual.
                PiezasEstirado(piezasEstirado, w, h, tw, th, div);
                int v = 0;
                for (int k = 0; k < 4; k++)
                {
                    int o = k * 8;
                    float[] q = piezasEstirado;
                    v = Trozos(v, q[o], q[o + 1], q[o + 2], q[o + 3], q[o + 4], q[o + 5], q[o + 6], q[o + 7], 0xFFFFFFFFu);
                }
                if (v > 0) stars.DibujaMedia(trozosHuecos, v / 3);
            }
            else
            {
                PiezasEstirado(piezasEstirado, w, h, tw, th, div);   // (sin reservar nada por cuadro)
                float[] q = piezasEstirado;
                for (int k = 0; k < 4; k++)
                {
                    int o = k * 8;
                    Trozo(mediaQuad, k * 6, q[o], q[o + 1], q[o + 2], q[o + 3], q[o + 4], q[o + 5], q[o + 6], q[o + 7]);
                }
                stars.DibujaMedia(mediaQuad, 8);
            }
            cieloMedioEsteCuadro = true;
            if (capa)
            {
                // Lo que se pinte encima hasta el final del fondo (aurora, estrellas, luna, fugaces) lleva la vineta en cada
                // punto; GpuPanel.RenderFrame la apaga al terminar el fondo.
                capaUnicaEsteCuadro = true;
                stars.VinetaPuntos = vineta != IntPtr.Zero;
            }
            return true;
        }

        // G2: la malla del grano para la imagen intermedia. Tejas de 2048 pixeles de pantalla, que alli son 1024: un texel
        // del grano por pixel de la imagen, alineado con su centro. Se rehace solo si cambia la pantalla o el grano.
        private D3D9.Vtx[] ruidoMediaMalla;
        private int ruidoMediaAncho, ruidoMediaAlto, ruidoMediaN, ruidoMediaDiv;
        private float ruidoMediaU, ruidoMediaV;
        private int MallaRuidoMedia(int div, out D3D9.Vtx[] malla)
        {
            malla = null;
            int ancho = Width, alto = Height, teja = div * RuidoTeja;   // con el cielo a un cuarto (J2), tejas de 4096
            if (ruidoTex == IntPtr.Zero || ancho <= 0 || alto <= 0) return 0;
            if (ruidoMediaMalla == null || ruidoMediaAncho != ancho || ruidoMediaAlto != alto || ruidoMediaU != ruU || ruidoMediaV != ruV
                || ruidoMediaDiv != div)
            {
                int n = checked(((ancho - 1) / teja + 1) * ((alto - 1) / teja + 1) * 6);
                if (ruidoMediaMalla == null || ruidoMediaMalla.Length != n) ruidoMediaMalla = new D3D9.Vtx[n];
                int pos = 0;
                for (int y = 0; y < alto; y += teja)
                    for (int x = 0; x < ancho; x += teja)
                    {
                        float aw = Math.Min(teja, ancho - x), ah = Math.Min(teja, alto - y);
                        float u = ruU * (aw / teja), v = ruV * (ah / teja);
                        SetV(ref ruidoMediaMalla[pos], x, y, 0, 0); SetV(ref ruidoMediaMalla[pos + 1], x + aw, y, u, 0);
                        SetV(ref ruidoMediaMalla[pos + 2], x + aw, y + ah, u, v); SetV(ref ruidoMediaMalla[pos + 3], x, y, 0, 0);
                        SetV(ref ruidoMediaMalla[pos + 4], x + aw, y + ah, u, v); SetV(ref ruidoMediaMalla[pos + 5], x, y + ah, 0, v);
                        pos += 6;
                    }
                ruidoMediaAncho = ancho; ruidoMediaAlto = alto; ruidoMediaU = ruU; ruidoMediaV = ruV; ruidoMediaN = n; ruidoMediaDiv = div;
            }
            malla = ruidoMediaMalla;
            return ruidoMediaN;
        }

        // EL ESTIRADO. El pixel p de la pantalla cae en (p + 0.5) / 2 de la imagen intermedia (en texeles): la textura va de 0
        // a la mitad del ancho de la pantalla. Pero el filtro lee tambien el texel de al lado, y en la ultima columna (las dos
        // ultimas si el ancho es impar) ese texel ya es de FUERA de la pantalla: donde una nebulosa, en su punto mas corrido,
        // acaba justo en el borde, alli no hay nebulosa y el borde salia hasta 19 niveles mas oscuro. Asi que la pantalla se
        // estira en cuatro trozos: lo de dentro, como se dijo; la tira de la derecha lee solo la ultima columna buena de la
        // imagen (la que cubre los ultimos pixeles de dentro), la de abajo la ultima fila buena, y la esquina las dos.
        // Mismo dibujo y misma textura: una sola orden de 8 triangulos. Con el cielo a un cuarto (J2), lo mismo con 4: el
        // pixel p cae en (p + 0.5) / 4, y la ultima columna buena es la que cubre cuatro pixeles enteros de dentro.
        internal static void ArmaEstirado(D3D9.Vtx[] q, int w, int h, int tw, int th) { ArmaEstirado(q, w, h, tw, th, 2); }
        internal static void ArmaEstirado(D3D9.Vtx[] q, int w, int h, int tw, int th, int div)
        {
            float[] p = new float[32];
            PiezasEstirado(p, w, h, tw, th, div);
            for (int k = 0; k < 4; k++)
            {
                int o = k * 8;
                Trozo(q, k * 6, p[o], p[o + 1], p[o + 2], p[o + 3], p[o + 4], p[o + 5], p[o + 6], p[o + 7]);
            }
        }
        // Los cuatro trozos del estirado: x0, y0, x1, y1 (pixeles de pantalla) y u0, v0, u1, v1, de 8 en 8.
        private readonly float[] piezasEstirado = new float[32];
        internal static void PiezasEstirado(float[] p, int w, int h, int tw, int th, int div)
        {
            if (div != 4) div = 2;
            int jx = w / div - 1, jy = h / div - 1;   // la ultima columna y fila de la imagen enteras dentro
            // Hasta aqui (sin incluir), el pixel lee solo texeles de dentro: p <= div * (j + 0.5) - 0.5.
            int finX = (int)Math.Floor(div * (jx + 0.5) - 0.5) + 1, finY = (int)Math.Floor(div * (jy + 0.5) - 0.5) + 1;
            float e = 1f / div;
            float uIn = e * finX / tw, vIn = e * finY / th;
            float uB = (jx + 0.5f) / tw, vB = (jy + 0.5f) / th;
            Pieza(p, 0, 0, 0, finX, finY, 0, 0, uIn, vIn);
            Pieza(p, 8, finX, 0, w, finY, uB, 0, uB, vIn);
            Pieza(p, 16, 0, finY, finX, h, 0, vB, uIn, vB);
            Pieza(p, 24, finX, finY, w, h, uB, vB, uB, vB);
        }
        private static void Pieza(float[] p, int o, float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1)
        { p[o] = x0; p[o + 1] = y0; p[o + 2] = x1; p[o + 3] = y1; p[o + 4] = u0; p[o + 5] = v0; p[o + 6] = u1; p[o + 7] = v1; }
        private static void Trozo(D3D9.Vtx[] q, int o, float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1)
        {
            SetV(ref q[o], x0, y0, u0, v0); SetV(ref q[o + 1], x1, y0, u1, v0); SetV(ref q[o + 2], x1, y1, u1, v1);
            SetV(ref q[o + 3], x0, y0, u0, v0); SetV(ref q[o + 4], x1, y1, u1, v1); SetV(ref q[o + 5], x0, y1, u0, v1);
        }

        // GRANO Y VINETA EN UNA PASADA: la malla del grano (con los huecos de las portadas) con un segundo juego de
        // coordenadas para la vineta, que se estira a toda la pantalla igual que cuando va sola.
        private bool DibujaGranoYVineta()
        {
            D3D9.Vtx[] malla; int n = MallaRuido(out malla);
            if (n <= 0 || malla == null) return n == 0;
            if (granoVineta2 == null || granoVineta2.Length < n) granoVineta2 = new D3D9.Vtx2[n];
            float su = vinetaU / Math.Max(1, Width), sv = vinetaV / Math.Max(1, Height);
            for (int i = 0; i < n; i++)
            {
                D3D9.Vtx a = malla[i];
                D3D9.Vtx2 b;
                b.X = a.X; b.Y = a.Y; b.Z = a.Z; b.Rhw = a.Rhw; b.Color = a.Color; b.U = a.U; b.V = a.V;
                b.U2 = (a.X + 0.5f) * su; b.V2 = (a.Y + 0.5f) * sv;
                granoVineta2[i] = b;
            }
            return stars.DibujaDosTexturas(ruidoTex, vinetaTex, granoVineta2, n / 3);
        }

        // ---------------- LO QUE CUESTA CADA CUADRO (1) ----------------
        private int cuentaN;
        private long cuentaPedidos, cuentaEnvios, cuentaCambios;
        private double cuentaArea;
        private void ReiniciaCuenta() { cuentaN = 0; cuentaPedidos = cuentaEnvios = cuentaCambios = 0; cuentaArea = 0; }
        private void TerminaCuenta(double preparar, double dibujar, double presentar)
        {
            if (stars == null) return;
            cuentaN++;
            cuentaPedidos += stars.Pedidos; cuentaEnvios += stars.Envios; cuentaCambios += stars.Cambios;
            cuentaArea += stars.AreaPx;
        }
        internal double PedidosPorCuadro { get { return cuentaN > 0 ? (double)cuentaPedidos / cuentaN : 0; } }
        internal double EnviosPorCuadro { get { return cuentaN > 0 ? (double)cuentaEnvios / cuentaN : 0; } }
        internal double CambiosPorCuadro { get { return cuentaN > 0 ? (double)cuentaCambios / cuentaN : 0; } }
        // Pantallas pintadas por cuadro (pixeles pintados / pixeles de la pantalla). Solo con CuentaArea.
        internal double PantallasPorCuadro
        { get { return cuentaN > 0 && Width > 0 && Height > 0 ? cuentaArea / cuentaN / ((double)Width * Height) : 0; } }
        // Lo tipico (mediana) de los cuadros medidos desde ReiniciaMedida, del diagnostico de fluidez.
        internal void Medianas(out double intervalo, out double preparar, out double dibujar, out double presentar, out double fuera, out double p95)
        { fluidez.Medianas(out intervalo, out preparar, out dibujar, out presentar, out fuera, out p95); }
        internal bool PuedeSinEspera { get { return stars != null && stars.PuedeSinEspera; } }
        internal bool PideSinEspera(bool sinEspera) { return stars != null && stars.PideSinEspera(sinEspera); }
        internal bool SinEspera { get { return stars != null && stars.SinEspera; } }
        internal bool PuedeCieloMedia { get { return stars != null && stars.PuedeMedia; } }
        // Las veces por segundo que refresca la pantalla (60 si Windows no lo sabe), sin el 10 % de sobra del tope.
        internal int RefrescoHz
        {
            get
            {
                int hz = 0;
                try
                {
                    IntPtr dc = GetDC(IntPtr.Zero);
                    if (dc != IntPtr.Zero)
                    {
                        try { hz = GetDeviceCaps(dc, VREFRESH); }
                        finally { ReleaseDC(IntPtr.Zero, dc); }
                    }
                }
                catch { }
                return (hz < 24 || hz > 400) ? 60 : hz;
            }
        }
        internal bool PuedeGranoVineta { get { return stars != null && stars.PuedeDosTexturas; } }
        internal bool PuedeFondoUnaCapa { get { return PuedeCieloMedia; } }   // va en la imagen intermedia
    }
}
#endif
