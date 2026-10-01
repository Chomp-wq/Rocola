using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
class VerticesGpu
{
 const BindingFlags B=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static Type T,V; static int checks;
 static object Get(object o,string n){return T.GetField(n,B).GetValue(o);}
 static void Set(object o,string n,object v){T.GetField(n,B).SetValue(o,v);}
 static object Call(object o,string n,params object[] p){try{return T.GetMethod(n,B).Invoke(o,p);}catch(TargetInvocationException e){throw e.InnerException;}}
 static void Check(bool ok,string s){if(!ok)throw new Exception(s);checks++;Console.WriteLine("OK "+s);}
 [StructLayout(LayoutKind.Sequential)] struct Desc {public uint Format,Type,Usage,Pool,Multi,Quality,Width,Height;}
 [StructLayout(LayoutKind.Sequential)] struct Rect {public int Pitch;public IntPtr Data;}
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Hr(IntPtr self);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint Rel(IntPtr self);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Back(IntPtr self,uint a,uint b,uint c,out IntPtr surface);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Description(IntPtr self,out Desc desc);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Offscreen(IntPtr self,uint w,uint h,uint format,uint pool,out IntPtr surface,IntPtr shared);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Readback(IntPtr self,IntPtr source,IntPtr dest);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Lock(IntPtr self,out Rect r,IntPtr region,uint flags);
 static D Fn<D>(IntPtr p,int slot)where D:class{return (D)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(p),slot*IntPtr.Size),typeof(D));}
 static void Release(IntPtr p){if(p!=IntPtr.Zero)Fn<Rel>(p,2)(p);}
 static Array Geometry(int tris,int w,int h)
 {
  Array a=Array.CreateInstance(V,tris*3);
  for(int i=0;i<tris*3;i++)
  {
   int face=i/3,k=i%3; float x=(face%20)*(w/20f),y=(face/20%15)*(h/15f);
   if(k==1)x+=w/12f;if(k==2)y+=h/8f;
   object v=Activator.CreateInstance(V); V.GetField("X").SetValue(v,x-.5f);V.GetField("Y").SetValue(v,y-.5f);
   V.GetField("Rhw").SetValue(v,1f);V.GetField("Color").SetValue(v,0x80000000u | (uint)((face*97%256)<<16) | (uint)((face*51%256)<<8) | (uint)(face*7%256));
   a.SetValue(v,i);
  } return a;
 }
 static Array Sky(int w,int h)
 {
  Array a=Array.CreateInstance(V,12); int[] xs={0,w,0,0,w,w};int[] ys={0,0,h,h,0,h};
  for(int i=0;i<12;i++){object v=Activator.CreateInstance(V);V.GetField("X").SetValue(v,(float)xs[i%6]-.5f);V.GetField("Y").SetValue(v,(float)ys[i%6]-.5f);V.GetField("Rhw").SetValue(v,1f);V.GetField("Color").SetValue(v,0xFF182848u);a.SetValue(v,i);}return a;
 }
 static void Draw(object d,Array sky,Array mesh,int tris,int n)
 {Check((bool)Call(d,"BeginFrame"),"BeginFrame");Call(d,"DrawSky",sky);for(int i=0;i<n;i++)Call(d,"DrawMesh",mesh,tris);}
 static int[] Pixels(object d)
 {
  IntPtr dev=(IntPtr)Get(d,"dev"),back=IntPtr.Zero,off=IntPtr.Zero;bool locked=false;
  Check(Fn<Hr>(dev,42)(dev)>=0,"EndScene antes de lectura"); Set(d,"inScene",false); Call(d,"SueltaPinesVertices");
  try{
   Check(Fn<Back>(dev,18)(dev,0,0,0,out back)>=0,"obtener superficie HAL");
   Desc desc;Check(Fn<Description>(back,12)(back,out desc)>=0,"descripcion de superficie");
   Check(Fn<Offscreen>(dev,36)(dev,desc.Width,desc.Height,desc.Format,2,out off,IntPtr.Zero)>=0,"superficie de lectura");
   Check(Fn<Readback>(dev,32)(dev,back,off)>=0,"leer pixeles reales de la GPU");
   Rect r;Check(Fn<Lock>(off,13)(off,out r,IntPtr.Zero,0x10)>=0,"bloquear lectura");locked=true;
   int[] p=new int[desc.Width*desc.Height];for(int y=0;y<desc.Height;y++)Marshal.Copy(new IntPtr(r.Data.ToInt64()+(long)y*r.Pitch),p,y*(int)desc.Width,(int)desc.Width);return p;
  }finally{if(locked)Fn<Hr>(off,14)(off);Release(off);Release(back);}
 }
 static double Bench(object d,Array sky,Array mesh,bool disabled)
 {
  Set(d,"verticesSinSoporte",disabled);Set(d,"verticesModo",disabled?1:2);
  MethodInfo begin=T.GetMethod("BeginFrame",B),ds=T.GetMethod("DrawSky",B),dm=T.GetMethod("DrawMesh",B),end=T.GetMethod("EndFrame",B);
  object[] sp={sky},mp={mesh,1024}; Stopwatch sw=Stopwatch.StartNew();
  for(int i=0;i<80;i++){if(!(bool)begin.Invoke(d,null))throw new Exception("BeginFrame bench");ds.Invoke(d,sp);for(int k=0;k<8;k++)dm.Invoke(d,mp);if(!(bool)end.Invoke(d,null))throw new Exception("EndFrame bench");}
  return sw.Elapsed.TotalMilliseconds/80;
 }
 [STAThread]static int Main(string[] args)
 {
  try{
   Assembly a=Assembly.LoadFrom(args[0]);T=a.GetType("Rocola.D3D9Device",true);V=a.GetType("Rocola.D3D9+Vtx",true);
   using(Control c=new Control()){c.Size=new Size(320,240);IntPtr h=c.Handle; object d=Activator.CreateInstance(T,B,null,new object[]{h,320,240},null);
   try{
    Check((bool)T.GetProperty("Ok",B).GetValue(d,null),"Direct3D HAL disponible");
    Array sky=Sky(320,240),mesh=Geometry(120,320,240);
    Set(d,"verticesSinSoporte",true);Draw(d,sky,mesh,120,4);
    Check(object.ReferenceEquals(Get(d,"verticesFijados"),mesh),"el ultimo array reutilizado pertenece a esta escena HAL");
    int[] up=Pixels(d);
    Check(Get(d,"verticesFijados")==null,"lectura tras EndScene no deja un pin vivo");
    Set(d,"verticesSinSoporte",false);Set(d,"verticesModo",2);Draw(d,sky,mesh,120,4);int[] vb=Pixels(d);
    Check((IntPtr)Get(d,"verticesGpu")!=IntPtr.Zero,"usa el anillo GPU y no el respaldo UP");
    bool same=up.Length==vb.Length;for(int i=0;i<up.Length && same;i++)same=up[i]==vb[i];Check(same,"anillo GPU identico pixel a pixel a UP, con transparencias");
    IntPtr buffer=(IntPtr)Get(d,"verticesGpu");Draw(d,sky,mesh,120,150);Pixels(d);Check((IntPtr)Get(d,"verticesGpu")==buffer,"reutiliza buffer y recicla con DISCARD");
    Check((bool)Call(d,"Redimensiona",400,300),"reset de resolucion libera recurso DEFAULT antes de reset");
    Check((IntPtr)Get(d,"verticesGpu")==IntPtr.Zero,"recurso anterior retirado tras reset");
    Draw(d,Sky(400,300),Geometry(120,400,300),120,1);Pixels(d);Check((IntPtr)Get(d,"verticesGpu")!=IntPtr.Zero,"recrea anillo tras reset");
    Check((bool)Call(d,"Redimensiona",320,240),"regresa a resolucion inicial");
    Array big=Geometry(1024,320,240);double tUp=Bench(d,sky,big,true),tVb=Bench(d,sky,big,false);
    Console.WriteLine("HAL sintetico 8 x 1024 triangulos/cuadro: UP "+tUp.ToString("F3")+" ms; anillo "+tVb.ToString("F3")+" ms. No son FPS de la rocola.");
    Set(d,"verticesModo",0);Set(d,"verticesPruebaTris",0);Set(d,"verticesPruebas",0);
    Draw(d,sky,mesh,120,44);Pixels(d);
    Check((int)Get(d,"verticesModo")!=0,"calibra con dibujos reales y elige automaticamente");
    object changed=mesh.GetValue(0);V.GetField("Color").SetValue(changed,0xFFFF3399u);mesh.SetValue(changed,0);
    Set(d,"verticesSinSoporte",true);Draw(d,sky,mesh,120,3);int[] changedUp=Pixels(d);
    Set(d,"verticesSinSoporte",false);Set(d,"verticesModo",2);Draw(d,sky,mesh,120,3);int[] changedVb=Pixels(d);
    same=changedUp.Length==changedVb.Length;for(int i=0;i<changedUp.Length && same;i++)same=changedUp[i]==changedVb[i];
    Check(same,"modificar el mismo array conserva pixeles identicos en UP y anillo");
    V.GetField("Color").SetValue(changed,0xFFFF3399u);mesh.SetValue(changed,0);
    Set(d,"verticesSinSoporte",true);Draw(d,sky,mesh,120,1);
    V.GetField("Color").SetValue(changed,0xFF33FF99u);mesh.SetValue(changed,0);Call(d,"DrawMesh",mesh,120);
    int[] vivo=Pixels(d);
    V.GetField("Color").SetValue(changed,0xFFFF3399u);mesh.SetValue(changed,0);Draw(d,sky,mesh,120,1);
    V.GetField("Color").SetValue(changed,0xFF33FF99u);mesh.SetValue(changed,0);
    Set(d,"inScene",false);Call(d,"DrawMesh",mesh,120);Set(d,"inScene",true);int[] temporal=Pixels(d);
    same=vivo.Length==temporal.Length;for(int i=0;i<vivo.Length && same;i++)same=vivo[i]==temporal[i];
    Check(same,"mutacion durante la escena se lee igual con pin reutilizado o temporal");
    Draw(d,sky,mesh,120,1);Check((bool)Call(d,"EndFrame"),"EndFrame HAL presenta una escena normal");
    Check(Get(d,"verticesFijados")==null && (long)Get(d,"pinesBytes")==0,"EndFrame libera el pin y su referencia");
    Draw(d,sky,mesh,120,1);Check(Get(d,"verticesFijados")!=null,"escena abierta al probar Dispose");
   }finally{Call(d,"Dispose");}Check((IntPtr)Get(d,"verticesGpu")==IntPtr.Zero,"Dispose libera vertices");
   Check(Get(d,"verticesFijados")==null && (long)Get(d,"pinesBytes")==0,"Dispose libera tambien la ultima escena HAL abierta");}
   Console.WriteLine(checks+" comprobaciones de vertices HAL.");return 0;
  }catch(Exception e){Console.WriteLine("FALLO "+e);return 1;}
 }
}
