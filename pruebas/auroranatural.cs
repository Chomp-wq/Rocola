using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Forms;
using System.Runtime.Serialization;

class AuroraNaturalTest
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    delegate uint Velo(uint rgb, int row, float x, float time, float ray);
    delegate int Mesh(object vertices, float[] height, uint[] curtain, uint[] floor, float[] alpha, float[] rays, float width, float baseY, float panelHeight);
    static Assembly app, baseline;
    static Type panel, vertex;
    static Velo veil;
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static object Get(object value, string name) { return value.GetType().GetField(name, All).GetValue(value); }
    static void Set(object value, string name, object data) { value.GetType().GetField(name, All).SetValue(value, data); }
    static object Run(string name, params object[] values) { return panel.GetMethod(name, All).Invoke(null, values); }
    static Mesh Bridge(Assembly assembly, string name)
    {
        Type p = assembly.GetType("Rocola.GpuPanel", true);
        MethodInfo method = p.GetMethod(name, All);
        Type[] signature = { typeof(object), typeof(float[]), typeof(uint[]), typeof(uint[]), typeof(float[]), typeof(float[]), typeof(float), typeof(float), typeof(float) };
        DynamicMethod bridge = new DynamicMethod("AuroraTest" + name, typeof(int), signature, p.Module, true);
        ILGenerator il = bridge.GetILGenerator();
        ParameterInfo[] parameters = method.GetParameters();
        for (int i = 0; i < signature.Length; i++)
        {
            il.Emit(OpCodes.Ldarg, (short)i);
            if (i == 0) il.Emit(OpCodes.Castclass, parameters[0].ParameterType);
        }
        il.Emit(OpCodes.Call, method); il.Emit(OpCodes.Ret);
        return (Mesh)bridge.CreateDelegate(typeof(Mesh));
    }
    static void Profiles()
    {
        float previous = -1f;
        for (int row = 0; row <= 6; row++)
        {
            float level = (float)Run("AuroraNaturalNivel", row);
            Check(level > previous && level >= 0 && level <= 1, "Niveles no crecientes");
            previous = level;
            foreach (float x in new float[] { 0f, .125f, .47f, 1f })
                foreach (float time in new float[] { 0f, 1f, 29f, 628.31853f })
                {
                    uint c = veil(0x652CC9u, row, x, time, 1.2f);
                    Check((c & 0xFFFFFFu) == 0x652CC9u, "Velo cambia la paleta");
                    Check((c >> 24) >= 1 && (c >> 24) <= 255, "Velo fuera de rango");
                    if (row == 0 || row == 6) Check((c >> 24) == 255, "Horizonte/punta afectados por mascara");
                }
        }
        Check((float)Run("AuroraNaturalNivel", -1) == 0 && (float)Run("AuroraNaturalNivel", 99) == 1, "Niveles fuera de rango");
        int differingHeights = 0, differingTimes = 0, maxJump = 0;
        for (int i = 0; i <= 144; i++) for (int row = 1; row < 6; row++)
        {
            float x = i / 144f;
            int first = (int)(veil(0u, row, x, 10f, 1f) >> 24);
            int after = (int)(veil(0u, row, x, 10f + 1f / 24f, 1f) >> 24);
            maxJump = Math.Max(maxJump, Math.Abs(first - after));
            if (veil(0u, row, x, 10f, 1f) != veil(0u, row + 1, x, 10f, 1f)) differingHeights++;
            if (veil(0u, row, x, 10f, 1f) != veil(0u, row, x, 20f, 1f)) differingTimes++;
        }
        Check(differingHeights > 144 * 4, "Vetas identicas por altura: sigue siendo una raya");
        Check(differingTimes > 144 * 4, "Velo no evoluciona");
        Check(maxJump <= 2, "Velo salta incluso a 24 FPS");
        foreach(float unusual in new float[]{float.NaN,float.NegativeInfinity,float.PositiveInfinity,-20f,1000000f})
            for(int row=1;row<6;row++) Check((veil(0x7AFF23u,row,unusual,unusual,unusual)>>24)>=1,"Valores no finitos");
        foreach(string method in new string[]{"AuroraNaturalNivel","AuroraNaturalOnda","AuroraNaturalVelo","AuroraNaturalPeso","AurMallaEstilo"})
            foreach(Codigo.Paso instruction in Codigo.Pasos(panel.GetMethod(method,All)))
                Check(instruction.Op != 0x8D && instruction.Op != 0x73, method + " asigna memoria en la ruta caliente");
    }
    static void Compare(Array expected, Array actual, string name)
    {
        foreach(string field in new string[]{"X","Y","Z","Rhw","Color","U","V"})
        {
            FieldInfo oldField = expected.GetType().GetElementType().GetField(field, All);
            FieldInfo newField = actual.GetType().GetElementType().GetField(field, All);
            for(int i=0;i<actual.Length;i++)
                Check(oldField.GetValue(expected.GetValue(i)).Equals(newField.GetValue(actual.GetValue(i))), name+" difiere: "+i+"/"+field);
        }
    }
    static void Geometry()
    {
        float[] heights=new float[145], alpha=new float[145], rays=new float[145];
        uint[] curtain=new uint[7*145], floor=new uint[5*145];
        for(int i=0;i<145;i++)
        {
            heights[i]=120+(float)Math.Sin(i*.08)*75; alpha[i]=.42f+i*.002f; rays[i]=.7f+i*.003f;
            for(int row=0;row<7;row++) curtain[row*145+i]=((uint)(0x174091+i*522+row*117)&0xFFFFFFu);
            for(int row=0;row<5;row++) floor[row*145+i]=((uint)(0x174091+i*522+row*117)&0xFFFFFFu);
        }
        Mesh old=Bridge(baseline,"AurMalla"), legacy=Bridge(app,"AurMalla"), natural=Bridge(app,"AurMallaNatural");
        Array oldVertices=Array.CreateInstance(baseline.GetType("Rocola.D3D9+Vtx",true),8640);
        Array vertices=Array.CreateInstance(vertex,8640);
        foreach(int width in new int[]{640,1024,3440}) foreach(float baseY in new float[]{300,560,1180})
        {
            Check(old(oldVertices,heights,curtain,floor,alpha,rays,width,baseY,1440)==8640,"Presupuesto viejo");
            Check(legacy(vertices,heights,curtain,floor,alpha,rays,width,baseY,1440)==8640,"Presupuesto Legacy");
            Compare(oldVertices,vertices,"Legacy");
            for(int row=0;row<7;row++)for(int i=0;i<145;i++)
                curtain[row*145+i]=veil(curtain[row*145+i],row,i/144f,16f,rays[i]);
            Check(natural(vertices,heights,curtain,floor,alpha,rays,width,baseY,1440)==8640,"Presupuesto Natural");
            Check(Get(vertices.GetValue(0),"Color").Equals(Get(vertices.GetValue(4*144*6+2),"Color")),"Salto suelo/cortina");
            Check((float)Get(vertices.GetValue(0),"Y")==baseY-.5f,"Horizonte separado");
            foreach(object point in vertices)
            {
                float x=(float)Get(point,"X"),y=(float)Get(point,"Y");
                Check(!float.IsNaN(x)&&!float.IsInfinity(x)&&x>=-.5f&&x<=width+.5f,"Vertice X");
                Check(!float.IsNaN(y)&&!float.IsInfinity(y)&&y>=0&&y<=1440,"Vertice Y");
            }
            // La mascara pertenece exclusivamente a Natural; el siguiente caso
            // vuelve a proporcionar los RGB Legacy originales.
            for(int i=0;i<curtain.Length;i++)curtain[i]&=0xFFFFFFu;
        }
        const int iterations=3000;
        for(int i=0;i<60;i++)natural(vertices,heights,curtain,floor,alpha,rays,3440,1180,1440);
        Stopwatch clock=Stopwatch.StartNew();
        for(int i=0;i<iterations;i++)legacy(vertices,heights,curtain,floor,alpha,rays,3440,1180,1440);
        clock.Stop(); double legacyMs=clock.Elapsed.TotalMilliseconds/iterations;
        clock.Reset(); clock.Start();
        for(int i=0;i<iterations;i++)natural(vertices,heights,curtain,floor,alpha,rays,3440,1180,1440);
        clock.Stop(); double naturalMs=clock.Elapsed.TotalMilliseconds/iterations;
        clock.Reset(); clock.Start();
        for(int round=0;round<iterations;round++)for(int row=1;row<6;row++)for(int i=0;i<145;i++)
            curtain[row*145+i]=veil(curtain[row*145+i],row,i/144f,round*.016f,rays[i]);
        clock.Stop(); double veilMs=clock.Elapsed.TotalMilliseconds/iterations;
        Console.WriteLine("COSTE aislado: Legacy="+legacyMs.ToString("0.000")+" ms/malla, Natural="+naturalMs.ToString("0.000")+" ms/malla, 725 mascaras="+veilMs.ToString("0.000")+" ms/muestra; no son FPS reales.");
    }
    static void LegacyDraw()
    {
        Type oldPanel=baseline.GetType("Rocola.GpuPanel",true);
        using(Control expected=(Control)Activator.CreateInstance(oldPanel,true))
        using(Control actual=(Control)Activator.CreateInstance(panel,true))
        {
            Control[] controls={expected,actual}; Assembly[] assemblies={baseline,app};
            foreach(float baseY in new float[]{672f,560f,510f})
            {
                for(int i=0;i<2;i++)
                {
                    Control p=controls[i]; Type type=p.GetType(),waveType=assemblies[i].GetType("Rocola.GpuAurora",true);
                    p.Size=new System.Drawing.Size(1024,768);
                    Set(p,"stars",FormatterServices.GetUninitializedObject(assemblies[i].GetType("Rocola.GpuStarfield",true)));
                    Set(p,"FondoNatural",false); Set(p,"waveNatural",true); Set(p,"aurRelojOk",false); Set(p,"aurT",18d);
                    uint[] bands=(uint[])Get(p,"coverBands"),palette=(uint[])Get(p,"flowColors");
                    for(int k=0;k<bands.Length;k++)bands[k]=(uint)(0x1749CC+k*2654)&0xFFFFFFu;
                    for(int k=0;k<palette.Length;k++)palette[k]=(uint)(0xEF54A8+k*357)&0xFFFFFFu;
                    Set(p,"coverWeight",.55f); Set(p,"flowBlend",.6f);
                    object wave=Activator.CreateInstance(waveType);
                    Set(wave,"Has",true); Set(wave,"B",new float[]{.4f,.7f,.5f,.9f,.3f,.7f,.8f,.6f});
                    Set(wave,"BaseY",baseY); Set(wave,"Alto",280f); Set(wave,"Dim",1f); Set(wave,"DimFilo",1f);
                    Set(wave,"Rgb",0x8833CCu); Set(wave,"Rgb2",0x44AA88u); Set(wave,"RgbA",0xDF76B1u); Set(wave,"Rgb2A",0x73CFEDu);
                    type.GetMethod("DrawAurora",All).Invoke(p,new object[]{wave});
                }
                foreach(string name in new string[]{"aurH","aurHs","aurA","aurR","aurCortina","aurSueloC","aurNi","aurLi","aurNc","aurLc"})
                {
                    Array a=(Array)Get(expected,name),b=(Array)Get(actual,name);
                    for(int i=0;i<a.Length;i++)Check(a.GetValue(i).Equals(b.GetValue(i)),"DrawAurora Legacy cambia "+name+"/"+i);
                }
                Compare((Array)Get(expected,"aurV"),(Array)Get(actual,"aurV"),"DrawAurora Legacy relleno");
                Compare((Array)Get(expected,"aurVb"),(Array)Get(actual,"aurVb"),"DrawAurora Legacy brillo");
            }
        }
    }
    static void WarmBuffers()
    {
        using(Control p=(Control)Activator.CreateInstance(panel,true))
        {
            p.Size=new System.Drawing.Size(1024,768);
            Type stars=app.GetType("Rocola.GpuStarfield",true), waveType=app.GetType("Rocola.GpuAurora",true);
            Set(p,"stars",FormatterServices.GetUninitializedObject(stars)); Set(p,"FondoNatural",true);
            Set(p,"OndasFps",24); Set(p,"waveNatural",false);
            object wave=Activator.CreateInstance(waveType); Set(wave,"Has",true); Set(wave,"B",new float[]{.4f,.7f,.5f,.9f,.3f,.7f,.8f,.6f});
            Set(wave,"BaseY",560f); Set(wave,"Alto",280f); Set(wave,"Dim",1f); Set(wave,"DimFilo",1f);
            Set(wave,"Rgb",0x8833CCu); Set(wave,"Rgb2",0x44AA88u);
            object[] values={wave}; MethodInfo draw=panel.GetMethod("DrawAurora",All);
            draw.Invoke(p,values);
            string[] names={"aurH","aurHs","aurA","aurR","aurCortina","aurSueloC","aurV","aurVb"};
            object[] buffers=new object[names.Length];
            for(int i=0;i<names.Length;i++)buffers[i]=Get(p,names[i]);
            for(int pass=0;pass<50;pass++)
            {
                Set(wave,"BaseY",560f-pass*2); Set(p,"waveNatural",false); draw.Invoke(p,values);
                for(int i=0;i<names.Length;i++)Check(ReferenceEquals(buffers[i],Get(p,names[i])),"Se reasigna "+names[i]);
                Array v=(Array)Get(p,"aurV"); float y=(float)Get(v.GetValue(0),"Y");
                Check(y==560f-pass*2-.5f,"Horizonte no sigue la barra entre muestras de 24 FPS");
            }
            Check(((Array)Get(p,"aurV")).Length==8640&&((Array)Get(p,"aurVb")).Length==7992,"Mallas crecen");
            Check(Codigo.Llama(draw,"SampleWave")&&Codigo.Llama(draw,"AuroraNaturalVelo"),"Cadencia/muestra del nuevo velo desconectada");
        }
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            app=Assembly.LoadFile(System.IO.Path.GetFullPath(args[0]));
            panel=app.GetType("Rocola.GpuPanel");
            if(panel==null){Console.WriteLine("OK CPU congelado: no hay aurora GPU");return 0;}
            baseline=args.Length>1?Assembly.LoadFile(System.IO.Path.GetFullPath(args[1])):app;
            vertex=app.GetType("Rocola.D3D9+Vtx",true);
            veil=(Velo)Delegate.CreateDelegate(typeof(Velo),panel.GetMethod("AuroraNaturalVelo",All));
            Profiles(); Geometry(); LegacyDraw(); WarmBuffers();
            Console.WriteLine("PASS "+checks+" comprobaciones de aurora natural; comparacion Legacy externa="+(args.Length>1));return 0;
        }
        catch(Exception e){Console.WriteLine("FALLO "+e);return 1;}
    }
}
