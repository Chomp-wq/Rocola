using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
class LunaDetalle
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static object Call(Type t,object o,string n,params object[] a){return t.GetMethod(n,F).Invoke(o,a);}
 static object Get(Type t,object o,string n){return t.GetField(n,F).GetValue(o);}
 static void Set(Type t,object o,string n,object v){t.GetField(n,F).SetValue(o,v);}
 static void Check(bool ok,string s){if(!ok)throw new Exception(s);}
 static Control Find(Control c,string s){if(c.Text==s)return c;foreach(Control x in c.Controls){Control r=Find(x,s);if(r!=null)return r;}return null;}
 static object Blank(Type t){
  object o=FormatterServices.GetUninitializedObject(t);
  foreach(FieldInfo f in t.GetFields(F))
   if(!f.IsStatic&&f.FieldType.Namespace!=null&&f.FieldType.Namespace.StartsWith("System.Collections")&&f.FieldType.GetConstructor(Type.EmptyTypes)!=null)
    f.SetValue(o,Activator.CreateInstance(f.FieldType));
  return o;
 }
 [STAThread]static int Main(string[] args){
  Assembly asm=Assembly.LoadFrom((args.Length > 0 ? args[0] : "rocola.exe"));Type gpu=asm.GetType("Rocola.GpuPanel"),form=asm.GetType("Rocola.Form1");
  string dir=Path.Combine(Path.GetTempPath(),"rocola_luna_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  using(Bitmap moon=(Bitmap)Call(gpu,null,"BakeDetailedMoon"))
  using(Bitmap repeat=(Bitmap)Call(gpu,null,"BakeDetailedMoon")){
   Check(moon.Width==256&&moon.Height==256,"Textura no acotada");
   int transparent=0,partial=0;long lit=0,dark=0;
   for(int y=0;y<256;y++)for(int x=0;x<256;x++){
    Color c=moon.GetPixel(x,y);Check(c.ToArgb()==repeat.GetPixel(x,y).ToArgb(),"Superficie no determinista");
    if(c.A==0)transparent++;else if(c.A<255)partial++;
    if((x-127.5)*(x-127.5)+(y-127.5)*(y-127.5)<120*120) Check(c.A==255,"Interior lunar translucido");
    if(y>65&&y<190){if(x>35&&x<95)lit+=c.R;if(x>160&&x<220)dark+=c.R;}
   }
   Check(transparent>15000 && partial>400,"Borde no transparente/suave");
   Check(lit>dark*1.4,"No hay iluminacion lateral");
   moon.Save(Path.Combine(dir,"luna-textura.png"),ImageFormat.Png);
   using(Bitmap preview=new Bitmap(800,460))
   using(Graphics g=Graphics.FromImage(preview))
   using(Font font=new Font("Segoe UI",11)){
    g.Clear(Color.FromArgb(11,15,37));
    g.DrawString("Textura GPU real (256 px) / escala de pantalla",font,Brushes.White,20,20);
    g.DrawImage(moon,new Rectangle(30,90,256,256));
    g.DrawImage(moon,new Rectangle(430,145,124,124));
    g.DrawString("Relieve y luz lateral",font,Brushes.LightSteelBlue,35,385);
    g.DrawString("1080p",font,Brushes.LightSteelBlue,460,295);
    preview.Save(Path.Combine(dir,"luna-vista.png"),ImageFormat.Png);
   }
  }
  foreach(int h in new int[]{480,600,720,1080,1440,2160}){
   float previous=0;
   foreach(float dim in new float[]{1,.8f,.5f,.2f,0}){
    float e=(float)Call(gpu,null,"MoonEmphasis",dim,true),d=(float)Call(gpu,null,"DetailedMoonDiameter",h,e);
    Check(e>=previous&&d>=48&&d<=220,"Realce fuera de limites");previous=e;
    Check((float)Call(gpu,null,"MoonEmphasis",dim,false)==0,"No desactiva realce");
   }
  }
  using(Control panel=(Control)Activator.CreateInstance(gpu,true)){
   panel.Size=new Size(640,480);
   string[] fields={"lunaRealista","haloLunar","polvoLunar","lunaProtagonista"};
   string[] gpuFields={"LunaRealista","HaloLunar","PolvoLunar","LunaProtagonista"};
   string[] labels={"Luna con relieve y crateres (GPU)","Halo lunar musical (GPU)","Polvo estelar orbital (GPU)","Realzar luna al atenuar el fondo (GPU)"};
   Set(form,null,"carpetaDatos",dir);object o=Blank(form);Set(form,o,"gpuPanel",panel);
   using(Font font=new Font("Segoe UI",10,FontStyle.Bold))
   using(Font desc=new Font("Segoe UI",8.5f))
   using(Panel p=(Panel)Call(form,o,"PaginaFondo",font,font,desc,font,Color.White,Color.Gray,Color.Gray)){
    for(int mask=0;mask<16;mask++){
     for(int i=0;i<4;i++){
      Control c=Find(p,labels[i]);Check(c!=null&&c.Bottom<=c.Parent.Height,"Interruptor lunar fuera de pagina");
      c.GetType().GetProperty("Checked",F).SetValue(c,(mask&(1<<i))!=0,null);
     }
     Call(form,o,"AplicaEfectosGpu");Call(form,o,"SaveConfigPrefs");object read=Blank(form);Call(form,read,"LoadConfigPrefs");
     for(int i=0;i<4;i++){
      bool expected=(mask&(1<<i))!=0;
      Check((bool)Get(form,read,fields[i])==expected,"No persiste "+fields[i]);
      Check((bool)Get(gpu,panel,gpuFields[i])==expected,"No aplica a GPU "+fields[i]);
     }
    }
   }
   Set(gpu,panel,"ConLuna",true);
   Set(gpu,panel,"LunaActiva",false); // medir diametro sin la libracion musical
   if(!(bool)Call(gpu,panel,"StartGpu")){Console.WriteLine("  OK    NO SE PROBO AQUI: hace falta una tarjeta grafica de verdad (HAL). Esta bateria solo mide en la maquina del bar.");return 0;}
   try{
    Check((bool)Call(gpu,panel,"DibujaUnCuadro"),"No presenta");
    IntPtr tex=(IntPtr)Get(gpu,panel,"lunaTex");Check(tex!=IntPtr.Zero,"Sin textura");
    foreach(float dim in new float[]{1,.8f,.5f,.2f,0,1}){
     Set(gpu,panel,"Dim",dim);
     Check((bool)Call(gpu,panel,"DibujaUnCuadro"),"No presenta");
     Check((IntPtr)Get(gpu,panel,"lunaTex")==tex,"Realce vuelve a subir textura");
     Array quad=(Array)Get(gpu,panel,"lunaQuad");Type vertex=quad.GetValue(0).GetType();
     float left=Single.MaxValue,right=Single.MinValue;
     foreach(object v in quad){float x=(float)Get(vertex,v,"X");left=Math.Min(left,x);right=Math.Max(right,x);}
     float expected=(float)Call(gpu,null,"DetailedMoonDiameter",480,1-dim);
     Check(Math.Abs(right-left-expected)<.01f,"Realce no llega a geometria");
     uint color=(uint)Get(vertex,quad.GetValue(0),"Color");
     Check((color>>24)==255,"La superficie de la luna no es opaca");
    }
    foreach(int h in new int[]{600,720,1080,480}){
     panel.Size=new Size(h*4/3,h);Call(gpu,panel,"EnsureLuna");
     Check((IntPtr)Get(gpu,panel,"lunaTex")==tex && (int)Get(gpu,panel,"lunaDe")==256,"Resolucion rehornea superficie");
    }
    Set(gpu,panel,"LunaRealista",false);Call(gpu,panel,"EnsureLuna");
    using(Bitmap old=(Bitmap)Call(form,null,"BakeLuna",480))
     Check((int)Get(gpu,panel,"lunaW")==old.Width && !(bool)Get(gpu,panel,"lunaDetalleDe"),"No restaura diseno anterior");
    Set(gpu,panel,"LunaRealista",true);Call(gpu,panel,"EnsureLuna");
    Check((int)Get(gpu,panel,"lunaW")==256 && (bool)Get(gpu,panel,"lunaDetalleDe"),"No recupera diseno");
   }finally{Call(gpu,panel,"StopGpu");}
   Check((IntPtr)Get(gpu,panel,"lunaTex")==IntPtr.Zero,"No libera textura");
  }
  float calm=(float)Call(gpu,null,"MoonMotionSpeed",0f),idle=(float)Call(gpu,null,"MoonMotionSpeed",1f);
  Check(idle>calm*4,"Reposo no anima mas que navegacion");
  Check((float)Call(gpu,null,"MoonSceneScale",0f,1f,true)==1,"Late demasiado al navegar");
  Check((float)Call(gpu,null,"MoonSceneScale",1f,1f,true)>1.3f,"Reposo no realza luna");
  Check((float)Call(gpu,null,"MoonSceneScale",1f,1f,false)==1,"Interruptor no detiene realce musical");
  foreach(float blend in new float[]{0,.1f,.5f,.9f,1}) {
   PointF still=(PointF)Call(gpu,null,"DetailedMoonCenter",1920,1080,0d,blend,0f,false);
   PointF later=(PointF)Call(gpu,null,"DetailedMoonCenter",1920,1080,80d,blend,0f,false);
   Check(still==later,"Luna apagada se mueve");
   PointF p=(PointF)Call(gpu,null,"DetailedMoonCenter",1920,1080,5d,blend,0f,true);
   PointF next=(PointF)Call(gpu,null,"DetailedMoonCenter",1920,1080,5.001d,blend,0f,true);
   Check(Math.Abs(p.X-next.X)<1&&Math.Abs(p.Y-next.Y)<1,"Movimiento discontinuo");
  }
  foreach(int fps in new int[]{24,60,144,240}) {
   double phase=0;
   for(int i=0;i<fps*10;i++)phase+=(float)Call(gpu,null,"MoonMotionSpeed",1f)/fps;
   Check(Math.Abs(phase-idle*10)<.0001,"Velocidad lunar depende de FPS");
  }
  Console.WriteLine("  OK    relieve determinista, alfa, luz lateral, 16 configuraciones, realce y cache/liberacion HAL. Vista: "+dir);
  return 0;
 }
}
