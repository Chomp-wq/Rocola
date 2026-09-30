using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NAudio.Wave;

class ContinuaFuente : ISampleProvider
{
    private readonly float[] x; private readonly WaveFormat f; private readonly int trozo; private int p;
    public ContinuaFuente(float[] x, WaveFormat f, int trozo) { this.x=x;this.f=f;this.trozo=trozo; }
    public WaveFormat WaveFormat { get { return f; } }
    public int Read(float[] b,int o,int n) { n=Math.Min(Math.Min(n,trozo),x.Length-p);if(n<=0)return 0;for(int i=0;i<n;i++)b[o+i]=x[p+i];p+=n;return n; }
}
class ContinuaReloj : IWavePlayer, IWavePosition
{
    public long Bytes; public bool Falla; public WaveFormat Formato; public Action AlConsultar;
    public WaveFormat OutputWaveFormat { get { return Formato; } }
    public long GetPosition() { if(Falla)throw new IOException("reloj no disponible");long b=Bytes;Action a=AlConsultar;if(a!=null)a();return b; }
    public void Init(IWaveProvider p){} public void Play(){} public void Pause(){} public void Stop(){} public void Dispose(){}
    public float Volume { get { return 1; } set{} }
    public PlaybackState PlaybackState { get { return PlaybackState.Playing; } }
    public event EventHandler<StoppedEventArgs> PlaybackStopped { add{} remove{} }
}
class SalidaContinua
{
    const BindingFlags T=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static Assembly a; static Type tf,tr,te,ta; static int mal,bien; static string dir;
    static void Check(bool q,string s){Console.WriteLine((q?"OK ":"FALLO ")+s);if(q)bien++;else mal++;}
    static object New(Type t,params object[] p){return Activator.CreateInstance(t,T,null,p,null);}
    static object Get(object o,string n){return o.GetType().GetField(n,T).GetValue(o);}
    static void Set(object o,string n,object v){o.GetType().GetField(n,T).SetValue(o,v);}
    static object Call(object o,string n,params object[] p){return o.GetType().GetMethod(n,T).Invoke(o,p);}
    static WaveFormat Fmt(int hz,int ch,int mask){return (WaveFormat)ta.GetMethod("Flotante",T).Invoke(null,new object[]{hz,ch,mask});}
    static bool Layout(WaveFormat f,WaveFormat g){return (bool)tr.GetMethod("SePuede",T).Invoke(null,new object[]{f,g});}
    static float[] Lee(ISampleProvider p,int chunk){List<float> y=new List<float>();float[] b=new float[chunk+7];int n;while((n=p.Read(b,3,chunk))>0)for(int i=0;i<n;i++)y.Add(b[3+i]);return y.ToArray();}
    static float[] Tono(int hz,int ch,double f,double sec){int n=(int)(sec*hz);float[] x=new float[n*ch];for(int i=0;i<n;i++)x[i*ch]=(float)(0.5*Math.Sin(2*Math.PI*f*i/hz));return x;}
    static float[] Conv(float[] x,int hzIn,int hzOut,int chunk,int skip)
    {return Lee((ISampleProvider)New(tr,new ContinuaFuente(x,Fmt(hzIn,2,3),777),Fmt(hzOut,2,3),skip),chunk);}
    static double Rms(float[] x,int hz,int c){double s=0;int n=0;for(int i=hz/5;i<hz/2;i++){double v=x[2*i+c];s+=v*v;n++;}return Math.Sqrt(s/n);}
    static double Db(double x){return 20*Math.Log10(Math.Max(1e-15,x));}
    static double Amplitud(float[] x,int hz,double f){double co=0,si=0;int desde=hz/5,hasta=hz/2;for(int i=desde;i<hasta;i++){double t=2*Math.PI*f*i/hz;co+=x[2*i]*Math.Cos(t);si+=x[2*i]*Math.Sin(t);}return 2*Math.Sqrt(co*co+si*si)/(hasta-desde);}
    static void Calidad()
    {
        foreach(int[] rates in new int[][]{new int[]{44100,48000},new int[]{48000,44100},new int[]{96000,48000}}){
            int hi=rates[0],ho=rates[1];float[] x=Tono(hi,2,1000,0.6),y=Conv(x,hi,ho,997,0);
            Check(y.Length==2*(((long)(x.Length/2)*ho+hi-1)/hi),"duracion exacta "+hi+" -> "+ho);
            Check(Math.Abs(Db(Rms(y,ho,0)/(0.5/Math.Sqrt(2))))<0.01&&Rms(y,ho,1)==0,"1 kHz plano y canales separados "+hi+" -> "+ho);
            float[] z=Conv(x,hi,ho,1,0);bool igual=z.Length==y.Length;for(int i=0;igual&&i<y.Length;i++)igual=z[i]==y[i];
            Check(igual,"lecturas de una muestra conservan cuadros y valores "+hi+" -> "+ho);
            y=Conv(Tono(hi,2,18000,0.6),hi,ho,997,0);
            double g=Db(Rms(y,ho,0)/(0.5/Math.Sqrt(2)));Console.WriteLine("  18kHz "+hi+" -> "+ho+": "+g.ToString("0.000")+" dB");
            Check(Math.Abs(g)<0.1,"banda hasta 18 kHz conservada");
            y=Conv(Tono(hi,2,20000,0.6),hi,ho,997,0);
            g=Db(Rms(y,ho,0)/(0.5/Math.Sqrt(2)));Console.WriteLine("  20kHz "+hi+" -> "+ho+": "+g.ToString("0.000")+" dB");
            Check(Math.Abs(g)<0.1,"20 kHz conservados sin rebajar calidad");
            if(hi%ho!=0){ // a tasa entera la supuesta imagen se pliega exactamente sobre el tono correcto
                y=Conv(x,hi,ho,997,0);double image=Db(Amplitud(y,ho,hi-1000)/0.5);
                Console.WriteLine("  imagen de 1kHz: "+image.ToString("0.0")+" dB");Check(image< -80,"imagenes de interpolacion suprimidas");
            }
            float[] impulse=new float[259*2];impulse[258*2]=0.5f;float[] yi=Conv(impulse,hi,ho,7,0);
            int peak=0;for(int k=1;k<yi.Length/2;k++)if(Math.Abs(yi[2*k])>Math.Abs(yi[2*peak]))peak=k;
            Check(Math.Abs(peak-258.0*ho/hi)<=1&&yi.Length==2*((259L*ho+hi-1)/hi),"impulso final en su tiempo y duracion exacta");
            if(hi>ho){
                y=Conv(Tono(hi,2,ho/2.0+500,0.6),hi,ho,997,0);
                double alias=Db(Rms(y,ho,0)/(0.5/Math.Sqrt(2)));Console.WriteLine("  plegado "+alias.ToString("0.0")+" dB");
                Check(alias< -80,"antialias antes de bajar tasa");
            }
        }
        float[] burst=new float[44101*2];for(int i=43700;i<44101;i++){burst[2*i]=(float)(.5*Math.Sin(2*Math.PI*1000*(i-43700)/44100));}
        float[] outb=Conv(burst,44100,48000,1001,0);double ei=0,eo=0;foreach(float v in burst)ei+=v*v;foreach(float v in outb)eo+=v*v;
        Check(Math.Abs(eo/48000/(ei/44100)-1)<0.01,"ultima rafaga completa sin agregar padding");
        float[] plain=Tono(44100,2,1000,.6),pad=new float[plain.Length+222];Array.Copy(plain,0,pad,222,plain.Length);
        float[] yp=Conv(pad,44100,48000,999,111),yn=Conv(plain,44100,48000,999,0);bool same=yp.Length==yn.Length;for(int i=0;same&&i<yp.Length;i++)same=yp[i]==yn[i];
        Check(same,"cebado origen descartado antes de convertir");
        Check(!Layout(Fmt(48000,6,0x3f),Fmt(44100,6,0x60f))&&!Layout(Fmt(48000,6,0),Fmt(44100,6,0))&&!Layout(Fmt(48000,1,4),Fmt(44100,2,3)),
            "layouts distintos, desconocidos y mono/estereo se reconectan: sin mezclar ni inventar");
        long low=(long)tr.GetMethod("PresupuestoTrabajo",T).Invoke(null,new object[]{2}),high=(long)tr.GetMethod("PresupuestoTrabajo",T).Invoke(null,new object[]{16});
        bool fitsLow=(bool)tr.GetMethod("DentroPresupuesto",T).Invoke(null,new object[]{Fmt(48000,2,3),Fmt(192000,2,3),2});
        bool fitsHigh=(bool)tr.GetMethod("DentroPresupuesto",T).Invoke(null,new object[]{Fmt(48000,2,3),Fmt(192000,2,3),16});
        Check(low==40000000L&&high==320000000L&&!fitsLow&&fitsHigh,"presupuesto de conversion conserva2C2T y crece sin techo artificial en equipos mayores");
        Check((long)tr.GetMethod("PresupuestoTrabajo",T).Invoke(null,new object[]{32})==640000000L,"32 procesadores siguen aumentando presupuesto, sin cap de ocho");
        foreach(int canales in new int[]{6,8})foreach(int procesadores in new int[]{2,16,32}){
            int mascara=canales==6?0x3f:0x63f;
            bool cabe=(bool)tr.GetMethod("DentroPresupuesto",T).Invoke(null,new object[]{Fmt(96000,canales,mascara),Fmt(192000,canales,mascara),procesadores});
            Check(cabe==(procesadores>2),"96->192k "+canales+"ch: politica conservadora2C y permite16/32 ("+procesadores+")");
        }
        object next=new object(),enc=New(te,new ContinuaFuente(new float[20],Fmt(44100,2,3),20),new object());
        Call(enc,"Prepara",new ContinuaFuente(new float[200],Fmt(44100,2,3),200),0,next);float[] rel=new float[64];((ISampleProvider)enc).Read(rel,0,rel.Length);
        object early=Call(enc,"TomaRelevoPorCuadro",9L),due=Call(enc,"TomaRelevoPorCuadro",10L);
        Check(early==null&&due!=null&&object.ReferenceEquals(Get(due,"Ficha"),next)&&Call(enc,"TomaRelevoPorCuadro",100L)==null,"relevo por reloj espera cuadro audible exacto y se entrega una vez");
        enc=New(te,new ContinuaFuente(new float[60],WaveFormat.CreateIeeeFloatWaveFormat(44100,6),60),new object());
        Check(!(bool)Call(enc,"Prepara",new ContinuaFuente(new float[60],WaveFormat.CreateIeeeFloatWaveFormat(44100,6),60),0,new object()),"relevo directo desconocido no presupone layout por seis canales");
        Stopwatch sw=Stopwatch.StartNew();Conv(Tono(44100,2,1000,10),44100,48000,8192,0);Console.WriteLine("  conversion sinc: "+(sw.Elapsed.TotalMilliseconds/10).ToString("0.00")+" ms por segundo en ESTE PC (incluye reserva/lecturas)");
    }
    static void TablaCapturada()
    {
        object gate=tr.GetField("tablaCerrojo",T).GetValue(null);
        FieldInfo hi=tr.GetField("tablaIn",T),ho=tr.GetField("tablaOut",T),lista=tr.GetField("tablaLista",T),warm=tr.GetField("calentando",T);
        object oldHi=hi.GetValue(null),oldHo=ho.GetValue(null),oldList=lista.GetValue(null),oldWarm=warm.GetValue(null);
        double[] tablaA=(double[])tr.GetMethod("CreaFiltro",T).Invoke(null,new object[]{44100,48000});
        double[] tablaB=(double[])tr.GetMethod("CreaFiltro",T).Invoke(null,new object[]{48000,44100});
        WaveFormat bIn=Fmt(48000,2,3),bOut=Fmt(44100,2,3);
        try{
            // Interleaving real: B ya esta en cache; otro calentador esta preparando A.
            lock(gate){hi.SetValue(null,48000);ho.SetValue(null,44100);lista.SetValue(null,tablaB);warm.SetValue(null,true);}
            double[] capturada=(double[])tr.GetMethod("CapturaTabla",T).Invoke(null,new object[]{bIn,bOut});
            Check(object.ReferenceEquals(capturada,tablaB),"captura tablaB bajo lock aun con otro calentadorA activo");
            // A termina justo antes del constructor de B y sustituye la cache.
            lock(gate){hi.SetValue(null,44100);ho.SetValue(null,48000);lista.SetValue(null,tablaA);warm.SetValue(null,false);}
            ContinuaFuente src=new ContinuaFuente(Tono(48000,2,1000,.05),bIn,777);
            object seguro=New(tr,src,bOut,0,capturada);
            Check(object.ReferenceEquals(Get(seguro,"filtro"),tablaB)&&object.ReferenceEquals(lista.GetValue(null),tablaA)&&(int)Get(src,"p")==0,
                "publicarA entre captura y ctorB conserva referenciaB: sin calcular tabla ni leer origen en UI");
            float[] y=Lee((ISampleProvider)seguro,997),refB=Conv(Tono(48000,2,1000,.05),48000,44100,997,0);
            bool same=y.Length==refB.Length;for(int k=0;same&&k<y.Length;k++)same=y[k]==refB[k];
            Check(same,"tabla capturada despues de reemplazo produce PCM identico al constructor directo");
            bool rechaza=false;try{New(tr,new ContinuaFuente(new float[32],bIn,32),bOut,0,(double[])null);}catch(Exception e){rechaza=(e.InnerException??e) is ArgumentException;}
            Check(rechaza&&object.ReferenceEquals(lista.GetValue(null),tablaA),"constructor de produccion con tabla ausente rechaza, nunca cae al calculo sincrono");
            lock(gate)warm.SetValue(null,true);
            Check(tr.GetMethod("CapturaTabla",T).Invoke(null,new object[]{bIn,bOut})==null&&object.ReferenceEquals(lista.GetValue(null),tablaA),
                "cachemiss mientras calienta otra tasa espera siguiente tick sin consumir la tabla ni el lector");
            MethodInfo prepara=tf.GetMethod("PreparaSiguienteSinPausa",T);bool usa4=false,usa3=false;
            foreach(Codigo.Paso paso in Codigo.Pasos(prepara))if(paso.Op==0x73){MethodBase c=prepara.Module.ResolveMethod(paso.Numero);if(c.DeclaringType==tr){usa4|=c.GetParameters().Length==4;usa3|=c.GetParameters().Length==3;}}
            Check(usa4&&!usa3,"UI solamente construye conversion con referencia capturada, nunca ctor sincrono de fixtures");
        }finally{lock(gate){hi.SetValue(null,oldHi);ho.SetValue(null,oldHo);lista.SetValue(null,oldList);warm.SetValue(null,oldWarm);}}
    }
    static void CosteMulticanal()
    {
        foreach(int canales in new int[]{6,8})foreach(int[] tasas in new int[][]{new int[]{96000,192000},new int[]{192000,96000}}){
            int hi=tasas[0],ho=tasas[1],mascara=canales==6?0x3f:0x63f;
            if(!Layout(Fmt(hi,canales,mascara),Fmt(ho,canales,mascara))){Console.WriteLine("NO MEDIDO coste192k "+canales+"ch: presupuesto de ESTE PC no permite conversion");continue;}
            const int segundos=3;float[] x=Tono(hi,canales,1000,segundos),b=new float[8192*canales];
            Stopwatch reloj=Stopwatch.StartNew();ISampleProvider p=(ISampleProvider)New(tr,new ContinuaFuente(x,Fmt(hi,canales,mascara),4096*canales),Fmt(ho,canales,mascara),0);
            long n=0;int got;bool finito=true;while((got=p.Read(b,0,b.Length))>0){n+=got;for(int k=0;k<got;k++)if(float.IsNaN(b[k])||float.IsInfinity(b[k]))finito=false;}
            reloj.Stop();Console.WriteLine("  coste sinc "+hi+"->"+ho+" "+canales+"ch: "+(reloj.Elapsed.TotalMilliseconds/segundos).ToString("0.00")+" ms/s en ESTE PC, un hilo (incluye tabla/reserva; buffers de lectura reutilizados)");
            Check(finito&&n==(long)ho*canales*segundos,"conversion192k multicanal conserva todos los cuadros finitos sin dispositivo");
        }
    }
    static string Wav(string name,int frames,int hz){
        string p=Path.Combine(dir,name);using(WaveFileWriter w=new WaveFileWriter(p,Fmt(hz,2,3))){float[] b=new float[frames*2];for(int i=0;i<frames;i++)b[2*i]=b[2*i+1]=i/(float)(frames+1);w.WriteSamples(b,0,b.Length);}return p;
    }
    static object Open(object form,string p){return Call(form,"OpenAudio",p);}
    static void Recorte()
    {
        object form=Codigo.FormSinEncender(tf);Set(form,"saltaSilencios",false);
        int hz=44100,raw=600,start=128,music=257;
        object source=Open(form,Wav("seek.wav",raw,hz));Type fr=tf.GetNestedType("FuenteSinRelleno",T);
        object cut=New(fr,source,(long)start,(long)music);MethodInfo seek=fr.GetMethod("Salta",T);ISampleProvider sp=(ISampleProvider)cut;
        foreach(int p in new int[]{101,3,256,257,-9,1000,0}){
            seek.Invoke(cut,new object[]{TimeSpan.FromTicks((long)Math.Round((double)p*TimeSpan.TicksPerSecond/hz))});
            float[] y=Lee(sp,8);int wanted=Math.Max(0,Math.Min(music,p));bool q=y.Length==(music-wanted)*2;
            for(int i=0;q&&i<y.Length;i++)q=y[i]==(start+wanted+i/2)/(float)(raw+1);
            Check(q,"seek musical "+p+" conserva muestras correctas y final LAME");
            TimeSpan now=(TimeSpan)fr.GetProperty("CurrentTime").GetValue(cut,null),total=(TimeSpan)fr.GetProperty("TotalTime").GetValue(cut,null);
            Check(Math.Abs(now.TotalSeconds-(double)music/hz)<1e-7&&Math.Abs(total.TotalSeconds-(double)music/hz)<1e-7,"tiempos musicales exactos sin ms de redondeo");
        }
        ((IDisposable)cut).Dispose();
        // La misma ruta de Salta usada por el salto automatico de un principio de mas de 20 segundos.
        int frames=hz*24+600;source=Open(form,Wav("silencio-seek.wav",frames,hz));cut=New(fr,source,1105L,(long)(frames-2000));Type plan=a.GetType("Rocola.PlanSilencios");
        object pl=New(plan);Set(pl,"Inicio",21L*hz);Type fs=tf.GetNestedType("FuenteSinSilencios",T);object silent=New(fs,cut,pl);
        float[] ys=Lee((ISampleProvider)silent,998);int expected=(frames-2000)-21*hz;
        Check(ys.Length==expected*2&&ys[0]==(1105+21*hz)/(float)(frames+1)&&ys[ys.Length-1]==(1105+frames-2001)/(float)(frames+1),
            "salto automatico largo conserva coordenadas y padding final");
        ((IDisposable)silent).Dispose();
        source=Open(form,Wav("seek-silencios.wav",600,hz));pl=New(plan);Set(pl,"Fin",257L);silent=New(fs,source,pl);
        foreach(int p in new int[]{3,101,256,257,-9,1000}){
            Call(silent,"Salta",TimeSpan.FromTicks((long)Math.Round((double)p*TimeSpan.TicksPerSecond/hz)));
            int q=Math.Max(0,Math.Min(257,p));TimeSpan actual=(TimeSpan)fs.GetProperty("CurrentTime").GetValue(silent,null);
            float[] data=Lee((ISampleProvider)silent,998);bool good=data.Length==(257-q)*2;
            for(int i=0;good&&i<data.Length;i++)good=data[i]==(q+i/2)/(float)601;
            Check(good&&Math.Abs(actual.TotalSeconds-(double)q/hz)<1e-7,"seek recorte silencios cuadro"+p+" coincide decoder,limite y coordenadas exactas");
        }
        ((IDisposable)silent).Dispose();
    }
    static void Mp3Real()
    {
        object form=Codigo.FormSinEncender(tf);Set(form,"saltaSilencios",false);
        foreach(string file in new string[]{"relleno_p0.mp3","relleno_48k.mp3","relleno_mono.mp3"}){
            string path=Path.Combine(Environment.CurrentDirectory,file);if(!File.Exists(path))path=Path.Combine(Path.Combine(Environment.CurrentDirectory,"pruebas"),file);
            if(!File.Exists(path)){Console.WriteLine("NO MEDIDO seekMP3: falta fixture "+file);continue;}
            object src=Open(form,path);Type st=src.GetType();ISampleProvider sp=(ISampleProvider)src;int hz=sp.WaveFormat.SampleRate,ch=sp.WaveFormat.Channels;float[] full=Lee(sp,8192);int music=full.Length/ch;
            Check(st.Name=="FuenteSinRelleno","MP3 real LAME abierto con recorte: "+file);
            foreach(int p in new int[]{3,101,256,music-1,music,-9,music+20}){
                Call(src,"Salta",TimeSpan.FromTicks((long)Math.Round((double)p*TimeSpan.TicksPerSecond/hz)));
                TimeSpan actual=(TimeSpan)st.GetProperty("CurrentTime").GetValue(src,null);float[] remain=Lee(sp,8192);int q=Math.Max(0,Math.Min(music,p));
                Check(remain.Length==(music-q)*ch&&Math.Abs(actual.TotalSeconds-(double)q/hz)<1e-7,"MP3seekreal "+file+" cuadro"+p+" conserva cantidad y padding final");
                double delta=0;int tail=Math.Min(1024,remain.Length);for(int i=0;i<tail;i++)delta=Math.Max(delta,Math.Abs(remain[remain.Length-tail+i]-full[full.Length-tail+i]));
                Check(delta<0.0002,"MP3seekreal "+file+" cuadro"+p+" final coincide referencia decoder sin desplazar muestras (max "+delta.ToString("0.000000")+")");
            }
            ((IDisposable)src).Dispose();
        }
    }
    static void Reloj()
    {
        object form=Codigo.FormSinEncender(tf);Set(form,"saltaSilencios",false);
        object source=Open(form,Wav("clock.wav",44100*12,44100));Call(source,"Salta",TimeSpan.FromSeconds(10));
        object seq=New(te,new ContinuaFuente(Tono(44100,2,1000,10),Fmt(44100,2,3),8192),new object());
        Lee((ISampleProvider)seq,8192);Set(form,"reader",source);Set(form,"secuencia",seq);
        ContinuaReloj clock=new ContinuaReloj();clock.Formato=Fmt(48000,2,3);clock.Bytes=8L*clock.Formato.AverageBytesPerSecond;Set(form,"output",clock);
        FieldInfo delay=tf.GetField("audioVisualDelayMs",T);if(delay!=null)delay.SetValue(form,850);
        Check(Math.Abs((double)Call(form,"PosicionActual")-8)<1e-7,"recuperacion usa reloj real con remuestreo y dos segundos pendientes");
        clock.Falla=true;Check(Math.Abs((double)Call(form,"PosicionActual")-9.15)<1e-7,"driver sin reloj conserva respaldo XP");
        clock.Falla=false;clock.Bytes=100L*clock.Formato.AverageBytesPerSecond;
        Check(Math.Abs((double)Call(form,"PosicionActual")-9.15)<1e-7,"reloj fuera de rango no produce saltos");
        Type tc=tf.GetNestedType("CadenaCancion",T);object cadena=New(tc);Set(cadena,"Lector",source);Set(cadena,"InicioMusica",2.0);Set(cadena,"CuadroComienzo",44100L);Set(cadena,"CebadoInicial",4410);
        Set(form,"cadenaSonando",cadena);seq=New(te,new ContinuaFuente(Tono(44100,2,1000,10),Fmt(44100,2,3),8192),cadena);Lee((ISampleProvider)seq,8192);Set(form,"secuencia",seq);
        clock.Bytes=8L*clock.Formato.AverageBytesPerSecond;
        Check(Math.Abs((double)Call(form,"PosicionActual")-8.9)<1e-7,"reloj ignora readahead lector y resta inicio/cebado de cadena");
        Set(form,"retardoFinalReloj",0.004);Check(Math.Abs((double)Call(form,"PosicionActual")-8.896)<1e-7,"reloj descuenta limitador final posterior a remuestreo de dispositivo");Set(form,"retardoFinalReloj",0.0);
        Set(cadena,"CuadroComienzo",9L*44100);Set(cadena,"CebadoInicial",0);Check(Math.Abs((double)Call(form,"PosicionActual")-2)<1e-7,"cadena aun no oida no avanza ni repite readahead");
        // Mapa inverso: dos segundos entregados pueden haber atravesado un hueco de cuatro segundos.
        Type plan=a.GetType("Rocola.PlanSilencios"),fs=tf.GetNestedType("FuenteSinSilencios",T);object pl=New(plan);Set(pl,"Inicio",44100L);Set(pl,"HuecoDesde",3L*44100);Set(pl,"HuecoHasta",7L*44100);
        Call(source,"Salta",TimeSpan.Zero);object silent=New(fs,source,pl);Set(cadena,"Lector",silent);Set(cadena,"CuadroComienzo",0L);Set(seq,"muestras",3L*44100*2);clock.Bytes=2L*clock.Formato.AverageBytesPerSecond;
        Check(Math.Abs((double)Call(form,"PosicionActual")-7)<1e-7,"reloj atraviesa hueco de silencio usando coordenadas musicales");
        Lee((ISampleProvider)silent,998);Call(silent,"Salta",TimeSpan.FromSeconds(2));clock.Bytes=1L*clock.Formato.AverageBytesPerSecond;
        Check(Math.Abs((double)Call(form,"PosicionActual")-7)<1e-7,"retomar antes de hueco restablece mapa y salto automatico");
        float[] resumed=Lee((ISampleProvider)silent,998);Check(resumed.Length==(12-2-4)*44100*2,"salto de hueco vuelve a aplicarse al retomar, sin leer silencio");
        ((IDisposable)silent).Dispose();
    }
    static void RelojIntercalado()
    {
        object form=Codigo.FormSinEncender(tf);Set(form,"saltaSilencios",false);Set(form,"credits",7);
        object src=Open(form,Wav("clock-race.wav",44100*12,44100));Call(src,"Salta",TimeSpan.FromSeconds(10));Set(form,"reader",src);
        Type tc=tf.GetNestedType("CadenaCancion",T);object cad=New(tc);Set(cad,"Lector",src);Set(cad,"InicioMusica",2.0);Set(cad,"CuadroComienzo",44100L);Set(cad,"CebadoInicial",4410);Set(form,"cadenaSonando",cad);
        object seq=New(te,new ContinuaFuente(new float[44100*2*2],Fmt(44100,2,3),8192),cad),siguiente=new object();
        Call(seq,"Prepara",new ContinuaFuente(new float[44100*4*2],Fmt(44100,2,3),8192),0,siguiente);Set(form,"secuencia",seq);
        float[] bloque=new float[44100*2*2];((ISampleProvider)seq).Read(bloque,0,bloque.Length); // A ya entrego dos segundos; B aun no empezo.
        ContinuaReloj clock=new ContinuaReloj();clock.Formato=Fmt(48000,2,3);clock.Bytes=(long)(1.5*clock.Formato.AverageBytesPerSecond);
        clock.AlConsultar=delegate{((ISampleProvider)seq).Read(bloque,0,44100*2);};Set(form,"output",clock);
        // GetPosition hace avanzar Read DESPUES de la instantanea de entregados, pero el aparato sigue en1,5s.
        object[] instantanea=new object[]{0.0,0.0};bool valida=(bool)tf.GetMethod("PendientePorReloj",T).Invoke(form,instantanea);
        Check(valida&&Math.Abs((double)instantanea[0]-.5)<1e-7&&Math.Abs((double)instantanea[1]-1.5)<1e-7
              &&(long)te.GetProperty("CuadrosEntregados",T).GetValue(seq,null)==3L*44100,
            "GetPosition intercala Read: snapshot valida0,5s pendientes/1,5s oidos aunque bomba ya entrego3s");
        Check(Math.Abs((double)Call(form,"PosicionActual")-2.4)<1e-7,"avance bomba durante reloj no adelanta posicion musical ni recuperacion");
        Call(form,"RevisaRelevo",Environment.TickCount);
        Check((bool)te.GetProperty("HayRelevoPendiente",T).GetValue(seq,null)&&(int)Get(form,"credits")==7,
            "relevo UI conserva B pendiente: bomba avanzo, pero reloj aun no oyo su cuadro2s");
        IOException fallo=new IOException("salida fallo de mentira");object parada=Call(form,"RecogeAlParar",new StoppedEventArgs(fallo));
        Check(((StoppedEventArgs)parada).Exception==fallo&&(bool)te.GetProperty("HayRelevoPendiente",T).GetValue(seq,null)&&(int)Get(form,"credits")==7,
            "al fallar salida Read concurrente no da por oida B ni consume relevo o credito");
        foreach(string metodo in new string[]{"PosicionPorReloj","RevisaRelevo","RecogeAlParar"})
            Check(!Codigo.Nombres(tf.GetMethod(metodo,T)).Contains("get_CuadrosEntregados"),metodo+" usa snapshot del reloj sin segunda lectura entregados(t1)");
        clock.AlConsultar=null;clock.Bytes=2L*clock.Formato.AverageBytesPerSecond;instantanea=new object[]{0.0,0.0};
        valida=(bool)tf.GetMethod("PendientePorReloj",T).Invoke(form,instantanea);
        object relevo=valida?Call(seq,"TomaRelevoPorCuadro",(long)Math.Floor((double)instantanea[1]*44100)):null;
        Check(relevo!=null&&object.ReferenceEquals(Get(relevo,"Ficha"),siguiente)&&Call(seq,"TomaRelevoPorCuadro",10L*44100)==null,
            "relevo se recoge una vez cuando el mismo snapshot del driver alcanza su cuadro exacto");
        ((IDisposable)src).Dispose();
    }
    static int Main(string[] args){
        a=Assembly.LoadFrom(args[0]);tf=a.GetType("Rocola.Form1");tr=a.GetType("Rocola.RemuestreoContinuo");te=a.GetType("Rocola.Encadenado");ta=a.GetType("Rocola.FormatoAudio");
        dir=Path.Combine(Path.GetTempPath(),"rocola-continuidad-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS",dir);
        try{Calidad();TablaCapturada();CosteMulticanal();Recorte();Mp3Real();Reloj();RelojIntercalado();}catch(Exception e){Check(false,(e.InnerException??e).ToString());}
        Console.WriteLine("RESULTADO "+bien+" OK, "+mal+" fallos. Solo muestras sinteticas; sin dispositivo ni sonido.");return mal==0?0:1;
    }
}
