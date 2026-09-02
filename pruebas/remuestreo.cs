using System;
using System.Diagnostics;
using NAudio.Dsp;

// ¿VALE LA PENA REMUESTREAR DENTRO DE LA ROCOLA?
//
// LA IDEA. Casi todos los MP3 son de 44100 Hz, pero el mezclador de Windows corre a la frecuencia que
// tenga puesta la tarjeta -en la practica, 48000 casi siempre-. O sea que HOY, en cada cancion, quien
// convierte 44.1 a 48 es Windows, con su remuestreador, que en XP tiene fama de malo. Si la rocola
// entregara el audio YA a 48000, el mezclador no tendria nada que convertir y ese paso desapareceria.
//
// Es el mismo premio que promete ASIO4ALL -que Windows no toque el audio- pero sin driver que instalar,
// sin dejar mudos los videos (que suenan por Windows Media Player, fuera de la cadena de la rocola) y
// sin perder el colchon de un segundo que hace que la musica no se corte nunca.
//
// PERO SOLO PAGA SI SE CUMPLEN DOS COSAS, y las dos se miden aqui:
//
//   1) QUE CUESTE POCO. El audio se rellena desde el hilo de la pantalla, y cada bloque de 50 ms ya
//      cuesta 0.92 ms aqui (3.7 ms en la maquina del bar, medido por 'atoron'). Si remuestrear duplica
//      eso, se cambia un problema por otro peor.
//
//   2) QUE EL REMUESTREADOR DE LA ROCOLA SEA DE VERDAD MEJOR que uno malo. Si un remuestreo pobre ya
//      ensucia por debajo de lo que se oye en un bar, entonces lo que hace Windows da igual y esto es
//      trabajo para nada.
//
// COMO SE MIDE LA CALIDAD. Se mete un tono puro, se remuestrea, y se mira lo mas alto que aparece en el
// sitio donde NO deberia haber nada. Eso es la basura que anade el remuestreador, y se dice en decibelios
// por debajo del tono. Cuanto mas abajo, mas limpio.
//
// UNA HONESTIDAD POR DELANTE: aqui NO se puede medir el remuestreador de Windows, que es de Windows y
// esta maquina es Linux. Lo que se mide es un remuestreo POBRE (interpolacion lineal, dos muestras) como
// representante de "uno malo", y uno BUENO (sinc ventaneado) como el que llevaria la rocola. Entre esos
// dos numeros esta lo que hay en juego; cual de los dos se parece a Windows XP habria que medirlo alli.
//
// Y UNA COSA QUE SALIO MIDIENDO, que no es obvia: 44100 -> 48000 es una razon EXACTA (147/160). Llevando
// la fase con enteros en vez de con decimales, la tabla de coeficientes no redondea nada. Con una tabla
// "de proposito general" de 512 fases y la fase sacada del numero con decimales, quedaba un suelo de
// basura a -57 dB en los agudos que NO era el filtro, era el redondeo de la tabla. Con la razon exacta
// desaparece y el mismo filtro pasa de -57 a -92 dB. Si algun dia esto se hace, hay que hacerlo asi.
class Remuestreo
{
    const int HzIn = 44100, HzOut = 48000;

