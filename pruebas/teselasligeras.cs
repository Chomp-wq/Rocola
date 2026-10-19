using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

// Analisis completo y por presupuesto contra otro release. No arranca Rocola.
class TeselasLigeras
{
    const BindingFlags B = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static int checks, cases; static long bytes; static int consumed;
    delegate object Analyze(Bitmap b,int side,bool edge);
    delegate object Create(Bitmap b,int side);
    delegate object Advance(object job,long ticks);
    static object Get(object o,string n){return o.GetType().GetField(n,B).GetValue(o);}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
    sealed class Api
    {
        public Type Tiles,Job; public Analyze De; public Create New; public Advance Step;
        public Api(Assembly app)
        {
            Tiles=app.GetType("Rocola.TeselasVisibles");Job=app.GetType("Rocola.TrabajoTeselas");
            De=(Analyze)Delegate.CreateDelegate(typeof(Analyze),Tiles.GetMethod("De",B));
            DynamicMethod make=new DynamicMethod("NewJob",typeof(object),new Type[]{typeof(Bitmap),typeof(int)},typeof(TeselasLigeras),true);
            ILGenerator il=make.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Newobj,Job.GetConstructor(B,null,new Type[]{typeof(Bitmap),typeof(int)},null));il.Emit(OpCodes.Ret);
            New=(Create)make.CreateDelegate(typeof(Create));
            DynamicMethod step=new DynamicMethod("StepJob",typeof(object),new Type[]{typeof(object),typeof(long)},typeof(TeselasLigeras),true);
            il=step.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Castclass,Job);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Call,Job.GetMethod("Avanza",B));il.Emit(OpCodes.Ret);
            Step=(Advance)step.CreateDelegate(typeof(Advance));
        }
    }
    static byte[] Raw(Array a)
    {
        byte[] b=new byte[a.Length*Marshal.SizeOf(a.GetType().GetElementType())];if(b.Length==0)return b;
        GCHandle h=GCHandle.Alloc(a,GCHandleType.Pinned);try{Marshal.Copy(h.AddrOfPinnedObject(),b,0,b.Length);}finally{h.Free();}return b;
    }
    static void Same(object a,object b,string why)
    {
        foreach(string f in new string[]{"Ancho","Alto","N","Fraccion"})Check(Get(a,f).Equals(Get(b,f)),why+" "+f+" actual="+Get(a,f)+" previo="+Get(b,f));
        foreach(string f in new string[]{"Rect","Vtx"}){
            byte[] x=Raw((Array)Get(a,f)),y=Raw((Array)Get(b,f));Check(x.Length==y.Length,why+" longitud "+f);
            for(int i=0;i<x.Length;i++)if(x[i]!=y[i])throw new Exception(why+" "+f+" byte="+i);
            bytes+=x.Length;checks++;
        }
    }
    static object Finish(Api api,Bitmap b,int side,long budget)
    {
        // libgdiplus puede escribir en el origen al desbloquear una region ReadOnly.
        // Cada ejecucion recibe una copia independiente, identica antes de empezar.
        using(Bitmap input=(Bitmap)b.Clone()) {
        object job=api.New(input,side);Check(api.Step(job,0)==null&&api.Step(job,-1)==null,"presupuesto no positivo no avanza");
        object done=null;int loops=0;
        while(done==null){done=api.Step(job,budget);if(++loops>100000)throw new Exception("no termina el analisis");}
        Check(Object.ReferenceEquals(done,api.Step(job,1)),"resultado terminado estable");return done;
        }
    }
    static void Compare(Api a,Api old,Bitmap bitmap,int side,bool edge,int sample)
    {
        object current=a.De(bitmap,side,edge);Check((int)Get(current,"N")==((int[])Get(current,"Rect")).Length/4,"recuento de rectangulos");
        if(old!=null)Same(current,old.De(bitmap,side,edge),"sincrono");
        if(!edge){
            long budget=sample%2==0?1:Stopwatch.Frequency/1000;
            object stepped=Finish(a,bitmap,side,budget);Same(current,stepped,"por presupuesto");
            if(old!=null)Same(stepped,Finish(old,bitmap,side,budget),"por presupuesto anterior caso="+cases+" lado="+side+" muestra="+sample);
        }
        cases++;
    }
    static Bitmap Image(int width,int height,int kind,int seed)
    {
        Bitmap b=new Bitmap(width,height,PixelFormat.Format32bppArgb);int[] row=new int[width];Random random=new Random(seed);
        BitmapData d=b.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
        try{for(int y=0;y<height;y++){
            for(int x=0;x<width;x++){
                bool on=kind==0?false:kind==1?true:kind==2?(x%12<3):kind==3?((x+(y/8)%3*4)%24<5):kind==4?(random.Next(100)<6):((seed&(1<<((x/6)%3+3*((y/6)%3))))!=0);
                row[x]=on?unchecked((int)(0x01000000u|((uint)(x*771+y*1531)&0xFFFFFFu))):0x00123456;
            }
            Marshal.Copy(row,0,new IntPtr(d.Scan0.ToInt64()+(long)y*d.Stride),width);
        }}finally{b.UnlockBits(d);}return b;
    }
    static void Probe(Api api,string name)
    {
        using(Bitmap b=Image(18,18,5,1)) {
            int before=b.GetPixel(0,0).ToArgb();object job=api.New(b,1);int loops=0;
            while(api.Step(job,1)==null)if(++loops>100000)throw new Exception("sonda no termina");
            Console.WriteLine("SONDA ReadOnly "+name+" antes="+before.ToString("X8")+" despues="+b.GetPixel(0,0).ToArgb().ToString("X8")+"; entradas de comparacion independientes.");
        }
    }
    static void Regression(Api a,Api old)
    {
        Probe(a,"actual");if(old!=null)Probe(old,"anterior");
        Check(a.De(null,2,false)==null,"imagen nula");
        for(int mask=0;mask<512;mask++)using(Bitmap b=Image(18,18,5,mask))
            foreach(int side in new int[]{1,2,4,16})foreach(bool edge in new bool[]{false,true})Compare(a,old,b,side,edge,mask);
        foreach(Size size in new Size[]{new Size(1,1),new Size(17,33),new Size(257,129),new Size(1024,768),new Size(1280,1024),new Size(1920,1080)})
        for(int kind=0;kind<5;kind++)using(Bitmap b=Image(size.Width,size.Height,kind,6300+kind))
            foreach(int side in new int[]{2,16})foreach(bool edge in new bool[]{false,true})Compare(a,old,b,side,edge,kind);
        if(old==null)Console.WriteLine("Comparacion independiente OMITIDA: falta release anterior.");
        Console.WriteLine("OK "+checks+" comprobaciones, "+cases+" casos, "+bytes+" bytes comparados.");
    }
    static void Timing(string name,Bitmap source,Func<Bitmap,object> work)
    {
        double[] times=new double[21];for(int batch=-2;batch<times.Length;batch++){
            using(Bitmap input=(Bitmap)source.Clone()) {
                long start=Stopwatch.GetTimestamp();object result=work(input);double ms=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
                consumed+=(int)Get(result,"N");if(batch>=0)times[batch]=ms;
            }
        }
        Array.Sort(times);Console.WriteLine(name+" mediana_ms="+times[10].ToString("F4",CultureInfo.InvariantCulture)+" p95_ms="+times[19].ToString("F4",CultureInfo.InvariantCulture)+" max_ms="+times[20].ToString("F4",CultureInfo.InvariantCulture));
    }
    static void Measure(Api api)
    {
        foreach(Size size in new Size[]{new Size(1024,768),new Size(1280,1024),new Size(1920,1080)})
        foreach(int kind in new int[]{0,1,2,3})using(Bitmap b=Image(size.Width,size.Height,kind,6300))
        {
            int side=kind==3?16:2;
            Timing(size+" patron="+kind+" lado="+side+" completo",b,delegate(Bitmap input){return api.De(input,side,false);});
            Timing(size+" patron="+kind+" lado="+side+" incremental",b,delegate(Bitmap input){
                object job=api.New(input,side),result=null;while(result==null)result=api.Step(job,Stopwatch.Frequency/2000);return result;
            });
        }
        Console.WriteLine("Control="+consumed+"; tiempo total de analisis, no tiempo de cuadro ni GPU. Patrones: 0 vacio, 1 opaco, 2 fragmentado, 3 tramos cambiantes.");
    }
    static int Main(string[] args)
    {
        try{
            Api a=new Api(Assembly.LoadFile(System.IO.Path.GetFullPath(args[0])));
            if(args.Length>1&&args[1]=="medir")Measure(a);
            else Regression(a,args.Length>1?new Api(Assembly.LoadFile(System.IO.Path.GetFullPath(args[1]))):null);
            return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
