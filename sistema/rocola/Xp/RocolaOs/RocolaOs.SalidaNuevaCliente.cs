// ROCOLA OS (cadena de audio nueva; CADENA_NUEVA.md §4, CADENA_NUEVA_salida.md): UNA FUENTE DE LA SALIDA NUEVA. Lo que en XP era «abrir la
// tarjeta» (WaveOutPropio -> SalidaAlsa.Abre) con la salida nueva es «enchufar una fuente» al motor, que tiene la tarjeta SIEMPRE abierta
// (RocolaOs.SalidaNuevaMotor.cs): cada cancion (y su relevo), cada una de Spotify, la cancion muda del diagnostico, el sonido de los videos
// (RocolaOs.SalidaNuevaVideo.cs) y los tonos de la medida del convertidor. Cerrar la «tarjeta» es desenchufarla: la tarjeta no se cierra.
//
// LO QUE HACE (con el formato que le den: coma flotante de 32 bits o PCM de 16, 24 o 32; uno, dos o mas canales; cualquier frecuencia):
//   - pasa cada bloque a coma flotante y a dos canales (uno: el mismo en los dos; mas de dos: los dos primeros, y la bitacora lo dice);
//   - si no viene a 44 100 Hz, lo convierte AQUI con el convertidor sinc de alta calidad de XP (Codigo/SinPausa.cs, RemuestreoContinuo: el del
//     relevo sin pausa y de frecuencia_salida=), en el hilo del que escribe (nunca en el de la pantalla), y si la razon pasa de 4 (8 kHz),
//     en dos pasos; nunca el lineal de plughw;
//   - lo deja en su anillo (a 44,1 kHz) y el motor lo toma de ahi en cada periodo de la tarjeta, por su ganancia (1 en la musica: su nivel
//     ya lo puso la cadena; la del video; la del tono);
//   - y le dice a quien escribe lo que XP le preguntaba a la tarjeta: cuanto cabe (Libres), cuanto ya sono (Sonado: con las marcas que deja
//     el motor al tomar y lo que la tarjeta lleva sonado), tirar lo que no sono (Vacia: los ultimos 5 ms que iban a sonar se apagan con media
//     curva de coseno en vez de cortarse a media onda), pausa y cierre.
// El convertidor, al final de lo que hay, se queda con su media ventana (96 cuadros, unos 2 ms) esperando lo que sigue: si el anillo se
// vacia (acabo la cancion, o el que escribe no llego a tiempo) el motor le pide que la saque con ceros detras (Agota) y el siguiente bloque
// empieza un tramo nuevo. Si la tarjeta se cae, todo lo de la fuente lanza ErrorAlsa con ENODEV (lo que XP trata como «fallo el aparato»:
// espera y retoma la misma cancion; Codigo/SalidaSonido.cs).
using System;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    // LA FUENTE PARA QUIEN EMPUJA: un convertidor que recibe lo que le dan y entrega solo lo que ya puede calcular entero
    internal sealed class SalidaNuevaEtapa
    {
        private sealed class Empuje : ISampleProvider
        {
            private readonly WaveFormat formato;
            private float[] cola = new float[8192];
            private int inicio, cuenta;   // en muestras
            internal Empuje(int tasa) { formato = WaveFormat.CreateIeeeFloatWaveFormat(tasa, 2); }
            public WaveFormat WaveFormat { get { return formato; } }
            internal void Agrega(float[] b, int o, int muestras)
            {
                if (cuenta + muestras > cola.Length)
                {
                    float[] n = new float[Math.Max(cola.Length * 2, cuenta + muestras)];
                    Array.Copy(cola, inicio, n, 0, cuenta);
                    cola = n; inicio = 0;
                }
                else if (inicio + cuenta + muestras > cola.Length) { Array.Copy(cola, inicio, cola, 0, cuenta); inicio = 0; }
                Array.Copy(b, o, cola, inicio + cuenta, muestras);
                cuenta += muestras;
            }
            internal void AgregaCeros(int muestras)
            {
                float[] z = new float[muestras];
                Agrega(z, 0, muestras);
            }
            public int Read(Span<float> destino)
            {
                int n = Math.Min(destino.Length, cuenta);
                n -= n % 2;
                if (n <= 0) return 0;
                cola.AsSpan(inicio, n).CopyTo(destino);
                inicio += n; cuenta -= n;
                if (cuenta == 0) inicio = 0;
                return n;
            }
        }

        internal readonly int TasaIn, TasaOut;
        private readonly int mitad;
        private readonly Empuje empuje;
        private readonly RemuestreoContinuo conv;
        private long real, emitidos;   // cuadros de verdad que entraron; cuadros que salieron
        private float[] salida = new float[8192];

        internal SalidaNuevaEtapa(int tasaIn, int tasaOut)
        {
            TasaIn = tasaIn; TasaOut = tasaOut;
            mitad = (int)Math.Ceiling(96.0 * Math.Max(1.0, (double)tasaIn / tasaOut));   // (la de RemuestreoContinuo: la misma cuenta)
            empuje = new Empuje(tasaIn);
            conv = new RemuestreoContinuo(empuje, WaveFormat.CreateIeeeFloatWaveFormat(tasaOut, 2), 0);   // (busca la tabla ya hecha; si no, la hace aqui)
        }
        internal static bool SePuede(int a, int b)
        { return RemuestreoContinuo.SePuede(WaveFormat.CreateIeeeFloatWaveFormat(a, 2), WaveFormat.CreateIeeeFloatWaveFormat(b, 2)); }
        internal long Entraron { get { return real; } }
        // Lo que ya se puede calcular entero (la ventana de cada cuadro de salida con todo lo de delante): ceil((K + 1) * out / in) con
        // K = entraron - mitad - 1
        private long Calculables(long entrada)
        {
            long k = entrada - mitad - 1;
            if (k < 0) return 0;
            return ((k + 1) * TasaOut + TasaIn - 1) / TasaIn;
        }
        // Mete «cuadros» (dos canales) y devuelve los que salieron, en «fuera» (que crece si hace falta)
        internal int Empuja(float[] b, int o, int cuadros, ref float[] fuera)
        {
            empuje.Agrega(b, o, cuadros * 2);
            real += cuadros;
            return Saca(Calculables(real), ref fuera);
        }
        // Lo que quedo dentro (la media ventana), con ceros detras: hasta el ultimo cuadro que le toca a lo que entro. La etapa ya no sigue.
        internal int Agota(ref float[] fuera)
        {
            empuje.AgregaCeros((mitad + 2) * 2);
            long total = (real * TasaOut + TasaIn - 1) / TasaIn;
            return Saca(total, ref fuera);
        }
        private int Saca(long hasta, ref float[] fuera)
        {
            long n = hasta - emitidos;
            if (n <= 0) return 0;
            int muestras = (int)n * 2;
            if (fuera == null || fuera.Length < muestras) fuera = new float[Math.Max(muestras, 8192)];
            int hechas = 0;
            while (hechas < muestras)
            {
                int r = conv.Read(fuera.AsSpan(hechas, muestras - hechas));
                if (r <= 0) break;
                hechas += r;
            }
            hechas -= hechas % 2;
            emitidos += hechas / 2;
            return hechas / 2;
        }
    }

    // UNA FUENTE ENCHUFADA AL MOTOR
    internal sealed class SalidaNuevaCliente : SalidaAlsa.IFuenteRocolaOs
    {
        internal const int Tasa = SalidaNueva.Tasa;
        internal readonly string Nombre;
        string SalidaAlsa.IFuenteRocolaOs.Nombre { get { return "salida_nueva (" + Nombre + ")"; } }
        internal readonly WaveFormat Formato;      // lo que escribe quien la abrio
        internal readonly int TasaIn, CanalesIn, BytesPorCuadroIn, CuadrosBuferIn, CuadrosPeriodoIn;
        private readonly int tipo;                 // 1 PCM16, 2 PCM24, 3 PCM32, 4 float32
        private readonly object cerrojo = new object();
        // el anillo, a 44,1 kHz y dos canales
        private readonly float[] anillo;
        private readonly int capacidad;            // en cuadros
        private long puestos, tomados;             // cuadros que entraron al anillo y que tomo el motor, desde que se abrio
        // la conversion (si no viene a 44,1 kHz): una o dos etapas; el tramo de ahora empieza en (tramoIn, tramoOut)
        private SalidaNuevaEtapa[] etapas;
        private long tramoIn, tramoOut, dentro;    // cuadros de quien escribe al empezar el tramo; del anillo; y los que estan en el convertidor
        private readonly long[] tramosIn = new long[8], tramosOut = new long[8]; private int nTramos;
        private float[] apoyo = new float[8192], apoyo2 = new float[8192], apoyo3;
        // lo que se dio desde el ultimo Vacia (en cuadros de quien escribe) y desde donde cuenta el anillo
        private long escritos, escritosTotal, baseOut, generacion;
        private long sonadoAntes;
        // las marcas del motor: en que cuadro de la tarjeta empezo cada toma, cuantos tomo y cuantos llevaba tomados
        private const int Marcas = 256;
        private readonly long[] marcaTarjeta = new long[Marcas], marcaAntes = new long[Marcas];
        private readonly int[] marcaCuantos = new int[Marcas];
        private int marcaSig, marcaN;
        // el estado
        private volatile bool pausada, cerrada, apagando;
        private int apagado;                       // cuadros de la cola que se apagan (Vacia): los ultimos que quedan en el anillo
        internal volatile float Ganancia = 1f;     // la que pone quien la usa (el video, el tono); se sigue con una rampa de 20 ms
        internal float GananciaActual = 1f;        // (la del motor)
        internal volatile bool SinVolumenDelDueno; // (el tono de la medida: tal cual, sin el volumen del dueno)
        internal volatile bool Termina;            // (el video: quien escribe ya no dara mas; lo ultimo se apaga en 5 ms)
        internal bool CanalesAvisados;
        internal long CuadrosRellenados, Tramos;   // cuantas veces se saco la media ventana (para la hoja)

        internal SalidaNuevaCliente(string nombre, WaveFormat f, int cuadrosBloque, int bloques)
        {
            Nombre = nombre; Formato = f;
            TasaIn = f.SampleRate; CanalesIn = Math.Max(1, (int)f.Channels); BytesPorCuadroIn = Math.Max(1, (int)f.BlockAlign);
            tipo = TipoDe(f);
            if (tipo == 0) throw new ErrorAlsa(-ErrorAlsa.EINVAL, "salida_nueva: formato " + f.Encoding + " de " + f.BitsPerSample + " bits", true);
            if (TasaIn < 4000 || TasaIn > 384000) throw new ErrorAlsa(-ErrorAlsa.EINVAL, "salida_nueva: frecuencia " + TasaIn, true);
            CuadrosPeriodoIn = Math.Max(1, cuadrosBloque);
            CuadrosBuferIn = Math.Max(2, cuadrosBloque * Math.Max(2, bloques));
            capacidad = (int)Math.Min(int.MaxValue / 4, (long)Math.Ceiling(CuadrosBuferIn * (double)Tasa / TasaIn) + 1024);   // (lo de XP y un poco: la latencia no crece)
            anillo = new float[capacidad * 2];
            NuevoTramo(); Tramos = 0;
            if (TasaIn != Tasa) ApuntaLaPrimera(nombre, TasaIn, f);
        }
        // la primera fuente de cada clase y frecuencia que no es 44,1 kHz (en la sesion): que la bitacora diga que la convierte la salida nueva
        // (y no XP con su limitador detras: ganchos g441-g445)
        private static readonly System.Collections.Generic.HashSet<string> convertidasDichas = new System.Collections.Generic.HashSet<string>();
        private static void ApuntaLaPrimera(string nombre, int hz, WaveFormat f)
        {
            string k = nombre + "|" + hz;
            lock (convertidasDichas) { if (!convertidasDichas.Add(k)) return; }
            RocolaOs.Apunta(SalidaNueva.NombreFuncion, nombre + " a " + hz + " Hz (" + f.Encoding + " " + f.BitsPerSample + " bits): la convierte la salida nueva a "
                + Tasa + " Hz con el sinc de XP, sin limitador (la primera a esa frecuencia)");
        }
        private static int TipoDe(WaveFormat f)
        {
            WaveFormatEncoding e = f.Encoding;
            WaveFormatExtensible x = f as WaveFormatExtensible;
            if (x != null)
            {
                if (x.SubFormat == new Guid("00000001-0000-0010-8000-00aa00389b71")) e = WaveFormatEncoding.Pcm;
                else if (x.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71")) e = WaveFormatEncoding.IeeeFloat;
                else return 0;
            }
            if (e == WaveFormatEncoding.IeeeFloat) return f.BitsPerSample == 32 ? 4 : 0;
            if (e != WaveFormatEncoding.Pcm) return 0;
            return f.BitsPerSample == 16 ? 1 : f.BitsPerSample == 24 ? 2 : f.BitsPerSample == 32 ? 3 : 0;
        }
        // Un tramo de conversion nuevo (al abrir, al vaciar y despues de sacar la media ventana). Las etapas las hace el primer Escribe del
        // tramo (en el hilo de quien escribe: hacer la tabla del convertidor, si no estaba, cuesta unos 20 ms, y aqui puede ser el hilo del motor
        // o el de la pantalla)
        private void NuevoTramo()
        {
            etapas = null;
            tramoIn = escritosTotal; tramoOut = puestos; dentro = 0;
            if (nTramos == tramosIn.Length) { Array.Copy(tramosIn, 1, tramosIn, 0, nTramos - 1); Array.Copy(tramosOut, 1, tramosOut, 0, nTramos - 1); nTramos--; }
            tramosIn[nTramos] = tramoIn; tramosOut[nTramos] = tramoOut; nTramos++;
            Tramos++;
        }
        private SalidaNuevaEtapa[] Etapas()
        {
            if (etapas != null || TasaIn == Tasa) return etapas;
            if (SalidaNuevaEtapa.SePuede(TasaIn, Tasa)) etapas = new SalidaNuevaEtapa[] { new SalidaNuevaEtapa(TasaIn, Tasa) };
            else
            {
                // (8 kHz: la razon pasa de 4) en dos pasos, por la del medio
                int medio = TasaIn < Tasa ? TasaIn * 2 : TasaIn / 2;
                while (!SalidaNuevaEtapa.SePuede(medio, Tasa) && medio > 4000 && medio < 768000) medio = TasaIn < Tasa ? medio * 2 : medio / 2;
                etapas = new SalidaNuevaEtapa[] { new SalidaNuevaEtapa(TasaIn, medio), new SalidaNuevaEtapa(medio, Tasa) };
            }
            return etapas;
        }

        // ---------- lo que pregunta quien escribe (lo que en XP contestaba la tarjeta) ----------
        private void Comprueba()
        {
            if (SalidaNueva.Caida) throw new ErrorAlsa(-ErrorAlsa.ENODEV, "salida_nueva (la tarjeta no esta)");
        }
        // Cuantos cuadros (de quien escribe) caben ahora
        public int Libres()
        {
            Comprueba();
            lock (cerrojo)
            {
                long libresOut = capacidad - (puestos - tomados) - 256;
                if (libresOut <= 0) return 0;
                long n = libresOut * TasaIn / Tasa - dentro - 2;
                return (int)Math.Max(0, Math.Min(n, int.MaxValue));
            }
        }
        public long Escritos { get { lock (cerrojo) return escritos; } }

        // Escribe un bloque (cuadros enteros). Si no cupiera (no deberia: se pregunta Libres antes), espera a que el motor tome, como mucho 2 s.
        public void Escribe(byte[] datos, int bytes)
        {
            Comprueba();
            int cuadros = bytes / BytesPorCuadroIn;
            if (cuadros <= 0) return;
            int espera = 0;
            while (Libres() < cuadros)
            {
                if (cerrada) return;
                if (++espera > 2000) throw new ErrorAlsa(-ErrorAlsa.EIO, "salida_nueva: la tarjeta dejo de pedir sonido");
                Thread.Sleep(1);
            }
            lock (cerrojo)
            {
                if (cerrada) return;
                if (apoyo.Length < cuadros * 2) apoyo = new float[cuadros * 2];
                AFlotante(datos, cuadros, apoyo);
                escritos += cuadros; escritosTotal += cuadros;
                SalidaNuevaEtapa[] et = Etapas();
                if (et == null) { Pon(apoyo, cuadros); return; }
                float[] a = apoyo; int n = cuadros;
                for (int e = 0; e < et.Length; e++)
                {
                    float[] fuera = e % 2 == 0 ? apoyo2 : apoyo3;
                    n = et[e].Empuja(a, 0, n, ref fuera);
                    if (e % 2 == 0) apoyo2 = fuera; else apoyo3 = fuera;
                    a = fuera;
                }
                dentro = escritosTotal - tramoIn - (long)Math.Floor((puestos + n - tramoOut) * (double)TasaIn / Tasa);
                if (dentro < 0) dentro = 0;
                Pon(a, n);
            }
        }
        // EL SONIDO DE UN VIDEO (el hilo de la tuberia: RocolaOs.SalidaNuevaVideo.cs): como Escribe, pero sin esperar ni fallar si la tarjeta se
        // cayo (lee de la tuberia solo lo que cabe; sin tarjeta, el motor lo tira a su ritmo)
        internal void EscribeDeVideo(byte[] datos, int bytes)
        {
            int cuadros = bytes / BytesPorCuadroIn;
            if (cuadros <= 0 || cerrada) return;
            lock (cerrojo)
            {
                if (apoyo.Length < cuadros * 2) apoyo = new float[cuadros * 2];
                AFlotante(datos, cuadros, apoyo);
                escritos += cuadros; escritosTotal += cuadros;
                SalidaNuevaEtapa[] et = Etapas();
                if (et == null) { Pon(apoyo, cuadros); return; }
                float[] a = apoyo; int n = cuadros;
                for (int e = 0; e < et.Length; e++)
                {
                    float[] fuera = e % 2 == 0 ? apoyo2 : apoyo3;
                    n = et[e].Empuja(a, 0, n, ref fuera);
                    if (e % 2 == 0) apoyo2 = fuera; else apoyo3 = fuera;
                    a = fuera;
                }
                dentro = 1;
                Pon(a, n);
            }
        }
        // QUIEN ESCRIBE YA NO DARA MAS (el video acabo): lo que quedo en el convertidor se saca ya (para que el apagado de 5 ms caiga al final de
        // verdad), lo que queda suena, se apaga y la fuente se va sola
        internal void AcabaYSuelta()
        {
            lock (cerrojo)
            {
                if (etapas != null && dentro > 0) { int k = AgotaEtapas(out float[] a); if (a != null && k > 0) Pon(a, k); NuevoTramo(); }
                Termina = true;
                cerrada = true;
            }
        }
        // la media ventana de las etapas, con ceros detras (con el cerrojo)
        private int AgotaEtapas(out float[] a)
        {
            a = null; int k = 0;
            for (int e = 0; e < etapas.Length; e++)
            {
                float[] f1 = null, f2 = null;
                int m1 = e > 0 && k > 0 ? etapas[e].Empuja(a, 0, k, ref f1) : 0;
                int m2 = etapas[e].Agota(ref f2);
                float[] j = new float[(m1 + m2) * 2 + 2];
                if (m1 > 0) Array.Copy(f1, j, m1 * 2);
                if (m2 > 0) Array.Copy(f2, 0, j, m1 * 2, m2 * 2);
                a = j; k = m1 + m2;
            }
            CuadrosRellenados += k;
            return k;
        }

        // a dos canales en coma flotante
        private void AFlotante(byte[] d, int cuadros, float[] o)
        {
            int ch = CanalesIn, bpc = BytesPorCuadroIn;
            if (ch > 2 && !CanalesAvisados) { CanalesAvisados = true; RocolaOs.Apunta(SalidaNueva.NombreFuncion, Nombre + ": " + ch + " canales: suenan los dos primeros"); }
            for (int c = 0; c < cuadros; c++)
            {
                int p = c * bpc;
                float l = Muestra(d, p), r = ch >= 2 ? Muestra(d, p + bpc / ch) : l;
                o[c * 2] = l; o[c * 2 + 1] = r;
            }
        }
        private float Muestra(byte[] d, int o)
        {
            switch (tipo)
            {
                case 1: return (short)(d[o] | (d[o + 1] << 8)) / 32768f;
                case 2: return (((d[o] | (d[o + 1] << 8) | (d[o + 2] << 16)) << 8) >> 8) / 8388608f;
                case 3: return BitConverter.ToInt32(d, o) / 2147483648f;
                default: return BitConverter.ToSingle(d, o);
            }
        }
        // al anillo (con el cerrojo)
        private void Pon(float[] a, int cuadros)
        {
            if (cuadros <= 0) return;
            if (puestos - tomados + cuadros > capacidad) cuadros = (int)(capacidad - (puestos - tomados));   // (no deberia: Libres)
            for (int c = 0; c < cuadros; c++)
            {
                int i = (int)((puestos + c) % capacidad) * 2;
                anillo[i] = a[c * 2]; anillo[i + 1] = a[c * 2 + 1];
            }
            puestos += cuadros;
        }

        // Los cuadros (de quien escribe, desde el ultimo Vacia) que ya sonaron: por las marcas del motor y lo que lleva sonado la tarjeta
        public long Sonado()
        {
            Comprueba();
            long tarjeta = SalidaNueva.SonadoTarjeta();
            lock (cerrojo)
            {
                long outSonado = -1;
                for (int k = 0; k < marcaN; k++)
                {
                    int i = (marcaSig - 1 - k + Marcas) % Marcas;
                    if (marcaTarjeta[i] <= tarjeta) { outSonado = marcaAntes[i] + Math.Min(marcaCuantos[i], tarjeta - marcaTarjeta[i]); break; }
                }
                if (outSonado < 0) outSonado = marcaN > 0 ? marcaAntes[(marcaSig - marcaN + Marcas) % Marcas] : tomados;
                if (marcaN == 0) outSonado = Math.Min(outSonado, baseOut);
                // del anillo a lo de quien escribe: por el tramo que lo contiene
                long inSonado;
                int t = nTramos - 1;
                while (t > 0 && tramosOut[t] > outSonado) t--;
                inSonado = tramosIn[t] + (long)Math.Floor((outSonado - tramosOut[t]) * (double)TasaIn / Tasa);
                if (t + 1 < nTramos && inSonado > tramosIn[t + 1]) inSonado = tramosIn[t + 1];
                long desde = escritosTotal - escritos;   // lo de antes del ultimo Vacia no cuenta
                long r = Math.Max(0, Math.Min(escritos, inSonado - desde));
                if (outSonado >= puestos && dentro == 0) r = escritos;   // ya sono todo lo que entro
                if (r < sonadoAntes) r = sonadoAntes;   // (nunca para atras)
                sonadoAntes = r;
                return r;
            }
        }

        // Tira lo que no sono (waveOutReset / snd_pcm_drop): de lo que queda en el anillo, los 5 ms primeros se apagan y el resto se va. La
        // cuenta de quien escribe vuelve a 0.
        public void Vacia()
        {
            lock (cerrojo)
            {
                long hay = puestos - tomados;
                int cola = (int)Math.Min(hay, Tasa * 5 / 1000);
                for (int c = 0; c < cola; c++)
                {
                    float g = (float)(0.5 + 0.5 * Math.Cos(Math.PI * (c + 1) / (cola + 1)));
                    int i = (int)((tomados + c) % capacidad) * 2;
                    anillo[i] *= g; anillo[i + 1] *= g;
                }
                puestos = tomados + cola;
                escritos = 0; sonadoAntes = 0; generacion++;
                marcaN = 0;
                baseOut = puestos;
                NuevoTramo();
            }
        }
        public void Pausa(bool si) { pausada = si; }
        public void Cierra() { Vacia(); cerrada = true; }
        internal bool Cerrada { get { return cerrada; } }
        internal bool Vacio { get { lock (cerrojo) return puestos == tomados && dentro == 0; } }
        internal long EnElAnillo { get { lock (cerrojo) return puestos - tomados; } }

        // ---------- lo que hace el motor (su hilo) ----------
        // Toma hasta n cuadros y los SUMA en mezcla (dos canales) por su ganancia; la toma empieza en el cuadro «tarjeta» de la tarjeta.
        // tirar = se toman y no suenan (la medida del convertidor: la musica sigue su curso en silencio). Devuelve los tomados.
        internal int Toma(float[] mezcla, int n, long tarjeta, bool tirar)
        {
            if (pausada) return 0;
            lock (cerrojo)
            {
                long hay = puestos - tomados;
                if (hay == 0 && etapas != null && dentro > 0)
                {
                    // se acabo lo que habia: la media ventana del convertidor, con ceros detras, y un tramo nuevo para lo que venga
                    float[] a; int k = AgotaEtapas(out a);
                    if (a != null && k > 0) Pon(a, k);
                    NuevoTramo();
                    hay = puestos - tomados;
                }
                int toma = (int)Math.Min(hay, n);
                if (toma <= 0) return 0;
                float g0 = GananciaActual, g1 = Ganancia;
                if (tirar) g0 = g1 = 0f;
                int rampa = g0 == g1 ? 0 : Math.Min(toma, Tasa / 50);
                // el video que acabo: lo ultimo (5 ms) se apaga
                long quedanDespues = hay - toma;
                int apaga = Termina && quedanDespues < Tasa * 5 / 1000 ? (int)Math.Min(toma, Tasa * 5 / 1000 - quedanDespues) : 0;
                for (int c = 0; c < toma; c++)
                {
                    float g = c < rampa ? g0 + (g1 - g0) * (c + 1) / rampa : g1;
                    if (apaga > 0 && c >= toma - apaga)
                    {
                        long falta = (toma - c) + quedanDespues;   // los que quedan hasta el final, con este
                        g *= (float)(0.5 - 0.5 * Math.Cos(Math.PI * Math.Min(1.0, falta / (Tasa * 0.005))));
                    }
                    int i = (int)((tomados + c) % capacidad) * 2;
                    mezcla[c * 2] += anillo[i] * g;
                    mezcla[c * 2 + 1] += anillo[i + 1] * g;
                }
                if (!tirar) GananciaActual = g1;
                // la marca
                marcaTarjeta[marcaSig] = tarjeta; marcaAntes[marcaSig] = tomados; marcaCuantos[marcaSig] = toma;
                marcaSig = (marcaSig + 1) % Marcas; if (marcaN < Marcas) marcaN++;
                tomados += toma;
                return toma;
            }
        }
    }
}
