using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using System.Collections.Generic;
class EstrellaFlujo {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static Type gpu,form,flow;static int checks;static object aurora,music;
 static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Call(Type t,object o,string n,params object[] a){return t.GetMethod(n,F).Invoke(o,a);}
 static object Run(object o,string n,params object[] a){return Call(gpu,o,n,a);}
 static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 static T Aurora<T>(){return (T)aurora;}
 static T Music<T>(){return (T)music;}
 static void Bounds(Control page){
  foreach(Control c in page.Controls){
   Check(c.Left>=0&&c.Top>=0&&c.Right<=page.ClientSize.Width&&c.Bottom<=page.ClientSize.Height,"Control recortado: "+c.Text);
   foreach(Control d in page.Controls)if(c!=d)Check(!c.Bounds.IntersectsWith(d.Bounds),"Controles solapados: "+c.Text+" / "+d.Text);
   if(c.HasChildren)Bounds(c);
  }
 }
 static Control Find(Control p,string name){foreach(Control c in p.Controls){if(c.Text==name)return c;Control r=Find(c,name);if(r!=null)return r;}return null;}
 static void Dynamics(){
  foreach(int height in new int[]{480,600,720,1080,1440,2160})
  foreach(int width in new int[]{640,1280,1920,3440}){
   float radius=(float)Run(null,"DetailedMoonDiameter",height,1f)*.67f;
   float minX=width,maxX=0,minY=height,maxY=0;
   for(int i=0;i<900;i++){
    PointF p=(PointF)Run(null,"StarTravelCenter",width,height,i*.037,i*.051,radius,height*.72f,height*.20f);
    Check(p.X-radius>=0&&p.X+radius<=width&&p.Y-radius>=0&&p.Y+radius<=height*.72f+.1f,"Sale del cielo");
    minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);minY=Math.Min(minY,p.Y);maxY=Math.Max(maxY,p.Y);
   }
   if(width>=height){Check(minX<width*.32f&&maxX>width*.68f,"Confinada a la derecha");Check(maxY-minY>height*.05f,"Sin recorrido vertical");}
  }
  double reference=-1;
  foreach(int fps in new int[]{24,60,144,240}){
   using(Control p=(Control)Activator.CreateInstance(gpu,true)){
    Set(p,"idleBlend",1f);
    for(int i=0;i<fps*20;i++)Run(p,"AdvanceStarMusic",1f/fps,.4f,.3f,.2f);
    double phase=(double)Get(p,"starTravelX");
    if(reference<0)reference=phase;else Check(Math.Abs(phase-reference)<.035,"Movimiento depende de FPS");
   }
  }
  double[] travel=new double[4];
  for(int mode=0;mode<4;mode++){
   using(Control p=(Control)Activator.CreateInstance(gpu,true)){
    Set(p,"idleBlend",mode==3?0f:1f);Run(p,"SetStarGenre",mode==2?"Ambient":"Rock");
    for(int i=0;i<600;i++)Run(p,"AdvanceStarMusic",1f/60,mode==0?0f:.7f,mode==0?0f:.5f,mode==0?0f:.4f);
    travel[mode]=(double)Get(p,"starTravelX");
    if(mode==1){
     Check((float)Get(p,"starDrive")>.5f,"La musica no impulsa");
     for(int i=0;i<600;i++)Run(p,"AdvanceStarMusic",1f/60,0f,0f,0f);
     Check((float)Get(p,"starDrive")<.001f,"Silencio no calma");
     double prior=(double)Get(p,"starTravelX");Set(p,"LunaActiva",false);
     Run(p,"AdvanceStarMusic",.1f,1f,1f,1f);Check((double)Get(p,"starTravelX")==prior,"Toggle no detiene");
    }
   }
  }
  Check(travel[1]>travel[0]*2,"No distingue silencio/musica");
  Check(travel[1]>travel[2],"Genero no matiza");Check(travel[1]>travel[3]*2,"No calma al navegar");
  Check((float)Run(null,"StarGenreDrive",(object)null)==1,"Genero ausente");
  using(Control bass=(Control)Activator.CreateInstance(gpu,true))
  using(Control high=(Control)Activator.CreateInstance(gpu,true)){
   Set(bass,"idleBlend",1f);Set(high,"idleBlend",1f);
   for(int i=0;i<300;i++){
    Run(bass,"AdvanceStarMusic",1f/60,1f,0f,0f);
    Run(high,"AdvanceStarMusic",1f/60,0f,0f,1f);
   }
   Check((double)Get(bass,"starTravelX")>(double)Get(high,"starTravelX"),"Graves no distinguen recorrido");
   Check((double)Get(high,"starTravelY")>(double)Get(bass,"starTravelY"),"Agudos no distinguen recorrido");
   Type type=gpu.Assembly.GetType("Rocola.StarMusic");music=Activator.CreateInstance(type);
   Set(music,"Bass",.8f);Set(music,"Mid",.7f);Set(music,"High",.6f);
   Set(bass,"GetStarMusic",Delegate.CreateDelegate(gpu.GetField("GetStarMusic",F).FieldType,typeof(EstrellaFlujo).GetMethod("Music",F).MakeGenericMethod(type)));
   Set(bass,"lunaClock",(double)Run(null,"AhoraMs")-50);
   Run(bass,"UpdateMoonMusic",0f);
   Check((float)Get(bass,"lunaBeat")>0&&(float)Get(bass,"lunaMid")>0&&(float)Get(bass,"lunaHigh")>0,"Apagar aurora corta musica de estrella");
  }
 }
 static void Shape(string dir){
  using(Bitmap b=(Bitmap)Run(null,"BakeStar",true)){
   for(int i=0;i<5;i++){
    double a=-Math.PI*.5+i*Math.PI*2/5;
    Check(b.GetPixel((int)(127.5+91*Math.Cos(a)),(int)(127.5+91*Math.Sin(a))).A==255,"Falta punta "+i);
    a+=Math.PI/5;
    Check(b.GetPixel((int)(127.5+85*Math.Cos(a)),(int)(127.5+85*Math.Sin(a))).A<30,"No recorta valle "+i);
   }
   Check(b.GetPixel(127,127).A==255,"Interior transparente");
   b.Save(Path.Combine(dir,"estrella-icono.png"),ImageFormat.Png);
  }
 }
 static void Palette(Control p){
  uint[] source={0xFFFFFFu,0u,0xFF80C0u,0x70BFFFu};
  foreach(double pos in new double[]{0,.001,.25,.499,.75,.999,1,1000.25,-.25}){
   uint a=(uint)Call(flow,null,"Ring",source,pos),b=(uint)Call(flow,null,"Ring",source,pos+1);
   Check(a==b,"Salto al cerrar circulo de color");
  }
  Set(p,"ColoresFluidos",true);Set(p,"NebColor",0xFF3080FFu);Set(p,"NebColor2",0xFFFF4070u);
  Run(p,"SetCoverPalette",new Color[]{Color.White,Color.Black,Color.HotPink,Color.SteelBlue},1f);
  double now=0;for(int i=0;i<600;i++)Run(p,"AdvanceCoverPalette",now+=16);
  object colors=Get(p,"flowColors"),mesh=Get(p,"flowNebula");
  HashSet<uint> seen=new HashSet<uint>();
  for(int i=0;i<4000;i++){
   Run(p,"AdvanceCoverPalette",now+=16);
   uint c=(uint)Run(p,"FlowColor",0x80AABBCCu,.5f,.4f);
   Check((c>>24)==128,"Flujo cambia alfa");seen.Add(c&0xFFFFFFu);
  }
  Check(seen.Count>100,"Colores siguen fijos en el centro");
  Check(Object.ReferenceEquals(colors,Get(p,"flowColors"))&&Object.ReferenceEquals(mesh,Get(p,"flowNebula")),"Reasigna buffers de flujo");
  Set(p,"ColoresFluidos",false);for(int i=0;i<600;i++)Run(p,"AdvanceCoverPalette",now+=16);
  Check((uint)Run(p,"FlowColor",0x80554433u,.5f,.4f)==0x80554433u,"Apagar flujo no recupera colores");
  Set(p,"ColoresFluidos",true);Run(p,"SetCoverPalette",null,0f);
  for(int i=0;i<600;i++)Run(p,"AdvanceCoverPalette",now+=16);
  uint before=(uint)Run(p,"FlowColor",0xFFFFFFFFu,.2f,.2f);
  for(int i=0;i<600;i++)Run(p,"AdvanceCoverPalette",now+=16);
  Check(before!=(uint)Run(p,"FlowColor",0xFFFFFFFFu,.2f,.2f),"Sin caratula el genero queda estatico");
 }
 static void Config(Control panel){
  string dir=Path.Combine(Path.GetTempPath(),"rocola_flujo_config_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  form.GetField("carpetaDatos",F).SetValue(null,dir);
  object owner=Codigo.FormSinEncender(form);Set(owner,"gpuPanel",panel);
  using(Font f=new Font("Segoe UI",9))
  using(Panel page=(Panel)Call(form,owner,"PaginaFondo",f,f,f,f,Color.White,Color.Gray,Color.Gray)){
   Bounds(page);
   Control toggle=Find(page,"Colores del fondo en movimiento (GPU)");
   Check(toggle!=null,"Falta control");
   foreach(bool b in new bool[]{true,false,true}){
    toggle.GetType().GetProperty("Checked",F).SetValue(toggle,b,null);
    Call(form,owner,"SaveConfigPrefs");
    object read=Codigo.FormSinEncender(form);Call(form,read,"LoadConfigPrefs");
    Check((bool)Get(read,"coloresFluidos")==b&&(bool)Get(panel,"ColoresFluidos")==b,"No persiste/aplica flujo");
   }
  }
 }
 static uint VertexColor(object holder,string field){return (uint)Get(((Array)Get(holder,field)).GetValue(0),"Color");}
 static void Hal(Control panel,Assembly asm){
  panel.Size=new Size(960,600);Set(panel,"ConLuna",true);Set(panel,"ColoresFluidos",true);
  Set(panel,"Nieblas",2);Set(panel,"LunaActiva",true);
  Type au=asm.GetType("Rocola.GpuAurora");aurora=Activator.CreateInstance(au);
  Set(aurora,"Has",true);Set(aurora,"Alto",170f);Set(aurora,"BaseY",510f);Set(aurora,"Dim",1f);Set(aurora,"DimFilo",1f);
  Set(aurora,"Rgb",0x40AAFFu);Set(aurora,"Rgb2",0xC060FFu);Set(aurora,"RgbA",0xFF7090u);Set(aurora,"Rgb2A",0x90FFD0u);
  float[] bands=new float[32];for(int i=0;i<32;i++)bands[i]=.5f;
  Set(aurora,"B",bands);
  Set(panel,"GetAurora",Delegate.CreateDelegate(gpu.GetField("GetAurora",F).FieldType,typeof(EstrellaFlujo).GetMethod("Aurora",F).MakeGenericMethod(au)));
  if(!(bool)Run(panel,"StartGpu")){Console.WriteLine("HAL no disponible: no comprobado dibujo real.");return;}
  try{
   Set(panel,"flowBlend",1f);Check((bool)Run(panel,"DibujaUnCuadro"),"No presenta");
   IntPtr texture=(IntPtr)Get(panel,"lunaTex"),neb=(IntPtr)Get(panel,"nebTex");
   object sky=Get(panel,"stars"),mesh=Get(panel,"flowNebula");
   uint oldSky=VertexColor(sky,"flowSky"),oldNeb=VertexColor(panel,"flowNebula"),oldStar=VertexColor(sky,"starV");
   uint oldAur=((uint[])Get(panel,"aurNc"))[0];
   Set(panel,"flowPhase",.65d);
   Check((bool)Run(panel,"DibujaUnCuadro"),"No presenta flujo");
   Check(oldSky!=VertexColor(sky,"flowSky"),"Cielo fijo");
   Check(oldNeb!=VertexColor(panel,"flowNebula"),"Nebulosa fija");
   Check((oldStar&0xFFFFFFu)!=(VertexColor(sky,"starV")&0xFFFFFFu),"Estrellas fijas");
   Check(oldAur!=((uint[])Get(panel,"aurNc"))[0],"Aurora fija");
   Check(texture==(IntPtr)Get(panel,"lunaTex")&&neb==(IntPtr)Get(panel,"nebTex"),"Colores resuben textura");
   Check(Object.ReferenceEquals(mesh,Get(panel,"flowNebula")),"Malla no reutilizada");
   foreach(float b in bands)Check(b==.5f,"Flujo modifica las frecuencias");
   // Comparacion de trabajo local (excluye Present), no benchmark del programa con biblioteca/audio.
   foreach(bool enabled in new bool[]{false,true,false,true}){
    Set(panel,"ColoresFluidos",enabled);Set(panel,"flowBlend",enabled?1f:0f);
    for(int i=0;i<20;i++)Check((bool)Run(panel,"DibujaUnCuadro"),"Calentamiento HAL");
    Run(panel,"ReiniciaMedida");
    for(int i=0;i<100;i++)Check((bool)Run(panel,"DibujaUnCuadro"),"Medicion HAL");
    double ms=(double)gpu.GetProperty("MsTrabajo",F).GetValue(panel,null);
    Console.WriteLine("HAL sintetico 960x600, flujo "+enabled+": "+ms.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" ms/cuadro sin Present.");
   }
  }finally{Run(panel,"StopGpu");}
 }
 [STAThread]static int Main(string[] args){
  Assembly asm=Assembly.LoadFrom(args[0]);gpu=asm.GetType("Rocola.GpuPanel");form=asm.GetType("Rocola.Form1");flow=asm.GetType("Rocola.PaletteFlow");
  Dynamics();Shape(Path.GetDirectoryName(args[0]));
  using(Control p=(Control)Activator.CreateInstance(gpu,true)){Palette(p);Config(p);Hal(p,asm);}
  foreach(string name in new string[]{"AdvanceStarMusic","AdvanceColorFlow","DrawFlowNebula"})
   foreach(Codigo.Paso step in Codigo.Pasos(gpu.GetMethod(name,F)))Check(step.Op!=0x8D,"Reserva arreglos por cuadro: "+name);
  Console.WriteLine("OK estrella/icono/flujo: "+checks+" comprobaciones. No mide FPS del programa con musica.");return 0;
 }
}
