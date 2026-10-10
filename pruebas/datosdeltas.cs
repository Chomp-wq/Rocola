using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

class DatosDeltas
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Type F; static int checks;
    static object Get(object f, string n) { return F.GetField(n, B).GetValue(f); }
    static void Set(object f, string n, object v) { F.GetField(n, B).SetValue(f, v); }
    static object Call(object f, string n, params object[] a) { return F.GetMethod(n, B).Invoke(f, a); }
    static void Check(bool x, string why) { if (!x) throw new Exception(why); checks++; Console.WriteLine("OK "+why); }
    static void Wait(object f) { Codigo.EsperaGuardado(F, f); }
    static string Row(float rms, float d, string key) { return rms.ToString("0.######", CultureInfo.InvariantCulture)+"|"+d.ToString("0.##",CultureInfo.InvariantCulture)+"|"+key+"\r\n"; }
    static object Owner()
    {
        object f=Codigo.FormSinEncender(F); Set(f,"medidasDeltasActivas",true);
        foreach (FieldInfo x in F.GetFields(B))
            if (!x.IsStatic && x.FieldType.Name=="GuardadoReciente" && x.GetValue(f)==null) x.SetValue(f,Activator.CreateInstance(x.FieldType,true));
        return f;
    }
    static IDictionary Pending(object f, string n)
    { object d=Get(f,n); return (IDictionary)d.GetType().GetField("pendientes",B).GetValue(d); }
    static IDictionary Mirror(object f, string n)
    { object d=Get(f,n); return (IDictionary)d.GetType().GetField("Trabajo",B).GetValue(d); }
    static void Volume(string dir)
    {
        object f=Owner(); var loud=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
        var dur=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<20000;i++) { loud["cancion-"+i]=.1f; dur["cancion-"+i]=200f; }
        Set(f,"loudCache",loud); Set(f,"durCache",dur);
        Call(f,"SaveLoudCache"); Wait(f);
        string p=(string)Call(f,"LoudFile"), before=File.ReadAllText(p); var extra=new StringBuilder();
        for(int i=20000;i<20025;i++) { string k="cancion-"+i;loud[k]=.2f;dur[k]=250f;Call(f,"MarcarVolumen",k);extra.Append(Row(.2f,250f,k)); }
        Check(Pending(f,"cambiosVolumen").Count==25,"20000 medidas: pantalla conserva solo 25 pendientes");
        object mirror=Mirror(f,"cambiosVolumen");
        Call(f,"SaveLoudCache"); Wait(f);
        Check(File.ReadAllText(p)==before+extra.ToString(),"solo se anexan esas 25 lineas");
        Check(ReferenceEquals(mirror,Mirror(f,"cambiosVolumen")) && Pending(f,"cambiosVolumen").Count==0,"espejo reutilizado y pendientes confirmados");
        before=File.ReadAllText(p);dur["cancion-17"]=999.5f;Call(f,"MarcarVolumen","cancion-17");Call(f,"SaveLoudCache");Wait(f);
        Check(File.ReadAllText(p)==before+Row(.1f,999.5f,"cancion-17"),"cambio de duracion solo conserva RMS y se guarda");
        object gate=Get(f,"loudSaveLock");Monitor.Enter(gate);
        try {
            loud["cancion-9"]=.3f;Call(f,"MarcarVolumen","cancion-9");Call(f,"SaveLoudCache");
            loud["cancion-9"]=.4f;Call(f,"MarcarVolumen","cancion-9");Call(f,"SaveLoudCache");
        } finally {Monitor.Exit(gate);}
        Wait(f);
        Check(File.ReadAllText(p).EndsWith(Row(.4f,200f,"cancion-9")) && Pending(f,"cambiosVolumen").Count==0,"coalescer/cancelar nunca consume la medida mas reciente");
        using(FileStream locked=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.None)) {
            loud["cancion-18"]=.5f;Call(f,"MarcarVolumen","cancion-18");Call(f,"SaveLoudCache");Wait(f);
            Check(Pending(f,"cambiosVolumen").Contains("cancion-18"),"fallo del disco conserva pendientes para reintentar");
        }
        Call(f,"SaveLoudCache");Wait(f);
        Check(Pending(f,"cambiosVolumen").Count==0 && File.ReadAllText(p).Contains(Row(.5f,200f,"cancion-18")),"reintento completa lo perdido sin otra medicion");
        loud.Remove("cancion-8");dur.Remove("cancion-8");Call(f,"MarcarVolumen","cancion-8");Call(f,"SaveLoudCache");Wait(f);
        Check(!File.ReadAllText(p).Contains("|cancion-8\r\n") && !Mirror(f,"cambiosVolumen").Contains("cancion-8"),"baja compacta sin resucitar una ruta");
        File.AppendAllText(p,"cambio externo\r\n");Call(f,"SaveLoudCache");Wait(f);
        Check(!File.ReadAllText(p).Contains("cambio externo"),"cambio externo se repara aunque no haya delta");
        Set(f,"startupReadPending",true);loud["cancelada"]=.7f;Call(f,"MarcarVolumen","cancelada");Call(f,"SaveLoudCache");Wait(f);
        Check(!File.ReadAllText(p).Contains("|cancelada\r\n"),"carga pendiente no pisa datos anteriores");
        Set(f,"startupReadPending",false);Call(f,"SaveLoudCache");Wait(f);
        Check(File.ReadAllText(p).Contains(Row(.7f,0f,"cancelada")),"reanudacion guarda los pendientes retenidos");
        // Medicion sintetica: preparar espejo y huellas una vez, excluir I/O del cronometro UI.
        Stopwatch timer=Stopwatch.StartNew();
        for(int i=0;i<50;i++) { dur["cancion-"+i]=300f;Call(f,"MarcarVolumen","cancion-"+i); }
        timer.Stop(); Console.WriteLine("Sintetico 20000 filas: 50 publicaciones UI "+timer.Elapsed.TotalMilliseconds.ToString("0.000",CultureInfo.InvariantCulture)+" ms (incluye reflexion)");
        Call(f,"SaveLoudCache");Wait(f);
    }
    static void EqAndInteg()
    {
        object f=Owner(); var eq=new Dictionary<string,float[]>(StringComparer.OrdinalIgnoreCase);float[] c=new float[12]; c[8]=20000f;eq["eq.mp3"]=c;Set(f,"eqCache",eq);
        Call(f,"SaveEqCache");Wait(f);string p=(string)Call(f,"EqFile");string before=File.ReadAllText(p);
        c=(float[])c.Clone();c[0]=2.5f;eq["eq.mp3"]=c;Call(f,"MarcarEq","eq.mp3");c[0]=99f;
        Call(f,"SaveEqCache");Wait(f);
        Check(File.ReadAllText(p).StartsWith(before) && File.ReadAllText(p).Substring(before.Length).StartsWith("2.5;"),"EQ pendiente es inmutable y no cambia por un array prestado");
        eq.Remove("eq.mp3");Call(f,"MarcarEq","eq.mp3");Call(f,"SaveEqCache");Wait(f);Check(File.ReadAllText(p)=="","EQ borrado no reaparece");
        Type s=F.Assembly.GetType("Rocola.SonoridadIntegrada",true);object value=Activator.CreateInstance(s,B,null,new object[]{1,-12f,2f,20},null);
        IDictionary integ=(IDictionary)Get(f,"integCache");integ["integ.mp3"]=value;Set(f,"integEstado",1);Set(f,"integCambios",1L);
        List<string> future=new List<string>(); for(int i=0;i<1600;i++)future.Add("99|version-futura-"+i);Set(f,"integAjenas",future);
        Call(f,"SaveIntegCache");Wait(f);p=(string)Call(f,"IntegFile");before=File.ReadAllText(p);
        value=Activator.CreateInstance(s,B,null,new object[]{1,-13f,2f,20},null);integ["integ.mp3"]=value;Call(f,"MarcarIntegrada","integ.mp3");Set(f,"integCambios",2L);
        Call(f,"SaveIntegCache");Wait(f);
        Check(File.ReadAllText(p)==before+"1|-13|2|20|integ.mp3\r\n","integrada anexa sin compactar por registros de algoritmos futuros");
        integ.Remove("integ.mp3");Call(f,"MarcarIntegrada","integ.mp3");Set(f,"integCambios",3L);Call(f,"SaveIntegCache");Wait(f);
        string text=File.ReadAllText(p);
        Check(!text.Contains("|integ.mp3") && text.Contains("99|version-futura-1599"),"integrada compactada conserva TODOS los registros futuros");
        Set(f,"integEstado",2);integ["no-pisar"]=value;Call(f,"MarcarIntegrada","no-pisar");Set(f,"integCambios",4L);Call(f,"SaveIntegCache");Wait(f);
        Check(File.ReadAllText(p)==text,"cabecera futura/ilegible queda intacta");
    }
    static void Backup(string dir)
    {
        string a=Path.Combine(dir,"anexada.txt"),b=Path.Combine(dir,"reemplazada.txt"); File.WriteAllText(a,"sesion anterior\r\n");File.WriteAllText(b,"antes\r\n");
        object captured=Call(null,"CapturaRespaldo",(object)new string[]{a,b,Path.Combine(dir,"ausente")});
        try {
            File.AppendAllText(a,"sesion nueva\r\n");
            string replacement=b+".nuevo";File.WriteAllText(replacement,"despues\r\n");File.Replace(replacement,b,null);
            int yields=0;Action<string> cede=delegate(string r){yields++;};
            for(int n=0;n<2;n++) {
                string dest=Path.Combine(dir,"captura"+n);Call(null,"BackupToCapturado",dest,captured,cede);
                Check(File.ReadAllText(Path.Combine(dest,"anexada.txt"))=="sesion anterior\r\n" && File.ReadAllText(Path.Combine(dest,"reemplazada.txt"))=="antes\r\n","respaldo "+n+" conserva generacion capturada, excluye append/reemplazo posterior");
            }
            Check(yields>0,"copia y verificacion consultan presupuesto por bloque");
        } finally {((IDisposable)captured).Dispose();}
        captured=Call(null,"CapturaRespaldo",(object)new string[]{a});
        try {
            using(var stream=new FileStream(a,FileMode.Open,FileAccess.Write,FileShare.ReadWrite|FileShare.Delete))stream.SetLength(1);
            bool failed=false;string dest=Path.Combine(dir,"truncado");
            try {Call(null,"BackupToCapturado",dest,captured,null);}catch(TargetInvocationException){failed=true;}
            Check(failed && !Directory.Exists(dest) && Directory.GetDirectories(dir,".rocola_backup_pending_*").Length==0,"origen truncado no publica respaldo incompleto ni destruye anteriores");
        } finally {((IDisposable)captured).Dispose();}
    }
    static IList CapturedFiles(object capture) { return (IList)capture.GetType().GetField("Archivos", B).GetValue(capture); }
    static FileStream CapturedReader(object capture)
    {
        object record=CapturedFiles(capture)[0];
        return (FileStream)record.GetType().GetField("Lector", B).GetValue(record);
    }
    static object CaptureWithoutWaiting(string path, ManualResetEvent release)
    {
        object captured=null;Exception failure=null;Stopwatch timer=Stopwatch.StartNew();
        Thread captureThread=new Thread(delegate() {
            try {captured=Call(null,"CapturaRespaldo",(object)new string[]{path});}
            catch(Exception ex) {failure=ex;}
        });
        captureThread.Start();bool finished=captureThread.Join(1500);
        if(!finished) {release.Set();captureThread.Join();if(captured!=null)((IDisposable)captured).Dispose();}
        if(failure!=null)throw failure;
        Check(finished,"captura termina mientras el escritor conserva su candado largo");
        timer.Stop();Console.WriteLine("Contencion sintetica: captura "+timer.Elapsed.TotalMilliseconds.ToString("0.000",CultureInfo.InvariantCulture)+" ms; escritor seguia pausado");
        return captured;
    }
    static void BackupContention(string dir)
    {
        string path=Path.Combine(dir,"contendida.txt");File.WriteAllText(path,"previa\r\n",new UTF8Encoding(false));
        Check(!Codigo.Llama(F.GetMethod("CapturaRespaldo",B),"CandadoDe")
            && Codigo.Llama(F.GetMethod("PublicaGuardadoSeguro",B),"ProtegeRespaldo")
            && Codigo.Llama(F.GetMethod("AnexaSeguro",B),"ProtegeRespaldo")
            && Codigo.Llama(F.GetNestedType("DiarioTabla",B).GetMethod("Anade",B),"ProtegeRespaldo"),
            "captura no usa candado largo y los tres caminos escritores reservan generacion");
        using(var ready=new ManualResetEvent(false))
        using(var release=new ManualResetEvent(false))
        {
            Exception workerFailure=null;
            Func<TextWriter,bool> format=delegate(TextWriter w) {w.Write("nueva\r\n");ready.Set();release.WaitOne();return true;};
            Thread writer=new Thread(delegate() {try{Call(null,"EscribeFlujoSeguro",path,format,null);}catch(Exception ex){workerFailure=ex;ready.Set();}});
            writer.Start();ready.WaitOne();object captured=null;Microsoft.Win32.SafeHandles.SafeFileHandle handle=null;
            try
            {
                captured=CaptureWithoutWaiting(path,release);handle=CapturedReader(captured).SafeFileHandle;
                Check(CapturedFiles(captured).Count==1,"formato/volcado pausado conserva original sin esperar");
            }
            finally {release.Set();writer.Join();}
            try
            {
                if(workerFailure!=null)throw workerFailure;
                string dest=Path.Combine(dir,"contenido-formato");Call(null,"BackupToCapturado",dest,captured,null);
                Check(File.ReadAllText(Path.Combine(dest,"contendida.txt"))=="previa\r\n","tras publicar se respalda la generacion previa exacta");
            }
            finally {if(captured!=null)((IDisposable)captured).Dispose();}
            Check(handle.IsClosed,"ultimo propietario cierra el handle anterior");
        }
        File.WriteAllText(path,"entera\r\n",new UTF8Encoding(false));
        using(var ready=new ManualResetEvent(false))
        using(var release=new ManualResetEvent(false))
        {
            Exception workerFailure=null;Thread writer=new Thread(delegate() {
                try
                {
                    lock(Call(null,"CandadoDe",path))
                    using((IDisposable)Call(null,"ProtegeRespaldo",path))
                    using(var stream=new FileStream(path,FileMode.Open,FileAccess.Write,FileShare.Read))
                    {
                        stream.Position=stream.Length;byte[] partial=Encoding.UTF8.GetBytes("registro par");
                        stream.Write(partial,0,partial.Length);stream.Flush();ready.Set();release.WaitOne();
                        throw new IOException("fallo simulado durante anexo");
                    }
                }
                catch(IOException) {}
                catch(Exception ex){workerFailure=ex;ready.Set();}
            });
            writer.Start();ready.WaitOne();object first=null,second=null;FileStream reader=null;Microsoft.Win32.SafeHandles.SafeFileHandle handle=null;
            try
            {
                first=CaptureWithoutWaiting(path,release);
                second=Call(null,"CapturaRespaldo",(object)new string[]{path});reader=CapturedReader(first);handle=reader.SafeFileHandle;
                Check(ReferenceEquals(reader,CapturedReader(second)),"capturas concurrentes retienen la misma generacion reservada");
                string dest=Path.Combine(dir,"anexo-en-curso");Call(null,"BackupToCapturado",dest,first,null);
                Check(File.ReadAllText(Path.Combine(dest,"contendida.txt"))=="entera\r\n","anexo parcial nunca entra en captura mientras Write/Flush sigue activo");
                ((IDisposable)first).Dispose();((IDisposable)first).Dispose();first=null;
                Check(!handle.IsClosed,"liberar una captura no cierra el handle del escritor ni otra captura");
            }
            finally {release.Set();writer.Join();if(first!=null)((IDisposable)first).Dispose();}
            try
            {
                if(workerFailure!=null)throw workerFailure;
                Check(!handle.IsClosed,"fallo del escritor no invalida captura retenida");
                string dest=Path.Combine(dir,"anexo-tras-fallo");Call(null,"BackupToCapturado",dest,second,null);
                Check(File.ReadAllText(Path.Combine(dest,"contendida.txt"))=="entera\r\n","despues del fallo sigue la generacion completa sin registro parcial");
            }
            finally {if(second!=null)((IDisposable)second).Dispose();}
            Check(handle.IsClosed,"sin escritor ni capturas no quedan handles retenidos");
        }
        // La reparacion debe conservar bytes originales para una captura ya abierta, sin SetLength.
        byte[] corrupt=Encoding.UTF8.GetBytes("canci\u00f3n anterior\r\nregistro cortado");
        File.WriteAllBytes(path,corrupt);object old=Call(null,"CapturaRespaldo",(object)new string[]{path});
        try
        {
            Call(null,"AnexaSeguro",path,"a\u00f1adida\r\n");
            string dest=Path.Combine(dir,"antes-reparacion");Call(null,"BackupToCapturado",dest,old,null);
            Check(Convert.ToBase64String(File.ReadAllBytes(Path.Combine(dest,"contendida.txt")))==Convert.ToBase64String(corrupt),
                "reparar cola corrupta no trunca ni altera bytes de la generacion capturada");
            Check(File.ReadAllText(path)=="canci\u00f3n anterior\r\na\u00f1adida\r\n" && Convert.ToBase64String(File.ReadAllBytes(path+".bak"))==Convert.ToBase64String(corrupt),
                "reparacion publica solo lineas sanas con el mismo UTF8 y conserva original en bak");
        }
        finally{((IDisposable)old).Dispose();}
        string missing=Path.Combine(dir,"inicialmente-ausente.txt");
        using((IDisposable)Call(null,"ProtegeRespaldo",missing))
        {
            File.WriteAllText(missing,"empezando",new UTF8Encoding(false));
            object empty=Call(null,"CapturaRespaldo",(object)new string[]{missing});
            try{Check(CapturedFiles(empty).Count==0,"archivo creado a medias no inventa una generacion previa");}
            finally{((IDisposable)empty).Dispose();}
        }
        // Simula el intervalo del fallback FAT32; el nombre desaparece pero el handle no.
        File.WriteAllText(path,"antes del cambio\r\n",new UTF8Encoding(false));
        object protection=Call(null,"ProtegeRespaldo",path);object retained=null;Microsoft.Win32.SafeHandles.SafeFileHandle held=null;
        try
        {
            string previous=path+".prev";File.Move(path,previous);
            retained=Call(null,"CapturaRespaldo",(object)new string[]{path});held=CapturedReader(retained).SafeFileHandle;
            Check(CapturedFiles(retained).Count==1,"fallback original a bak no omite el archivo durante la brecha");
            File.WriteAllText(path,"reemplazo\r\n",new UTF8Encoding(false));
        }
        finally{((IDisposable)protection).Dispose();}
        try
        {
            string dest=Path.Combine(dir,"brecha-reemplazo");Call(null,"BackupToCapturado",dest,retained,null);
            Check(File.ReadAllText(Path.Combine(dest,"contendida.txt"))=="antes del cambio\r\n","referencia previa permanece tras cerrar proteccion de reemplazo");
        }
        finally{if(retained!=null)((IDisposable)retained).Dispose();}
        Check(held.IsClosed,"captura de reemplazo tambien cierra el ultimo handle");
        Call(null,"AnexaSeguro",missing,"\r\ncompleta\r\n");
        Check(File.ReadAllText(missing)=="\r\ncompleta\r\n","anexo posterior repara un archivo creado/cortado sin perder formato");
    }
    static int Main(string[] args)
    {
        F=Assembly.LoadFrom(args[0]).GetType("Rocola.Form1",true);
        string dir=Path.Combine(Path.GetTempPath(),"rocola_deltas_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        object previous=Get(null,"carpetaDatos");Set(null,"carpetaDatos",dir);
        try {Volume(dir);EqAndInteg();Backup(dir);BackupContention(dir);Console.WriteLine("PASS "+checks+" comprobaciones de datos/deltas/respaldo");return 0;}
        catch(Exception ex){Console.WriteLine("FAIL "+ex);return 1;}
        finally{Set(null,"carpetaDatos",previous);Directory.Delete(dir,true);}
    }
}
