using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Windows.Forms;

class EspacioNaturalTest
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Assembly app, baseline;
    static int checks;
    static void Check(bool ok, string text) { checks++; if (!ok) throw new Exception(text); }
    static object Call(object instance, string method, params object[] args) { return instance.GetType().GetMethod(method, B).Invoke(instance, args); }
    static object Run(Assembly a, string method, params object[] args) { return a.GetType("Rocola.GpuPanel", true).GetMethod(method, B).Invoke(null, args); }
    static void Set(object instance, string field, object value) { instance.GetType().GetField(field, B).SetValue(instance, value); }
    static object Get(object instance, string field) { return instance.GetType().GetField(field, B).GetValue(instance); }
    static ulong Hash(Bitmap b)
    {
        BitmapData d = b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try {
            int[] row = new int[b.Width]; ulong value = 14695981039346656037UL;
            for (int y=0;y<b.Height;y++) {
                Marshal.Copy(new IntPtr(d.Scan0.ToInt64()+y*(long)d.Stride),row,0,row.Length);
                foreach(int p in row) { value ^= unchecked((uint)p); value *= 1099511628211UL; }
            }
            return value;
        } finally { b.UnlockBits(d); }
    }
    static Array Mesh(Assembly a, string method, float width, float baseY, float height, bool glow)
    {
        int count=glow?7992:8640; Array vertices=Array.CreateInstance(a.GetType("Rocola.D3D9+Vtx",true),count);
        float[] h=new float[145],alpha=new float[145],rays=new float[145];
        uint[] curtain=new uint[7*145],ground=new uint[5*145],colors=new uint[145];
        for(int i=0;i<145;i++) {
            h[i]=(float)(80+Math.Sin(i*.11)*40); alpha[i]=.2f+i*.002f; rays[i]=.7f+i*.003f;
            colors[i]=(uint)(i*733)&0xFFFFFF;
            for(int j=0;j<7;j++)curtain[j*145+i]=(uint)(0x27499B+i*421+j*1025)&0xFFFFFF;
            for(int j=0;j<5;j++)ground[j*145+i]=(uint)(0xC04195+i*51+j*2025)&0xFFFFFF;
            ground[i]=curtain[i]; // el pie compartido usa la misma muestra de paleta
        }
        object[] args=glow ? new object[]{vertices,h,colors,alpha,colors,alpha,width,baseY} :
            new object[]{vertices,h,curtain,ground,alpha,rays,width,baseY,height};
        Check((int)Run(a,method,args)==count,method+": presupuesto de vertices cambia");
        return vertices;
    }
    static void Geometry()
    {
        foreach(int width in new int[]{640,1024,1920,3440}) foreach(float baseY in new float[]{300,560,1200}) {
            Array old=Mesh(baseline,"AurMalla",width,baseY,1440,false);
            Array current=Mesh(app,"AurMalla",width,baseY,1440,false);
            Equal(old,current,"AurMalla Legacy");
            old=Mesh(baseline,"AurBrillo",width,baseY,1440,true);
            current=Mesh(app,"AurBrillo",width,baseY,1440,true);
            Equal(old,current,"AurBrillo Legacy");
            foreach(bool glow in new bool[]{false,true}) {
                current=Mesh(app,glow?"AurBrilloNatural":"AurMallaNatural",width,baseY,1440,glow);
                foreach(object v in current) foreach(string field in new string[]{"X","Y","Z","Rhw","U","V"}) {
                    float f=(float)Get(v,field); Check(!float.IsNaN(f)&&!float.IsInfinity(f),"Vertice natural no finito");
                }
                if(!glow) {
                    // Seis filas y cuatro de suelo comparten exactamente el horizonte.
                    float y=(float)Get(current.GetValue(0),"Y");
                    Check(y==baseY-.5f,"Suelo desplazado del horizonte");
                    Check(Get(current.GetValue(0),"Color").Equals(Get(current.GetValue(4*144*6+2),"Color")),"Salto de color/alfa suelo-cortina");
                }
            }
        }
        for(int i=0;i<=1000;i++) {
            float f=i/1000f, ray=(float)Run(app,"RayoNatural",.64f+f*.72f);
            Check(ray>=.28f&&ray<=1.72f,"Veta natural fuera de rango");
            float fall=(float)Run(app,"BajaNatural",f);
            Check(fall>=0&&fall<=1,"Desvanecido natural fuera de rango");
        }
        Check((float)Run(app,"RayoNatural",float.NaN)==.28f,"NaN en veta");
        Check((float)Run(app,"BajaNatural",float.NaN)==1,"NaN en desvanecido");
        foreach(uint color in new uint[]{0xFF000000,0xFFFFFFFF,0xFFF02AB0,0xFF195BE2})for(int layer=0;layer<3;layer++) {
            Check((uint)Run(app,"CieloEstilo",color,layer,false)==color,"Legacy altera cielo");
            uint sky=(uint)Run(app,"CieloEstilo",color,layer,true);
            Check((sky&0xFF000000)==(color&0xFF000000),"Cielo cambia alfa");
            Check((sky&255)>=((sky>>16)&255),"Paleta musical pinta de rojo el cielo alto");
        }
    }
    static void Equal(Array old,Array current,string label)
    {
        string[] fields={"X","Y","Z","Rhw","Color","U","V"};
        for(int i=0;i<old.Length;i++)foreach(string field in fields)
            Check(Get(old.GetValue(i),field).Equals(Get(current.GetValue(i),field)),label+" difiere en "+i+"/"+field);
    }
    static void Cache()
    {
        Type f=app.GetType("Rocola.Form1",true); FieldInfo strength=f.GetField("nebNaturalFuerza",B); object previous=strength.GetValue(null);
        FieldInfo oldStrength=baseline.GetType("Rocola.Form1",true).GetField("nebNaturalFuerza",B);
        object oldPrevious=oldStrength.GetValue(null);
        try {
            strength.SetValue(null,1f);
            oldStrength.SetValue(null,1f);
            using(Control panel=(Control)Activator.CreateInstance(app.GetType("Rocola.GpuPanel",true),true)) {
                Check(!(bool)Get(panel,"FondoNatural"),"Nuevo panel no inicia Legacy");
                foreach(bool detail in new bool[]{true,false}) {
                    Set(panel,"CieloProfundo",detail); Set(panel,"FondoNatural",false);
                    Bitmap legacy=(Bitmap)Call(panel,"MapaNebEstilo",64,32);
                    using(Bitmap expected=(Bitmap)(detail?Run(baseline,"BakeNatural",64,32):
                        baseline.GetType("Rocola.GpuStarfield",true).GetMethod("BakeNebulosas",B).Invoke(null,new object[]{64,32})))
                        Check(Hash(legacy)==Hash(expected),"Mapa Legacy cacheado cambia pixels");
                    Set(panel,"FondoNatural",true);
                    Bitmap natural=(Bitmap)Call(panel,"MapaNebEstilo",64,32);
                    Check(!ReferenceEquals(legacy,natural)&&Hash(legacy)!=Hash(natural),"Selector no cambia mapa");
                    Check(ReferenceEquals(natural,Call(panel,"MapaNebEstilo",64,32)),"Rehornea mapa natural");
                    Set(panel,"FondoNatural",false);
                    Check(ReferenceEquals(legacy,Call(panel,"MapaNebEstilo",64,32)),"Alternar rehornea Legacy");
                }
                Set(panel,"FondoNatural",true);
                Bitmap via=(Bitmap)Call(panel,"MapaViaEstilo",64,32);
                Check(ReferenceEquals(via,Call(panel,"MapaViaEstilo",64,32)),"Rehornea Via Lactea");
                Bitmap map=(Bitmap)Call(panel,"MapaNebEstilo",64,32);
                strength.SetValue(null,.5f);
                Check(!ReferenceEquals(map,Call(panel,"MapaNebEstilo",64,32)),"Fuerza no invalida cache");
                map=(Bitmap)Call(panel,"MapaNebEstilo",64,32);
                Check(!ReferenceEquals(map,Call(panel,"MapaNebEstilo",96,48)),"Tamano no invalida cache");
                Check(!ReferenceEquals(via,Call(panel,"MapaViaEstilo",96,48)),"Tamano no invalida Via Lactea");
                Call(panel,"LiberaMapasEstilo");
                foreach(Bitmap b in (Bitmap[])Get(panel,"mapasNebEstilo")) Check(b==null,"Cierre deja nebulosa");
                foreach(Bitmap b in (Bitmap[])Get(panel,"mapasViaEstilo")) Check(b==null,"Cierre deja Via Lactea");
                // Cambiar estilo fuerza muestra aunque no haya vencido el reloj 24 FPS.
                panel.Size=new Size(1024,768); Set(panel,"OndasFps",24);
                object wave=Activator.CreateInstance(app.GetType("Rocola.GpuAurora",true));
                Set(wave,"Has",true); Set(wave,"B",new float[8]); Set(wave,"BaseY",560f);
                object[] sample={wave,0.0}; Call(panel,"SampleWaveAt",sample);
                Set(panel,"FondoNatural",false); sample[0]=wave; sample[1]=1.0;
                Check((bool)Call(panel,"SampleWaveAt",sample),"Estilo usa muestras viejas durante cambio");
            }
        } finally { oldStrength.SetValue(null,oldPrevious); strength.SetValue(null,previous); }
    }
    [STAThread] static int Main(string[] args)
    {
        try {
            app=Assembly.LoadFile(System.IO.Path.GetFullPath(args[0]));
            baseline=args.Length>1?Assembly.LoadFile(System.IO.Path.GetFullPath(args[1])):app;
            Geometry(); Cache(); Console.WriteLine("PASS "+checks+" comprobaciones: aurora natural, cache acotada y cadencia. Comparacion externa Legacy: "+(args.Length>1));return 0;
        } catch(Exception e) { Console.WriteLine(e); return 1; }
    }
}
