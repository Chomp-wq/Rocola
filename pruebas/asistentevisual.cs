using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Ayuda aislada: no arranca Form1 ni abre musica, creditos o corpus del usuario.
// Las capturas opcionales vienen del backbuffer HAL real, no de un mockup.
class AsistenteVisual
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type form, gpu, state, vtx;
    static int checks;
    static object Get(object o, string n) { return o.GetType().GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, B).SetValue(o, v); }
    static object Call(Type t, object o, string n, params object[] a)
    { try { return t.GetMethod(n, B).Invoke(o, a); } catch (TargetInvocationException e) { throw new Exception(n, e.InnerException); } }
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
    static float F(object o, string n) { return (float)Get(o, n); }
    static Rectangle Card(Size s) { return (Rectangle)Call(gpu, null, "AsistentePanelRect", s.Width, s.Height); }
    static object State(bool visible, bool animated, Bitmap panel, int version)
    {
        object s = Activator.CreateInstance(state);
        Set(s, "Visible", visible); Set(s, "Animada", animated); Set(s, "Panel", panel); Set(s, "Version", version); return s;
    }
    static object Owner(Size size)
    {
        object owner = Codigo.FormSinEncender(form);
        DynamicMethod m = new DynamicMethod("BaseAisladaAsistente", typeof(void), new Type[] { typeof(object) }, typeof(AsistenteVisual), true);
        ILGenerator code = m.GetILGenerator(); code.Emit(OpCodes.Ldarg_0); code.Emit(OpCodes.Castclass, typeof(Form));
        code.Emit(OpCodes.Call, typeof(Form).GetConstructor(Type.EmptyTypes)); code.Emit(OpCodes.Ret);
        m.Invoke(null, new object[] { owner }); ((Form)owner).ClientSize = size; return owner;
    }
    static void Layout(string output)
    {
        foreach (Size size in new Size[] { new Size(640,480), new Size(800,600), new Size(1024,768), new Size(1280,720),
            new Size(1920,1080), new Size(3440,1440), new Size(3840,2160) })
        {
            Rectangle r = Card(size);
            Check(r.Left > 0 && r.Right <= size.Width && r.Top > 0 && r.Bottom <= size.Height - 48 && r.Height <= 800 && r.Width<=1120, "Globo dentro de pantalla y acotado: " + size);
            PointF center = (PointF)Call(gpu, null, "AsistenteCentroFrente", size.Width, size.Height);
            float radius = (float)Call(gpu, null, "AsistenteRadioFrente", size.Width, size.Height);
            Check(center.X - radius * 1.35f > 0 && center.X + radius * 1.35f < r.Left,
                "Personaje no tapa opciones: " + size);
            Check(center.Y - radius > 0 && center.Y + radius * 1.55f < size.Height - 48, "Miembros visibles: " + size);
            Check(r.Left-(center.X+radius*1.35f)<48,"Estrella junto al globo incluso en 4K/ultrapanoramico");
            Rectangle a = (Rectangle)Call(form, null, "AsistenteOpcionRect", r.Width, r.Height, 0);
            Rectangle b = (Rectangle)Call(form, null, "AsistenteOpcionRect", r.Width, r.Height, 1);
            Rectangle c = (Rectangle)Call(form, null, "AsistenteOpcionRect", r.Width, r.Height, 2);
            Check(a.Left >= 0 && a.Right <= r.Width && a.Top >= 0 && c.Bottom < r.Height * .93f && !a.IntersectsWith(b) && !b.IntersectsWith(c), "Opciones separadas y alcanzables: " + size);
            foreach (int page in new int[] { 0, 1, 2 })
            for (int step = 0; step < (page == 1 ? 3 : 1); step++)
            foreach (int sel in new int[] { 0, 1 })
            using (Bitmap bitmap = (Bitmap)Call(form, null, "PintaPanelAsistente", r.Width, r.Height, page, step, sel))
            {
                Check(bitmap.Size == r.Size && bitmap.GetPixel(0,0).A == 0 && bitmap.GetPixel(r.Width / 2, r.Height / 2).A >= 240, "Vidrio legible sin fondo rectangular opaco");
                Check(bitmap.GetPixel(6,(int)(r.Height*.46f)).A>150 && bitmap.GetPixel(6,r.Height/5).A==0,
                    "Globo con cola hacia la estrella, no caja rectangular");
                if (size.Width == 640 || size.Width == 1024)
                    bitmap.Save(Path.Combine(output, "panel-" + size.Width + "-" + page + "-" + step + "-" + sel + ".png"), ImageFormat.Png);
            }
        }
        foreach (Size extreme in new Size[] { Size.Empty, new Size(-10,-10), new Size(1,1), new Size(16,16) })
        {
            Rectangle r = Card(extreme); Check(r.Width >= 1 && r.Height >= 1 && r.X >= 0 && r.Y >= 0, "Geometria minimizada segura");
            using(Bitmap tiny=(Bitmap)Call(form,null,"PintaPanelAsistente",r.Width,r.Height,0,0,0))
                Check(tiny.Width>=1&&tiny.Height>=1,"Globo minimizado sin error GDI");
        }
    }
    static void Mesh()
    {
        Array vertices = Array.CreateInstance(vtx, 2600);
        foreach (float radius in new float[] { 1, 65, 220 })
        foreach (float amount in new float[] { 0, .001f, .5f, 1, 2 })
        foreach (float gesture in new float[] { -1, 0, 1 })
        {
            int n = (int)Call(gpu, null, "AsistenteMallaMiembros", vertices, 500f, 300f, radius, amount, gesture, 1f);
            Check(n >= 0 && n <= vertices.Length && n % 3 == 0 && (amount == 0 ? n == 0 : n > 0), "Malla acotada y completa");
            for (int i = 0; i < n; i++)
            {
                object vertex = vertices.GetValue(i); float x = F(vertex, "X"), y = F(vertex, "Y"); uint color = (uint)Get(vertex, "Color");
                Check(Finite(x) && Finite(y) && Math.Abs(x-500) < radius * 1.6f + 5 && Math.Abs(y-300) < radius * 1.6f + 5, "Vertice finito dentro de miembros");
                Check((color & 0xFFFFFFu) == 0x06080Du && ((color >> 24) == 0 || (color >> 24) == 255), "Negro solido, solo el filo transparente");
            }
        }
        foreach (float bad in new float[] { 0, -1, float.NaN, float.PositiveInfinity })
            Check((int)Call(gpu, null, "AsistenteMallaMiembros", vertices, 0f, 0f, bad, 1f, 0f, 1f) == 0, "Radio invalido no dibuja");
        Check((int)Call(gpu, null, "AsistenteMallaMiembros", null, 0f, 0f, 10f, 1f, 0f, 1f) == 0, "Buffer nulo seguro");
        Check((int)Call(gpu, null, "AsistenteMallaMiembros", Array.CreateInstance(vtx,2599), 0f, 0f, 10f, 1f, 0f, 1f) == 0, "No desborda un buffer corto");
    }
    static object Pose(Control p, double milliseconds)
    { Set(p, "cuadroMs", milliseconds); Call(gpu, p, "AvanzaAsistente"); return Get(p, "asistentePose"); }
    static void Animation()
    {
        foreach (bool natural in new bool[] { false, true })
        foreach (bool threeD in new bool[] { false, true })
        foreach (int fps in new int[] { 24, 50, 60, 144, 240 })
        using (Control panel = (Control)Activator.CreateInstance(gpu, true))
        {
            panel.Size = new Size(1024,768); Set(panel, "FondoNatural", natural); Set(panel, "Estrella3D", threeD);
            Set(panel, "ConLuna", true); Set(panel, "LunaActiva", false); Set(panel, "uiBotH", 68);
            Set(panel, "asistenteEstado", State(true, true, null, 1));
            object origin = Call(gpu, panel, "AsistentePoseFondo"); object first = Pose(panel, 1000);
            PointF start = (PointF)Get(origin,"Centro"), firstPoint = (PointF)Get(first,"Centro");
            Check(Math.Abs(start.X - firstPoint.X) < .01 && Math.Abs(start.Y - firstPoint.Y) < .01 && Math.Abs(F(origin,"Radio")-F(first,"Radio")) < .01, "Salto parte de la estrella actual");
            double lastX = start.X;
            for (int i = 1; i <= Math.Ceiling(.72 * fps); i++)
            {
                double t = Math.Min(.72, i / (double)fps); object pose = Pose(panel, 1000 + t * 1000);
                PointF at = (PointF)Get(pose,"Centro");
                Check(Finite(at.X) && Finite(at.Y) && at.X <= lastX + .01 && at.Y >= F(pose,"Radio") + 4, "Trayecto continuo con reloj de cuadro"); lastX = at.X;
            }
            object final = Pose(panel, 1720); PointF expected = (PointF)Call(gpu, null,"AsistenteCentroFrente",1024,768);
            PointF actual = (PointF)Get(final,"Centro");
            Check(Math.Abs(actual.X-expected.X) < .01 && Math.Abs(actual.Y-expected.Y) < .01 && F(final,"Miembros") == 1, "Llega al mismo sitio independientemente de FPS/estilo");
            Set(panel, "asistenteEstado", State(false,true,null,1)); Pose(panel,2000);
            Check((bool)Get(panel,"asistenteDibujando"), "Sigue visible durante el regreso");
            Pose(panel,2720);
            Check(!(bool)Get(panel,"asistenteDibujando"), "Devuelve la estrella al fondo al terminar");
            Set(panel,"asistenteEstado",State(true,false,null,1)); Pose(panel,3000);
            Check(F(Get(panel,"asistentePose"),"Miembros")==1, "Animacion apagada conserva ayuda y personaje");
            Set(panel,"asistenteEstado",State(false,false,null,1)); Pose(panel,3001);
            Check(!(bool)Get(panel,"asistenteDibujando"), "Cerrar sin animacion es inmediato");
        }
        using (Control panel = (Control)Activator.CreateInstance(gpu,true))
        {
            panel.Size=new Size(1920,1080); Set(panel,"uiBotH",70); Set(panel,"asistenteEstado",State(true,true,null,1)); Pose(panel,1000);
            panel.Size=new Size(640,480); object pose=Pose(panel,1250); PointF p=(PointF)Get(pose,"Centro");
            Check(Finite(p.X) && Finite(p.Y) && p.X>0 && p.X<640 && p.Y>=F(pose,"Radio"), "Redimensionar durante el salto no saca al personaje");
        }
    }
    static void CacheAndMenu()
    {
        using (Form owner = (Form)Owner(new Size(640,480)))
        {
            Set(owner,"asistenteVisible",true); Call(form,owner,"BuildAsistenteState");
            Bitmap first=(Bitmap)Get(owner,"asistentePanel"); int version=(int)Get(owner,"asistentePanelVersion");
            for(int i=0;i<120;i++) Call(form,owner,"BuildAsistenteState");
            Check(ReferenceEquals(first,Get(owner,"asistentePanel")) && version==(int)Get(owner,"asistentePanelVersion"), "120 cuadros no crean bitmaps");
            Type gestures=form.Assembly.GetType("Rocola.AsistenteGestoGpu",true);
            int ev=(int)Get(owner,"asistenteEvento");
            for(int i=0;i<100;i++) { Call(form,owner,"AnimaAsistente",Enum.Parse(gestures,"Saludar")); Call(form,owner,"BuildAsistenteState"); }
            Check(ReferenceEquals(first,Get(owner,"asistentePanel")) && version==(int)Get(owner,"asistentePanelVersion")
                && (int)Get(owner,"asistenteEvento")==ev+100,"Los gestos no rehacen el globo");
            Call(form,owner,"SeleccionaAsistente",1); int selectedEvent=(int)Get(owner,"asistenteEvento");
            for(int i=0;i<100;i++) Call(form,owner,"SeleccionaAsistente",1);
            Check((int)Get(owner,"asistenteEvento")==selectedEvent,"Repeticion/hover inmovil no reinicia el gesto");
            Call(form,owner,"SeleccionaAsistente",0);
            Call(form,owner,"BuildAsistenteState"); first=(Bitmap)Get(owner,"asistentePanel"); version=(int)Get(owner,"asistentePanelVersion");
            // Desde el 11-10-2026 el menu tiene tres opciones y cada una empieza una guia paso a paso.
            Call(form,owner,"SeleccionaAsistente",9); Check((int)Get(owner,"asistenteSeleccion")==2,"Tres opciones, sin pasarse");
            Call(form,owner,"SeleccionaAsistente",0);
            Call(form,owner,"EligeAsistente"); Check((int)Get(owner,"asistenteMision")==1,"Primera opcion: poner una cancion, guiada");
            Call(form,owner,"BuildAsistenteState"); Check(!ReferenceEquals(first,Get(owner,"asistentePanel")) && (int)Get(owner,"asistentePanelVersion")==version+1,"Empezar la guia reemplaza una sola imagen");
            Bitmap guia=(Bitmap)Get(owner,"asistentePanel"); int vGuia=(int)Get(owner,"asistentePanelVersion");
            for(int i=0;i<60;i++) Call(form,owner,"BuildAsistenteState");
            Check(ReferenceEquals(guia,Get(owner,"asistentePanel")) && vGuia==(int)Get(owner,"asistentePanelVersion"),"Guiando, el globo no se rehace por cuadro");
            Check((Size)Call(gpu,null,"AsistenteGuiaTam",640,480)==guia.Size,"Globo pequeno de la guia");
            Set(owner,"asistenteMision",0); Set(owner,"asistenteSeleccion",1); Call(form,owner,"EligeAsistente");
            Check((int)Get(owner,"asistenteMision")==2,"Segunda opcion: buscar por el nombre");
            Set(owner,"asistenteMision",0); Set(owner,"asistenteSeleccion",2); Call(form,owner,"EligeAsistente");
            Check((int)Get(owner,"asistenteMision")==3,"Tercera opcion: aprender a moverse");
            Call(form,owner,"SueltaAsistente"); Check(Get(owner,"asistentePanel")==null,"Bitmap del formulario se libera");
        }
        MethodInfo entry=form.GetMethod("ProcessCmdKey",B), frame=gpu.GetMethod("RenderFrame",B);
        Check(Codigo.Llama(entry,"AsistenteEntrada") && form.GetMethod("ReenviaEntradaAsistente",B)==null,"Los botones responden al instante (sin acorde rojo+verde)");
        Check(Codigo.Llama(frame,"PreparaAsistente") && Codigo.Llama(frame,"AvanzaAsistente") && Codigo.Llama(frame,"DibujaAsistente"),"Conectado al cuadro GPU");
        Check(!Codigo.Llama(form.GetMethod("AbreAsistente",B),"AddItem") && !Codigo.Llama(form.GetMethod("EligeAsistente",B),"AddItem"),"Ayuda no encola ni gasta credito");
        Check(Codigo.Llama(form.GetMethod("EligeAsistente",B),"EmpiezaMision") && Codigo.Llama(gpu.GetMethod("DibujaAsistente",B),"DibujaFocoGuia")
            && Codigo.Llama(gpu.GetMethod("DibujaAsistente",B),"DibujaFlechaGuia"),"La ayuda guia sobre la Rocola real: foco y flecha");
        foreach(string method in new string[]{"AvanzaAsistente","DibujaAsistente","AsistenteMallaMiembros","AsistenteSegmento","AsistenteDisco",
            "AvanzaPersonalidad","AsistenteActua","AsistenteMezcla","AsistenteMiembrosExpresivos","AsistenteBrazosExpresivos",
            "AsistenteArmaBrazos","AsistenteGeometriaValida","AsistenteRotaMalla"})
            foreach(Codigo.Paso step in Codigo.Pasos(gpu.GetMethod(method,B))) Check(step.Op!=0x8D,"Sin reservar arreglos en animacion: "+method);
    }

    static object GestureState(string gesture,int serial,int selection)
    {
        object s=State(true,true,null,1);
        Set(s,"Gesto",Enum.Parse(form.Assembly.GetType("Rocola.AsistenteGestoGpu",true),gesture));
        Set(s,"Evento",serial); Set(s,"Seleccion",selection); return s;
    }
    static void Personality()
    {
        Type exp=form.Assembly.GetType("Rocola.AsistenteExpresionGpu",true);
        Type gestures=form.Assembly.GetType("Rocola.AsistenteGestoGpu",true);
        FieldInfo[] fields=exp.GetFields(B);
        object neutral=Call(gpu,null,"AsistenteNeutral");
        Check(gpu.GetMethod("AsistenteMallaCara",B)==null && gpu.GetField("asistenteCara",B)==null,
            "El renderer no contiene rostro ni buffer facial");
        foreach(string name in new string[]{"Ojos","Guino","MiraX","MiraY","Cejas","Sonrisa","Boca"})
            Check(exp.GetField(name,B)==null,"Expresiones exclusivamente corporales: "+name);
        Array limbs=Array.CreateInstance(vtx,1903),arms=Array.CreateInstance(vtx,703);
        foreach(object gesture in Enum.GetValues(gestures))
        {
            object previous=null;
            for(int frame=0;frame<=1440;frame++) {
                double time=frame/240.0;
                object e=Call(gpu,null,"AsistenteActua",time,time,gesture,1,true);
                foreach(FieldInfo field in fields) {
                    float value=(float)field.GetValue(e);
                    Check(Finite(value)&&Math.Abs(value)<2,"Expresion finita y acotada: "+gesture+"/"+field.Name);
                    if(previous!=null) Check(Math.Abs(value-(float)field.GetValue(previous))<.12,
                        "Gesto continuo a 240 Hz: "+gesture+"/"+field.Name);
                }
                previous=e;
            }
            foreach(double time in new double[]{0,.25,.7,1.5,2.3,4.9,8.5})
            {
                object e=Call(gpu,null,"AsistenteActua",time,time,gesture,1,true);
                foreach(float radius in new float[]{40,220})
                foreach(bool front in new bool[]{false,true}) {
                    Array vertices=front ? arms : limbs; string method=front ? "AsistenteBrazosExpresivos" : "AsistenteMiembrosExpresivos";
                    object sentinel=Activator.CreateInstance(vtx); Set(sentinel,"X",12345f); vertices.SetValue(sentinel,vertices.Length-1);
                    int n=(int)Call(gpu,null,method,vertices,500f,300f,radius,1f,1f,e,F(e,"Inclinacion"));
                    Check(n>0&&n%3==0&&n<=vertices.Length-3,"Geometria expresiva acotada: "+gesture);
                    Check(F(vertices.GetValue(vertices.Length-1),"X")==12345f,"No escribe fuera de malla");
                    for(int i=0;i<n;i++) {
                        object vertex=vertices.GetValue(i); float x=F(vertex,"X"),y=F(vertex,"Y");
                        Check(Finite(x)&&Finite(y)&&Math.Abs(x-500)<radius*1.8f+5&&Math.Abs(y-300)<radius*1.8f+5,"Miembros no salen de personaje");
                        Check(((uint)Get(vertex,"Color")&0xFFFFFFu)==0x06080Du,"Solo miembros negros, no rasgos sobre el icono");
                    }
                }
            }
            object still=Call(gpu,null,"AsistenteActua",16.1,2.2,gesture,1,false);
            foreach(FieldInfo field in fields) Check((float)field.GetValue(still)==(float)field.GetValue(neutral),"Toggle apaga TODOS los gestos");
            if(Convert.ToInt32(gesture)!=0) {
                object active=Call(gpu,null,"AsistenteActua",.6,.6,gesture,1,true);
                object idle=Call(gpu,null,"AsistenteActua",.6,.6,Enum.ToObject(gestures,0),1,true);
                float difference=0;
                foreach(FieldInfo field in fields) difference+=Math.Abs((float)field.GetValue(active)-(float)field.GetValue(idle));
                Check(difference>.20f,"Cada accion tiene lenguaje corporal propio: "+gesture);
            }
        }
        foreach(double bad in new double[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity,-1}) {
            object safe=Call(gpu,null,"AsistenteActua",bad,bad,Enum.ToObject(gestures,1),0,true);
            foreach(FieldInfo field in fields) Check(Finite((float)field.GetValue(safe)),"Reloj invalido seguro");
        }
        foreach(string method in new string[]{"AsistenteBrazosExpresivos","AsistenteMiembrosExpresivos"})
        foreach(float bad in new float[]{0,-1,float.NaN,float.PositiveInfinity})
            Check((int)Call(gpu,null,method,method=="AsistenteBrazosExpresivos" ? arms : limbs,0f,0f,bad,1f,1f,neutral,0f)==0,"Radio invalido no desborda");
        Check((int)Call(gpu,null,"AsistenteBrazosExpresivos",Array.CreateInstance(vtx,699),0f,0f,100f,1f,1f,neutral,0f)==0,"Buffer frontal corto no desborda");
        Check((int)Call(gpu,null,"AsistenteMiembrosExpresivos",Array.CreateInstance(vtx,1899),0f,0f,100f,1f,1f,neutral,0f)==0,"Buffer posterior corto no desborda");
        using(Control p=(Control)Activator.CreateInstance(gpu,true)) {
            p.Size=new Size(640,480); Set(p,"uiBotH",68); Set(p,"asistenteEstado",GestureState("Saludar",1,0));
            Pose(p,1000); Pose(p,2100); object before=Get(p,"asistenteExpresion");
            Set(p,"asistenteEstado",GestureState("Credito",2,1)); Pose(p,2100); object after=Get(p,"asistenteExpresion");
            foreach(FieldInfo field in fields) Check((float)field.GetValue(before)==(float)field.GetValue(after),"Cambiar de gesto parte de la pose actual");
            for(int i=1;i<=60;i++) {
                Pose(p,2100+i*1000.0/240); object e=Get(p,"asistenteExpresion");
                foreach(FieldInfo field in fields) Check(Math.Abs((float)field.GetValue(e)-(float)field.GetValue(after))<.13,"Mezcla rapida sin salto");
                after=e;
            }
            Set(p,"asistenteEstado",State(true,false,null,1)); Pose(p,2500);
            foreach(FieldInfo field in fields) Check((float)field.GetValue(Get(p,"asistenteExpresion"))==(float)field.GetValue(neutral),"Desactivar en vivo conserva pose quieta");
        }
        // Con postura/pasos vivos, las manos siguen sin entrar en los botones y
        // los pies no pisan la barra de estado en ninguna resolucion habitual.
        foreach(Size size in new Size[]{new Size(640,480),new Size(1024,768),new Size(1920,1080),new Size(3840,2160)})
        using(Control p=(Control)Activator.CreateInstance(gpu,true)) {
            p.Size=size; Set(p,"uiBotH",68); Rectangle card=Card(size); int serial=1;
            foreach(object gesture in Enum.GetValues(gestures))
            foreach(int selection in new int[]{0,1}) {
                Set(p,"asistenteEstado",GestureState(gesture.ToString(),serial++,selection)); Pose(p,1000);
                foreach(double t in new double[]{.0,.2,.6,1.5,2.3,4.3,8.5}) {
                    object pose=Pose(p,1720+t*1000); PointF c=(PointF)Get(pose,"Centro");
                    object e=Get(p,"asistenteExpresion");
                    foreach(bool front in new bool[]{false,true}) {
                        Array vertices=front ? arms : limbs;
                        int n=(int)Call(gpu,null,front ? "AsistenteBrazosExpresivos" : "AsistenteMiembrosExpresivos",
                            vertices,c.X,c.Y,F(pose,"Radio"),1f,1f,e,F(pose,"Giro"));
                        for(int i=0;i<n;i++) {
                            object v=vertices.GetValue(i);
                            Check(F(v,"X")>0 && F(v,"X")<card.Left-2 && F(v,"Y")>0 && F(v,"Y")<size.Height-48,
                                "Lenguaje corporal no recorta miembros ni tapa controles: "+size+"/"+gesture);
                        }
                    }
                }
                Set(p,"asistenteEstado",State(false,false,null,1)); Pose(p,12000);
            }
        }
    }

    // Coste CPU sintetico del reloj fino + dos mallas, SIN reflexion/boxing en el
    // bucle, sin HAL, sin lectura de disco. No es FPS de la Rocola de produccion.
    static void PersonalityCost()
    {
        DynamicMethod m=new DynamicMethod("CuadroPersonalidad",typeof(void),new Type[]{typeof(Control),typeof(double)},typeof(AsistenteVisual),true);
        ILGenerator il=m.GetILGenerator(); LocalBuilder local=il.DeclareLocal(gpu);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass,gpu); il.Emit(OpCodes.Stloc,local);
        il.Emit(OpCodes.Ldloc,local); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Stfld,gpu.GetField("cuadroMs",B));
        il.Emit(OpCodes.Ldloc,local); il.Emit(OpCodes.Call,gpu.GetMethod("AvanzaAsistente",B));
        foreach(string method in new string[]{"AsistenteMiembrosExpresivos","AsistenteBrazosExpresivos"}) {
            il.Emit(OpCodes.Ldloc,local); il.Emit(OpCodes.Ldfld,gpu.GetField(method=="AsistenteBrazosExpresivos" ? "asistenteBrazos" : "asistenteMiembros",B));
            foreach(float number in new float[]{200,200,100,1,1}) il.Emit(OpCodes.Ldc_R4,number);
            il.Emit(OpCodes.Ldloc,local); il.Emit(OpCodes.Ldfld,gpu.GetField("asistenteExpresion",B)); il.Emit(OpCodes.Ldc_R4,0f);
            il.Emit(OpCodes.Call,gpu.GetMethod(method,B)); il.Emit(OpCodes.Pop);
        }
        il.Emit(OpCodes.Ret);
        Action<Control,double> frame=(Action<Control,double>)m.CreateDelegate(typeof(Action<Control,double>));
        foreach(bool natural in new bool[]{false,true})
        foreach(bool threeD in new bool[]{false,true})
        using(Control p=(Control)Activator.CreateInstance(gpu,true)) {
            p.Size=new Size(1024,768); Set(p,"uiBotH",68); Set(p,"FondoNatural",natural); Set(p,"Estrella3D",threeD);
            Set(p,"asistenteEstado",GestureState("Explicar",1,0));
            for(int i=0;i<300;i++) frame(p,1000+i*1000.0/60);
            double[] times=new double[2000]; int[] gc=new int[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
            for(int i=0;i<times.Length;i++) {
                long start=Stopwatch.GetTimestamp(); frame(p,6000+i*1000.0/60);
                times[i]=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
            }
            for(int i=0;i<3;i++) Check(GC.CollectionCount(i)==gc[i],"Cuadros sin colecciones GC (no implica causalidad de pausas externas)");
            Array.Sort(times);
            Console.WriteLine("SINTETICO personalidad "+(natural?"Natural":"Legacy")+"/"+(threeD?"3D":"2D")+
                ": CPU p95="+times[1900].ToString("F3")+" ms p99="+times[1980].ToString("F3")+" ms max="+times[1999].ToString("F3")+" ms. No FPS reales.");
        }
    }

    [StructLayout(LayoutKind.Sequential)] struct Desc { public uint Format, Type, Usage, Pool, Multi, Quality, Width, Height; }
    [StructLayout(LayoutKind.Sequential)] struct Locked { public int Pitch; public IntPtr Data; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Hr(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint Rel(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Back(IntPtr self,uint a,uint b,uint c,out IntPtr surface);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Description(IntPtr self,out Desc desc);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Offscreen(IntPtr self,uint w,uint h,uint format,uint pool,out IntPtr surface,IntPtr shared);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Readback(IntPtr self,IntPtr source,IntPtr dest);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Lock(IntPtr self,out Locked data,IntPtr region,uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Present(IntPtr self,IntPtr source,IntPtr dest,IntPtr window,IntPtr dirty);
    static T Fn<T>(IntPtr p,int slot) where T:class { return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(p),slot*IntPtr.Size),typeof(T)); }
    static void Release(IntPtr p) { if(p!=IntPtr.Zero) Fn<Rel>(p,2)(p); }
    static Bitmap ReadPixels(object device,Size size)
    {
        IntPtr dev=(IntPtr)Get(device,"dev"),back=IntPtr.Zero,off=IntPtr.Zero; bool locked=false;
        try {
            Check(Fn<Back>(dev,18)(dev,0,0,0,out back)>=0,"Backbuffer asistente HAL"); Desc desc;
            Check(Fn<Description>(back,12)(back,out desc)>=0 && desc.Width==size.Width && desc.Height==size.Height,"Resolucion exacta HAL");
            Check(Fn<Offscreen>(dev,36)(dev,desc.Width,desc.Height,desc.Format,2,out off,IntPtr.Zero)>=0,"Readback superficie HAL");
            Check(Fn<Readback>(dev,32)(dev,back,off)>=0,"Lectura real HAL"); Locked data;
            Check(Fn<Lock>(off,13)(off,out data,IntPtr.Zero,0x10)>=0,"Lectura pixels HAL"); locked=true;
            Bitmap bitmap=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppArgb); BitmapData dest=bitmap.LockBits(new Rectangle(Point.Empty,size),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try {
                int[] row=new int[size.Width]; for(int y=0;y<size.Height;y++) {
                    Marshal.Copy(new IntPtr(data.Data.ToInt64()+y*(long)data.Pitch),row,0,row.Length);
                    for(int x=0;x<row.Length;x++) row[x]|=unchecked((int)0xFF000000u);
                    Marshal.Copy(row,0,new IntPtr(dest.Scan0.ToInt64()+y*(long)dest.Stride),row.Length);
                }
            } finally { bitmap.UnlockBits(dest); }
            return bitmap;
        } finally { if(locked)Fn<Hr>(off,14)(off); Release(off); Release(back); }
    }
    static void Hal(string output)
    {
        foreach(Size size in new Size[]{new Size(640,480),new Size(1024,768)})
        using(Control p=(Control)Activator.CreateInstance(gpu,true))
        {
            p.Size=size; p.Visible=false; Check((bool)Call(gpu,p,"StartGpu"),"HAL del asistente disponible");
            object sky=Get(p,"stars"),device=Get(sky,"gpu"); Set(p,"uiBotH",68); Set(p,"idleBlend",0f);
            Call(sky.GetType(),sky,"SetSky",0xFF06081Au,0xFF101C3Au,0xFF1A2C5Au);
            Rectangle r=Card(size);
            using(Bitmap bitmap=(Bitmap)Call(form,null,"PintaPanelAsistente",r.Width,r.Height,0,0,0))
            {
                StateSource source=new StateSource(State(true,true,bitmap,1));
                DynamicMethod read=new DynamicMethod("EstadoTipadoAislado",state,new Type[]{typeof(StateSource)},typeof(AsistenteVisual),true);
                ILGenerator code=read.GetILGenerator(); code.Emit(OpCodes.Ldarg_0); code.Emit(OpCodes.Ldfld,typeof(StateSource).GetField("Value"));
                code.Emit(OpCodes.Unbox_Any,state); code.Emit(OpCodes.Ret);
                gpu.GetField("GetAsistenteState",B).SetValue(p,read.CreateDelegate(gpu.GetField("GetAsistenteState",B).FieldType,source));
                // Incluso con la estrella de fondo apagada, la ayuda debe tener su
                // propio personaje. Nunca toca ni vuelve a encender esa preferencia.
                Set(p,"ConLuna",false);
                foreach(bool natural in new bool[]{false,true})
                foreach(bool threeD in new bool[]{false,true})
                {
                    Set(p,"FondoNatural",natural); Set(p,"Estrella3D",threeD);
                    source.Value=State(true,true,bitmap,1); Call(gpu,p,"PreparaAsistente"); Call(gpu,p,"EnsureLuna");
                    Pose(p,1000); Pose(p,1720);
                    IntPtr texture=(IntPtr)Get(p,"asistenteTex"); Check(texture!=IntPtr.Zero,"Tarjeta subida antes de Begin");
                    for(int frame=0;frame<120;frame++) {
                        Call(gpu,p,"PreparaAsistente"); Check((IntPtr)Get(p,"asistenteTex")==texture,"Tarjeta GPU reutilizada");
                    }
                    Check((bool)Call(sky.GetType(),sky,"Begin",size.Width,size.Height,0f,0f),"Begin asistente HAL");
                    Call(gpu,p,"DibujaLuna"); // ocultada: no debe duplicar personaje ni ecos
                    Call(gpu,p,"DibujaAsistente");
                    IntPtr dev=(IntPtr)Get(device,"dev"); Check(Fn<Hr>(dev,42)(dev)>=0,"EndScene asistente HAL");
                    Set(device,"inScene",false); Call(device.GetType(),device,"SueltaPinesVertices");
                    using(Bitmap shot=ReadPixels(device,size)) {
                        string file=Path.Combine(output,"asistente-"+(natural?"natural":"legacy")+"-"+(threeD?"3d":"2d")+"-"+size.Width+"x"+size.Height+".png");
                        shot.Save(file,ImageFormat.Png);
                        int black=0,light=0; PointF c=(PointF)Call(gpu,null,"AsistenteCentroFrente",size.Width,size.Height);
                        float radius=(float)Call(gpu,null,"AsistenteRadioFrente",size.Width,size.Height);
                        for(int y=(int)(c.Y-radius);y<(int)(c.Y+radius*1.55f);y++)
                        for(int x=(int)(c.X-radius*1.35f);x<(int)(c.X+radius*1.35f);x++) {
                            Color color=shot.GetPixel(x,y); if((color.ToArgb()&0xFFFFFF)==0x06080D) black++;
                            if(color.R+color.G+color.B>180)light++;
                        }
                        Check(black>20 && light>100,"Brazos/piernas negros y cuerpo visible en HAL");
                        // Comparar el centro con EL MISMO material original sin
                        // miembros/globo. Ojos, boca o placas faciales alterarian
                        // estos pixeles; la geometria central debe quedar intacta.
                        float presence=(float)Get(p,"asistenteFrente");
                        try {
                            Set(p,"asistenteFrente",0f);
                            Check((bool)Call(sky.GetType(),sky,"Begin",size.Width,size.Height,0f,0f),"Begin material sin rasgos HAL");
                            Call(gpu,p,"DibujaAsistente");
                            Check(Fn<Hr>(dev,42)(dev)>=0,"EndScene material sin rasgos HAL");
                            Set(device,"inScene",false); Call(device.GetType(),device,"SueltaPinesVertices");
                            using(Bitmap original=ReadPixels(device,size)) {
                                for(int y=(int)(c.Y-radius*.20f);y<(int)(c.Y+radius*.20f);y++)
                                for(int x=(int)(c.X-radius*.20f);x<(int)(c.X+radius*.20f);x++) {
                                    Color a=shot.GetPixel(x,y),b=original.GetPixel(x,y);
                                    Check(Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B)<=3,
                                        "Icono sin rostro: centro igual al material original en HAL");
                                }
                            }
                        } finally { Set(p,"asistenteFrente",presence); }
                    }
                    Check(Fn<Present>(dev,17)(dev,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero)>=0,"Present asistente HAL");
                    // Capturas de acciones con el renderer real, no sprites dibujados
                    // por la prueba. El globo cambia solo con el mensaje, no con el gesto.
                    if(size.Width==640) {
                        int serial=2;
                        foreach(string gesture in new string[]{"Saludar","Explicar","Buscar","Senalar","Confirmar","Credito","Despedir"}) {
                            object s=GestureState(gesture,serial,1); Set(s,"Panel",bitmap); Set(s,"Version",1); source.Value=s;
                            double time=4000+serial*2000;
                            Call(gpu,p,"PreparaAsistente"); Pose(p,time); Pose(p,time+600);
                            Check((IntPtr)Get(p,"asistenteTex")==texture,"Animaciones GPU conservan la textura del globo");
                            Check((bool)Call(sky.GetType(),sky,"Begin",size.Width,size.Height,0f,0f),"Begin gesto HAL");
                            Call(gpu,p,"DibujaAsistente"); Check(Fn<Hr>(dev,42)(dev)>=0,"EndScene gesto HAL");
                            Set(device,"inScene",false); Call(device.GetType(),device,"SueltaPinesVertices");
                            using(Bitmap shot=ReadPixels(device,size)) shot.Save(Path.Combine(output,
                                "gesto-"+(natural?"natural":"legacy")+"-"+(threeD?"3d":"2d")+"-"+gesture.ToLowerInvariant()+".png"),ImageFormat.Png);
                            Check(Fn<Present>(dev,17)(dev,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero)>=0,"Present gesto HAL"); serial++;
                        }
                    }
                    // Cambiar mensaje puede cambiar la opacidad, NO desplazar
                    // los botones debajo de un clic que acaba de llegar.
                    float oldY=F(((Array)Get(p,"asistenteQuad")).GetValue(0),"Y");
                    using(Bitmap changed=(Bitmap)Call(form,null,"PintaPanelAsistente",r.Width,r.Height,1,0,0)) {
                        object s=State(true,true,changed,2); Set(s,"Pagina",1); Set(s,"Evento",40); source.Value=s;
                        Set(p,"cuadroMs",30000.0); Call(gpu,p,"PreparaAsistente"); Pose(p,30000);
                        Check((bool)Call(sky.GetType(),sky,"Begin",size.Width,size.Height,0f,0f),"Begin cambio mensaje HAL");
                        Call(gpu,p,"DibujaAsistente");
                        Check(F(((Array)Get(p,"asistenteQuad")).GetValue(0),"Y")==oldY,"Cambio de texto conserva posiciones de clic");
                        Check(Fn<Hr>(dev,42)(dev)>=0,"EndScene mensaje HAL"); Set(device,"inScene",false); Call(device.GetType(),device,"SueltaPinesVertices");
                        Check(Fn<Present>(dev,17)(dev,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero)>=0,"Present cambio mensaje HAL");
                        s=State(true,true,changed,3); Set(s,"Pagina",1); Set(s,"Leccion",1); source.Value=s;
                        Set(p,"cuadroMs",30100.0); Call(gpu,p,"PreparaAsistente");
                        Check((int)Get(p,"asistenteTextoClave")==5 && (double)Get(p,"asistenteTextoInicio")==30.1,"Siguiente paso tambien anima el mensaje");
                        s=State(true,true,changed,4); Set(s,"Pagina",1); Set(s,"Leccion",1); Set(s,"Seleccion",1); source.Value=s;
                        Set(p,"cuadroMs",30200.0); Call(gpu,p,"PreparaAsistente");
                        Check((double)Get(p,"asistenteTextoInicio")==30.1,"Cambiar opcion no reinicia el fundido del mensaje");
                    }
                    source.Value=State(false,true,bitmap,1); Call(gpu,p,"PreparaAsistente"); Pose(p,2000); Pose(p,2720); Call(gpu,p,"PreparaAsistente");
                    Check((IntPtr)Get(p,"asistenteTex")==IntPtr.Zero && bitmap.Width==r.Width,"Textura liberada; bitmap prestado sigue vivo");
                    Check(!(bool)Get(p,"ConLuna"),"La ayuda no modifica el toggle del fondo");
                }
            }
            Call(gpu,p,"StopGpu"); Check((IntPtr)Get(device,"dev")==IntPtr.Zero,"Dispositivo liberado");
        }
    }
    class StateSource { public object Value; public StateSource(object value) { Value=value; } }
    [STAThread] static int Main(string[] args)
    {
        try {
            Assembly assembly=Assembly.LoadFrom(args[0]); form=assembly.GetType("Rocola.Form1",true); gpu=assembly.GetType("Rocola.GpuPanel",true);
            state=assembly.GetType("Rocola.AsistenteEstadoGpu",true); vtx=assembly.GetType("Rocola.D3D9+Vtx",true);
            string output=args.Length>1 && args[1]!="--sin-hal" ? args[1] : Path.Combine(Path.GetTempPath(),"rocola-asistente-visual-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(output);
            Layout(output); Mesh(); Animation(); CacheAndMenu(); Personality(); PersonalityCost();
            if(Array.IndexOf(args,"--sin-hal")<0 && Environment.OSVersion.Platform==PlatformID.Win32NT) Hal(output);
            else Console.WriteLine("OMITIDO HAL: no confirma apariencia final GPU.");
            Console.WriteLine("PASS asistente visual: "+checks+" comprobaciones. Capturas: "+output+". Fixture aislada, no FPS reales ni prueba XP."); return 0;
        } catch(Exception e) { Console.WriteLine("FAIL "+e); return 1; }
    }
}
