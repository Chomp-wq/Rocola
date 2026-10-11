#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace Rocola
{
    internal struct StarMusic { public float Bass,Mid,High; }
    internal sealed partial class GpuPanel
    {
        // Cinco puntas facetadas como rocola.ico. Geometria propia horneada una vez
        // para la GPU: no se modifica el icono ni el renderizador CPU.
        internal static Bitmap BakeStar(bool detailed)
        { return BakeStarEstilo(detailed, false); }
        internal static Bitmap BakeStarNatural(bool detailed)
        { return BakeStarEstilo(detailed, true); }
        private static Bitmap BakeStarEstilo(bool detailed, bool natural)
        {
            const int n = MoonTextureSize;
            int[] pixels = new int[n*n];
            for (int y=0;y<n;y++) for (int x=0;x<n;x++)
            {
                double dx=(x-127.5)/127.5,dy=(y-127.5)/127.5,r=dx*dx+dy*dy;
                int a=r>=1?0:(int)((natural ? 18 : 32)*Math.Exp(-r*3)*(1-r));
                pixels[y*n+x]=unchecked((int)((uint)a<<24))|0xB0CFFF;
            }
            Bitmap b=new Bitmap(n,n,PixelFormat.Format32bppArgb);
            BitmapData data=b.LockBits(new Rectangle(0,0,n,n),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try { for(int y=0;y<n;y++)Marshal.Copy(pixels,y*n,new IntPtr(data.Scan0.ToInt64()+y*(long)data.Stride),n); }
            finally { b.UnlockBits(data); }
            PointF center=new PointF(127.5f,127.5f);
            PointF[] edge=new PointF[10];
            for(int i=0;i<10;i++){
                double angle=-Math.PI*.5+i*Math.PI/5;
                float r=i%2==0?110f:49f;
                edge[i]=new PointF(center.X+r*(float)Math.Cos(angle),center.Y+r*(float)Math.Sin(angle));
            }
            using(Graphics g=Graphics.FromImage(b))
            using(SolidBrush body=new SolidBrush(natural ? Color.FromArgb(205,214,226) : Color.FromArgb(185,201,230)))
            using(Pen rim=new Pen(natural ? Color.FromArgb(220,36,46,61) : Color.FromArgb(235,22,34,61),natural ? 1.6f : 3f)){
                g.SmoothingMode=SmoothingMode.AntiAlias;
                g.FillPolygon(body,edge);
                if(detailed){
                    // Caras plata a la izquierda y acero azulado a la derecha; siguen admitiendo tinte.
                    Color[] facets={Color.FromArgb(231,239,255),Color.FromArgb(125,166,228),
                        Color.FromArgb(57,106,184),Color.FromArgb(27,57,113),Color.FromArgb(71,105,163),
                        Color.FromArgb(27,44,77),Color.FromArgb(139,156,188),Color.FromArgb(222,230,246),
                        Color.FromArgb(170,192,226),Color.FromArgb(244,248,255)};
                    if(natural)facets=new Color[]{Color.FromArgb(243,247,252),Color.FromArgb(189,204,221),
                        Color.FromArgb(107,127,150),Color.FromArgb(61,76,96),Color.FromArgb(116,134,154),
                        Color.FromArgb(51,65,83),Color.FromArgb(154,173,193),Color.FromArgb(223,232,241),
                        Color.FromArgb(197,211,225),Color.FromArgb(248,251,255)};
                    for(int i=0;i<10;i++)
                    using(LinearGradientBrush face=new LinearGradientBrush(center,edge[i],
                        facets[i],Color.FromArgb(255,Math.Min(255,facets[i].R+14),Math.Min(255,facets[i].G+14),Math.Min(255,facets[i].B+14))))
                        g.FillPolygon(face,new PointF[]{center,edge[i],edge[(i+1)%10]});
                }
                g.DrawPolygon(rim,edge);
                using(Pen bevel=new Pen(natural ? Color.FromArgb(106,235,243,252) : Color.FromArgb(150,225,238,255),natural ? .8f : 1.2f))
                    for(int i=0;i<10;i+=2)g.DrawLine(bevel,center,edge[i]);
                if(detailed){
                    PaintStarGlint(g,29,96,natural ? 9 : 15);
                    PaintStarGlint(g,173,166,natural ? 5 : 9);
                }
            }
            return b;
        }
        private static void PaintStarGlint(Graphics g,float x,float y,float r)
        { g.FillPolygon(Brushes.White,new PointF[]{new PointF(x,y-r),new PointF(x+2,y-2),new PointF(x+r,y),new PointF(x+2,y+2),new PointF(x,y+r),new PointF(x-2,y+2),new PointF(x-r,y),new PointF(x-2,y-2)}); }

        public Func<StarMusic> GetStarMusic;
        private string starGenre;
        private float starGenreTarget=1,starGenreDrive=1,starDrive,starAttack,starPreviousEnergy;
        private double starTravelX=.65,starTravelY=2.8;
        private float starVelocityX=.0234f,starVelocityY=.018f;
        private double starEchoPhase;
        internal void SetStarGenre(string genre)
        {
            if(string.Equals(starGenre,genre,StringComparison.OrdinalIgnoreCase))return;
            starGenre=genre;starGenreTarget=StarGenreDrive(genre);
        }
        internal static float StarGenreDrive(string genre)
        {
            string g=(genre??"").ToLowerInvariant();
            if(g.Contains("metal")||g.Contains("electr")||g.Contains("dance")||g.Contains("techno")||g.Contains("rock"))return 1.22f;
            if(g.Contains("clasic")||g.Contains("clásic")||g.Contains("classical")||g.Contains("ambient")||g.Contains("balad")||g.Contains("jazz"))return .78f;
            if(g.Contains("regga")||g.Contains("hip")||g.Contains("rap"))return 1.12f;
            return 1;
        }
        // LA ESTRELLA YA NO BAILA CON LA MUSICA (lo pidio el dueño el 24-09-2026: "da brincos"). Con cada golpe le
        // cambiaban de golpe la velocidad, el giro, el balanceo, el tamaño y el halo, y a la vista eran saltitos.
        // Ahora va siempre a su ritmo de CRUCERO -el que antes le daba una cancion de energia media- y solo cambia
        // de ritmo con lo que cambia despacio: se calma al navegar y se anima en reposo.
        //
        // La musica se sigue escuchando aqui, pero solo para lo que NO es la estrella: la velocidad del flujo de
        // color del espectro (starDrive, PaletaCaratulaGpu.cs) y las barritas de la que suena (lunaBeat y demas,
        // DiscoVivoGpu.cs).
        internal const float StarCrucero = .40f;                                   // el starDrive de una cancion media
        internal const float StarCruceroMedios = .30f, StarCruceroAgudos = .20f;   // y sus medios y agudos
        internal void AdvanceStarMusic(float dt,float bass,float mid,float high)
        {
            dt=Math.Min(.1f,SafeMoonLevel(dt));
            bass=SafeMoonLevel(bass);mid=SafeMoonLevel(mid);high=SafeMoonLevel(high);
            float energy=.52f*bass+.28f*mid+.20f*high;
            float onset=Math.Max(0,energy-starPreviousEnergy);starPreviousEnergy=energy;
            starAttack=Math.Max(starAttack*(float)Math.Exp(-dt*4.5),Math.Min(1,onset*4));
            starGenreDrive+=(starGenreTarget-starGenreDrive)*(1-(float)Math.Exp(-dt*2));
            float target=SafeMoonLevel((energy+starAttack*.35f)*starGenreDrive);
            starDrive+=(target-starDrive)*(1-(float)Math.Exp(-dt*(target>starDrive?12:3)));
            if(LunaActiva){
                float calm=.18f+.82f*SafeMoonLevel(idleBlend);
                // Velocidades de crucero, fijas: la trayectoria no tiene ni un tiron. Lo unico que las mueve es 'calm',
                // que cambia despacio (el reposo entra y sale en segundos), y aun asi pasa por la velocidad filtrada.
                AdvanceStarVelocity(ref starTravelX,ref starVelocityX,calm*(.13f+.62f*StarCrucero),dt);
                AdvanceStarVelocity(ref starTravelY,ref starVelocityY,calm*(.10f+.30f*StarCruceroMedios+.22f*StarCruceroAgudos),dt);
                // El giro en 3D (Estrella3dGpu.cs): una vuelta cada ~3,4 s en reposo y cada ~19 s navegando, con la
                // misma suavidad que el recorrido.
                AdvanceStarVelocity(ref starYaw,ref starSpin,calm*(float)(2*Math.PI)*(.16f+.34f*StarCrucero),dt);
                starEchoPhase=(starEchoPhase+dt*(.30+.45*StarCrucero))%1.0;
                moonMotionTime+=dt*MoonMotionSpeed(idleBlend)*(1+.55*StarCrucero);
            }
        }
        private static void AdvanceStarVelocity(ref double phase,ref float velocity,float target,float dt)
        {
            const double response=5;
            double blend=1-Math.Exp(-dt*response);
            phase+=target*dt+(velocity-target)*blend/response;
            velocity+=(target-velocity)*(float)blend;
        }
        private PointF StarSceneCenter()
        {
            // Reservar el tamano maximo (el del reposo), no el de ahora: crecer
            // no desplaza los limites de la orbita.
            float margin=DetailedMoonDiameter(Height,1)*MoonSceneScale(1,true)*.5f;
            return StarTravelCenter(Width,Height,starTravelX,starTravelY,margin,StatusTopY,IdleRise);
        }
        internal static PointF StarTravelCenter(int w,int h,double px,double py,float radius,float statusTop,float rise)
        {
            float mx=Math.Min(w*.45f,Math.Max(12,radius*1.65f));
            float top=Math.Min(h*.25f,Math.Max(12,radius*1.65f));
            float bottom=Math.Max(top,Math.Min(statusTop-radius*1.8f,h*.52f-Math.Max(0,rise)*.22f));
            double x=.5+.49*Math.Sin(px+.18*Math.Sin(py));
            double y=.5+.48*Math.Sin(py+.24*Math.Sin(px*.71));
            return new PointF(mx+(w-2*mx)*(float)x,top+(bottom-top)*(float)y);
        }
        public bool EstelaEstelar = true;
        private struct StarEcho
        {
            internal double Time;
            internal float X,Y,Radius,Angle,Strength;
            internal uint Color;
            internal double Yaw;      // el giro en 3D de ese momento (la copia lo conserva)
            internal float Pitch;
        }
        private readonly StarEcho[] starEchoes=new StarEcho[12];
        private readonly D3D9.Vtx[] starEchoQuad=new D3D9.Vtx[6];
        private readonly D3D9.Vtx[] starEchoBatch=new D3D9.Vtx[12*6];
        private int starEchoHead,starEchoCount,starEchoWidth,starEchoHeight;
        private double starEchoLast=-1,starEchoClock=-1;
        private float starEchoVisibility;
        private void ResetStarEchoes()
        { starEchoHead=starEchoCount=0;starEchoLast=starEchoClock=-1;starEchoVisibility=0;starEchoCamino=0;starEchoHayUlt=false; }

        internal static float StarEchoAlpha(double age,float visibility)
        {
            if(double.IsNaN(age)||age<0||age>=1.8)return 0;
            float life=(float)(1-age/1.8);
            return .86f*SafeMoonLevel(visibility)*life;
        }
        internal static float StarEchoAlphaNatural(double age,float visibility,bool threeD)
        {
            double life = threeD ? EcoVida : 1.8;
            if (double.IsNaN(age) || age < 0 || age >= life) return 0;
            float entrance = age < EcoEntra ? (float)(age / EcoEntra) : 1;
            float remaining = (float)(1 - age / life);
            // Perfil polinomico suave; no ruido, reservas ni una potencia por eco.
            return .72f * SafeMoonLevel(visibility) * entrance * remaining * remaining;
        }
        private void UpdateStarEchoes(double now,float x,float y,float radius,float angle,uint color)
        {
            // Cambios de pantalla, pausas y reinicios no dejan fantasmas en otro lugar.
            if(starEchoWidth!=Width||starEchoHeight!=Height||now<starEchoClock||
                (starEchoClock>=0&&now-starEchoClock>.3))
            { ResetStarEchoes();starEchoWidth=Width;starEchoHeight=Height; }
            float dt=starEchoClock<0?0:(float)Math.Max(0,Math.Min(.1,now-starEchoClock));
            starEchoClock=now;
            float target=EstelaEstelar&&LunaActiva?SafeMoonLevel(idleBlend):0;
            starEchoVisibility+=(target-starEchoVisibility)*(1-(float)Math.Exp(-dt*8));
            if(target<=0)
            {
                if(starEchoVisibility<.002f){starEchoCount=0;starEchoLast=-1;}
                return;
            }
            if(starEchoLast>=0&&now-starEchoLast<.14-.07*StarCrucero)return;
            // Si no hubo desplazamiento real, no apilar siluetas en el mismo punto.
            if(starEchoCount>0)
            {
                StarEcho last=starEchoes[(starEchoHead+starEchoes.Length-1)%starEchoes.Length];
                float dx=x-last.X,dy=y-last.Y;
                if(dx*dx+dy*dy<1)return;
            }
            starEchoes[starEchoHead]=new StarEcho {Time=now,X=x,Y=y,Radius=radius,Angle=angle,
                Strength=.48f+.52f*StarCrucero,Color=StarEchoColor(color)};
            starEchoHead=(starEchoHead+1)%starEchoes.Length;
            starEchoCount=Math.Min(starEchoes.Length,starEchoCount+1);starEchoLast=now;
        }
        private uint StarEchoColor(uint color)
        {
            // Paleta real del disco/genero y fase continua, nunca el indice del
            // buffer circular. Cada eco conserva el color de cuando nacio.
            uint accent=PaletteFlow.Sample(flowColors,(float)(starEchoPhase*1.25),0);
            uint rgb=PaletteFlow.Mix(color,accent,.85f);
            // Luz neutra para leer las facetas con portadas oscuras; sin colores fijos.
            return 0xFF000000u|((48+((rgb>>16)&255)*207/255)<<16)|
                ((48+((rgb>>8)&255)*207/255)<<8)|(48+(rgb&255)*207/255);
        }
        private static void RotateStarQuad(D3D9.Vtx[] quad,float x,float y,float angle)
        {
            float co=(float)Math.Cos(angle),si=(float)Math.Sin(angle);
            for(int i=0;i<quad.Length;i++)
            {
                float dx=quad[i].X-x,dy=quad[i].Y-y;
                quad[i].X=x+dx*co-dy*si;quad[i].Y=y+dx*si+dy*co;
            }
        }
        private void DrawStarEchoes(double now)
        {
            int vertices=0;
            for(int i=0;i<starEchoCount;i++)
            {
                StarEcho echo=starEchoes[(starEchoHead-starEchoCount+i+starEchoes.Length)%starEchoes.Length];
                double age=now-echo.Time;
                float alpha=(FondoNatural ? StarEchoAlphaNatural(age,starEchoVisibility,false) : StarEchoAlpha(age,starEchoVisibility))*echo.Strength;
                if(age<.055||alpha<.003f)continue;
                float r=echo.Radius*(.88f+.10f*(float)(1-age/1.8));
                uint tint=((uint)(alpha*255)<<24)|(echo.Color&0xFFFFFFu);
                if(FondoNatural)
                {
                    // 29) La copia envejece: se abre y pierde color (la misma cuenta que en 3D).
                    float edad=EdadEco(age,1.8);
                    r*=1f+.35f*edad;
                    D3D9.Vtx gris=new D3D9.Vtx(); gris.Color=tint; EnvejeceVertice(ref gris,0,0,1f,.70f*edad); tint=gris.Color;
                }
                SetTexQuadA(starEchoQuad,echo.X-r,echo.Y-r,echo.X+r,echo.Y+r,luU,luV,tint);
                RotateStarQuad(starEchoQuad,echo.X,echo.Y,echo.Angle);
                Array.Copy(starEchoQuad,0,starEchoBatch,vertices,6);vertices+=6;
            }
            if(vertices>0)stars.DrawTexturedBatch(lunaTex,starEchoBatch,vertices/3);
        }
        private uint StarColor()
        {
            float position=(float)(.5+.5*Math.Sin(moonMotionTime*.035));
            uint rgb=MixCoverColor(NebColor,CoverColor(position));
            uint r=(rgb>>16)&255,g=(rgb>>8)&255,b=rgb&255;
            return 0xFF000000u|((64+r*191/255)<<16)|((64+g*191/255)<<8)|(64+b*191/255);
        }
        internal static float IdleWallAlpha(float legacyAlpha)
        { return float.IsNaN(legacyAlpha) ? 1f : Math.Max(0f,Math.Min(1f,(legacyAlpha-.1f)/.9f)); }
    }
}
#endif
