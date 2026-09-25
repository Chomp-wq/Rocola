using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
class TextosGpu
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static int checks;
    static Type form,gpu,groups;
    static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
    static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
    static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,F).Invoke(o,args);}
    static void Check(bool b,string text){if(!b)throw new Exception(text);checks++;}
    static object Static(Type t,string n,params object[] args){return t.GetMethod(n,F).Invoke(null,args);}
    static IList Runs(object f,int index){return (IList)Get(((Array)Get(f,"cintasTexto")).GetValue(index),"Items");}
    [STAThread] static int Main(string[] args)
    {
        Assembly a=Assembly.LoadFrom(args[0]);form=a.GetType("Rocola.Form1");gpu=a.GetType("Rocola.GpuPanel");
        groups=a.GetType("Rocola.CintasTexto");
        string[,] cases={{"03 - Dreamer","Dreamer"},{"02-Crazy Train","Crazy Train"},{"-07 - Hellraiser","Hellraiser"},
            {"01.Feel You","Feel You"},{"  009 — Canción","Canción"},{"99 Luftballons","99 Luftballons"},{"1979","1979"},
            {"2001 - A Space Odyssey","2001 - A Space Odyssey"},{"3.14 Song","3.14 Song"},{"03 -  ","03 -  "},{"",""}};
        for(int i=0;i<cases.GetLength(0);i++)Check((string)Static(form,"TituloSinPista",cases[i,0])==cases[i,1],"Prefijo: "+cases[i,0]);
        Check(Static(form,"TituloSinPista",new object[]{null})==null,"null");
        using(Bitmap canvas=new Bitmap(800,120))
        using(Graphics g=Graphics.FromImage(canvas))
        using(Font font=new Font("Tahoma",20,FontStyle.Bold,GraphicsUnit.Pixel))
        using(StringFormat sf=new StringFormat())
        {
            object group=Activator.CreateInstance(groups,true);
            RectangleF rect=new RectangleF(12,20,170,30);
            string title="Una canción con un nombre muy largo para leer por completo";
            Call(group,"Begin");
            Check(!(bool)Call(group,"Add",g,"Corta",font,Color.White,rect,sf),"Mueve texto que cabe");
            Check((bool)Call(group,"Add",g,title,font,Color.White,rect,sf),"No captura texto largo");
            Call(group,"End");
            IList list=(IList)Get(group,"Items"); object run=list[0];Bitmap[] tiles=(Bitmap[])Get(run,"Tiles");
            int born=(int)Get(run,"Born");
            for(int i=0;i<tiles.Length;i++)Check(tiles[i].Width<=1024,"Textura demasiado ancha");
            for(int j=0;j<50;j++){
                Call(group,"Begin");Call(group,"Add",g,title,font,Color.White,rect,sf);Call(group,"End");
                Check(ReferenceEquals(run,list[0]),"Recrea la cache al cambiar reloj");
                Check((int)Get(list[0],"Born")==born,"Reinicia la animacion");
            }
            Call(group,"Begin");Call(group,"Add",g,title,font,Color.Cyan,rect,sf);Call(group,"End");
            Check((int)Get(list[0],"Born")==born,"El color reinicia el desplazamiento");
            Call(group,"Begin");
            Check(!(bool)Call(group,"Add",g,new string('W',20000),font,Color.White,rect,sf),"Sin limite para metadatos corruptos");
            Call(group,"End");Check(list.Count==0,"No libera titulos retirados");
            ((IDisposable)group).Dispose();
            float excess=360;
            double ida=Math.Max(1200,excess/42*1000.0),vuelta=Math.Max(700,excess/140*1000.0);
            Check((float)Static(gpu,"CorreTitulo",1000.0,excess)==0,"Sin pausa inicial");
            Check(Math.Abs((float)Static(gpu,"CorreTitulo",2200+ida+600,excess)-excess)<.01,"No muestra final");
            Check((float)Static(gpu,"CorreTitulo",2200+ida+1400+vuelta+100,excess)==0,"No repite");
            for(int i=0;i<500;i++){float x=(float)Static(gpu,"CorreTitulo",i*100.0,excess);Check(x>=0&&x<=excess,"Fuera del rango");}
        }
        object host=Codigo.FormSinEncender(form);Set(host,"gpuEnabled",true);Set(host,"textosDeslizantes",true);
        foreach(int width in new int[]{800,1024,1920}){
            using(Bitmap canvas=new Bitmap(width,88))
            using(Graphics g=Graphics.FromImage(canvas)){
                g.Clear(Color.FromArgb(12,20,44));
                using((IDisposable)Call(host,"CapturaTexto",0))
                    Static(form,"PintaTarjetaSiguiente",g,"Depeche Mode - Enjoy the Silence (Live in Berlin / Extended Version)",new Rectangle(12,5,Math.Min(300,width/3),72));
                Check(Runs(host,0).Count>0,"Tarjeta sin cinta");
                PaintRuns(g,Runs(host,0),0);
                canvas.Save(Path.Combine(Path.GetDirectoryName(args[0]),"texto-"+width+".png"),ImageFormat.Png);
            }
            string[] titles=new string[12],artists=new string[12];Image[] covers=new Image[12];
            for(int i=0;i<12;i++){titles[i]="Canción "+i+" con un título muy largo, versión extendida";artists[i]="Artista de nombre largo";}
            object[] values={width,240,"Tema","Artista","",titles,artists,covers,12,Color.CornflowerBlue,true,2,true,0};
            using((IDisposable)Call(host,"CapturaTexto",1))
            using(Bitmap panel=(Bitmap)Static(form,"RenderIdlePanelLayout",values)){
                Check(Runs(host,1).Count>=12,"No captura los 12 titulos en cola");
                using(Graphics g=Graphics.FromImage(panel))PaintRuns(g,Runs(host,1),5000);
                panel.Save(Path.Combine(Path.GetDirectoryName(args[0]),"cola-"+width+".png"),ImageFormat.Png);
            }
        }
        Type songList=form.GetNestedType("SongListView",F);
        using(Control c=(Control)Activator.CreateInstance(songList,true))
        using(Font font=new Font("Tahoma",18,FontStyle.Bold)){
            c.Size=new Size(500,260);c.Font=font;c.BackColor=Color.FromArgb(9,13,24);
            Set(c,"RowH",52);Set(c,"Sel",0);Set(c,"Accent",Color.CornflowerBlue);
            Set(c,"GetCount",new Func<int>(delegate{return 5;}));
            Set(c,"GetText",new Func<int,string>(delegate(int i){return "Canción "+i+" con nombre muy largo que antes quedaba truncado";}));
            Set(c,"GetRight",new Func<int,string>(delegate(int i){return "4:36";}));
            using((IDisposable)Call(host,"CapturaTexto",2))
            using(Bitmap b=(Bitmap)Call(c,"BakeGpuListLayer",false))Check(Runs(host,2).Count==5,"La pagina no captura todas sus filas");
            using((IDisposable)Call(host,"CapturaTexto",3))
            using(Bitmap b=(Bitmap)Call(c,"BakeGpuListLayer",true))Check(Runs(host,3).Count==1,"Fila elegida sin cinta");
        }
        Set(host,"textosDeslizantes",false);
        using((IDisposable)Call(host,"CapturaTexto",0)){}
        Check(Runs(host,0).Count==0,"Interruptor no libera la cinta");
        Call(host,"SueltaCintasTexto");Check(Get(host,"cintasTexto")==null,"No libera grupos");
        Console.WriteLine("OK "+checks+" comprobaciones; composicion GDI+ y cache, no FPS ni sesion musical.");
        return 0;
    }
    static void PaintRuns(Graphics g,IList runs,double time){
        foreach(object run in runs){
            RectangleF r=(RectangleF)Get(run,"Rect");Bitmap[] tiles=(Bitmap[])Get(run,"Tiles");
            float offset=(float)Static(gpu,"CorreTitulo",time,Math.Max(0,(int)Get(run,"Width")-r.Width));
            System.Drawing.Drawing2D.GraphicsState state=g.Save();g.SetClip(r);
            for(int i=0;i<tiles.Length;i++)g.DrawImageUnscaled(tiles[i],(int)(r.X+i*1024-offset),(int)r.Y);
            g.Restore(state);
        }
    }
}
