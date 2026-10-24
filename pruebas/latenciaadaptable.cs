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
            // Desde el 24-10-2026 la salida clasica no baja de 750 ms (la rellena la pantalla, que se para hasta 766 ms): su minimo es el medio.
            int inicial=clasica?1000:300,medio=clasica?750:225,minimo=clasica?750:150;
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
        {   // 24-10-2026: el hilo de la musica (no consumidor) lee trozos de 25 ms y tiene 200 ms de colchon. Pasarse 15-20 ms (un tic de XP)
            // ya no es un atasco; pasarse de la mitad del colchon, si.
            object q=Activator.CreateInstance(perfil,B,null,new object[]{true},null);
            for(int i=0;i<=30;i++)Observa(q,i,1,0,1000,true);
            Check(Sugerida(q)==750,"Bajo a 750 tras 30 s estables");
            MethodInfo obs=perfil.GetMethod("Observa",B);
            obs.Invoke(q,new object[]{31L*System.Diagnostics.Stopwatch.Frequency,25.0,17.0,0.0,750,false});
            obs.Invoke(q,new object[]{32L*System.Diagnostics.Stopwatch.Frequency,25.0,60.0,0.0,750,false});
            Check(Sugerida(q)==750,"Trozo de 25 ms del hilo de la musica que tarda 17 o 60 ms: no es un atasco (tiene 200 ms de colchon)");
            obs.Invoke(q,new object[]{33L*System.Diagnostics.Stopwatch.Frequency,25.0,130.0,0.0,750,false});
            Check(Sugerida(q)==1000,"El que tarda 130 ms (mas de la mitad del colchon) si sube el margen");
            for(int i=34;i<=200;i++)Observa(q,i,1,0,Sugerida(q),true);
            Check(Sugerida(q)==750,"Y la salida clasica nunca baja de 750 ms");
        }
        {   // La caja negra (22-10-2026): cada cambio de nivel queda anotado (en numeros, sin reservar nada en la musica) para rocola_error.txt.
            object cp=Activator.CreateInstance(perfil,B,null,new object[]{true},null);
            Func<object,string> toma=delegate(object o){return (string)perfil.GetMethod("TomaCambios",B).Invoke(o,null);};
            Func<object,bool> hay=delegate(object o){return (bool)perfil.GetProperty("HayCambios",B).GetValue(o,null);};
            Check(!hay(cp)&&toma(cp)==null,"Sin cambios de nivel no hay nada que anotar");
            for(int i=0;i<=29;i++)Observa(cp,i,1,0,1000,true);
            Check(!hay(cp),"Sin cambio de nivel (antes de los 30 s) sigue sin haber nada");
            Observa(cp,30,1,0,1000,true);
            Check(hay(cp),"Al bajar un escalon se avisa a la pantalla");
            string t1=toma(cp);
            Check(t1!=null&&t1.Contains("baja el margen de 1000 a 750 ms")&&t1.Contains("30 s de musica sin atascos"),"Se anota la bajada con su motivo: "+t1);
            Check(!hay(cp)&&toma(cp)==null,"Una vez tomado no se repite");
            Observa(cp,31,1,700,750,true);
            string t2=toma(cp);
            Check(t2!=null&&t2.Contains("sube el margen de 750 a 1000 ms")&&t2.Contains("atasco de 700 ms"),"Se anota la subida por un atasco, con su tamano: "+t2);
            Observa(cp,32,1,700,1000,true);
            Check(!hay(cp),"Un atasco en el nivel de partida no cambia nada y no se anota");
            for(int i=33;i<=63;i++)Observa(cp,i,1,0,1000,true);
            toma(cp);
            perfil.GetMethod("Fallo",B).Invoke(cp,null);
            string t3=toma(cp);
            Check(t3!=null&&t3.Contains("fallo o corte de la salida"),"Un fallo de la salida que sube el margen tambien se anota: "+t3);
            perfil.GetMethod("Fallo",B).Invoke(cp,null);
            Check(!hay(cp),"Otro fallo con el margen ya arriba no anota nada");
            // Muchos cambios seguidos: la lista tiene tope y lo dice, y no reserva sin limite.
            for(int k=0;k<12;k++){for(int i=0;i<=31;i++)Observa(cp,64+k*40+i,1,0,1000,true);Observa(cp,64+k*40+32,1,700,750,true);}
            string t4=toma(cp);
            Check(t4!=null&&t4.Contains("cambios mas sin anotar"),"Con la lista llena dice cuantos faltaron: "+t4.Substring(Math.Max(0,t4.Length-40)));
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
        {   // Los avisos tardios de la salida se cuentan (solo del lado de la salida, el consumidor).
            object p2=Activator.CreateInstance(perfil,B,null,new object[]{true},null);
            object m2=Activator.CreateInstance(medidor,B,null,new object[]{new Fuente(),p2,1000,true},null);
            float[] bb=new float[200];
            ((ISampleProvider)m2).Read(bb,2,100);System.Threading.Thread.Sleep(450);((ISampleProvider)m2).Read(bb,2,100);
            Check((int)medidor.GetProperty("Atrasos",B).GetValue(m2,null)==1&&(int)medidor.GetProperty("MaxAtrasoMs",B).GetValue(m2,null)>=400,"Un aviso de la salida que llega 450 ms tarde con 1000 ms de latencia se cuenta (y el peor)");
            ((ISampleProvider)m2).Read(bb,2,100);
            Check((int)medidor.GetProperty("Atrasos",B).GetValue(m2,null)==1,"Uno puntual no se cuenta dos veces");
            object m3=Activator.CreateInstance(medidor,B,null,new object[]{new Fuente(),p2,1000,false},null);
            ((ISampleProvider)m3).Read(bb,2,100);System.Threading.Thread.Sleep(450);((ISampleProvider)m3).Read(bb,2,100);
            Check((int)medidor.GetProperty("Atrasos",B).GetValue(m3,null)==0,"El medidor del productor (no consumidor) no cuenta avisos de la salida");
        }
        Console.WriteLine("PASS latencia: "+checks+" comprobaciones sinteticas, no mide latencia fisica del DAC");return 0;
    }
}
