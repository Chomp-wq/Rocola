using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
class HojeoPortadas {
 const BindingFlags F=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Call(Type t,object o,string n,params object[] a){return t.GetMethod(n,F).Invoke(o,a);}
 static object Get(Type t,object o,string n){return t.GetField(n,F).GetValue(o);}
 static void Set(Type t,object o,string n,object v){t.GetField(n,F).SetValue(o,v);}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static Bitmap cover;static bool ready;static int foregroundCalls;
 static Image Visible<T>(T album){foregroundCalls++;return cover;}
 static Image Cached<T>(T album){return ready?cover:null;}
 static Delegate Provider(Type album,Type func,string method){
  return Delegate.CreateDelegate(func,typeof(HojeoPortadas).GetMethod(method,F).MakeGenericMethod(album));
 }
 static void TestPendingPages(Type form) {
  // Sin abrir la biblioteca ni arrancar trabajadores: ejercitar solicitudes reales y su estado en vuelo.
  object owner=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(form);
  foreach(FieldInfo f in form.GetFields(F))
   if(!f.IsStatic&&f.FieldType.Namespace!=null&&f.FieldType.Namespace.StartsWith("System.Collections")&&f.FieldType.GetConstructor(Type.EmptyTypes)!=null)
    f.SetValue(owner,Activator.CreateInstance(f.FieldType));
  Set(form,owner,"coverLock",new object());Set(form,owner,"coverGen",7);
  Set(form,owner,"coverPumpCount",Int32.MaxValue);
  Call(form,owner,"RequestCoverByPath","en-vuelo",true);
  object hi=Get(form,owner,"coverQHi"),lo=Get(form,owner,"coverQLo");
  IDictionary pending=(IDictionary)Get(form,owner,"coverLoading");
  object flight=pending["en-vuelo"];
  Call(hi.GetType(),hi,"RemoveFirst");Set(flight.GetType(),flight,"Node",null);
  for(int i=0;i<20;i++){
   Call(form,owner,"RequestCoverByPath","visible",true);
   Call(form,owner,"RequestCoverByPath","vecina",false);
   Check(pending.Count==3,"Solicitud cancelada no puede volver a pedirse");
   Call(form,owner,"PrepareCoverPage");
   Check((int)Get(form,owner,"coverGen")==7,"Cambio de pagina invalida lectura en vuelo");
   Check(pending.Count==1&&Object.ReferenceEquals(flight,pending["en-vuelo"]),"Pierde registro de lectura en vuelo");
   Check(((ICollection)hi).Count==0&&((ICollection)lo).Count==0,"Cola antigua no vaciada");
   Call(form,owner,"RequestCoverByPath","en-vuelo",true);
   Check(pending.Count==1&&((ICollection)hi).Count==0,"Duplica decodificacion en vuelo");
  }
  Call(form,owner,"RequestCoverByPath","vecina",false);
  Call(form,owner,"RequestCoverByPath","vecina",true);
  Check(((ICollection)lo).Count==0&&((ICollection)hi).Count==1,"No prioriza portada ahora visible");
  Call(form,owner,"InvalidateCoverMemory");
  Check((int)Get(form,owner,"coverGen")==8&&pending.Count==0,"F5 no invalida contenido anterior");
  Call(form,owner,"RequestCoverByPath","en-vuelo",true);
  Check((int)Get(flight.GetType(),pending["en-vuelo"],"Gen")==8,"Nueva lectura usa generacion antigua");
  Call(form,owner,"PrepareCoverPage");
  Check(pending.Count==0,"Registro fantasma tras F5 y cambio de pagina");
 }
 [STAThread] static int Main(string[] args) {
  Assembly asm=Assembly.LoadFrom((args.Length > 0 ? args[0] : "rocola.exe"));Type form=asm.GetType("Rocola.Form1");
  TestPendingPages(form);
  Type view=form.GetNestedType("AlbumView",F),album=form.GetNestedType("Album",F),cell=asm.GetType("Rocola.GpuGridCell");
  Check(!(bool)Call(view,null,"RapidPageTurn",100,0,false),"Primera pagina tratada como rafaga");
  Check(!(bool)Call(view,null,"RapidPageTurn",500,100,true),"Paso pausado tratado como rafaga");
  Check((bool)Call(view,null,"RapidPageTurn",250,100,true),"No detecta rafaga");
  Check((bool)Call(view,null,"RapidPageTurn",Int32.MinValue+20,Int32.MaxValue-20,true),"Fallo al desbordar reloj");
  using(Control panel=(Control)Activator.CreateInstance(view,true))
  using(Bitmap image=new Bitmap(160,160)) {
   cover=image;using(Graphics g=Graphics.FromImage(image))g.Clear(Color.Orange);
   panel.Size=new Size(1280,720);
   IList page=(IList)Get(view,panel,"Page");
   for(int i=0;i<18;i++){
    object a=Activator.CreateInstance(album,true);
    Set(album,a,"Folder","disco"+i);Set(album,a,"CoverPath","portada"+i);Set(album,a,"Artist","Artista");Set(album,a,"Name","Disco "+i);
    page.Add(a);
   }
   Set(view,panel,"GetCover",Provider(album,view.GetField("GetCover",F).FieldType,"Visible"));
   Set(view,panel,"PeekCoverGpu",Provider(album,view.GetField("PeekCoverGpu",F).FieldType,"Cached"));
   Array cells=Array.CreateInstance(cell,18);
   Call(view,panel,"StartSlide",1,1280);
   int count=(int)Call(view,panel,"BuildGpuGrid",cells),shown=0;
   for(int i=0;i<count;i++)if(Get(cell,cells.GetValue(i),"Bmp")!=null)shown++;
   Check(shown>0,"Pagina aislada oculta todo hasta acabar animacion");
   Set(view,panel,"slideTick",-1);
   for(int i=0;i<18;i++)Call(view,panel,"BuildGpuGrid",cells);
   object[] cached=new object[count];for(int i=0;i<count;i++)cached[i]=Get(cell,cells.GetValue(i),"Bmp");
   Set(view,panel,"gpuLastSlideTick",Environment.TickCount-1000);
   Call(view,panel,"StartSlide",-1,1280);Call(view,panel,"BuildGpuGrid",cells);
   for(int i=0;i<count;i++)Check(Object.ReferenceEquals(cached[i],Get(cell,cells.GetValue(i),"Bmp")),"Volver a pagina rehornea cache");
   Set(view,panel,"gpuLastSlideTick",Environment.TickCount);
   Call(view,panel,"StartSlide",1,1280);foregroundCalls=0;Call(view,panel,"BuildGpuGrid",cells);
   for(int i=0;i<count;i++)Check(Object.ReferenceEquals(cached[i],Get(cell,cells.GetValue(i),"Bmp")),"Rafaga descarta caratula completa ya preparada");
   Check(foregroundCalls==0,"Rafaga solicita caratulas");
   Set(view,panel,"PortadasLod",false);Call(view,panel,"BuildGpuGrid",cells);
   for(int i=0;i<count;i++)Check(Get(cell,cells.GetValue(i),"Bmp")==null,"LOD apagado no conserva ocultacion previa");
   Set(view,panel,"HojeoLigero",false);Call(view,panel,"BuildGpuGrid",cells);
   Check(Get(cell,cells.GetValue(0),"Bmp")!=null,"Toggle no restaura caratulas en rafaga");
   Set(view,panel,"slideTick",-1);
   IList ahead=(IList)Activator.CreateInstance(page.GetType());
   object nextAlbum=Activator.CreateInstance(album,true);
   Set(album,nextAlbum,"Folder","siguiente");Set(album,nextAlbum,"CoverPath","proxima");Set(album,nextAlbum,"Artist","Artista");Set(album,nextAlbum,"Name","Nuevo");
   ahead.Add(nextAlbum);Call(view,panel,"SetAhead",ahead);
   IDictionary cache=(IDictionary)Get(view,panel,"gpuCellCache");int before=cache.Count;
   ready=false;foregroundCalls=0;
   for(int i=0;i<5;i++)Call(view,panel,"BakeAheadGpu");
   Check(cache.Count==before,"Prehornea placeholders sin imagen");
   ready=true;
   for(int i=0;i<5;i++)Call(view,panel,"BakeAheadGpu");
   Check(cache.Count==before+1,"No recoge portada que llego despues de primera pasada");
   Check(foregroundCalls==0,"Prehorneado hace peticiones visibles");
   int aheadCount=(int)Call(view,panel,"BuildGpuAhead",cells);
   Check(aheadCount==1&&Get(cell,cells.GetValue(0),"Bmp")!=null,"No ofrece imagen horneada a precarga GPU");
   Check(foregroundCalls==0,"Preparacion GPU hace E/S visible");
   Call(view,panel,"StartSlide",1,1280);
   Check((int)Call(view,panel,"BuildGpuAhead",cells)==0,"Precarga GPU durante deslizamiento");
   Set(view,panel,"slideTick",-1);
   page.Clear();page.Add(nextAlbum);Call(view,panel,"BuildGpuGrid",cells);
   Bitmap warmed=(Bitmap)Get(cell,cells.GetValue(0),"Bmp");
   Check(warmed!=null,"No reutiliza pagina preparada");
   Call(view,panel,"BuildGpuGrid",cells);
   Check(Object.ReferenceEquals(warmed,Get(cell,cells.GetValue(0),"Bmp")),"Repite horneado de pagina preparada");
  }
  Console.WriteLine("  OK    pagina simple inmediata, cache reutilizada, rafaga sin carga, precarga tardia, cancelacion sin registros fantasma y F5.");
  return 0;
 }
}
