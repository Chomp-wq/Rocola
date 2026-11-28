// ROCOLA OS (cadena de audio nueva; CADENA_NUEVA.md §4; CADENA_NUEVA_salida.md, «Las pruebas de sonido por el mismo flujo»): UNA PRUEBA DE
// SONIDO QUE OCUPA LA SALIDA NUEVA SOLA. Para lo que suena para medir o afinar (el ruido por bandas de la hojita del billar para el celular,
// los tonos de la medida del convertidor) mientras la tarjeta sigue siempre abierta: la prueba entra EN LUGAR de la musica (la musica sigue su
// curso callada, como si sonara: su reloj, sus bloques y su fin no se paran) con su ganancia fija, con o sin el volumen del dueno, y al
// acabarse (la fuente devuelve 0) o al pararla, la musica vuelve a oirse en el mismo periodo. Una a la vez. LA MUSICA MANDA: si mientras tanto
// se enchufa una fuente nueva (una cancion pedida, un video, la prueba de bocinas de XP), la prueba se corta sola (SalidaNueva.Enchufa) y
// Sonando pasa a false: nada pedido suena callado.
//
//   SalidaNuevaPruebaSonido p = SalidaNueva.EmpiezaPrueba(fuente, "billar: ruido por bandas", 1f, true);
//   (null: la salida nueva no va -sin ella, el camino de XP: CreaWaveOut, que con ella tambien funciona, sumado a la musica- o ya hay otra)
//   p.Sonando, p.SegundosOidos (lo que ya se oyo, con el bufer de la tarjeta descontado), p.Para()
//
// La fuente es un ISampleProvider de NAudio de cualquier frecuencia y uno o dos canales (uno: el mismo por los dos); si no viene a 44,1 kHz se
// convierte con el sinc de XP (RemuestreoContinuo), hecho aqui, en el hilo que llama (la tabla, si no estaba, cuesta unos 20 ms: no llamar
// desde la pantalla con una frecuencia rara). Su Read lo llama el hilo del motor cada periodo (20 ms): tiene que ser rapido y no esperar.
using System;
using NAudio.Wave;

namespace Rocola
{
    internal sealed class SalidaNuevaPruebaSonido : ISalidaNuevaGenerador
    {
        private readonly ISampleProvider fuente;
        private readonly int canales;
        private float[] buf = new float[4096];
        internal readonly string Quien;
        internal readonly float Ganancia;
        internal readonly bool ConVolumenDelDueno;
        private long generados, empiezaTarjeta = -1;
        private volatile bool acabo;

        internal SalidaNuevaPruebaSonido(ISampleProvider f, string quien, float ganancia, bool conVolumen)
        {
            Quien = quien ?? "prueba"; Ganancia = ganancia; ConVolumenDelDueno = conVolumen;
            WaveFormat w = f.WaveFormat;
            if (w.Channels < 1 || w.Channels > 2) throw new ArgumentException("la prueba de sonido es de uno o dos canales");
            if (w.SampleRate != SalidaNueva.Tasa)
            {
                WaveFormat a = FormatoAudio.Flotante(SalidaNueva.Tasa, w.Channels, FormatoAudio.Mascara(w));
                f = new RemuestreoContinuo(f, a, 0);   // (lanza si no se puede convertir esa)
            }
            fuente = f; canales = w.Channels;
        }
        internal bool Sonando { get { return !acabo && object.ReferenceEquals(SalidaNueva.Exclusiva, this); } }
        // lo que ya se oyo (s): lo dado menos lo que la tarjeta aun tiene por delante
        internal double SegundosOidos
        {
            get
            {
                if (empiezaTarjeta < 0) return 0;
                long oido = SalidaNueva.SonadoTarjeta() - empiezaTarjeta;
                return Math.Max(0, Math.Min(oido, generados)) / (double)SalidaNueva.Tasa;
            }
        }
        internal void Para() { acabo = true; SalidaNueva.SueltaPrueba(this); }

        // (el hilo del motor) un periodo de la prueba, en lugar de la musica
        public int Genera(float[] mezcla, int cuadros)
        {
            if (empiezaTarjeta < 0) empiezaTarjeta = SalidaNueva.EscritosTarjetaSinCerrojo;
            int n = cuadros * canales, hechas = 0;
            if (buf.Length < n) buf = new float[n];
            while (!acabo && hechas < n)
            {
                int r = fuente.Read(buf.AsSpan(hechas, n - hechas));
                if (r <= 0) { acabo = true; break; }
                hechas += r;
            }
            int c = hechas / canales;
            for (int i = 0; i < cuadros; i++)
            {
                float l = 0, d = 0;
                if (i < c) { l = buf[i * canales] * Ganancia; d = canales == 2 ? buf[i * canales + 1] * Ganancia : l; }
                mezcla[i * 2] = l; mezcla[i * 2 + 1] = d;
            }
            generados += c;
            if (acabo) SalidaNueva.SueltaPrueba(this);
            return cuadros;
        }
    }

    internal static partial class SalidaNueva
    {
        internal static volatile bool ExclusivaConVolumen;
        internal static long EscritosTarjetaSinCerrojo;   // (lo pone el motor antes de mezclar: la prueba sabe en que cuadro empezo)
        // UNA PRUEBA DE SONIDO QUE OCUPA LA SALIDA SOLA (arriba). null si la salida nueva no va o ya hay otra (la medida del convertidor, otra prueba).
        internal static SalidaNuevaPruebaSonido EmpiezaPrueba(ISampleProvider fuente, string quien, float ganancia, bool conVolumenDelDueno)
        {
            if (!Va || !Abierta || fuente == null || Exclusiva != null) return null;
            SalidaNuevaPruebaSonido p;
            try { p = new SalidaNuevaPruebaSonido(fuente, quien, ganancia, conVolumenDelDueno); }
            catch (Exception ex) { RocolaOs.Falla(NombreFuncion, "la prueba de sonido «" + quien + "»", ex); return null; }
            ExclusivaConVolumen = conVolumenDelDueno;
            Exclusiva = p;
            RocolaOs.Apunta(NombreFuncion, "prueba de sonido «" + p.Quien + "» (sola, ganancia " + (20 * Math.Log10(Math.Max(1e-9, ganancia))).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                + " dB" + (conVolumenDelDueno ? ", con el volumen del dueno" : ", sin el volumen del dueno") + "): la musica sigue callada");
            return p;
        }
        internal static void SueltaPrueba(ISalidaNuevaGenerador g)
        {
            if (g != null && object.ReferenceEquals(Exclusiva, g)) { Exclusiva = null; ExclusivaConVolumen = false; }
        }
    }
}
