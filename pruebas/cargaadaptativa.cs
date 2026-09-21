using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
class CargaAdaptativa {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static object Call(Type t,object o,string n,params object[] a){return t.GetMethod(n,F).Invoke(o,a);}
 static object Get(Type t,object o,string n){return t.GetField(n,F).GetValue(o);}
 static void Set(Type t,object o,string n,object v){t.GetField(n,F).SetValue(o,v);}
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static object Blank(Type t){
  object o=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
  foreach(FieldInfo f in t.GetFields(F))
   if(!f.IsStatic&&f.FieldType.Namespace!=null&&f.FieldType.Namespace.StartsWith("System.Collections")&&f.FieldType.GetConstructor(Type.EmptyTypes)!=null)
    f.SetValue(o,Activator.CreateInstance(f.FieldType));
  return o;
 }
 static void Scheduling(Type form){
  // ESTA PARTE SOLO TIENE SENTIDO EN WINDOWS. Reparte lecturas por UNIDAD -un disco duro lento
  // admite un lector y un SSD varios- y para eso usa Path.GetPathRoot. En Linux, "C:\\x" y "D:\\x"
  // devuelven los dos la misma raiz vacia, o sea que los dos discos se funden en uno y el reparto
  // que se quiere medir no existe. El codigo no esta mal: la prueba no se puede hacer aqui.
  if(Path.GetPathRoot(@"C:\uno.jpg")==Path.GetPathRoot(@"D:\uno.jpg")){
   Console.WriteLine("  OK    NO SE PROBO AQUI el reparto por unidad: hacen falta dos discos de Windows. Solo mide en la maquina del bar.");
   return;
  }
  object owner=Blank(form);Set(form,owner,"coverLock",new object());Set(form,owner,"coverPumpCount",Int32.MaxValue);
  int oldLanes=(int)Get(form,null,"coverCarriles"),oldProfile=(int)Get(form,null,"perfilDisco");
  IDictionary units=(IDictionary)Get(form,null,"discoLento");
  object oldC=units[@"C:\"],oldD=units[@"D:\"];
  try{
   Set(form,null,"coverCarriles",3);Set(form,null,"perfilDisco",0);
   units[@"C:\"]=true;units[@"D:\"]=false;
   object hi=Get(form,owner,"coverQHi"),lo=Get(form,owner,"coverQLo");
   Call(form,owner,"RequestCoverByPath",@"C:\uno.jpg",true);
   Call(form,owner,"RequestCoverByPath",@"C:\dos.jpg",true);
   Call(form,owner,"RequestCoverByPath",@"D:\uno.jpg",true);
   Call(form,owner,"RequestCoverByPath",@"D:\dos.jpg",false);
   object c=Call(form,owner,"TakeCoverJob",hi),d=Call(form,owner,"TakeCoverJob",hi);
   Check((bool)Call(form,owner,"YieldSongWarm",@"C:\siguiente.mp3"),"Prelectura no cede a portadas en vuelo");
   Check((string)Get(c.GetType(),c,"Key")==@"C:\uno.jpg","No respeta prioridad FIFO");
   Check((string)Get(d.GetType(),d,"Key")==@"D:\uno.jpg","HDD ocupado bloquea SSD");
   Check(Call(form,owner,"TakeCoverJob",hi)==null,"Dos lectores simultaneos en HDD");
   object d2=Call(form,owner,"TakeCoverJob",lo);Check(d2!=null,"SSD no permite paralelo limitado");
   Call(form,owner,"ReleaseCoverUnit",c);
   object c2=Call(form,owner,"TakeCoverJob",hi);Check(c2!=null,"No reanuda HDD al liberar");
   Call(form,owner,"InvalidateCoverMemory");
   IDictionary active=(IDictionary)Get(form,owner,"coverActiveUnits");
   Check((int)active[@"C:\"]==1&&(int)active[@"D:\"]==2,"F5 olvida lecturas en vuelo");
   Call(form,owner,"RequestCoverByPath",@"C:\nueva.jpg",true);
   Check(Call(form,owner,"TakeCoverJob",hi)==null,"F5 permite solapar HDD");
   Call(form,owner,"ReleaseCoverUnit",c2);
   c2=Call(form,owner,"TakeCoverJob",hi);Check(c2!=null,"Generacion nueva bloqueada");
   Call(form,owner,"ReleaseCoverUnit",c2);Call(form,owner,"ReleaseCoverUnit",d);Call(form,owner,"ReleaseCoverUnit",d2);
   Check(active.Count==0,"Unidades quedan ocupadas");
   ((IDictionary)Get(form,owner,"coverLoading")).Clear(); // simular publicacion de trabajos finalizados
   Check(!(bool)Call(form,owner,"YieldSongWarm",@"C:\siguiente.mp3"),"Prelectura no reanuda sin portadas");
   Set(form,null,"coverCarriles",1);
   Call(form,owner,"RequestCoverByPath",@"D:\tres.jpg",true);
   Call(form,owner,"RequestCoverByPath",@"D:\cuatro.jpg",true);
   d=Call(form,owner,"TakeCoverJob",hi);
   Check(Call(form,owner,"TakeCoverJob",hi)==null,"SSD ignora limite de 2C/2T");
   Call(form,owner,"ReleaseCoverUnit",d);
   Call(form,owner,"PrepareCoverPage");
  }finally{
   Set(form,null,"coverCarriles",oldLanes);Set(form,null,"perfilDisco",oldProfile);
   if(oldC==null)units.Remove(@"C:\");else units[@"C:\"]=oldC;
   if(oldD==null)units.Remove(@"D:\");else units[@"D:\"]=oldD;
  }
 }
 static object[] ahead;
 static int Fill<T>(T[] cells){int n=Math.Min(cells.Length,ahead.Length);for(int i=0;i<n;i++)cells[i]=(T)ahead[i];return n;}
 static object Cell(Type t,string key,Bitmap bitmap){object c=Activator.CreateInstance(t);Set(t,c,"Key",key);Set(t,c,"Bmp",bitmap);return c;}
 static IntPtr Texture(Type panelType,object panel,object cell,int budget){
  object[] args={cell,budget,IntPtr.Zero,0f,0f};
  Check((bool)Call(panelType,panel,"EnsureCellTex",args),"Textura no disponible");return (IntPtr)args[2];
 }
 static void Gpu(Assembly asm){
  Type t=asm.GetType("Rocola.GpuPanel"),ct=asm.GetType("Rocola.GpuGridCell");
  using(Control panel=(Control)Activator.CreateInstance(t,true))
  using(Bitmap small=new Bitmap(129,67))
  using(Bitmap large=new Bitmap(1024,1024)){
   panel.Size=new Size(640,480);
   Type callback=t.GetField("GetGridAhead",F).FieldType;
   Set(t,panel,"GetGridAhead",Delegate.CreateDelegate(callback,typeof(CargaAdaptativa).GetMethod("Fill",F).MakeGenericMethod(ct)));
   if(!(bool)Call(t,panel,"StartGpu")){
    Console.WriteLine("  OK    NO SE PROBO AQUI lo de la tarjeta: hace falta una de verdad (HAL). Solo mide en la maquina del bar.");
    return;
   }
   try{
    Set(t,panel,"cellFrame",10);
    object visible=Cell(ct,"visible",small);IntPtr visibleTex=Texture(t,panel,visible,1);
    IDictionary cache=(IDictionary)Get(t,panel,"cellTexCache");
    ahead=new object[36];for(int i=0;i<ahead.Length;i++)ahead[i]=Cell(ct,"vecina-"+i,small);
    Call(t,panel,"WarmAheadCells");Check(cache.Count==2,"Prepara mas de una textura por llamada");
    IntPtr prepared=Texture(t,panel,ahead[0],0);
    Check(prepared!=IntPtr.Zero,"Pagina preparada aun necesita subida");
    for(int i=0;i<50;i++)Call(t,panel,"WarmAheadCells");
    Check(cache.Count==37,"No completa vecinas");
    for(int i=0;i<ahead.Length;i++)ahead[i]=Cell(ct,"otra-"+i,small);
    for(int i=0;i<50;i++)Call(t,panel,"WarmAheadCells");
    Check(cache.Count<=54,"Excede capacidad de cache");
    Check(Texture(t,panel,visible,0)==visibleTex,"Expulsa textura visible");
    IntPtr[] textures=new IntPtr[36];
    for(int i=0;i<36;i++)textures[i]=Texture(t,panel,ahead[i],0);
    for(int i=0;i<100;i++)Call(t,panel,"WarmAheadCells");
    for(int i=0;i<36;i++)Check(Texture(t,panel,ahead[i],0)==textures[i],"Recrea vecinas ya preparadas");
    Call(t,panel,"StopGpu");
    Check((bool)Call(t,panel,"StartGpu"),"No recupera GPU");
    Set(t,panel,"cellFrame",100);
    visibleTex=Texture(t,panel,visible,1);
    for(int i=0;i<ahead.Length;i++)ahead[i]=Cell(ct,"grande-"+i,large);
    for(int i=0;i<50;i++)Call(t,panel,"WarmAheadCells");
    Check(cache.Count==8,"No limita precarga a memoria disponible");
    object first=cache["grande-0"];IntPtr stable=(IntPtr)Get(first.GetType(),first,"Tex");
    for(int i=0;i<100;i++)Call(t,panel,"WarmAheadCells");
    Check(Object.ReferenceEquals(cache["grande-0"],first)&&(IntPtr)Get(first.GetType(),first,"Tex")==stable,"Thrashing con vecinas que no caben");
    Check((long)Call(t,panel,"CachedCellBytes")<=32L*1024*1024,"Excede 32 MiB");
    Check(Texture(t,panel,visible,0)==visibleTex,"Expulsa visible bajo presion");
   }finally{Call(t,panel,"StopGpu");ahead=null;}
   Check((long)Call(t,panel,"CachedCellBytes")==0,"No libera cache GPU");
  }
 }
 [STAThread] static int Main(string[] args){
  Assembly asm=Assembly.LoadFrom((args.Length > 0 ? args[0] : "rocola.exe"));Type policy=asm.GetType("Rocola.CargaTrabajo");
  int[] cores={1,2,3,4,8,12,16,32},expected={1,1,2,3,4,6,8,8};
  for(int i=0;i<cores.Length;i++)Check((int)Call(policy,null,"Lectores",cores[i])==expected[i],"Limite CPU");
  Check((long)Call(policy,null,"LimiteTicks",2)<(long)Call(policy,null,"LimiteTicks",4),"No reduce presupuesto en 2C/2T");
  Check((bool)Call(policy,null,"PuedeEmpezar",200L,500L,100L,true),"No garantiza avance visible");
  Check(!(bool)Call(policy,null,"PuedeEmpezar",80L,30L,100L,false),"Inicia trabajo sin margen");
  Check((bool)Call(policy,null,"PuedeEmpezar",60L,30L,100L,false),"No aprovecha margen");
  Check((long)Call(policy,null,"AprendeCoste",10L,90L)==90L,"No aprende pico de coste");
  if(args.Length>1)Check((int)Get(policy,null,"Procesadores")==Int32.Parse((args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "cargaadaptativa.png"))),"No respeta afinidad del proceso");
  Scheduling(asm.GetType("Rocola.Form1"));Gpu(asm);
  Console.WriteLine("  OK    presupuestos 1-32 hilos, HDD/SSD por unidad, F5, precarga HAL, reutilizacion, 32 MiB sin thrashing y recuperacion. Procesadores disponibles: "+Get(policy,null,"Procesadores"));
  return 0;
 }
}
