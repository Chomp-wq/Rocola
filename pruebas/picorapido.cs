using System;
using System.Collections.Generic;
using System.Reflection;

// EL DETECTOR DE PICO REAL, CON EL FILTRO DESENROLLADO (22-10-2026; PicoReal.cs, DetectorPicoReal.Bloque).
//
// El limitador de pico real pasa cada muestra por un filtro de 24 coeficientes (12 parejas simetricas) cuando algo cerca puede pasar del techo. El bucle de
// doce vueltas de Bloque se escribio ahora doce veces seguidas, con los 36 coeficientes en variables y la suma a + b hecha una vez para u y w: las mismas
// sumas en el mismo orden (u, v y w acumulan de una en una, de la pareja 0 a la 11), asi que sale lo mismo bit a bit.
//
// La referencia es la propia rocola: DetectorPicoReal.Empuja (muestra a muestra) conserva el bucle de siempre, y Bloque tiene que dar EXACTAMENTE lo mismo que
// el, en cualquier troceo, con cualquier salto entre muestras (mono, estereo, seis canales), con el estado pasando de una llamada a la siguiente, con el techo
// de fabrica y otros, y con senales de todo tipo: musica fuerte (el filtro siempre), musica bajita (casi nunca), alternando, ruido a tope, silencio, ceros con signo
// (-0), numeros diminutos (desnormales), NaN e infinitos y el vaciado del final (datos nulos). Ademas, la copia de la mejor entre el pico que ya habia y el de este
// canal (primero = false) de varios canales juntos.
class PicoRapido
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static int Bits(float x) { return BitConverter.ToInt32(BitConverter.GetBytes(x), 0); }

    static Type tdet; static MethodInfo mEmpuja, mBloque;
    delegate float DEmpuja(float x);

    static float[] Senal(int caso, int n, Random r)
    {
        float[] x = new float[n];
        switch (caso)
        {
            case 0: for (int i = 0; i < n; i++) x[i] = (float)((r.NextDouble() * 2 - 1) * 0.95); break;                       // fuerte: el filtro casi siempre
            case 1: for (int i = 0; i < n; i++) x[i] = (float)((r.NextDouble() * 2 - 1) * 0.1); break;                        // bajita: casi nunca
            case 2: for (int i = 0; i < n; i++) x[i] = (float)((r.NextDouble() * 2 - 1) * ((i / 700) % 3 == 0 ? 0.97 : 0.05)); break;   // alterna
            case 3: for (int i = 0; i < n; i++) x[i] = (i & 1) == 0 ? 0.99f : -0.99f; break;                                  // a tope, fs/2
            case 4: for (int i = 0; i < n; i++) x[i] = (float)(Math.Sin(i * Math.PI / 2 + Math.PI / 4) * 0.7071); break;     // el caso de libro: muestras en 0,707, onda en 1
            case 5: break;                                                                                                    // silencio
            case 6: for (int i = 0; i < n; i++) x[i] = (i % 5 == 0) ? -0.0f : 0.0f; x[n / 2] = 0.9f; break;                  // ceros con signo
            case 7: for (int i = 0; i < n; i++) x[i] = (float)((r.NextDouble() * 2 - 1) * 1e-40); x[n / 3] = 0.95f; break;    // desnormales
            case 8: for (int i = 0; i < n; i++) x[i] = (float)((r.NextDouble() * 2 - 1) * 0.9); x[n / 4] = float.NaN; x[n / 2] = float.PositiveInfinity; x[3 * n / 4] = float.NegativeInfinity; break;
            default: for (int i = 0; i < n; i++) x[i] = (float)(0.9 * Math.Sin(i * 0.05) + (r.NextDouble() - 0.5) * 0.2); break;
        }
        return x;
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        tdet = asm.GetType("Rocola.DetectorPicoReal", false);
        Check(tdet != null, "la rocola trae DetectorPicoReal");
        if (tdet == null) { Console.WriteLine("  FALLO picorapido: esta rocola no tiene DetectorPicoReal"); return 1; }
        mEmpuja = tdet.GetMethod("Empuja", T); mBloque = tdet.GetMethod("Bloque", T);
        Check(mEmpuja != null && mBloque != null, "DetectorPicoReal trae Empuja y Bloque");
        if (mal > 0) return 1;
        int taps = (int)tdet.GetField("Taps", T).GetValue(null);
        Check(taps == 24, "el filtro tiene 24 coeficientes por fase (Bloque esta desenrollado para 24)");

        Random r = new Random(5);
        int iguales = 0, distintos = 0, hechos = 0;
        foreach (int caso in new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 })
        foreach (float techo in new float[] { 0.88f, 0.5f, 0.98f })
        foreach (int canales in new int[] { 1, 2, 6 })
        foreach (int trozo in new int[] { 1, 7, 64, 1000, 4410 })
        {
            int cuadros = (trozo >= 1000 ? 9000 : 2500);
            // un detector de referencia por canal (muestra a muestra) y otro por canal (por bloques)
            object[] dr = new object[canales], db = new object[canales];
            DEmpuja[] e = new DEmpuja[canales];
            for (int c = 0; c < canales; c++)
            {
                dr[c] = Activator.CreateInstance(tdet, T, null, new object[] { techo }, null);
                db[c] = Activator.CreateInstance(tdet, T, null, new object[] { techo }, null);
                e[c] = (DEmpuja)Delegate.CreateDelegate(typeof(DEmpuja), dr[c], mEmpuja);
            }
            float[] x = Senal(caso, cuadros * canales, r);
            // la salida de referencia: el mayor de los canales en cada cuadro, canal a canal en el mismo orden que Bloque
            float[] esperado = new float[cuadros];
            for (int f = 0; f < cuadros; f++)
            {
                float m = 0f;
                for (int c = 0; c < canales; c++) { float p = e[c](x[f * canales + c]); if (c == 0 || p > m) m = p; }
                esperado[f] = m;
            }
            // por bloques, de a 'trozo' cuadros (el detector guarda su estado entre llamadas)
            float[] obtenido = new float[cuadros];
            float[] tmp = new float[trozo];
            for (int f0 = 0; f0 < cuadros; f0 += trozo)
            {
                int n = Math.Min(trozo, cuadros - f0);
                for (int c = 0; c < canales; c++)
                    mBloque.Invoke(db[c], new object[] { x, f0 * canales + c, canales, n, tmp, c == 0 });
                Array.Copy(tmp, 0, obtenido, f0, n);
            }
            bool igual = true;
            for (int f = 0; f < cuadros && igual; f++) if (Bits(esperado[f]) != Bits(obtenido[f])) igual = false;
            hechos++;
            if (igual) iguales++; else { distintos++; if (distintos <= 4) Console.WriteLine("  FALLO senal " + caso + ", techo " + techo + ", " + canales + " canales, trozo " + trozo + ": difiere"); }
        }
        Check(distintos == 0, hechos + " combinaciones (10 senales x 3 techos x 3 formatos x 5 troceos): " + iguales + " iguales bit a bit al detector muestra a muestra (" + distintos + " distintas)");

        // el vaciado del final: datos nulos (ceros) tras la musica, el estado sigue
        {
            object a = Activator.CreateInstance(tdet, T, null, new object[] { 0.88f }, null), b = Activator.CreateInstance(tdet, T, null, new object[] { 0.88f }, null);
            DEmpuja ea = (DEmpuja)Delegate.CreateDelegate(typeof(DEmpuja), a, mEmpuja);
            float[] x = Senal(0, 3000, r);
            float[] ref1 = new float[3000 + 40]; float[] got = new float[3000 + 40];
            for (int i = 0; i < 3000; i++) ref1[i] = ea(x[i]);
            for (int i = 0; i < 40; i++) ref1[3000 + i] = ea(0f);
            float[] tmp = new float[3040];
            mBloque.Invoke(b, new object[] { x, 0, 1, 3000, tmp, true }); Array.Copy(tmp, 0, got, 0, 3000);
            mBloque.Invoke(b, new object[] { null, 0, 1, 40, tmp, true }); Array.Copy(tmp, 0, got, 3000, 40);
            bool igual = true;
            for (int i = 0; i < got.Length && igual; i++) if (Bits(ref1[i]) != Bits(got[i])) igual = false;
            Check(igual, "el vaciado del final (datos nulos) sigue dando lo mismo que Empuja con ceros");
        }

        Console.WriteLine();
        Console.WriteLine(mal == 0 ? "OK picorapido: " + ok + " comprobaciones" : "FALLAN " + mal + " de " + (ok + mal));
        return mal == 0 ? 0 : 1;
    }
}
