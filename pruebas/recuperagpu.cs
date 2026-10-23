// Contrato de recuperacion Direct3D 9: parametros de entrada/salida, reentrada
// desde Reset, perdida prolongada y recuperacion antes de preparar texturas.
// No necesita un controlador Windows: inyecta callbacks con las firmas COM reales.
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;
class RecuperaGpu
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type T, P, S; static int checks, fallos, resets, comienzos, estados, cooperativas;
    static int resetResultado, coopResultado; static uint anchoPedido, altoPedido, cuentaPedida;
    static bool reentrada; static object dispositivo; static Control panel;
    static object Get(object o, string n) { return o.GetType().GetField(n,B).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n,B).SetValue(o,v); }
    static object Call(object o, string n, params object[] a)
    { try { return o.GetType().GetMethod(n,B).Invoke(o,a); } catch(TargetInvocationException e) { throw e.InnerException; } }
    static void Check(bool b, string s) { checks++; if(!b) fallos++; Console.WriteLine((b?"OK ":"FALLO ")+s); }
    static void Enchufa(object d, string campo, string callback)
    {
        FieldInfo f = T.GetField(campo,B); MethodInfo invoke = f.FieldType.GetMethod("Invoke");
        ParameterInfo[] ps = invoke.GetParameters(); Type[] ts = new Type[ps.Length];
        for(int i=0;i<ts.Length;i++) ts[i]=ps[i].ParameterType;
        DynamicMethod m = new DynamicMethod("callback_"+campo,invoke.ReturnType,ts,typeof(RecuperaGpu),true);
        ILGenerator il=m.GetILGenerator();
        MethodInfo cb=typeof(RecuperaGpu).GetMethod(callback,B);
        for(int i=0;i<cb.GetParameters().Length;i++) { il.Emit(OpCodes.Ldarg,i); if(ts[i].IsByRef)il.Emit(OpCodes.Conv_I); }
        il.Emit(OpCodes.Call,cb); il.Emit(OpCodes.Ret);
        f.SetValue(d,m.CreateDelegate(f.FieldType));
    }
    static int Estado(IntPtr d) { estados++; return 0; }
    static int Coop(IntPtr d) { cooperativas++; return coopResultado; }
    static int Begin(IntPtr d) { comienzos++; return 0; }
    static int Nada(IntPtr d) { return 0; }
    static int Reset(IntPtr d, IntPtr pp)
    {
        resets++;
        anchoPedido=(uint)Marshal.ReadInt32(pp,0); altoPedido=(uint)Marshal.ReadInt32(pp,4); cuentaPedida=(uint)Marshal.ReadInt32(pp,12);
        if(reentrada)
        {
            reentrada=false;
            int antes=comienzos, antesR=resets;
            Check(!(bool)Call(dispositivo,"BeginFrame"),"Reset bloquea BeginFrame reentrante");
            Check(!(bool)Call(dispositivo,"Redimensiona",1280,720),"Reset bloquea otro Reset reentrante");
            Check(comienzos==antes && resets==antesR,"ninguna llamada grafica anidada dentro de Reset");
            if(panel!=null) Check(!(bool)Call(panel,"RenderFrame"),"WM_PAINT durante Reset no entra en otro cuadro");
        }
        // Contrato documentado de Reset: estos parametros se ponen a cero al regresar.
        Marshal.WriteInt32(pp,0,0); Marshal.WriteInt32(pp,4,0); Marshal.WriteInt32(pp,12,0);
        return resetResultado;
    }
    static object Nuevo()
    {
        resets=comienzos=estados=cooperativas=0; resetResultado=coopResultado=0; panel=null; reentrada=false;
        object d=FormatterServices.GetUninitializedObject(T); dispositivo=d;
        Set(d,"dev",new IntPtr(123));
        object pp=Activator.CreateInstance(T.Assembly.GetType("Rocola.D3D9+PresentParams"));
        pp.GetType().GetField("BackBufferWidth").SetValue(pp,1024u);pp.GetType().GetField("BackBufferHeight").SetValue(pp,768u);
        pp.GetType().GetField("BackBufferCount").SetValue(pp,1u);Set(d,"pp",pp);
        Enchufa(d,"reset","Reset");Enchufa(d,"testCoop","Coop");Enchufa(d,"begin","Begin");Enchufa(d,"end","Nada");
        Enchufa(d,"present","Nada");Enchufa(d,"setRS","Estado");Enchufa(d,"setFVF","Estado");Enchufa(d,"setSamp","Estado");
        return d;
    }
    static uint Param(object d,string n) {object p=Get(d,"pp");return (uint)p.GetType().GetField(n).GetValue(p);}
    static void VenceEspera(object d)
    { FieldInfo f=T.GetField("resetProximo",B); if(f!=null) f.SetValue(d,unchecked(Environment.TickCount-1)); }
    static void Pruebas()
    {
        object d=Nuevo();
        Check((bool)Call(d,"Redimensiona",1280,720),"resize valido recupera dispositivo");
        Check(anchoPedido==1280 && altoPedido==720 && cuentaPedida==1,"Reset recibe el tamano pedido");
        Check(Param(d,"BackBufferWidth")==1280 && Param(d,"BackBufferHeight")==720 && Param(d,"BackBufferCount")==1,"Reset no destruye los parametros persistentes");
        int n=resets; Check((bool)Call(d,"Redimensiona",1280,720) && resets==n,"mismo tamano no reinicia cada cuadro");
        d=Nuevo(); resetResultado=unchecked((int)0x88760868);
        Check(!(bool)Call(d,"Redimensiona",1280,720),"Reset fallido no declara recuperacion");
        Check(Param(d,"BackBufferWidth")==1280 && Param(d,"BackBufferHeight")==720,"Reset fallido conserva tamano para reintentar");
        int est=estados; n=resets; int co=cooperativas;
        bool cerrado=true; for(int i=0;i<100;i++) cerrado &= !(bool)Call(d,"BeginFrame");
        Check(cerrado,"100 cuadros perdidos no abren escena");
        Check(resets==n && cooperativas==co && estados==est,"100 cuadros fallidos no martillean Reset/estados/cooperativa");
        using(System.Drawing.Bitmap bmp=new System.Drawing.Bitmap(2,2))
        {
            object[] crea={bmp,0f,0f};
            Check((IntPtr)Call(d,"CreateTexturePow2",crea)==IntPtr.Zero,"no crea texturas despues de Reset fallido");
            object[] actualiza={new IntPtr(456),bmp,0f,0f};
            Check(!(bool)Call(d,"UpdateTexturePow2",actualiza),"no bloquea texturas despues de Reset fallido");
            Check(!(bool)Call(d,"UpdateTextureRegion",new IntPtr(456),bmp,new System.Drawing.Rectangle(0,0,2,2)),"no sube regiones despues de Reset fallido");
        }
        VenceEspera(d); resetResultado=0;
        Check((bool)Call(d,"BeginFrame") && anchoPedido==1280 && altoPedido==720,"reintento conserva resolucion y vuelve a dibujar");
        Check((bool)Call(d,"EndFrame"),"primer cuadro recuperado se presenta");
        d=Nuevo();Set(d,"lost",true);coopResultado=unchecked((int)0x88760868);
        Check(!(bool)Call(d,"BeginFrame") && resets==0,"DEVICELOST espera antes de resetear");
        d=Nuevo(); reentrada=true;
        Check((bool)Call(d,"Redimensiona",1152,864),"Reset con mensajes reentrantes termina");
        Check(Param(d,"BackBufferWidth")==1152 && Param(d,"BackBufferHeight")==864,"mensaje anidado no cambia resolucion a mitad de Reset");
        d=Nuevo();Set(d,"lost",true);coopResultado=0;
        Check((bool)Call(d,"BeginFrame") && resets==0,"controlador que vuelve directamente a S_OK recupera estados");
        Check(!(bool)Call(d,"BeginFrame") && comienzos==1,"no abre otra escena dentro de una escena");
        Call(d,"EndFrame");
        d=Nuevo();Set(d,"lost",true);coopResultado=unchecked((int)0x88760869);
        Check((bool)Call(d,"BeginFrame") && resets==1,"DEVICENOTRESET reinicia y abre escena");
        Call(d,"EndFrame");
        using(Control c=(Control)Activator.CreateInstance(P,true))
        {
            c.Size=new System.Drawing.Size(1024,768); panel=c;
            d=Nuevo(); panel=c; Set(d,"lost",true); coopResultado=unchecked((int)0x88760868);
            object s=FormatterServices.GetUninitializedObject(S);Set(s,"gpu",d);Set(c,"stars",s);
            FieldInfo grid=P.GetField("GetGrid",B);
            // Si llega a preparar contenido, el callback falla antes de poder usar el dispositivo falso.
            grid.SetValue(c,Delegate.CreateDelegate(grid.FieldType,typeof(RecuperaGpu).GetMethod("Rejilla",B)));
            Set(c,"ShowAlbum",true);
            Check(!(bool)Call(c,"RenderFrame"),"dispositivo perdido se comprueba antes de preparar contenido");
            Check(!(bool)Get(c,"renderEnCurso"),"salida anticipada libera guardia del cuadro");
            d=Nuevo(); panel=c;
            s=Activator.CreateInstance(S,B,null,new object[]{IntPtr.Zero,1024,768,4321},null);
            Set(s,"gpu",d);Call(s,"BuildStars",1024,768,4321);Set(c,"stars",s);
            c.Size=new System.Drawing.Size(1152,864); reentrada=true;
            bool corto=false;
            try {Call(c,"RenderFrame");} catch(Exception e) { corto=e.Message.Contains("preparo la rejilla"); }
            Check(corto && !(bool)Get(c,"renderEnCurso"),"excepcion al preparar contenido tambien libera guardia");
            Check((int)Get(s,"patronW")==1152 && (int)Get(s,"patronH")==864,"campo estrellado sigue el nuevo tamano");
            // Si Reset fallo antes, pp ya contiene el tamano pedido; el patron debe rehacerse al recuperarse.
            Set(s,"patronW",1024);Set(s,"patronH",768); Call(s,"Redimensiona",1152,864);
            Check((int)Get(s,"patronW")==1152 && (int)Get(s,"patronH")==864,"reconstruye patron aplazado aunque pp ya tenga el nuevo tamano");
            Set(c,"stars",null); // no liberar el puntero ficticio al cerrar el Control
        }
    }
    static int Rejilla(Array a) { throw new Exception("preparo la rejilla con dispositivo perdido"); }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Assembly a=Assembly.LoadFrom(args[0]);T=a.GetType("Rocola.D3D9Device",true);P=a.GetType("Rocola.GpuPanel",true);S=a.GetType("Rocola.GpuStarfield",true);
            Pruebas(); Console.WriteLine(checks+" comprobaciones, "+fallos+" fallos; callbacks sinteticos, no hardware XP.");
            return fallos==0?0:1;
        }
        catch(Exception e) {Console.WriteLine("FALLO "+e);return 1;}
    }
}
