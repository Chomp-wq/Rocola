using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

// Paletas: mismos puntos, alfa y colores. paletaligera Rocola.exe [anterior.exe | medir].
class PaletaLigera
{
    const BindingFlags B=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    delegate int[] Samples(Bitmap b,int nx,int ny);
    delegate Color[] Palette(Bitmap b);
    static int checks,cases;static long samples;static int sink;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
    sealed class Api
    {
        public Samples Read;public Palette Colors;
        public Api(Assembly a){Type t=a.GetType("Rocola.Form1");
            Read=(Samples)Delegate.CreateDelegate(typeof(Samples),t.GetMethod("MuestrasPaleta",B));
            Colors=(Palette)Delegate.CreateDelegate(typeof(Palette),t.GetMethod("ExtractCoverPalette",B));}
    }
    static Color Pixel(int x,int y,int pattern)
    {
        int a=255,r=(x*17+y*3)&255,g=(x*5+y*29)&255,b=(x*31+y*11)&255;
        if(pattern==1)r=g=b=(x+y)&255;
        if(pattern==2)a=(x*13+y*7)&255;
        if(pattern==3)a=((x+y)&1)==0?127:128;
        if(pattern==4)r=g=b=0;
        return Color.FromArgb(a,r,g,b);
    }
    static Bitmap Create(int w,int h,PixelFormat fmt,int pattern)
    {
        Bitmap b=new Bitmap(w,h,fmt);bool indexed=fmt==PixelFormat.Format8bppIndexed;
        if(indexed){ColorPalette p=b.Palette;for(int i=0;i<p.Entries.Length;i++)p.Entries[i]=Pixel(i,0,pattern);b.Palette=p;}
        BitmapData d=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,fmt);
        try{
            int stride=Math.Abs(d.Stride),step=indexed?1:fmt==PixelFormat.Format24bppRgb?3:4;byte[] row=new byte[stride];
            for(int y=0;y<h;y++){
                for(int x=0;x<w;x++){
                    if(indexed){row[x]=(byte)((x+y)&255);continue;}
                    Color c=Pixel(x,y,pattern);int a=c.A,r=c.R,g=c.G,bl=c.B;
                    if(fmt==PixelFormat.Format32bppPArgb){r=r*a/255;g=g*a/255;bl=bl*a/255;}
                    int at=x*step;row[at]=(byte)bl;row[at+1]=(byte)g;row[at+2]=(byte)r;
                    if(step==4)row[at+3]=(byte)a;
                }
                Marshal.Copy(row,0,new IntPtr(d.Scan0.ToInt64()+(long)y*d.Stride),stride);
            }
        }finally{b.UnlockBits(d);}
        return b;
    }
    static int[] Oracle(Bitmap b,int nx,int ny)
    {
        int w=b.Width,h=b.Height;int[] a=new int[nx*ny];
        for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)a[y*nx+x]=b.GetPixel((2*x+1)*w/(2*nx),(2*y+1)*h/(2*ny)).ToArgb();
        return a;
    }
    static void Same(int[] a,int[] b,string why)
    {
        Check((a==null)==(b==null),why+" disponibilidad");if(a==null)return;
        Check(a.Length==b.Length,why+" longitud");
        for(int i=0;i<a.Length;i++)Check(a[i]==b[i],why+" muestra "+i);
        samples+=a.Length;
    }
    static void SameColors(Color[] a,Color[] b)
    {
        Check(a.Length==b.Length,"misma cantidad de tonos");
        for(int i=0;i<a.Length;i++)Check(a[i].ToArgb()==b[i].ToArgb(),"mismo tono y orden");
    }
    static void Case(Api a,Api old,Bitmap b,Bitmap prior)
    {
        cases++;int nx=Math.Min(32,b.Width),ny=Math.Min(32,b.Height);
        int[] expected=Oracle(b,nx,ny),now=a.Read(b,nx,ny);
        bool fallback=b.PixelFormat==PixelFormat.Format8bppIndexed;
        if(b.PixelFormat==PixelFormat.Format32bppPArgb)foreach(int c in expected)if((uint)c>>24!=255)fallback=true;
        Check((now==null)==fallback,"solo PArgb translucido e indexados usan GetPixel");
        if(now!=null)Same(now,expected,"oraculo GetPixel");
        if(old!=null){Same(now,old.Read(prior,nx,ny),"release anterior");SameColors(a.Colors(b),old.Colors(prior));}
        else Check(a.Colors(b).Length<=8,"paleta acotada");
        // El lector debe desbloquear incluso cuando abandona la ruta PArgb.
        BitmapData d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,b.PixelFormat);b.UnlockBits(d);Check(true,"bitmap desbloqueado");
    }
    static void Regression(Api a,Api old)
    {
        Check(a.Colors(null).Length==0,"imagen nula sin tonos");
        PixelFormat[] formats={PixelFormat.Format24bppRgb,PixelFormat.Format32bppRgb,PixelFormat.Format32bppArgb,PixelFormat.Format32bppPArgb,PixelFormat.Format8bppIndexed};
        foreach(Size s in new Size[]{new Size(1,1),new Size(2,3),new Size(17,19),new Size(31,33),new Size(73,59),new Size(257,129),new Size(1024,768),new Size(1920,1080)})
        foreach(PixelFormat fmt in formats)for(int p=0;p<5;p++)
            using(Bitmap b=Create(s.Width,s.Height,fmt,p))using(Bitmap prior=old==null?null:Create(s.Width,s.Height,fmt,p))Case(a,old,b,prior);
        // Memoria de abajo hacia arriba, incluidos alfa 127/128 y RGB opaco.
        for(int p=0;p<5;p++){
            int size=17*19*4;IntPtr mem=Marshal.AllocHGlobal(size),before=Marshal.AllocHGlobal(size);
            try{using(Bitmap b=new Bitmap(17,19,-68,PixelFormat.Format32bppArgb,new IntPtr(mem.ToInt64()+68*18)))
                using(Bitmap prior=new Bitmap(17,19,-68,PixelFormat.Format32bppArgb,new IntPtr(before.ToInt64()+68*18))){
                    for(int y=0;y<19;y++)for(int x=0;x<17;x++){Color c=Pixel(x,y,p);b.SetPixel(x,y,c);prior.SetPixel(x,y,c);}
                    Case(a,old,b,prior);
                }
            }finally{Marshal.FreeHGlobal(mem);Marshal.FreeHGlobal(before);}
        }
        if(old==null)Console.WriteLine("Comparacion independiente OMITIDA: falta release anterior.");
        Console.WriteLine("OK "+checks+" comprobaciones; "+cases+" imagenes, "+samples+" muestras comparadas; paletas, alfa, orden y desbloqueo conservados.");
    }
    static void Measure(Api a)
    {
        foreach(Size s in new Size[]{new Size(73,59),new Size(1024,768),new Size(1920,1080)})
        foreach(PixelFormat fmt in new PixelFormat[]{PixelFormat.Format24bppRgb,PixelFormat.Format32bppArgb,PixelFormat.Format32bppPArgb,PixelFormat.Format8bppIndexed})
        foreach(int pattern in new int[]{0,2})using(Bitmap b=Create(s.Width,s.Height,fmt,pattern)){
            double[] times=new double[21];
            for(int batch=-2;batch<21;batch++){
                long start=Stopwatch.GetTimestamp();
                for(int i=0;i<10;i++)sink+=a.Colors(b).Length;
                double us=(Stopwatch.GetTimestamp()-start)*1000000.0/Stopwatch.Frequency/10;if(batch>=0)times[batch]=us;
            }
            Array.Sort(times);Console.WriteLine(s+" formato="+fmt+" patron="+pattern+" mediana_us="+times[10].ToString("F3",CultureInfo.InvariantCulture)+" p95_lote_us="+times[19].ToString("F3",CultureInfo.InvariantCulture)+" max_lote_us="+times[20].ToString("F3",CultureInfo.InvariantCulture));
        }
        Console.WriteLine("21 lotes de 10 extracciones completas, sin reflexion ni creacion de imagenes dentro del tramo medido; control="+sink+". Linux/GDI+, no FPS ni disco real.");
    }
    static int Main(string[] args)
    {
        try{Api app=new Api(Assembly.LoadFile(Path.GetFullPath(args[0])));
            if(args.Length>1&&args[1]=="medir")Measure(app);
            else Regression(app,args.Length>1?new Api(Assembly.LoadFile(Path.GetFullPath(args[1]))):null);
            return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
