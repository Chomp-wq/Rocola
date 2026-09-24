using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
class CristalCola
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static Control Find(Control c,string text){if(c.Text==text)return c;foreach(Control x in c.Controls){Control r=Find(x,text);if(r!=null)return r;}return null;}
 static object Blank(Type t){
  object o=FormatterServices.GetUninitializedObject(t);
  foreach(FieldInfo f in t.GetFields(F))
   if(!f.IsStatic&&f.FieldType.Namespace!=null&&f.FieldType.Namespace.StartsWith("System.Collections")&&f.FieldType.GetConstructor(Type.EmptyTypes)!=null)
    f.SetValue(o,Activator.CreateInstance(f.FieldType));
  return o;
 }
 [STAThread]static int Main(string[] args)
 {
  Type t=Assembly.LoadFrom((args.Length > 0 ? args[0] : "rocola.exe")).GetType("Rocola.Form1",true);
  string dir=Path.Combine(Path.GetTempPath(),"rocola_cristal_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  t.GetField("carpetaDatos",F).SetValue(null,dir);
  object o=Blank(t);
  using(Font font=new Font("Segoe UI",10,FontStyle.Bold))
  using(Font desc=new Font("Segoe UI",8.5f))
  using(Panel p=(Panel)t.GetMethod("PaginaFondo",F).Invoke(o,new object[]{font,font,desc,font,Color.White,Color.Gray,Color.Gray}))
  {
   string[] labels={"Interior de discos con cristal (GPU)","Cola ampliada de 12 canciones (GPU)","Respuesta inmediata al toque (GPU)","Transiciones de vista y seleccion (GPU)","Vidrio por capas y color vivo (GPU)","Transiciones suaves de color (GPU)","Estrella viajera con orbitas (GPU)","Ocultar controles en reposo (GPU)","Ondas unidas al estado (GPU)"};
   string[] fields={"cancionesCristal","colaDoce","respuestaInmediata","transicionesTacto","glassCapas","coloresCache","lunaActiva","controlesReposo","ondasUnidas"};
   foreach(Control card in p.Controls){
    if(card.Bottom>p.Height||card.Right>p.Width)throw new Exception("Tarjeta fuera de Fondo");
    foreach(Control c in card.Controls){
     if(c.Top<32||c.Bottom>card.Height||c.Right>card.Width)throw new Exception("Control recortado: "+c.Text);
     foreach(Control other in card.Controls)if(other!=c&&c.Bounds.IntersectsWith(other.Bounds))throw new Exception("Mandos solapados");
    }
   }
   for(int mask=0;mask<(1<<fields.Length);mask++){
    for(int i=0;i<fields.Length;i++){Control c=Find(p,labels[i]);if(c==null||c.Bottom>c.Parent.Height)throw new Exception("Falta control");
     c.GetType().GetProperty("Checked",F).SetValue(c,(mask&(1<<i))!=0,null);
     if((bool)t.GetField(fields[i],F).GetValue(o)!=((mask&(1<<i))!=0))throw new Exception("Evento incorrecto");}
    t.GetMethod("SaveConfigPrefs",F).Invoke(o,null);object read=Blank(t);t.GetMethod("LoadConfigPrefs",F).Invoke(read,null);
    for(int i=0;i<fields.Length;i++)if((bool)t.GetField(fields[i],F).GetValue(read)!=((mask&(1<<i))!=0))throw new Exception("No persiste "+fields[i]);
   }
  }
  foreach(int w in new int[]{480,640,800,1024,1280,1920,3440})
  foreach(int count in new int[]{0,1,2,3,6,10,12,20})
  using(Bitmap cover=new Bitmap(32,32))
  {
   using(Graphics cg=Graphics.FromImage(cover))cg.Clear(Color.Lime);
   int h=w<1100?220:340,n=Math.Min(12,count);string[] titles=new string[n],artists=new string[n];Image[] covers=new Image[n];
   for(int i=0;i<n;i++){titles[i]="Cancion "+(i+1)+" - titulo largo";artists[i]="Depeche Mode";covers[i]=cover;}
   for(int i=0;i<12;i++){Rectangle r=(Rectangle)t.GetMethod("IdleQueueCell",F).Invoke(null,new object[]{w,h,i,true});
    if(r.Right>w-24||r.Bottom>h-32||r.Width<1||r.Height<1)throw new Exception("Celda recortada");
    for(int j=0;j<i;j++)if(r.IntersectsWith((Rectangle)t.GetMethod("IdleQueueCell",F).Invoke(null,new object[]{w,h,j,true})))throw new Exception("Cruce cola");}
   using(Bitmap b=(Bitmap)t.GetMethod("RenderIdlePanelQueue",F).Invoke(null,new object[]{w,h,"I Feel You","Depeche Mode","0:24 / 4:36",titles,artists,covers,count,Color.MediumPurple,true,2,true}))
   using(Bitmap preview=new Bitmap(w,h)){
    for(int i=0;i<n;i++){
     Rectangle cell=(Rectangle)t.GetMethod("IdleQueueCellCount",F).Invoke(null,new object[]{w,h,i,n,true});
     Rectangle thumb=(Rectangle)t.GetMethod("IdleQueueThumb",F).Invoke(null,new object[]{cell});
     if(!cell.Contains(thumb)||thumb.Width<20)throw new Exception("Miniatura fuera de tarjeta");
     if(b.GetPixel(thumb.X+3,thumb.Y+3).ToArgb()!=Color.Lime.ToArgb())throw new Exception("Miniatura no visible");
     for(int j=0;j<i;j++)
      if(cell.IntersectsWith((Rectangle)t.GetMethod("IdleQueueCellCount",F).Invoke(null,new object[]{w,h,j,n,true})))throw new Exception("Cruce cola adaptable");
    }
    using(Graphics g=Graphics.FromImage(preview)){g.Clear(Color.FromArgb(23,17,44));g.DrawImageUnscaled(b,0,0);}
    if(count==12||count==2)preview.Save(Path.Combine(dir,"cola-"+w+"-"+count+".png"));
   }
  }
  using(Bitmap b=(Bitmap)t.GetMethod("BakeSongGlass",F).Invoke(null,new object[]{900,420})){
   if(b.GetPixel(0,0).A!=0||b.GetPixel(450,210).A<180||b.GetPixel(450,210).A>=255)throw new Exception("Cristal sin transparencia");
   b.Save(Path.Combine(dir,"cristal.png"));
  }
  string source=(args.Length > 1 ? File.ReadAllText((args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "cristalcola.png"))) : Codigo.FuenteDeForm1());
  string guard=null;foreach(string line in source.Split('\n'))if(line.Contains("cc != lTit"))guard=line;
  if(guard==null||!guard.Contains("cc != bPagF")||!guard.Contains("cc != pFon"))throw new Exception("Fondo se oculta");
  Console.WriteLine("  OK    512 combinaciones de opciones, Fondo sin cruces, 56 paneles, miniaturas visibles, vidrio y pestanas. Imagenes: "+dir);
  return 0;
 }
}