    // ---------- EL REMUESTREADOR BUENO: sinc ventaneado, con tabla de fases ----------
    //
    // Cada muestra de salida cae ENTRE dos de entrada. Un remuestreador bueno no toma la de al lado ni
    // promedia las dos: reconstruye el valor que la onda tenia en ese punto sumando N muestras vecinas,
    // cada una pesada por la funcion sinc, que es la que dice como se reparte una muestra en el tiempo.
    //
    // Los pesos NO se calculan en cada muestra -eso serian senos y cosenos por muestra, carisimo-: se
    // guardan hechos en una tabla de FASES. La fase es "en que parte del hueco entre dos muestras caes".
    // Con 512 fases, el error de redondear a la fase mas cercana queda muy por debajo de lo demas.
    sealed class Sinc
    {
        readonly int taps, fases;
        readonly float[] tabla;   // [fase * taps + i]
        public Sinc(int taps, int fases)
        {
            this.taps = taps; this.fases = fases;
            tabla = new float[taps * fases];
            for (int f = 0; f < fases; f++)
            {
                double frac = f / (double)fases;
                double suma = 0;
                for (int i = 0; i < taps; i++)
                {
                    double x = (i - (taps / 2 - 1)) - frac;         // distancia en muestras al punto pedido
                    double s = (Math.Abs(x) < 1e-9) ? 1.0 : Math.Sin(Math.PI * x) / (Math.PI * x);
                    // Ventana de Blackman-Harris: sin ventana, cortar la sinc a N muestras deja ondulacion
                    // en la banda y fugas fuera de ella.
                    double u = (i + 0.5) / taps;
                    double w = 0.35875 - 0.48829 * Math.Cos(2 * Math.PI * u) + 0.14128 * Math.Cos(4 * Math.PI * u) - 0.01168 * Math.Cos(6 * Math.PI * u);
                    double c = s * w;
                    tabla[f * taps + i] = (float)c; suma += c;
                }
                // Normalizar cada fase: asi una senal continua sale con el mismo nivel en todas y no
                // aparece un zumbido a la frecuencia de las fases.
                float inv = (float)(1.0 / suma);
                for (int i = 0; i < taps; i++) tabla[f * taps + i] *= inv;
            }
        }
        public int Taps { get { return taps; } }

        // LA RAZON 44100 -> 48000 ES EXACTA: 147/160. Simplificando, cada muestra de salida avanza 147/160
        // de muestra de entrada, asi que las fases posibles son EXACTAMENTE 160 y se repiten. Llevando la
        // fase con enteros, la tabla no redondea NADA y el unico error que queda es el del filtro.
        //
        // Esto importa mas de lo que parece: con una tabla "de proposito general" de 512 fases y la fase
        // sacada del numero con decimales, el redondeo deja un suelo de basura a -57 dB en los agudos, y
        // eso NO es el filtro, es la tabla. Con la razon exacta, ese suelo desaparece.
        public const int L = 160, M = 147;
        public int Exacto(float[] ent, int n, float[] sal, ref int i0, ref int fase)
        {
            int m = 0, medio = taps / 2 - 1;
            while (i0 + taps - medio <= n)
            {
                if (i0 - medio < 0) { fase += M; i0 += fase / L; fase %= L; continue; }
                int b = (fase * fases / L) * taps, a = i0 - medio;
                float acc = 0f;
                for (int i = 0; i < taps; i++) acc += ent[a + i] * tabla[b + i];
                sal[m++] = acc;
                fase += M; i0 += fase / L; fase %= L;
            }
            i0 -= n;   // lo que sobra para el bloque siguiente
            return m;
        }
        // Remuestrea 'n' muestras (un canal) de hzIn a hzOut. 'pos' lleva la posicion fraccionaria entre
        // llamadas, que es lo que hace que los bloques encajen sin un clic en la costura.
        public int Convierte(float[] ent, int n, float[] sal, ref double pos)
        {
            double paso = (double)HzIn / HzOut;
            int m = 0, medio = taps / 2 - 1;
            while (true)
            {
                int i0 = (int)pos;
                if (i0 + taps - medio > n) break;
                if (i0 - medio < 0) { pos += paso; continue; }
                int f = (int)((pos - i0) * fases); if (f >= fases) f = fases - 1;
                int b = f * taps, a = i0 - medio;
                float acc = 0f;
                for (int i = 0; i < taps; i++) acc += ent[a + i] * tabla[b + i];
                sal[m++] = acc;
                pos += paso;
            }
            pos -= (int)pos;   // la parte entera ya se consumio
            return m;
        }
    }

    // ---------- EL POBRE: interpolacion lineal (dos muestras) ----------
    // Es lo que sale de "tomar el punto de en medio de la raya que une dos muestras". Barato y sucio.
    static int Lineal(float[] ent, int n, float[] sal, ref double pos)
    {
        double paso = (double)HzIn / HzOut;
        int m = 0;
        while (true)
        {
            int i0 = (int)pos;
            if (i0 + 1 >= n) break;
            float fr = (float)(pos - i0);
            sal[m++] = ent[i0] * (1f - fr) + ent[i0 + 1] * fr;
            pos += paso;
        }
        pos -= (int)pos;
        return m;
    }

