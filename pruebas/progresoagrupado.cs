using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;

// .NET 3.5. Compara triangulos Y estados, ignorando solo los limites de los envios.
// progresoagrupado Rocola.exe [anterior.exe | medir]. Controlador artificial.
class ProgresoAgrupado
{
    const BindingFlags B=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static int checks,calls,triangles; static long bytes; static object device;
    static bool capture; static MemoryStream trace; static BinaryWriter writer;
    delegate void Band(float x,float y,float w,float h,uint color);
    delegate void Tube(float x,float y,float w,float h,uint color,bool intense,double time);
    static object Get(object o,string n){return o.GetType().GetField(n,B).GetValue(o);}
    static void Set(object o,string n,object v){o.GetType().GetField(n,B).SetValue(o,v);}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
    public static int Draw(IntPtr self,uint type,uint tris,IntPtr data,uint stride)
    {
        calls++;triangles+=(int)tris;
        if(capture){
            Check(type==4&&stride==28,"lista de triangulos y formato originales");
            int blend=(int)Get(device,"stBlend"),tss=(int)Get(device,"stTss");long tex=((IntPtr)Get(device,"stTex")).ToInt64();
            Check((blend==3&&tss==1&&tex==0)||(blend==2&&tss==2&&tex==7),"mezcla normal para bandas, aditiva para chispa");
            byte[] dataBytes=new byte[tris*3*stride];Marshal.Copy(data,dataBytes,0,dataBytes.Length);
            for(int t=0;t<tris;t++){
                writer.Write(type);writer.Write(stride);writer.Write(blend);writer.Write(tss);writer.Write(tex);
                writer.Write(dataBytes,t*3*(int)stride,3*(int)stride);
            }
        }
        return 0;
    }
    public static int Two(IntPtr self,uint a,uint b){return 0;}
    public static int Three(IntPtr self,uint a,uint b,uint c){return 0;}
    public static int Texture(IntPtr self,uint stage,IntPtr tex){return 0;}
    static void Bind(object d,string field,string method)
    {FieldInfo f=d.GetType().GetField(field,B);f.SetValue(d,Delegate.CreateDelegate(f.FieldType,typeof(ProgresoAgrupado).GetMethod(method,B)));}
    sealed class Scene:IDisposable
    {
        public Control Owner;public object Device;public Band Plain;public Tube Neon;public Action Flush,Free;
        public Scene(Assembly app)
        {
            Type panel=app.GetType("Rocola.GpuPanel");Owner=(Control)Activator.CreateInstance(panel,true);
            Device=FormatterServices.GetUninitializedObject(app.GetType("Rocola.D3D9Device"));
            Set(Device,"dev",new IntPtr(1));Set(Device,"inScene",true);Set(Device,"stDireccion",1);Set(Device,"verticesSinSoporte",true);
            Flush=(Action)Delegate.CreateDelegate(typeof(Action),Device,Device.GetType().GetMethod("Junta",B));
            Free=(Action)Delegate.CreateDelegate(typeof(Action),Device,Device.GetType().GetMethod("SueltaJunta",B));
            Bind(Device,"drawUp","Draw");Bind(Device,"setTex","Texture");Bind(Device,"setTSS","Three");Bind(Device,"setRS","Two");
            object stars=FormatterServices.GetUninitializedObject(app.GetType("Rocola.GpuStarfield"));
            Set(stars,"gpu",Device);Set(stars,"uiGlowTex",new IntPtr(7));Set(Owner,"stars",stars);
            Plain=(Band)Delegate.CreateDelegate(typeof(Band),Owner,panel.GetMethod("ProgressBand",B));
            Neon=(Tube)Delegate.CreateDelegate(typeof(Tube),Owner,panel.GetMethod("TuboNeon",B));
        }
        public void Dispose(){Free();Set(Device,"dev",IntPtr.Zero);Set(Owner,"stars",null);Owner.Dispose();}
    }
    static byte[] Run(Scene s,int mode,float x,float y,float w,float h,uint color,double time)
    {
        calls=0;triangles=0;device=s.Device;
        trace=new MemoryStream();writer=new BinaryWriter(trace);
        if(mode==0)s.Plain(x,y,w,h,color);else s.Neon(x,y,w,h,color,mode==2,time);
        s.Flush();writer.Flush();byte[] result=trace.ToArray();writer.Close();return result;
    }
    static void Regression(Assembly app,Assembly old)
    {
        capture=true;Random random=new Random(196300);
        using(Scene a=new Scene(app))using(Scene b=old==null?null:new Scene(old)){
            object buffer=Get(a.Owner,"progressV");Check(((Array)buffer).Length==60,"buffer acotado a diez quads");
            foreach(bool joined in new bool[]{false,true}){
            Set(a.Device,"juntar",joined);if(b!=null)Set(b.Device,"juntar",joined);
            foreach(Size size in new Size[]{new Size(1024,768),new Size(1280,1024),new Size(1920,1080)}){
                a.Owner.Size=size;if(b!=null)b.Owner.Size=size;
                for(int mode=0;mode<3;mode++)for(int frame=0;frame<600;frame++){
                    float x=(float)random.NextDouble()*size.Width-20,y=(float)random.NextDouble()*size.Height;
                    float w=frame%17==0?0:frame%19==0?-1:frame%7==0?.1f:(float)random.NextDouble()*size.Width;
                    float h=frame%23==0?0:frame%11==0?.2f:4+(float)random.NextDouble()*40;
                    uint color=(uint)random.Next()^((uint)random.Next()<<16);
                    double time=frame%13==0?double.NaN:frame%3==0?-frame*129.3:frame*4891337.73;
                    byte[] va=Run(a,mode,x,y,w,h,color,time);int nc=calls,nt=triangles;
                    Check(!(bool)Get(a.Device,"lost"),"controlador actual sano");
                    Check(nc==(mode==0?1:h>=4f?2:1),"envios agrupados acotados");
                    Check(nt==(mode==0?4:4+(w>0?8+(h>0?8:0):0)+(h>=4?12:0)),"todos los triangulos de bandas y chispa");
                    if(mode==0)Check(nt==4,"barra y borde completos");
                    if(b!=null){
                        byte[] vb=Run(b,mode,x,y,w,h,color,time);
                        Check(!(bool)Get(b.Device,"lost"),"controlador anterior sano");
                        Check(nt==triangles&&nc<=calls,"mismos triangulos con menos envios");
                        if(joined)Check(nc==calls,"el agrupador general ya igualaba los envios al controlador");
                        Check(va.Length==vb.Length,"misma longitud de captura");
                        for(int i=0;i<va.Length;i++)if(va[i]!=vb[i])throw new Exception("dibujo distinto modo="+mode+" cuadro="+frame+" byte="+i);
                        bytes+=(long)nt*3*28;checks++;
                    }
                    Check(Object.ReferenceEquals(buffer,Get(a.Owner,"progressV")),"sin reemplazar el buffer");
                }
            }
            }
            Set(a.Device,"lost",true);Check(Run(a,2,10,10,100,10,0xABCDEF,12).Length==0,"dispositivo perdido no dibuja");
            Set(a.Device,"lost",false);Check(Run(a,2,10,10,100,10,0xABCDEF,12).Length>0&&calls==2,"recupera sin triangulos anteriores");
        }
        if(old==null)Console.WriteLine("Comparacion independiente OMITIDA: falta release anterior.");
        Console.WriteLine("OK "+checks+" comprobaciones, "+bytes+" bytes; 10800 escenas, con agrupador general apagado y encendido, en tres resoluciones; orden, vertices, texturas y mezcla conservados.");
    }
    static void Measure(Assembly app)
    {
        capture=false;using(Scene s=new Scene(app)){
            foreach(bool joined in new bool[]{false,true}){
            Set(s.Device,"juntar",joined);
            foreach(Size size in new Size[]{new Size(1024,768),new Size(1280,1024),new Size(1920,1080)}){
                s.Owner.Size=size;
                for(int mode=0;mode<3;mode++){
                    double[] samples=new double[31];int count=0;
                    for(int batch=-3;batch<31;batch++){
                        calls=0;triangles=0;long start=Stopwatch.GetTimestamp();
                        for(int frame=0;frame<1000;frame++){
                            if(mode==0)s.Plain(20,size.Height-18,(frame+1)*size.Width/1001f,10,0xA0123456);
                            else s.Neon(20,size.Height-18,(frame+1)*size.Width/1001f,10,0xFF123456,mode==2,frame*16.7);
                            s.Flush();
                        }
                        double us=(Stopwatch.GetTimestamp()-start)*1000000.0/Stopwatch.Frequency/1000;if(batch>=0)samples[batch]=us;
                        Check(!(bool)Get(s.Device,"lost")&&triangles==(mode==0?4000:32000),"medida con todos los triangulos");count=calls/1000;
                        if(joined)Check(count==(mode==0?1:2),"agrupador general realmente activo");
                    }
                    Array.Sort(samples);Console.WriteLine(size+" agrupador="+joined+" modo="+mode+" envios="+count+" mediana_us="+samples[15].ToString("F3",CultureInfo.InvariantCulture)+" p95_lote_us="+samples[29].ToString("F3",CultureInfo.InvariantCulture)+" max_lote_us="+samples[30].ToString("F3",CultureInfo.InvariantCulture));
                }
            }
        }
        }
        Console.WriteLine("31 lotes de 1000 dibujos; sin reflexion ni capturas en el tramo medido. Sin GPU ni audio real; no son FPS.");
    }
    [STAThread]static int Main(string[] args)
    {
        try{Assembly app=Assembly.LoadFile(Path.GetFullPath(args[0]));
            if(args.Length>1&&args[1]=="medir")Measure(app);
            else Regression(app,args.Length>1?Assembly.LoadFile(Path.GetFullPath(args[1])):null);
            return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
