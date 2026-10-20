using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
class TironesCaratulas
{
 const BindingFlags B=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 static int checks;
 static Type G,V,A;
 static object Call(Type t,object o,string n,params object[] a){return t.GetMethod(n,B).Invoke(o,a);}
 static object Get(Type t,object o,string n){return t.GetField(n,B).GetValue(o);}
 static void Set(Type t,object o,string n,object v){t.GetField(n,B).SetValue(o,v);}
 static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static void Policy()
 {
  Check(!(bool)Call(G,null,"CanProbeCoverMemory",550d,0d,-2000d,true,false),"no sondea mientras se asienta pagina");
  Check((bool)Call(G,null,"CanProbeCoverMemory",600d,0d,-2000d,true,false),"primer sondeo tras asentamiento");
  Check(!(bool)Call(G,null,"CanProbeCoverMemory",10000d,9900d,0d,false,false),"nueva pagina posterga sondeo vencido");
  Check(!(bool)Call(G,null,"CanProbeCoverMemory",10000d,0d,0d,false,true),"carga pendiente posterga sondeo");
  Check((bool)Call(G,null,"CanProbeCoverMemory",60000d,0d,0d,false,false),"quieto permite actualizar memoria cada minuto");
  Check(!(bool)Call(G,null,"CanProbeCoverMemory",59999d,0d,0d,false,false),"no repite sondeo antes del minuto (ni cada 10 s, ni cada dos segundos)");
 }
 static void Labels(bool baseline)
 {
  using(Control view=(Control)Activator.CreateInstance(V,true))
  {
   IDictionary cache=(IDictionary)Get(V,view,"gpuCellCache"),anon=(IDictionary)Get(V,view,"gpuAnonymousCache");
   Type entry=V.GetNestedType("CeldaGpu",B);
   Set(V,view,"capCeldas",108);Set(V,view,"keepCeldas",72);
   for(int i=0;i<180;i++){
    object c=Activator.CreateInstance(entry,true);Set(entry,c,"Bmp",new Bitmap(16,16));Set(entry,c,"Uso",(long)i);
    if(i<150)cache.Add("album-"+i,c);
    else {object a=Activator.CreateInstance(A,true);anon.Add(a,c);}
   }
   int before=cache.Count+anon.Count;Stopwatch timer=Stopwatch.StartNew();Call(V,view,"PodaGpuCells");timer.Stop();
   int removed=before-cache.Count-anon.Count;
   Console.WriteLine("Etiquetas: primera poda="+removed+" imagenes, "+timer.Elapsed.TotalMilliseconds.ToString("F3")+" ms");
   if(!baseline){
    Check(removed>=1&&removed<=4,"poda de etiquetas se reparte");
    for(int i=0;i<180&&cache.Count+anon.Count>72;i++)Call(V,view,"PodaGpuCells");
    Check(cache.Count+anon.Count==72,"poda converge al umbral inferior");
    Check(cache.Contains("album-149")&&anon.Count==30,"conserva etiquetas mas recientes incluidas sin carpeta");
    Call(V,view,"PodaGpuCells");Check(cache.Count+anon.Count==72,"no sobrepoda");
   }
   Call(V,view,"ClearGpuCells");Check(cache.Count==0&&anon.Count==0,"reset libera todo");
  }
 }
 static void Hal(bool baseline)
 {
  using(Control panel=(Control)Activator.CreateInstance(G,true)){
   panel.Size=new Size(640,480);
   if(!(bool)Call(G,panel,"StartGpu"))throw new Exception("HAL requerido, no se puede confirmar limpieza de texturas");
   Type cell=G.Assembly.GetType("Rocola.GpuGridCell"),gov=G.Assembly.GetType("Rocola.CoverGovernor");
   ArrayList bitmaps=new ArrayList();
   try{
    // Cada perfil se somete al mismo barrido con presion real sobre texturas D3D.
    for(int profile=1;profile<=3;profile++){
     object control=Activator.CreateInstance(gov,B,null,new object[]{2},null);Call(gov,control,"SetProfile",profile);
     Set(G,panel,"CoverControl",null);Set(G,panel,"cellFrame",10);
     for(int i=0;i<60;i++){
      Bitmap b=new Bitmap(256,256);bitmaps.Add(b);
      object c=Activator.CreateInstance(cell);Set(cell,c,"Direct",true);Set(cell,c,"FullCover",b);
      object[] args={c,1,false,0f,0f};
      Check((IntPtr)Call(G,panel,"PrepareNativeCover",args)!=IntPtr.Zero,"sube portada para prueba de presion");
     }
     IDictionary native=(IDictionary)Get(G,panel,"nativeCovers");
     object visible=Activator.CreateInstance(cell);Set(cell,visible,"Direct",true);Set(cell,visible,"FullCover",bitmaps[0]);
     Set(G,panel,"cellFrame",100);object[] keep={visible,0,true,0f,0f};Call(G,panel,"PrepareNativeCover",keep);
     Set(G,panel,"cellTextureBudget",4L*256*256*4);Set(G,panel,"CoverControl",control);Call(gov,control,"Begin",1000d,200);
     int before=native.Count;Stopwatch t=Stopwatch.StartNew();Call(G,panel,"EvictCellTex");t.Stop();
     int removed=before-native.Count;Console.WriteLine("Perfil "+profile+": primer barrido="+removed+" texturas, "+t.Elapsed.TotalMilliseconds.ToString("F3")+" ms");
     if(!baseline){
      Check(removed>=1&&removed<=4,"barrido limitado en todos los perfiles");
      for(int frame=101;frame<200&&native.Count>4;frame++){
       Set(G,panel,"cellFrame",frame);Call(G,panel,"PrepareNativeCover",keep);
       Call(gov,control,"Begin",1000d+frame*5,200);
       int n=native.Count;Call(G,panel,"EvictCellTex");
       Check(n-native.Count<=4,"ningun cuadro libera mas de cuatro");
       Check((bool)Call(G,panel,"NativeCoverReady",visible),"visible protegida durante limpieza gradual");
      }
      Check(native.Count==4,"presion converge sin fuga");
      Check((long)Call(G,panel,"CachedCellBytes")==4L*256*256*4,"contabilidad coincide tras limpieza");
     }
     Set(G,panel,"CoverControl",null);Call(G,panel,"ReleaseNativeCovers");
     foreach(Bitmap b in bitmaps)b.Dispose();bitmaps.Clear();
    }
    if(!baseline){
     Set(G,panel,"cellTextureBudget",32L*1024*1024);Set(G,panel,"cellFrame",1000);
     Array grid=(Array)Get(G,panel,"gridBuf");IDictionary lod=(IDictionary)Get(G,panel,"lodTextures");
     Bitmap preview=new Bitmap(32,32);bitmaps.Add(preview);
     for(int i=0;i<18;i++){object c=Activator.CreateInstance(cell);Set(cell,c,"Key","lod-"+i);Set(cell,c,"Preview",preview);grid.SetValue(c,0);Call(G,panel,"PrepareLod",0,false);}
     Set(G,panel,"cellFrame",1100);Call(G,panel,"TrimLod",false);
     Check(lod.Count==18,"volver a pagina conserva previews sin resubir");
     for(int frame=1101;frame<1301;frame++){
      Set(G,panel,"cellFrame",frame);
      for(int i=0;i<18;i++){object c=Activator.CreateInstance(cell);Set(cell,c,"Key","lod-"+frame+"-"+i);Set(cell,c,"Preview",preview);grid.SetValue(c,0);Call(G,panel,"PrepareLod",0,false);}
      Call(G,panel,"TrimLod",false);Check(lod.Count<=144,"historial LOD acotado incluso en rafaga");
     }
     Call(G,panel,"TrimLod",true);Check(lod.Count==0,"reset libera historial LOD entero");
     // Una rafaga no puede crecer indefinidamente mientras la poda va por tandas.
     IDictionary native=(IDictionary)Get(G,panel,"nativeCovers"),labels=(IDictionary)Get(G,panel,"cellTexCache");
     Type entry=G.GetNestedType("CellTex",B);int maximum=(int)Get(G,panel,"cellTextureCapacity")+36;
     for(int i=0;i<maximum;i++){
      Bitmap b=new Bitmap(1,1);bitmaps.Add(b);native.Add(b,Activator.CreateInstance(entry,true));
      labels.Add("limit-"+i,Activator.CreateInstance(entry,true));
     }
     object pending=Activator.CreateInstance(cell);Set(cell,pending,"Direct",true);Set(cell,pending,"FullCover",preview);
     Set(cell,pending,"Key","over-limit");Set(cell,pending,"Bmp",preview);
     object[] upload={pending,1,true,0f,0f};
     Check((IntPtr)Call(G,panel,"PrepareNativeCover",upload)==IntPtr.Zero&&(int)upload[1]==1,"limite nativo aplaza sin consumir presupuesto");
     object[] labelUpload={pending,1,IntPtr.Zero,0f,0f};
     Check(!(bool)Call(G,panel,"EnsureCellTex",labelUpload)&&(int)labelUpload[1]==1,"limite etiquetas aplaza sin consumir presupuesto");
     Check(native.Count==maximum&&labels.Count==maximum,"no crecen caches pendientes de poda");
    }
   }finally{Call(G,panel,"StopGpu");foreach(Bitmap b in bitmaps)b.Dispose();}
  }
 }
 [STAThread]static int Main(string[] args)
 {
  try{
   if(Environment.OSVersion.Platform==PlatformID.Win32NT)Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(3);
   Assembly asm=Assembly.LoadFile(System.IO.Path.GetFullPath(args[0]));G=asm.GetType("Rocola.GpuPanel",true);
   Type f=asm.GetType("Rocola.Form1",true);V=f.GetNestedType("AlbumView",B);A=f.GetNestedType("Album",B);
   bool baseline=Array.IndexOf(args,"--antes")>=0;
   if(!baseline)Policy();Labels(baseline);
   if(Environment.OSVersion.Platform==PlatformID.Win32NT&&Array.IndexOf(args,"--sin-hal")<0)Hal(baseline);else Console.WriteLine("HAL omitido: no confirma limpieza real del driver");
   Console.WriteLine("PASS "+checks+" pruebas de mantenimiento gradual en dos hilos; no mide FPS del programa con biblioteca real");return 0;
  }catch(Exception ex){Console.WriteLine("FALLO "+ex);return 1;}
 }
}

