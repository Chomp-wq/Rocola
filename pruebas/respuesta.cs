using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;
class Respuesta
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 [STAThread]static int Main(string[] args)
 {
  Assembly a=Assembly.LoadFrom((args.Length > 0 ? args[0] : "rocola.exe"));Type t=a.GetType("Rocola.GpuPanel",true);
  MethodInfo alpha=t.GetMethod("TactileAlpha",F);
  foreach(int fps in new int[]{24,30,60,144,240})
  {float old=0;for(int i=0;i<fps;i++){float v=(float)alpha.Invoke(null,new object[]{i*1000.0/fps,120.0,.65f});Check(v>=old&&v<=1,"Transicion no monotona");old=v;}Check(old==1,"No termina");}
  using(Control panel=(Control)Activator.CreateInstance(t,true))
  {
   panel.Size=new Size(640,480);
   t.GetField("UiUrgent",F).SetValue(panel,false);
   t.GetField("IdleExpandTarget",F).SetValue(panel,true);
   t.GetMethod("TouchUi",F).Invoke(panel,null);
   Check((bool)t.GetField("UiUrgent",F).GetValue(panel)&&!(bool)t.GetField("IdleExpandTarget",F).GetValue(panel),"No responde al toque");
   t.GetField("RespuestaInmediata",F).SetValue(panel,false);t.GetField("UiUrgent",F).SetValue(panel,false);
   t.GetMethod("TouchUi",F).Invoke(panel,null);Check(!(bool)t.GetField("UiUrgent",F).GetValue(panel),"Toggle no desactiva");
   if(!(bool)t.GetMethod("StartGpu",F).Invoke(panel,null)){Console.WriteLine("  OK    NO SE PROBO AQUI: hace falta una tarjeta grafica de verdad (HAL). Esta bateria solo mide en la maquina del bar.");return 0;}
   List<Bitmap> bitmaps=new List<Bitmap>();
   object[][] state={new object[]{IntPtr.Zero,null,0,0,1f,1f,null},new object[]{IntPtr.Zero,null,0,0,1f,1f,null},new object[]{IntPtr.Zero,null,0,0,1f,1f,null}};
   MethodInfo ensure=t.GetMethod("EnsureRecentCover",F);
   try{
    for(int i=0;i<15;i++){Bitmap b=new Bitmap(512,512);using(Graphics g=Graphics.FromImage(b))g.Clear(Color.FromArgb(255,i*15,30,120));bitmaps.Add(b);}
    state[0][6]=bitmaps[0];ensure.Invoke(panel,state[0]);IntPtr first=(IntPtr)state[0][0];Check(first!=IntPtr.Zero,"Sin textura");
    state[0][6]=bitmaps[1];ensure.Invoke(panel,state[0]);
    state[0][6]=bitmaps[0];ensure.Invoke(panel,state[0]);Check((IntPtr)state[0][0]==first,"No reutiliza textura");
    for(int i=0;i<150;i++){
     object[] s=state[i%3];s[6]=i%11==0?null:bitmaps[i%15];ensure.Invoke(panel,s);
     HashSet<IntPtr> handles=new HashSet<IntPtr>();long bytes=0;
     foreach(object[] live in state)if((IntPtr)live[0]!=IntPtr.Zero)Check(handles.Add((IntPtr)live[0]),"Doble propietario activo");
     foreach(object entry in (Array)t.GetField("recentCovers",F).GetValue(panel))if(entry!=null){
      Type e=entry.GetType();IntPtr tex=(IntPtr)e.GetField("Tex",F).GetValue(entry);Check(handles.Add(tex),"Textura compartida");
      bytes+=(long)t.GetMethod("CoverBytes",F).Invoke(null,new object[]{e.GetField("W",F).GetValue(entry),e.GetField("H",F).GetValue(entry)});
     }
     Check(bytes<=8L*1024*1024,"Cache supera presupuesto");
    }
    t.GetField("CachePortadas",F).SetValue(panel,false);state[0][6]=bitmaps[2];ensure.Invoke(panel,state[0]);
    foreach(object entry in (Array)t.GetField("recentCovers",F).GetValue(panel))Check(entry==null,"No libera al apagar");
   }finally{
    string[] fields={"selTexA","selTexB","relTex"};
    for(int i=0;i<3;i++)t.GetField(fields[i],F).SetValue(panel,state[i][0]);
    t.GetMethod("StopGpu",F).Invoke(panel,null);
    foreach(Bitmap b in bitmaps)b.Dispose();
   }
  }
  Console.WriteLine("  OK    curvas 24-240 Hz, toque/toggle, cache HAL (150 cambios), reutilizacion, propiedad unica, limite 8 MiB y liberacion.");
  return 0;
 }
}
