using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;
class AudioEstereo {
 const BindingFlags F=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static Type form;static int checks;
 delegate void Acc(float[] b,int o,int ch,float[][] cf,float[] st,double[] sum);
 static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Call(object o,string n,params object[] a){return form.GetMethod(n,F).Invoke(o,a);}
 static double[] Energy(int fs,int ch,int mode) {
  Acc acc=(Acc)Delegate.CreateDelegate(typeof(Acc),form.GetMethod("AccumulateEqFrame",F));
  float[][] cf=new float[8][];float[] centres=(float[])form.GetField("EqCentros",F).GetValue(null);
  for(int b=0;b<8;b++)cf[b]=(float[])Call(null,"EqBpf",centres[b],fs);
  float[] state=new float[32*ch],frame=new float[ch+2];double[] sums=new double[8];
  for(int i=0;i<fs/5;i++){
   float x=(float)(.1*Math.Sin(i*2*Math.PI*440/fs)+.07*Math.Sin(i*2*Math.PI*3700/fs));
   for(int c=0;c<ch;c++)frame[c+1]=(mode==1&&(c&1)==1)?-x:(mode==2&&c>0?0:x);
   acc(frame,1,ch,cf,state,sums);
  }return sums;
 }
 static float[] Measure(string path,bool opposite){
  const int fs=44100;float[] b=new float[4096];
  using(WaveFileWriter w=new WaveFileWriter(path,WaveFormat.CreateIeeeFloatWaveFormat(fs,2))){
   for(int p=0;p<fs*2;){
    int n=Math.Min(2048,fs*2-p);
    for(int i=0;i<n;i++){float x=(float)(.1*Math.Sin((p+i)*2*Math.PI*440/fs)+.08*Math.Sin((p+i)*2*Math.PI*3700/fs));b[i*2]=x;b[i*2+1]=opposite?-x:x;}
    w.WriteSamples(b,0,n*2);p+=n;
   }
  }
  object owner=Codigo.FormSinEncender(form);
  object[] args={path,true,0f,0f,null};
  Check((bool)Call(owner,"MeasureFile",args),"No mide WAV");
  float[] curve=args[4] as float[];Check(curve!=null&&curve.Length==12&&curve[11]==1,"No marca medicion nueva");
  return curve;
 }
 static void Cache(){
  object f=Codigo.FormSinEncender(form);Set(f,"startupReadPending",true);
  Dictionary<string,float[]> cache=(Dictionary<string,float[]>)Get(f,"eqCache");
  for(int fields=8;fields<=12;fields++){
   string line="";for(int i=0;i<fields;i++)line+=(i>0?";":"")+(i==11?"1":i.ToString());
   float[] c=(float[])Call(null,"ReadEqValues",line,line.Length);
   Check(c.Length==12&&c[0]==0&&c[7]==7&&c[11]==(fields==12?1:0),"Formato viejo pierde datos");
   cache["fixture"]=c;Check((bool)Call(f,"HasStereoEq","fixture")==(fields==12),"Migracion confunde versiones");
  }
  // Una huella vieja no se mezcla 50/50: sus primeras ocho bandas se reemplazan.
  float[] old=new float[12];for(int i=0;i<8;i++)old[i]=30;old[8]=18000;old[9]=.12f;old[10]=40;
  cache["old"]=old;Set(f,"eqMeasPath","old");Set(f,"eqMeasN",882000L);
  double[] sums=new double[8];for(int i=0;i<8;i++)sums[i]=882000;
  Set(f,"eqMeasSum",sums);Call(f,"FinalizeEqMeasure");
  float[] fresh=cache["old"];for(int i=0;i<8;i++)Check(fresh[i]==0,"Contamina huella con mezcla mono vieja");
  Check(fresh[8]==18000&&fresh[9]==.12f&&fresh[10]==40&&fresh[11]==1,"Pierde metadatos");
  Type track=form.GetNestedType("Track",F);IList tracks=(IList)Get(f,"tracks");
  Dictionary<string,float> durations=(Dictionary<string,float>)Get(f,"durCache");
  HashSet<string> deep=(HashSet<string>)Get(f,"deepDone");
  foreach(string name in new string[]{"legacy","updated","short"}){
   object t=Activator.CreateInstance(track,true);Set(t,"Path",name);tracks.Add(t);
   durations[name]=name=="short"?.1f:120f;deep.Add(name);
  }
  cache["legacy"]=old;cache["updated"]=fresh;
  foreach(bool turbo in new bool[]{false,true}){
   object[] args={turbo,0};List<string> pending=(List<string>)Call(f,"TandaPorMedir",args);
   Check(pending.Count==1&&pending[0]=="legacy","No migra incrementalmente o repite archivos cortos");
  }
 }
 static int Main(string[] args){
  form=Assembly.LoadFrom(args[0]).GetType("Rocola.Form1");
  foreach(string method in new string[]{"MeasureFile","ReportAudioBlock"})
   Check(Codigo.Llama(form.GetMethod(method,F),"AccumulateEqFrame"),"Ruta sin nueva medicion: "+method);
  foreach(Codigo.Paso p in Codigo.Pasos(form.GetMethod("AccumulateEqFrame",F)))
   Check(p.Op!=0x8D&&p.Op!=0x73,"Reserva por cuadro de medicion");
  foreach(int fs in new int[]{22050,44100,48000,96000}){
   double[] mono=Energy(fs,1,0);
   foreach(int ch in new int[]{2,6})foreach(int mode in new int[]{0,1,2}){
    double[] multi=Energy(fs,ch,mode);
    for(int b=0;b<8;b++)Check(Math.Abs(multi[b]*(mode==2?ch:1)-mono[b])<1e-8*Math.Max(1,mono[b]),"Potencia depende de fase/canales");
   }
  }
  Cache();
  string folder=Path.Combine(Path.GetTempPath(),"rocola-stereo-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(folder);
  try{
   float[] a=Measure(Path.Combine(folder,"same.wav"),false),b=Measure(Path.Combine(folder,"opposite.wav"),true);
   for(int i=0;i<8;i++)Check(Math.Abs(a[i]-b[i])<1e-5,"Archivo en contrafase pierde espectro");
  }finally{Directory.Delete(folder,true);}
  Console.WriteLine("OK audio estereo: "+checks+" comprobaciones; fase, archivos WAV y migracion incremental.");
  return 0;
 }
}
