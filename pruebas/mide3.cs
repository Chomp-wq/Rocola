using System; using System.Collections; using System.Collections.Generic; using System.Diagnostics;
using System.Drawing; using System.Drawing.Imaging; using System.Reflection; using System.Windows.Forms;
// Lo que cuesta, POR CUADRO, pedirle a la rejilla de discos las 18 casillas de la pagina (con el cajon caliente).
namespace Rocola { static class Mide3 {
  static Image lo;
  static Image Tapa<T>(T a){ return lo; }
  static void Main(){
    Type TAV=null,TAlb=null,TF=typeof(Form1);
    foreach(Type t in TF.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)){
      if(t.Name=="AlbumView") TAV=t; if(t.Name=="Album") TAlb=t; }
    Bitmap portada=new Bitmap(500,500,PixelFormat.Format32bppPArgb);
    using(Graphics pg=Graphics.FromImage(portada)){ pg.Clear(Color.FromArgb(255,30,40,90));
      for(int i=0;i<40;i++) pg.FillEllipse(new SolidBrush(Color.FromArgb(90,200,120+i,255-i)),i*9,i*8,70,70); }
    Control av=(Control)Activator.CreateInstance(TAV,true);
    av.SetBounds(0,0,1024,460);
    av.Font=new Font(FontFamily.GenericSansSerif,10f,FontStyle.Bold);
    Type tFunc=typeof(Func<,>).MakeGenericType(TAlb,typeof(Image));
    lo=portada;
    TAV.GetField("GetCover").SetValue(av,Delegate.CreateDelegate(tFunc,
      typeof(Mide3).GetMethod("Tapa",BindingFlags.Static|BindingFlags.NonPublic).MakeGenericMethod(TAlb)));
    IList lista=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlb));
    for(int i=0;i<18;i++){
      object a=Activator.CreateInstance(TAlb,true);
      TAlb.GetField("Folder").SetValue(a,@"C:\Musica\Disco"+i);
      TAlb.GetField("Name").SetValue(a,"Disco numero "+i+" con nombre largo");
      TAlb.GetField("Artist").SetValue(a,"Artista "+i);
      TAlb.GetField("Letter").SetValue(a,(char)('A'+i%26));
      lista.Add(a); }
    TAV.GetField("Page").SetValue(av,lista);
    MethodInfo mB=TAV.GetMethod("BuildGpuGrid",BindingFlags.Instance|BindingFlags.Public);
    Array buf=Array.CreateInstance(TF.GetNestedTypes(BindingFlags.NonPublic)[0],1);
    foreach(Type t in typeof(Mide3).Assembly.GetTypes()) if(t.Name=="GpuGridCell") buf=Array.CreateInstance(t,64);
    object[] arg=new object[]{buf};
    for(int i=0;i<400;i++) mB.Invoke(av,arg);   // dejar el cajon caliente
    double m=double.MaxValue;
    for(int b=0;b<7;b++){ Stopwatch sw=Stopwatch.StartNew(); for(int i=0;i<500;i++) mB.Invoke(av,arg); sw.Stop();
      double ms=sw.Elapsed.TotalMilliseconds/500; if(ms<m) m=ms; }
    Console.WriteLine("  pedir las 18 casillas de la pagina (cajon caliente): "+m.ToString("0.000")+" ms por cuadro");
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long a0=GC.GetTotalMemory(true);
    for(int i=0;i<3000;i++) mB.Invoke(av,arg);
    long a1=GC.GetTotalMemory(false);
    Console.WriteLine("  basura por cuadro: "+((a1-a0)/3000.0).ToString("0")+" bytes (incluye la reflexion de la prueba)");
  }
} }
