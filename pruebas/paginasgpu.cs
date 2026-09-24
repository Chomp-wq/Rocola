using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
class PaginasGpu
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks;
    static Type form, list, gpu;
    static void Check(bool b, string s) { if (!b) throw new Exception(s); checks++; }
    static void Set(object o, string n, object v) { o.GetType().GetField(n,F).SetValue(o,v); }
    static object Get(object o, string n) { return o.GetType().GetField(n,F).GetValue(o); }
    static object Call(object o, string n, params object[] a) { return o.GetType().GetMethod(n,F).Invoke(o,a); }
    static object Prop(object o, string n) { return o.GetType().GetProperty(n,F).GetValue(o,null); }
    [STAThread] static int Main(string[] args)
    {
        Assembly a = Assembly.LoadFrom(args[0]); form=a.GetType("Rocola.Form1"); list=form.GetNestedType("SongListView",F); gpu=a.GetType("Rocola.GpuPanel");
        string output=Path.GetDirectoryName(Path.GetFullPath(args[0]));
        foreach(int width in new int[]{640,1200,2300})
        using(Control c=(Control)Activator.CreateInstance(list,true))
        using(Control panel=(Control)Activator.CreateInstance(gpu,true))
        using(Control host=(Control)Activator.CreateInstance(form.GetNestedType("BgPanel",F),true))
        using(Font font=new Font("Tahoma",22,FontStyle.Bold))
        {
            c.Size=new Size(width,800); c.Font=font; c.BackColor=Color.FromArgb(9,13,24);
            Set(c,"RowH",64); Set(c,"Accent",Color.CornflowerBlue);
            Set(c,"GetCount",new Func<int>(delegate{return 80;}));
            Set(c,"GetText",new Func<int,string>(delegate(int i){return (i+1)+" - Enjoy the Silence / Canción de prueba";}));
            Set(c,"GetRight",new Func<int,string>(delegate(int i){return "4:36";}));
            Set(c,"Sel",0);
            object f=Codigo.FormSinEncender(form);
            Set(f,"gpuPanel",panel); Set(f,"panelSongs",host); Set(f,"songList",c);
            Set(f,"glassCapas",true); Set(f,"cancionesCristal",true); Set(f,"cancionesTransparentes",true);
            Set(panel,"GlassPieces",true);
            Set(f,"uiPieza",new Bitmap[4]); Set(f,"uiPiezaRect",new Rectangle[4]); Set(f,"uiPiezaSig",new long[4]); Set(f,"uiPiezaVer",new int[4]);
            IList songs=(IList)Get(f,"songItems"); for(int i=0;i<80;i++)songs.Add("tema");
            try
            {
                for(int i=0;i<4;i++)Call(f,"EnsureSongPiece",i);
                Call(c,"StartSlide",1); Set(c,"PageStart",12);Set(c,"Sel",12);
                Check((bool)Prop(f,"CanUseSongGlass"),"El hojeo desactiva las capas GPU");
                Stopwatch sw=Stopwatch.StartNew();
                for(int i=0;i<4;i++)Call(f,"EnsureSongPiece",i);
                double bake=sw.Elapsed.TotalMilliseconds;
                // El reloj es el mismo durante los 16 cuadros: no se falsifica el token.
                Bitmap[] pictures=(Bitmap[])((Bitmap[])Get(f,"uiPieza")).Clone();
                int[] versions=(int[])((int[])Get(f,"uiPiezaVer")).Clone();
                long sig=(long)Call(f,"UiSig");
                sw.Reset();sw.Start();
                for(int frame=0;frame<16;frame++)
                {
                    for(int i=0;i<3;i++)
                    {
                        Call(f,"EnsureSongPiece",i);
                        Check(ReferenceEquals(pictures[i],((Bitmap[])Get(f,"uiPieza"))[i]),"Rehornea durante desplazamiento");
                        Check(versions[i]==((int[])Get(f,"uiPiezaVer"))[i],"Reenvia pixeles durante desplazamiento");
                    }
                    Check(sig==(long)Call(f,"UiSig"),"El tiempo de la animacion invalida la UI");
                }
                Console.WriteLine(width+" px: horneado al cambiar "+bake.ToString("F2")+" ms; cache por cuadro "+(sw.Elapsed.TotalMilliseconds/16).ToString("F3")+" ms (no FPS)");
                for(int frame=0;frame<4;frame++)
                {
                    Thread.Sleep(1); // fuera de la medicion: el reloj avanza sin cambiar el token
                    Check(sig==(long)Call(f,"UiSig"),"Animacion real invalida la UI");
                    if((bool)Prop(c,"GpuPreviousReady"))
                    {
                        Call(f,"EnsureSongPiece",3);
                        Check(ReferenceEquals(pictures[3],((Bitmap[])Get(f,"uiPieza"))[3]),"Recopia pagina saliente animada");
                    }
                }
                // Pagina previa convertida una vez; no hay captura repetida del vidrio.
                if((bool)Prop(c,"GpuPreviousReady"))
                {
                    Call(f,"EnsureSongPiece",3);
                    Check(ReferenceEquals(pictures[3],((Bitmap[])Get(f,"uiPieza"))[3]),"Recopia pagina previa");
                }
                Call(c,"OlvidaPagina"); Call(f,"EnsureSongPiece",3);
                Check(((Bitmap[])Get(f,"uiPieza"))[3]==null,"No libera la pagina saliente");
                Set(c,"PageStart",0);Set(c,"Sel",0);Call(f,"EnsureSongPiece",1);Call(f,"EnsureSongPiece",2);
                Call(c,"StartSlide",-1);Set(c,"PageStart",24);Set(c,"Sel",24);
                Check((float)Prop(c,"GpuSlideTravel")<0,"Sentido inverso perdido");
                Call(f,"EnsureSongPiece",1);Call(f,"EnsureSongPiece",2);
                using(Bitmap expected=(Bitmap)Call(c,"BakeGpuListLayer",true))
                    Check(expected.Size==((Bitmap[])Get(f,"uiPieza"))[2].Size,"Fila seleccionada mal dimensionada");
                Set(f,"colaVer",1);int old=((int[])Get(f,"uiPiezaVer"))[1];Call(f,"EnsureSongPiece",1);
                Check(((int[])Get(f,"uiPiezaVer"))[1]>old,"La cola no invalida la pagina");
            }
            finally {Call(f,"SueltaPiezas");}
        }
        // Prueba de dibujo HAL: la porcion trasladada queda recortada al interior de la lista.
        using(Control p=(Control)Activator.CreateInstance(gpu,true))
        using(Bitmap b=new Bitmap(320,160,PixelFormat.Format32bppPArgb))
        {
            p.Size=new Size(640,480); using(Graphics g=Graphics.FromImage(b))g.Clear(Color.White);
            float dx=80;
            Set(p,"GetGlassMode",new Func<bool>(delegate{return true;}));
            Set(p,"PiezasN",4);Set(p,"GetPieza",new Func<int,Bitmap>(delegate(int i){return b;}));
            Set(p,"GetPiezaVersion",new Func<int,int>(delegate(int i){return 1;}));
            Set(p,"GetPiezaRect",new Func<int,Rectangle>(delegate(int i){return new Rectangle(40,40,320,160);}));
            Set(p,"GetSongSlide",new Func<float>(delegate{return dx;}));Set(p,"GetSongTravel",new Func<float>(delegate{return 320f;}));
            if((bool)Call(p,"StartGpu"))try
            {
                Check((bool)Call(p,"DibujaUnCuadro"),"HAL no presenta");
                foreach(float move in new float[]{-300,-80,0,80,300})
                {
                    dx=move;
                    Check((bool)Call(p,"DibujaUnCuadro"),"HAL no presenta desplazamiento");
                    foreach(object v in (Array)Get(p,"piezaQuad"))
                    {
                        float x=(float)Get(v,"X"),u=(float)Get(v,"U");
                        Check(x>=39.5f && x<=360f,"Dibuja fuera de lista");Check(u>=0 && u<=1,"UV fuera de textura");
                    }
                }
            }finally{Call(p,"StopGpu");}
            else throw new Exception("HAL no disponible: falta verificar GPU");
        }
        Check(!Codigo.Llama(gpu.GetMethod("DibujaUnCuadro",F),"DibujaVinilo") && !Codigo.Llama(gpu.GetMethod("DibujaUnCuadro",F),"DibujaViniloReposo"),"Sigue dibujando vinilos");
        Check(!Codigo.Llama(form.GetMethod("DiscoVivoTic",F),"PreparaVinilo") && !Codigo.Llama(form.GetMethod("SuenaVivoTic",F),"PreparaViniloReposo"),"Sigue horneando vinilos");
        using(Bitmap b=new Bitmap(1000,150,PixelFormat.Format32bppPArgb))
        using(Graphics g=Graphics.FromImage(b))
        {
            g.Clear(Color.FromArgb(12,18,34));g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            form.GetMethod("PintaTarjetaSiguiente",F).Invoke(null,new object[]{g,"Depeche Mode · Enjoy the Silence",new Rectangle(15,15,450,70)});
            form.GetMethod("PintaTarjetaSiguiente",F).Invoke(null,new object[]{g,"Título muy largo con caracteres: Б 世界 & canción",new Rectangle(490,15,240,55)});
            b.Save(Path.Combine(output,"siguiente-nuevo.png"));
        }
        using(Control pager=(Control)Activator.CreateInstance(form.GetNestedType("SongPagerBar",F),true))
        using(Bitmap b=new Bitmap(1000,150,PixelFormat.Format32bppPArgb))
        using(Graphics g=Graphics.FromImage(b))
        {
            object f=Codigo.FormSinEncender(form); Set(f,"songPager",pager);Set(f,"bgAccentTarget",Color.CornflowerBlue);
            g.Clear(Color.FromArgb(12,18,34));g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Set(pager,"Pages",12);
            for(int i=0;i<3;i++)
            {
                pager.Bounds=new Rectangle(0,i*50,1000,44);Call(pager,"PonSinAnimar",i==2?11:i);
                Call(f,"DrawSongPagerGpu",g);
            }
            b.Save(Path.Combine(output,"paginas-nuevo.png"));
        }
        Console.WriteLine("OK paginas GPU: "+checks+" comprobaciones; vinilos desconectados y siguiente dibujado.");return 0;
    }
}
