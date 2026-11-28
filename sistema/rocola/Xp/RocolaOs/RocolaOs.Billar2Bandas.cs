// ROCOLA OS (la cadena nueva del billar, B; CADENA_NUEVA_B.md §3): EL RUIDO POR BANDAS PARA AFINAR CON EL CELULAR, EN MONO. El de Ctrl+F3
// (AfinarLocal.cs, PruebaBanda) le da a cada canal su propio ruido: dos bocinas no se suman ni se restan, que era lo justo con la cadena de hoy
// (estereo). La B toca TODO EN MONO (las mismas muestras por todas las bocinas): en el grave, dos bocinas repartidas por el local se suman o se
// restan segun donde este uno parado, y eso es lo que el dueno tiene que medir. Asi que aqui cada banda es UN ruido rosa de una octava, el mismo en
// L y R, al mismo nivel que el de Ctrl+F3 (-20 dB eficaces), con los mismos generadores (PruebaBanda.Generador) y el mismo orden: las ocho de grave
// a agudo, 8 s cada una con 1,5 s de silencio entre ellas, con su entrada y salida de 50 ms. Suena con la rocola callada y no pasa por ninguna
// cadena (se mide el local tal cual). Por donde: con la salida nueva (salida_nueva=1, la tarjeta siempre abierta: CADENA_NUEVA_salida.md, «Las
// pruebas de sonido por el mismo flujo») como PRUEBA QUE OCUPA LA SALIDA SOLA (SalidaNueva.EmpiezaPrueba): en coma flotante, sin los 16 bits de
// CreaWaveOut, con ganancia 1 y el volumen del dueno (el mismo con que suena la musica que se va a corregir); si empieza a sonar algo pedido se
// corta sola (la musica manda). Sin la salida nueva (o con la tarjeta aun cerrada), por su propia salida (CreaWaveOut), como la prueba de
// bocinas de XP.
using System;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        internal sealed class RocolaOsBillar2Bandas : ISampleProvider
        {
            internal const int Tasa = PruebaBanda.Tasa;
            private readonly WaveFormat formato = WaveFormat.CreateIeeeFloatWaveFormat(Tasa, 2);
            private readonly long porBanda, suena, total;
            private long pos;
            private readonly PruebaBanda.Generador[] g = new PruebaBanda.Generador[8];
            private readonly double[] nivel = new double[8];
            internal RocolaOsBillar2Bandas()
            {
                suena = (long)(Tasa * PruebaBanda.Segundos);
                porBanda = suena + (long)(Tasa * PruebaBanda.PausaSeg);
                total = porBanda * 7 + suena;
                for (int k = 0; k < 8; k++)
                {
                    float fc = AfinaCentros[k];
                    g[k] = new PruebaBanda.Generador(fc, 31337u + (uint)k);
                    PruebaBanda.Generador cal = new PruebaBanda.Generador(fc, 777u);
                    for (int i = 0; i < Tasa / 2; i++) cal.Siguiente();   // que se asiente
                    double s = 0; for (int i = 0; i < Tasa; i++) { double v = cal.Siguiente(); s += v * v; }
                    double ef = Math.Sqrt(s / Tasa);
                    nivel[k] = ef > 1e-12 ? Math.Pow(10.0, PruebaBanda.NivelDb / 20.0) / ef : 0.0;
                    for (int i = 0; i < Tasa / 2; i++) g[k].Siguiente();
                }
            }
            internal double NivelDe(int k) { return nivel[k]; }
            internal long Total { get { return total; } }
            // la banda que se oye con 'retrasoMs' de salida por delante (-1: en un silencio o antes de empezar)
            internal int BandaOida(int retrasoMs) { return BandaEn(pos - (long)Math.Max(0, retrasoMs) * Tasa / 1000); }
            // la banda en el cuadro 'oido' (lo que ya se oyo: con la salida nueva lo cuenta ella, con el bufer de la tarjeta descontado)
            internal int BandaEn(long oido)
            {
                if (oido < 0) return -1;
                long k = oido / porBanda;
                if (k >= 8) k = 7;
                if (oido - k * porBanda >= suena) return -1;
                return (int)k;
            }
            public WaveFormat WaveFormat { get { return formato; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                int cuadros = count / 2, hechos = 0;
                long fundido = (long)(Tasa * PruebaBanda.FundidoSeg);
                for (; hechos < cuadros && pos < total; hechos++, pos++)
                {
                    int k = (int)(pos / porBanda); long en = pos - k * porBanda;
                    double v = 0.0;
                    if (en < suena)
                    {
                        double env = 1.0;
                        if (en < fundido) env = en / (double)fundido;
                        else if (suena - en < fundido) env = (suena - en) / (double)fundido;
                        v = g[k].Siguiente() * nivel[k] * env;
                    }
                    buffer[offset + 2 * hechos] = (float)v;
                    buffer[offset + 2 * hechos + 1] = (float)v;   // (el mismo en los dos: como suena la B)
                }
                return hechos * 2;
            }
        }

        private RocolaOsBillar2Bandas rocolaOsBillar2Bandas;
        private SalidaNuevaPruebaSonido rocolaOsBillar2PorLaNueva;   // (con la salida nueva: la prueba que la ocupa sola)
        // TOCAR LAS OCHO BANDAS (el boton de la hojita): lo que dice la hojita
        internal string RocolaOsBillar2ProbarBandas()
        {
            if (nowPlaying != null || videoOn) return "Hay música sonando: la medida es con la rocola callada.";
            ParaPruebaBocinas(); RocolaOsBillar2SueltaNueva();
            RocolaOsBillar2Bandas p = new RocolaOsBillar2Bandas();
            if (SalidaNueva.Va && SalidaNueva.Abierta)
            {
                SalidaNuevaPruebaSonido n = SalidaNueva.EmpiezaPrueba(p, "billar: las 8 bandas en mono", 1f, true);
                if (n != null)
                {
                    rocolaOsBillar2PorLaNueva = n; rocolaOsBillar2Bandas = p;
                    RocolaOs.Apunta(RocolaOsBillar2.Nombre, "suenan las 8 bandas en mono para afinar con el celular (por la salida nueva)");
                    return "";
                }
                if (SalidaNueva.Exclusiva != null) return "La salida está ocupada con otra prueba (la medida del convertidor): espera a que acabe.";
            }
            IWavePlayer w = null;
            try
            {
                w = CreaWaveOut();
                w.Init(new NAudio.Wave.SampleProviders.SampleToWaveProvider16(p));
                pruebaSalida = w; rocolaOsBillar2Bandas = p;
                w.PlaybackStopped += PruebaAcabo;
                w.Play();
                RocolaOs.Apunta(RocolaOsBillar2.Nombre, "suenan las 8 bandas en mono para afinar con el celular");
                return "";
            }
            catch (Exception ex)
            {
                if (w != null) { try { w.PlaybackStopped -= PruebaAcabo; } catch { } try { w.Dispose(); } catch { } }
                pruebaSalida = null; rocolaOsBillar2Bandas = null;
                RocolaOs.Falla(RocolaOsBillar2.Nombre, "tocar las bandas", ex);
                return "No se pudo abrir la salida de sonido: " + MotivoFallo(ex) + ".";
            }
        }
        // en grande: la banda que suena («250 Hz   3 de 8»), o «» si nada
        internal string RocolaOsBillar2BandaGrande()
        {
            RocolaOsBillar2Bandas p = rocolaOsBillar2Bandas;
            SalidaNuevaPruebaSonido n = rocolaOsBillar2PorLaNueva;
            if (p != null && n != null)
            {
                if (!n.Sonando) { RocolaOsBillar2SueltaNueva(); return ""; }   // (acabo, o se corto porque empezo algo pedido)
                int bn = p.BandaEn((long)(n.SegundosOidos * RocolaOsBillar2Bandas.Tasa));
                return bn < 0 ? "" : NombreBanda(bn) + "   " + (bn + 1) + " de 8";
            }
            if (p == null || pruebaSalida == null) return "";
            int b = p.BandaOida(latenciaAbiertaMs > 0 ? latenciaAbiertaMs : 1000);
            return b < 0 ? "" : NombreBanda(b) + "   " + (b + 1) + " de 8";
        }
        internal void RocolaOsBillar2ParaBandas() { RocolaOsBillar2SueltaNueva(); if (rocolaOsBillar2Bandas != null) { rocolaOsBillar2Bandas = null; ParaPruebaBocinas(); } }
        private void RocolaOsBillar2SueltaNueva()
        {
            SalidaNuevaPruebaSonido n = rocolaOsBillar2PorLaNueva;
            if (n == null) return;
            rocolaOsBillar2PorLaNueva = null;
            if (n.Sonando) n.Para();
            if (pruebaSalida == null) rocolaOsBillar2Bandas = null;
        }
    }
}
