using System;
using System.IO;
using System.Drawing;
using System.Reflection;
class CuboFila
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
 static int Main(string[] args)
 {
  Assembly a=Assembly.LoadFrom(args[0]);
  Type form=a.GetType("Rocola.Form1",true),gpu=a.GetType("Rocola.GpuPanel",true);
  int checks=0;
  foreach(int w in new int[]{480,640,1024,1920,3440})
  foreach(int h in new int[]{220,260,340})
  foreach(string time in new string[]{"0:24 / 4:36","59:59 / 120:00","1:23:45 / 12:34:56"})
  {
   Rectangle cube=(Rectangle)gpu.GetMethod("IdleCoverRect",F).Invoke(null,new object[]{w,h});
   object[] p={w,h,"I Feel You","Depeche Mode",time,new string[0],new string[0],new Image[0],0,Color.MediumPurple,true,2,true,0};
   using(Bitmap panel=(Bitmap)form.GetMethod("RenderIdlePanelLayout",F).Invoke(null,p))
   {
    int start=(int)p[13];
    if(cube.Bottom!=h-8||cube.Top<40)throw new Exception("Cubo fuera de la fila");
    if(start<=cube.Right+24||w-24-start<80)throw new Exception("Tiempo/progreso se pisan");
    for(int y=cube.Top;y<cube.Bottom;y++)
    for(int x=cube.Left;x<cube.Right;x++)
      if(panel.GetPixel(x,y).A!=0)throw new Exception("Sigue el relleno detras del cubo");
    if(w==1920&&h==340&&time=="0:24 / 4:36")
     using(Bitmap preview=new Bitmap(w,h))
     using(Graphics g=Graphics.FromImage(preview))
     {
      g.Clear(Color.FromArgb(25,18,45));g.DrawImageUnscaled(panel,0,0);
      // Guia de geometria, no captura del cubo real ni de la aplicacion.
      g.DrawRectangle(Pens.Cyan,cube);
      g.DrawLine(Pens.Magenta,start,h-18,w-24,h-18);
      preview.Save(args[1]);
     }
    checks++;
   }
  }
  Console.WriteLine("PASS: "+checks+" casos; cubo alineado, fondo alfa cero y espacio para tiempo/progreso.");
  return 0;
 }
}
