using System;
using System.IO;
using System.Reflection;
using System.Threading;
class Revision20260921 {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 public sealed class Source:IDisposable { public int Disposed; public void Dispose(){Interlocked.Increment(ref Disposed);} }
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 static object Call(Type t,object target,string method,params object[] args){return t.GetMethod(method,F).Invoke(target,args);}
 static void Wait(Func<bool> check){for(int i=0;i<500;i++){if(check())return;Thread.Sleep(10);}throw new Exception("Timeout");}
 [STAThread] static void Main(string[] args){
  Assembly assembly=Assembly.LoadFrom(args.Length>0?args[0]:"rocola.exe");Type form=assembly.GetType("Rocola.Form1");
  Type policy=assembly.GetType("Rocola.CargaTrabajo");
  long roomy=(long)Call(policy,null,"PresupuestoGpu",16,60,1.0),busy=(long)Call(policy,null,"PresupuestoGpu",16,60,14.0);
  Check(roomy>busy && roomy>(long)Call(policy,null,"LimiteTicks",16),"GPU no aprovecha margen");
  Check((long)Call(policy,null,"PresupuestoGpu",2,60,1.0)<roomy,"No protege 2C");
  Check((long)Call(policy,null,"PresupuestoGpu",16,60,double.NaN)==(long)Call(policy,null,"LimiteTicks",16),"Presupuesto invalido");
  Type ahead=assembly.GetType("Rocola.MiraPorDelante"+(char)96+"1").MakeGenericType(typeof(Source));
  object slot=Activator.CreateInstance(ahead,true);
  string dir=Path.Combine(Path.GetTempPath(),"rocola-review-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(dir);
  try{
   string path=Path.Combine(dir,"sample.wav");File.WriteAllBytes(path,new byte[80]);
   Func<string,string> signature=delegate(string p){return (string)Call(form,null,"AudioFileSignature",p);};
   ahead.GetField("Signature",F).SetValue(slot,signature);
   Source old=new Source();Action queued=null;
   Action<Action> post=delegate(Action action){Interlocked.Exchange(ref queued,action);};
   Call(ahead,slot,"Pide",path,new Func<Source>(delegate{return old;}),post);
   Wait(delegate{return queued!=null;});Interlocked.Exchange(ref queued,null)();
   File.WriteAllBytes(path,new byte[81]);
   Check(Call(ahead,slot,"Toma",path)==null && old.Disposed==1,"Entrego archivo modificado");
   Source mid=new Source();
   Call(ahead,slot,"Pide",path,new Func<Source>(delegate{return mid;}),post);
   Wait(delegate{return queued!=null;});
   File.WriteAllBytes(path,new byte[82]);Interlocked.Exchange(ref queued,null)();
   Check(mid.Disposed==1 && !(bool)ahead.GetProperty("Listo",F).GetValue(slot,null),"Cambio durante apertura");
   Source good=new Source();
   Call(ahead,slot,"Pide",path,new Func<Source>(delegate{return good;}),post);
   Wait(delegate{return queued!=null;});Interlocked.Exchange(ref queued,null)();
   Check(ReferenceEquals(Call(ahead,slot,"Toma",path),good),"No entrega archivo vigente");good.Dispose();
   Check((string)Call(form,null,"AudioFileSignature",Path.Combine(dir,"missing"))==null,"Acepta inexistente");
   // Cancelar un BeginInvoke pendiente debe soltar el lector aunque el mensaje no llegue.
   using(BinaryWriter w=new BinaryWriter(File.Create(path))){
    w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(40);
    w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);
    w.Write(44100);w.Write(88200);w.Write((short)2);w.Write((short)16);
    w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(4);w.Write(0);
   }
   object owner=Codigo.FormSinEncender(form);
   object reader=Call(form,owner,"OpenAudio",path);
   form.GetField("audioReady",F).SetValue(owner,reader);
   Call(form,owner,"CancelAudioOpen");Call(form,owner,"CancelAudioOpen");
   Check(form.GetField("audioReady",F).GetValue(owner)==null,"Propiedad pendiente no liberada");
   using(FileStream exclusive=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(exclusive.Length>0,"Lector retenido al cerrar");
   // Un repaso completo no debe desaparecer porque otro tenga una medicion rapida en vuelo.
   string key=Path.Combine(dir,"claim");
   Check((bool)Call(form,null,"ReclamaMedida",key,false),"No obtiene turno inicial");
   bool claimed=false;using(ManualResetEvent done=new ManualResetEvent(false)){
    Thread thread=new Thread(delegate(){claimed=(bool)Call(form,null,"ReclamaMedidaEsperando",key,true,new Func<bool>(delegate{return false;}));done.Set();});
    thread.Start();Check(!done.WaitOne(100,false),"Pierde repaso ocupado");
    Call(form,null,"MedidaPuesta",key);
    Check(!done.WaitOne(60,false),"Suelta antes de publicar");
    Call(form,null,"SueltaMedida",key);Check(done.WaitOne(5000,false)&&claimed,"No retoma repaso completo");thread.Join();
   }
   Call(form,null,"ReiniciaMedidasHechas");
   Check(!(bool)Call(form,null,"ReclamaMedida",key,false),"Reset borra turnos activos");
   Call(form,null,"SueltaMedida",key);
   Check(!(bool)Call(form,null,"ReclamaMedidaEsperando",key,false,new Func<bool>(delegate{return true;})),"Ignora cancelacion");
   Call(form,owner,"PublicaMedida",key,0f,12f,null);
   Check(!(bool)Call(form,null,"YaSeMidio",key),"Duracion sola bloquea sonoridad");
  }finally{Call(ahead,slot,"Suelta");Directory.Delete(dir,true);}
  Console.WriteLine("  OK    identidad de precarga, cierre sin callback, turno hasta publicar, repaso completo y cancelacion.");
 }
}
