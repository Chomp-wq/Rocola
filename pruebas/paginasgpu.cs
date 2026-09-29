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
    static bool sinHal;   // sin tarjeta de verdad (este servidor, Mono): la parte HAL se salta y se dice
    static void Set(object o, string n, object v) { o.GetType().GetField(n,F).SetValue(o,v); }
    static object Get(object o, string n) { return o.GetType().GetField(n,F).GetValue(o); }
    static object Call(object o, string n, params object[] a) { return o.GetType().GetMethod(n,F).Invoke(o,a); }
    static object Prop(object o, string n) { return o.GetType().GetProperty(n,F).GetValue(o,null); }
    [STAThread] static int Main(string[] args)
    {
        Assembly a = Assembly.LoadFrom(args[0]); form=a.GetType("Rocola.Form1"); list=form.GetNestedType("SongListView",F); gpu=a.GetType("Rocola.GpuPanel");
        string output=Path.GetDirectoryName(Path.GetFullPath(args[0]));
        foreach(int width in new int[]{640,1200,2300})
        foreach(bool vertical in new bool[]{false,true})
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
            // Tantas piezas como tenga el programa (NumPiezas), no un numero escrito a mano: eran 4 y el buscador con portadas
            // anadio la quinta; con 4 fijas, SueltaPiezas se salia del arreglo y la bateria entera caia antes de mirar nada.
            int piezas=(int)form.GetField("NumPiezas",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public).GetValue(null);
            Set(f,"uiPieza",new Bitmap[piezas]); Set(f,"uiPiezaRect",new Rectangle[piezas]); Set(f,"uiPiezaSig",new long[piezas]); Set(f,"uiPiezaVer",new int[piezas]);
            IList songs=(IList)Get(f,"songItems"); for(int i=0;i<80;i++)songs.Add("tema");
            try
            {
                for(int i=0;i<4;i++)Call(f,"EnsureSongPiece",i);
                Bitmap cachedPage=((Bitmap[])Get(f,"uiPieza"))[1];
                int pageVersion=((int[])Get(f,"uiPiezaVer"))[1], rowVersion=((int[])Get(f,"uiPiezaVer"))[2];
                Set(c,"Accent",Color.HotPink);
                Call(f,"EnsureSongPiece",1); Call(f,"EnsureSongPiece",2);
                Check(ReferenceEquals(cachedPage,((Bitmap[])Get(f,"uiPieza"))[1]) && pageVersion==((int[])Get(f,"uiPiezaVer"))[1],"El acento rehace la pagina sin seleccion");
                Check(rowVersion<((int[])Get(f,"uiPiezaVer"))[2],"El resaltado pierde los cambios de color");
                c.BackColor=Color.FromArgb(18,12,25); Call(f,"EnsureSongPiece",1);
                Check(pageVersion<((int[])Get(f,"uiPiezaVer"))[1],"La clave de transparencia pierde cambios de fondo");
                Call(f,"EnsureSongPiece",2);
                Call(c,"StartGpuSlide",1,vertical); Set(c,"PageStart",12);Set(c,"Sel",12);
                Check((float)Prop(c,"GpuSlideTravel") == (vertical ? 800 : width),"Distancia no corresponde al eje");
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
                Call(c,"StartGpuSlide",-1,vertical);Set(c,"PageStart",24);Set(c,"Sel",24);
                Check((float)Prop(c,"GpuSlideTravel")<0,"Sentido inverso perdido");
                Call(f,"EnsureSongPiece",1);Call(f,"EnsureSongPiece",2);
                using(Bitmap expected=(Bitmap)Call(c,"BakeGpuListLayer",true))
                    Check(expected.Size==((Bitmap[])Get(f,"uiPieza"))[2].Size,"Fila seleccionada mal dimensionada");
                Set(f,"colaVer",1);int old=((int[])Get(f,"uiPiezaVer"))[1];Call(f,"EnsureSongPiece",1);
                Check(((int[])Get(f,"uiPiezaVer"))[1]>old,"La cola no invalida la pagina");
                Set(f,"gpuEnabled",true);Set(f,"animNiv",2);
                Set(f,"songSel",11);Set(c,"PageStart",0);Set(c,"Sel",11);Call(f,"EnsureSongPiece",1);
                Call(f,"MoveSongInput",1,true);
                Check((int)Get(c,"PageStart")==12 && (bool)Get(c,"GpuSlideVertical"),"Bajar a otra pagina no anima vertical");
                Set(f,"songSel",79);Set(c,"PageStart",72);Set(c,"Sel",79);Call(f,"EnsureSongPiece",1);
                Call(f,"MoveSongInput",1,true);
                Check((int)Get(c,"PageStart")==0 && (float)Prop(c,"GpuSlideTravel")>0,"Ultima a primera pierde direccion abajo");
                Call(f,"EnsureSongPiece",1);Call(f,"MoveSongInput",-1,true);
                Check((int)Get(c,"PageStart")==72 && (float)Prop(c,"GpuSlideTravel")<0,"Primera a ultima pierde direccion arriba");
                Set(f,"songSel",0);Set(c,"PageStart",0);Set(c,"Sel",0);Call(f,"EnsureSongPiece",1);
                Call(f,"MoveSongInput",12,false);
                Check((int)Get(c,"PageStart")==12 && !(bool)Get(c,"GpuSlideVertical"),"Derecha no anima lateral");
                Check(Get(f,"songPagerFlash")!=null && (float)Call(Get(f,"songPagerFlash"),"Level",1)>0,"Derecha no enciende su flecha");
                Set(f,"songSel",0);Call(f,"MoveSongInput",-12,false);
                Check((int)Get(f,"songSel")==0,"Anterior cicla en el borde");
                Set(f,"animNiv",0);Call(f,"MoveSongInput",12,false);
                Check(!(bool)Prop(c,"Sliding"),"Animacion no respeta el interruptor existente");
                Check(!(bool)Get(f,"songMoveVertical") && (int)Get(f,"songMoveDirection")==0,"Entrada deja direccion residual");
            }
            finally {Call(f,"SueltaPiezas");}
        }
        MethodInfo clock=form.GetMethod("TextoFaltan",F);
        Check((string)clock.Invoke(null,new object[]{TimeSpan.FromSeconds(107),TimeSpan.FromSeconds(252),"x"})=="1:47 / 4:12","Reloj no muestra duracion total");
        Check((string)clock.Invoke(null,new object[]{TimeSpan.FromSeconds(260),TimeSpan.FromSeconds(252),"x"})=="4:12 / 4:12","Reloj sobrepasa duracion");
        Check((string)clock.Invoke(null,new object[]{TimeSpan.Zero,TimeSpan.Zero,"0:00 / 0:00"})=="0:00 / 0:00","Reloj inventa duracion");
        foreach(int height in new int[]{480,768,1440,2160})
        {
            float previous=height;
            for(int step=0;step<=120;step++)
            {
                float t=step/120f;
                float y=(float)form.GetMethod("DesplazamientoBuscador",F).Invoke(null,new object[]{t,(float)height,true});
                float closing=(float)form.GetMethod("DesplazamientoBuscador",F).Invoke(null,new object[]{1-t,(float)height,false});
                Check(y>=0 && y<=previous && Math.Abs(y-closing)<.001f,"Buscador salta o rebota");
                previous=y;
            }
            Check(previous==0,"Buscador no llega a su sitio");
        }
        Check(Codigo.Llama(form.GetMethod("BuildWinState",F),"DesplazamientoBuscador"),"Curva de entrada desconectada");
        // Prueba de dibujo HAL: la porcion trasladada queda recortada al interior de la lista.
        using(Control p=(Control)Activator.CreateInstance(gpu,true))
        using(Bitmap b=new Bitmap(320,160,PixelFormat.Format32bppPArgb))
        {
            p.Size=new Size(640,480); using(Graphics g=Graphics.FromImage(b))g.Clear(Color.White);
            float dx=80; bool vertical=false; int reads=0;
            Set(p,"GetGlassMode",new Func<bool>(delegate{return true;}));
            Set(p,"PiezasN",4);Set(p,"GetPieza",new Func<int,Bitmap>(delegate(int i){return b;}));
            Set(p,"GetPiezaVersion",new Func<int,int>(delegate(int i){return 1;}));
            Set(p,"GetPiezaRect",new Func<int,Rectangle>(delegate(int i){return new Rectangle(40,40,320,160);}));
            Set(p,"GetSongSlide",new Func<float>(delegate{reads++;return dx;}));Set(p,"GetSongTravel",new Func<float>(delegate{return vertical ? 160f : 320f;}));
            Set(p,"GetSongVertical",new Func<bool>(delegate{return vertical;}));
            if((bool)Call(p,"StartGpu"))try
            {
                Check((bool)Call(p,"DibujaUnCuadro"),"HAL no presenta");
                foreach(bool axis in new bool[]{false,true})
                foreach(float move in new float[]{-140,-80,0,80,140})
                {
                    dx=move; vertical=axis;
                    reads=0;
                    Check((bool)Call(p,"DibujaUnCuadro"),"HAL no presenta desplazamiento");
                    Check(reads==1,"Las capas no comparten el mismo instante");
                    foreach(object v in (Array)Get(p,"piezaQuad"))
                    {
                        float x=(float)Get(v,"X"),u=(float)Get(v,"U");
                        Check(x>=39.5f && x<=360f,"Dibuja fuera de lista");Check(u>=0 && u<=1,"UV fuera de textura");
                        float y=(float)Get(v,"Y"),vcoord=(float)Get(v,"V");
                        Check(y>=39.5f && y<=200f,"Dibuja fuera de lista vertical");Check(vcoord>=0 && vcoord<=1,"V fuera de textura");
                    }
                }
            }finally{Call(p,"StopGpu");}
            else { sinHal = true; Console.WriteLine("  SE SALTA el dibujo real con Direct3D (HAL): aqui no hay tarjeta. En Windows con tarjeta se hace."); }
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
            using(Control host=(Control)Activator.CreateInstance(form.GetNestedType("BgPanel",F),true))
            {
                host.Bounds=new Rectangle(20,30,1000,600);Set(f,"panelSongs",host);
                object flash=Activator.CreateInstance(form.GetNestedType("InstrFlash",F),true);
                Set(f,"songPagerFlash",flash);
                long signature=(long)Call(f,"UiSig");
                Call(flash,"Fire",0,Color.Gold);Call(flash,"Fire",1,Color.Gold);
                Array glows=Array.CreateInstance(gpu.GetNestedType("UiGlow",F),10);
                Check((int)Call(f,"FillUiGlows",(object)glows)==2,"Faltan halos de las flechas");
                Check((long)Call(f,"UiSig")==signature,"El brillo obliga a subir la interfaz");
                Check((float)Get(glows.GetValue(0),"Cx")<(float)Get(glows.GetValue(1),"Cx"),"Halos invertidos");
                Check((uint)Get(glows.GetValue(0),"Argb")>>24>0,"Halo invisible");
            }
        }
        Console.WriteLine("OK paginas GPU: "+checks+" comprobaciones; vinilos desconectados y siguiente dibujado"+(sinHal?" (SIN el dibujo real con Direct3D: no hay tarjeta aqui)":"")+".");return 0;
    }
}
