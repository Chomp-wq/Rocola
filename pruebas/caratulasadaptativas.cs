using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
class CaratulasAdaptativas
{
 const BindingFlags B=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static Type governor,store,panel,cell,form,view,album; static int checks; static Bitmap preview; static bool omitHal;
 static object Get(object o,string n){return o.GetType().GetField(n,B).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,B).SetValue(o,v);}
 static object Call(Type t,object o,string n,params object[] a){try{return t.GetMethod(n,B).Invoke(o,a);}catch(TargetInvocationException e){throw e.InnerException;}}
 static object New(Type t,params object[] a){return a.Length==0 ? Activator.CreateInstance(t,true) : Activator.CreateInstance(t,B,null,a,null);}
 static void Check(bool v,string s){if(!v)throw new Exception(s);checks++;Console.WriteLine("OK "+s);}
 static string Key(int n){return n.ToString("x16")+"_256.jpg";}
 static Control Find(Control p,string text){foreach(Control c in p.Controls){if(c.Text==text)return c;Control r=Find(c,text);if(r!=null)return r;}return null;}
 static void Profiles(string root)
 {
  Check((int)Call(form,null,"PerfilCargaCaratulas","3")==3 && (int)Call(form,null,"PerfilCargaCaratulas"," 2 ")==2,"perfiles validos se leen sin depender del idioma");
  foreach(string value in new string[]{null,"",-1+"","4","999999999999999","rapido"})
   Check((int)Call(form,null,"PerfilCargaCaratulas",value)==0,"perfil invalido vuelve a automatico: "+value);
  object owner=Codigo.FormSinEncender(form);Set(owner,"carpetaDatos",root);
  if(governor!=null)Set(owner,"coverGovernor",New(governor,16));
  using(Font nom=new Font("Segoe UI",10f,FontStyle.Bold))using(Font des=new Font("Segoe UI",8.5f))
  using(Panel page=(Panel)Call(form,owner,"PaginaPantalla",nom,des,nom,Color.White,Color.Gray,Color.DimGray))
  {
   string[] names={"Automático","Fluidez","Equilibrado","Carga rápida"};
   Control card=Find(page,names[0]).Parent, sheet=card.Parent;
   Check(card.Bottom<=sheet.Height && card.Right<=sheet.Width,"tarjeta de carga cabe en la segunda hoja");
   bool layout=true;foreach(Control c in card.Controls){layout &= c.Bottom<=card.Height && c.Right<=card.Width;foreach(Control d in card.Controls)if(c!=d && c.Bounds.IntersectsWith(d.Bounds))layout=false;}
   Check(layout,"cuatro perfiles y explicaciones no se pisan ni se recortan");
   bool space=true;foreach(Control c in sheet.Controls)if(c!=card && c.Bounds.IntersectsWith(card.Bounds))space=false;
   Check(space,"tarjeta no tapa los controles de reproduccion");
   Call(typeof(Control),Find(page,"Reproducción y carátulas"),"OnClick",EventArgs.Empty);
   Check(sheet.Visible,"perfiles se pueden abrir desde su pestaña");
   using(Bitmap screenshot=new Bitmap(card.Width,card.Height))
   {
    // Raiz independiente: DrawToBitmap de .NET incluye el offset del panel
    // superior en algunos controles nativos si se captura una tarjeta anidada.
    Point location=card.Location;sheet.Controls.Remove(card);card.Location=Point.Empty;
    try{card.DrawToBitmap(screenshot,new Rectangle(Point.Empty,card.Size));}
    finally{sheet.Controls.Add(card);card.Location=location;}
    screenshot.Save(Path.Combine(root,"perfiles.png"),System.Drawing.Imaging.ImageFormat.Png);
    using(Graphics graphics=Graphics.FromImage(screenshot))
    {
     bool textFits=true;foreach(Control c in card.Controls)if(c is Label && graphics.MeasureString(c.Text,c.Font,c.Width).Height>c.Height)textFits=false;
     Check(textFits,"explicaciones completas caben con las fuentes reales");
    }
   }
   for(int i=0;i<4;i++)
   {
    Call(typeof(Control),Find(page,names[i]),"OnClick",EventArgs.Empty);
    Check((int)Get(owner,"caratulasCarga")==i && (bool)Get(owner,"f3CambioGraf"),"boton elige perfil "+names[i]);
    if(governor!=null)Check((int)Get(Get(owner,"coverGovernor"),"profile")==i,"perfil se aplica sin reiniciar "+names[i]);
    Call(form,owner,"SaveConfigPrefs");Codigo.EsperaGuardado(form,owner);
    object read=Codigo.FormSinEncender(form);Set(read,"carpetaDatos",root);Call(form,read,"LoadConfigPrefs");
    Check((int)Get(read,"caratulasCarga")==i && File.ReadAllText(Path.Combine(root,"rocola_config.txt")).Contains("caratulas_carga="+i),"perfil persiste "+names[i]);
   }
   // El aviso toast requiere la ventana completa: comprobar el estado antes de esa llamada.
   try {Call(typeof(Control),Find(page,"Pantalla como de fabrica"),"OnClick",EventArgs.Empty);}catch(NullReferenceException){}
   Check((int)Get(owner,"caratulasCarga")==0 && Find(page,names[0])!=null && !Object.ReferenceEquals(card,Find(page,names[0]).Parent),"fabrica restaura automatico y renueva la seleccion visible");
  }
  if(governor==null)return;
  double[] budgets=new double[4];
  for(int p=0;p<4;p++)
  {
   object g=New(governor,16);Call(governor,g,"SetProfile",p);Call(governor,g,"Begin",1d,200);budgets[p]=(double)Get(g,"allowance");
   for(int i=0;i<900;i++){Call(governor,g,"Begin",10d*i,200);Call(governor,g,"Observe",10d*i+1,1d,200);}
   int readers=(int)governor.GetProperty("ProfileReaders",B).GetValue(g,null);
   Check(readers==(p==1?2:p==2?4:8),"lectores seguros segun perfil y hardware "+p);
   Call(governor,g,"Spend",(long)(Stopwatch.Frequency*.002));Call(governor,g,"Observe",9010d,40d,200);
   Call(governor,g,"Begin",9020d,200);
   Check((double)Get(g,"allowance")<=.5 && (int)Get(g,"ReaderPause")>0,"perfil conserva proteccion bajo presion "+p);
   object weak=New(governor,2);Call(governor,weak,"SetProfile",p);Set(weak,"ReaderLimit",8);
   Call(governor,weak,"BeginBackground",1d);
   Check((int)governor.GetProperty("ProfileReaders",B).GetValue(weak,null)==1 && (double)Get(weak,"allowance")<=3,"dos hilos conservan limites en perfil "+p);
  }
  Check(budgets[1]<budgets[2] && budgets[2]<budgets[0] && budgets[0]<budgets[3],"perfiles cambian presupuesto, no solo nombre");
  object keep=New(governor,16);Call(governor,keep,"SetProfile",3);Call(governor,keep,"SetProfile",-1);Call(governor,keep,"Begin",1d,200);
  Check((double)Get(keep,"allowance")== (double)Call(governor,null,"Budget",1d,200),"automatico mantiene exactamente el presupuesto anterior");
  Set(keep,"baseMs",7d);Set(keep,"ReaderLimit",5);Set(keep,"pressure",6);
  Call(governor,keep,"SetProfile",1);
  Check((double)Get(keep,"baseMs")==7d && (int)Get(keep,"ReaderLimit")==5 && (int)Get(keep,"pressure")==6,"cambiar perfil conserva aprendizaje y presion");
 }
 static void Regulator()
 {
  Check((double)Call(governor,null,"Budget",1d,200)>.35,"equipo rapido dispone de margen a 200 FPS");
  Check((double)Call(governor,null,"Budget",1d,30)>(double)Call(governor,null,"Budget",1d,200),"30 no es un techo universal de cuadros");
  Check((double)Call(governor,null,"Budget",40d,200)==.35,"no inventa margen cuando el fondo ya es caro");
  Check((double)Call(governor,null,"Budget",double.NaN,200)==.35,"medida no valida usa presupuesto seguro");
  object g=New(governor,2);Call(governor,g,"Begin",1d,200);
  Call(governor,g,"Spend",(long)(Stopwatch.Frequency*.010));
  Check(!(bool)Call(governor,g,"CanStart",(long)(Stopwatch.Frequency*.002),true,false),"horneado y upload comparten gasto");
  Set(g,"PendingUpload",true);Call(governor,g,"Begin",2d,200);
  Check((bool)Get(g,"DeferBake"),"pendiente de upload bloquea nuevos horneados antes de sufrir inanicion");
  Check((bool)Call(governor,g,"CanStart",(long)(Stopwatch.Frequency*.0001),true,true),"reserva margen para el LOD");
  Call(governor,g,"Spend",(long)(Stopwatch.Frequency*.002));
  Call(governor,g,"Observe",50d,40d,200);
  Check((int)Get(g,"ReaderPause")>0 && (int)Get(g,"ReaderLimit")==1,"pico cede CPU y conserva un lector en 2 hilos");
  object fast=New(governor,16);
  for(int i=1;i<=6;i++){Call(governor,fast,"Begin",50d*i,200);Call(governor,fast,"Observe",50d*i+1,1d,200);}
  Check((int)Get(fast,"pressure")==0,"intervalos externos lentos sin trabajo caro no estrangulan precarga");
  Check((bool)Call(governor,fast,"BeginBackground",350d) && (bool)Call(governor,fast,"CanStart",(long)(Stopwatch.Frequency*.005),false,false),"casilla de 5 ms puede precargarse con tope de 200 FPS");
  for(int i=0;i<1800;i++){Call(governor,fast,"Begin",5d*i,200);Call(governor,fast,"Observe",5d*i+1,1d,200);}
  Check((int)Get(fast,"ReaderLimit")==8,"equipo sano libera ocho lectores en lugar de quedar atado al arranque");
  Call(governor,fast,"ResetMeasure");for(int i=1;i<=100;i++)Call(governor,fast,"RecordInterval",(double)i);
  Check((double)Call(governor,fast,"Percentile",.95)==95 && (double)Call(governor,fast,"Percentile",.99)==99,"P95/P99 no ocultan picos tras una media");
  Call(governor,fast,"ResetMeasure");
  Call(governor,fast,"Page","a",false,10d);Call(governor,fast,"FinishPage",true,35d);Call(governor,fast,"FinishPage",true,60d);
  Check((int)Get(fast,"CompletedPages")==1 && (double)Get(fast,"WorstPageMs")==25,"latencia de pagina completa se cuenta una sola vez");
  Call(governor,fast,"Page","b",false,100d);Call(governor,fast,"Page","c",false,110d);Call(governor,fast,"FinishPage",true,120d);
  Check((int)Get(fast,"CompletedPages")==2,"pagina abandonada no se cuenta como terminada");
 }
 static void TimedLearning()
 {
  foreach(int fps in new int[]{30,60,200})
  {
   object g=New(governor,16);
   for(int i=0;i<=fps*3;i++){double now=1000d+i*1000d/fps;Call(governor,g,"Begin",now,fps);Call(governor,g,"Observe",now,1d,fps);}
   Check((int)Get(g,"ReaderLimit")==5,"tres segundos sanos liberan cinco lectores a "+fps+" FPS");
   Call(governor,g,"ResetMeasure");
   for(int i=1;i<=fps*3;i++){double now=4000d+i*1000d/fps;Call(governor,g,"Begin",now,fps);Call(governor,g,"Observe",now,1d,fps);}
   Check((int)Get(g,"ReaderLimit")==8,"reset del medidor conserva aprendizaje a "+fps+" FPS");
   Call(governor,g,"Begin",7100d,fps);Call(governor,g,"Spend",(long)(Stopwatch.Frequency*.010));Call(governor,g,"Observe",7100d,40d,fps);
   for(int i=1;i<=19;i++){Call(governor,g,"Begin",7100d+i*10,fps);Call(governor,g,"Observe",7100d+i*10,1d,fps);}
   Check((int)Get(g,"pressure")==1,"presion dura al menos 200 ms a "+fps+" FPS");
   Call(governor,g,"Begin",7301d,fps);Call(governor,g,"Observe",7301d,1d,fps);
   Check((int)Get(g,"pressure")==0,"presion se libera por tiempo a "+fps+" FPS");
  }
  object rapid=New(governor,16);Call(governor,rapid,"SetProfile",3);
  for(int i=0;i<=150;i++){Call(governor,rapid,"Begin",1000d+i*5,200);Call(governor,rapid,"Observe",1000d+i*5,1d,200);}
  Check((int)Get(rapid,"ReaderLimit")==3,"carga rapida aprende cada 375 ms");
  Set(rapid,"pressure",1);long expensive=(long)(Stopwatch.Frequency*.020);
  Call(governor,rapid,"Begin",2000d,200);Check((bool)Call(governor,rapid,"CanStart",expensive,true,false),"primera pieza visible progresa bajo presion");Call(governor,rapid,"Spend",expensive);
  Call(governor,rapid,"Begin",2065d,200);Check(!(bool)Call(governor,rapid,"CanStart",expensive,true,false),"pieza visible espera 66 ms sin depender de cuadros");
  Call(governor,rapid,"Begin",2066d,200);Check((bool)Call(governor,rapid,"CanStart",expensive,true,false),"pieza visible reanuda a los 66 ms");
  Call(governor,rapid,"VisitCover","ready",true);Call(governor,rapid,"VisitCover","ready",false);Call(governor,rapid,"VisitCover","late",false);Call(governor,rapid,"VisitCover","late",true);
  Check((int)Get(rapid,"SeenCovers")==2 && (int)Get(rapid,"ReadyAtFirstSeen")==1,"GPU previas cuenta primera visita unica, no llegadas posteriores");
  Call(governor,rapid,"ResetMeasure");Check((int)Get(rapid,"SeenCovers")==0,"cada prueba tiene sus contadores de cache");
 }
 static void WaitReaders(object owner,int count,bool pumps)
 {
  Stopwatch timer=Stopwatch.StartNew();
  while(timer.ElapsedMilliseconds<3000){lock(Get(owner,"coverLock")){if((int)(pumps?Get(owner,"coverPumpCount"):Call(form,owner,"ActiveCoverReaders"))==count)return;}System.Threading.Thread.Sleep(1);}
  throw new Exception("No convergieron los lectores: "+count);
 }
 static void Readers()
 {
  int lanes=(int)form.GetField("coverCarriles",B).GetValue(null), disk=(int)form.GetField("perfilDisco",B).GetValue(null);
  FieldInfo semaphore=form.GetField("coverSem",B);object oldSemaphore=semaphore.GetValue(null);
  object owner=Codigo.FormSinEncender(form);object g=New(governor,16);Set(owner,"gpuEnabled",true);Set(owner,"coverGovernor",g);Set(g,"ReaderLimit",4);Call(governor,g,"SetProfile",1);
  System.Threading.Semaphore gate=new System.Threading.Semaphore(0,8);bool draining=false;
  try
  {
   form.GetField("coverCarriles",B).SetValue(null,4);form.GetField("perfilDisco",B).SetValue(null,1); // SSD simulado, sin abrir archivos
   Set(owner,"coverPumpCount",Int32.MaxValue);
   foreach(string route in new string[]{@"C:\one.jpg",@"C:\two.jpg",@"D:\one.jpg",@"D:\two.jpg"})Call(form,owner,"RequestCoverByPath",route,true);
   object hi=Get(owner,"coverQHi");object a=Call(form,owner,"TakeCoverJob",hi),b=Call(form,owner,"TakeCoverJob",hi);
   Check(a!=null && b!=null && Call(form,owner,"TakeCoverJob",hi)==null,"Fluidez limita dos lectores globales aunque haya dos SSD");
   Call(form,owner,"ReleaseCoverUnit",a);object c=Call(form,owner,"TakeCoverJob",hi);Check(c!=null,"liberar cupo global deja avanzar otra unidad");
   Call(form,owner,"InvalidateCoverMemory");Check((int)Call(form,owner,"ActiveCoverReaders")==2,"F5 conserva lecturas en vuelo en el cupo global");
   Call(form,owner,"ReleaseCoverUnit",b);Call(form,owner,"ReleaseCoverUnit",c);
   form.GetField("perfilDisco",B).SetValue(null,2);Call(governor,g,"SetProfile",3);
   foreach(string route in new string[]{@"C:\one.jpg",@"C:\two.jpg",@"D:\one.jpg",@"D:\two.jpg"})Call(form,owner,"RequestCoverByPath",route,true);
   a=Call(form,owner,"TakeCoverJob",hi);b=Call(form,owner,"TakeCoverJob",hi);
   Check((string)Get(a,"Unit")!=(string)Get(b,"Unit") && Call(form,owner,"TakeCoverJob",hi)==null,"dos HDD mantienen un lector por unidad");
   Call(form,owner,"ReleaseCoverUnit",a);Call(form,owner,"ReleaseCoverUnit",b);Call(form,owner,"InvalidateCoverMemory");
   // Trabajadores reales detenidos ANTES de decodificar: probar el despertar sin tocar imagenes/datos reales.
   form.GetField("perfilDisco",B).SetValue(null,1);Set(owner,"coverPumpCount",0);Call(governor,g,"SetProfile",1);semaphore.SetValue(null,gate);
   foreach(string route in new string[]{@"C:\one.jpg",@"C:\two.jpg",@"D:\one.jpg",@"D:\two.jpg"})Call(form,owner,"RequestCoverByPath",route,true);
   WaitReaders(owner,2,false);Check((int)Get(owner,"coverPumpCount")==2,"cola despierta dos trabajadores reales en Fluidez");
   Set(owner,"caratulasCarga",3);Call(form,owner,"AplicaPerfilCargaCaratulas");WaitReaders(owner,4,false);
   Check((int)Get(owner,"coverPumpCount")==4,"cambio de perfil despierta trabajo pendiente sin otra peticion");
   Set(owner,"caratulasCarga",1);Call(form,owner,"AplicaPerfilCargaCaratulas");
   Check((int)Call(form,owner,"ActiveCoverReaders")==4 && Call(form,owner,"TakeCoverJob",hi)==null,"bajar perfil no cancela lecturas ni admite nuevas sobre el cupo");
   Call(form,owner,"InvalidateCoverMemory");gate.Release(8);draining=true;WaitReaders(owner,0,true);
   Check((int)Call(form,owner,"ActiveCoverReaders")==0,"trabajadores liberan cupos y terminan tras invalidar");
  }
  finally
  {
   if(!draining){Call(form,owner,"InvalidateCoverMemory");if((int)Get(owner,"coverPumpCount")!=Int32.MaxValue){gate.Release(8);WaitReaders(owner,0,true);}}
   semaphore.SetValue(null,oldSemaphore);gate.Close();form.GetField("coverCarriles",B).SetValue(null,lanes);form.GetField("perfilDisco",B).SetValue(null,disk);
  }
 }
 static void History(string root)
 {
  object owner=Codigo.FormSinEncender(form);Type scene=form.GetNestedType("EscenaMedida",B);object measure=null;
  for(int i=0;i<13;i++)
  {
   measure=New(scene);Set(measure,"PerfilCarga",i%4);Set(measure,"Ancho",3440);Set(measure,"Alto",1440);Set(measure,"Inicio","A/12");Set(measure,"Hora",new DateTime(2026,10,1,12,34,i));Set(measure,"MusicaReal",true);
   Set(measure,"P95",14.2d);Set(measure,"P99",33.1d);Set(measure,"Fps",60);Set(measure,"PreviasGpu",8);Set(measure,"VistasGpu",54);Call(form,owner,"RegisterCoverMeasurement",measure);
  }
  Check(((IList)Get(owner,"historialCaratulas")).Count==12,"historial acotado a doce pruebas por sesion");
  Set(owner,"caratulasCarga",0);Check(!(bool)Call(form,owner,"CoverMeasurementStale",measure),"medida identifica su perfil original");
  Set(owner,"caratulasCarga",1);Check((bool)Call(form,owner,"CoverMeasurementStale",measure) && ((string)Call(form,owner,"CoverMeasurementContext",measure)).StartsWith("Anterior:"),"medida de otro perfil queda marcada como anterior");
  using(Font normal=new Font("Segoe UI",10f,FontStyle.Bold))using(Font small=new Font("Segoe UI",8.5f))
  using(Panel page=(Panel)Call(form,owner,"BuildCoverHistory",normal,small,Color.White,Color.Gray))using(Bitmap image=new Bitmap(page.Width,page.Height))
  {
   bool fit=true;using(Graphics graphics=Graphics.FromImage(image))foreach(Control control in page.Controls)
   {
    fit &= control.Right<=page.Width && control.Bottom<=page.Height;
    if(control is Label)fit &= graphics.MeasureString(control.Text,control.Font,control.Width).Height<=control.Height;
    foreach(Control value in control.Controls)fit &= value.Right<=control.Width && value.Bottom<=control.Height && graphics.MeasureString(value.Text,value.Font,value.Width).Height<=value.Height;
   }
   Check(fit,"doce comparaciones y explicaciones caben con sus fuentes reales");page.DrawToBitmap(image,new Rectangle(Point.Empty,page.Size));image.Save(Path.Combine(root,"comparacion.png"));
  }
  using(Font normal=new Font("Segoe UI",10f,FontStyle.Bold))using(Font small=new Font("Segoe UI",8.5f))
  using(Panel page=(Panel)Call(form,owner,"PaginaImagen",normal,normal,small,normal,Color.White,Color.Gray,Color.DimGray))
  {
   Control button=Find(page,"Comparar perfiles");Check(button!=null && button.Enabled && button.Right<=page.Width && button.Bottom<=page.Height,"comparacion accesible desde Imagen sin recortar su boton");
   Check(((Delegate)Get(owner,"CoverProfileChanged")).GetInvocationList().Length==1,"pagina registra un aviso de cambio de perfil");
   page.Dispose();Check(Get(owner,"CoverProfileChanged")==null,"cerrar pagina elimina sus avisos y no retiene controles");
  }
  if(governor==null)return;
  using(Control gpu=(Control)New(panel))
  {
   gpu.Size=new Size(1024,768);Set(owner,"gpuPanel",gpu);Set(owner,"caratulasCarga",0);Check((bool)Call(form,owner,"CoverMeasurementStale",measure),"cambio de resolucion invalida la propuesta anterior");
   gpu.Size=new Size(3440,1440);Check(!(bool)Call(form,owner,"CoverMeasurementStale",measure),"misma resolucion y perfil recuperan contexto valido");
   object c=New(cell);Set(c,"Key","ready");Set(c,"Bmp",imageForReady);Set(c,"HasCover",true);
   Type texture=panel.GetNestedType("CellTex",B);object cached=New(texture);Set(cached,"Tex",new IntPtr(1));Set(cached,"HasCover",true);Set(cached,"Bmp",imageForReady);((IDictionary)Get(gpu,"cellTexCache"))["ready"]=cached;
   Check((bool)Call(panel,gpu,"PreparedCover",c),"GPU previa exige textura valida de la misma imagen");
   Set(cached,"Bmp",null);Check(!(bool)Call(panel,gpu,"PreparedCover",c),"textura vieja no se cuenta como caratula preparada");
   Set(cached,"Tex",IntPtr.Zero); // no liberar puntero ficticio al disponer
  }
 }
 static readonly Bitmap imageForReady=new Bitmap(1,1);
 static void Cache(string root)
 {
  string path=Path.Combine(root,"pack.bin");object s=New(store,path,18);
  using(Bitmap full=new Bitmap(256,256))
  {
   using(Graphics g=Graphics.FromImage(full)){g.Clear(Color.Magenta);g.FillRectangle(Brushes.Lime,0,0,128,256);}
   using(Bitmap small=(Bitmap)Call(store,null,"Small",full))
   {
    Check(small.Width==32 && small.Height==32,"LOD no hornea una celda de pantalla completa");
    byte[] bytes=(byte[])Call(store,null,"ToBytes",small);
    Call(store,s,"Save",Key(1),bytes);long length=new FileInfo(path).Length;
    Call(store,s,"Save",Key(1),bytes);Check(new FileInfo(path).Length==length,"no escribe en disco por cada uso");
    object again=New(store,path,18);
    using(Bitmap b=(Bitmap)Call(store,again,"Get",Key(1)))Check(b!=null && b.GetPixel(4,16).G==255 && b.GetPixel(28,16).R==255,"preview persistente conserva silueta y colores");
    Check(Call(store,again,"Get",Key(2))==null,"identidad distinta no recibe una imagen vieja");
    for(int i=2;i<180;i++)Call(store,s,"Save",Key(i),bytes);
    Check(((IDictionary)Get(s,"data")).Count==18,"memoria acotada tras muchas paginas");
    Check(new FileInfo(path).Length<150*4110,"archivo agrupado se compacta sin crecer sin final");
    using(FileStream fs=new FileStream(path,FileMode.Append))fs.WriteByte(7);
    object broken=New(store,path,18);
    using(Bitmap b=(Bitmap)Call(store,broken,"Get",Key(179)))Check(b!=null,"cola incompleta no pierde registros completos");
    Call(store,broken,"Save",Key(180),bytes);
    object recovered=New(store,path,18);
    using(Bitmap b=(Bitmap)Call(store,recovered,"Get",Key(180)))Check(b!=null,"escritura recupera archivo incompleto atomicamente");
   }
  }
  byte[] corrupt=File.ReadAllBytes(path);corrupt[16]^=127;File.WriteAllBytes(path,corrupt);
  object bad=New(store,path,18);
  string damaged=BitConverter.ToUInt64(corrupt,4).ToString("x16")+"_256.jpg";
  Check(Call(store,bad,"Get",damaged)==null,"checksum rechaza el registro corrupto");
  using(Bitmap b=(Bitmap)Call(store,bad,"Get",Key(180)))Check(b!=null,"registro corrupto no mata la cache completa");
 }
 static Image NoCover<T>(T a){return null;}
 static Image Lod<T>(T a){return preview;}
 static Delegate Provider(string name,string method){return Delegate.CreateDelegate(view.GetField(name,B).FieldType,typeof(CaratulasAdaptativas).GetMethod(method,B).MakeGenericMethod(album));}
 static void Grid()
 {
  using(Control v=(Control)New(view))using(Bitmap p=new Bitmap(32,32))
  {
   preview=p;v.Size=new Size(3440,1280);object a=New(album);Set(a,"Folder","lod");Set(a,"CoverPath","no-leer.jpg");Set(a,"Name","Nombre completo");Set(a,"Artist","Artista");
   ((IList)Get(v,"Page")).Add(a);Set(v,"GetCover",Provider("GetCover","NoCover"));Set(v,"PeekCoverGpu",Provider("PeekCoverGpu","NoCover"));Set(v,"PeekPreviewGpu",Provider("PeekPreviewGpu","Lod"));
   Array cells=Array.CreateInstance(cell,36);Call(view,v,"BuildGpuGrid",cells);
   Check(Object.ReferenceEquals(Get(cells.GetValue(0),"Preview"),preview) && !(bool)Get(cells.GetValue(0),"HasCover"),"preview separada de caratula final");
   Call(view,v,"StartSlide",1,3440);Set(v,"gpuLastSlideTick",Environment.TickCount);Call(view,v,"StartSlide",1,3440);Call(view,v,"BuildGpuGrid",cells);
   Check(Object.ReferenceEquals(Get(cells.GetValue(0),"Preview"),preview),"rafaga mantiene LOD sin leer original");
   Set(v,"PortadasLod",false);Call(view,v,"BuildGpuGrid",cells);Check(Get(cells.GetValue(0),"Preview")==null,"toggle visual desactiva preview");
   Check(((IDictionary)Get(v,"cellCache")).Count==0,"no utiliza cache ni render CPU");
   Set(v,"PeekCoverGpu",Provider("PeekCoverGpu","Lod"));
   Set(v,"slideTick",-1);
   IList ahead=(IList)Activator.CreateInstance(Get(v,"Page").GetType());ahead.Add(a);Call(view,v,"SetAhead",ahead);
   object governorObject=New(governor,16);Set(v,"CoverControl",governorObject);
   Set(governorObject,"pressure",1);Call(governor,governorObject,"Begin",10d,200);
   Set(v,"aheadBakeTicks",(long)(Stopwatch.Frequency*.010));
   Call(view,v,"BakeAheadGpu");Check((int)Get(v,"aheadPos")==0,"precarga aplazada no salta la casilla pendiente");
   Set(governorObject,"pressure",0);Call(governor,governorObject,"BeginBackground",100d);
   Call(view,v,"BakeAheadGpu");Check(((IDictionary)Get(v,"gpuCellCache")).Count==1 && (int)Get(v,"aheadPos")==1,"precarga pendiente progresa al recuperar margen");
   object cached=((IDictionary)Get(v,"gpuCellCache"))["lod"];long use=(long)Get(cached,"Uso");
   Call(governor,governorObject,"BeginBackground",150d);Call(view,v,"BakeAheadGpu");
   Check((long)Get(cached,"Uso")>use,"vecina reutilizada protege su lugar en la cache");
  }
  using(Control v=(Control)New(view))using(Bitmap p=new Bitmap(32,32))
  {
   preview=p;v.Size=new Size(640,480);Set(v,"PeekCoverGpu",Provider("PeekCoverGpu","Lod"));
   Array cells=Array.CreateInstance(cell,36);Call(view,v,"BuildGpuGrid",cells);
   IList ahead=(IList)Activator.CreateInstance(Get(v,"Page").GetType());
   for(int i=0;i<3;i++){object a=New(album);Set(a,"Folder","ahead-"+i);Set(a,"Name","Disco "+i);Set(a,"Artist","Artista");ahead.Add(a);}
   Call(view,v,"SetAhead",ahead);object g=New(governor,16);Call(governor,g,"SetProfile",3);Set(v,"CoverControl",g);
   Set(v,"aheadBakeTicks",(long)(Stopwatch.Frequency*.004));Set(v,"gpuBakeCost",(long)(Stopwatch.Frequency*.004));Call(governor,g,"BeginBackground",1000d);Call(view,v,"BakeAheadGpu");
   Check(((IDictionary)Get(v,"gpuCellCache")).Count==3,"precarga de casillas de 4 ms aprovecha presupuesto adaptativo sin tope fijo redundante");
  }
 }
 static void Hal()
 {
  if(omitHal || Environment.OSVersion.Platform!=PlatformID.Win32NT){Console.WriteLine("OMITIDA textura HAL: sin dispositivo solicitado o fuera de Windows; no confirma render GPU.");return;}
  using(Control host=new Control())using(Control p=(Control)New(panel))using(Bitmap bmp=new Bitmap(32,32))
  {
   host.Size=new Size(640,480);p.Size=host.Size;
   Type star=panel.Assembly.GetType("Rocola.GpuStarfield");object sky=New(star,host.Handle,640,480,4321);Set(p,"stars",sky);
   try
   {
    Check((bool)star.GetProperty("Ok",B).GetValue(sky,null),"HAL disponible");
    Array buffer=(Array)Get(p,"gridBuf");object c=New(cell);Set(c,"Key","hal-lod");Set(c,"Preview",bmp);Set(c,"A",1f);Set(c,"CoverRect",new RectangleF(30,30,220,220));buffer.SetValue(c,0);Set(p,"gridN",1);
    Call(panel,p,"PrepareLod",0,false);IntPtr tex=((IntPtr[])Get(p,"lodTex"))[0];Check(tex!=IntPtr.Zero,"LOD sube textura real 32x32");
    Call(panel,p,"PrepareLod",0,false);Check(((IntPtr[])Get(p,"lodTex"))[0]==tex,"LOD no resube textura por cuadro");
    Call(star,sky,"Begin",640,480,0f,0f);Call(panel,p,"DrawLod",false);Check((bool)Call(star,sky,"End"),"LOD dibuja cubo con textura real");
    Call(panel,p,"PrepareLod",0,true);Check(((float[])Get(p,"lodAlpha"))[0]>.9f,"transicion comienza sin salto");
    System.Threading.Thread.Sleep(160);
    Call(panel,p,"PrepareLod",0,true);Check(((float[])Get(p,"lodAlpha"))[0]==0,"imagen final elimina preview tras fundido");
    Call(panel,p,"TrimLod",true);Check(((IDictionary)Get(p,"lodTextures")).Count==0,"reset libera todas las texturas LOD");
   }
   finally {Call(panel,p,"StopGpu");}
  }
 }
 [STAThread]static int Main(string[] args)
 {
  try
  {
   for(int i=1;i<args.Length;i++){if(args[i]=="--dos-hilos")Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(3);else if(args[i]=="--sin-hal")omitHal=true;else throw new ArgumentException("Opcion de prueba desconocida: "+args[i]);}
   Assembly a=Assembly.LoadFrom(args[0]);governor=a.GetType("Rocola.CoverGovernor");store=a.GetType("Rocola.GpuPreviewStore");panel=a.GetType("Rocola.GpuPanel");cell=a.GetType("Rocola.GpuGridCell");form=a.GetType("Rocola.Form1");view=form.GetNestedType("AlbumView",B);album=form.GetNestedType("Album",B);
   string root=Path.Combine(Path.GetTempPath(),"rocola-lod-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   Profiles(root);History(root);Console.WriteLine("Artefactos de prueba: "+root);
   if(governor!=null){Regulator();TimedLearning();Readers();Cache(root);Grid();Hal();}
   Console.WriteLine("PASS "+checks+" comprobaciones; no equivale a XP/HDD/CPU antiguos ni FPS de biblioteca.");return 0;
  }
  catch(Exception e){Console.WriteLine("FALLO "+e);return 1;}
 }
}
