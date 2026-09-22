using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
class EstelaEstelar {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static Type gpu;static int checks;
 static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Call(object o,string n,params object[] a){return gpu.GetMethod(n,F).Invoke(o,a);}
 static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 static void Tick(object panel,double time,float x,float y){Call(panel,"UpdateStarEchoes",time,x,y,65f,0f,0xFF82C6FFu);}
 static void Preview(object panel,string path){
  using(Bitmap star=(Bitmap)Call(null,"BakeStar",true))
  using(Bitmap scene=new Bitmap(1000,420))
  using(Graphics g=Graphics.FromImage(scene))
  using(Font font=new Font("Segoe UI",12)){
   g.Clear(Color.FromArgb(9,12,31));
   g.DrawString("Estrella GPU: textura real y posiciones guardadas de la estela",font,Brushes.LightSteelBlue,24,20);
   Array echoes=(Array)Get(panel,"starEchoes");
   int head=(int)Get(panel,"starEchoHead"),count=(int)Get(panel,"starEchoCount");
   for(int i=0;i<count;i++){
    object e=echoes.GetValue((head-count+i+echoes.Length)%echoes.Length);
    double age=2-(double)Get(e,"Time");float alpha=(float)Call(null,"StarEchoAlpha",age,1f);
    if(age<.055)continue;
    float r=(float)Get(e,"Radius")*(.80f+.15f*(float)(1-age/1.6));
    Paint(g,star,(float)Get(e,"X"),(float)Get(e,"Y"),r,(uint)Get(e,"Color"),alpha);
   }
   PointF current=(PointF)Call(null,"DetailedMoonCenter",1000,600,9.2,1f,0f,true);
   Paint(g,star,current.X,current.Y,65,0xFF82C6FFu,1);
   g.DrawString("Nucleo definido / ecos cromaticos / sin esfera texturizada",font,Brushes.LightSteelBlue,24,378);
   scene.Save(path,ImageFormat.Png);
  }
 }
 static void Paint(Graphics g,Bitmap b,float x,float y,float r,uint color,float alpha){
  using(ImageAttributes a=new ImageAttributes()){
   ColorMatrix m=new ColorMatrix();m.Matrix00=((color>>16)&255)/255f;m.Matrix11=((color>>8)&255)/255f;m.Matrix22=(color&255)/255f;m.Matrix33=alpha;a.SetColorMatrix(m);
   g.DrawImage(b,new Rectangle((int)(x-r),(int)(y-r),(int)(r*2),(int)(r*2)),0,0,b.Width,b.Height,GraphicsUnit.Pixel,a);
  }
 }
 [STAThread]static int Main(string[] args){
  gpu=Assembly.LoadFrom(args[0]).GetType("Rocola.GpuPanel");
  using(Control panel=(Control)Activator.CreateInstance(gpu,true)){
   panel.Size=new Size(1000,600);Set(panel,"idleBlend",1f);
   for(int i=0;i<=120;i++){
    PointF p=(PointF)Call(null,"DetailedMoonCenter",1000,600,6+i/60d*1.6,1f,0f,true);
    Tick(panel,i/60d,p.X,p.Y);
   }
   Check((int)Get(panel,"starEchoCount")==8,"Historia no acotada");
   Check(((Array)Get(panel,"starEchoBatch")).Length==48,"Geometria crece");
   object history=Get(panel,"starEchoes"),batch=Get(panel,"starEchoBatch");
   Preview(panel,System.IO.Path.Combine(System.IO.Path.GetDirectoryName(args[0]),"estrella-ecos.png"));
   for(int i=121;i<3000;i++)Tick(panel,i/60d,680+i*.1f,200);
   Check(Object.ReferenceEquals(history,Get(panel,"starEchoes"))&&Object.ReferenceEquals(batch,Get(panel,"starEchoBatch")),"Reasigna buffers");
   Set(panel,"EstelaEstelar",false);
   for(int i=3000;i<3120;i++)Tick(panel,i/60d,800,200);
   Check((int)Get(panel,"starEchoCount")==0,"Interruptor no vacia");
   Set(panel,"EstelaEstelar",true);Call(panel,"ResetStarEchoes");
   for(int i=0;i<300;i++)Tick(panel,i/60d,500,200);
   Check((int)Get(panel,"starEchoCount")==1,"Punto quieto acumula ecos");
   Tick(panel,8,600,200);Check((int)Get(panel,"starEchoCount")==1,"Pausa conserva ecos viejos");
   Tick(panel,1,610,200);Check((int)Get(panel,"starEchoCount")==1,"Reloj atras conserva ecos");
   panel.Width=1100;Tick(panel,1.02,620,200);Check((int)Get(panel,"starEchoCount")==1,"Resize conserva ecos");
   Set(panel,"LunaActiva",false);
   for(int i=0;i<90;i++)Tick(panel,1.04+i/60d,620,200);
   Check((int)Get(panel,"starEchoCount")==0,"Movimiento apagado conserva estela");
   foreach(int fps in new int[]{24,60,144,240}){
    Call(panel,"ResetStarEchoes");Set(panel,"LunaActiva",true);
    for(int i=0;i<=fps*2;i++)Tick(panel,i/(double)fps,300+i*70f/fps,200);
    Check((int)Get(panel,"starEchoCount")==8,"Capacidad depende de FPS");
    Check((float)Get(panel,"starEchoVisibility")>.99,"Fundido depende de FPS");
   }
   float previous=1;
   for(int i=0;i<=180;i++){
    float alpha=(float)Call(null,"StarEchoAlpha",i*.01,1f);
    Check(alpha>=0&&alpha<=previous,"No se desvanece");previous=alpha;
   }
   foreach(double age in new double[]{-1,double.NaN,double.PositiveInfinity,10})
    Check((float)Call(null,"StarEchoAlpha",age,1f)==0,"Edad invalida");
   Check(Codigo.Llama(gpu.GetMethod("DrawDetailedMoon",F),"DrawStarEchoes"),"Estela no conectada");
   Check(Codigo.Llama(gpu.GetMethod("DrawStarEchoes",F),"DrawTexturedBatch"),"No agrupa envios");
   foreach(string method in new string[]{"UpdateStarEchoes","DrawStarEchoes"})
    foreach(Codigo.Paso p in Codigo.Pasos(gpu.GetMethod(method,F)))Check(p.Op!=0x8D,"Reserva arreglos por cuadro");
   // Presentacion HAL real; la imagen anterior es una composicion de pruebas, no captura de pantalla.
   Set(panel,"ConLuna",true);
   if((bool)Call(panel,"StartGpu")){
    try{
     for(int i=0;i<10;i++)Check((bool)Call(panel,"DibujaUnCuadro"),"Presentacion HAL");
     IntPtr texture=(IntPtr)Get(panel,"lunaTex");
     Check(texture!=IntPtr.Zero,"No sube textura");
     Call(panel,"ResetStarEchoes");Set(panel,"idleBlend",1f);
     double now=(double)Call(null,"AhoraMs")/1000;
     for(int i=0;i<8;i++)Tick(panel,now-1.5+i*.18,500+i*8,150);
     Set(panel,"starEchoClock",now);Set(panel,"starEchoVisibility",1f);
     Check((bool)Call(panel,"DibujaUnCuadro"),"Presentacion HAL con ecos");
     object vertex=((Array)Get(panel,"starEchoBatch")).GetValue(0);
     Check(((uint)Get(vertex,"Color")>>24)>0,"HAL no dibuja geometria de la estela");
     Check((bool)Call(panel,"DibujaUnCuadro")&&texture==(IntPtr)Get(panel,"lunaTex"),"Vuelve a subir textura");
    }finally{Call(panel,"StopGpu");}
    Check((int)Get(panel,"starEchoCount")==0,"StopGpu no limpia historia");
   }else Console.WriteLine("HAL no disponible: falta comprobacion de presentacion.");
  }
  Console.WriteLine("OK estrella/estela: "+checks+" comprobaciones. Vista: estrella-ecos.png");return 0;
 }
}
