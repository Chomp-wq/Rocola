using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;

// .NET 3.5 + leercodigo.cs; [anterior.exe | medir]. Controlador artificial.
class RejillaLigera
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static int checks, calls, triangles; static long bytes; static byte[] sent; static bool capture;
    delegate void Native<T>(int index, T cell, uint color);
    delegate void Atlas<T,V>(T cell, float u, float v, uint color, V[] result);
    static object Get(object o,string n) { return o.GetType().GetField(n,B).GetValue(o); }
    static void Set(object o,string n,object v) { o.GetType().GetField(n,B).SetValue(o,v); }
    static void Check(bool ok,string why) { if(!ok)throw new Exception(why);checks++; }
    public static int Draw(IntPtr self,uint type,uint tris,IntPtr data,uint stride)
    { calls++;triangles=(int)tris;if(capture){sent=new byte[tris*3*stride];Marshal.Copy(data,sent,0,sent.Length);}return 0; }
    public static int Two(IntPtr self,uint a,uint b) { return 0; }
    public static int Three(IntPtr self,uint a,uint b,uint c) { return 0; }
    public static int Texture(IntPtr self,uint stage,IntPtr tex) { return 0; }
    static void Bind(object d,string field,string method)
    { FieldInfo f=d.GetType().GetField(field,B);f.SetValue(d,Delegate.CreateDelegate(f.FieldType,typeof(RejillaLigera).GetMethod(method,B))); }
    sealed class Scene : IDisposable
    {
        public Type Panel,Cell,Vertex; public Control Owner; public object Device,Source; public Bitmap Cover;
        public Scene(Assembly assembly)
        {
            Panel=assembly.GetType("Rocola.GpuPanel");Cell=assembly.GetType("Rocola.GpuGridCell");Vertex=assembly.GetType("Rocola.D3D9+Vtx");
            Owner=(Control)Activator.CreateInstance(Panel,true);
            Device=FormatterServices.GetUninitializedObject(assembly.GetType("Rocola.D3D9Device"));
            Set(Device,"dev",new IntPtr(1));Set(Device,"stBlend",3);Set(Device,"stDireccion",1);
            Bind(Device,"drawUp","Draw");Bind(Device,"setTex","Texture");Bind(Device,"setTSS","Three");Bind(Device,"setRS","Two");
            object star=FormatterServices.GetUninitializedObject(assembly.GetType("Rocola.GpuStarfield"));Set(star,"gpu",Device);Set(Owner,"stars",star);
            Cover=new Bitmap(1,1);Source=Activator.CreateInstance(Panel.GetNestedType("CellTex",B),true);
            Set(Source,"Tex",new IntPtr(7));Set(Source,"Bw",256);Set(Source,"Bh",256);
            ((IDictionary)Get(Owner,"nativeCovers")).Add(Cover,Source);
            ((IntPtr[])Get(Owner,"nativeGridTex"))[0]=new IntPtr(7);
            ((float[])Get(Owner,"nativeGridU"))[0]=.75f;((float[])Get(Owner,"nativeGridV"))[0]=.875f;
        }
        public object MakeCell(int i,int width,int height)
        {
            object cell=Activator.CreateInstance(Cell),layout=Activator.CreateInstance(Panel.Assembly.GetType("Rocola.GpuCellLayout"));
            Set(cell,"X",(float)(i%6)*width/6-17.25f);Set(cell,"Y",(float)(i%3)*height/3+3.125f);
            Set(cell,"W",width/6f);Set(cell,"H",height/3f);Set(cell,"CoverRect",new RectangleF((float)Get(cell,"X"),(float)Get(cell,"Y"),width/7f,height/4f));
            Set(cell,"FullCover",Cover);Set(cell,"CoverReady",i%2==0);
            foreach(FieldInfo f in layout.GetType().GetFields(B)) if(f.FieldType==typeof(Rectangle))
                f.SetValue(layout,new Rectangle(i%7,i%11,32+i%127,4+i%53));
            Set(layout,"Width",512);Set(layout,"Height",512);Set(layout,"Separate",i%2==0);Set(layout,"HideBlank",i%3==0);
            Set(cell,"Layout",layout);return cell;
        }
        public void Dispose()
        {
            Set(Device,"dev",IntPtr.Zero);Set(Owner,"stars",null);
            ((IDictionary)Get(Owner,"nativeCovers")).Clear();Array.Clear((Array)Get(Owner,"nativeGridTex"),0,36);
            Owner.Dispose();Cover.Dispose();
        }
    }
    static byte[] Raw(Array a)
    {
        byte[] b=new byte[a.Length*Marshal.SizeOf(a.GetType().GetElementType())];GCHandle h=GCHandle.Alloc(a,GCHandleType.Pinned);
        try { Marshal.Copy(h.AddrOfPinnedObject(),b,0,b.Length); } finally { h.Free(); }return b;
    }
    static void Same(byte[] a,byte[] b,string name)
    {
        Check((a==null)==(b==null),name+" visibilidad");if(a==null)return;
        Check(a.Length==b.Length,name+" longitud");for(int i=0;i<a.Length;i++)if(a[i]!=b[i])throw new Exception(name+" byte="+i);
        bytes+=a.Length;checks++;
    }
    static byte[] Render(Scene s,object c,uint color,out int count)
    { sent=null;calls=0;triangles=0;s.Panel.GetMethod("DrawNativeCover",B).Invoke(s.Owner,new object[]{0,c,color});Check(calls<=1,"un envio por portada");count=triangles;return sent; }
    static void Regression(Assembly app,Assembly old)
    {
        capture=true;
        using(Scene a=new Scene(app))using(Scene b=old==null?null:new Scene(old))
        {
            Array va=Array.CreateInstance(a.Vertex,42),vb=b==null?null:Array.CreateInstance(b.Vertex,42);
            foreach(Size size in new Size[]{new Size(1024,768),new Size(1280,1024),new Size(1920,1080)})
            for(int i=0;i<1000;i++)
            {
                object ca=a.MakeCell(i,size.Width,size.Height),cb=b==null?null:b.MakeCell(i,size.Width,size.Height);
                uint color=((uint)(i%256)<<24)|((uint)(i*16381)&0xFFFFFFu);
                foreach(Scene s in b==null?new Scene[]{a}:new Scene[]{a,b}) {
                    Set(s.Source,"Bw",1+i%1024);Set(s.Source,"Bh",1+i%513);
                    ((float[])Get(s.Owner,"nativeGridU"))[0]=.25f+(i%8)/16f;
                    ((float[])Get(s.Owner,"nativeGridV"))[0]=.25f+(i%4)/8f;
                    ((IntPtr[])Get(s.Owner,"nativeGridTex"))[0]=i%5==0?IntPtr.Zero:new IntPtr(7);
                }
                if(i%10==0){Set(ca,"FullCover",null);if(cb!=null)Set(cb,"FullCover",null);}
                int na,nb;byte[] ra=Render(a,ca,color,out na);
                if(b!=null){byte[] rb=Render(b,cb,color,out nb);Check(na==nb,"mismos triangulos");Same(ra,rb,"portada");}
                else Check(na==0||na==2||na==18,"portada conserva 0/2/18 triangulos");
                a.Panel.GetMethod("AtlasCellVertices",B).Invoke(null,new object[]{ca,.75f,.875f,color,va});
                if(b!=null){b.Panel.GetMethod("AtlasCellVertices",B).Invoke(null,new object[]{cb,.75f,.875f,color,vb});Same(Raw(va),Raw(vb),"atlas");}
            }
            if(b==null)Console.WriteLine("Comparacion contra anterior OMITIDA: falta segundo ejecutable.");
        }
        Console.WriteLine("OK "+checks+" comprobaciones, "+bytes+" bytes; 3000 portadas y 3000 atlas en tres resoluciones.");
    }
    static void Timing(string name,Action work)
    {
        double[] times=new double[31];for(int b=-3;b<31;b++){
            long start=Stopwatch.GetTimestamp();for(int i=0;i<1000;i++)work();
            double us=(Stopwatch.GetTimestamp()-start)*1000000.0/Stopwatch.Frequency/1000;if(b>=0)times[b]=us;
        }
        Array.Sort(times);Console.WriteLine(name+" mediana_us="+times[15].ToString("F3",CultureInfo.InvariantCulture)+" p95_lote_us="+times[29].ToString("F3",CultureInfo.InvariantCulture)+" max_lote_us="+times[30].ToString("F3",CultureInfo.InvariantCulture));
    }
    static void Measure<T,V>(Assembly app)
    {
        capture=false;using(Scene s=new Scene(app)){
            Native<T> native=(Native<T>)Delegate.CreateDelegate(typeof(Native<T>),s.Owner,s.Panel.GetMethod("DrawNativeCover",B));
            Atlas<T,V> atlas=(Atlas<T,V>)Delegate.CreateDelegate(typeof(Atlas<T,V>),s.Panel.GetMethod("AtlasCellVertices",B));
            V[] vertices=new V[42];
            foreach(Size size in new Size[]{new Size(1024,768),new Size(1280,1024),new Size(1920,1080)}){
                T[] cells=new T[36];for(int i=0;i<36;i++)cells[i]=(T)s.MakeCell(i,size.Width,size.Height);
                Timing(size+" portadas36",delegate{for(int i=0;i<36;i++)native(0,cells[i],0xC0FFFFFFu);});
                Timing(size+" atlas36",delegate{for(int i=0;i<36;i++)atlas(cells[i],.75f,.875f,0xC0FFFFFFu,vertices);});
            }
        }
        Console.WriteLine("Lotes sinteticos: 36 celdas, atlas mixtos; sin GPU, audio ni monitor. No son FPS.");
    }
    [STAThread] static int Main(string[] args)
    {
        try{
            Assembly app=Assembly.LoadFile(System.IO.Path.GetFullPath(args[0]));
            if(args.Length>1&&args[1]=="medir")typeof(RejillaLigera).GetMethod("Measure",B).MakeGenericMethod(app.GetType("Rocola.GpuGridCell"),app.GetType("Rocola.D3D9+Vtx")).Invoke(null,new object[]{app});
            else Regression(app,args.Length>1?Assembly.LoadFile(System.IO.Path.GetFullPath(args[1])):null);
            return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
