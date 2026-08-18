using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

// LOS DOS BUGS DEL FONDO, comprobados aqui para que no vuelvan:
//
//   1) La aurora APARECIA Y DESAPARECIA de golpe ("pop in"). Se dibujaba o no se dibujaba segun si la banda
//      mas alta pasaba de un pelo, asi que en cualquier pasaje bajito se borraba entera de un cuadro para
//      otro y volvia igual de golpe. Ahora hay una PRESENCIA que entra y sale despacio.
//
//   2) Las estrellas parecian PARARSE. Dos causas: la aurora se dibujaba ENCIMA y las tapaba en media
//      pantalla, y la deriva del campo miraba el pulso YA ATENUADO, asi que al navegar (que atenua el fondo)
//      el movimiento se congelaba. Aqui se comprueba el orden de dibujado leyendo el propio ejecutable.
class Presencia
{
    const int FS = 44100, CH = 2;
    static Type TF; static object F; static MethodInfo REP, AVZ;
    static object Fld(string n) { return TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(F); }
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }
    static float Pres() { return (float)Fld("auroraPres"); }
    static float Cte(string n)
    {
        FieldInfo f = TF.GetField(n, BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        object v = f.GetRawConstantValue();
        return (v is int) ? (int)v : (float)v;
    }
    static object BUILD2()
    {
        SetFld("bgDim", 1f);
        return TF.GetMethod("BuildAuroraState", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(F, null);
    }
    static float CalmaDe(object g) { return (float)g.GetType().GetField("Calma").GetValue(g); }
    // Lo que la rocola va a dibujar de aurora con una atenuacion de fondo dada: se le pregunta a ella misma.
    const float BgDimLevel = 0.45f;   // a lo que baja el fondo mientras alguien esta eligiendo cancion
    static float DimDe(MethodInfo build, float bgDim)
    {
        SetFld("bgDim", bgDim);
        object g = build.Invoke(F, null);
        return (float)g.GetType().GetField("Dim").GetValue(g);
    }
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const float Cuadro = 0.042f;   // el temporizador de animacion va a 42 ms (~24 cuadros por segundo)
    static string Seg(int cuadros) { return (cuadros * Cuadro).ToString("0.0") + " s"; }

    // Mete 'seg' segundos de un tono en la cadena y deja specNow con lo ultimo que se midio.
    static void Suena(double hz, double amp, double seg)
    {
        int n = (int)(seg * FS); float[] buf = new float[4096 * CH]; int hecho = 0;
        while (hecho < n)
        {
            int m = Math.Min(4096, n - hecho);
            for (int i = 0; i < m; i++) { float v = (float)(amp * Math.Sin(2*Math.PI*hz*(hecho+i)/FS)); buf[i*CH]=v; buf[i*CH+1]=v; }
            REP.Invoke(F, new object[] { buf, 0, m*CH, FS, CH });
            hecho += m;
        }
        object q = Fld("levelQ"); Type tq = q.GetType();
        int cnt = (int)tq.GetProperty("Count").GetValue(q, null);
        MethodInfo deq = tq.GetMethod("Dequeue"); object ult = null;
        for (int i = 0; i < cnt; i++) ult = deq.Invoke(q, null);
        if (ult != null)
        {
            float[] esp = (float[])ult.GetType().GetField("Esp").GetValue(ult);
            if (esp != null) Array.Copy(esp, (float[])Fld("specNow"), 8);
        }
    }
    static void Callate() { Array.Clear((float[])Fld("specNow"), 0, 8); }

    // Avanza n cuadros y devuelve el mayor SALTO de presencia que hubo entre dos cuadros seguidos.
    static float Avanza(int n, out float min, out float max)
    {
        float salto = 0f; min = 9f; max = -9f; float ant = Pres();
        for (int i = 0; i < n; i++)
        {
            AVZ.Invoke(F, null);
            float p = Pres();
            float d = Math.Abs(p - ant);
            if (d > salto) salto = d;
            if (p < min) min = p; if (p > max) max = p;
            ant = p;
        }
        return salto;
    }
    static float Avanza(int n) { float a, b; return Avanza(n, out a, out b); }

    // ---------------- lectura del propio ejecutable: ORDEN de dibujado ----------------
    // Recorre el codigo compilado de GpuStarfield.Begin y anota, en orden, a que se llama. Asi se comprueba
    // de verdad -no de palabra- que la aurora se pinta despues del cielo y ANTES de las estrellas.
    // Tamaño del operando de cada instruccion de un byte (lo justo para poder saltar de una a la siguiente).
    static int LargoOperando(int op)
    {
        if (op == 0x45)  return -1;                                   // switch: se trata aparte abajo
        if (op >= 0x2B && op <= 0x37) return 1;                       // saltos cortos
        if (op >= 0x38 && op <= 0x44) return 4;                       // saltos largos
        switch (op)
        {
            case 0x0E: case 0x10: case 0x11: case 0x12: case 0x13:    // ldarg.s / starg.s / ldloc.s...
            case 0x1F: case 0xDE: return 1;
            case 0x20: case 0x22: case 0x27: case 0x28: case 0x6F:
            case 0x72: case 0x73: case 0x74: case 0x75: case 0x79:
            case 0x7B: case 0x7C: case 0x7D: case 0x7E: case 0x7F: case 0x80:
            case 0x81: case 0x8C: case 0x8D: case 0x8F: case 0xA3: case 0xA4:
            case 0xA5: case 0xC2: case 0xC6: case 0xD0: case 0x29: return 4;
            case 0x21: case 0x23: return 8;
            default: return 0;
        }
    }
    // switch lleva un contador y tantos destinos como diga: hay que saltarlos todos o se pierde el hilo.
    static List<string> LlamadasSeguro(MethodInfo m)
    {
        List<string> r = new List<string>();
        MethodBody b = m.GetMethodBody(); if (b == null) return r;
        byte[] il = b.GetILAsByteArray(); if (il == null) return r;
        Module mod = m.Module;
        int i = 0;
        while (i < il.Length)
        {
            int op = il[i];
            if (op == 0xFE) { i += 2; continue; }
            if (op == 0x45) { int n = BitConverter.ToInt32(il, i + 1); i += 1 + 4 + 4 * n; continue; }
            int len = LargoOperando(op);
            if ((op == 0x28 || op == 0x6F) && i + 4 < il.Length)
            {
                int tok = BitConverter.ToInt32(il, i + 1);
                try { MethodBase mb = mod.ResolveMethod(tok); r.Add(mb.DeclaringType.Name + "." + mb.Name); } catch { }
            }
            i += 1 + len;
        }
        return r;
    }
    static int Pos(List<string> l, string q) { for (int i = 0; i < l.Count; i++) if (l[i] == q) return i; return -1; }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        F = FormatterServices.GetUninitializedObject(TF);
        SetFld("levelLock", new object()); SetFld("nowPlaying", null);
        SetFld("specVis", new float[8]); SetFld("specNow", new float[8]);
        SetFld("specPiso", new float[8]); SetFld("specGolpe", new float[8]);
        SetFld("auroraAmount", 0.55f); SetFld("bgDim", 1f);
        SetFld("levelQ", Activator.CreateInstance(TF.GetField("levelQ", BindingFlags.Instance|BindingFlags.NonPublic).FieldType));
        REP = TF.GetMethod("ReportAudioBlock", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        AVZ = TF.GetMethod("AvanzaAurora", BindingFlags.Instance|BindingFlags.NonPublic);
        if (AVZ == null || TF.GetField("auroraPres", BindingFlags.Instance|BindingFlags.NonPublic) == null)
        { Console.WriteLine("falta la presencia de la aurora"); return 1; }

        Console.WriteLine("\n  1) LA AURORA ENTRA POCO A POCO, NO DE GOLPE\n");
        Check(Pres() == 0f, "con la rocola recien abierta y sin musica, la aurora no esta (presencia 0)");

        Suena(220, 0.5, 0.6);
        int cuadrosHasta90 = -1; float saltoMax = 0f, ant = Pres();
        for (int i = 0; i < 200; i++)
        {
            AVZ.Invoke(F, null);
            float p = Pres();
            float d = p - ant; if (d > saltoMax) saltoMax = d;
            if (cuadrosHasta90 < 0 && p >= 0.90f) cuadrosHasta90 = i + 1;
            ant = p;
        }
        Console.WriteLine("     tarda " + Seg(cuadrosHasta90) + " en estar del todo, y el mayor salto de un cuadro al siguiente es "
                          + (saltoMax * 100).ToString("0.0") + "%");
        Check(Pres() > 0.99f, "con la cancion sonando, la aurora acaba estando entera");
        Check(cuadrosHasta90 >= 20, "y tarda mas de 0.8 s en llegar: se ve ENTRAR, no aparecer de golpe");
        Check(saltoMax <= 0.09f, "ningun cuadro cambia mas del 9%: el ojo no puede ver un escalon asi");

        Console.WriteLine("\n  2) Y SE VA POCO A POCO, MAS DESPACIO TODAVIA\n");
        Callate();
        int cuadrosHastaMitad = -1; float bajaMax = 0f; ant = Pres();
        for (int i = 0; i < 1200; i++)
        {
            AVZ.Invoke(F, null);
            float p = Pres();
            float d = ant - p; if (d > bajaMax) bajaMax = d;
            if (cuadrosHastaMitad < 0 && p <= 0.7f) cuadrosHastaMitad = i + 1;
            ant = p;
        }
        Console.WriteLine("     baja al 70% en " + Seg(cuadrosHastaMitad)
                          + "; la mayor bajada de un cuadro al siguiente es " + (bajaMax * 100).ToString("0.00") + "%");
        Check(cuadrosHastaMitad >= 20, "se va MAS DESPACIO de lo que entra");
        Check(bajaMax <= 0.02f, "ningun cuadro pierde mas del 2%: la salida es un fundido, no un corte");

        Console.WriteLine("\n  3) EL BUG DE VERDAD: UN HUECO CORTO DE LA CANCION NO LA BORRA\n");
        // asi era antes: bastaba medio segundo flojo (una intro, un puente, un respiro) para que se apagara
        // entera y volviera de golpe. Ahora un hueco asi apenas se nota.
        Suena(220, 0.5, 0.6); Avanza(200);
        float[] huecos = { 0.3f, 0.6f, 1.0f, 2.0f };
        bool aguanta = true;
        Console.WriteLine("     hueco de silencio      queda de aurora     ¿se sigue dibujando?");
        foreach (float s in huecos)
        {
            Suena(220, 0.5, 0.6); Avanza(200);            // vuelve a estar entera
            Callate();
            int n = (int)(s / Cuadro);
            Avanza(n);
            float queda = Pres();
            bool dibuja = queda > 0.004f;
            Console.WriteLine(string.Format("       {0,4:0.0} s              {1,10:0.0}%              {2}", s, queda * 100, dibuja ? "si" : "NO"));
            if (queda < 0.60f || !dibuja) aguanta = false;
        }
        Check(aguanta, "hasta con 2 s enteros de silencio la aurora sigue ahi, con mas del 60%: nada de parpadeo");

        // y en cuanto vuelve la musica, sube; no hay saltos ni al bajar ni al subir
        Suena(220, 0.5, 0.6);
        float min, max; float salto = Avanza(120, out min, out max);
        Check(salto <= 0.09f, "al volver la musica tambien sube suave (ningun salto mayor del 9%)");

        Console.WriteLine("\n  4) LA PRESENCIA Y LA ATENUACION AL NAVEGAR SON LO QUE MANDA\n");
        // Se comprueba llamando al PROPIO BuildAuroraState (que es quien decide lo que se ve), no repitiendo
        // su formula aqui: si alguien la cambia, esta prueba se entera.
        MethodInfo BUILD = TF.GetMethod("BuildAuroraState", BindingFlags.Instance|BindingFlags.NonPublic);
        Check(BUILD != null, "el estado de la aurora se puede pedir aparte");
        Suena(220, 0.5, 0.6); Avanza(200);
        float presTope = Pres();
        float dimTope = DimDe(BUILD, 1f), dimNav = DimDe(BUILD, BgDimLevel);
        Console.WriteLine("     con la rocola quieta la aurora se dibuja al " + (dimTope*100).ToString("0")
                          + "%, y mientras alguien elige cancion, al " + (dimNav*100).ToString("0") + "%");
        Check(dimTope > 0.98f, "en reposo se dibuja ENTERA: es cuando nadie la mira de cerca");
        Check(dimNav < dimTope * 0.60f, "y en cuanto alguien toca una tecla se serena BASTANTE mas que las estrellas: cansa menos la vista");
        // El suelo era 0.45 (la aurora se quedaba en el 65% y apenas se notaba que se calmaba). Se bajo a
        // proposito: es lo mas grande y lo que mas se mueve de la pantalla, y mientras alguien intenta LEER
        // una lista de canciones -mas todavia si ya lleva unas cuantas encima- es justo lo que mas estorba.
        // Este suelo sigue estando para lo que estaba: que se calme, pero que NO desaparezca.
        Check(dimNav > 0.25f, "pero sin desaparecer: sigue viendose mientras se elige");
        Callate(); Avanza((int)(3.0f / Cuadro));
        float presMedia = Pres();
        float dimMedia = DimDe(BUILD, 1f);
        Console.WriteLine("     y a los 3 s de silencio, al " + (dimMedia*100).ToString("0") + "%");
        Check(dimMedia < dimTope * 0.75f && dimMedia > 0.10f, "a los 3 s de silencio ya se ve claramente mas tenue, pero SIGUE ahi");
        Check(presMedia < presTope, "porque la presencia va bajando sola");

        Console.WriteLine("\n  5) LAS ESTRELLAS: SE DIBUJAN ENCIMA DE LA AURORA, NO DEBAJO\n");
        Type TS = null, TP = null;
        foreach (Type t in asm.GetTypes()) { if (t.Name == "GpuStarfield") TS = t; if (t.Name == "GpuPanel") TP = t; }
        if (TS == null) { Console.WriteLine("     no se encontro GpuStarfield"); fallos++; }
        else
        {
            MethodInfo beg = null;
            foreach (MethodInfo m in TS.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                if (m.Name == "Begin") beg = m;
            Check(beg != null && beg.GetParameters().Length == 4, "el arranque del cuadro recibe el pulso SIN atenuar aparte (para que la deriva no se congele al navegar)");
            if (beg != null && beg.GetParameters().Length == 4)
                Check(beg.GetParameters()[3].Name == "pulseCrudo", "y ese parametro es justo el pulso crudo");

            FieldInfo tc = TS.GetField("TrasCielo", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            Check(tc != null, "hay un hueco para dibujar entre el cielo y las estrellas");

            if (beg != null)
            {
                List<string> l = LlamadasSeguro(beg);
                int pCielo = Pos(l, "D3D9Star.DrawSky"); if (pCielo < 0) foreach (string s in l) if (s.EndsWith(".DrawSky")) { pCielo = Pos(l, s); break; }
                int pAur = -1, pEstr = -1;
                for (int i = 0; i < l.Count; i++)
                {
                    if (pAur < 0 && l[i] == "Dibuja.Invoke") pAur = i;
                    if (pEstr < 0 && l[i].EndsWith(".BuildStarVerts")) pEstr = i;
                }
                Console.WriteLine("     orden dentro del cuadro:  cielo(" + pCielo + ")  ->  aurora(" + pAur + ")  ->  estrellas(" + pEstr + ")");
                Check(pCielo >= 0 && pAur > pCielo, "la aurora se pinta DESPUES del cielo");
                Check(pEstr >= 0 && pAur >= 0 && pEstr > pAur, "y las estrellas DESPUES de la aurora: se siguen viendo por delante, no tapadas");
            }
        }
        if (TP != null)
        {
            FieldInfo d = TP.GetField("trasCielo", BindingFlags.Instance|BindingFlags.NonPublic);
            Check(d != null && d.IsInitOnly, "el enganche se crea UNA vez al arrancar: no genera basura en cada cuadro");
        }

        Console.WriteLine("\n  6) SIN MUSICA NO SE APAGA: SE QUEDA EN REPOSO\n");
        // Antes caia a cero y el cielo se quedaba vacio (en silencio, con un video puesto o en el modo
        // atraccion). Ahora se queda baja y tenue, y ademas RESPIRA muy despacio para que se vea viva.
        float reposo = Cte("AurReposo"), onda = Cte("AurReposoOnda"), paso = Cte("AurReposoPaso");
        Callate();
        Avanza(3000);                                   // dos minutos largos de silencio
        float rMin = 9f, rMax = -9f, saltoRep = 0f; float ant2 = Pres();
        int n2 = (int)(70f / Cuadro);                   // 70 s: mas de dos respiraciones enteras
        for (int i = 0; i < n2; i++)
        {
            AVZ.Invoke(F, null);
            float p = Pres();
            if (p < rMin) rMin = p; if (p > rMax) rMax = p;
            float d = Math.Abs(p - ant2); if (d > saltoRep) saltoRep = d;
            ant2 = p;
        }
        Console.WriteLine(string.Format("     tras dos minutos de silencio la aurora se queda entre el {0:0}% y el {1:0}%, respirando",
                                        rMin*100, rMax*100));
        Check(rMin > 0.20f, "NO se apaga: siempre queda aurora en pantalla, aunque no suene nada");
        Check(rMax < 0.65f, "pero se queda claramente por debajo de lo que sale con musica: es un reposo, no lo mismo");
        Check(rMax - rMin > 0.05f, "y RESPIRA: no es una imagen fija pegada al fondo");
        Console.WriteLine(string.Format("     una respiracion entera dura {0:0} s y el mayor cambio de un cuadro al siguiente es {1:0.000}%",
                                        6.2831853f/paso*Cuadro, saltoRep*100));
        Check(6.2831853f/paso*Cuadro > 20f, "cada respiracion dura mas de veinte segundos: es una animacion CALMADA");
        Check(saltoRep < 0.004f, "y ningun cuadro cambia ni la mitad de un uno por ciento: no se ve latir, se ve respirar");

        // La forma en reposo: lomas anchas que se corren despacio, no una raya recta
        Type TP2 = null;
        foreach (Type t in asm.GetTypes()) if (t.Name == "GpuPanel") TP2 = t;
        MethodInfo CALMA = (TP2 != null) ? TP2.GetMethod("AurCalma", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public) : null;
        Check(CALMA != null, "hay una forma propia para el reposo (no se queda plana como una neblina)");
        if (CALMA != null)
        {
            float lo = 9f, hi = -9f, mueve = 0f;
            for (int i = 0; i <= 144; i++)
            {
                float fx = i/144f;
                float v0 = (float)CALMA.Invoke(null, new object[]{ fx, 0f });
                float v1 = (float)CALMA.Invoke(null, new object[]{ fx, 1f });      // un segundo despues
                if (v0 < lo) lo = v0; if (v0 > hi) hi = v0;
                float d = Math.Abs(v1 - v0); if (d > mueve) mueve = d;
            }
            Console.WriteLine(string.Format("     la forma en reposo va del {0:0}% al {1:0}% de alto y en un segundo se mueve como mucho {2:0.0}%",
                                            lo*100, hi*100, mueve*100));
            Check(hi - lo > 0.15f, "tiene lomas de verdad: se ve una aurora, no una franja recta");
            Check(mueve < 0.03f, "y se mueve DESPACIO: en un segundo entero no cambia ni un 3%");
        }

        // Y con musica, el reposo desaparece por debajo: manda la cancion
        Suena(220, 0.5, 0.6); Avanza(200);
        float calmaConMusica = CalmaDe(BUILD2());
        Callate(); Avanza(3000);
        float calmaSinMusica = CalmaDe(BUILD2());
        Console.WriteLine(string.Format("     con la cancion sonando el reposo pesa {0:0}%, y sin ella, {1:0}%", calmaConMusica*100, calmaSinMusica*100));
        Check(calmaConMusica < 0.02f, "con musica el reposo no pinta nada: manda la cancion");
        Check(calmaSinMusica > 0.90f, "y sin musica manda el reposo");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
