using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;

// Compara los vertices enviados por las dos pasadas del polvo orbital.
// Uso: polvoligero Rocola.exe [anterior.exe | medir]. Controlador artificial.
class PolvoLigero
{
    const BindingFlags B=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static int checks,calls,triangles;static long bytes;static bool capture;static byte[] sent;
    delegate void Dust(float x,float y,float radius,float visibility,bool front);
    static object Get(object o,string n){return o.GetType().GetField(n,B).GetValue(o);}
    static void Set(object o,string n,object v){o.GetType().GetField(n,B).SetValue(o,v);}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
    public static int Draw(IntPtr self,uint type,uint tris,IntPtr data,uint stride)
    {calls++;triangles=(int)tris;if(capture){sent=new byte[tris*3*stride];Marshal.Copy(data,sent,0,sent.Length);}return 0;}
    public static int Two(IntPtr self,uint a,uint b){return 0;}
    public static int Three(IntPtr self,uint a,uint b,uint c){return 0;}
    public static int Texture(IntPtr self,uint stage,IntPtr tex){return 0;}
    static void Bind(object d,string field,string method)
    {FieldInfo f=d.GetType().GetField(field,B);f.SetValue(d,Delegate.CreateDelegate(f.FieldType,typeof(PolvoLigero).GetMethod(method,B)));}
    sealed class Scene:IDisposable
    {
        public Control Owner;public object Device;public Dust Render;public Action<double> Time;
        public Scene(Assembly a)
        {
            Type panel=a.GetType("Rocola.GpuPanel");Owner=(Control)Activator.CreateInstance(panel,true);
            Device=FormatterServices.GetUninitializedObject(a.GetType("Rocola.D3D9Device"));
            Set(Device,"dev",new IntPtr(1));Set(Device,"stBlend",2);Set(Device,"stDireccion",1);
            Set(Device,"verticesSinSoporte",true); // El controlador artificial captura DrawPrimitiveUP.
            Bind(Device,"drawUp","Draw");Bind(Device,"setTex","Texture");Bind(Device,"setTSS","Three");Bind(Device,"setRS","Two");
            object stars=FormatterServices.GetUninitializedObject(a.GetType("Rocola.GpuStarfield"));Set(stars,"gpu",Device);Set(stars,"uiGlowTex",new IntPtr(7));Set(Owner,"stars",stars);
            Render=(Dust)Delegate.CreateDelegate(typeof(Dust),Owner,panel.GetMethod("DrawLunarDust",B));
            DynamicMethod tick=new DynamicMethod("SetDustTime",typeof(void),new Type[]{typeof(object),typeof(double)},typeof(PolvoLigero),true);
            ILGenerator il=tick.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Castclass,panel);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Stfld,panel.GetField("moonMotionTime",B));il.Emit(OpCodes.Ret);
            Time=(Action<double>)tick.CreateDelegate(typeof(Action<double>),Owner);
        }
        public void Config(int flags,int frame,Size size)
        {
            Owner.Size=size;Set(Owner,"PolvoLunar",(flags&1)!=0);Set(Owner,"HaloLunar",(flags&2)!=0);
            Set(Owner,"LunaActiva",(flags&4)!=0);Set(Owner,"CoronaCompacta",(flags&8)!=0);
            Set(Owner,"flowBlend",(flags&16)!=0?.85f:0f);Set(Owner,"coverWeight",(frame%3)*.5f);
            Set(Owner,"NebColor",0xFF123456u+(uint)frame*137);
            uint[] flow=(uint[])Get(Owner,"flowColors"),cover=(uint[])Get(Owner,"coverBands");
            for(int i=0;i<flow.Length;i++)flow[i]=(uint)(i*112397+frame*31991)&0xFFFFFFu;
            for(int i=0;i<cover.Length;i++)cover[i]=(uint)(i*219919+frame*7371)&0xFFFFFFu;
            Time(frame<60?(frame/2)*.093:(79-frame)*.071);
        }
        public void Dispose(){Set(Device,"dev",IntPtr.Zero);Set(Owner,"stars",null);Owner.Dispose();}
    }
    static byte[] Run(Scene s,float x,float y,float radius,float alpha,bool front,out int n)
    {sent=null;calls=0;triangles=0;s.Render(x,y,radius,alpha,front);Check(calls<=1,"un envio por pasada");n=triangles;return sent;}
    static void Same(byte[] a,byte[] b)
    {
        Check((a==null)==(b==null),"misma visibilidad");if(a==null)return;
        Check(a.Length==b.Length,"mismos vertices");for(int i=0;i<a.Length;i++)if(a[i]!=b[i])throw new Exception("vertice distinto byte="+i);
        bytes+=a.Length;checks++;
    }
    static void Regression(Assembly app,Assembly old)
    {
        capture=true;using(Scene a=new Scene(app))using(Scene b=old==null?null:new Scene(old)){
            object cache=null;
            foreach(Size size in new Size[]{new Size(1024,768),new Size(1280,1024),new Size(1920,1080)})
            for(int flags=0;flags<32;flags++)for(int frame=0;frame<80;frame++){
                a.Config(flags,frame,size);if(b!=null)b.Config(flags,frame,size);
                int total=0;
                foreach(bool front in frame%2==0?new bool[]{false,true}:new bool[]{true,false}){
                    float x=size.Width*.7f+frame*.17f,y=size.Height*.4f-frame*.13f,r=frame%9==0?.4f:20+frame*.8f,alpha=(frame%11)/10f;
                    int na,nb;byte[] va=Run(a,x,y,r,alpha,front,out na);
                    total+=na;
                    Check(na<=120&&na%6==0,"recuento de halos acotado");
                    if(b!=null){byte[] vb=Run(b,x,y,r,alpha,front,out nb);Check(na==nb,"mismo orden y numero de triangulos");Same(va,vb);}
                }
                Check(!(bool)Get(a.Device,"lost"),"controlador actual sano");
                if(b!=null)Check(!(bool)Get(b.Device,"lost"),"controlador anterior sano");
                Check(total==((flags&1)!=0?108:0)+((flags&2)!=0&&frame%9!=0?12:0),"todas las particulas y halos enviados");
                if((flags&1)!=0){
                    object next=Get(a.Owner,"polvoMuestras");Check(next!=null&&((Array)next).Length==18,"dieciocho muestras acotadas");
                    if(cache!=null)Check(Object.ReferenceEquals(cache,next),"sin reemplazar el buffer al cambiar tiempo/opciones");cache=next;
                }else if(cache==null)Check(Get(a.Owner,"polvoMuestras")==null,"sin reservar muestras con polvo apagado");
            }
            // Un dispositivo perdido no debe enviar nada; recuperar permite usar las muestras existentes.
            a.Config(7,12,new Size(1024,768));Set(a.Device,"lost",true);int n;
            Check(Run(a,400,300,70,1,false,out n)==null,"dispositivo perdido");Set(a.Device,"lost",false);
            Check(Run(a,400,300,70,1,false,out n)!=null,"recuperacion de dispositivo");
        }
        if(old==null)Console.WriteLine("Comparacion independiente OMITIDA: falta release anterior.");
        Console.WriteLine("OK "+checks+" comprobaciones, "+bytes+" bytes; 7680 escenas, 15360 pasadas, 32 combinaciones y tres resoluciones.");
    }
    static void Measure(Assembly app)
    {
        capture=false;using(Scene s=new Scene(app)){
            foreach(Size size in new Size[]{new Size(1024,768),new Size(1280,1024),new Size(1920,1080)})
            foreach(int mode in new int[]{0,1,2,3}){
                // 0 sin polvo, 1 polvo quieto, 2 movimiento cada cuadro, 3 fase repetida cada dos.
                s.Config(mode==0?2:mode==1?3:7,1,size);double[] samples=new double[31];
                for(int batch=-3;batch<31;batch++){
                    calls=0;
                    long start=Stopwatch.GetTimestamp();
                    for(int frame=0;frame<1000;frame++){
                        s.Time((mode==3?frame/2:frame)*.017);
                        s.Render(size.Width*.7f,size.Height*.4f,70,.7f,false);
                        s.Render(size.Width*.7f,size.Height*.4f,70,.7f,true);
                    }
                    double us=(Stopwatch.GetTimestamp()-start)*1000000.0/Stopwatch.Frequency/1000;if(batch>=0)samples[batch]=us;
                    Check(!(bool)Get(s.Device,"lost"),"controlador sano durante medida");
                    Check(calls==(mode==0?1000:2000),"todos los envios durante medida");
                }
                Array.Sort(samples);Console.WriteLine(size+" modo="+mode+" mediana_us="+samples[15].ToString("F3",CultureInfo.InvariantCulture)+" p95_lote_us="+samples[29].ToString("F3",CultureInfo.InvariantCulture)+" max_lote_us="+samples[30].ToString("F3",CultureInfo.InvariantCulture));
            }
        }
        Console.WriteLine("31 lotes de 1000 pares de pasadas; delegados sin reflexion/capturas dentro de la medida. Sin GPU ni audio real; no son FPS.");
    }
    [STAThread]static int Main(string[] args)
    {
        try{
            Assembly app=Assembly.LoadFile(System.IO.Path.GetFullPath(args[0]));
            if(args.Length>1&&args[1]=="medir")Measure(app);
            else Regression(app,args.Length>1?Assembly.LoadFile(System.IO.Path.GetFullPath(args[1])):null);
            return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
