using System;
using System.Collections.Generic;
using System.Reflection;
using NAudio.Wave;

// Solo PCM sintetico y reflexion: no abre formulario, salida, musica ni modelos instalados.
sealed class FuenteOriginalPrueba : ISampleProvider
{
    readonly float[] datos; readonly WaveFormat formato; readonly int[] trozos; int pos, turno;
    internal FuenteOriginalPrueba(float[] x, WaveFormat f, int[] t) { datos=x; formato=f; trozos=t; }
    public WaveFormat WaveFormat { get { return formato; } }
    public int Read(float[] b,int o,int n) {
        if(trozos!=null)n=Math.Min(n,trozos[turno++%trozos.Length]);
        n=Math.Min(n,datos.Length-pos); if(n<=0)return 0;
        Array.Copy(datos,pos,b,o,n);pos+=n;return n;
    }
}
class DspOriginal
{
    const BindingFlags B=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static Type form, original, spectral, tap, bajo, formatos; static int checks;
    static void Check(bool ok,string why) { checks++; if(!ok)throw new Exception(why); Console.WriteLine("OK "+why); }
    static object Get(object o,string n) { return o.GetType().GetField(n,B).GetValue(o); }
    static void Set(object o,string n,object v) { o.GetType().GetField(n,B).SetValue(o,v); }
    static object Crea(Type t,params object[] a) { return Activator.CreateInstance(t,B,null,a,null); }
    static WaveFormat Float(int ch,int mask) { return (WaveFormat)formatos.GetMethod("Flotante",B).Invoke(null,new object[]{44100,ch,mask}); }
    static int Mask(WaveFormat f) { return (int)formatos.GetMethod("Mascara",B).Invoke(null,new object[]{f}); }
    static object Sbr(ISampleProvider src,bool activo) {
        foreach(ConstructorInfo c in spectral.GetConstructors(B)) {
            ParameterInfo[] p=c.GetParameters();
            if(p.Length>0&&p[0].ParameterType==typeof(ISampleProvider)) {
                object s=c.Invoke(p.Length==1?new object[]{src}:new object[]{src,2048});
                Set(s,"Active",activo);return s;
            }
        }throw new Exception("No constructor SpectralTap");
    }
    static float[] Lee(ISampleProvider p) {
        List<float> y=new List<float>();float[] b=new float[1031];int n;
        while((n=p.Read(b,2,b.Length-4))>0)for(int i=0;i<n;i++)y.Add(b[2+i]);
        return y.ToArray();
    }
    static bool Finito(Array a) {
        if(a==null)return true;foreach(object o in a) {
            Array sub=o as Array;if(sub!=null){if(!Finito(sub))return false;continue;}
            double v=Convert.ToDouble(o);if(double.IsNaN(v)||double.IsInfinity(v))return false;
        }return true;
    }
    sealed class Resultado { public object Owner, Sbr; public float[] Y; }
    static Resultado Ruta(float[] x,bool activo,int[] trozos) {
        object owner=Codigo.FormSinEncender(form);
        Set(owner,"auroraAmount",0f);Set(owner,"curMeasurePath","synthetic");Set(owner,"eqMeasPath","synthetic");Set(owner,"eqMeasSum",new double[8]);
        object raw=Crea(original,new FuenteOriginalPrueba(x,Float(2,3),trozos),owner);
        object s=Sbr((ISampleProvider)raw,activo), l=Crea(tap,s,owner);
        Set(raw,"Tap",l);Set(l,"MideOriginal",true);Set(owner,"tapMedido",l);
        Resultado r=new Resultado();r.Owner=owner;r.Sbr=s;r.Y=Lee((ISampleProvider)l);return r;
    }
    static float[] Senal(int ch) {
        int frames=3*44100;float[] x=new float[frames*ch];Random random=new Random(42);double[] phase=new double[160];
        for(int k=0;k<phase.Length;k++)phase[k]=random.NextDouble()*2*Math.PI;
        for(int f=0;f<frames;f++) {
            double v=.03*Math.Sin(2*Math.PI*800*f/44100.0);
            for(int k=0;k<phase.Length;k++)v+=.004*Math.Sin(2*Math.PI*(6000+k*37)*f/44100.0+phase[k]);
            for(int c=0;c<ch;c++)x[f*ch+c]=(float)(v*(1-.08*c));
        }return x;
    }
    static double Error(float[] a,float[] b) {
        if(a.Length!=b.Length)return double.PositiveInfinity;double e=0;
        for(int i=0;i<a.Length;i++)e=Math.Max(e,Math.Abs(a[i]-b[i]));return e;
    }
    static void Entrada() {
        float[] x=Senal(2);Resultado apagado=Ruta(x,false,null), activo=Ruta(x,true,null);
        Check((long)Get(activo.Owner,"curSamples")==x.Length && (long)Get(activo.Owner,"eqMeasN")==x.Length/2,
              "Original se mide una vez; no incluye retraso STFT, realce ni cola");
        Check((double)Get(activo.Owner,"curSumSq")== (double)Get(apagado.Owner,"curSumSq"),"Energia K original exacta con DSEE encendido/apagado");
        double[] a=(double[])Get(activo.Owner,"eqMeasSum"),b=(double[])Get(apagado.Owner,"eqMeasSum");
        bool exacto=true;for(int i=0;i<8;i++)if(a[i]!=b[i])exacto=false;
        Check(exacto,"Ocho bandas originales exactas con DSEE encendido/apagado");
        Check(Error(activo.Y,apagado.Y)>.001,"La comparacion no es vacua: DSEE cambia realmente el PCM");
        float[] basura=(float[])x.Clone(),limpio=(float[])x.Clone();
        int[] puntos={2*44100,2*44100+137,2*44100+519};
        float[] valores={float.NaN,float.PositiveInfinity,1e30f};
        for(int i=0;i<puntos.Length;i++){basura[puntos[i]]=valores[i];limpio[puntos[i]]=0;}
        Resultado corrompido=Ruta(basura,true,new int[]{1,7,509,2}),referencia=Ruta(limpio,true,new int[]{1,7,509,2});
        Check(Finito(corrompido.Y)&&Finito((Array)Get(corrompido.Sbr,"numSuave")),"NaN/Inf/absurdos no contaminan memoria DSEE ni PCM");
        Check(Error(corrompido.Y,referencia.Y)==0,"Una muestra rota equivale a cero local, no a 70 ms de mute");
        Check(Finito((Array)Get(corrompido.Owner,"kwSt"))&&Finito((Array)Get(corrompido.Owner,"eqMeasSt")),"Memorias de medicion originales finitas");
        float[] directo=Lee((ISampleProvider)Sbr(new FuenteOriginalPrueba(basura,Float(2,3),null),true));
        Check(Finito(directo),"SpectralTap directo tambien sanea antes de FFT");
        List<string> calls=Codigo.Llamadas(form.GetMethod("ArmaCadena",B));
        int ir=-1,id=-1,isbr=-1;
        for(int i=0;i<calls.Count;i++){if(calls[i]=="FuenteOriginal..ctor")ir=i;if(calls[i]=="DeclipProvider..ctor")id=i;if(calls[i]=="SpectralTap..ctor")isbr=i;}
        Check(ir>=0&&id>ir&&isbr>ir,"Cadena real valida/mide fuente antes de Declip y DSEE");
    }
    static void Mascara() {
        foreach(int[] layout in new int[][]{new int[]{1,8},new int[]{2,12},new int[]{6,0x60F}}) {
            int ch=layout[0],mask=layout[1];WaveFormat f=Float(ch,mask);
            Check(Mask(f)==mask,"Float conserva mascara "+mask+" con "+ch+" canales");
            WaveFormat pcm=(WaveFormat)formatos.GetMethod("Pcm",B).Invoke(null,new object[]{44100,16,ch,mask});
            Check(Mask(pcm)==mask && pcm.BitsPerSample==16,"PCM16 conserva mascara "+mask);
            float[] x=Senal(ch);int lfe=ch==6?3:ch-1;
            object owner=Codigo.FormSinEncender(form);Set(owner,"auroraAmount",0f);
            object l=Crea(tap,new FuenteOriginalPrueba(x,f,null),owner);
            Set(l,"Excite",1f);tap.GetProperty("EqDb",B).SetValue(l,new float[]{3,2,1,2,3,2,1,3},null);
            float[] y=Lee((ISampleProvider)l);bool intacto=y.Length==x.Length+31*ch;
            for(int i=0;i<y.Length/ch&&intacto;i++)if(y[i*ch+lfe]!=(i<31?0:x[(i-31)*ch+lfe]))intacto=false;
            Check(intacto,"EQ/realce omiten LFE; seco y cola exactos para mascara "+mask);
            y=Lee((ISampleProvider)Crea(bajo,new FuenteOriginalPrueba(x,f,null),.8f));intacto=true;
            for(int i=0;i<x.Length/ch;i++)if(y[i*ch+lfe]!=x[i*ch+lfe])intacto=false;
            Check(intacto,"DeepBass no inyecta armonicos en LFE "+mask);
            y=Lee((ISampleProvider)Sbr(new FuenteOriginalPrueba(x,f,null),true));double e=0;
            for(int i=0;i<x.Length/ch;i++)e=Math.Max(e,Math.Abs(y[i*ch+lfe]-x[i*ch+lfe]));
            Check(y.Length==x.Length&&e<2e-6,"DSEE conserva LFE sin reconstruir (error "+e.ToString("R")+")");
            object d=Crea(form.GetNestedType("DitherWaveProvider16",B),new FuenteOriginalPrueba(x,f,null));
            Check(Mask(((IWaveProvider)d).WaveFormat)==mask,"Salida dither conserva mascara "+mask);
        }
        float[] desconocido=Senal(6);
        float[] z=Lee((ISampleProvider)Crea(bajo,new FuenteOriginalPrueba(desconocido,Float(6,0),null),.8f));
        Check(Error(desconocido,z)==0,"DeepBass desconoce layout multicanal: no mezcla ni inventa LFE");
        WaveFormatExtensible invalido=new WaveFormatExtensible(44100,32,2);
        typeof(WaveFormatExtensible).GetField("dwChannelMask",B).SetValue(invalido,0);
        Check(Mask(invalido)==0,"Extensible con mascara invalida no se vuelve stereo supuesto");
        Check(Mask(Float(2,0))==0,"Float conserva desconocida explicita tambien con 2 canales");
        Check(Mask((WaveFormat)formatos.GetMethod("Pcm",B).Invoke(null,new object[]{44100,16,2,0}))==0,
              "PCM16 conserva desconocida explicita tambien con 2 canales");
    }
    static void Cosecha() {
        string dir=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"rocola-original-cosecha-"+Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        FieldInfo carpeta=form.GetField("carpetaDatos",B);object antes=carpeta.GetValue(null);carpeta.SetValue(null,dir);
        try {
            foreach(bool asimetrico in new bool[]{false,true}) {
                string file=System.IO.Path.Combine(dir,asimetrico?"leftcut-rightbright.wav":"bothbright.wav");
                float[] low=Senal(2),data=new float[12*44100*2];Random random=new Random(85);
                for(int i=0;i<data.Length/2;i++){data[i*2]=asimetrico?low[(i%(low.Length/2))*2]:(float)(.12*(random.NextDouble()-.5));data[i*2+1]=(float)(.12*(random.NextDouble()-.5));}
                using(WaveFileWriter w=new WaveFileWriter(file,Float(2,3)))w.WriteSamples(data,0,data.Length);
                object fuente = Crea(form.GetNestedType("FileAudioSource",B),file);
                try {
                    TimeSpan tiempo32=TimeSpan.FromTicks((long)Math.Round(32.0*TimeSpan.TicksPerSecond/44100));
                    fuente.GetType().GetMethod("Salta",B).Invoke(fuente,new object[]{tiempo32});
                    TimeSpan actual=(TimeSpan)fuente.GetType().GetProperty("CurrentTime",B).GetValue(fuente,null);
                    TimeSpan total=(TimeSpan)fuente.GetType().GetProperty("TotalTime",B).GetValue(fuente,null);
                    Check(actual.Ticks==tiempo32.Ticks&&total.Ticks==12L*TimeSpan.TicksPerSecond,
                          "Fuente musical conserva cuadros/ticks exactos al seek; asimetria="+asimetrico);
                } finally { ((IDisposable)fuente).Dispose(); }
                float[] off=null,on=null;
                foreach(bool cosecha in new bool[]{false,true}) {
                    object owner=Codigo.FormSinEncender(form);Set(owner,"iaHarvestOn",cosecha);
                    object[] args={file,false,0f,0f,null};
                    Check((bool)form.GetMethod("MeasureFile",B).Invoke(owner,args),"MeasureFile sintetico "+asimetrico+"/"+cosecha);
                    if(cosecha)on=(float[])args[4];else off=(float[])args[4];
                }
                bool igual=off!=null&&on!=null&&off.Length==on.Length;
                for(int i=0;igual&&i<off.Length;i++)if(off[i]!=on[i])igual=false;
                Check(igual,"Ficha completa original igual con cosecha on/off; asimetria="+asimetrico);
                string idx=System.IO.Path.Combine(dir,"rocola_ia_datos.idx");
                byte[] registros=System.IO.File.ReadAllBytes(idx);int n=(registros.Length-16)/64;
                Check(n==(asimetrico?1:2),"Cosecha canales correctos: "+n+" registros; asimetria="+asimetrico);
                if(!asimetrico) {
                    Check(BitConverter.ToInt32(registros,16+24)==0&&BitConverter.ToInt32(registros,80+24)==1,"Ambos canales indexados como 0/1");
                    Check(BitConverter.ToUInt64(registros,16+32)==BitConverter.ToUInt64(registros,80+32),"Ambos canales comparten grupo fuente/cancion");
                    int pares=BitConverter.ToInt32(registros,16+8)+BitConverter.ToInt32(registros,80+8);
                    int diana=(int)form.GetField("IaFramesPerSong",B).GetValue(null);
                    float[] cuts=(float[])form.GetField("IaCutsHz",B).GetValue(null);
                    Check(pares<=diana*cuts.Length,"Cosecha L/R conserva presupuesto total de pares ("+pares+")");
                } else Check(BitConverter.ToInt32(registros,16+24)==1,"Solo derecha brillante es maestra; izquierda cortada no");
                System.IO.File.Delete(idx);System.IO.File.Delete(System.IO.Path.Combine(dir,"rocola_ia_datos.bin"));
            }
        } finally { carpeta.SetValue(null,antes);System.IO.Directory.Delete(dir,true); }
    }
    static int Main(string[] args) {
        Assembly a=Assembly.LoadFrom(args[0]);form=a.GetType("Rocola.Form1",true);
        original=form.GetNestedType("FuenteOriginal",B);spectral=form.GetNestedType("SpectralTap",B);tap=form.GetNestedType("LevelTap",B);
        bajo=form.GetNestedType("DeepBassProvider",B);formatos=a.GetType("Rocola.FormatoAudio",true);
        Entrada();Mascara();Cosecha();Console.WriteLine("PASS DSP original: "+checks+" comprobaciones sinteticas; no XP/DAC/escucha.");return 0;
    }
}
