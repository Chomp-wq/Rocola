using System;
using System.Drawing;
using System.Drawing.Imaging;
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
            Check(r.Left > 0 && r.Right <= size.Width && r.Top > 0 && r.Bottom <= size.Height - 48 && r.Height <= 800, "Tarjeta dentro de pantalla: " + size);
            PointF center = (PointF)Call(gpu, null, "AsistenteCentroFrente", size.Width, size.Height);
            float radius = (float)Call(gpu, null, "AsistenteRadioFrente", size.Width, size.Height);
            Check(center.X - radius * 1.35f > 0 && center.X + radius * 1.35f < r.Left,
                "Personaje no tapa opciones: " + size);
            Check(center.Y - radius > 0 && center.Y + radius * 1.55f < size.Height - 48, "Miembros visibles: " + size);
            Rectangle a = (Rectangle)Call(form, null, "AsistenteOpcionRect", r.Width, r.Height, 0);
            Rectangle b = (Rectangle)Call(form, null, "AsistenteOpcionRect", r.Width, r.Height, 1);
            Check(a.Left >= 0 && a.Right <= r.Width && a.Top >= 0 && b.Bottom < r.Height * .93f && !a.IntersectsWith(b), "Opciones separadas y alcanzables: " + size);
            foreach (int page in new int[] { 0, 1, 2 })
            for (int step = 0; step < (page == 1 ? 3 : 1); step++)
            foreach (int sel in new int[] { 0, 1 })
            using (Bitmap bitmap = (Bitmap)Call(form, null, "PintaPanelAsistente", r.Width, r.Height, page, step, sel))
            {
                Check(bitmap.Size == r.Size && bitmap.GetPixel(0,0).A == 0 && bitmap.GetPixel(r.Width / 2, r.Height / 2).A >= 240, "Vidrio legible sin fondo rectangular opaco");
                if (size.Width == 640 || size.Width == 1024)
                    bitmap.Save(Path.Combine(output, "panel-" + size.Width + "-" + page + "-" + step + "-" + sel + ".png"), ImageFormat.Png);
            }
        }
        foreach (Size extreme in new Size[] { Size.Empty, new Size(-10,-10), new Size(1,1), new Size(16,16) })
        { Rectangle r = Card(extreme); Check(r.Width >= 1 && r.Height >= 1 && r.X >= 0 && r.Y >= 0, "Geometria minimizada segura"); }
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
            Check((bool)gpu.GetProperty("AsistenteEnTransicion",B).GetValue(panel,null), "Sigue visible durante el regreso");
            Pose(panel,2720);
            Check(!(bool)gpu.GetProperty("AsistenteEnTransicion",B).GetValue(panel,null), "Devuelve la estrella al fondo al terminar");
            Set(panel,"asistenteEstado",State(true,false,null,1)); Pose(panel,3000);
            Check(F(Get(panel,"asistentePose"),"Miembros")==1, "Animacion apagada conserva ayuda y personaje");
            Set(panel,"asistenteEstado",State(false,false,null,1)); Pose(panel,3001);
            Check(!(bool)gpu.GetProperty("AsistenteEnTransicion",B).GetValue(panel,null), "Cerrar sin animacion es inmediato");
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
            Call(form,owner,"EligeAsistente"); Check((int)Get(owner,"asistentePagina")==1,"Primera opcion abre tutorial");
            Call(form,owner,"BuildAsistenteState"); Check(!ReferenceEquals(first,Get(owner,"asistentePanel")) && (int)Get(owner,"asistentePanelVersion")==version+1,"Cambiar pagina reemplaza una sola imagen");
            Call(form,owner,"EligeAsistente"); Call(form,owner,"EligeAsistente");
            Check((int)Get(owner,"asistenteLeccion")==2,"Tres pasos de guia");
            Call(form,owner,"EligeAsistente"); Check((int)Get(owner,"asistentePagina")==2,"Ultimo paso ayuda a poner una cancion");
            Set(owner,"asistentePagina",0); Set(owner,"asistenteSeleccion",1); Call(form,owner,"EligeAsistente");
            Check((int)Get(owner,"asistentePagina")==2,"Segunda opcion lleva a buscar");
            Set(owner,"asistentePagina",1); Set(owner,"asistenteSeleccion",1); Call(form,owner,"EligeAsistente");
            Check((int)Get(owner,"asistentePagina")==0 && (int)Get(owner,"asistenteSeleccion")==0,"Volver al inicio es coherente");
            Call(form,owner,"SueltaAsistente"); Check(Get(owner,"asistentePanel")==null,"Bitmap del formulario se libera");
        }
        MethodInfo entry=form.GetMethod("ProcessCmdKey",B), frame=gpu.GetMethod("RenderFrame",B);
        Check(Codigo.Llama(entry,"AsistenteEntrada") && Codigo.Llama(form.GetMethod("OnKeyUp",B),"ReenviaEntradaAsistente"),"Pulsar y soltar no pierde toques rapidos");
        Check(Codigo.Llama(frame,"PreparaAsistente") && Codigo.Llama(frame,"AvanzaAsistente") && Codigo.Llama(frame,"DibujaAsistente"),"Conectado al cuadro GPU");
        Check(!Codigo.Llama(form.GetMethod("AbreAsistente",B),"AddItem") && !Codigo.Llama(form.GetMethod("EligeAsistente",B),"AddItem"),"Ayuda no encola ni gasta credito");
        Check(Codigo.Llama(form.GetMethod("EligeAsistente",B),"OpenSearch"),"Ayuda usa el buscador real");
        foreach(string method in new string[]{"AvanzaAsistente","DibujaAsistente","AsistenteMallaMiembros","AsistenteSegmento","AsistenteDisco"})
            foreach(Codigo.Paso step in Codigo.Pasos(gpu.GetMethod(method,B))) Check(step.Op!=0x8D,"Sin reservar arreglos en animacion: "+method);
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
                    }
                    Check(Fn<Present>(dev,17)(dev,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero)>=0,"Present asistente HAL");
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
            Layout(output); Mesh(); Animation(); CacheAndMenu();
            if(Array.IndexOf(args,"--sin-hal")<0 && Environment.OSVersion.Platform==PlatformID.Win32NT) Hal(output);
            else Console.WriteLine("OMITIDO HAL: no confirma apariencia final GPU.");
            Console.WriteLine("PASS asistente visual: "+checks+" comprobaciones. Capturas: "+output+". Fixture aislada, no FPS reales ni prueba XP."); return 0;
        } catch(Exception e) { Console.WriteLine("FAIL "+e); return 1; }
    }
}
