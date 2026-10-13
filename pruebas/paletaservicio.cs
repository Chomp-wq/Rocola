using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

class PaletaServicio
{
    const BindingFlags F = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static int cases;
    static object aurora;
    static T Aurora<T>() { return (T)aurora; }
    static object Get(Type t, object o, string name) { return t.GetField(name,F).GetValue(o); }
    static void Set(Type t, object o, string name, object value) { t.GetField(name,F).SetValue(o,value); }
    static object Call(Type t, object o, string name, params object[] p) { return t.GetMethod(name,F).Invoke(o,p); }
    static void Check(bool ok, string message) { cases++; if (!ok) throw new Exception(message); }
    static Control Find(Control p,string text) { foreach(Control c in p.Controls) { if(c.Text==text)return c; Control n=Find(c,text);if(n!=null)return n; } return null; }
    [STAThread] static int Main(string[] args)
    {
        try { Run(Assembly.LoadFrom(args[0])); Console.WriteLine("  OK    "+cases+" casos: paleta, pesos, cache HAL, luna elevada y creditos de servicio. No prueba XP ni latencia de audio."); return 0; }
        catch(Exception e) { Console.WriteLine(e); return 1; }
    }
    static void Run(Assembly asm)
    {
        Type form=asm.GetType("Rocola.Form1"),gpu=asm.GetType("Rocola.GpuPanel"),gate=asm.GetType("Rocola.SecretCreditGate");
        object key=Activator.CreateInstance(gate,true);
        foreach(uint start in new uint[]{0,1000,0xFFFFFF00u})
        {
            Call(gate,key,"Update",false,"",start);
            Call(gate,key,"Update",true,"CHOMPS21",start);
            foreach(uint delta in new uint[]{0,4999,5000,15000})
            {
                Call(gate,key,"Update",true,"CHOMPS21",unchecked(start+delta));
                Check((bool)gate.GetProperty("Ready",F).GetValue(key,null)==(delta>=5000),"Tiempo de acceso incorrecto");
            }
            Call(gate,key,"Update",true,"CHOMPS21x",unchecked(start+16000));
            Check(!(bool)gate.GetProperty("Ready",F).GetValue(key,null),"No revoca al editar");
        }
        foreach(string text in new string[]{null,"","CHOMPS2","CHOMPS21 "," CHOMPS21","CHOMPS210"})
            Check(!(bool)Call(gate,null,"Matches",text),"Codigo admite texto extra");
        Check((bool)Call(gate,null,"Matches","chomps21"),"Codigo no acepta minusculas");

        string dir=Path.Combine(Path.GetTempPath(),"rocola_paleta_servicio_"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir); Set(form,null,"carpetaDatos",dir);
        Type search=form.GetNestedType("SearchOverlay",F);
        using(Control overlay=(Control)Activator.CreateInstance(search,true))
        using(Timer timer=new Timer())
        using(Bitmap canvas=new Bitmap(900,500))
        using(Graphics graphics=Graphics.FromImage(canvas))
        {
            object f=Codigo.FormSinEncender(form),clock=Activator.CreateInstance(gate,true);
            Set(form,f,"secretCreditGate",clock);Set(form,f,"searchOverlay",overlay);
            Set(form,f,"toastTimer",timer);Set(form,f,"searchOpen",true);Set(form,f,"stateRestored",true);
            Set(search,overlay,"Escrito","CHOMPS21");Set(search,overlay,"Query","CHOMPS21");Set(search,overlay,"SeparateGpu",true);
            Call(form,f,"UpdateSecretCredits");
            Check(((IList)Get(search,overlay,"Results")).Count==0,"Resultados antes de cinco segundos");
            Call(search,overlay,"DrawResults",graphics,new Rectangle(0,0,700,450)); // cache vacia
            int version=(int)Get(search,overlay,"DatosVer");
            Set(gate,clock,"since",unchecked((uint)Environment.TickCount-5001u));
            Call(form,f,"UpdateSecretCredits");
            object item=Get(form,f,"secretCreditItem");
            Check(item!=null&&((IList)Get(search,overlay,"Results")).Count==1,"No aparece tarjeta");
            Check((int)Get(search,overlay,"DatosVer")!=version,"Tarjeta no invalida cache");
            Call(search,overlay,"DrawResults",graphics,new Rectangle(0,0,700,450));
            Check((int)Get(search,overlay,"resCacheDatos")==(int)Get(search,overlay,"DatosVer"),"Cache no redibuja acceso");
            canvas.Save(Path.Combine(dir,"tarjeta-servicio.png"));
            for(int i=1;i<=3;i++)
            {
                Call(form,f,"ChooseSearchResult",item);
                Check((int)Get(form,f,"credits")==4*i,"No entrega cuatro por pulsacion");
                Codigo.EsperaGuardado(form,f);   // el estado se escribe en un hilo de fondo (estadodetras.cs)
                Check(File.ReadAllText(Path.Combine(dir,"rocola_estado.txt")).Contains("creditos="+4*i+"\r\n"),"No persiste creditos");
                Check((long)Get(form,f,"statsCoins")==0&&(long)Get(form,f,"statsCreditsAdded")==0,"Cuenta servicio como monedas");
                Check((bool)Get(form,f,"searchOpen"),"Cierra acceso tras usar");
            }
            foreach(string block in new string[]{"startupReadPending","startupClosing","libBuilding"})
            {
                Set(form,f,block,true);Call(form,f,"ChooseSearchResult",item);Set(form,f,block,false);
                Check((int)Get(form,f,"credits")==12,"Escribe durante carga/cierre");
            }
            Set(form,f,"stateRestored",false);Call(form,f,"ChooseSearchResult",item);
            Check((int)Get(form,f,"credits")==12,"Sobrescribe estado sin restaurar");Set(form,f,"stateRestored",true);
            Set(form,f,"credits",int.MaxValue-2);Call(form,f,"ChooseSearchResult",item);
            Check((int)Get(form,f,"credits")==int.MaxValue-2,"Desbordamiento");
            Set(form,f,"credits",12);Set(search,overlay,"Escrito","CHOMPS2");Call(form,f,"ChooseSearchResult",item);
            Check((int)Get(form,f,"credits")==12&&Get(form,f,"secretCreditItem")==null,"Tarjeta vieja concede creditos");
            Set(search,overlay,"Escrito","CHOMPS21");Call(form,f,"UpdateSecretCredits");
            Check(Get(form,f,"secretCreditItem")==null,"No reinicia cinco segundos");
            Set(form,f,"searchOpen",false);Call(form,f,"UpdateSecretCredits");
            Check(!(bool)gate.GetProperty("Ready",F).GetValue(clock,null),"Acceso queda abierto");
        }

        Color[] input={Color.Red,Color.Lime,Color.Blue,Color.Cyan,Color.Magenta,Color.Yellow,Color.White,Color.Black};
        Color[] palette;
        using(Bitmap b=new Bitmap(256,128))
        {
            using(Graphics g=Graphics.FromImage(b))for(int i=0;i<8;i++)using(Brush brush=new SolidBrush(input[i]))g.FillRectangle(brush,i*32,0,32,128);
            palette=(Color[])Call(form,null,"ExtractCoverPalette",b);
            Check(palette.Length==8,"Pierde colores representativos");
            foreach(Color c in input){ bool found=false; foreach(Color p in palette) if(c.ToArgb()==p.ToArgb())found=true;Check(found,"Falta color "+c); }
        }
        using(Bitmap b=new Bitmap(1,1))
        {
            b.SetPixel(0,0,Color.Transparent);Check(((Color[])Call(form,null,"ExtractCoverPalette",b)).Length==0,"Usa RGB transparente");
            b.SetPixel(0,0,Color.Gray);Color[] p=(Color[])Call(form,null,"ExtractCoverPalette",b);
            Check(p.Length==1&&p[0].R==p[0].G&&p[0].G==p[0].B,"Inventa color gris");
        }
        using(Control panel=(Control)Activator.CreateInstance(gpu,true))
        {
            double now=0;
            foreach(float weight in new float[]{0,.2f,.5f,1})
            {
                Call(gpu,panel,"SetCoverPalette",palette,weight);
                for(int i=0;i<300;i++)Call(gpu,panel,"AdvanceCoverPalette",now+=16);
                Check(Math.Abs((float)Get(gpu,panel,"coverWeight")-weight)<.0001,"Peso no converge");
                for(int i=0;i<8;i++)
                {
                    uint color=(uint)Call(gpu,panel,"CoverColor",i/7f);
                    Check((color&0xFFFFFFu)==((uint)palette[i].ToArgb()&0xFFFFFFu),"Banda pierde tono de portada");
                    uint mixed=(uint)Call(gpu,panel,"MixCoverColor",0xA0204060u,color);
                    Check(mixed>>24==0xA0,"Cambia transparencia");
                    if(weight==0)Check(mixed==0xA0204060u,"Disco 0 altera fondo");
                    if(weight==1)Check((mixed&0xFFFFFFu)==color,"Disco 100 conserva tinte artificial");
                }
            }
            object floats=Get(gpu,panel,"coverCurrent"),bands=Get(gpu,panel,"coverBands");
            Call(gpu,panel,"SetCoverPalette",new Color[]{Color.Red},1f);
            uint before=(uint)Call(gpu,panel,"CoverColor",0f);
            Call(gpu,panel,"AdvanceCoverPalette",now+=1);
            uint after=(uint)Call(gpu,panel,"CoverColor",0f);
            Check(before==after||after!=0xFF0000u,"Transicion instantanea");
            for(int i=0;i<300;i++)Call(gpu,panel,"AdvanceCoverPalette",now+=16);
            Check(object.ReferenceEquals(floats,Get(gpu,panel,"coverCurrent"))&&object.ReferenceEquals(bands,Get(gpu,panel,"coverBands")),"Reasigna por cuadro");
            Call(gpu,panel,"SetCoverPalette",null,1f);
            for(int i=0;i<300;i++)Call(gpu,panel,"AdvanceCoverPalette",now+=16);
            Check((float)Get(gpu,panel,"coverWeight")==0,"Portada ausente deja colores anteriores");

            object f=Codigo.FormSinEncender(form);Set(form,f,"gpuPanel",panel);
            Type albumType=form.GetNestedType("Album",F),playType=form.GetNestedType("PlayItem",F),cacheType=form.GetNestedType("ColoresDisco",F);
            object album=Activator.CreateInstance(albumType,true),play=Activator.CreateInstance(playType,true),cached=Activator.CreateInstance(cacheType);
            Set(albumType,album,"CoverPath","paleta-sintetica");Set(playType,play,"Disc",album);Set(form,f,"nowPlaying",play);
            Set(cacheType,cached,"Paleta",palette);
            ((IDictionary)Get(form,f,"colorPortada"))["paleta-sintetica"]=cached;
            Set(form,f,"paletaCompleta",true);Set(form,null,"fondoPortadaOn",true);
            foreach(double weight in new double[]{0,.2,.5,1})
            foreach(bool noGenre in new bool[]{false,true})
            {
                FieldInfo field=form.GetField("portadaManda",F);field.SetValue(null,Convert.ChangeType(weight,field.FieldType));
                Set(form,f,"bgSinGenero",noGenre);Call(form,f,"SyncGpuCoverPalette");
                float expected=noGenre&&weight>0?1:(float)weight;
                Check(Math.Abs((float)Get(gpu,panel,"coverTargetWeight")-expected)<.00001,"Preset o sin genero no llega al render");
            }
            foreach(string block in new string[]{"attractOn","bgArtistaFijo"})
            {
                Set(form,f,block,true);Call(form,f,"SyncGpuCoverPalette");Set(form,f,block,false);
                Check((float)Get(gpu,panel,"coverTargetWeight")==0,"No respeta atraccion/artista fijo");
            }
            Set(form,f,"paletaCompleta",false);Call(form,f,"SyncGpuCoverPalette");
            Check((float)Get(gpu,panel,"coverTargetWeight")==0,"No desactiva paleta");
            Set(form,f,"paletaCompleta",true);Set(playType,play,"IsVideo",true);Call(form,f,"SyncGpuCoverPalette");
            Check((float)Get(gpu,panel,"coverTargetWeight")==0,"Colorea video con disco anterior");
            Set(playType,play,"IsVideo",false);((IDictionary)Get(form,f,"colorPortada")).Clear();
            Call(form,f,"SyncGpuCoverPalette");Check((float)Get(gpu,panel,"coverTargetWeight")==0,"Cache invalidada retiene portada");
            using(Font font=new Font("Segoe UI",9,FontStyle.Bold))
            using(Panel page=(Panel)Call(form,f,"PaginaFondo",font,font,font,font,Color.White,Color.Gray,Color.Gray))
            {
                Control toggle=Find(page,"Paleta completa de caratula");
                Check(toggle!=null&&toggle.Bottom<=toggle.Parent.Height,"Toggle no cabe");
                foreach(bool enabled in new bool[]{true,false,true})
                {
                    toggle.GetType().GetProperty("Checked",F).SetValue(toggle,enabled,null);
                    Call(form,f,"SaveConfigPrefs");object copy=Codigo.FormSinEncender(form);Call(form,copy,"LoadConfigPrefs");
                    Check((bool)Get(form,copy,"paletaCompleta")==enabled,"Toggle no persiste");
                }
            }

            panel.Size=new Size(640,480);
            Type au=asm.GetType("Rocola.GpuAurora");
            aurora=Activator.CreateInstance(au);Set(au,aurora,"Has",true);Set(au,aurora,"Alto",150f);
            Set(au,aurora,"BaseY",400f);Set(au,aurora,"Dim",1f);Set(au,aurora,"DimFilo",1f);
            float[] spectrum=new float[32];for(int i=0;i<spectrum.Length;i++)spectrum[i]=.6f;
            Set(au,aurora,"B",spectrum);
            MethodInfo provider=typeof(PaletaServicio).GetMethod("Aurora",F).MakeGenericMethod(au);
            Set(gpu,panel,"GetAurora",Delegate.CreateDelegate(gpu.GetField("GetAurora",F).FieldType,provider));
            if((bool)Call(gpu,panel,"StartGpu"))
            {
                try
                {
                    Call(gpu,panel,"SetCoverPalette",palette,1f);
                    for(int i=0;i<300;i++)Call(gpu,panel,"AdvanceCoverPalette",now+=16);
                    Check((bool)Call(gpu,panel,"DibujaUnCuadro"),"HAL no presenta");
                    IntPtr texture=(IntPtr)Get(gpu,panel,"nebTex");object geometry=Get(gpu,panel,"aurV");
                    Check(texture!=IntPtr.Zero&&geometry!=null,"No dibuja nebulosa/aurora");
                    uint[] colors=(uint[])Get(gpu,panel,"aurLc");Check(colors[0]!=colors[colors.Length/2],"Aurora sigue monocroma");
                    for(int j=0;j<10;j++)
                    {
                        Call(gpu,panel,"SetCoverPalette",j%2==0?new Color[]{Color.Red}:palette,.5f);
                        Check((bool)Call(gpu,panel,"DibujaUnCuadro"),"HAL falla al cambiar portada");
                        Check((IntPtr)Get(gpu,panel,"nebTex")==texture,"Cambio de color recrea textura");
                        Check(object.ReferenceEquals(geometry,Get(gpu,panel,"aurV")),"Recrea geometria");
                    }
                }finally{Call(gpu,panel,"StopGpu");}
            } else Console.WriteLine("  OMITIDA comprobacion HAL: dispositivo no disponible.");
        }
        foreach(int height in new int[]{480,600,720,1080,1440,2160})
        for(int p=0;p<100;p+=5)
        {
            PointF normal=(PointF)Call(gpu,null,"DetailedMoonCenter",1920,height,(double)p,0f,0f,true);
            PointF idle=(PointF)Call(gpu,null,"DetailedMoonCenter",1920,height,(double)p,1f,0f,true);
            Check(idle.Y<normal.Y-height*.18f,"Orbita de reposo no sube");
            PointF stationary=(PointF)Call(gpu,null,"DetailedMoonCenter",1920,height,(double)p,1f,0f,false);
            Check(Math.Abs(stationary.Y-height*.47f)<.01,"Toggle lunar modifica posicion fija");
        }
        Console.WriteLine("  Artefactos aislados: "+dir);
    }
}
