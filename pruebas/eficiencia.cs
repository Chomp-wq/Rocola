using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

class Eficiencia
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type F; static Assembly A; static int checks;
    static object Get(object o, string n) { return (o == null ? F : o.GetType()).GetField(n,B).GetValue(o); }
    static void Set(object o, string n, object v) { (o == null ? F : o.GetType()).GetField(n,B).SetValue(o,v); }
    static object Call(object o, string n, params object[] p)
    { try { return (o == null ? F : o.GetType()).GetMethod(n,B).Invoke(o,p); } catch(TargetInvocationException e) { throw e.InnerException; } }
    static void Check(bool ok, string s) { if(!ok) throw new Exception(s); checks++; Console.WriteLine("OK "+s); }
    static void Plan()
    {
        object f=Codigo.FormSinEncender(F); typeof(Form).GetConstructor(Type.EmptyTypes).Invoke(f,new object[0]);
        ((IDictionary)Get(f,"eqCache"))["x"]=new float[12];
        ((IDictionary)Get(f,"loudCache"))["x"]=.2f;
        ((IDictionary)Get(f,"durCache"))["x"]=20f;
        object p=Call(f,"CapturaParaBiblioteca",false);
        Check(Get(p,"EqDatos")==null && Get(p,"IntegDatos")==null && Get(p,"CancionesDatos")==null,"plan de duracion no copia tablas ajenas");
        Call(p,"Prepara");
        Check(((IDictionary)Get(p,"Dur")).Count==1 && ((IDictionary)Get(p,"Loud")).Count==0,"duraciones completas con indices vacios seguros");
        ((IDisposable)p).Dispose();
        object puerta=Get(f,"audioDatosLock"); bool listo=false;
        Monitor.Enter(puerta);
        Thread ui=new Thread(new ThreadStart(delegate { Call(f,"ConsumeLevels",Environment.TickCount); listo=true; }));
        try { ui.Start(); Check(ui.Join(1000) && listo,"la pantalla consume niveles mientras la puerta de calculo de audio esta ocupada"); }
        finally { Monitor.Exit(puerta); }
        Check(!object.ReferenceEquals(puerta,Get(f,"levelLock")),"calculo y cola visual usan cerrojos independientes");
        Set(null,"perfilDisco",2);
        object turno=Call(f,"PideTurnoAnalisis","C:\\prueba\\a.mp3",new Func<bool>(delegate{return false;}));
        object siguiente=null;
        using(ManualResetEvent entro=new ManualResetEvent(false))
        {
            Thread t=new Thread(new ThreadStart(delegate { entro.Set(); siguiente=Call(f,"PideTurnoAnalisis","C:\\prueba\\b.mp3",new Func<bool>(delegate{return false;})); }));
            t.Start(); entro.WaitOne(); Thread.Sleep(40);
            Stopwatch sw=Stopwatch.StartNew(); ((IDisposable)turno).Dispose();
            Check(t.Join(1000) && siguiente!=null,"liberar HDD despierta al trabajador por senal");
            Console.WriteLine("Despertar HDD: "+sw.Elapsed.TotalMilliseconds.ToString("F2")+" ms; no mide el disco.");
            ((IDisposable)siguiente).Dispose();
        }
        Set(null,"perfilDisco",0);
    }
    static void Tablas()
    {
        Type t=A.GetType("Rocola.RemuestreoContinuo",true);
        MethodInfo captura=t.GetMethod("CapturaTabla",B);
        NAudio.Wave.WaveFormat destino=NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000,2);
        int[] tasas={44100,32000,22050}; object[] refs=new object[3];
        for(int i=0;i<tasas.Length;i++)
        {
            object[] p={NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(tasas[i],2),destino};
            Stopwatch sw=Stopwatch.StartNew();
            while((refs[i]=captura.Invoke(null,p))==null && sw.ElapsedMilliseconds<10000) Thread.Sleep(10);
            Check(refs[i]!=null,"tabla preparada "+tasas[i]+" -> 48000");
        }
        for(int i=0;i<tasas.Length;i++) Check(object.ReferenceEquals(refs[i],captura.Invoke(null,new object[]{NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(tasas[i],2),destino})),"al alternar se reutilizan los mismos coeficientes "+tasas[i]);
        long bytes=0; foreach(object x in (IEnumerable)t.GetField("tablas",B).GetValue(null)) bytes+=((double[])Get(x,"Datos")).LongLength*8;
        Check(bytes<=6*1024*1024,"cache de conversion acotada a 6 MiB");
    }
    static void Paletas()
    {
        foreach(PixelFormat fmt in new PixelFormat[]{PixelFormat.Format32bppArgb,PixelFormat.Format32bppRgb,PixelFormat.Format24bppRgb,PixelFormat.Format32bppPArgb})
        foreach(int alpha in new int[]{255,117})
        using(Bitmap b=new Bitmap(73,59,fmt))
        {
            Random r=new Random(42);
            for(int y=0;y<b.Height;y++) for(int x=0;x<b.Width;x++) b.SetPixel(x,y,Color.FromArgb(alpha,r.Next(256),r.Next(256),r.Next(256)));
            int[] fast=(int[])Call(null,"MuestrasPaleta",b,32,32);
            if(fmt==PixelFormat.Format32bppPArgb && alpha!=255) Check(fast==null,"PArgb translucido conserva GetPixel y su redondeo");
            else
            {
                bool igual=fast!=null;
                for(int y=0;y<32 && igual;y++) for(int x=0;x<32;x++) if(fast[y*32+x]!=b.GetPixel((2*x+1)*b.Width/64,(2*y+1)*b.Height/64).ToArgb()) igual=false;
                Check(igual,"muestras identicas a GetPixel: "+fmt+" alfa "+alpha);
            }
        }
    }
    static void Fuentes()
    {
        object f=Codigo.FormSinEncender(F);
        foreach(FieldInfo field in F.GetFields(B)) if(!field.IsStatic && field.FieldType.Name=="GuardadoReciente" && field.GetValue(f)==null) field.SetValue(f,Activator.CreateInstance(field.FieldType,true));
        Set(f,"fuentesEstado",1); Set(f,"startupReadPending",false);
        Set(f,"fuentesTrabajo",null); // el fixture inicializa diccionarios que en produccion empiezan null
        object antes=Get(null,"carpetaDatos");
        string dir=Path.Combine(Path.GetTempPath(),"rocola-fuentes-ef-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        Set(null,"carpetaDatos",dir);
        try
        {
            Type rt=F.GetNestedType("RegistroFuente",B), ft=F.GetNestedType("FuenteMedida",B);
            IDictionary tabla=(IDictionary)Get(f,"fuentesMedidas"), pendientes=(IDictionary)Get(f,"fuentesPendientes");
            for(int i=0;i<2000;i++) tabla["cancion-"+i]=Registro(rt,ft,i);
            Set(f,"fuentesCambios",1L); Call(f,"SaveFuentesMedidas"); Codigo.EsperaGuardado(F,f);
            string ruta=Path.Combine(dir,"rocola_fuentes.txt"); long uno=new FileInfo(ruta).Length;
            Check(uno>60000 && File.ReadAllLines(ruta).Length==2001,"primer guardado conserva las 2000 fuentes cargadas");
            object changed=Registro(rt,ft,99999); tabla["cancion-5"]=changed; pendientes["cancion-5"]=changed;
            Set(f,"fuentesCambios",2L); Call(f,"SaveFuentesMedidas"); Codigo.EsperaGuardado(F,f);
            long dos=new FileInfo(ruta).Length;
            Check(dos>uno && dos-uno<200,"una medida anade solo una linea a 2000 fuentes");
            object d=Get(f,"diarioFuentes");
            Check((int)Get(d,"Enteros")==1 && (int)Get(d,"Anadidos")==1 && pendientes.Count==0,"diario y pendientes se actualizan despues de guardar");
            Check(!Codigo.Llama(F.GetMethod("SaveFuentesMedidas",B),"get_Count"),"SaveFuentesMedidas no recorre la biblioteca en la UI");
            Console.WriteLine("Fuentes: primera escritura "+uno+" bytes; actualizacion "+(dos-uno)+" bytes.");
            string texto=File.ReadAllText(ruta);
            File.WriteAllText(ruta,texto.Replace("99999|","99998|"));
            File.SetLastWriteTimeUtc(ruta,DateTime.UtcNow.AddSeconds(5));
            Set(f,"fuentesCambios",3L); Call(f,"SaveFuentesMedidas"); Codigo.EsperaGuardado(F,f);
            Check((int)Get(d,"Enteros")==2,"cambio externo del mismo largo fuerza reconstruccion segura");
            Call(f,"OlvidaMedidas","cancion-9"); Call(f,"SaveFuentesMedidas"); Codigo.EsperaGuardado(F,f);
            Check((int)Get(d,"Enteros")==3 && !File.ReadAllText(ruta).Contains("|cancion-9\r\n"),"borrar una fuente compacta sin resucitar la identidad antigua");
            File.AppendAllText(ruta,"1|1|1|0123456789ABCDEF|-|cortada");
            Call(f,"LoadFuentesMedidas");
            Check(!((IDictionary)Get(f,"fuentesMedidas")).Contains("cortada"),"la ultima linea incompleta no se acepta");
        }
        finally { Set(null,"carpetaDatos",antes); Directory.Delete(dir,true); }
    }
    static void ColorPreparado()
    {
        object f=Codigo.FormSinEncender(F); Set(f,"gpuEnabled",true);
        object album=Activator.CreateInstance(F.GetNestedType("Album",B),true);Set(album,"CoverPath","preparada.jpg");
        using(Bitmap b=new Bitmap(32,32))
        {
            Call(f,"CachePut","preparada.jpg",b);
            object tonos=Activator.CreateInstance(F.GetNestedType("ColoresDisco",B));
            Set(tonos,"Prom",Color.Red);Set(tonos,"Fuente",b);
            ((IDictionary)Get(f,"coloresPreparados"))["preparada.jpg"]=tonos;
            Check(((Color)Call(f,"ColorDePortadaDe",album)).ToArgb()==Color.Red.ToArgb(),"UI usa colores preparados para la imagen exacta");
            object cache=((IDictionary)Get(f,"colorPortada"))["preparada.jpg"];
            Check(Get(cache,"Fuente")==null,"cache de tonos no retiene bitmaps expulsados");
            Call(f,"CachePut","preparada.jpg",b);
            Check(!((IDictionary)Get(f,"coloresPreparados")).Contains("preparada.jpg"),"reemplazar portada invalida sus colores preparados");
            using(Bitmap anterior=new Bitmap(8,8))
            {
                ((IDictionary)Get(f,"colorPortada")).Clear();
                Set(tonos,"Fuente",anterior);
                ((IDictionary)Get(f,"coloresPreparados"))["preparada.jpg"]=tonos;
                Call(f,"ColorDePortadaDe",album);
                cache=((IDictionary)Get(f,"colorPortada"))["preparada.jpg"];
                Check(Get(cache,"Fuente")==null,"preparado de otra imagen tampoco queda retenido al recalcular");
            }
        }
        Check(Codigo.Llama(F.GetMethod("CoverPump",B),"ExtractCoverPalette") && Codigo.Llama(F.GetMethod("CoverPump",B),"ColoresDePortada"),"el trabajador calcula la paleta antes de publicarla");
        IntPtr data=Marshal.AllocHGlobal(17*19*4);
        try
        {
            using(Bitmap b=new Bitmap(17,19,-17*4,PixelFormat.Format32bppArgb,new IntPtr(data.ToInt64()+17*18*4)))
            {
                for(int y=0;y<19;y++)for(int x=0;x<17;x++)b.SetPixel(x,y,Color.FromArgb(255,x*13,y*11,91));
                int[] pixels=(int[])Call(null,"MuestrasPaleta",b,17,19);bool same=pixels!=null;
                for(int y=0;y<19&&same;y++)for(int x=0;x<17;x++)if(pixels[y*17+x]!=b.GetPixel(x,y).ToArgb())same=false;
                Check(same,"lectura de pixeles conserva stride negativo");
            }
        }
        finally { Marshal.FreeHGlobal(data); }
    }
    static object Registro(Type rt, Type ft,long n)
    {
        object r=Activator.CreateInstance(rt,true);
        Set(r,"Fuente",Activator.CreateInstance(ft,B,null,new object[]{n,1L,1L},null));
        Set(r,"Medidas","0123456789ABCDEF"); Set(r,"Mapa","-"); return r;
    }
    [STAThread] static int Main(string[] args)
    {
        if(args.Length>1 && args[1]=="--dos-hilos")
        { Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(3); Console.WriteLine("Afinidad de esta prueba: dos hilos logicos; no es un CPU antiguo real."); }
        try
        {
            A=Assembly.LoadFrom(args[0]); F=A.GetType("Rocola.Form1",true);
            int procesadores=(int)A.GetType("Rocola.CargaTrabajo",true).GetField("Procesadores",B).GetValue(null);
            Check(args.Length>1 && args[1]=="--dos-hilos" ? procesadores==2 : procesadores>0 && procesadores<=Environment.ProcessorCount,
                "el presupuesto respeta los procesadores disponibles, sin imponer dos hilos a todos");
            Plan(); Tablas(); Paletas(); ColorPreparado(); Fuentes();
            Console.WriteLine(checks+" comprobaciones de eficiencia; sin FPS ni hardware antiguo medidos."); return 0;
        }
        catch(Exception e) { Console.WriteLine("FALLO "+e); return 1; }
    }
}
