using System; using System.Reflection;
// PRUEBA DE QUE NO CAMBIO NADA: se llama a AurBrillo y AurMalla en las DOS versiones del ejecutable con
// entradas identicas y se comparan los vertices que producen, campo por campo. Si sale un solo numero
// distinto, el dibujo cambiaria.
class Igual {
    static int fallos=0;
    static void Check(bool ok,string q){ Console.WriteLine((ok?"  OK   ":"  FALLO")+"  "+q); if(!ok) fallos++; }
    static Type Buscar(Assembly a,string n){ foreach(Type t in a.GetTypes()) if(t.Name==n) return t; return null; }
    static Array Corre(Assembly asm,string metodo,int semilla,out int n){
        Type tp=Buscar(asm,"GpuPanel"), td=Buscar(asm,"D3D9"), tv=null;
        foreach(Type t in td.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)) if(t.Name=="Vtx") tv=t;
        MethodInfo m=tp.GetMethod(metodo,BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        int cols=144, puntos=cols+1;
        Random r=new Random(semilla);
        if(metodo=="AurBrillo"){
            FieldInfo fN=tp.GetField("AurBrilloVtx",BindingFlags.Static|BindingFlags.NonPublic);
            int nv=(int)fN.GetValue(null);
            Array v=Array.CreateInstance(tv,nv);
            float[] h=new float[puntos], n2=new float[puntos], g2=new float[puntos];
            uint[] cn=new uint[puntos], cl=new uint[puntos];
            for(int i=0;i<puntos;i++){ h[i]=(float)r.NextDouble()*220f; n2[i]=(float)r.NextDouble();
                g2[i]=(float)r.NextDouble(); cn[i]=(uint)r.Next()|0xFF000000u; cl[i]=(uint)r.Next()|0xFF000000u; }
            n=(int)m.Invoke(null,new object[]{v,h,cn,n2,cl,g2,1024f,660f});
            return v;
        } else {
            int nv=(6+4)*cols*6;
            Array v=Array.CreateInstance(tv,nv);
            float[] h=new float[puntos], a=new float[puntos], r2=new float[puntos];
            uint[] c1=new uint[(6+1)*puntos], c2=new uint[(4+1)*puntos];
            for(int i=0;i<puntos;i++){ h[i]=(float)r.NextDouble()*220f; a[i]=(float)r.NextDouble(); r2[i]=(float)r.NextDouble(); }
            for(int i=0;i<c1.Length;i++) c1[i]=(uint)r.Next()|0xFF000000u;
            for(int i=0;i<c2.Length;i++) c2[i]=(uint)r.Next()|0xFF000000u;
            n=(int)m.Invoke(null,new object[]{v,h,c1,c2,a,r2,1024f,660f,768f});
            return v;
        }
    }
    static int Main(){
        Assembly viejo=Assembly.LoadFrom("rocola.exe"), nuevo=Assembly.LoadFrom("rocola_nuevo.exe");
        string[] metodos={"AurBrillo","AurMalla"};
        foreach(string met in metodos){
            Console.WriteLine("\n  "+met+"\n");
            int distintos=0, total=0; string primero=null;
            for(int semilla=1;semilla<=8;semilla++){
                int nv1,nv2;
                Array a=Corre(viejo,met,semilla,out nv1);
                Array b=Corre(nuevo,met,semilla,out nv2);
                if(nv1!=nv2){ Console.WriteLine("     semilla "+semilla+": producen distinto numero de vertices ("+nv1+" vs "+nv2+")"); distintos++; continue; }
                Type tv1=a.GetType().GetElementType(), tv2=b.GetType().GetElementType();
                FieldInfo[] f1=tv1.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                FieldInfo[] f2=tv2.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                for(int i=0;i<nv1;i++){
                    object v1=a.GetValue(i), v2=b.GetValue(i);
                    for(int k=0;k<f1.Length;k++){
                        object x=f1[k].GetValue(v1), y=f2[k].GetValue(v2);
                        total++;
                        if(!x.Equals(y)){ distintos++;
                            if(primero==null) primero="vertice "+i+", campo "+f1[k].Name+": "+x+" vs "+y; }
                    }
                }
            }
            Console.WriteLine("     numeros comparados: "+total+"   distintos: "+distintos);
            if(primero!=null) Console.WriteLine("     primero: "+primero);
            Check(distintos==0,met+" produce EXACTAMENTE los mismos vertices que antes");
        }
        Console.WriteLine(fallos==0?"\nTODO BIEN":"\nFALLOS: "+fallos);
        return fallos; }
}
