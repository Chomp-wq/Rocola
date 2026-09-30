using System;
using System.Reflection;
using NAudio.Wave;
class LatenciaAdaptable
{
    const BindingFlags B=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static int checks;
    static void Check(bool c,string s){checks++;if(!c)throw new Exception(s);Console.WriteLine("OK "+s);}
    static int Sugerida(object p){return (int)p.GetType().GetProperty("Sugerida",B).GetValue(p,null);}
    static void Observa(object p,int segundo,double costo,double atraso,int ms,bool consumidor){
        long ticks=(segundo+1L)*System.Diagnostics.Stopwatch.Frequency;
        p.GetType().GetMethod("Observa",B).Invoke(p,new object[]{ticks,1000.0,costo,atraso,ms,consumidor});
    }
    sealed class Fuente:ISampleProvider {
        int pos;public WaveFormat WaveFormat{get{return WaveFormat.CreateIeeeFloatWaveFormat(44100,2);}}
        public int Read(float[] b,int o,int n){n=Math.Min(n,88200-pos);for(int i=0;i<n;i++)b[o+i]=(pos+i-40000)/100000f;pos+=n;return n;}
    }
    static int Main(string[] args){
        Assembly a=Assembly.LoadFrom(args[0]);Type perfil=a.GetType("Rocola.PerfilLatenciaAudio",true);
        foreach(bool clasica in new bool[]{true,false}){
            object p=Activator.CreateInstance(perfil,B,null,new object[]{clasica},null);
            int inicial=clasica?1000:300,medio=clasica?750:225,minimo=clasica?500:150;
            Check(Sugerida(p)==inicial,"Inicio conservador "+inicial);
            for(int i=0;i<30;i++)Observa(p,i,1,0,inicial,true);
            Check(Sugerida(p)==inicial,"No baja antes de 30 segundos reales");
            Observa(p,30,1,0,inicial,true);Check(Sugerida(p)==medio,"Baja solo un escalon estable");
            for(int i=31;i<90;i++)Observa(p,i,1,0,inicial,true);
            Check(Sugerida(p)==medio,"No sigue bajando sin haber aplicado la latencia intermedia");
            for(int i=90;i<=121;i++)Observa(p,i,1,0,medio,true);
            Check(Sugerida(p)==minimo,"Segunda apertura estable alcanza minimo acotado");
            Observa(p,122,1,minimo,minimo,true);Check(Sugerida(p)==inicial,"Atasco de callback devuelve margen conservador");
            for(int i=123;i<=154;i++)Observa(p,i,1,0,inicial,false);
            Check(Sugerida(p)==inicial,"Precarga del productor no cuenta como reproduccion estable");
            Observa(p,155,700,0,inicial,false);Check(Sugerida(p)==inicial,"Pico DSP invalida estabilidad sin reducir calidad");
        }
        object policy=Activator.CreateInstance(perfil,B,null,new object[]{true},null);
        Type medidor=a.GetType("Rocola.FuenteLatenciaAudio",true);
        ISampleProvider m=(ISampleProvider)Activator.CreateInstance(medidor,B,null,new object[]{new Fuente(),policy,1000,true},null);
        ISampleProvider r=new Fuente();float[] x=new float[1031],y=new float[1031];int total=0,n;
        while((n=m.Read(x,2,1027))>0){Check(r.Read(y,2,1027)==n,"Duracion PCM identica por trozo");for(int i=2;i<n+2;i++)if(x[i]!=y[i])throw new Exception("PCM alterado");total+=n;}
        Check(total==88200,"Medidor no altera ni pierde PCM con offsets/cuentas impares");
        Type bomba=a.GetType("Rocola.BombaSonido",true);
        object pump=Activator.CreateInstance(bomba,B,null,new object[]{new Fuente(),500,300},null);
        Check((int)bomba.GetField("ArranqueObjetivoMs",B).GetValue(pump)==500 && (int)bomba.GetField("EsperaLecturaMs",B).GetValue(pump)==300,"Precarga y espera siguen el bufer realmente abierto");
        ((IDisposable)pump).Dispose();
        // Un productor atascado puede entregar ceros sin aumentar Huecos otra vez.
        // No deben acreditarse como 30 segundos de musica estable.
        object parada=Activator.CreateInstance(bomba,B,null,new object[]{new Fuente(),500,300},null);
        bomba.GetField("hilo",B).SetValue(parada,new System.Threading.Thread(delegate(){}));
        bomba.GetField("atascada",B).SetValue(parada,true);
        for(int i=0;i<=31;i++)Observa(policy,i,1,0,1000,true);
        ISampleProvider vigilada=(ISampleProvider)Activator.CreateInstance(medidor,B,null,new object[]{parada,policy,750,true},null);
        Check(vigilada.Read(x,2,100)==100 && Sugerida(policy)==1000,"Ceros temporales reinician el margen aunque Huecos no cambie");
        perfil.GetField("musicaEstable",B).SetValue(policy,29999.0);
        vigilada.Read(x,2,100);
        Check((double)perfil.GetField("musicaEstable",B).GetValue(policy)==0,"Productor aun atascado no vuelve a sumar estabilidad");
        ((IDisposable)parada).Dispose();
        Console.WriteLine("PASS latencia: "+checks+" comprobaciones sinteticas, no mide latencia fisica del DAC");return 0;
    }
}
