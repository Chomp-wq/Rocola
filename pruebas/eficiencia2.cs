using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;

class Eficiencia2
{
    const BindingFlags B=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static Assembly A; static Type F; static int checks,huellas;
    static object Get(object o,string n) { return (o==null?F:o.GetType()).GetField(n,B).GetValue(o); }
    static void Set(object o,string n,object v) { (o==null?F:o.GetType()).GetField(n,B).SetValue(o,v); }
    static object Call(object o,string n,params object[] p)
    { try { return (o==null?F:o.GetType()).GetMethod(n,B).Invoke(o,p); } catch(TargetInvocationException e) { throw e.InnerException; } }
    static void Check(bool ok,string s) { if(!ok)throw new Exception(s); checks++;Console.WriteLine("OK "+s); }
    static bool vigente=true;
    static bool cancelaFila, fallaFila;
    static long Hash(string key,string value) { huellas++;return value.GetHashCode(); }
    static void Fila(TextWriter w,string key,string value)
    { if(fallaFila)throw new IOException("fallo de prueba");if(cancelaFila)vigente=false;w.Write(key+"|"+value+"\r\n"); }
    static void Diario(string dir)
    {
        Type t=F.GetNestedType("DiarioTabla",B);
        object d=Activator.CreateInstance(t,true);
        MethodInfo m=t.GetMethod("GuardaCambios",B);
        Check(m!=null,"existe guardado por cambios sin instantanea completa en cada escritura");
        m=m.MakeGenericMethod(typeof(string));
        Type ht=F.GetNestedType("HuellaDe`1",B).MakeGenericType(typeof(string)),wt=F.GetNestedType("FilaDe`1",B).MakeGenericType(typeof(string));
        Delegate h=Delegate.CreateDelegate(ht,typeof(Eficiencia2).GetMethod("Hash",B)),w=Delegate.CreateDelegate(wt,typeof(Eficiencia2).GetMethod("Fila",B));
        Dictionary<string,string> tabla=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        List<KeyValuePair<string,string>> cambios=new List<KeyValuePair<string,string>>();
        string path=Path.Combine(dir,"delta.txt");
        Func<bool> v=delegate {return vigente;};
        Func<bool> save=delegate {return (bool)m.Invoke(d,new object[]{path,tabla,cambios,h,w,"PRUEBA v1",v});};
        for(int i=0;i<5000;i++)tabla["ruta-"+i]="dato-"+i;
        Check(save() && File.ReadAllLines(path).Length==5001,"primera escritura conserva toda la tabla");
        long antes=new FileInfo(path).Length;
        tabla["ruta-2"]="cambiada";tabla["ruta-3"]="otra";tabla["nueva"]="n";
        cambios.Add(new KeyValuePair<string,string>("ruta-2",tabla["ruta-2"]));
        cambios.Add(new KeyValuePair<string,string>("ruta-3",tabla["ruta-3"]));
        cambios.Add(new KeyValuePair<string,string>("nueva","n"));
        huellas=0;
        Check(save() && huellas<=6,"tres cambios en 5001 entradas no recalculan las otras huellas");
        Console.WriteLine("Delta: "+huellas+" huellas para tres cambios de 5001 registros.");
        Check((int)Get(d,"Anadidos")==1 && new FileInfo(path).Length-antes<100,"solo se anaden los tres registros");
        antes=new FileInfo(path).Length;huellas=0;
        Check(save() && huellas==3 && new FileInfo(path).Length==antes,"reintento identico no duplica registros");
        vigente=false;huellas=0;
        Check(!save() && huellas==0 && new FileInfo(path).Length==antes,"cancelacion no consume ni escribe cambios");
        vigente=true;
        cambios.Clear();tabla.Remove("ruta-4");cambios.Add(new KeyValuePair<string,string>("ruta-4",null));
        Check(save() && (int)Get(d,"Enteros")==2 && !File.ReadAllText(path).Contains("ruta-4|"),"baja compacta sin resucitar la entrada borrada");
        cambios.Clear();
        string texto=File.ReadAllText(path);File.WriteAllText(path,texto.Replace("cambiada","cambiadb"));
        File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddSeconds(5));
        Check(save() && (int)Get(d,"Enteros")==3 && File.ReadAllText(path).Contains("cambiada"),"restauracion externa del mismo largo reconstruye desde la tabla propia");
        Set(d,"filasArchivo",tabla.Count+tabla.Count/2+1000);
        tabla["ruta-3"]="tercera";cambios.Add(new KeyValuePair<string,string>("ruta-3","tercera"));
        Check(save() && (int)Get(d,"Enteros")==4 && File.ReadAllLines(path).Length==tabla.Count+1,"umbral de repetidas conserva compactacion segura");
        cambios.Clear();tabla["ruta-3"]="cuarta";cambios.Add(new KeyValuePair<string,string>("ruta-3","cuarta"));
        antes=new FileInfo(path).Length;cancelaFila=true;
        Check(!save() && new FileInfo(path).Length==antes,"cancelacion despues de formatear no escribe un lote obsoleto");
        cancelaFila=false;vigente=true;
        Check(save() && File.ReadAllText(path).Contains("ruta-3|cuarta"),"reintentar el lote cancelado no pierde los pendientes");
        cambios.Clear();tabla["ruta-3"]="quinta";cambios.Add(new KeyValuePair<string,string>("ruta-3","quinta"));
        antes=new FileInfo(path).Length;fallaFila=true;bool error=false;
        try {save();}catch(TargetInvocationException e){error=e.InnerException is IOException;}finally{fallaFila=false;}
        Check(error && new FileInfo(path).Length==antes,"fallo al preparar una escritura conserva el archivo anterior");
        Check(save() && File.ReadAllText(path).Contains("ruta-3|quinta") && File.ReadAllLines(path).Length==tabla.Count+1,"tras fallo reconstruye sin perder ni duplicar medidas");
    }
    static bool Wait(Func<bool> condition,int ms)
    { Stopwatch sw=Stopwatch.StartNew();while(!condition() && sw.ElapsedMilliseconds<ms)Thread.Sleep(10);return condition(); }
    static bool Same(Bitmap a,Bitmap b)
    { if(a.Size!=b.Size)return false;for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y).ToArgb()!=b.GetPixel(x,y).ToArgb())return false;return true; }
    static void Portada(string dir)
    {
        object data=Get(null,"carpetaDatos"),oldThumb=Get(null,"thumbDir");
        Set(null,"carpetaDatos",dir);Set(null,"thumbDir",null);
        object f=Codigo.FormSinEncender(F);typeof(Form).GetConstructor(Type.EmptyTypes).Invoke(f,new object[0]);Set(f,"gpuEnabled",true);
        string path=Path.Combine(dir,"cover.png"),baseline=Path.Combine(dir,"original.jpg");
        using(Bitmap b=new Bitmap(600,600))using(Graphics g=Graphics.FromImage(b))
        {g.Clear(Color.MidnightBlue);g.FillEllipse(Brushes.HotPink,30,90,420,390);b.Save(path,ImageFormat.Png);}
        try
        {
            string cache=(string)Call(null,"ThumbCachePath",path);
            object[] p={path,cache,null};
            using(Bitmap imagen=(Bitmap)Call(null,"DecodeCoverForPublication",p))
            using(Bitmap pendiente=(Bitmap)p[2])
            using(Bitmap original=(Bitmap)Call(null,"DecodeCoverWithIdentity",path,baseline))
            {
                Check(imagen!=null && pendiente!=null && !object.ReferenceEquals(imagen,pendiente),"guardado diferido tiene bitmap privado");
                Check(Same(imagen,original) && Same(imagen,pendiente),"publicar antes no cambia un solo pixel ni el filtro");
                Check(!File.Exists(cache),"decodificar para publicar no espera ni escribe la cache");
            }
            object puerta=Call(null,"CandadoMiniatura",cache),coverLock=Get(f,"coverLock");
            Bitmap publicada=null;
            Monitor.Enter(puerta);
            try
            {
                Call(f,"RequestCoverByPath",path,true);
                bool lista=Wait(delegate {lock(coverLock){publicada=((IDictionary)Get(f,"coverCache"))[path] as Bitmap;return publicada!=null;}},5000);
                Check(lista && !File.Exists(cache),"la portada llega a memoria aunque el guardado de miniatura este bloqueado");
                if(publicada!=null)publicada.Dispose(); // probar que el escritor NO utiliza esta imagen prestada
            }
            finally {Monitor.Exit(puerta);}
            Check(Wait(delegate {lock(coverLock)return (int)Get(f,"coverPumpCount")==0;},5000),"trabajador termina y libera el turno de unidad");
            Check(File.Exists(cache),"se guarda la copia aunque la portada publicada haya sido liberada");
            byte[] x=File.ReadAllBytes(cache),y=File.ReadAllBytes(baseline);bool igual=x.Length==y.Length;
            for(int i=0;i<x.Length && igual;i++)igual=x[i]==y[i];
            Check(igual,"JPEG diferido identico byte a byte al guardado anterior");
            object[] hit={path,cache,null};
            using(Bitmap b=(Bitmap)Call(null,"DecodeCoverForPublication",hit))
                Check(b!=null && hit[2]==null,"cache existente no reserva una copia para volver a guardarla");
            string rota=Path.Combine(dir,"no-es-carpeta"),otra=Path.Combine(dir,"otra.png");
            File.WriteAllText(rota,"prueba");File.Copy(path,otra);Set(null,"thumbDir",rota);
            Call(f,"RequestCoverByPath",otra,true);
            Check(Wait(delegate {lock(coverLock)return ((IDictionary)Get(f,"coverCache")).Contains(otra) && (int)Get(f,"coverPumpCount")==0;},5000),
                "fallo de guardado no impide publicar otra portada ni liberar el trabajador");
            lock(coverLock)((Bitmap)((IDictionary)Get(f,"coverCache"))[otra]).Dispose();
        }
        finally
        {
            Wait(delegate {lock(Get(f,"coverLock"))return (int)Get(f,"coverPumpCount")==0;},5000);
            Set(null,"carpetaDatos",data);Set(null,"thumbDir",oldThumb);
        }
    }
    delegate IntPtr Fijar(object d,Array a,out GCHandle temporal);
    static int FinFallido(IntPtr p) { return -1; }
    static void Pines()
    {
        Type t=A.GetType("Rocola.D3D9Device",true),vt=A.GetType("Rocola.D3D9+Vtx",true);
        MethodInfo m=t.GetMethod("FijaVertices",B);Check(m!=null,"reutilizacion de puntero por escena");
        DynamicMethod dm=new DynamicMethod("captura",typeof(IntPtr),new Type[]{typeof(object),typeof(Array),typeof(GCHandle).MakeByRefType()},typeof(Eficiencia2).Module,true);
        ILGenerator il=dm.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Castclass,t);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Castclass,vt.MakeArrayType());il.Emit(OpCodes.Ldarg_2);il.Emit(OpCodes.Call,m);il.Emit(OpCodes.Ret);
        Fijar pin=(Fijar)dm.CreateDelegate(typeof(Fijar));
        object d=FormatterServices.GetUninitializedObject(t);Array a=Array.CreateInstance(vt,360),b=Array.CreateInstance(vt,360);GCHandle tmp;
        Set(d,"inScene",true);IntPtr uno=pin(d,a,out tmp);Check(!tmp.IsAllocated,"el puntero pertenece a la escena");
        GCHandle h=(GCHandle)Get(d,"pinVertices");
        Check(pin(d,a,out tmp)==uno && GCHandle.ToIntPtr(h)==GCHandle.ToIntPtr((GCHandle)Get(d,"pinVertices")),"dibujos repetidos reutilizan exactamente el mismo pin");
        pin(d,b,out tmp);Check(object.ReferenceEquals(Get(d,"verticesFijados"),b),"cambiar array sustituye el pin, no guarda un historial");
        Call(d,"SueltaPinesVertices");Check(Get(d,"verticesFijados")==null && (long)Get(d,"pinesBytes")==0 && !((GCHandle)Get(d,"pinVertices")).IsAllocated,"fin de escena libera referencias y pin");
        Array grande=Array.CreateInstance(vt,230000);pin(d,grande,out tmp);
        Check(tmp.IsAllocated && Get(d,"verticesFijados")==null,"array mayor del presupuesto conserva pin temporal");
        tmp.Free();
        Set(d,"inScene",false);pin(d,a,out tmp);Check(tmp.IsAllocated && Get(d,"verticesFijados")==null,"fuera de escena conserva el camino anterior");tmp.Free();
        Set(d,"inScene",true);pin(d,a,out tmp);Call(d,"Dispose");
        Check(Get(d,"verticesFijados")==null && !((GCHandle)Get(d,"pinVertices")).IsAllocated,"Dispose tambien libera una escena abandonada");
        Set(d,"inScene",true);pin(d,a,out tmp);Set(d,"dev",new IntPtr(1));
        Type fin=A.GetType("Rocola.D3D9+HrSelf",true);
        Set(d,"end",Delegate.CreateDelegate(fin,typeof(Eficiencia2).GetMethod("FinFallido",B)));
        try
        {
            Check(!(bool)Call(d,"EndFrame"),"fallo de EndScene rechaza el cuadro");
            Check(Get(d,"verticesFijados")==null && !((GCHandle)Get(d,"pinVertices")).IsAllocated,"EndFrame fallido tambien libera el pin");
        }
        finally {Set(d,"dev",IntPtr.Zero);Call(d,"SueltaPinesVertices");}
        // Cronometrar solo gestion de punteros, no COM/reflexion ni FPS.
        for(int mode=0;mode<3;mode++)
        {
            Set(d,"inScene",mode!=0);
            Stopwatch sw=Stopwatch.StartNew();
            for(int i=0;i<500000;i++){pin(d,mode==2 && (i&1)!=0?b:a,out tmp);if(tmp.IsAllocated)tmp.Free();}
            Console.WriteLine("Punteros "+(mode==0?"temporales":mode==1?"reutilizados":"alternados sin reutilizar")+": "+sw.Elapsed.TotalMilliseconds.ToString("F2")+" ms / 500000; no son FPS.");
            Call(d,"SueltaPinesVertices");
        }
    }
    [STAThread] static int Main(string[] args)
    {
        string dir=Path.Combine(Path.GetTempPath(),"rocola-eficiencia2-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try
        {
            if(args.Length>1 && args[1]=="--dos-hilos")Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(3);
            A=Assembly.LoadFrom(args[0]);F=A.GetType("Rocola.Form1",true);
            Diario(dir);Portada(dir);Pines();Console.WriteLine(checks+" comprobaciones de eficiencia2.");return 0;
        }
        catch(Exception e){Console.WriteLine("FALLO "+e);return 1;}
        finally{Directory.Delete(dir,true);}
    }
}
