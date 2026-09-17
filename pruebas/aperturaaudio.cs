using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using NAudio.Wave;
class AperturaAudioTest {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 public sealed class Source:IDisposable {
  public int Disposed; public IDisposable Inner;
  public void Dispose(){Interlocked.Increment(ref Disposed);if(Inner!=null)Inner.Dispose();}
 }
 static void Check(bool yes,string why){if(!yes)throw new Exception(why);}
 static object Call(Type t,object o,string n,params object[] a){return t.GetMethod(n,F).Invoke(o,a);}
 static readonly Queue<Action> posted=new Queue<Action>();
 static readonly AutoResetEvent notice=new AutoResetEvent(false);
 static void Post(Action a){lock(posted)posted.Enqueue(a);notice.Set();}
 static void Pump(){
  Action a=null;
  for(int i=0;i<50&&a==null;i++){
   lock(posted)if(posted.Count>0)a=posted.Dequeue();
   if(a==null)notice.WaitOne(100,false);
  }
  Check(a!=null,"No llega resultado");a();
 }
 static void Wait(Func<bool> predicate,string why){
  for(int i=0;i<500;i++){if(predicate())return;Thread.Sleep(10);}throw new Exception(why);
 }
 static Type workerType;
 static object worker;
 static void Start(Func<Source> open,Action<Source,Exception> done){Call(workerType,worker,"Start",open,done,new Action<Action>(Post));}
 static void Cancel(){Call(workerType,worker,"Cancel");}
 static void Races(){
  int main=Thread.CurrentThread.ManagedThreadId,active=0,maxActive=0,started=0,delivered=0,oldDelivered=0;
  Source old=new Source(),last=new Source();
  using(ManualResetEvent entered=new ManualResetEvent(false))
  using(ManualResetEvent release=new ManualResetEvent(false)){
   Start(delegate{
    Check(Thread.CurrentThread.ManagedThreadId!=main,"Abre en hilo del llamador");
    int running=Interlocked.Increment(ref active);maxActive=Math.Max(maxActive,running);entered.Set();
    release.WaitOne();Interlocked.Decrement(ref active);return old;
   },delegate(Source s,Exception e){oldDelivered++;s.Dispose();});
   Check(entered.WaitOne(5000,false),"No inicia apertura");
   for(int i=0;i<100;i++)Start(delegate{Interlocked.Increment(ref started);return new Source();},delegate(Source s,Exception e){s.Dispose();});
   Start(delegate{maxActive=Math.Max(maxActive,Interlocked.Increment(ref active));Interlocked.Decrement(ref active);return last;},
    delegate(Source s,Exception e){Check(Thread.CurrentThread.ManagedThreadId==main,"Entrega fuera de UI");Check(e==null&&s==last,"Resultado incorrecto");delivered++;s.Dispose();});
   release.Set();Pump();
  }
  Check(maxActive==1&&started==0&&oldDelivered==0&&delivered==1,"Concurrencia/solicitudes obsoletas");
  Check(old.Disposed==1&&last.Disposed==1,"No libera una sola vez");
  Source abandoned=new Source();
  Start(delegate{return abandoned;},delegate(Source s,Exception e){oldDelivered++;});
  Wait(delegate{lock(posted)return posted.Count>0;},"No publica para cancelar");
  Cancel();Check(abandoned.Disposed==1,"Cancel no libera resultado listo");Pump();
  Check(oldDelivered==0,"Entrega despues de cancelar");
  Source closed=new Source();
  Call(workerType,worker,"Start",new Func<Source>(delegate{return closed;}),
   new Action<Source,Exception>(delegate(Source s,Exception e){throw new Exception("No debe entregar");}),
   new Action<Action>(delegate(Action a){throw new ObjectDisposedException("ventana");}));
  Wait(delegate{return closed.Disposed==1;},"Fuga cuando desaparece ventana");
  Exception failure=new IOException("fallo de prueba");int errors=0;
  Start(delegate{throw failure;},delegate(Source s,Exception e){Check(s==null&&ReferenceEquals(e,failure),"Pierde error");errors++;});
  Pump();Check(errors==1,"Error duplicado");
  // Reintento desde la propia entrega: una cache fallida puede abrir el original.
  Source fallback=new Source();
  Start(delegate{throw failure;},delegate(Source s,Exception e){
   Start(delegate{return fallback;},delegate(Source original,Exception err){Check(err==null,"Falla reintento");original.Dispose();});
  });
  Pump();Pump();Check(fallback.Disposed==1,"Reintento no completa");
  using(ManualResetEvent blocked=new ManualResetEvent(false))
  using(ManualResetEvent unblock=new ManualResetEvent(false)){
   Source late=new Source();
   Start(delegate{blocked.Set();unblock.WaitOne();return late;},delegate(Source s,Exception e){oldDelivered++;});
   Check(blocked.WaitOne(5000,false),"No inicia para cancelar");Cancel();unblock.Set();
   Wait(delegate{return late.Disposed==1;},"No libera apertura cancelada en vuelo");Check(oldDelivered==0,"Entrega tardia");
  }
 }
 static void Audio(Type form){
  object owner=FormatterServices.GetUninitializedObject(form);
  string dir=Path.Combine(Path.GetTempPath(),"rocola-open-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  try{
   foreach(int format in new int[]{1,3})foreach(int channels in new int[]{1,2}){
    string path=Path.Combine(dir,"audio-"+format+"-"+channels+".wav");int size=format==1?2:4,rate=44100,frames=4096;
    using(BinaryWriter w=new BinaryWriter(File.Create(path))){
     w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+frames*channels*size);
     w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)format);w.Write((short)channels);
     w.Write(rate);w.Write(rate*channels*size);w.Write((short)(channels*size));w.Write((short)(size*8));
     w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(frames*channels*size);
     for(int i=0;i<frames*channels;i++){float sample=(float)(.3*Math.Sin(i*.07));if(format==1)w.Write((short)(sample*32767));else w.Write(sample);}
    }
    Source prepared=null;
    Start(delegate{return new Source{Inner=(IDisposable)Call(form,owner,"OpenAudio",path)};},
     delegate(Source s,Exception error){Check(error==null,"No abre WAV en worker");prepared=s;});
    Pump();Check(prepared!=null,"No entrega WAV");
    using(prepared)
    using(IDisposable normal=(IDisposable)Call(form,owner,"OpenAudio",path)){
     ISampleProvider a=(ISampleProvider)prepared.Inner,b=(ISampleProvider)normal;
     Check(a.WaveFormat.Equals(b.WaveFormat),"Formato distinto");
     float[] x=new float[391],y=new float[391];int read,total=0;
     do{
      read=a.Read(x,7,360);int n=b.Read(y,7,360);Check(read==n,"Longitud distinta");
      for(int i=0;i<read;i++)Check(x[i+7]==y[i+7],"PCM distinto");
      total+=read;
     }while(read>0);
     Check(total==frames*channels,"Muestras perdidas");
    }
    using(FileStream exclusive=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(exclusive.Length>0,"Archivo retenido");
   }
  }finally{Directory.Delete(dir,true);}
 }
 static void AudioFiles(Type form,string directory){
  object owner=FormatterServices.GetUninitializedObject(form);
  foreach(string path in Directory.GetFiles(directory)){
   Source prepared=null;
   Start(delegate{return new Source{Inner=(IDisposable)Call(form,owner,"OpenAudio",path)};},
    delegate(Source s,Exception error){Check(error==null,"No abre "+path+": "+error);prepared=s;});
   Pump();Check(prepared!=null,"No entrega "+path);
   Wait(delegate{return !(bool)workerType.GetField("running",F).GetValue(worker);},"Worker no termina");
   using(prepared)
   using(IDisposable normal=(IDisposable)Call(form,owner,"OpenAudio",path)){
    ISampleProvider a=(ISampleProvider)prepared.Inner,b=(ISampleProvider)normal;
    Check(a.WaveFormat.Equals(b.WaveFormat),"Formato distinto: "+path);
    float[] x=new float[391],y=new float[391];int read,total=0;
    do{
     read=a.Read(x,7,360);Check(read==b.Read(y,7,360),"Longitud distinta: "+path);
     for(int i=0;i<read;i++)Check(x[i+7]==y[i+7],"PCM distinto: "+path);
     total+=read;
    }while(read>0);
    Check(total>0,"Archivo vacio: "+path);
   }
   using(FileStream exclusive=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))Check(exclusive.Length>0,"Archivo retenido");
   Console.WriteLine("PASS: PCM exacto y archivo liberado: "+Path.GetFileName(path));
  }
 }
 [STAThread]
 static void Main(string[] args){
  Assembly asm=Assembly.LoadFrom(args[0]);workerType=asm.GetType("Rocola.AperturaEnFondo"+(char)96+"1").MakeGenericType(typeof(Source));
  worker=Activator.CreateInstance(workerType,true);
  try{Races();Audio(asm.GetType("Rocola.Form1"));if(args.Length>1)AudioFiles(asm.GetType("Rocola.Form1"),args[1]);}
  finally{Cancel();}
  Console.WriteLine("PASS: apertura fuera del llamador, un trabajador, 100 saltos coalescidos, cancelacion, cierre, errores, reintento y PCM WAV 16/float32 mono/estereo identico.");
 }
}
