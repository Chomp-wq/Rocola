#if ROCOLA_GPU
using System;
using System.Drawing; using Rocola.Nativo; using SkiaSharp;   // EN LINUX: el horno de la luna hace su mapa con SkiaSharp directo y las funciones nativas (Xp/Nativo), no con GDI+
using System.Runtime.InteropServices;
namespace Rocola
{
    internal sealed partial class GpuPanel
    {
        public bool LunaRealista = true, HaloLunar = true, PolvoLunar = true, LunaProtagonista = true;
        private bool lunaDetalleDe;
        internal const int MoonTextureSize = 256;
        // Solo prepara una textura del renderizador GPU, nunca la pared CPU.
        // Mapa determinista de mares/crateres; no se calcula ni se sube por cuadro.
        internal static SKBitmap BakeDetailedMoon()   // EN LINUX: un SKBitmap (nadie lo pide como Bitmap: no lo llama nadie, tampoco en XP)
        {
            const int n = MoonTextureSize;
            float[] relief = new float[n * n];
            float[] albedo = new float[n * n];
            Random random = new Random(7319);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double nx = (x - 127.5) / 124, ny = (y - 127.5) / 124;
                    double sea = Math.Sin(nx * 5 + Math.Sin(ny * 6)) * Math.Cos(ny * 5 - nx * 2);
                    double maria = Math.Exp(-((nx+.25)*(nx+.25)*9+(ny+.32)*(ny+.32)*13))
                        + .8*Math.Exp(-((nx+.48)*(nx+.48)*20+(ny-.10)*(ny-.10)*12));
                    double grain = random.NextDouble() - .5;
                    albedo[y * n + x] = (float)(.74 + .12 * sea - .22*Math.Min(1,maria)*(1+.2*sea) + .085 * grain);
                    relief[y * n + x] = (float)(grain * .0007);
                }
            // Cuencas con fondo hundido, pared y borde elevado; radios variados.
            for (int crater = 0; crater < 230; crater++)
            {
                double cx = 16 + random.NextDouble() * 224, cy = 16 + random.NextDouble() * 224;
                double r = 1.4 + Math.Pow(random.NextDouble(), 3) * 15;
                double px=(cx-127.5)/124, py=(cy-127.5)/124, distance=Math.Sqrt(px*px+py*py);
                if (distance>=.97) continue;
                double radialX=distance>.001 ? px/distance : 1, radialY=distance>.001 ? py/distance : 0;
                double foreshorten=Math.Max(.25,Math.Sqrt(1-distance*distance));
                int left = Math.Max(0, (int)(cx-r*1.3)), right = Math.Min(n-1, (int)(cx+r*1.3));
                int top = Math.Max(0, (int)(cy-r*1.3)), bottom = Math.Min(n-1, (int)(cy+r*1.3));
                for (int y = top; y <= bottom; y++)
                    for (int x = left; x <= right; x++)
                    {
                        double radial=((x-cx)*radialX+(y-cy)*radialY)/foreshorten;
                        double tangent=-(x-cx)*radialY+(y-cy)*radialX;
                        double d = Math.Sqrt(radial*radial+tangent*tangent)/r;
                        if (d > 1.3) continue;
                        double bowl = d < 1 ? -.65 * (1-d*d) : 0;
                        double rim = .32 * Math.Exp(-Math.Pow((d-.94)/.12,2));
                        relief[y*n+x] += (float)((bowl+rim)*r/124*.22);
                        albedo[y*n+x] += (float)(rim*.18 - Math.Max(0,1-d)*.045);
                    }
            }
            int[] pixels = new int[n*n];
            for (int y = 1; y < n-1; y++)
                for (int x = 1; x < n-1; x++)
                {
                    double nx=(x-127.5)/124, ny=(y-127.5)/124, d=nx*nx+ny*ny;
                    if (d >= 1) continue;
                    int i=y*n+x;
                    double z=Math.Sqrt(1-d);
                    double dx=(relief[i+1]-relief[i-1])*62, dy=(relief[i+n]-relief[i-n])*62;
                    double sx=nx-dx*z, sy=ny-dy*z;
                    double len=Math.Sqrt(sx*sx+sy*sy+z*z);
                    double sun=Math.Max(0,(-.83*sx-.25*sy+.50*z)/len);
                    // La cara nocturna conserva luz terrestre tenue, no un recorte negro.
                    double v=Math.Min(1,albedo[i]*(.105+.99*Math.Pow(sun,.72)));
                    int a=(int)(255*Math.Min(1,(1-Math.Sqrt(d))*124));
                    int red=(int)(255*v), green=(int)(250*v), blue=(int)(241*v+5*(1-sun));
                    pixels[i]=(a<<24)|(red<<16)|(green<<8)|Math.Min(255,blue);
                }
            SKBitmap bitmap = Mapas.Nuevo(n,n);   // EN LINUX: new Bitmap(n,n,Format32bppArgb): un SKBitmap premultiplicado y transparente (Nativo/Mapas.cs)
            // EN LINUX: LockBits(WriteOnly, Format32bppArgb), Marshal.Copy fila a fila y UnlockBits: los puntos sin premultiplicar, al mapa
            Pixeles.EscribeSinPremultiplicar(bitmap,pixels,n);   // EN LINUX: (Nativo/Pixeles.cs: premultiplica como la capa de la transicion de la referencia; diferencia (a) de PORT_DIBUJO.md)
            // EN LINUX: (sin UnlockBits: no hay copia aparte que soltar)
            return bitmap;
        }
        internal static float MoonEmphasis(float backgroundDim, bool enabled)
        { return enabled ? 1-SafeMoonLevel(backgroundDim) : 0; }
        internal static float DetailedMoonDiameter(int height, float emphasis)
        { return Math.Max(48,Math.Min(180,height*.115f))*(1+.22f*SafeMoonLevel(emphasis)); }
        private double moonMotionTime;
        // En reposo la estrella se agranda un 32 %, despacio. Ya no late con los golpes (EstrellaGpu.cs, StarCrucero).
        internal static float MoonSceneScale(float idle, bool active)
        { return active ? 1 + SafeMoonLevel(idle) * .32f : 1; }
        // Su balanceo: un vaiven lento, siempre igual de ancho.
        internal static float StarRoll(double motionTime)
        { return (float)((.08 + .16 * StarCrucero) * Math.Sin(motionTime * .31)); }
        internal static float MoonMotionSpeed(float idle)
        { return .30f + 1.30f * SafeMoonLevel(idle); }
        internal static PointF DetailedMoonCenter(int width, int height, double phase, float idle, float rise, bool active)
        {
            float mix = active ? SafeMoonLevel(idle) : 0;
            return new PointF(width * (.79f + (active ? (.004f + .052f * mix) * (float)Math.Sin(phase * .70) : 0)),
                height * (.47f - .22f * mix + (active ? (.003f + .028f * mix) * (float)Math.Cos(phase * .70) : 0)) - rise * .32f);
        }
        private void DrawDetailedMoon()
        {
            float emphasis=LunaProtagonista ? Math.Max(MoonEmphasis(Dim,true),idleBlend) : 0;
            float diameter=DetailedMoonDiameter(Height,emphasis)*MoonSceneScale(idleBlend,LunaActiva), radius=diameter*.5f;
            PointF center=LunaActiva
                ? StarSceneCenter()
                : DetailedMoonCenter(Width,Height,moonMotionTime,idleBlend,IdleRise,false);
            float cx=center.X, cy=center.Y;
            cx=Math.Max(radius,Math.Min(Width-radius,cx));
            cy=Math.Max(radius,Math.Min(StatusTopY-radius,cy));
            float visibility=LunaActiva ? .40f+.60f*idleBlend : .72f+.28f*emphasis;
            DrawLunarDust(cx,cy,radius,visibility,false);
            // La superficie es solida: solo el borde antialias y el halo llevan transparencia.
            uint color=StarColorAt(cx,cy);
            float angle=LunaActiva ? StarRoll(moonMotionTime) : 0;
            // El momento en que se va a VER este cuadro (Estrella3dGpu.cs, "EL RELOJ DE LA PANTALLA").
            double now=(cuadroMs>=0 ? cuadroMs : AhoraMs())/1000.0;
            if(Estrella3D)
            {
                DrawStar3d(now,cx,cy,radius,angle,color);
                DrawLunarDust(cx,cy,radius,visibility,true);
                return;
            }
            if (!(ReposoSinEstela && AhorroReposo))
            {
                UpdateStarEchoes(now,cx,cy,radius,angle,color);
                DrawStarEchoes(now);
            }
            SetTexQuadA(lunaQuad,cx-radius,cy-radius,cx+radius,cy+radius,luU,luV,color);
            RotateStarQuad(lunaQuad,cx,cy,angle);
            stars.DrawQuad(lunaTex,lunaQuad);
            // Punto de luz blanco encima del tinte; la estrella no parece una pegatina plana.
            stars.GlowBatchBegin(1);
            stars.GlowBatchAdd(cx-radius*.73f,cy-radius*.29f,radius*.16f,0xC0F1F7FFu);
            stars.GlowBatchEnd();
            DrawLunarDust(cx,cy,radius,visibility,true);
        }
        private struct MuestraPolvo
        {
            internal float Seno, Coseno;
            internal bool Frente;
        }
        private MuestraPolvo[] polvoMuestras;
        private double polvoFase;
        private void PreparaPolvoLunar()
        {
            double fase = LunaActiva ? moonMotionTime : 0;
            if (polvoMuestras != null && polvoFase == fase) return;
            if (polvoMuestras == null) polvoMuestras = new MuestraPolvo[18];
            for (int orbit = 0; orbit < 3; orbit++) {
                double angle = fase * (.55 + orbit * .12) + orbit * 2.094;
                for (int tail = 5; tail >= 0; tail--) {
                    double a = angle - tail * .05, seno = Math.Sin(a);
                    int index = orbit * 6 + tail;
                    polvoMuestras[index].Frente = seno >= 0;
                    polvoMuestras[index].Seno = (float)seno;
                    polvoMuestras[index].Coseno = (float)Math.Cos(a);
                }
            }
            // Solo la orbita: posicion, radio, color y alfa siguen vivos en cada pasada.
            polvoFase = fase;
        }
        private void DrawLunarDust(float cx,float cy,float radius,float visibility,bool front)
        {
            if (!PolvoLunar && (front || !HaloLunar)) return;
            stars.GlowBatchBegin(21);
            if (!front && HaloLunar)
            {
                uint rgb=StarColorAt(cx,cy)&0xFFFFFFu;
                stars.GlowBatchAdd(cx,cy,radius*(CoronaCompacta ? 1.05f : 1.9f),((uint)(50*visibility)<<24)|rgb);   // la corona, quieta: sin latido
                stars.GlowBatchAdd(cx-radius*.16f,cy-radius*.06f,radius*(CoronaCompacta ? .78f : 1.20f),((uint)(64*visibility)<<24)|FlowColor(0xC9DAFFu,cx/Math.Max(1,Width),cy/Math.Max(1,Height)));
            }
            if (PolvoLunar)
            {
                PreparaPolvoLunar();
                for(int orbit=0;orbit<3;orbit++)
                {
                    for(int tail=5;tail>=0;tail--)
                    {
                        MuestraPolvo muestra = polvoMuestras[orbit * 6 + tail];
                        if (muestra.Frente != front) continue;
                        float dx=muestra.Coseno*radius*(1.38f+orbit*.17f);
                        float dy=muestra.Seno*radius*(.40f+orbit*.09f)-dx*.24f;
                        float size=radius*(tail==0 ? .043f : .025f);   // el polvo tampoco late: gira a su paso
                        uint alpha=(uint)((tail==0 ? 165 : 58-tail*8)*visibility);
                        uint dust=FlowColor(0xD5E5FFu,(cx+dx)/Math.Max(1,Width),(cy+dy)/Math.Max(1,Height));
                        stars.GlowBatchAdd(cx+dx,cy+dy,Math.Max(1,size),(alpha<<24)|dust);
                    }
                }
            }
            stars.GlowBatchEnd();
        }
    }
}
#endif
