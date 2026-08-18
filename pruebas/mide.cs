using System; using System.Collections.Generic; using System.Diagnostics; using System.Drawing;
using System.Drawing.Imaging; using System.Reflection; using System.Windows.Forms;
// Cuanto cuesta dibujar la lista de un disco (una pagina entera) y la textura de UI del modo GPU.
class Mide {
  static double Min(int reps, Action f){ f(); f(); double m=double.MaxValue;
    for(int b=0;b<7;b++){ Stopwatch sw=Stopwatch.StartNew(); for(int i=0;i<reps;i++) f(); sw.Stop();
      double ms=sw.Elapsed.TotalMilliseconds/reps; if(ms<m) m=ms; } return m; }
  static int Main(string[] a){
    Assembly asm=Assembly.LoadFrom(a.Length>0?a[0]:"rocola.exe");
    Type TF=asm.GetType("Rocola.Form1",true); Type TL=null;
    foreach(Type t in TF.GetNestedTypes(BindingFlags.NonPublic)) if(t.Name=="SongListView") TL=t;
    Control c=(Control)Activator.CreateInstance(TL,true);
    int W=980,H=560,RH=38; int filas=H/RH;
    c.SetBounds(0,0,W,H); c.Font=new Font(FontFamily.GenericSansSerif,11f,FontStyle.Bold);
    TL.GetField("RowH").SetValue(c,RH);
    TL.GetField("Accent").SetValue(c,Color.FromArgb(150,110,220));
    TL.GetField("GetCount").SetValue(c,new Func<int>(delegate{return 60;}));
    TL.GetField("GetText").SetValue(c,new Func<int,string>(delegate(int i){return "Cancion numero "+(i+1)+" - titulo bastante largo";}));
    TL.GetField("GetRight").SetValue(c,new Func<int,string>(delegate(int i){return "3:4"+(i%10);}));
    TL.GetField("GetQueuePos").SetValue(c,new Func<int,int>(delegate(int i){return (i%7==0)?1:(i%11==0)?-1:0;}));
    TL.GetField("GetGenero").SetValue(c,new Func<int,string>(delegate(int i){return (i%3==0)?"Regional":"Pop";}));
    MethodInfo R=TL.GetMethod("RenderContent",new Type[]{typeof(Graphics)});
    Bitmap bmp=new Bitmap(W,H,PixelFormat.Format32bppPArgb);
    Graphics g=Graphics.FromImage(bmp);
    double tl=Min(60,delegate{ R.Invoke(c,new object[]{g}); });
    Console.WriteLine("  lista de un disco ("+filas+" filas a "+W+"x"+H+"): "+tl.ToString("0.000")+" ms");
    Console.WriteLine("  a 66 cuadros/s eso seria el "+(tl/15.15*100).ToString("0.0")+"% de un cuadro");
    // Y la copia a textura de pantalla completa (lo que se sube a la grafica)
    Bitmap full=new Bitmap(1920,1000,PixelFormat.Format32bppPArgb);
    double tc=Min(20,delegate{
      BitmapData bd=full.LockBits(new Rectangle(0,0,1920,1000),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
      full.UnlockBits(bd); });
    Console.WriteLine("  bloquear/soltar una textura de pantalla completa: "+tc.ToString("0.000")+" ms");
    return 0; }
}
