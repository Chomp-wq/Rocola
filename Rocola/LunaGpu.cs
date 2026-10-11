#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Imaging;
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
        internal static Bitmap BakeDetailedMoon()
        {
            const int n = MoonTextureSize;
            float[] relief, albedo;
            MapaLunar(n, out relief, out albedo);
            return SombreaMapaLunar(n, relief, albedo);
        }
        // El mapa de mares y crateres, a cualquier tamano. A 256 sale exactamente el de siempre (mismas
        // cuentas y mismo azar); la luna real de Espacio natural (LunaRealGpu.cs) lo pide mas chico.
        internal static void MapaLunar(int n, out float[] relief, out float[] albedo)
        {
            relief = new float[n * n];
            albedo = new float[n * n];
            double c0 = (n - 1) * .5, rad = n * 124.0 / 256.0, esc = n / 256.0;
            Random random = new Random(7319);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double nx = (x - c0) / rad, ny = (y - c0) / rad;
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
                double cx = n / 16.0 + random.NextDouble() * (n * 224.0 / 256.0), cy = n / 16.0 + random.NextDouble() * (n * 224.0 / 256.0);
                double r = (1.4 + Math.Pow(random.NextDouble(), 3) * 15) * esc;
                double px=(cx-c0)/rad, py=(cy-c0)/rad, distance=Math.Sqrt(px*px+py*py);
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
                        relief[y*n+x] += (float)((bowl+rim)*r/rad*.22);
                        albedo[y*n+x] += (float)(rim*.18 - Math.Max(0,1-d)*.045);
                    }
            }
        }
        private static Bitmap SombreaMapaLunar(int n, float[] relief, float[] albedo)
        {
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
            Bitmap bitmap = new Bitmap(n,n,PixelFormat.Format32bppArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0,0,n,n),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try { for(int y=0;y<n;y++) Marshal.Copy(pixels,y*n,new IntPtr(data.Scan0.ToInt64()+y*data.Stride),n); }
            finally { bitmap.UnlockBits(data); }
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
            float emphasis=LunaProtagonista ? Math.Max(MoonEmphasis(SceneDim,true),idleBlend) : 0;
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
            // 27) Natural: se ladea en las curvas. En 3D el giro positivo va al reves que en el plano.
            float bank=FondoNatural ? BancoNatural(now,cx,cy) : 0f;
            if(Estrella3D)
            {
                DrawStar3d(now,cx,cy,radius,angle-bank,color);
                DrawLunarDust(cx,cy,radius,visibility,true);
                return;
            }
            angle+=bank;
            UpdateStarEchoes(now,cx,cy,radius,angle,color);
            DrawStarEchoes(now);
            if(FondoNatural)color=0xFF000000u|(PaletteFlow.Mix(0xD6E4FFu,color,.26f)&0xFFFFFFu);
            SetTexQuadA(lunaQuad,cx-radius,cy-radius,cx+radius,cy+radius,luU,luV,color);
            RotateStarQuad(lunaQuad,cx,cy,angle);
            stars.DrawQuad(lunaTex,lunaQuad);
            // Punto de luz blanco encima del tinte; la estrella no parece una pegatina plana.
            if(FondoNatural) stars.DrawDestello(cx-radius*.73f,cy-radius*.29f,radius*.30f,0x96F1F7FFu);   // 26) en cruz
            else
            {
            stars.GlowBatchBegin(1);
            stars.GlowBatchAdd(cx-radius*.73f,cy-radius*.29f,radius*(FondoNatural ? .10f : .16f),FondoNatural ? 0xA0F1F7FFu : 0xC0F1F7FFu);
            stars.GlowBatchEnd();
            }
            DrawLunarDust(cx,cy,radius,visibility,true);
        }
        private void DrawLunarDust(float cx,float cy,float radius,float visibility,bool front)
        {
            if (!PolvoLunar && (front || !HaloLunar)) return;
            stars.GlowBatchBegin(21);
            uint rgb=StarColorAt(cx,cy)&0xFFFFFFu;
            if (!front && HaloLunar)
            {
                stars.GlowBatchAdd(cx,cy,radius*(FondoNatural ? 1.52f : 1.9f),((uint)((FondoNatural ? 32 : 50)*visibility)<<24)|rgb);   // la corona, quieta: sin latido
                uint corona=FlowColor(0xC9DAFFu,cx/Math.Max(1,Width),cy/Math.Max(1,Height));
                if(FondoNatural)corona=PaletteFlow.Mix(0xD9E6FFu,corona,.24f)&0xFFFFFFu;
                float sube=FondoNatural && Estrella3D ? 1f+.75f*destelloCorona : 1f;   // 30) con el destello del metal
                stars.GlowBatchAdd(cx-radius*.16f,cy-radius*.06f,radius*(FondoNatural ? 1.08f : 1.20f),((uint)Math.Min(255f,(FondoNatural ? 48 : 64)*visibility*sube)<<24)|corona);
            }
            if (PolvoLunar)
                for(int orbit=0;orbit<3;orbit++)
                {
                    double angle=(LunaActiva ? moonMotionTime*(.55+orbit*.12) : 0)+orbit*2.094;
                    for(int tail=5;tail>=0;tail--)
                    {
                        double a=angle-tail*(FondoNatural ? .085 : .05);
                        double depth = Math.Sin(a);
                        if ((depth>=0)!=front) continue;
                        float dx=(float)Math.Cos(a)*radius*(FondoNatural ? (1.26f+orbit*.15f) : (1.38f+orbit*.17f));
                        float dy=(float)depth*radius*(FondoNatural ? (.32f+orbit*.08f) : (.40f+orbit*.09f))-dx*(FondoNatural ? .16f : .24f);
                        float size=radius*(FondoNatural ? (tail==0 ? .034f : .018f) : (tail==0 ? .043f : .025f));   // el polvo tampoco late: gira a su paso
                        uint alpha=(uint)((FondoNatural ? (tail==0 ? 132 : 34-tail*5) : (tail==0 ? 165 : 58-tail*8))*visibility);
                        if (FondoNatural) {
                            float depthSize, depthLight;
                            ProfundidadPolvoNatural((float)depth, out depthSize, out depthLight);
                            size *= depthSize; alpha = (uint)(alpha * depthLight * PolvoAlLado(dx,dy));   // 28) luz de un lado
                        }
                        uint dust=FlowColor(0xD5E5FFu,(cx+dx)/Math.Max(1,Width),(cy+dy)/Math.Max(1,Height));
                        stars.GlowBatchAdd(cx+dx,cy+dy,Math.Max(1,size),(alpha<<24)|dust);
                    }
                }
            stars.GlowBatchEnd();
        }
    }
}
#endif
