#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Rocola
{
    // LA BOVEDA DE ESPACIO NATURAL (ideas 1, 2, 3 y 34 de la presentacion del 11-10-2026).
    //
    // Legacy pinta el cielo con dos cuadrilateros y tres paradas de color: entre parada y parada la tarjeta
    // mezcla en linea recta, y donde se juntan las dos rectas queda un quiebre que el ojo ve como una raya.
    // Aqui la misma boveda se pinta con una malla de 10 franjas por 3 columnas (60 triangulos, una sola
    // llamada, sin textura): la curva que une las tres paradas es suave (sin quiebre), cerca del horizonte
    // aparece el resplandor verdoso del aire, la luz del lado de la luna aclara un poco ese costado y, si el
    // interruptor lo pide, el cielo se hace mas hondo pasada la medianoche y toma un tono de crepusculo a
    // la hora de abrir. Todo son colores de vertice: ni texturas, ni pasadas de pantalla, ni memoria por cuadro.
    // Legacy no pasa por aqui.
    internal sealed partial class GpuStarfield
    {
        // 10 franjas y no mas, a proposito: con 64 triangulos o mas la boveda seria el primer dibujo "grande" del
        // cuadro y la sonda de D3D9Device.Draw mediria el anillo de vertices con ella (pocos vertices, donde UP
        // gana siempre) en vez de con las estrellas. Con 10 la curva sigue sin quiebre: frente a 12 franjas, el
        // color se separa de la curva exacta lo mismo (alrededor de un nivel de 255 con los cortes de Ctrl+F3).
        internal const int CieloFilasNatural = 10, CieloColsNatural = 3;
        private static readonly float[] CieloColX = { 0f, .55f, .80f, 1f };
        private static readonly float[] CieloColLuz = { 0f, 0f, 1f, .55f };
        private readonly D3D9.Vtx[] skyNatV = new D3D9.Vtx[CieloFilasNatural * CieloColsNatural * 6];
        private readonly uint[] skyNatC = new uint[(CieloFilasNatural + 1) * (CieloColsNatural + 1)];
        // La hora la calcula el panel (una vez cada veinte segundos) y llega aqui ya resuelta.
        public float HoraHondura, HoraCrepusculo;

        private void DibujaCieloNatural(int w, int h)
        {
            float corte = Form1.CorteDelCielo;
            int pc = CieloColsNatural + 1;
            for (int j = 0; j <= CieloFilasNatural; j++)
            {
                float v = j / (float)CieloFilasNatural;
                for (int i = 0; i < pc; i++)
                    skyNatC[j * pc + i] = ColorCieloNatural(skyTop, skyMid, skyBot, corte, v, CieloColLuz[i], HoraHondura, HoraCrepusculo);
            }
            int n = 0;
            for (int j = 0; j < CieloFilasNatural; j++)
            {
                float y0 = h * j / (float)CieloFilasNatural, y1 = h * (j + 1) / (float)CieloFilasNatural;
                for (int i = 0; i < CieloColsNatural; i++)
                {
                    float x0 = w * CieloColX[i], x1 = w * CieloColX[i + 1];
                    SetQuad(skyNatV, n, x0, y0, x1, y1, skyNatC[j * pc + i], skyNatC[j * pc + i + 1],
                            skyNatC[(j + 1) * pc + i + 1], skyNatC[(j + 1) * pc + i]);
                    n += 6;
                }
            }
            gpu.DrawSkyMesh(skyNatV, n / 3);
        }

        // El color de la boveda a la altura v (0 arriba, 1 abajo) con 'lateral' de luz del costado (0..1).
        // Pura: se puede comprobar sin tarjeta.
        internal static uint ColorCieloNatural(uint top, uint mid, uint bot, float corte, float v, float lateral,
                                               float hondura, float crepusculo)
        {
            if (!(v > 0f)) v = 0f; else if (v > 1f) v = 1f;
            if (!(corte > .05f)) corte = .05f; else if (corte > .95f) corte = .95f;
            if (!(lateral > 0f)) lateral = 0f; else if (lateral > 1f) lateral = 1f;
            if (!(hondura > 0f)) hondura = 0f; else if (hondura > 1f) hondura = 1f;
            if (!(crepusculo > 0f)) crepusculo = 0f; else if (crepusculo > 1f) crepusculo = 1f;
            float r = Curva3(v, corte, (top >> 16) & 255, (mid >> 16) & 255, (bot >> 16) & 255);
            float g = Curva3(v, corte, (top >> 8) & 255, (mid >> 8) & 255, (bot >> 8) & 255);
            float b = Curva3(v, corte, top & 255, mid & 255, bot & 255);
            // 1) El resplandor del aire: una franja verdosa muy tenue por encima del horizonte.
            float d = (v - .80f) / .12f, glow = (float)Math.Exp(-d * d);
            r += 1.5f * glow; g += 6.5f * glow; b += 2.5f * glow;
            // 3) La luz del costado de la luna: crece hacia abajo, nunca arriba del todo.
            float lado = lateral * v * v;
            r += 4f * lado; g += 7f * lado; b += 12f * lado;
            // 34) La hora: mas hondo de madrugada (sobre todo arriba) y crepusculo calido abajo.
            float hondo = 1f - .24f * hondura * (1f - .45f * v);
            r *= hondo; g *= hondo; b *= hondo;
            float bajo = crepusculo * v * v * v;
            r += 20f * bajo; g += 8f * bajo; b -= 6f * bajo;
            return 0xFF000000u | (Canal(r) << 16) | (Canal(g) << 8) | Canal(b);
        }
        private static uint Canal(float v) { return !(v > 0f) ? 0u : v >= 255f ? 255u : (uint)(v + .5f); }

        // 2) La curva entre las tres paradas: Hermite monotona (Fritsch-Carlson). Pasa por las tres, no se
        // sale de su rango y la pendiente es continua en la parada del medio: no deja la raya del quiebre.
        internal static float Curva3(float v, float corte, float c0, float c1, float c2)
        {
            float d0 = (c1 - c0) / corte, d1 = (c2 - c1) / (1f - corte);
            float m1 = (d0 * d1 <= 0f) ? 0f : 2f / (1f / d0 + 1f / d1);
            if (v <= corte)
            {
                float t = v / corte;
                return Hermite(t, c0, c1, d0 * corte, m1 * corte);
            }
            else
            {
                float t = (v - corte) / (1f - corte);
                return Hermite(t, c1, c2, m1 * (1f - corte), d1 * (1f - corte));
            }
        }
        private static float Hermite(float t, float p0, float p1, float m0, float m1)
        {
            float t2 = t * t, t3 = t2 * t;
            float v = (2 * t3 - 3 * t2 + 1) * p0 + (t3 - 2 * t2 + t) * m0 + (-2 * t3 + 3 * t2) * p1 + (t3 - t2) * m1;
            float lo = Math.Min(p0, p1), hi = Math.Max(p0, p1);
            return v < lo ? lo : v > hi ? hi : v;
        }
    }

    internal sealed partial class GpuPanel
    {
        public bool CieloHora = true;   // Ctrl+F3 -> Fondo: "Cielo segun la hora (Natural)"
        private int horaCieloMs;
        private bool horaCieloLista;
        // Una vez cada veinte segundos: leer el reloj de Windows por cuadro no hace falta para algo que
        // cambia en horas.
        private void ActualizaHoraCielo()
        {
            if (!FondoNatural || !CieloHora) { stars.HoraHondura = 0f; stars.HoraCrepusculo = 0f; horaCieloLista = false; return; }
            int now = Environment.TickCount;
            if (horaCieloLista && unchecked(now - horaCieloMs) < 20000) return;
            horaCieloMs = now; horaCieloLista = true;
            DateTime t = DateTime.Now;
            float hondura, crepusculo;
            HoraDelCielo(t.Hour + t.Minute / 60f, out hondura, out crepusculo);
            stars.HoraHondura = hondura; stars.HoraCrepusculo = crepusculo;
        }
        // Pura: hora local (0..24) -> cuanto mas hondo y cuanto crepusculo.
        internal static void HoraDelCielo(float hora, out float hondura, out float crepusculo)
        {
            if (float.IsNaN(hora) || float.IsInfinity(hora)) hora = 22f;
            hora %= 24f; if (hora < 0f) hora += 24f;
            float s = hora >= 12f ? hora - 24f : hora;   // 23 h -> -1; 3 h -> 3
            hondura = s < 3f ? SuaveHora((s + 1f) / 4f) : 1f - SuaveHora((s - 3f) / 3f);
            if (s < -1f || s > 6f) hondura = 0f;
            float dt = (hora - 19.5f) / 1.4f, da = (hora - 6.3f) / .8f;
            crepusculo = Math.Max((float)Math.Exp(-dt * dt), .7f * (float)Math.Exp(-da * da));
        }
        // 4) EL GRANO PAREJO. La teja de siempre reparte sus puntos al azar: deja grumos y huecos que, mirados
        // de cerca, son manchas. Esta usa ruido de gradiente entrelazado (el de Jimenez): el mismo numero de
        // puntos claros y oscuros, el mismo alfa y la misma subida media, pero repartidos sin grumos.
        // Misma teja de 1024 que se sube una vez; Legacy y la pared por CPU siguen con la suya.
        // Como la de Legacy, se arma repitiendo una pieza con UN relleno y no pixel a pixel: la cuenta se hace en
        // 256x256 (65 mil pixeles en vez de un millon, en el hilo de la interfaz y en equipos de dos nucleos).
        // Con alfa 2 de 255, la costura entre pieza y pieza no se ve: sigue siendo mitad claros, sin grumos.
        internal const int RuidoNaturalPieza = 256;
        internal static Bitmap BakeRuidoNatural(int lado)
        {
            if (lado < 1 || lado > 4096) throw new ArgumentOutOfRangeException("lado");
            int t = Math.Min(lado, RuidoNaturalPieza);
            int[] pixels = new int[t * t];
            uint claro = (uint)Form1.RuidoAlfa << 24 | 0x00FFFFFFu, oscuro = (uint)Form1.RuidoAlfa << 24;
            for (int y = 0; y < t; y++)
                for (int x = 0; x < t; x++)
                {
                    double f = .06711056 * x + .00583715 * y; f -= Math.Floor(f);
                    double n = 52.9829189 * f; n -= Math.Floor(n);
                    pixels[y * t + x] = unchecked((int)(n >= .5 ? claro : oscuro));
                }
            Bitmap pieza = new Bitmap(t, t, PixelFormat.Format32bppArgb);
            Bitmap b = null;
            try
            {
                BitmapData data = pieza.LockBits(new Rectangle(0, 0, t, t), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try { for (int y = 0; y < t; y++) Marshal.Copy(pixels, y * t, new IntPtr(data.Scan0.ToInt64() + y * (long)data.Stride), t); }
                finally { pieza.UnlockBits(data); }
                if (t == lado) { Bitmap sola = pieza; pieza = null; return sola; }
                b = new Bitmap(lado, lado, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(b))
                using (TextureBrush tb = new TextureBrush(pieza, WrapMode.Tile))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;   // se COPIA el grano con su alfa, no se mezcla
                    g.FillRectangle(tb, 0, 0, lado, lado);
                }
                Bitmap listo = b; b = null;
                return listo;
            }
            finally
            {
                if (pieza != null) pieza.Dispose();
                if (b != null) b.Dispose();
            }
        }
        // NEBULOSAS DE ESPACIO NATURAL (ideas 5, 7, 8 y 31). Las mismas dos pasadas de la misma textura:
        //  7) cada una a su ritmo y con su recorrido (la lejana se mueve menos y mas despacio), asi se leen
        //     como dos distancias y no como una capa repetida;
        //  8) una respiracion lentisima del brillo, desfasada entre las dos (minutos, no segundos);
        //  5) el mapa ya trae el color del gas: el tinte de la cancion se aclara para no apagarlo;
        // 31) al pasar de pagina se corren un poco, menos que las estrellas (y vuelven despacio a su sitio).
        private double nebNaturalMs = -1, nebParalajeT;
        private float nebParalaje, viaParalaje;
        internal const float ParalajeNebK = .30f, ParalajeViaK = .14f;   // respecto a lo que se corren las estrellas grandes
        internal void EmpujaCapasNatural(float dx)
        {
            if (float.IsNaN(dx) || float.IsInfinity(dx)) return;
            nebParalaje += dx * ParalajeNebK; viaParalaje += dx * ParalajeViaK;
        }
        private void DibujaNieblasNatural(float mx, float my, float c, float sn)
        {
            double now = AhoraMs();
            float dt = nebNaturalMs < 0 ? 0f : (float)Math.Max(0, Math.Min(.25, (now - nebNaturalMs) / 1000.0));
            nebNaturalMs = now; nebParalajeT += dt;
            // Vuelven a su sitio en unos segundos, con el mismo amortiguado para las dos.
            float k = (float)Math.Exp(-dt * .9f);
            nebParalaje *= k; viaParalaje *= k;
            float lim = mx * .20f;
            if (nebParalaje > lim) nebParalaje = lim; else if (nebParalaje < -lim) nebParalaje = -lim;
            float t = (float)nebParalajeT;
            float lejos = .86f + .14f * (float)Math.Sin(t * .037), cerca = .86f + .14f * (float)Math.Sin(t * .029 + 2.1);
            float c2 = (float)Math.Cos(NebFase * 1.7f + 1.3f), s2 = (float)Math.Sin(NebFase * 1.7f + 1.3f);
            uint a1 = (uint)(178f * lejos), a2 = (uint)(178f * cerca);
            NieblaDeriva(mx, my, c * mx * .55f + nebParalaje * .6f, sn * my * .55f,
                (a1 << 24) | TinteGasNatural(TinteNebulosaEstilo(NebColor, false, NebulosasCine, true)));
            NieblaDeriva(mx, my, -c2 * mx * .80f + nebParalaje, -s2 * my * .80f,
                (a2 << 24) | TinteGasNatural(TinteNebulosaEstilo(NebColor2, true, NebulosasCine, true)));
        }
        private float ParalajeVia(float mx)
        {
            float lim = mx * .20f;
            if (viaParalaje > lim) viaParalaje = lim; else if (viaParalaje < -lim) viaParalaje = -lim;
            return viaParalaje;
        }
        // El tinte nocturno, aclarado hacia un gris azulado: el color del gas (que ya trae el mapa) se ve,
        // y la cancion sigue mandando en el matiz. Mismo canal mas alto que menos: no tiñe el cielo entero.
        internal static uint TinteGasNatural(uint tinte)
        {
            uint r = (tinte >> 16) & 255, g = (tinte >> 8) & 255, b = tinte & 255;
            r = 255 - (255 - r) * 45 / 100; g = 255 - (255 - g) * 45 / 100; b = 255 - (255 - b) * 45 / 100;
            return (r << 16) | (g << 8) | b;
        }
        private static float SuaveHora(float x)
        {
            if (!(x > 0f)) return 0f;
            if (x >= 1f) return 1f;
            return x * x * (3f - 2f * x);
        }
    }
}
#endif
