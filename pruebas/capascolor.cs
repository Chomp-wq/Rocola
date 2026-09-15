using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
class CapasColor
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static object Call(Type t,object o,string name,params object[] args){return t.GetMethod(name,F).Invoke(o,args);}
 static void Set(Type t,object o,string name,object v){t.GetField(name,F).SetValue(o,v);}
 static object Get(Type t,object o,string name){return t.GetField(name,F).GetValue(o);}
 static int[] Pixels(Bitmap b) {
  BitmapData d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
  try{int[] p=new int[b.Width*b.Height];for(int y=0;y<b.Height;y++)Marshal.Copy(new IntPtr(d.Scan0.ToInt64()+y*d.Stride),p,y*b.Width,b.Width);return p;}
  finally{b.UnlockBits(d);}
 }
 [STAThread]static int Main(string[] args)
 {
  Assembly asm=Assembly.LoadFrom(args[0]);Type form=asm.GetType("Rocola.Form1"),list=form.GetNestedType("SongListView",F);
  using(Control c=(Control)Activator.CreateInstance(list,true))
  using(Font font=new Font("Tahoma",10,FontStyle.Bold))
  {
   c.Size=new Size(980,540);c.Font=font;
   Set(list,c,"RowH",30);Set(list,c,"Accent",Color.MediumPurple);
   Set(list,c,"GetCount",new Func<int>(delegate{return 80;}));
   Set(list,c,"GetText",new Func<int,string>(delegate(int i){return "Cancion "+i+" - Depeche Mode";}));
   Set(list,c,"GetRight",new Func<int,string>(delegate(int i){return "4:36";}));
   Set(list,c,"GetGenero",new Func<int,string>(delegate(int i){return i%2==0?"Pop":"Rock";}));
   Set(list,c,"GetQueuePos",new Func<int,int>(delegate(int i){return i%4==0?i/4+1:0;}));
   Set(list,c,"GetSonando",new Func<int,bool>(delegate(int i){return i==4;}));
   long signature=(long)Call(list,c,"GpuLayerSignature");
   using(Bitmap page=(Bitmap)Call(list,c,"BakeGpuListLayer",false))
   for(int selected=0;selected<18;selected++)
   {
    Set(list,c,"Sel",selected);
    Check((long)Call(list,c,"GpuLayerSignature")==signature,"Mover seleccion invalida pagina");
    using(Bitmap row=(Bitmap)Call(list,c,"BakeGpuListLayer",true))
    using(Bitmap actual=new Bitmap(980,540,PixelFormat.Format32bppPArgb))
    using(Bitmap expected=new Bitmap(980,540,PixelFormat.Format32bppPArgb))
    {
     Rectangle cut=new Rectangle(0,selected*30,980,30);
     using(Graphics g=Graphics.FromImage(actual))
     using(SolidBrush veil=new SolidBrush(Color.FromArgb(210,12,16,30)))
     {
      g.FillRectangle(veil,0,0,980,540);
      g.ExcludeClip(cut);g.DrawImageUnscaled(page,0,0);g.ResetClip();
      g.DrawImageUnscaled(row,cut.Location);
     }
     using(Graphics g=Graphics.FromImage(expected))Call(list,c,"RenderTransparentGpuMode",g,false);
     int[] pa=Pixels(actual),pe=Pixels(expected);int mismatch=0;
     for(int i=0;i<pa.Length;i++)if(pa[i]!=pe[i])mismatch++;
     Check(mismatch==0,"Composicion distinta: "+mismatch+" pixeles / seleccion "+selected);
    }
   }
   Set(list,c,"Datos",1);
   Check((long)Call(list,c,"GpuLayerSignature")!=signature,"No invalida contenido");
   using(Bitmap page=(Bitmap)Call(list,c,"BakeGpuListLayer",false)){}
   Call(list,c,"StartSlide",1);
   Check((bool)list.GetProperty("Sliding",F).GetValue(c,null),"Se perdio el hojeo");
   Call(list,c,"OlvidaPagina");
   using(Bitmap target=new Bitmap(980,540))
   using(Graphics g=Graphics.FromImage(target))
   {
    Stopwatch sw=Stopwatch.StartNew();
    for(int i=0;i<120;i++){Set(list,c,"Sel",i%18);Call(list,c,"RenderTransparentGpuMode",g,true);}
    double full=sw.Elapsed.TotalMilliseconds/120;
    using(Bitmap page=(Bitmap)Call(list,c,"BakeGpuListLayer",false)){}
    sw.Reset();sw.Start();
    for(int i=0;i<120;i++){Set(list,c,"Sel",i%18);using(Bitmap b=(Bitmap)Call(list,c,"BakeGpuListLayer",true)){}}
    double layer=sw.Elapsed.TotalMilliseconds/120;
    Console.WriteLine("Microbenchmark GDI+ (no FPS): completo "+full.ToString("F2")+" ms / fila "+layer.ToString("F2")+" ms; upload 540 -> 30 filas.");
   }
  }
  Type gpu=asm.GetType("Rocola.GpuPanel");
  foreach(float value in new float[]{float.NaN,float.PositiveInfinity,-5,0,.5f,5})
  {float safe=(float)Call(gpu,null,"SafeMoonLevel",value);Check(safe>=0&&safe<=1,"Luna: nivel invalido");}
  PointF p0=(PointF)Call(gpu,null,"MoonCenter",640,480,0.0,0f),p1=(PointF)Call(gpu,null,"MoonCenter",640,480,12.0,0f);
  Check(p0!=p1,"Luna inmovil");
  using(Control panel=(Control)Activator.CreateInstance(gpu,true))
  using(Bitmap bitmap=new Bitmap(640,400))
  {
   panel.Size=new Size(640,480);int version=1,key=1;long content=1;
   using(Graphics g=Graphics.FromImage(bitmap))g.Clear(Color.DarkBlue);
   Set(gpu,panel,"GetUi",new Func<Bitmap>(delegate{return bitmap;}));
   Set(gpu,panel,"GetUiVersion",new Func<int>(delegate{return version;}));
   Set(gpu,panel,"GetPaletteKey",new Func<int>(delegate{return key;}));
   Set(gpu,panel,"GetPaletteContent",new Func<long>(delegate{return content;}));
   Set(gpu,panel,"ConLuna",true);
   Check((bool)Call(gpu,panel,"StartGpu"),"HAL no disponible");
   try{
    Check((bool)Call(gpu,panel,"DibujaUnCuadro"),"No presenta cuadro");
    IntPtr original=(IntPtr)Get(gpu,panel,"uiTex");
    Check(original!=IntPtr.Zero && (IntPtr)Get(gpu,panel,"lunaTex")!=IntPtr.Zero,"No sube UI/luna");
    key++;version++;Call(gpu,panel,"DibujaUnCuadro");
    Array old=(Array)Get(gpu,panel,"paletteOld");object first=old.GetValue(0);
    Check(first!=null && (IntPtr)Get(first.GetType(),first,"Tex")==original,"No guarda extremo anterior");
    Check((IntPtr)Get(gpu,panel,"uiTex")!=original,"Textura tiene dos propietarios");
    IntPtr current=(IntPtr)Get(gpu,panel,"uiTex");
    for(int i=0;i<10;i++)Call(gpu,panel,"DibujaUnCuadro");
    Check((IntPtr)Get(gpu,panel,"uiTex")==current,"Transicion vuelve a subir textura");
    content++;Call(gpu,panel,"DibujaUnCuadro");
    foreach(object item in old)Check(item==null,"Cambio de contenido deja letras fantasma");
    key++;version++;Call(gpu,panel,"DibujaUnCuadro");
    Call(gpu,panel,"TouchUi");
    foreach(object item in old)Check(item==null,"Toque espera transicion");
    Set(gpu,panel,"ColoresCache",false);key++;version++;Call(gpu,panel,"DibujaUnCuadro");
    foreach(object item in old)Check(item==null,"Toggle no libera texturas");
   }finally{Call(gpu,panel,"StopGpu");}
  }
  Console.WriteLine("PASS: 18 selecciones pixel-identicas, firmas, paginacion; HAL/texturas de color, cancelacion al toque, limpieza y luna.");
  return 0;
 }
}
