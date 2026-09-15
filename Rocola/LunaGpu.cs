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
        private void DrawDetailedMoon()
        {
            float emphasis=MoonEmphasis(Dim,LunaProtagonista);
            float diameter=DetailedMoonDiameter(Height,emphasis), radius=diameter*.5f;
            double seconds=AhoraMs()/1000;
            float cx=Width*(.79f+(LunaActiva ? .025f*(float)Math.Sin(seconds*.055) : 0));
            float cy=Height*(.47f+(LunaActiva ? .018f*(float)Math.Cos(seconds*.067) : 0))-IdleRise*.32f;
            cx=Math.Max(radius*2,Math.Min(Width-radius*2,cx));
            cy=Math.Max(radius*2,Math.Min(StatusTopY-radius*2,cy));
            float visibility=.72f+.28f*emphasis;
            DrawLunarDust(cx,cy,radius,visibility,false);
            float fr,fg,fb; Form1.TinteDelAire(cy,Height,out fr,out fg,out fb);
            uint color=((uint)(visibility*255)<<24)|((uint)((.82f+.18f*fr)*255)<<16)|
                ((uint)((.82f+.18f*fg)*255)<<8)|(uint)((.82f+.18f*fb)*255);
            SetTexQuadA(lunaQuad,cx-radius,cy-radius,cx+radius,cy+radius,luU,luV,color);
            stars.DrawQuad(lunaTex,lunaQuad);
            DrawLunarDust(cx,cy,radius,visibility,true);
        }
        private void DrawLunarDust(float cx,float cy,float radius,float visibility,bool front)
        {
            if (!PolvoLunar && (front || !HaloLunar)) return;
            stars.GlowBatchBegin(21);
            uint rgb=NebColor&0xFFFFFFu;
            if (!front && HaloLunar)
            {
                float beat=LunaActiva ? lunaBeat : 0;
                stars.GlowBatchAdd(cx,cy,radius*(1.9f+beat*.12f),((uint)((50+beat*22)*visibility)<<24)|rgb);
                stars.GlowBatchAdd(cx-radius*.16f,cy-radius*.06f,radius*1.20f,((uint)(64*visibility)<<24)|0xC9DAFFu);
            }
            if (PolvoLunar)
                for(int orbit=0;orbit<3;orbit++)
                {
                    float band=LunaActiva ? (orbit==0 ? lunaBeat : orbit==1 ? lunaMid : lunaHigh) : 0;
                    double angle=(LunaActiva ? AhoraMs()/1000*(.13+orbit*.035) : 0)+orbit*2.094;
                    for(int tail=5;tail>=0;tail--)
                    {
                        double a=angle-tail*.05;
                        if ((Math.Sin(a)>=0)!=front) continue;
                        float dx=(float)Math.Cos(a)*radius*(1.38f+orbit*.17f);
                        float dy=(float)Math.Sin(a)*radius*(.40f+orbit*.09f)-dx*.24f;
                        float size=radius*(tail==0 ? .043f+band*.019f : .025f);
                        uint alpha=(uint)((tail==0 ? 165+band*65 : 58-tail*8)*visibility);
                        stars.GlowBatchAdd(cx+dx,cy+dy,Math.Max(1,size),(alpha<<24)|0xD5E5FFu);
                    }
                }
            stars.GlowBatchEnd();
        }
    }
}
#endif
