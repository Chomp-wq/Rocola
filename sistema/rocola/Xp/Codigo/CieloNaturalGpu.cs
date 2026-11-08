#if ROCOLA_GPU
using System;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: el horno del cielo profundo hace su mapa con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+
using System.Runtime.InteropServices;
namespace Rocola
{
    internal sealed partial class GpuPanel
    {
        private static float NoiseHash(int x,int y)
        {
            uint v=unchecked((uint)(x*374761393+y*668265263));
            v=unchecked((v^(v>>13))*1274126177u);
            return ((v^(v>>16))&65535)/65535f;
        }
        private static float CloudNoise(float x,float y)
        {
            int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);
            float fx=x-ix,fy=y-iy; fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
            float a=NoiseHash(ix,iy),b=NoiseHash(ix+1,iy),c=NoiseHash(ix,iy+1),d=NoiseHash(ix+1,iy+1);
            return (a+(b-a)*fx)*(1-fy)+(c+(d-c)*fx)*fy;
        }
        internal static SKBitmap BakeNatural(int w,int h)   // EN LINUX: un SKBitmap: lo sube D3D9Star.cs (EnsureNieblas), que ya recibe SKBitmap
        {
            if(w<1||h<1)throw new ArgumentOutOfRangeException("w");
            SKBitmap b=Mapas.Nuevo(w,h);   // EN LINUX: new Bitmap(w,h,Format32bppArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
            int[] pixels=new int[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float u=x/(float)Math.Max(1,w-1),v=y/(float)Math.Max(1,h-1);
                float warp=CloudNoise(u*4+7,v*4+3),n=0,amp=.52f,scale=5;
                for(int k=0;k<5;k++){n+=amp*CloudNoise(u*scale+warp*2,v*scale+warp+11);amp*=.5f;scale*=2.03f;}
                float ridge=1-Math.Abs(CloudNoise(u*27+warp*4,v*22+warp*3)*2-1);
                float envelope=(float)Math.Exp(-Math.Pow((v-(.35+.15*Math.Sin(u*6)))*2.4,2));
                float value=Math.Max(0,n-.28f)*envelope*(.6f+.4f*ridge*ridge);
                // La fuerza multiplica el VALOR y no el tope: asi la bruma ancha baja y los picos se
                // quedan donde estan, que es donde vive la forma de la nube. Bajando tambien el tope
                // saldria lo mismo pero todo mas oscuro, que no es lo que se quiere.
                int alpha=(int)Math.Min(125,value*180*Form1.NebFuerza*Form1.nebNaturalFuerza);
                pixels[y*w+x]=unchecked((int)((uint)alpha<<24|0x00FFFFFFu));
            }
            // EN LINUX: LockBits(WriteOnly, Format32bppArgb), Marshal.Copy fila a fila y UnlockBits: los puntos sin premultiplicar, al mapa
            Pixeles.EscribeSinPremultiplicar(b,pixels,w);   // EN LINUX: (Nativo/Pixeles.cs)
            // EN LINUX: (sin UnlockBits: no hay copia aparte que soltar)
            return b;
        }
        internal static float PerfilDefinido(float[] bands,float x)
        {
            if(bands==null||bands.Length<2)return 0;
            if(float.IsNaN(x))x=0;
            float t=Math.Max(0,Math.Min(1,x))*(bands.Length-1); int center=(int)t;float sum=0;
            // Solo center y center+1 pueden aportar: t cae dentro de [center,center+1), asi que
            // center-1 y center+2 quedan siempre a distancia 1 o mas y la guarda de abajo (d>=1) los
            // descarta SIEMPRE. Eran dos vueltas de cada cuatro que no podian sumar nada, y esto corre
            // una vez por columna de la aurora en cada cuadro.
            for(int i=center;i<=Math.Min(bands.Length-1,center+1);i++)
            {
                float d=Math.Abs(t-i)/.90f;if(d>=1)continue;
                float value=bands[i];if(!(value>0))continue;value=Math.Min(1,value);
                float k=1-d*d*(3-2*d);sum=Math.Max(sum,value*k);
            }
            return Math.Min(1,sum);
        }

        // La malla siempre consulta las mismas 145 posiciones y SampleWave entrega ocho bandas.
        // Solo dos bandas pueden alcanzar cada columna (ambos radios son menores que uno).
        // Guardar los pesos espaciales exactos evita repetir distancias/divisiones/polinomios;
        // las intensidades musicales se leen de nuevo en cada muestra, sin cachearlas aqui.
        private struct PerfilColumna
        {
            internal int Banda0, Banda1;
            internal float Peso0, Peso1;
        }
        private static readonly PerfilColumna[] aurPerfilSuave = PreparaPerfilColumnas(false);
        private static readonly PerfilColumna[] aurPerfilDefinido = PreparaPerfilColumnas(true);
        private static PerfilColumna[] PreparaPerfilColumnas(bool definido)
        {
            PerfilColumna[] tabla = new PerfilColumna[AurCols + 1];
            for (int c = 0; c <= AurCols; c++)
            {
                float fx = (float)c / AurCols;
                float t = fx * 7; int centro = (int)t;
                PerfilColumna p = new PerfilColumna(); p.Banda0 = p.Banda1 = -1;
                for (int b = centro; b <= Math.Min(7, centro + 1); b++)
                {
                    float d = Math.Abs(t - b); d /= definido ? .90f : AurLobulo;
                    if (d >= 1f) continue;
                    float k = 1f - d * d * (3f - 2f * d);
                    if (p.Banda0 < 0) { p.Banda0 = b; p.Peso0 = k; }
                    else { p.Banda1 = b; p.Peso1 = k; }
                }
                tabla[c] = p;
            }
            return tabla;
        }
        internal static float AurPerfilColumna(float[] bands, int columna, bool definido)
        {
            // Conservar tambien el contrato general de las formulas, aunque SampleWave use ocho.
            if (bands == null || bands.Length != 8 || columna < 0 || columna > AurCols)
            {
                float fx = (float)columna / AurCols;
                return definido ? PerfilDefinido(bands, fx) : AurPerfil(bands, fx);
            }
            PerfilColumna p = (definido ? aurPerfilDefinido : aurPerfilSuave)[columna];
            if (definido)
            {
                float sum = 0f;
                if (p.Banda0 >= 0) {
                    float value = bands[p.Banda0];
                    if (value > 0f) sum = Math.Max(sum, Math.Min(1f, value) * p.Peso0);
                }
                if (p.Banda1 >= 0) {
                    float value = bands[p.Banda1];
                    if (value > 0f) sum = Math.Max(sum, Math.Min(1f, value) * p.Peso1);
                }
                return Math.Min(1f, sum);
            }
            double suma = 0.0;
            if (p.Banda0 >= 0) { double v = bands[p.Banda0] * p.Peso0; v *= v; suma += v * v; }
            if (p.Banda1 >= 0) { double v = bands[p.Banda1] * p.Peso1; v *= v; suma += v * v; }
            if (suma <= 0.0) return 0f;
            return (float)Math.Sqrt(Math.Sqrt(suma));
        }
    }
}
#endif