    // ---------- QUE LE HACE AL TONO: cuanto lo baja, y cuanta basura deja ----------
    //
    // Se mide EN EL TIEMPO y no con un analisis de frecuencias, que es mas facil de equivocar: se busca
    // el tono exacto que mejor encaja en lo que salio -su altura y su desfase, por minimos cuadrados-, se
    // resta, y lo que queda es EXACTAMENTE lo que el remuestreador se invento. Dos numeros salen de ahi:
    //
    //   PERDIDA: cuanto ha bajado el tono. Un remuestreador pobre apaga los agudos, y eso SI se oye:
    //            es la diferencia entre un plato que brilla y uno que suena tapado.
    //   BASURA:  lo que queda despues de quitar el tono, en decibelios por debajo de el. Es ruido y
    //            distorsion que no estaban en la cancion.
    static void Mide(float[] y, int n, double hz, double hzSenal, out double perdidaDb, out double basuraDb)
    {
        // Se descartan los bordes: el arranque del filtro no es representativo de nada.
        int ini = 2000, fin = n - 2000;
        if (fin - ini < 4096) { perdidaDb = 0; basuraDb = 0; return; }
        double w = 2 * Math.PI * hzSenal / hz;
        double sc = 0, ss = 0, cc = 0, sss = 0;
        for (int i = ini; i < fin; i++)
        {
            double c = Math.Cos(w * i), s2 = Math.Sin(w * i);
            sc += y[i] * c; ss += y[i] * s2; cc += c * c; sss += s2 * s2;
        }
        double a = (cc > 0) ? sc / cc : 0, b = (sss > 0) ? ss / sss : 0;
        double amp = Math.Sqrt(a * a + b * b);
        double resid = 0, senal = 0;
        for (int i = ini; i < fin; i++)
        {
            double ajuste = a * Math.Cos(w * i) + b * Math.Sin(w * i);
            double d = y[i] - ajuste;
            resid += d * d; senal += ajuste * ajuste;
        }
        perdidaDb = (amp > 1e-12) ? 20.0 * Math.Log10(amp / 0.5) : -999;   // el tono entro a 0.5
        basuraDb = (senal > 1e-20 && resid > 1e-20) ? 10.0 * Math.Log10(resid / senal) : -999;
    }

