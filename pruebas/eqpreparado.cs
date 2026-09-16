using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using NAudio.Wave;

class EqPreparado
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Get(Type t, object o, string name) { return t.GetField(name, F).GetValue(o); }
    static void Set(Type t, object o, string name, object v) { t.GetField(name, F).SetValue(o, v); }
    static void Check(bool b, string why) { if (!b) throw new Exception(why); }
    static void Equal(float[] a, float[] b, string why) {
        Check(a.Length == b.Length, why);
        for (int i=0;i<a.Length;i++) if (a[i] != b[i]) throw new Exception(why+" @"+i+": "+a[i]+" / "+b[i]);
    }
    sealed class Input : ISampleProvider {
        readonly WaveFormat format; long at;
        public Input(int rate,int channels) { format=WaveFormat.CreateIeeeFloatWaveFormat(rate,channels); }
        public WaveFormat WaveFormat { get { return format; } }
        public int Read(float[] b,int offset,int count) {
            for(int i=0;i<count;i++,at++) {
                double frame=at/format.Channels; int channel=(int)(at%format.Channels);
                b[offset+i]=(float)(.12*Math.Sin(frame*2*Math.PI*(431+channel*127)/format.SampleRate)+.08*Math.Sin(frame*2*Math.PI*7127/format.SampleRate));
            }
            return count;
        }
    }
    static ISampleProvider Tap(Type form,int rate,int channels) {
        object owner=FormatterServices.GetUninitializedObject(form);
        foreach(FieldInfo f in form.GetFields(F))
            if(!f.IsStatic && f.FieldType.Namespace!=null && f.FieldType.Namespace.StartsWith("System.Collections") && f.FieldType.GetConstructor(Type.EmptyTypes)!=null)
                f.SetValue(owner,Activator.CreateInstance(f.FieldType));
        Set(form,owner,"levelLock",new object());
        return (ISampleProvider)Activator.CreateInstance(form.GetNestedType("LevelTap",F),F,null,new object[]{new Input(rate,channels),owner},null);
    }
    static void Curve(ISampleProvider tap,float[] db) {
        Type t=tap.GetType();PropertyInfo p=t.GetProperty("EqDb",F);
        if(p!=null)p.SetValue(tap,db,null); else t.GetField("EqDb",F).SetValue(tap,db);
    }
    // Camino anterior independiente: prepara al leer, conserva estados al desactivar
    // y compacta las mismas bandas en el mismo orden, sin suavizados nuevos.
    sealed class Reference : ISampleProvider {
        readonly ISampleProvider source; readonly Type form; readonly float[] centers;
        public float[] Curve; float[] used,state,lp; float[][] coef;int active;
        public float Gain=1f,Master=1f,Excite;
        public Reference(Type f,int rate,int channels) { form=f;source=new Input(rate,channels);centers=(float[])Get(f,null,"EqCentros"); }
        public WaveFormat WaveFormat { get { return source.WaveFormat; } }
        public int Read(float[] buffer,int offset,int count) {
            int n=source.Read(buffer,offset,count);
            for(int i=offset;i<offset+n;i++){float v=buffer[i];if(!(v>=-4f&&v<=4f))buffer[i]=0;}
            float g=Gain*Master,ex=Excite;float[] db=Curve;
            bool eq=db!=null,excite=ex>.001f;int ch=WaveFormat.Channels;
            if(eq&&!Object.ReferenceEquals(db,used)){
                coef=new float[8][];active=0;
                for(int b=0;b<8;b++){
                    float[] cf=(float[])form.GetMethod("EqPeak",F).Invoke(null,new object[]{centers[b],WaveFormat.SampleRate,db[b]});
                    if(cf!=null)coef[active++]=cf;
                }
                state=new float[ch*32];used=db;
            }
            if(eq&&active==0)eq=false;
            if(g!=1f||excite||eq){
                if(excite&&lp==null)lp=new float[ch];
                float mix=ex*.5f;int ci=0;
                for(int i=offset;i<offset+n;i++){
                    float v=buffer[i]*g;
                    if(eq)for(int b=0;b<active;b++){
                        float[] cf=coef[b];int q=ci*32+b*4;
                        float o=cf[0]*v+cf[1]*state[q]+cf[2]*state[q+1]-cf[3]*state[q+2]-cf[4]*state[q+3];
                        state[q+1]=state[q];state[q]=v;state[q+3]=state[q+2];state[q+2]=o;v=o;
                    }
                    if(excite){
                        float low=(lp[ci]+=(v-lp[ci])*.55f);float high=v-low;
                        float x=high*2.5f;float tanh;
                        if(!(x>-3f))tanh=-1f;else if(x>3f)tanh=1f;else{float x2=x*x;tanh=x*(27f+x2)/(27f+9f*x2);}
                        v+=tanh*.4f*mix;
                    }
                    if(excite||eq){ci++;if(ci>=ch)ci=0;}
                    buffer[i]=v;
                }
            }
            return n;
        }
    }
    static void Test(Type form,Type baseline,int rate,int channels,float excite) {
        ISampleProvider tap=Tap(form,rate,channels),old=baseline==null?null:Tap(baseline,rate,channels);
        Reference reference=new Reference(form,rate,channels);reference.Gain=.87f;reference.Master=1.23f;reference.Excite=excite;
        foreach(ISampleProvider p in old==null?new[]{tap}:new[]{tap,old}){
            Set(p.GetType(),p,"Gain",.87f);Set(p.GetType(),p,"Master",1.23f);Set(p.GetType(),p,"Excite",excite);
        }
        float[] a={2f,-3f,0f,.00001f,4f,-1f,0f,1.5f},b={-1f,0f,2f,0f,-2f,1f,3f,0f},zero=new float[8];
        float[][] sequence={null,a,a,null,a,b,zero,b,null,b,a,(float[])a.Clone(),zero,null};
        for(int k=0;k<sequence.Length;k++){
            if(k==2) { Curve(tap,b); if(old!=null)Curve(old,b); reference.Curve=b; } // dos cambios sin leer: no reiniciar A
            Curve(tap,sequence[k]);reference.Curve=sequence[k];if(old!=null)Curve(old,sequence[k]);
            object pending=Get(tap.GetType(),tap,"pendingEq");
            if(sequence[k]!=null) {
                Check(pending!=null,"No prepara antes de leer");
                Check(((float[][])Get(pending.GetType(),pending,"Coefficients")).Length==8,"Coeficientes incompletos");
                Check(((float[])Get(pending.GetType(),pending,"State")).Length==channels*32,"Estado incompleto");
                Curve(tap,sequence[k]);Check(Object.ReferenceEquals(pending,Get(tap.GetType(),tap,"pendingEq")),"Reasignacion repite preparacion");
            }
            for(int block=0;block<5;block++){
                int count=(block==0?1:block==1?17:509)*channels,offset=3;
                float[] x=new float[count+6],y=new float[count+6],z=new float[count+6];
                for(int i=0;i<x.Length;i++)x[i]=y[i]=z[i]=777f;
                Check(tap.Read(x,offset,count)==reference.Read(y,offset,count),"Longitud distinta");
                Equal(x,y,"PCM de referencia distinto "+rate+"/"+channels+" cambio "+k);
                if(old!=null){old.Read(z,offset,count);Equal(x,z,"PCM del Release anterior distinto");}
            }
        }
    }
    static void TestConcurrent(Type form) {
        ISampleProvider tap=Tap(form,48000,2);Exception failure=null;
        float[] a={2,-2,1,-1,3,-3,2,-2},b={-1,1,-2,2,-3,3,-1,1};
        Thread writer=new Thread(delegate() {
            try {for(int i=0;i<2000;i++)Curve(tap,i%3==0?null:i%3==1?a:b);}
            catch(Exception e){failure=e;}
        });
        writer.Start();
        float[] data=new float[512];
        try {
            for(int i=0;i<2000;i++) {
                tap.Read(data,0,data.Length);
                foreach(float v in data)Check(!float.IsNaN(v)&&!float.IsInfinity(v)&&Math.Abs(v)<4,"Publicacion EQ incompleta");
            }
        } finally { writer.Join(); }
        if(failure!=null)throw failure;
    }
    static int Main(string[] args) {
        Type form=Assembly.LoadFrom(args[0]).GetType("Rocola.Form1",true);
        Type baseline=args.Length>1?Assembly.LoadFile(System.IO.Path.GetFullPath(args[1])).GetType("Rocola.Form1",true):null;
        foreach(int rate in new[]{22050,44100,48000,96000})
        foreach(int channels in new[]{1,2,6})
        foreach(float excite in new[]{0f,.35f})Test(form,baseline,rate,channels,excite);
        TestConcurrent(form);
        Console.WriteLine("PASS: 24 formatos/realces, 14 cambios EQ, offsets, identidad, estado y publicacion concurrente; PCM exacto"+(baseline!=null?" frente a Release previo":"")+".");
        return 0;
    }
}
