using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
class PartesCaratulas {
 const BindingFlags B=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static Type F,A,V,G,S,D,X; static bool queued; static Bitmap cover; static int checks;
 static object Get(object o,string n){return o.GetType().GetField(n,B).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,B).SetValue(o,v);}
 static object Call(Type t,object o,string n,params object[] p){try{return t.GetMethod(n,B).Invoke(o,p);}catch(TargetInvocationException e){throw e.InnerException;}}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("OK "+message);}
 static bool Q<T>(T a){return queued;} static Image Cover<T>(T a){return cover;}
 static Delegate Provider(string field,string method){return Delegate.CreateDelegate(V.GetField(field,B).FieldType,typeof(PartesCaratulas).GetMethod(method,B).MakeGenericMethod(A));}
 static int[] Pixels(Bitmap b){int[] p=new int[b.Width*b.Height];BitmapData d=b.LockBits(new Rectangle(Point.Empty,b.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);try{for(int y=0;y<b.Height;y++)Marshal.Copy(new IntPtr(d.Scan0.ToInt64()+y*(long)d.Stride),p,y*b.Width,b.Width);}finally{b.UnlockBits(d);}return p;}
 static void Paste(Graphics g,Bitmap atlas,object plan,string part,bool rotate){
  Rectangle src=(Rectangle)Get(plan,part),at=(Rectangle)Get(plan,part+"At");if(src.Width==0||src.Height==0)return;
  using(Bitmap region=atlas.Clone(src,PixelFormat.Format32bppPArgb)){
   if(rotate){for(int y=0;y<at.Height;y++)g.DrawImageUnscaled(region,at.X,at.Y+y);}
   else g.DrawImageUnscaled(region,at.Location);
  }
 }
 static long Pot(int w,int h){long x=1,y=1;while(x<w)x<<=1;while(y<h)y<<=1;return 4*x*y;}
 static void BakeBench(object view,object album){
  queued=false;
  for(int pass=0;pass<3;pass++){
   double oldMs=0,newMs=0;
   foreach(bool separated in pass%2==0?new bool[]{false,true}:new bool[]{true,false}){
    System.Diagnostics.Stopwatch t=System.Diagnostics.Stopwatch.StartNew();
    for(int i=0;i<18;i++){
     object[] p=separated?new object[]{album,573,385,38,3,null}:new object[]{album,573,385,38,3,cover,null};
     using(Bitmap b=(Bitmap)Call(V,view,separated?"BakeSeparatedCell":"BakeGpuCell",p)){}
    }
    if(separated)newMs=t.Elapsed.TotalMilliseconds;else oldMs=t.Elapsed.TotalMilliseconds;
   }
   Console.WriteLine("18 composiciones CPU a 3440: atlas anterior "+oldMs.ToString("F2")+" ms; etiquetas separadas "+newMs.ToString("F2")+" ms. No mide FPS ni lectura de biblioteca.");
  }
 }
 // SONDA DE LA LIBRERIA GRAFICA (Mono/libgdiplus). Con ella, geometria/borde/marca y texto dan rojo aqui SIN que el programa este roto:
 // el camino nuevo (BakeSeparatedCell) compone el atlas con dos operaciones que libgdiplus hace distinto de GDI+ de Windows,
 // y que el camino viejo (BakeCell) no usa. Comprobado el 24-10-2026 con programas sueltos y con el commit f4b9699 (donde nacio
 // esta bateria: sale el mismo "dif=1905 max=255"):
 //  (1) DrawImage(...) con CompositingMode.SourceCopy BORRA todo el destino fuera del rectangulo dibujado (operador SOURCE de cairo
 //      sin limite). PackEdge copia cuatro tiras seguidas con SourceCopy: cada una borra la anterior y el atlas queda sin bordes.
 //  (2) LockBits de un sub-rectangulo con origen distinto de (0,0) en el mismo formato (PArgb) devuelve Scan0 SIN desplazar al
 //      origen y con el stride completo: lo escrito cae en (0,0) y desborda (CopiaEtiquetaGpu escribe la etiqueta asi).
 //      Con las etiquetas copiadas con un LockBits de bitmap completo y el desplazamiento a mano, dif baja de 1905 a 361 y el resto es (1).
 // En Windows ninguna de las dos pasa. Si la sonda las detecta, esas dos comprobaciones salen como AVISO (no comprobado) en vez de
 // reventar; el resto de la bateria sigue. Eso NO prueba el aspecto: hay que mirar esos pixeles en Windows.
 static bool gdiMalo;
 static int Opacos(Bitmap b){int n=0;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A!=0)n++;return n;}
 static void Sonda(){
  bool borra,desplaza;
  using(Bitmap a=new Bitmap(50,2,PixelFormat.Format32bppPArgb))using(Bitmap t=new Bitmap(120,40,PixelFormat.Format32bppPArgb)){
   using(Graphics g=Graphics.FromImage(a))g.Clear(Color.FromArgb(120,0,0,0));
   using(Graphics g=Graphics.FromImage(t)){g.Clear(Color.Transparent);g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(a,new Point(1,1));}
   using(Graphics g=Graphics.FromImage(t)){g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(a,new Point(1,10));}
   borra=Opacos(t)!=200;
  }
  using(Bitmap t=new Bitmap(100,60,PixelFormat.Format32bppPArgb)){
   using(Graphics g=Graphics.FromImage(t))g.Clear(Color.Transparent);
   Rectangle r=new Rectangle(10,5,40,20);BitmapData d=t.LockBits(r,ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
   byte[] row=new byte[r.Width*4];for(int i=0;i<row.Length;i++)row[i]=255;
   for(int y=0;y<r.Height;y++)Marshal.Copy(row,0,new IntPtr(d.Scan0.ToInt64()+(long)y*d.Stride),row.Length);
   t.UnlockBits(d);
   int dentro=0,fuera=0;for(int y=0;y<60;y++)for(int x=0;x<100;x++)if(t.GetPixel(x,y).A!=0){if(r.Contains(x,y))dentro++;else fuera++;}
   desplaza=dentro!=r.Width*r.Height||fuera!=0;
  }
  gdiMalo=borra||desplaza;
  if(gdiMalo)Console.WriteLine("AVISO libgdiplus: SourceCopy borra fuera del rectangulo="+borra+"; LockBits de sub-rectangulo escribe fuera de sitio="+desplaza+". geometria/borde/marca y texto NO se comprueban en esta maquina (ver Sonda).");
  else Console.WriteLine("OK la libreria grafica compone el atlas como GDI+ (sonda de SourceCopy y LockBits de sub-rectangulo)");
 }
 static void Images(string output){
  Sonda();
  object a=Activator.CreateInstance(A,true);Set(a,"Folder","test");Set(a,"Name","Nombre largo de un disco");Set(a,"Artist","Artista");
  using(Control view=(Control)Activator.CreateInstance(V,true)){
   Set(view,"EnCola",Provider("EnCola","Q"));Set(view,"PortadasLod",false);
   foreach(Size size in new Size[]{new Size(1024,768),new Size(1280,720),new Size(1920,1080),new Size(2560,1440),new Size(3440,1440),new Size(3840,2160),new Size(3440,768)}){
    int cw=size.Width/6;object[] metrics={cw,(size.Height-160)/3,0,0,0};Call(V,null,"CellMetrics",metrics);int side=(int)metrics[2],lh=(int)metrics[3],gap=(int)metrics[4],h=side+gap+lh;
    foreach(bool q in new bool[]{false,true}){
     queued=q;object[] args={a,cw,side,lh,gap,null};
     using(Bitmap atlas=(Bitmap)Call(V,view,"BakeSeparatedCell",args))
     using(Bitmap previous=(Bitmap)V.GetMethod("BakeCell",B,null,new Type[]{A,typeof(int),typeof(int),typeof(int),typeof(int),typeof(Image)},null).Invoke(view,new object[]{a,cw,side,lh,gap,cover}))
     using(Bitmap rebuilt=new Bitmap(cw,h,PixelFormat.Format32bppPArgb)){
      object plan=args[5];
      using(Graphics g=Graphics.FromImage(rebuilt)){
       g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.Bilinear;g.SmoothingMode=SmoothingMode.AntiAlias;
       Rectangle face=(Rectangle)Call(G,null,"StaticFace",new RectangleF((cw-side)/2,0,side,side));
       g.DrawImage(cover,face);
       foreach(string part in new string[]{"Top","Bottom","Left","Right","Face","Text1","Text2"})Paste(g,atlas,plan,part,part=="Left"||part=="Right");
      }
      int[] x=Pixels(previous),y=Pixels(rebuilt);int max=0,diffs=0,textMax=0;
      for(int i=0;i<x.Length;i++)if(x[i]!=y[i]){diffs++;for(int b=0;b<32;b+=8){int d=Math.Abs(((x[i]>>b)&255)-((y[i]>>b)&255));max=Math.Max(max,d);if(i/cw>=side+gap)textMax=Math.Max(textMax,d);}}
      if(max>1){previous.Save(Path.Combine(output,"antes.png"));rebuilt.Save(Path.Combine(output,"despues.png"));for(int i=0,n=0;i<x.Length&&n<12;i++)if(x[i]!=y[i]){Console.WriteLine("diff "+i%cw+","+i/cw+" "+x[i].ToString("X8")+" "+y[i].ToString("X8"));n++;}}
      if(gdiMalo)Console.WriteLine("AVISO sin comprobar (libgdiplus defectuoso, ver Sonda): geometria/borde/marca y texto "+size+" cola="+q+" dif="+diffs+" max="+max+" textMax="+textMax);
      else{
       Check(max<=1,"geometria/borde/marca "+size+" cola="+q+" dif="+diffs+" max="+max);
       Check(textMax==0,"texto conserva pixeles originales");
      }
      Console.WriteLine("POT "+size+" anterior="+Pot(cw,h)+" etiqueta="+Pot(atlas.Width,atlas.Height)+" portada="+Pot(256,256));
      if(size.Width>=1920)Check(Pot(atlas.Width,atlas.Height)+Pot(256,256)<=Pot(cw,h),"memoria no aumenta a resolucion alta");
      Check((bool)Get(plan,"Separate"),"plano usa portada separada");
      Type cell=G.Assembly.GetType("Rocola.GpuGridCell"),vertex=G.Assembly.GetType("Rocola.D3D9+Vtx");
      object c=Activator.CreateInstance(cell);Set(c,"Layout",plan);Set(c,"W",(float)cw*1.2f);Set(c,"H",(float)h*1.2f);Set(c,"CoverReady",true);
      Array vertices=Array.CreateInstance(vertex,42);Call(G,null,"AtlasCellVertices",c,1f,1f,0xFFFFFFFFu,vertices);
      for(int i=0;i<42;i++){float u=(float)Get(vertices.GetValue(i),"U"),v=(float)Get(vertices.GetValue(i),"V");if(u<0||u>1||v<0||v>1)throw new Exception("UV fuera de atlas");}
     }
    }
   }
   BakeBench(view,a);
   Type cellType=G.Assembly.GetType("Rocola.GpuGridCell");Array cells=Array.CreateInstance(cellType,36);view.Size=new Size(3440,1280);Set(view,"PeekCoverGpu",Provider("PeekCoverGpu","Cover"));Set(view,"GetCover",Provider("GetCover","Cover"));((IList)Get(view,"Page")).Add(a);
   queued=false;for(int i=0;i<6;i++)Call(V,view,"BuildGpuGrid",cells);Bitmap label=(Bitmap)Get(cells.GetValue(0),"Bmp");Check(label!=null,"etiqueta preparada");
   Bitmap original=cover;using(Bitmap other=new Bitmap(256,256)){cover=other;Call(V,view,"BuildGpuGrid",cells);Check(ReferenceEquals(label,Get(cells.GetValue(0),"Bmp"))&&ReferenceEquals(other,Get(cells.GetValue(0),"FullCover")),"cambiar portada no recompone etiquetas");cover=original;}
  }
 }
 static string Key(int i){return i.ToString("x16")+"_256.jpg";}
 static object Store(string path,int cap){return Activator.CreateInstance(S,B,null,new object[]{path,cap},null);}
 static void Index(string output){
  string path=Path.Combine(output,"pack.bin");object store=Store(path,256);
  using(Bitmap small=(Bitmap)Call(S,null,"Small",cover)){byte[] data=(byte[])Call(S,null,"ToBytes",small);for(int i=0;i<256;i++){byte[] unique=(byte[])data.Clone();unique[0]=(byte)i;unique[3]=255;Call(S,store,"Save",Key(i),unique);}}
  Check(File.Exists(path+".idx"),"checkpoint de indice persistente");
  object next=Store(path,256);Call(S,next,"Load");Check((long)Get(next,"PixelBytesRead")==0,"arranque del indice no carga pixeles");
  Check((long)Get(next,"IndexBytesRead")<new FileInfo(path).Length/10,"metadatos menores que pack");
  Bitmap[] batch=(Bitmap[])Call(S,next,"GetBatch",(object)new string[]{Key(255),Key(3),Key(300),Key(128)});
  Check(batch[0]!=null&&batch[0].GetPixel(0,0).B==255&&batch[1]!=null&&batch[1].GetPixel(0,0).B==3&&batch[3]!=null&&batch[3].GetPixel(0,0).B==128,"ordenar lecturas no intercambia caratulas");
  for(int i=0;i<batch.Length;i++)if(batch[i]!=null)batch[i].Dispose();
  Check(batch[0]!=null&&batch[1]!=null&&batch[2]==null&&batch[3]!=null,"lectura ordenada respeta identidades y huecos");
  using(FileStream f=new FileStream(path+".idx",FileMode.Open,FileAccess.Write)){f.Position=5;f.WriteByte(201);}
  object corrupt=Store(path,256);Call(S,corrupt,"Load");Check((long)Get(corrupt,"PixelBytesRead")==0,"indice corrupto cae a cabeceras sin pixeles");
  using(Bitmap b=(Bitmap)Call(S,corrupt,"Get",Key(255)))Check(b!=null,"recupera pack con indice corrupto");
  using(FileStream f=new FileStream(path,FileMode.Append,FileAccess.Write))f.WriteByte(5);
  object tail=Store(path,256);using(Bitmap b=(Bitmap)Call(S,tail,"Get",Key(128)))Check(b!=null,"cola truncada no destruye registros");
  using(Bitmap small=(Bitmap)Call(S,null,"Small",cover))Call(S,tail,"Save",Key(256),Call(S,null,"ToBytes",small));
  object after=Store(path,256);using(Bitmap b=(Bitmap)Call(S,after,"Get",Key(128)))Check(b!=null,"compacta conservando entradas no leidas");
  using(Bitmap b=(Bitmap)Call(S,after,"Get",Key(256)))Check(b!=null,"compacta conservando nueva entrada");
  Check(new FileInfo(path).Length==4+256L*4108,"compactacion acotada");
  Check((long)Call(G,null,"LiveCellBudget",0u,32,4,0UL)==32*1024*1024L,"memoria desconocida conserva 32 MiB");
  Check((long)Call(G,null,"LiveCellBudget",uint.MaxValue,256,8,128UL*1024*1024)==16*1024*1024L,"presion RAM reduce cache sin bajar de 16 MiB");
  Check((long)Call(G,null,"LiveCellBudget",uint.MaxValue,256,8,0UL)==128*1024*1024L,"equipo potente no queda limitado a 32 MiB");
 }
 static void WaitWorker(object owner,int count,bool threads){
  System.Diagnostics.Stopwatch t=System.Diagnostics.Stopwatch.StartNew();
  while(t.ElapsedMilliseconds<3000){lock(Get(owner,"coverLock")){if((int)(threads?Get(owner,"coverPumpCount"):Call(F,owner,"ActiveCoverReaders"))==count)return;}System.Threading.Thread.Sleep(1);}
  throw new Exception("Trabajadores no convergen: "+count);
 }
 static void Workers(string root){
  object owner=Codigo.FormSinEncender(F);Type governor=F.Assembly.GetType("Rocola.CoverGovernor");
  object g=Activator.CreateInstance(governor,B,null,new object[]{16},null);Set(owner,"gpuEnabled",true);Set(owner,"coverGovernor",g);Set(g,"ReaderLimit",1);Call(governor,g,"SetProfile",1);
  FieldInfo sem=F.GetField("coverSem",B),lanes=F.GetField("coverCarriles",B),disk=F.GetField("perfilDisco",B);
  object old=sem.GetValue(null),oldLanes=lanes.GetValue(null),oldDisk=disk.GetValue(null);
  using(System.Threading.Semaphore gate=new System.Threading.Semaphore(0,8)){
   bool draining=false;
   try{
    sem.SetValue(null,gate);lanes.SetValue(null,1);disk.SetValue(null,1);
    Call(F,owner,"RequestCoverByPath",Path.Combine(root,"inexistente-1.jpg"),true);
    WaitWorker(owner,1,false);int started=(int)Get(owner,"coverWorkersStarted");gate.Release();WaitWorker(owner,0,false);
    Check((int)Get(owner,"coverPumpCount")==1,"trabajador permanece dormido para siguiente pagina");
    Check(gate.WaitOne(0),"retirar permiso devuelto antes de segunda solicitud");
    Call(F,owner,"RequestCoverByPath",Path.Combine(root,"inexistente-2.jpg"),true);WaitWorker(owner,1,false);
    Check((int)Get(owner,"coverWorkersStarted")==started,"siguiente solicitud reutiliza hilo existente");
    Call(F,owner,"StopCoverReaders");gate.Release();draining=true;WaitWorker(owner,0,true);
    Check((int)Call(F,owner,"ActiveCoverReaders")==0,"cierre despierta y libera trabajadores/cupos");
   }finally{
    if(!draining){Call(F,owner,"StopCoverReaders");try{gate.Release();}catch(System.Threading.SemaphoreFullException){}WaitWorker(owner,0,true);}
    sem.SetValue(null,old);lanes.SetValue(null,oldLanes);disk.SetValue(null,oldDisk);
   }
  }
 }
 [StructLayout(LayoutKind.Sequential)]struct Desc{public uint Format,Type,Usage,Pool,Multi,Quality,Width,Height;}
 [StructLayout(LayoutKind.Sequential)]struct Rect{public int Pitch;public IntPtr Data;}
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Hr(IntPtr self);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate uint Rel(IntPtr self);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Back(IntPtr self,uint a,uint b,uint c,out IntPtr surface);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Description(IntPtr self,out Desc desc);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Offscreen(IntPtr self,uint w,uint h,uint format,uint pool,out IntPtr surface,IntPtr shared);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Readback(IntPtr self,IntPtr source,IntPtr dest);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Lock(IntPtr self,out Rect r,IntPtr region,uint flags);
 static T Fn<T>(IntPtr p,int slot)where T:class{return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(p),slot*IntPtr.Size),typeof(T));}
 static void Release(IntPtr p){if(p!=IntPtr.Zero)Fn<Rel>(p,2)(p);}
 static int[] Pixels(object d)
 {
  IntPtr dev=(IntPtr)Get(d,"dev"),back=IntPtr.Zero,off=IntPtr.Zero;bool locked=false;
  Check(Fn<Hr>(dev,42)(dev)>=0,"EndScene HAL");Set(d,"inScene",false);Call(D,d,"SueltaPinesVertices");
  try{
   Check(Fn<Back>(dev,18)(dev,0,0,0,out back)>=0,"backbuffer HAL");Desc desc;Check(Fn<Description>(back,12)(back,out desc)>=0,"descripcion HAL");
   Check(Fn<Offscreen>(dev,36)(dev,desc.Width,desc.Height,desc.Format,2,out off,IntPtr.Zero)>=0,"superficie HAL");
   Check(Fn<Readback>(dev,32)(dev,back,off)>=0,"readback HAL");Rect r;Check(Fn<Lock>(off,13)(off,out r,IntPtr.Zero,0x10)>=0,"lock HAL");locked=true;
   int[] p=new int[desc.Width*desc.Height];for(int y=0;y<desc.Height;y++)Marshal.Copy(new IntPtr(r.Data.ToInt64()+(long)y*r.Pitch),p,y*(int)desc.Width,(int)desc.Width);return p;
  }finally{if(locked)Fn<Hr>(off,14)(off);Release(off);Release(back);}
 }
 static Array Quad(float x,float y,int w,int h,float u,float v,uint color)
 {
  Array r=Array.CreateInstance(X,6);int[] xs={0,w,w,0,w,0},ys={0,0,h,0,h,h};
  for(int i=0;i<6;i++){object z=Activator.CreateInstance(X);Set(z,"X",x+xs[i]-.5f);Set(z,"Y",y+ys[i]-.5f);Set(z,"Rhw",1f);Set(z,"Color",color);Set(z,"U",xs[i]==0?0f:u);Set(z,"V",ys[i]==0?0f:v);r.SetValue(z,i);}return r;
 }

 static void SavePixels(int[] pixels,string path){
  using(Bitmap b=new Bitmap(800,800,PixelFormat.Format32bppArgb)){BitmapData d=b.LockBits(new Rectangle(0,0,800,800),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
   try{for(int y=0;y<800;y++)Marshal.Copy(pixels,y*800,new IntPtr(d.Scan0.ToInt64()+y*(long)d.Stride),800);}finally{b.UnlockBits(d);}b.Save(path);
  }
 }
 static void NativeMemory(object panel){
  Type cell=G.Assembly.GetType("Rocola.GpuGridCell");Set(panel,"cellFrame",100);
  object visible=Activator.CreateInstance(cell);Set(visible,"Direct",true);Set(visible,"FullCover",cover);
  object[] keep={visible,4,true,0f,0f};Call(G,panel,"PrepareNativeCover",keep);
  using(Bitmap unused=new Bitmap(256,256))using(Bitmap next=new Bitmap(256,256))using(Bitmap label=new Bitmap(32,32)){
   object old=Activator.CreateInstance(cell);Set(old,"Direct",true);Set(old,"FullCover",unused);object[] prepare={old,4,false,0f,0f};
   Check((IntPtr)Call(G,panel,"PrepareNativeCover",prepare)!=IntPtr.Zero,"textura vecina real para prueba de memoria");
   object protectedSet=Get(panel,"protectedNativeAhead");Call(protectedSet.GetType(),protectedSet,"Add",unused);
   object incoming=Activator.CreateInstance(cell);Set(incoming,"Key","warm-memory");Set(incoming,"Bmp",label);Set(incoming,"FullCover",next);Set(incoming,"Direct",true);
   Set(panel,"cellTextureBudget",2*256*256*4L+32*32*4);
   Check(!(bool)Call(G,panel,"ReserveAheadSpace",incoming,0),"precarga no expulsa visible ni vecina protegida");
   Call(protectedSet.GetType(),protectedSet,"Clear");
   Check((bool)Call(G,panel,"ReserveAheadSpace",incoming,0),"precarga puede liberar portada historica sin etiqueta residual");
   Check((bool)Call(G,panel,"NativeCoverReady",visible)&&!(bool)Call(G,panel,"NativeCoverReady",old),"poda conserva visible y libera solo historica");
   Check((long)Call(G,panel,"CachedCellBytes")==256*256*4L,"contador incluye portada nativa y descuenta la liberada");
   Set(panel,"cellTextureBudget",32L*1024*1024);
  }
 }
 static void Hal(string output){
  using(Control host=new Control())using(Control panel=(Control)Activator.CreateInstance(G,true))using(Control view=(Control)Activator.CreateInstance(V,true)){
   host.Size=new Size(800,800);panel.Size=host.Size;Type star=G.Assembly.GetType("Rocola.GpuStarfield");object sky=Activator.CreateInstance(star,B,null,new object[]{host.Handle,800,800,4321},null);
   Set(panel,"stars",sky);object device=Get(sky,"gpu");D=device.GetType();X=G.Assembly.GetType("Rocola.D3D9+Vtx");
   try{
    Check((bool)star.GetProperty("Ok",B).GetValue(sky,null),"HAL disponible para portada separada");
    Set(view,"EnCola",Provider("EnCola","Q"));Set(view,"PortadasLod",false);object album=Activator.CreateInstance(A,true);Set(album,"Folder","hal");Set(album,"Name","Nombre largo de un disco");Set(album,"Artist","Artista");
    foreach(int side in new int[]{139,385,584})foreach(bool q in new bool[]{false,true}){
     int cw=side==139?170:side==385?573:640,lh=38,gap=3;queued=q;
     object[] oa={album,cw,side,lh,gap,cover,null},na={album,cw,side,lh,gap,null};
     using(Bitmap old=(Bitmap)Call(V,view,"BakeGpuCell",oa))using(Bitmap label=(Bitmap)Call(V,view,"BakeSeparatedCell",na)){
      object[] otArgs={old,0f,0f},ltArgs={label,0f,0f};IntPtr ot=(IntPtr)Call(star,sky,"CreateOverlay",otArgs),lt=(IntPtr)Call(star,sky,"CreateOverlay",ltArgs);
      try{
       Check(ot!=IntPtr.Zero&&lt!=IntPtr.Zero,"subida HAL de etiquetas y referencia");
       Type cell=G.Assembly.GetType("Rocola.GpuGridCell");object c=Activator.CreateInstance(cell);
       Set(c,"Key","native");Set(c,"Bmp",label);Set(c,"Layout",na[5]);Set(c,"FullCover",cover);Set(c,"Direct",true);Set(c,"CoverReady",true);Set(c,"W",(float)cw);Set(c,"H",(float)(side+gap+lh));
       object[] nativeArgs={c,8,true,0f,0f};IntPtr nt=(IntPtr)Call(G,panel,"PrepareNativeCover",nativeArgs);Check(nt!=IntPtr.Zero,"subida HAL de portada nativa");
       ((IntPtr[])Get(panel,"nativeGridTex"))[0]=nt;((float[])Get(panel,"nativeGridU"))[0]=(float)nativeArgs[3];((float[])Get(panel,"nativeGridV"))[0]=(float)nativeArgs[4];
       object[] again={c,0,true,0f,0f};Check((IntPtr)Call(G,panel,"PrepareNativeCover",again)==nt&&(int)again[1]==0,"caratula reutiliza textura sin presupuesto ni copia");
       foreach(float dx in new float[]{0f,.25f,.5f,.75f})foreach(uint color in new uint[]{0xFFFFFFFFu,0x88777777u}){
        float px=30+dx,py=30+.25f;Array bg=Array.CreateInstance(X,12),quad=Quad(0,0,800,800,1,1,0xFF182848u);Array.Copy(quad,bg,6);Array.Copy(quad,0,bg,6,6);
        Set(c,"X",px);Set(c,"Y",py);Set(c,"CoverRect",new RectangleF(px+(cw-side)/2,py,side,side));
        Check((bool)Call(D,device,"BeginFrame"),"Begin referencia HAL");Call(D,device,"DrawSky",bg);
        object reference=Activator.CreateInstance(cell);Set(reference,"X",px);Set(reference,"Y",py);Set(reference,"W",(float)cw);Set(reference,"H",(float)(side+gap+lh));Set(reference,"Layout",oa[6]);
        if((int)Get(oa[6],"Width")==0)Call(D,device,"DrawTexQuad",ot,Quad(px,py,cw,side+gap+lh,(float)otArgs[1],(float)otArgs[2],color));
        else Call(G,panel,"DrawAtlasCell",ot,reference,(float)otArgs[1],(float)otArgs[2],color);
        int[] before=Pixels(device);
        Check((bool)Call(D,device,"BeginFrame"),"Begin separado HAL");Call(D,device,"DrawSky",bg);Call(G,panel,"DrawNativeCover",0,c,color);Call(G,panel,"DrawAtlasCell",lt,c,(float)ltArgs[1],(float)ltArgs[2],color);int[] after=Pixels(device);
        double square=0;int n=0,textMax=0,max=0;
        for(int y=0;y<800;y++)for(int x=0;x<800;x++){int i=y*800+x;for(int b=0;b<24;b+=8){int diff=Math.Abs(((before[i]>>b)&255)-((after[i]>>b)&255));square+=diff*diff;n++;max=Math.Max(max,diff);if(y>=Math.Ceiling(py+side+gap)&&y<py+side+gap+lh)textMax=Math.Max(textMax,diff);}}
        double rmse=Math.Sqrt(square/n);Console.WriteLine("HAL lado="+side+" cola="+q+" dx="+dx+" color="+color+" RMSE="+rmse.ToString("F3")+" max="+max+" texto="+textMax);
        Check(textMax<=1,"texto HAL conserva legibilidad durante desplazamiento/fundido");
        if(side==584&&dx==0&&color==0xFFFFFFFFu){SavePixels(before,Path.Combine(output,"hal-antes.png"));SavePixels(after,Path.Combine(output,"hal-despues.png"));Console.WriteLine("Comparacion visual: "+output);}
        // La portada ahora se interpola una vez en D3D, antes se interpolaba en GDI y luego D3D.
        // No exigir igualdad de filtros: controlar error visual (PSNR > 36 dB), texto exacto
        // y mantener la comparacion software del contorno/geometria con redondeo <= 1.
        Check(rmse<4,"composicion HAL mantiene aspecto global (PSNR > 36 dB)");
       }
      }finally{Release(ot);Release(lt);}
     }
    }
    NativeMemory(panel);
   }finally{Call(G,panel,"StopGpu");}
  }
 }
 [STAThread]static int Main(string[] args){try{
  if(Array.IndexOf(args,"--dos-hilos")>=0)System.Diagnostics.Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(3);
  Assembly asm=Assembly.LoadFile(Path.GetFullPath(args[0]));F=asm.GetType("Rocola.Form1",true);A=F.GetNestedType("Album",B);V=F.GetNestedType("AlbumView",B);G=asm.GetType("Rocola.GpuPanel",true);S=asm.GetType("Rocola.GpuPreviewStore",true);
  string output=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0])),"partes-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
  using(cover=new Bitmap(256,256,PixelFormat.Format32bppPArgb)){using(Graphics g=Graphics.FromImage(cover)){g.Clear(Color.SteelBlue);g.FillEllipse(Brushes.White,12,20,140,200);g.FillRectangle(Brushes.Pink,190,0,66,256);}Images(output);Index(output);Workers(output);
   if(Environment.OSVersion.Platform==PlatformID.Win32NT&&Array.IndexOf(args,"--sin-hal")<0)Hal(output);else Console.WriteLine("OMITIDA comparacion HAL: no confirma aspecto GPU real.");
  }
  Console.WriteLine("PASS "+checks+" pruebas de partes/indice/memoria"+(gdiMalo?" (CON AVISO: geometria/borde/marca y texto SIN comprobar por el libgdiplus de esta maquina)":"")+"; salida="+output);return 0;
 }catch(Exception e){Console.WriteLine("FALLO "+e);return 1;}}
}
