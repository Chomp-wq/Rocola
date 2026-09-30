using System;
using System.Reflection;
using System.Collections.Generic;
using NAudio.Wave;
class AudioSuave {
 const BindingFlags F=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static Type form,tapType;static int checks;
 static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Eq(object o,float[] v){o.GetType().GetProperty("EqDb",F).SetValue(o,v,null);}
 sealed class Input:ISampleProvider {
  readonly WaveFormat format;long at;readonly bool dc;
  public Input(int fs,int ch,bool constant){format=WaveFormat.CreateIeeeFloatWaveFormat(fs,ch);dc=constant;}
  public WaveFormat WaveFormat{get{return format;}}
  public int Read(float[] b,int o,int n){for(int i=0;i<n;i++,at++)b[o+i]=dc?.1f:(float)(.1*Math.Sin(2*Math.PI*700*(at/format.Channels)/format.SampleRate)+.02*Math.Sin(2*Math.PI*9000*(at/format.Channels)/format.SampleRate));return n;}
 }
 static ISampleProvider Tap(int fs,int ch,bool dc){
  return (ISampleProvider)Activator.CreateInstance(tapType,F,null,new object[]{new Input(fs,ch,dc),Codigo.FormSinEncender(form)},null);
 }
 static float[] Read(ISampleProvider tap,int count,int block){
  float[] result=new float[count];int at=0;
  while(at<count){int n=Math.Min(block,count-at);float[] b=new float[n+6];for(int i=0;i<b.Length;i++)b[i]=987;
   Check(tap.Read(b,3,n)==n,"Longitud incorrecta");
   for(int i=0;i<3;i++)Check(b[i]==987&&b[n+3+i]==987,"Escribe fuera del buffer");
   Array.Copy(b,3,result,at,n);at+=n;
  }return result;
 }
 static void Gain(int fs,int ch){
  ISampleProvider t=Tap(fs,ch,true);Set(t,"Gain",.2f);
  float[] start=Read(t,ch*(31+8),ch);
  for(int i=0;i<start.Length;i++)Check(start[i]==(i<31*ch?0f:.1f*.2f),"Ganancia inicial exacta tras cebado de 31 cuadros");
  Set(t,"Master",.5f);int frames=(int)(fs*.020);
  float[] ramp=Read(t,(31+frames+5)*ch,1);float prev=.1f*.2f;
  for(int i=0;i<31*ch;i++)Check(ramp[i]==prev,"Cambiar ganancia no cambia el seco ya retenido");
  for(int f=0;f<frames+5;f++){
   float x=ramp[(31+f)*ch];Check(x<=prev+1e-8&&Math.Abs(x-prev)<.0001,"Escalon en ganancia");
   for(int c=1;c<ch;c++)Check(x==ramp[(31+f)*ch+c],"Rampa diferente por canal");prev=x;
  }
  Check(prev==.1f*(.2f*.5f),"Rampa no alcanza objetivo exacto");
  Set(t,"Master",1f);Read(t,ch*(frames/2),ch);
  float middle=(float)Get(Get(t,"gainRamp"),"Current");Set(t,"Master",0f);
  Read(t,31*ch,ch);   // agotar solo lo retenido antes de la nueva peticion
  float first=Read(t,ch,ch)[0];Check(Math.Abs(first-.1f*middle)<.0001,"Retarget vuelve al objetivo anterior");
  Read(t,ch*frames,ch*31);Check((float)Get(Get(t,"gainRamp"),"Current")==0,"No termina retarget");
 }
 static float[] Sequence(int fs,int ch,int block){
  ISampleProvider t=Tap(fs,ch,false);List<float> all=new List<float>();
  float[] a={2,-2,0,1,3,-1,2,0},b={-1,1,0,-2,2,1,-1,0};
  for(int k=0;k<8;k++){
   if(k==1){Set(t,"Gain",.6f);Eq(t,a);Set(t,"Excite",.3f);}
   if(k==2)Eq(t,b);if(k==3)Eq(t,null);if(k==4)Eq(t,a);
   if(k==5){Set(t,"Gain",1f);Set(t,"Excite",0f);Eq(t,b);}
   if(k==6)Eq(t,null);
   all.AddRange(Read(t,(k==7?fs/5:fs/100)*ch,block));
  }
  Check(Get(t,"currentEq")==null&&Get(t,"previousEq")==null,"Ultima peticion no gana");
  Check((int)Get(t,"eqFadeLeft")==0,"Mezcla EQ no termina");
  return all.ToArray();
 }
 static void EqContinuity(int fs,int ch){
  ISampleProvider t=Tap(fs,ch,false);ISampleProvider reference=Tap(fs,ch,false);
  int settle=fs/10*ch;Read(t,settle,257*ch);Read(reference,settle,257*ch);
  float[] a={0,0,0,6,0,0,0,0};Eq(t,a);
  object prepared=Get(t,"pendingEq");
  float[][] cf=(float[][])Get(prepared,"Coefficients");
  float[] dry=Read(reference,32*ch,ch),smooth=Read(t,32*ch,ch);
  for(int i=0;i<31*ch;i++)Check(smooth[i]==dry[i],"Cambiar EQ modifica muestras retenidas");
  float wet=dry[31*ch]*cf[0][0];
  float expected=dry[31*ch]+(wet-dry[31*ch])/(int)(fs*.030);
  Check(Math.Abs(smooth[31*ch]-expected)<1e-7,"Primer cuadro no cruza desde filtro anterior");
  Check(Math.Abs(smooth[31*ch]-dry[31*ch])<Math.Abs(wet-dry[31*ch])*.01,"EQ cambia de golpe");
  // Identidad inicial A y miles de reescrituras no recrean ni comparten estados activos.
  object pending=Get(t,"pendingEq");Eq(t,a);Check(Object.ReferenceEquals(pending,Get(t,"pendingEq")),"Reprepara curva identica");
  Read(t,fs/10*ch,137*ch);Check((int)Get(t,"eqFadeLeft")==0,"EQ no alcanza el filtro nuevo");
  Eq(t,null);Read(t,fs/10*ch,137*ch);Check(Get(t,"currentEq")==null,"No desactiva EQ");
 }
 static int Main(string[] args){
  form=Assembly.LoadFrom(args.Length>0?args[0]:"rocola.exe").GetType("Rocola.Form1");
  tapType=form.GetNestedType("LevelTap",F);
  Check(!Codigo.Llama(tapType.GetMethod("Read",F),"EqPeak"),"Coeficientes calculados al leer");
  foreach(Codigo.Paso p in Codigo.Pasos(tapType.GetMethod("Read",F)))Check(p.Op!=0x8D,"Reserva array en Read");
  foreach(int fs in new int[]{22050,44100,48000,96000})foreach(int ch in new int[]{1,2,6}){
   Gain(fs,ch);EqContinuity(fs,ch);
   float[] a=Sequence(fs,ch,1),b=Sequence(fs,ch,257*ch);
   Check(a.Length==b.Length,"Cuenta cambia con bloques");
   for(int i=0;i<a.Length;i++)Check(a[i]==b[i]&&!Single.IsNaN(a[i])&&!Single.IsInfinity(a[i]),"DSP depende de fragmentacion");
   float pole=(float)form.GetMethod("ExciterPole",F).Invoke(null,new object[]{fs});
   Check(pole>0&&pole<1,"Polo inestable");
   Check(Math.Abs(Math.Pow(1-pole,fs*.00002)-Math.Pow(.45,.882))<1e-6,"Tiempo del filtro cambia con formato");
  }
  Check((float)form.GetMethod("ExciterPole",F).Invoke(null,new object[]{44100})==.55f,"Cambia referencia 44.1k");
  Console.WriteLine("OK audio suave: "+checks+" comprobaciones; 12 formatos/canales, rampas, offsets, peticiones rapidas y fragmentacion.");return 0;
 }
}
