using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
class ReposoUnido
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static object Call(Type t,object o,string name,params object[] args){return t.GetMethod(name,F).Invoke(o,args);}
 static object Get(Type t,object o,string name){return t.GetField(name,F).GetValue(o);}
 static void Set(Type t,object o,string name,object value){t.GetField(name,F).SetValue(o,value);}
 static void Near(float a,float b,string message){Check(Math.Abs(a-b)<.002f,message+": "+a+" != "+b);}
 [STAThread]static int Main(string[] args)
 {
  Assembly asm=Assembly.LoadFrom((args.Length > 0 ? args[0] : "rocola.exe"));Type form=asm.GetType("Rocola.Form1"),gpu=asm.GetType("Rocola.GpuPanel");
  int cases=0;
  foreach(int w in new int[]{480,640,800,1024,1280,1499,1500,1920,2560,3440,3840})
  foreach(int h in new int[]{220,260,340})
  foreach(int count in new int[]{1,2,3,4,6,9,10,11,12})
  {
   Rectangle first=Rectangle.Empty;
   for(int i=0;i<count;i++)
   {
    Rectangle r=(Rectangle)Call(form,null,"IdleQueueCellCount",w,h,i,count,true);
    if(i==0)first=r;
    Check(r.Size==first.Size,"Tarjetas asimetricas");
    Check(r.X>=w*.52f-1 && r.Right<=w-24 && r.Top>=42 && r.Bottom<=h-32,"Fuera de cola");
    if(w>=1500)Check(r.Width<=520 && r.Height<=79,"Tarjetas gigantes con poca cola");
    Rectangle thumb=(Rectangle)Call(form,null,"IdleQueueThumb",r);
    Check(r.Contains(thumb) && thumb.Width>=20,"Miniatura oculta o recortada");
    for(int j=0;j<i;j++)Check(!r.IntersectsWith((Rectangle)Call(form,null,"IdleQueueCellCount",w,h,j,count,true)),"Cruce de tarjetas");
   }
   cases++;
  }
  foreach(int fps in new int[]{24,30,60,144,240})
  {
   float blend=0,previous=1;
   for(int i=0;i<fps*3;i++){
    blend=(float)Call(gpu,null,"StepIdle",blend,true,1.0/fps);
    float alpha=(float)Call(gpu,null,"IdleControlAlpha",blend,true);
    Check(alpha<=previous && alpha>=0f,"Atenuacion salta o invierte");previous=alpha;
   }
   Near(previous,0f,"Controles no desaparecen");
   for(int i=0;i<fps*2;i++){
    blend=(float)Call(gpu,null,"StepIdle",blend,false,1.0/fps);
    float alpha=(float)Call(gpu,null,"IdleControlAlpha",blend,true);
    Check(alpha>=previous && alpha<=1,"Restauracion salta o invierte");previous=alpha;
   }
   Near(previous,1,"No recupera controles");
  }
  Near((float)Call(gpu,null,"IdleControlAlpha",1f,false),1,"Toggle no desactiva");
  uint faded=(uint)Call(gpu,null,"FadeArgb",0xC8123456u,.02f);
  Check((faded&0xFFFFFFu)==0x123456 && (faded>>24)<=4,"El halo no se atenua o cambia de color");
  Type vtx=asm.GetType("Rocola.D3D9").GetNestedType("Vtx",F);
  int points=(int)Get(gpu,null,"AurCols")+1,nv=(int)Get(gpu,null,"AurBrilloVtx"),columns=(int)Get(gpu,null,"AurLineaCols");
  Array vertices=Array.CreateInstance(vtx,nv);
  float[] heights=new float[points],light=new float[points];uint[] colors=new uint[points];
  for(int i=0;i<points;i++){heights[i]=90;light[i]=1;colors[i]=0x99CCFF;}
  int seams=0;
  foreach(int h in new int[]{480,600,720,1080,1440,2160})
  foreach(int normal in new int[]{52,90,140})
  foreach(float blend in new float[]{0,.02f,.25f,.5f,.75f,.999f,1})
  {
   int expanded=(int)Call(gpu,null,"IdlePanelHeight",h);
   float top=(float)Call(gpu,null,"StatusPanelTop",h,normal,expanded,blend);
   float basis=(float)Call(gpu,null,"AuroraBaseAtStatus",h,normal,expanded,blend);
   Check((int)Call(gpu,null,"AurBrillo",vertices,heights,colors,light,colors,light,1920f,basis)>0,"No hay malla");
   for(int col=0;col<columns;col++){
    float y0=(float)Get(vtx,vertices.GetValue(col*30+12),"Y"),y1=(float)Get(vtx,vertices.GetValue(col*30+14),"Y");
    Near((y0+y1)*.5f+.5f,top+(int)Get(gpu,null,"StatusEdgeOffset"),"Horizonte despegado");
   }
   seams++;
  }
  using(Control panel=(Control)Activator.CreateInstance(gpu,true))
  using(Bitmap ui=new Bitmap(640,100))
  using(Bitmap bottom=new Bitmap(640,80))
  using(Bitmap idle=new Bitmap(640,220))
  {
   // Una UI visible: con una imagen vacia la optimizacion de teselas no envia
   // geometria. Se comprueba el buffer que realmente se envia, no uiQuad viejo.
   using(Graphics g=Graphics.FromImage(ui))g.Clear(Color.White);
   panel.Size=new Size(640,480);
   Set(gpu,panel,"GetUi",new Func<Bitmap>(delegate{return ui;}));
   Set(gpu,panel,"GetUiVersion",new Func<int>(delegate{return 1;}));
   Set(gpu,panel,"GetUiBot",new Func<Bitmap>(delegate{return bottom;}));
   Set(gpu,panel,"GetUiBotVersion",new Func<int>(delegate{return 1;}));
   Set(gpu,panel,"GetIdlePanel",new Func<Bitmap>(delegate{return idle;}));
   Set(gpu,panel,"IdleExpandTarget",true);
   Set(gpu,panel,"TransicionesTacto",false);
   Call(gpu,panel,"SetScene",1);
   if(!(bool)Call(gpu,panel,"StartGpu")){Console.WriteLine("  OK    NO SE PROBO AQUI: hace falta una tarjeta grafica de verdad (HAL). Esta bateria solo mide en la maquina del bar.");return 0;}
   try{
    IntPtr previous=IntPtr.Zero;
    foreach(float blend in new float[]{0,.2f,.5f,.8f,1,.8f,.5f,.2f}){
     Set(gpu,panel,"idleBlend",blend);
     Check((bool)Call(gpu,panel,"DibujaUnCuadro"),"No presenta");
     float top=(float)gpu.GetProperty("StatusTopY",F).GetValue(panel,null);
     int expectedAlpha=(int)((float)gpu.GetProperty("ControlsOpacity",F).GetValue(panel,null)*255);
     if(expectedAlpha>0){
      object tiles=Get(gpu,panel,"uiTeselas");
      Array drawn=tiles==null?(Array)Get(gpu,panel,"uiQuad"):(Array)Get(tiles.GetType(),tiles,"Vtx");
      Check(drawn.Length>0,"UI visible sin geometria");
      uint ink=(uint)Get(vtx,drawn.GetValue(0),"Color");
      Check((ink>>24)==expectedAlpha,"El alfa no llega a los controles dibujados");
     }
     // Alfa cero omite el envio; los vertices cacheados conservan su ultimo
     // color, no son una prueba de que la UI oculta se haya vuelto a dibujar.
     object aurora=Get(gpu,panel,"auCuadro");
     Near((float)Get(aurora.GetType(),aurora,"BaseY")-(float)Get(gpu,null,"AurLineaAlza"),top+1,"Seam en render real");
     if((float)Get(gpu,panel,"idleBlend")>0){
      Array quad=(Array)Get(gpu,panel,"adornoQuad");
      Near((float)Get(vtx,quad.GetValue(0),"Y")+.5f,top,"Panel usa otra coordenada");
     }
     IntPtr texture=(IntPtr)Get(gpu,panel,"uiTex");
     Check(texture!=IntPtr.Zero && (previous==IntPtr.Zero||texture==previous),"Fundido recrea textura UI");previous=texture;
    }
    Set(gpu,panel,"idleBlend",1f);
    Near((float)gpu.GetProperty("ControlsOpacity",F).GetValue(panel,null),0f,"Modo navegacion");
    foreach(int scene in new int[]{0,2,3,4}){
     Call(gpu,panel,"SetScene",scene);
     Near((float)gpu.GetProperty("ControlsOpacity",F).GetValue(panel,null),1,"Oculta interfaz activa");
    }
    Set(gpu,panel,"OndasUnidas",false);
    Call(gpu,panel,"DibujaUnCuadro");
    object legacy=Get(gpu,panel,"auCuadro");
    Near((float)Get(legacy.GetType(),legacy,"BaseY"),-(float)gpu.GetProperty("IdleRise",F).GetValue(panel,null),"Toggle no recupera union anterior");
   }finally{Call(gpu,panel,"StopGpu");}
  }
  Console.WriteLine("  OK    "+cases+" rejillas compactas/simetricas, fundido a 24-240 Hz, "+seams+" uniones de malla y render HAL sin recrear UI.");
  return 0;
 }
}
