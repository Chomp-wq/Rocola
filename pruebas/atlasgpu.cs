using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
class AtlasGpu
{
 const BindingFlags B=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 static Type F,A,V,C,G,D,X; static int checks; static Bitmap cover; static bool queued;
 static object Get(object o,string n){return o.GetType().GetField(n,B).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,B).SetValue(o,v);}
 static object Call(Type t,object o,string n,params object[] p){try{return t.GetMethod(n,B).Invoke(o,p);}catch(TargetInvocationException e){throw e.InnerException;}}
 static void Check(bool b,string s){if(!b)throw new Exception(s);checks++;Console.WriteLine("OK "+s);}
 static bool Q<T>(T a){return queued;}
 static Image Cover<T>(T a){return cover;}
 static Delegate Provider(Type t,string field,string method){return Delegate.CreateDelegate(t.GetField(field,B).FieldType,typeof(AtlasGpu).GetMethod(method,B).MakeGenericMethod(A));}
 static long Pot(int w,int h){long x=1,y=1;while(x<w)x<<=1;while(y<h)y<<=1;return x*y*4;}
 static int[] Bytes(Bitmap b)
 {
  BitmapData p=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
  try{int[] r=new int[b.Width*b.Height];for(int y=0;y<b.Height;y++)Marshal.Copy(new IntPtr(p.Scan0.ToInt64()+(long)y*p.Stride),r,y*b.Width,b.Width);return r;}
  finally{b.UnlockBits(p);}
 }
 static Bitmap Expand(Bitmap atlas,object plan,int w,int h)
 {
  if((int)Get(plan,"Width")==0)return (Bitmap)atlas.Clone();
  Bitmap result=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
  int[] data=Bytes(atlas),r=new int[w*h];
  foreach(string name in new string[]{"Face","Text1","Text2"})
  {
   Rectangle s=(Rectangle)Get(plan,name),d=(Rectangle)Get(plan,name+"At");
   for(int y=0;y<s.Height;y++)Array.Copy(data,(s.Y+y)*atlas.Width+s.X,r,(d.Y+y)*w+d.X,s.Width);
  }
  BitmapData p=result.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
  try{for(int y=0;y<h;y++)Marshal.Copy(r,y*w,new IntPtr(p.Scan0.ToInt64()+(long)y*p.Stride),w);}
  finally{result.UnlockBits(p);}return result;
 }
 static void Same(Bitmap a,Bitmap b,string msg)
 {
  int[] x=Bytes(a),y=Bytes(b);int n=0,max=0;for(int i=0;i<x.Length;i++)if(x[i]!=y[i]){n++;for(int s=0;s<32;s+=8)max=Math.Max(max,Math.Abs(((x[i]>>s)&255)-((y[i]>>s)&255)));}
  if(max>1)for(int i=0,shown=0;i<x.Length&&shown<10;i++)if(x[i]!=y[i]){Console.WriteLine("diff "+i%a.Width+","+i/a.Width+" "+x[i].ToString("X8")+" -> "+y[i].ToString("X8"));shown++;}
  Check(max<=1 && n<x.Length/100+1,msg+" ("+n+" pixeles, redondeo maximo="+max+")");
 }
 static object Album(string key){object a=Activator.CreateInstance(A,true);Set(a,"Folder",key);Set(a,"Name","Nombre largo de prueba - Disco");Set(a,"Artist","Artista de prueba");return a;}
 static void Images()
 {
  using(Control view=(Control)Activator.CreateInstance(V,true))
  {
   object a=Album("atlas");Set(view,"EnCola",Provider(V,"EnCola","Q"));
   foreach(Size size in new Size[]{new Size(1024,768),new Size(1280,720),new Size(1920,1080),new Size(2560,1440),new Size(3440,1440),new Size(3840,2160),new Size(3440,768)})
   {
    int cw=size.Width/6,ch=(size.Height-160)/3;object[] metrics={cw,ch,0,0,0};Call(V,null,"CellMetrics",metrics);
    int side=(int)metrics[2],lh=(int)metrics[3],gap=(int)metrics[4],h=side+gap+lh;
    foreach(bool q in new bool[]{false,true})foreach(Image image in new Image[]{cover,null})
    {
     queued=q;object[] p={a,cw,side,lh,gap,image,null};
     using(Bitmap compact=(Bitmap)Call(V,view,"BakeGpuCell",p))
     using(Bitmap old=(Bitmap)V.GetMethod("BakeCell",B,null,new Type[]{A,typeof(int),typeof(int),typeof(int),typeof(int),typeof(Image)},null).Invoke(view,new object[]{a,cw,side,lh,gap,image}))
     using(Bitmap expanded=Expand(compact,p[6],cw,h))
     {
      Same(old,expanded,size+" cola="+q+" portada="+(image!=null));
      int[] oi=Bytes(old),ei=Bytes(expanded);bool textSame=true;
      for(int t=(side+gap)*cw;t<oi.Length;t++)if(oi[t]!=ei[t])textSame=false;
      Check(textSame,"etiqueta conserva todos sus pixeles");
      Check(Pot(compact.Width,compact.Height)<=Pot(cw,h),"atlas no aumenta presupuesto POT");
      if(!q && image!=null)Console.WriteLine("Memoria "+size+": "+Pot(cw,h)+" -> "+Pot(compact.Width,compact.Height)+" bytes/casilla");
     }
    }
   }
   queued=false;
   int width=3440/6;object[] m={width,(1440-160)/3,0,0,0};Call(V,null,"CellMetrics",m);
   int sd=(int)m[2],lhh=(int)m[3],gp=(int)m[4];
   // Alternar lotes: solo composicion, no lectura del disco ni FPS de la biblioteca.
   for(int pass=0;pass<3;pass++)
   {
    Stopwatch timer=Stopwatch.StartNew();
    for(int i=0;i<36;i++)using(Bitmap b=(Bitmap)V.GetMethod("BakeCell",B,null,new Type[]{A,typeof(int),typeof(int),typeof(int),typeof(int),typeof(Image)},null).Invoke(view,new object[]{a,width,sd,lhh,gp,cover})){}
    double old=timer.Elapsed.TotalMilliseconds;timer.Reset();timer.Start();
    for(int i=0;i<36;i++){object[] p={a,width,sd,lhh,gp,cover,null};using(Bitmap b=(Bitmap)Call(V,view,"BakeGpuCell",p)){}}
    Console.WriteLine("36 composiciones: anterior "+old.ToString("F2")+" ms; atlas "+timer.Elapsed.TotalMilliseconds.ToString("F2")+" ms.");
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

 static void UploadBench(object d,Bitmap old,Bitmap compact)
 {
  for(int round=0;round<4;round++)
   foreach(bool atlas in round%2==0?new bool[]{false,true}:new bool[]{true,false})
   {
    Bitmap b=atlas?compact:old;Stopwatch sw=Stopwatch.StartNew();
    for(int k=0;k<36;k++){object[] p={b,0f,0f};IntPtr t=(IntPtr)Call(D,d,"CreateTexturePow2",p);if(t==IntPtr.Zero)throw new Exception("No crea textura durante medicion");Release(t);}
    Console.WriteLine("36 preparaciones HAL "+(atlas?"atlas":"anterior")+": "+sw.Elapsed.TotalMilliseconds.ToString("F2")+" ms; incluye crear/copiar/liberar, no FPS.");
   }
 }
 static void Hal()
 {
  using(Control host=new Control())using(Control view=(Control)Activator.CreateInstance(V,true))
  {
   host.Size=new Size(800,600);object d=Activator.CreateInstance(D,B,null,new object[]{host.Handle,800,600},null);
   try{
    Check((bool)D.GetProperty("Ok",B).GetValue(d,null),"HAL disponible para comparar atlas");Set(view,"EnCola",Provider(V,"EnCola","Q"));object a=Album("HAL");
    foreach(bool q in new bool[]{false,true})
    {
     queued=q;int cw=573,side=385,lh=38,gap=3;object[] p={a,cw,side,lh,gap,cover,null};
     using(Bitmap compact=(Bitmap)Call(V,view,"BakeGpuCell",p))
     using(Bitmap old=(Bitmap)V.GetMethod("BakeCell",B,null,new Type[]{A,typeof(int),typeof(int),typeof(int),typeof(int),typeof(Image)},null).Invoke(view,new object[]{a,cw,side,lh,gap,cover}))
     {
      object[] op={old,0f,0f},cp={compact,0f,0f};IntPtr ot=(IntPtr)Call(D,d,"CreateTexturePow2",op),ct=(IntPtr)Call(D,d,"CreateTexturePow2",cp);
      if(!q)UploadBench(d,old,compact);
      try{
       Check(ot!=IntPtr.Zero&&ct!=IntPtr.Zero,"texturas HAL");
       foreach(float dx in new float[]{0f,.25f,.5f,.75f})foreach(uint color in new uint[]{0xFFFFFFFFu,0x88777777u})
       {
        float px=30+dx,py=30+.25f;Array bg=Array.CreateInstance(X,12);Array b0=Quad(0,0,800,600,1,1,0xFF182848u);Array.Copy(b0,bg,6);Array.Copy(b0,0,bg,6,6);
        Check((bool)Call(D,d,"BeginFrame"),"Begin original");Call(D,d,"DrawSky",bg);Call(D,d,"DrawTexQuad",ot,Quad(px,py,cw,side+gap+lh,(float)op[1],(float)op[2],color));int[] before=Pixels(d);
        object c=Activator.CreateInstance(C);Set(c,"X",px);Set(c,"Y",py);Set(c,"W",(float)cw);Set(c,"H",(float)(side+gap+lh));Set(c,"Layout",p[6]);
        Check((bool)Call(D,d,"BeginFrame"),"Begin atlas");Call(D,d,"DrawSky",bg);
        if((int)Get(p[6],"Width")==0)Call(D,d,"DrawTexQuad",ct,Quad(px,py,cw,side+gap+lh,(float)cp[1],(float)cp[2],color));
        else{Array r=Array.CreateInstance(X,18);Call(G,null,"AtlasCellVertices",c,(float)cp[1],(float)cp[2],color,r);Call(D,d,"DrawQuads",ct,r,6);}
        int[] after=Pixels(d);int count=0,max=0;
        for(int i=0;i<before.Length;i++)if(before[i]!=after[i]){count++;for(int b=0;b<24;b+=8)max=Math.Max(max,Math.Abs(((before[i]>>b)&255)-((after[i]>>b)&255)));}
        if(max>1)for(int i=0,n=0;i<before.Length&&n<10;i++)if(before[i]!=after[i]){Console.WriteLine("HAL diff "+i%800+","+i/800+" "+before[i].ToString("X8")+" -> "+after[i].ToString("X8"));n++;}
        Check(max<=1 && count<before.Length/100,"atlas HAL equivalente, cola="+q+" dx="+dx+" color="+color+" ("+count+", diferencia maxima="+max+")");
       }
      }finally{Release(ot);Release(ct);}
     }
    }
   }finally{Call(D,d,"Dispose");}
  }
 }
 static void Cache()
 {
  using(Control view=(Control)Activator.CreateInstance(V,true))
  {
   view.Size=new Size(3440,1280);Set(view,"GetCover",Provider(V,"GetCover","Cover"));Set(view,"PeekCoverGpu",Provider(V,"PeekCoverGpu","Cover"));Set(view,"EnCola",Provider(V,"EnCola","Q"));
   IList page=(IList)Get(view,"Page");object a=Album("cache");page.Add(a);queued=false;
   Array cells=Array.CreateInstance(C,36);for(int i=0;i<5;i++)Call(V,view,"BuildGpuGrid",cells);
   Bitmap b=(Bitmap)Get(cells.GetValue(0),"Bmp");Check(b!=null,"atlas visible");
   Call(V,view,"BuildGpuGrid",cells);Check(ReferenceEquals(b,Get(cells.GetValue(0),"Bmp")),"revisitar no recompone");
   queued=true;Call(V,view,"BuildGpuGrid",cells);Check(!ReferenceEquals(b,Get(cells.GetValue(0),"Bmp")),"marca de cola invalida atlas");
   queued=false;Call(V,view,"BuildGpuGrid",cells);Check(ReferenceEquals(b,Get(cells.GetValue(0),"Bmp")),"quitar marca reutiliza original");
   using(Bitmap replacement=new Bitmap(256,256))
   {
    Bitmap oldCover=cover;cover=replacement;Call(V,view,"BuildGpuGrid",cells);Check(!ReferenceEquals(b,Get(cells.GetValue(0),"Bmp")),"sustituir portada invalida por identidad");cover=oldCover;
   }
   Check(((IDictionary)Get(view,"cellCache")).Count==0,"GPU no compone ni llena cache CPU");
   object anonymous=Album(null);page.Clear();page.Add(anonymous);
   queued=false;Call(V,view,"BuildGpuGrid",cells);Bitmap ab=(Bitmap)Get(cells.GetValue(0),"Bmp");
   Call(V,view,"BuildGpuGrid",cells);Check(ReferenceEquals(ab,Get(cells.GetValue(0),"Bmp")),"sin carpeta tambien reutiliza y posee su bitmap");
   queued=true;Call(V,view,"BuildGpuGrid",cells);Check(!ReferenceEquals(ab,Get(cells.GetValue(0),"Bmp")),"sin carpeta tambien actualiza marca de cola");
   Call(V,view,"ResetCellCache");Check(((IDictionary)Get(view,"gpuCellCache")).Count==0,"F5/reset libera atlas");
   Check(((IDictionary)Get(view,"gpuAnonymousCache")).Count==0,"F5/reset libera atlas sin carpeta");
   // Dos paginas preparadas y vuelta a la anterior: los atlas siguen cacheados.
   queued=false;page.Clear();for(int i=0;i<18;i++)page.Add(Album("primera-"+i));
   Array first=Array.CreateInstance(C,36);for(int i=0;i<24;i++)Call(V,view,"BuildGpuGrid",first);
   IList next=(IList)Activator.CreateInstance(page.GetType());for(int i=0;i<18;i++)next.Add(Album("segunda-"+i));
   Call(V,view,"SetAhead",next);for(int i=0;i<24;i++)Call(V,view,"BakeAheadGpu");
   Array ahead=Array.CreateInstance(C,36);int ac=(int)Call(V,view,"BuildGpuAhead",ahead);
   Check(ac==18,"precarga ofrece toda la pagina vecina");
   Bitmap[] warmed=new Bitmap[18];for(int i=0;i<18;i++){warmed[i]=(Bitmap)Get(ahead.GetValue(i),"Bmp");Check(warmed[i]!=null,"vecina ya compuesta "+i);}
   Set(view,"Page",next);Call(V,view,"BuildGpuGrid",cells);for(int i=0;i<18;i++)Check(ReferenceEquals(warmed[i],Get(cells.GetValue(i),"Bmp")),"pagina vecina no recompone "+i);
   Set(view,"Page",page);Call(V,view,"BuildGpuGrid",cells);for(int i=0;i<18;i++)Check(ReferenceEquals(Get(first.GetValue(i),"Bmp"),Get(cells.GetValue(i),"Bmp")),"volver no recompone "+i);
   view.Size=new Size(1024,608);Call(V,view,"BuildGpuGrid",cells);
   Check(!ReferenceEquals(Get(first.GetValue(0),"Bmp"),Get(cells.GetValue(0),"Bmp")),"cambio de resolucion invalida dimensiones");
  }
 }

 static void Metadata()
 {
  using(Control panel=(Control)Activator.CreateInstance(G,true))
  using(Bitmap b=new Bitmap(24,24))
  {
   IDictionary cache=(IDictionary)Get(panel,"cellTexCache");
   Type t=G.GetNestedType("CellTex",B);object tex=Activator.CreateInstance(t,true),cell=Activator.CreateInstance(C);
   Type lt=G.Assembly.GetType("Rocola.GpuCellLayout");object old=Activator.CreateInstance(lt),next=Activator.CreateInstance(lt);Set(old,"Width",120);Set(next,"Width",240);
   Set(tex,"Tex",new IntPtr(1));Set(tex,"Bmp",b);Set(tex,"Layout",old);cache["metadatos"]=tex;
   Set(cell,"Key","metadatos");Set(cell,"Bmp",cover);Set(cell,"Layout",next);
   try{
    object[] p={cell,0,IntPtr.Zero,0f,0f};
    Check((bool)Call(G,panel,"EnsureCellTex",p)&&(IntPtr)p[2]==new IntPtr(1),"sin presupuesto conserva la textura ya subida");
    Check((int)Get(Call(G,panel,"UploadedCellLayout",cell),"Width")==120,"usa metadatos de esa textura, no del bitmap nuevo");
   }finally{Set(tex,"Tex",IntPtr.Zero);cache.Clear();}
  }
 }
 [STAThread]static int Main(string[] args)
 {
  try{
   if(args.Length>1&&args[1]=="--dos-hilos")Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(3);
   Assembly asm=Assembly.LoadFrom(args[0]);F=asm.GetType("Rocola.Form1");V=F.GetNestedType("AlbumView",B);A=F.GetNestedType("Album",B);C=asm.GetType("Rocola.GpuGridCell");G=asm.GetType("Rocola.GpuPanel");D=asm.GetType("Rocola.D3D9Device");X=asm.GetType("Rocola.D3D9+Vtx");
   Check(V.GetMethod("BakeGpuCell",B)!=null,"existe composicion GPU compacta");
   using(Bitmap b=new Bitmap(256,256)){cover=b;using(Graphics g=Graphics.FromImage(b)){g.Clear(Color.Navy);g.FillEllipse(Brushes.Orange,20,40,185,160);g.DrawLine(Pens.Lime,0,0,255,255);g.DrawString("CARATULA",SystemFonts.DefaultFont,Brushes.White,35,80);}Images();Cache();Metadata();Hal();}
   using(Bitmap transparent=new Bitmap(256,256)){cover=transparent;using(Graphics g=Graphics.FromImage(transparent)){g.Clear(Color.Transparent);using(Brush br=new SolidBrush(Color.FromArgb(128,255,100,40)))g.FillEllipse(br,20,20,210,210);}Images();}
   Console.WriteLine("PASS atlas: "+checks+" comprobaciones; no son FPS de la biblioteca.");return 0;
  }catch(Exception e){Console.WriteLine("FALLO "+e);return 1;}
 }
}
