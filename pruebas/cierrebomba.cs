using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NAudio.Wave;

// Regresion de un disco/codecs que tarda mas de un segundo. Solo archivos y lectores sinteticos temporales.
class CierreBomba
{
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TB, TT;
    static int bien, mal;
    static object Get(object o, string n) { FieldInfo f=o.GetType().GetField(n,T); return f!=null?f.GetValue(o):o.GetType().GetProperty(n,T).GetValue(o,null); }
    static void Set(object o,string n,object v) { o.GetType().GetField(n,T).SetValue(o,v); }
    static object Call(object o,string n,params object[] a) { return o.GetType().GetMethod(n,T).Invoke(o,a); }
    static void Check(bool ok,string n) { Console.WriteLine((ok?"OK ":"FALLO ")+n); if(ok)bien++;else mal++; }
    static bool Espera(Func<bool> f) { Stopwatch s=Stopwatch.StartNew(); while(!f()&&s.ElapsedMilliseconds<4000)Thread.Sleep(5);return f(); }
    sealed class Lenta : ISampleProvider
    {
        internal readonly ManualResetEvent Dentro=new ManualResetEvent(false), Salir=new ManualResetEvent(false), Hecha=new ManualResetEvent(false);
        internal Func<float[],int,int,int> Lee;
        internal Exception Error;
        internal int Lecturas;
        internal bool CerroAntes;
        internal Func<bool> Cerrada;
        public WaveFormat WaveFormat {get{return WaveFormat.CreateIeeeFloatWaveFormat(44100,2);}}
        public int Read(float[] b,int o,int n)
        {
            Interlocked.Increment(ref Lecturas);Dentro.Set();Salir.WaitOne();
            CerroAntes=Cerrada!=null&&Cerrada();
            try {if(Error!=null)throw Error;return Lee!=null?Lee(b,o,n):0;}
            finally {Hecha.Set();}
        }
    }
    sealed class Tono : ISampleProvider
    {
        public WaveFormat WaveFormat {get{return WaveFormat.CreateIeeeFloatWaveFormat(44100,2);}}
        public int Read(float[] b,int o,int n) {for(int i=0;i<n;i++)b[o+i]=(float)(.2*Math.Sin(i*.15));return n;}
    }
    static object Nueva(ISampleProvider s) { return Activator.CreateInstance(TB,T,null,new object[]{s},null); }
    static void Arranca(object b) {Call(b,"CuandoEsteLista",new Action(delegate{}));}
    static void UnaLectura()
    {
        Lenta s=new Lenta();object b=Nueva(s);int cerradas=0, avisos=0;
        s.Cerrada=delegate{return Thread.VolatileRead(ref cerradas)>0;};
        Call(b,"CuandoEsteLista",new Action(delegate{Interlocked.Increment(ref avisos);}));
        Check(s.Dentro.WaitOne(3000),"la lectura esta dentro del origen");
        Thread.Sleep(1200);
        Stopwatch w=Stopwatch.StartNew();((IDisposable)b).Dispose();w.Stop();
        Action cerrar=delegate{Interlocked.Increment(ref cerradas);};Call(b,"CuandoPare",cerrar);Call(b,"CuandoPare",cerrar);
        Check(w.ElapsedMilliseconds<150 && (bool)Get(b,"Viva"),"cancelar no espera al Read bloqueado: "+w.ElapsedMilliseconds+" ms");
        Check(cerradas==0,"el recurso sigue abierto mientras Read lo usa");
        Check(((ISampleProvider)b).Read(new float[20],0,20)==0,"una salida cancelada no espera ni entrega audio viejo");
        s.Salir.Set();
        Check(Espera(delegate{return cerradas==1&&!(bool)Get(b,"Viva");}),"el propio lector libera una sola vez al terminar");
        Check(!s.CerroAntes&&s.Lecturas==1&&avisos==0,"sin acceso a recurso cerrado ni arranque tardio");
        ((IDisposable)b).Dispose();Call(b,"CuandoPare",cerrar);Thread.Sleep(20);
        Check(cerradas==1,"volver a cerrar no duplica liberacion");
    }
    static void FalloYTodaviaSinHilo()
    {
        Lenta s=new Lenta();s.Error=new IOException("lectura fallo tarde");object b=Nueva(s);int soltadas=0,avisos=0;
        Call(b,"CuandoEsteLista",new Action(delegate{Interlocked.Increment(ref avisos);}));s.Dentro.WaitOne(3000);
        Call(b,"CuandoPare",new Action(delegate{Interlocked.Increment(ref soltadas);}));s.Salir.Set();
        Check(Espera(delegate{return soltadas==1;}),"un fallo tardio tambien libera su lector");
        Check(avisos==0&&((ISampleProvider)b).Read(new float[20],0,20)==0,"el fallo cancelado no arranca ni falla la cancion nueva");
        object vacia=Nueva(new Tono());int libres=0;Call(vacia,"CuandoPare",new Action(delegate{Interlocked.Increment(ref libres);}));
        Check(Espera(delegate{return libres==1;}),"libera una bomba que nunca empezo");
    }
    static bool Cerrado(ISampleProvider lector) {try{lector.Read(new float[8],0,8);return false;}catch(ObjectDisposedException){return true;}catch(NullReferenceException){return true;}}
    static void Formulario(string dir)
    {
        string wav=Path.Combine(dir,"anterior.wav");
        using(WaveFileWriter w=new WaveFileWriter(wav,WaveFormat.CreateIeeeFloatWaveFormat(44100,2))) {float[] v=new float[88200];for(int i=0;i<v.Length;i++)v[i]=(float)(.15*Math.Sin(i*.1));w.WriteSamples(v,0,v.Length);}
        object f=Codigo.FormSinEncender(TF);Set(f,"progressTimer",new System.Windows.Forms.Timer());
        object lector=Call(f,"OpenAudio",wav);ISampleProvider audio=(ISampleProvider)lector;Set(f,"reader",lector);
        Lenta lenta=new Lenta();lenta.Lee=audio.Read;
        bool fuenteCerrada=false;lenta.Cerrada=delegate{return fuenteCerrada;};
        object tap=Activator.CreateInstance(TT,T,null,new object[]{lenta,f},null);
        object b=Call(f,"ConBomba",tap);Arranca(b);Check(lenta.Dentro.WaitOne(3000),"cadena de la rocola ocupada en su ultimo Read");
        Thread.Sleep(1200);Stopwatch reloj=Stopwatch.StartNew();Call(f,"DisposePlaybackNow");reloj.Stop();
        Check(reloj.ElapsedMilliseconds<150&&Get(f,"reader")==null&&Get(f,"bomba")==null,"parar la rocola retira referencias sin esperar al disco: "+reloj.ElapsedMilliseconds+" ms");
        // Una cancion nueva publica de verdad antes de que vuelva el lector anterior.
        Set(f,"curMeasurePath",wav);Set(f,"eqMeasPath",wav);Set(f,"eqMeasSum",new double[8]);
        object nueva=Activator.CreateInstance(TT,T,null,new object[]{new Tono(),f},null);
        float[] trozo=new float[4410];((ISampleProvider)nueva).Read(trozo,0,trozo.Length);
        long muestras=(long)Get(f,"curSamples"), eq=(long)Get(f,"eqMeasN");double energia=(double)Get(f,"curSumSq");
        int niveles=((ICollection)Get(f,"levelQ")).Count;
        Check(muestras>0&&eq>0&&energia>0,"la generacion nueva mide normalmente");
        lenta.Salir.Set();Check(Espera(delegate{return !(bool)Get(b,"Viva");}),"termina el lector retirado");
        Check(!lenta.CerroAntes && lenta.Lecturas==1,"el ultimo Read anterior tuvo su archivo abierto hasta terminar");
        Check((long)Get(f,"curSamples")==muestras&&(long)Get(f,"eqMeasN")==eq&&(double)Get(f,"curSumSq")==energia&&((ICollection)Get(f,"levelQ")).Count==niveles,
              "el bloque tardio no cambia sonoridad, EQ ni pulso de la cancion nueva");
        Check(Cerrado(audio),"el archivo anterior se cierra despues del ultimo Read");fuenteCerrada=true;
        ((System.Windows.Forms.Timer)Get(f,"progressTimer")).Dispose();
    }
    static int Main(string[] args)
    {
        Assembly a=Assembly.LoadFrom(args[0]);TF=a.GetType("Rocola.Form1",true);TB=a.GetType("Rocola.BombaSonido",true);TT=TF.GetNestedType("LevelTap",T);
        string d=Path.Combine(Path.GetTempPath(),"rocola_cierrebomba_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(d);Environment.SetEnvironmentVariable("ROCOLA_DATOS",d);
        try {UnaLectura();FalloYTodaviaSinHilo();Formulario(d);}catch(Exception e){Check(false,e.ToString());}
        finally {Codigo.EsperaBitacora();try{Directory.Delete(d,true);}catch{}}
        Console.WriteLine(bien+" comprobaciones; "+mal+" fallos");return mal==0?0:1;
    }
}
