using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
class EspacioPrecarga {
 const BindingFlags F=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static int checks;
 static object Call(Type t,object o,string n,params object[] a){return t.GetMethod(n,F).Invoke(o,a);}
 static object Get(Type t,object o,string n){return t.GetField(n,F).GetValue(o);}
 static void Set(Type t,object o,string n,object v){t.GetField(n,F).SetValue(o,v);}
 static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static object[] ahead;
 static int Fill<T>(T[] cells){int n=Math.Min(cells.Length,ahead.Length);for(int i=0;i<n;i++)cells[i]=(T)ahead[i];return n;}
 static object Cell(Type t,string key,Bitmap b){object c=Activator.CreateInstance(t);Set(t,c,"Key",key);Set(t,c,"Bmp",b);return c;}
 static IntPtr Texture(Type t,object p,object cell,int budget){
  object[] args={cell,budget,IntPtr.Zero,0f,0f};
  Check((bool)Call(t,p,"EnsureCellTex",args),"Textura aun exige carga al navegar");return (IntPtr)args[2];
 }
 static void Preload(Type form){
  Check((bool)Call(form,null,"PrimePageImmediately",100u,100u,false),"Primera pagina no precarga");
  Check(!(bool)Call(form,null,"PrimePageImmediately",499u,100u,true),"Rafaga precarga");
  Check((bool)Call(form,null,"PrimePageImmediately",500u,100u,true),"Paso lento no precarga");
  Check((bool)Call(form,null,"PrimePageImmediately",299u,UInt32.MaxValue-100u,true),"Reloj desbordado");
  Check(!(bool)Call(form,null,"PrimePageImmediately",298u,UInt32.MaxValue-100u,true),"Frontera del reloj");
  Check(Codigo.Llama(form.GetMethod("RefreshAlbums",F),"PrimeNextPage"),"Precarga no conectada");
  Check(Codigo.Llama(form.GetMethod("PrimeNextPage",F),"RequestCover"),"No usa cola existente");
  object owner=Codigo.FormSinEncender(form);Set(form,owner,"coverPumpCount",Int32.MaxValue);
  Set(form,owner,"coversOnHold",true); // no lanzar hilos ni leer discos reales
  Type album=form.GetNestedType("Album",F);
  IList list=(IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(album));
  int size=(int)Get(form,null,"PageSize");
  for(int i=0;i<size*3+2;i++){
   object a=Activator.CreateInstance(album,true);
   Set(album,a,"CoverPath","portada-"+i);list.Add(a);
  }
  Call(form,owner,"PrimeNextPage",list,0);
  IDictionary loading=(IDictionary)Get(form,owner,"coverLoading");
  Check(loading.Count==size,"No prepara una pagina completa durante coversOnHold");
  for(int i=0;i<size;i++)Check(loading.Contains("portada-"+(size+i)),"Precarga fuera de siguiente pagina");
  Check(((ICollection)Get(form,owner,"coverQHi")).Count==0,"Vecinas con prioridad visible");
  Check(((ICollection)Get(form,owner,"coverQLo")).Count==size,"No usa baja prioridad");
  Set(form,owner,"adjacentPageTick",unchecked((uint)Environment.TickCount));
  Call(form,owner,"PrimeNextPage",list,1);
  Check(loading.Count==size,"Rafaga acumula mas paginas");
  Call(form,owner,"RequestCoverByPath","portada-"+size,true);
  Check(loading.Count==size&&((ICollection)Get(form,owner,"coverQHi")).Count==1,"Promocion duplica lectura");
  Call(form,owner,"PrepareCoverPage");Check(loading.Count==0,"No cancela pendientes");
  Set(form,owner,"adjacentPageKnown",false);Call(form,owner,"PrimeNextPage",list,2);
  Check(loading.Count==2,"Ultima pagina parcial");
  Call(form,owner,"PrepareCoverPage");Set(form,owner,"adjacentPageKnown",false);
  Call(form,owner,"PrimeNextPage",list,Int32.MaxValue);Check(loading.Count==0,"Pagina desbordada");
  Set(form,owner,"currentLetter",'Z');
  object letters=Get(form,owner,"letterAvailable");Call(letters.GetType(),letters,"Add",'A');
  ((IDictionary)Get(form,owner,"byLetter"))['A']=list;
  Set(form,owner,"adjacentPageKnown",false);Call(form,owner,"PrimeNextPage",list,3);
  Check(loading.Count==size&&loading.Contains("portada-0"),"No precarga al cambiar de letra/ciclar");
  Check((char)Get(form,owner,"currentLetter")=='Z',"Precarga cambia seleccion");
  Call(form,owner,"PrepareCoverPage");Set(form,owner,"adjacentPageKnown",false);Set(form,owner,"libBuilding",true);
  Call(form,owner,"PrimeNextPage",list,0);Check(loading.Count==0,"Lee biblioteca durante reconstruccion");
 }
 static void Surface(Type gpu,string dir){
  using(Bitmap star=(Bitmap)Call(gpu,null,"BakeStar",true))
  using(Bitmap again=(Bitmap)Call(gpu,null,"BakeStar",true))
  using(Bitmap simple=(Bitmap)Call(gpu,null,"BakeStar",false)){
   Check(star.Width==256&&star.Height==256,"Textura sin limite");
   int different=0,clear=0,solid=0;
   for(int y=0;y<256;y++)for(int x=0;x<256;x++){
    Color c=star.GetPixel(x,y);Check(c.ToArgb()==again.GetPixel(x,y).ToArgb(),"No determinista");
    if(c.A==0)clear++;if(c.ToArgb()!=simple.GetPixel(x,y).ToArgb())different++;
    double nx=(x-127.5)/(256*.49),ny=(y-127.5)/(256*.49);
    if(nx*nx+ny*ny<.8*.8){Check(c.A==255,"Superficie translucida");solid++;}
   }
   Check(clear>14000&&solid>30000&&different>25000,"Sin corona/superficie detallada");
   string path=Path.Combine(dir,"estrella-superficie.png");star.Save(path,ImageFormat.Png);
   using(Bitmap preview=new Bitmap(760,330))
   using(Graphics g=Graphics.FromImage(preview))
   using(Font f=new Font("Segoe UI",11)){
    g.Clear(Color.FromArgb(6,11,30));g.DrawString("Superficie real GPU / estilos y color por vertice",f,Brushes.White,20,12);
    uint[] colors={0xFFFFAD6Fu,0xFF80C9FFu,0xFFFF8ADC};
    for(int i=0;i<3;i++)using(ImageAttributes attr=new ImageAttributes()){
     ColorMatrix matrix=new ColorMatrix();matrix.Matrix00=((colors[i]>>16)&255)/255f;
     matrix.Matrix11=((colors[i]>>8)&255)/255f;matrix.Matrix22=(colors[i]&255)/255f;
     attr.SetColorMatrix(matrix);
     g.DrawImage(i==2?simple:star,new Rectangle(15+i*250,55,230,230),0,0,256,256,GraphicsUnit.Pixel,attr);
    }
    preview.Save(Path.Combine(dir,"estrella-vista.png"),ImageFormat.Png);
   }
  }
 }
 static void Policies(Type gpu){
  const long mb=1024L*1024;
  Check((long)Call(gpu,null,"AdaptiveCellBudget",0u,192,8)==32*mb,"Sin datos no conserva cache");
  Check((long)Call(gpu,null,"AdaptiveCellBudget",UInt32.MaxValue,32,8)==32*mb,"RAM baja no conserva cache");
  Check((long)Call(gpu,null,"AdaptiveCellBudget",256u*1024*1024,64,4)==64*mb,"No reserva margen GPU");
  Check((long)Call(gpu,null,"AdaptiveCellBudget",UInt32.MaxValue,64,4)==96*mb,"Tope 32 bits");
  Check((long)Call(gpu,null,"AdaptiveCellBudget",UInt32.MaxValue,192,8)==128*mb,"Tope 64 bits");
  Check((int)Call(gpu,null,"AheadUploadAllowance",2)==1,"2C/2T sin freno");
  Check((int)Call(gpu,null,"AheadUploadAllowance",4)==2,"4 hilos no aprovecha margen");
  Check((int)Call(gpu,null,"AheadUploadAllowance",16)==4,"Equipo potente limitado a uno");
  Check((float)Call(gpu,null,"IdleWallAlpha",.1f)==0,"Muro sigue visible");
  Check((float)Call(gpu,null,"IdleWallAlpha",1f)==1,"No recupera muro");
  Check((float)Call(gpu,null,"IdleWallAlpha",Single.NaN)==1,"NaN oculta interfaz");
  Check((float)Call(gpu,null,"IdleControlAlpha",1f,true)==0,"Controles siguen visibles");
  Check((float)Call(gpu,null,"IdleControlAlpha",1f,false)==1,"Toggle no restaura controles");
  Check(Codigo.Llama(gpu.GetMethod("DrawPaletteOld",F),"get_ControlsOpacity"),"Cambio de color resucita controles ocultos");
  foreach(int fps in new int[]{24,60,144,240}){
   float b=0;
   for(int i=0;i<fps*6;i++)b=(float)Call(gpu,null,"StepIdle",b,true,1d/fps);
   Check(b==1&&(float)Call(gpu,null,"IdleControlAlpha",b,true)==0,"Reposo no llega a cero real");
   float next=(float)Call(gpu,null,"StepIdle",b,false,1d/fps);
   Check(next<b&&(float)Call(gpu,null,"IdleControlAlpha",next,true)>0,"No responde al volver a usar");
  }
 }
 static void Hardware(Assembly asm,Type gpu){
  Type cell=asm.GetType("Rocola.GpuGridCell");
  using(Control panel=(Control)Activator.CreateInstance(gpu,true))
  using(Bitmap bitmap=new Bitmap(512,512)){
   panel.Size=new Size(640,480);
   Set(gpu,panel,"GetGridAhead",Delegate.CreateDelegate(gpu.GetField("GetGridAhead",F).FieldType,
    typeof(EspacioPrecarga).GetMethod("Fill",F).MakeGenericMethod(cell)));
   if(!(bool)Call(gpu,panel,"StartGpu")){Console.WriteLine("HAL no disponible: cache/render real no comprobados");return;}
   try{
    Set(gpu,panel,"cellTextureBudget",64L*1024*1024);Set(gpu,panel,"cellFrame",100);
    IntPtr[] visible=new IntPtr[18];object[] page=new object[18];
    for(int i=0;i<18;i++){page[i]=Cell(cell,"visible-"+i,bitmap);visible[i]=Texture(gpu,panel,page[i],1);}
    ahead=new object[18];for(int i=0;i<18;i++)ahead[i]=Cell(cell,"siguiente-"+i,bitmap);
    for(int i=0;i<45;i++)Call(gpu,panel,"WarmAheadCells");
    Check(((IDictionary)Get(gpu,panel,"cellTexCache")).Count==36,"Dos paginas no caben con margen");
    for(int i=0;i<18;i++){
     Check(Texture(gpu,panel,page[i],0)==visible[i],"Precarga expulsa visibles");
     Check(Texture(gpu,panel,ahead[i],0)!=IntPtr.Zero,"Siguiente pagina no preparada");
    }
    Set(gpu,panel,"ConLuna",true);Set(gpu,panel,"LunaRealista",true);Set(gpu,panel,"NebColor",0xFFFF0000u);
    Call(gpu,panel,"EnsureLuna");IntPtr tex=(IntPtr)Get(gpu,panel,"lunaTex");
    uint red=(uint)Call(gpu,panel,"StarColor");Set(gpu,panel,"NebColor",0xFF0000FFu);
    uint blue=(uint)Call(gpu,panel,"StarColor");
    Check(red!=blue&&(blue>>24)==255,"Estrella no cambia color o no es opaca");
    for(int i=0;i<6;i++){
     Set(gpu,panel,"moonMotionTime",(double)i*8);Call(gpu,panel,"EnsureLuna");
     Check((IntPtr)Get(gpu,panel,"lunaTex")==tex,"Color/giro rehornea textura");
    }
    Check(Codigo.Llama(gpu.GetMethod("EnsureLuna",F),"BakeStar"),"GPU no dibuja estrella");
    Check(!Codigo.Llama(gpu.GetMethod("EnsureLuna",F),"BakeLuna"),"Sigue usando luna");
    Check(Codigo.Llama(gpu.GetMethod("DrawDetailedMoon",F),"StarColor"),"Tinte desconectado");
   }finally{Call(gpu,panel,"StopGpu");ahead=null;}
   Check((long)Call(gpu,panel,"CachedCellBytes")==0,"No libera memoria");
   Console.WriteLine("HAL: dos paginas completas 512px reutilizadas sin subidas al navegar; color y textura estable.");
  }
 }
 [STAThread]static int Main(string[] args){
  string exe=args.Length>0?args[0]:"rocola.exe";
  Assembly asm=Assembly.LoadFrom(exe);Type gpu=asm.GetType("Rocola.GpuPanel");
  string dir=Path.GetDirectoryName(Path.GetFullPath(exe));
  Preload(asm.GetType("Rocola.Form1"));Surface(gpu,dir);Policies(gpu);Hardware(asm,gpu);
  Console.WriteLine("OK espacio/precarga: "+checks+" comprobaciones. Vista: "+Path.Combine(dir,"estrella-vista.png"));return 0;
 }
}