    static float[] Tono(int n, double hz, double hzSenal)
    {
        float[] x = new float[n];
        for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(2 * Math.PI * hzSenal * i / hz));
        return x;
    }

    static int Main()
    {
        Console.WriteLine();
        Console.WriteLine("  REMUESTREAR 44100 -> 48000 DENTRO DE LA ROCOLA");
        Console.WriteLine();

        // ---------------- 1) LO QUE CUESTA ----------------
        Console.WriteLine("  1) LO QUE CUESTA, POR BLOQUE DE 50 ms (que es el que usa la rocola)");
        Console.WriteLine();
        Console.WriteLine("     (para comparar: la cadena de sonido ENTERA cuesta hoy 0.92 ms por bloque");
        Console.WriteLine("      en esta maquina, y 3.7 ms en la del bar, medido por 'atoron')");
        Console.WriteLine();
        int nBloque = HzIn * 50 / 1000;                 // muestras de entrada en 50 ms, por canal
        float[] ent = Tono(nBloque + 64, HzIn, 997.0);
        float[] sal = new float[nBloque * 2 + 64];
        int[] tapsProbar = { 2, 16, 32, 64 };
        Console.WriteLine("     remuestreador            us/bloque   us/bloque estereo   % de la cadena de hoy");
        Console.WriteLine("     ---------------------------------------------------------------------------");
        foreach (int taps in tapsProbar)
        {
            Sinc s = (taps > 2) ? new Sinc(taps, 512) : null;
            double pos = 0;
            // calentar
            for (int i = 0; i < 200; i++) { pos = 0; if (s == null) Lineal(ent, nBloque, sal, ref pos); else s.Convierte(ent, nBloque, sal, ref pos); }
            Stopwatch sw = Stopwatch.StartNew();
            const int VUELTAS = 2000;
            for (int i = 0; i < VUELTAS; i++) { pos = 0; if (s == null) Lineal(ent, nBloque, sal, ref pos); else s.Convierte(ent, nBloque, sal, ref pos); }
            sw.Stop();
            double us = sw.Elapsed.TotalMilliseconds * 1000.0 / VUELTAS;
            string nom = (taps == 2) ? "lineal (el pobre)" : ("sinc de " + taps + " muestras");
            Console.WriteLine(string.Format("     {0,-24} {1,7:0.0}   {2,15:0.0}   {3,17:0.0}%",
                              nom, us, us * 2, (us * 2 / 1000.0) / 0.92 * 100.0));
        }

        // ---------------- 2) LO QUE LE HACE AL SONIDO ----------------
        Console.WriteLine();
        Console.WriteLine("  2) LO QUE LE HACE AL SONIDO");
        Console.WriteLine();
        Console.WriteLine("     Se mete un tono puro, se remuestrea, y se mira que quedo de el.");
        Console.WriteLine("     PERDIDA = cuanto lo baja (los agudos apagados SI se oyen).");
        Console.WriteLine("     BASURA  = lo que se invento, en dB por debajo del tono (-60 ya no se oye en un bar).");
        Console.WriteLine();
        double[] tonos = { 200, 1000, 4000, 8000, 12000, 15000, 17000 };
        Console.WriteLine("                      lineal (el pobre)        sinc de 16          sinc de 32");
        Console.WriteLine("      tono         perdida    basura      perdida   basura     perdida   basura");
        Console.WriteLine("     ------------------------------------------------------------------------------");
        int nLargo = 1 << 17;
        // 160 fases = la razon exacta. Ni una mas hace falta, y ni una menos vale.
        Sinc s16 = new Sinc(16, Sinc.L), s32 = new Sinc(32, Sinc.L);
        double peorPerdLin = 0, peorBasLin = -999, peorBas16 = -999, peorPerd32 = 0, peorBas32 = -999;
        foreach (double f in tonos)
        {
            float[] x = Tono(nLargo, HzIn, f);
            float[] y = new float[nLargo * 2];
            double pL, bL, p16, b16, p32, b32, p = 0;
            int m = Lineal(x, nLargo, y, ref p); Mide(y, m, HzOut, f, out pL, out bL);
            int i0 = 0, fa = 0; m = s16.Exacto(x, nLargo, y, ref i0, ref fa); Mide(y, m, HzOut, f, out p16, out b16);
            i0 = 0; fa = 0; m = s32.Exacto(x, nLargo, y, ref i0, ref fa); Mide(y, m, HzOut, f, out p32, out b32);
            if (pL < peorPerdLin) peorPerdLin = pL;
            if (bL > peorBasLin) peorBasLin = bL;
            if (b16 > peorBas16) peorBas16 = b16;
            if (p32 < peorPerd32) peorPerd32 = p32;
            if (b32 > peorBas32) peorBas32 = b32;
            Console.WriteLine(string.Format("     {0,6:0} Hz   {1,8:0.00} dB {2,8:0.0} dB   {3,8:0.00} dB {4,7:0.0} dB  {5,8:0.00} dB {6,7:0.0} dB",
                              f, pL, bL, p16, b16, p32, b32));
        }

        // ---------------- 3) EN CONTEXTO ----------------
        Console.WriteLine();
        Console.WriteLine("  3) EN CONTEXTO");
        Console.WriteLine();
        Console.WriteLine(string.Format("     lo peor de un remuestreo POBRE:  apaga {0:0.0} dB y ensucia a {1:0} dB", -peorPerdLin, peorBasLin));
        Console.WriteLine(string.Format("     lo peor de uno BUENO (32):       apaga {0:0.00} dB y ensucia a {1:0} dB", -peorPerd32, peorBas32));
        Console.WriteLine(string.Format("     y con 16 muestras -la mitad de coste- se queda en {0:0} dB", peorBas16));
        Console.WriteLine();
        Console.WriteLine("     Lo que NO se puede medir aqui es el remuestreador de Windows, que es de Windows.");
        Console.WriteLine("     Entre esos dos numeros esta lo que hay en juego; a cual se parece el de XP habria");
        Console.WriteLine("     que medirlo en la maquina del bar.");
        return 0;
    }
}
