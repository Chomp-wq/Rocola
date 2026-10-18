using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

// .NET 3.5 + leercodigo.cs. Segundo argumento: release anterior o "medir".
class CubosLigeros
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type device, vertex;
    static bool capture;
    static byte[] sent;
    static int calls, checks;
    static long bytes;
    delegate void Cube(IntPtr tex, float u, float v, float x, float y, float w, float h, double angle, float alpha, double tilt);
    static object Get(object o,string n){return o.GetType().GetField(n,B).GetValue(o);}
    static void Set(object o,string n,object v){o.GetType().GetField(n,B).SetValue(o,v);}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
    public static int Draw(IntPtr self,uint type,uint tris,IntPtr data,uint stride)
    {calls++;if(capture){sent=new byte[tris*3*stride];Marshal.Copy(data,sent,0,sent.Length);}return 0;}
    public static int Two(IntPtr self,uint a,uint b){return 0;}
    public static int Three(IntPtr self,uint a,uint b,uint c){return 0;}
    public static int Texture(IntPtr self,uint stage,IntPtr tex){return 0;}
    static void Bind(object d,string field,string method)
    {FieldInfo f=d.GetType().GetField(field,B);f.SetValue(d,Delegate.CreateDelegate(f.FieldType,typeof(CubosLigeros).GetMethod(method,B)));}
    static object Device(Type t)
    {
        object d=FormatterServices.GetUninitializedObject(t);Type v=t.Assembly.GetType("Rocola.D3D9+Vtx");
        Set(d,"dev",new IntPtr(1));Set(d,"stBlend",3);Set(d,"stDireccion",1);
        Set(d,"cbx",new double[4]);Set(d,"cbz",new double[4]);Set(d,"cbo",new int[4]);
        Set(d,"cubeFaceV",Array.CreateInstance(v,6));Set(d,"cubeBatchV",Array.CreateInstance(v,24));Set(d,"CubeBaseColor",0x00384F62u);
        Bind(d,"drawUp","Draw");Bind(d,"setTex","Texture");Bind(d,"setTSS","Three");Bind(d,"setRS","Two");
        return d;
    }
    static Cube Renderer(object d){return (Cube)Delegate.CreateDelegate(typeof(Cube),d,d.GetType().GetMethod("DrawCube",B));}
    static byte[] Render(Cube draw,object[] a)
    {
        sent=null;calls=0;
        draw((IntPtr)a[0],(float)a[1],(float)a[2],(float)a[3],(float)a[4],(float)a[5],(float)a[6],(double)a[7],(float)a[8],(double)a[9]);
        Check(calls<=1,"como maximo un envio por cubo");return sent;
    }
    static void Compare(Cube current,Cube previous,object[] args)
    {
        byte[] a=Render(current,args),b=Render(previous,args);
        Check((a==null)==(b==null),"ambas versiones omiten el mismo cubo");if(a==null)return;
        Check(a.Length==b.Length,"mismo numero de vertices");
        for(int i=0;i<a.Length;i++)if(a[i]!=b[i])throw new Exception("Cubo distinto byte="+i);
        bytes+=a.Length;checks++;
    }
    static object[] Args(int slot,int frame,int w,int h)
    {return new object[]{new IntPtr(1),.75f,.875f,13f+slot*190,27f,w*.17f,h*.25f,(frame/2)*.017+slot*.4,.05f+(frame%20)/20f,(frame/2%9-4)*.04};}
    static void Regression(Type old)
    {
        object d=Device(device),p=old==null?null:Device(old);Cube draw=Renderer(d),previous=p==null?null:Renderer(p);capture=true;
        try{
            if(previous!=null){
                foreach(int[] size in new int[][]{new int[]{1024,768},new int[]{1280,1024},new int[]{1920,1080}})
                for(int frame=0;frame<128;frame++)for(int slot=0;slot<4;slot++){
                    object[] a=Args(slot,frame,size[0],size[1]);
                    Compare(draw,previous,a);a[8]=1f-(float)a[8];a[0]=new IntPtr(2);Compare(draw,previous,a);
                }
                object[] key=Args(0,0,1024,768);
                for(int pass=0;pass<3;pass++)for(int change=0;change<12;change++){
                    switch(change){
                        case 0:key[0]=new IntPtr(pass%2);break;
                        case 1:key[1]=.73f+pass*.01f;break;case 2:key[2]=.81f+pass*.01f;break;
                        case 3:key[3]=15f+pass;break;case 4:key[4]=28f+pass;break;
                        case 5:key[5]=160f+pass;break;case 6:key[6]=195f+pass;break;
                        case 7:key[7]=.122+pass;break;case 8:key[9]=.065+pass*.1;break;
                        case 9:Set(d,"CubeBaseColor",0x008C379Au+(uint)pass);Set(p,"CubeBaseColor",0x008C379Au+(uint)pass);key[0]=IntPtr.Zero;break;
                        case 10:key[8]=0f;break;case 11:key[8]=1.3f;break;
                    }
                    Compare(draw,previous,key);
                }
                Console.WriteLine("COMPARACION: 3072 dibujos a tres resoluciones y 36 cambios de claves/visibilidad identicos al anterior.");
            }else Console.WriteLine("Comparacion contra release anterior OMITIDA: falta segundo ejecutable.");
            // Cuatro poses distintas caben, aunque cambien las opacidades y las texturas.
            for(int i=0;i<4;i++)Render(draw,Args(i,0,1024,768));
            int made=(int)Get(d,"CubosCalculados"),hits=(int)Get(d,"CubosReusados");
            for(int f=0;f<20;f++)for(int i=0;i<4;i++){
                object[] a=Args(i,0,1024,768);a[8]=.1f+f*.04f;a[0]=new IntPtr(1+f%2);Render(draw,a);
            }
            Check((int)Get(d,"CubosCalculados")==made && (int)Get(d,"CubosReusados")==hits+80,"80 reutilizaciones sin congelar alfa/textura");
            Check((IntPtr)Get(d,"stTex")==new IntPtr(2),"la ultima textura llega al estado de dibujo aun reutilizando pose");
            Array cache=(Array)Get(d,"cuboPoses");Check(cache.Length==4,"cache limitada a cuatro poses");
            object[] identity=new object[4];for(int i=0;i<4;i++)identity[i]=cache.GetValue(i);
            for(int i=0;i<100;i++)Render(draw,Args(i%7,i*2,1024,768));
            for(int i=0;i<4;i++)Check(ReferenceEquals(identity[i],cache.GetValue(i)),"recicla las cuatro entradas");
            Set(d,"lost",true);Check(Render(draw,Args(0,0,1024,768))==null,"dispositivo perdido no dibuja");
            Set(d,"lost",false);Check(Render(draw,Args(0,0,1024,768))!=null,"recuperar dispositivo restaura dibujo");
        }finally{Set(d,"dev",IntPtr.Zero);if(p!=null)Set(p,"dev",IntPtr.Zero);}
    }
    static string N(double v){return v.ToString("F3",CultureInfo.InvariantCulture);}
    static void Measure()
    {
        object d=Device(device);Cube draw=Renderer(d);capture=false;
        Console.WriteLine("Tres cubos por cuadro; 31 lotes de 1000 cuadros sinteticos por delegado. Sin GPU, audio ni espera de monitor.");
        foreach(int fps in new int[]{60,30,24})foreach(bool fade in new bool[]{false,true}){
            double[] times=new double[31];int startCalls=calls;
            for(int batch=-3;batch<times.Length;batch++){
                long start=Stopwatch.GetTimestamp();
                for(int frame=0;frame<1000;frame++)for(int slot=0;slot<3;slot++){
                    int tick=frame*fps/60;double angle=tick*.017+slot*.4,tilt=(tick%9-4)*.04;
                    draw(new IntPtr(1),.75f,.875f,13f+slot*190,27f,173f,194f,angle,.7f,tilt);
                    if(fade && slot==0)draw(new IntPtr(2),.75f,.875f,13f+slot*190,27f,173f,194f,angle,.3f,tilt);
                }
                double us=(Stopwatch.GetTimestamp()-start)*1000000.0/Stopwatch.Frequency/1000;
                if(batch>=0)times[batch]=us;
            }
            Array.Sort(times);
            Console.WriteLine("cubos_fps="+fps+" fundido="+fade+" mediana_us="+N(times[15])+" p95_lote_us="+N(times[29])+" max_lote_us="+N(times[30])+" envios="+(calls-startCalls));
        }
        Set(d,"dev",IntPtr.Zero);
    }
    static void TextOffsets(Type oldPanel)
    {
        Type runType=device.Assembly.GetType("Rocola.CintaTexto");
        Type panel=device.Assembly.GetType("Rocola.GpuPanel");
        object run=Activator.CreateInstance(runType,true);
        Func<int,float,float> offset=(Func<int,float,float>)Delegate.CreateDelegate(typeof(Func<int,float,float>),run,runType.GetMethod("Desplazamiento",B));
        Func<double,float,float> original=(Func<double,float,float>)Delegate.CreateDelegate(typeof(Func<double,float,float>),(oldPanel??panel).GetMethod("CorreTitulo",B));
        foreach(int born in new int[]{0,Int32.MaxValue-20,Int32.MinValue+20,-10000})
        foreach(int width in new int[]{0,100,1024,4096})
        foreach(float visible in new float[]{0f,100f,1024f,2048f})
        for(int i=0;i<100;i++){
            int now=unchecked(born+i*371);Set(run,"Born",born);Set(run,"Width",width);
            float expected=original((uint)(now-born),Math.Max(0,width-visible));
            for(int repeat=0;repeat<4;repeat++)Check(BitConverter.ToInt32(BitConverter.GetBytes(offset(now,visible)),0)==BitConverter.ToInt32(BitConverter.GetBytes(expected),0),"desplazamiento exacto, incluso reloj envuelto");
        }
        // Cambiar ancho o nacimiento con el MISMO tick tambien debe invalidar.
        for(int i=0;i<100;i++){
            Set(run,"Born",i*21);Set(run,"Width",3000+i);
            Check(offset(12000,100+i)==original((uint)(12000-i*21),Math.Max(0,3000+i-(100+i))),"nacimiento/ancho vivos con mismo tick");
            Check(offset(12000,500+i)==original((uint)(12000-i*21),Math.Max(0,3000+i-(500+i))),"ancho visible vivo con mismo tick");
        }
        Console.WriteLine("TEXTOS: 25800 desplazamientos identicos, cuatro partes por titulo y cambios de tiempo/ancho/nacimiento.");
    }
    static void TextMeasure()
    {
        Type panel=device.Assembly.GetType("Rocola.GpuPanel"),runType=device.Assembly.GetType("Rocola.CintaTexto");
        object run=Activator.CreateInstance(runType,true);Set(run,"Born",0);Set(run,"Width",4096);
        MethodInfo m=runType.GetMethod("Desplazamiento",B);
        Func<int,float,float> cached=m==null?null:(Func<int,float,float>)Delegate.CreateDelegate(typeof(Func<int,float,float>),run,m);
        Func<double,float,float> old=(Func<double,float,float>)Delegate.CreateDelegate(typeof(Func<double,float,float>),panel.GetMethod("CorreTitulo",B));
        // Solo los titulos divididos usan la cache; una sola textura conserva la formula directa.
        foreach(int parts in new int[]{4}){
            double[] times=new double[31];float sum=0;
            for(int batch=-3;batch<31;batch++){
                long start=Stopwatch.GetTimestamp();
                if(cached==null)for(int i=0;i<1000;i++)for(int p=0;p<parts;p++)sum+=old(i*17,3996);
                else for(int i=0;i<1000;i++)for(int p=0;p<parts;p++)sum+=cached(i*17,100f);
                double us=(Stopwatch.GetTimestamp()-start)*1000000.0/Stopwatch.Frequency/1000;
                if(batch>=0)times[batch]=us;
            }
            Array.Sort(times);Console.WriteLine("texto_partes="+parts+" mediana_us="+N(times[15])+" p95_lote_us="+N(times[29])+" max_lote_us="+N(times[30])+" checksum="+sum);
        }
    }
    static int Main(string[] args)
    {
        try{
            Assembly app=Assembly.LoadFrom(args[0]);device=app.GetType("Rocola.D3D9Device");vertex=app.GetType("Rocola.D3D9+Vtx");
            Check(Marshal.SizeOf(vertex)==28,"formato de vertices conservado");
            if(args.Length>1 && args[1]=="medir"){Measure();TextMeasure();return 0;}
            Type old=args.Length>1?Assembly.LoadFile(System.IO.Path.GetFullPath(args[1])).GetType("Rocola.D3D9Device"):null;
            if(old!=null)Check(old.Assembly!=app,"referencia independiente");
            Regression(old);TextOffsets(old==null?null:old.Assembly.GetType("Rocola.GpuPanel"));Console.WriteLine("OK "+checks+" comprobaciones, "+bytes+" bytes comparados.");return 0;
        }catch(Exception e){Console.WriteLine("FALLO "+e);return 1;}
    }
}
