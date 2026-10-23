// LO QUE SUENA AHORA (S4 y S5, 13-10-2026; peticion: «Haz S1, S2, S3, S4 y S5»). Ctrl+F3 -> Sonido -> «Lo que suena
// ahora...».
//
// S4. ESCUCHAR SIN MEJORAS. Para decidir con el oido, en el bar, si las mejoras valen la pena: mientras dura (15 s, o
//     hasta volver a pulsar o cerrar la ventana), la cancion suena sin des-recorte, quitasiseo, DSEE, realce, EQ, bajo,
//     compensacion de noche, claridad, partes suaves, canal mudo ni continua. Lo que NO cambia es el volumen: el
//     nivelado, «Subir todo», la bajada de noche, el balance, las bocinas (grave en mono, subgrave, estereo) y el
//     limitador siguen igual, asi el oido no se engana con que «lo fuerte suena mejor». Cada etapa entra y sale en su
//     rampa de siempre (sin golpes). No se guarda: no es un ajuste.
//
// S5. QUE LE HACE A ESTA CANCION, en vivo (cada medio segundo con la hoja a la vista): el nivel que sale, cuanto baja el
//     limitador y que etapas estan actuando de verdad en la cancion que suena (no solo cuales estan encendidas).
using System;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
        internal const int SinMejorasSeg = 15;
        private static readonly float[] EqPlana = new float[8];
        private bool sinMejoras;
        private DateTime sinMejorasHasta;

        // Enciende o apaga «Escuchar sin mejoras» y lo aplica ya en la que suena y en la siguiente.
        private void PonSinMejoras(bool on)
        {
            sinMejoras = on;
            sinMejorasHasta = on ? DateTime.Now.AddSeconds(SinMejorasSeg) : DateTime.MinValue;
            ApplyNormNow();   // devuelve lo de siempre y, si toca, lo quita (AplicaSinMejoras, al final)
        }

        // Los segundos que quedan (0 = apagado). Se apaga solo al pasar el tiempo.
        private int SinMejorasQuedan()
        {
            if (!sinMejoras) return 0;
            double s = (sinMejorasHasta - DateTime.Now).TotalSeconds;
            if (s <= 0) { PonSinMejoras(false); return 0; }
            return (int)Math.Ceiling(s);
        }

        // Sobre lo ya puesto (ApplyNormNow / AplicaAPreparada): con «sin mejoras», todo lo que mejora a cero; sin el, las
        // etapas sin valor propio en esas dos (pases y sala) vuelven a lo suyo.
        private void AplicaSinMejoras(CadenaCancion c)
        {
            if (c == null) return;
            bool sin = sinMejoras;
            if (c.Arreglo != null) c.Arreglo.Pasa = sin;
            if (c.Declip != null) c.Declip.Pasa = sin;
            if (c.Siseo != null) c.Siseo.Pasa = sin;
            AjusteLocal lo = LocalDe(c);
            if (c.Sala != null) { c.Sala.Ajusta(sin ? false : lo.Claridad, sin ? false : lo.Suaves, balanceDb); c.Sala.AjustaTono(sin ? 0 : lo.Graves, sin ? 0 : lo.Agudos); }
            if (c.Presencia != null) c.Presencia.Ajusta(sin ? false : lo.Presencia);
            if (!sin) return;
            if (c.Tap != null) { c.Tap.Excite = 0f; c.Tap.EqDb = EqPlana; }
            if (c.Bajo != null) c.Bajo.Amount = 0f;
            if (c.Noche != null) c.Noche.Db = 0f;
            if (c.Sbr != null) c.Sbr.Active = false;
        }

        // S5: lo que le esta haciendo la cadena a la cancion que suena, en renglones cortos.
        private string TextoLoQueSuena()
        {
            CadenaCancion c = cadenaSonando;
            if (c == null || nowPlaying == null || nowPlaying.Song == null)
                return videoOn ? "Suena un video: no pasa por la cadena de sonido." : "No suena nada ahora.";
            StringBuilder sb = new StringBuilder();
            string t = nowPlaying.Song.Title ?? "";
            if (t.Length > 46) t = t.Substring(0, 45) + "...";
            sb.Append(t).Append("\n");
            if (c.Limitador != null)
            {
                float nivel = c.Limitador.TomaNivelDb(), baja = c.Limitador.TomaBajadaDb();
                sb.Append("Sale a ").Append(nivel <= -100f ? "silencio" : Math.Round(nivel).ToString("0") + " dB")
                  .Append(".   El limitador baja: ").Append(baja > -0.05f ? "nada" : (-baja).ToString("0.0") + " dB").Append("\n");
            }
            if (sinMejoras) { sb.Append("SIN MEJORAS: solo el volumen, las bocinas y el limitador."); return sb.ToString(); }
            sb.Append("Des-recorte: ").Append(c.Declip == null ? "no hace falta" : (c.Declip.Reparados > 0 ? "reparando picos" : "vigilando"));
            sb.Append("   Siseo: ").Append(c.Siseo == null ? "apagado" : (c.Siseo.RebajaDb < -0.05f ? c.Siseo.RebajaDb.ToString("0.0") + " dB" : "nada")).Append("\n");
            sb.Append("DSEE: ").Append(c.Sbr == null ? "no disponible" : (c.Sbr.Active ? "activo" : "apagado"));
            if (c.Tap != null)
            {
                sb.Append("   Realce: ").Append(Math.Round(c.Tap.Excite * 100f)).Append(" %");
                float[] eq = c.Tap.EqDb; float mx = 0f;
                if (eq != null) for (int i = 0; i < eq.Length && i < 8; i++) if (Math.Abs(eq[i]) > Math.Abs(mx)) mx = eq[i];
                sb.Append("   EQ: ").Append(Math.Abs(mx) < 0.05f ? "plano" : "hasta " + mx.ToString("+0.0;-0.0") + " dB");
            }
            sb.Append("\n");
            if (c.Bajo != null) sb.Append("Bajo: ").Append(Math.Round(c.Bajo.Amount * 100f)).Append(" %   ");
            if (c.Mono != null) sb.Append("Estereo: ").Append(c.Mono.AnchoAhora >= 0.995f ? "como viene" : "al " + Math.Round(c.Mono.AnchoAhora * 100f) + " %");
            sb.Append("\n");
            if (c.Arreglo != null)
            {
                sb.Append("Canal mudo: ").Append(c.Arreglo.RepartoAhora > 0.01f ? "arreglado" : "no");
                sb.Append("   Continua: ").Append(c.Arreglo.ContinuaAhora > 0.01f ? "quitada (" + Math.Round(c.Arreglo.ContinuaDb) + " dB)" : "no");
            }
            return sb.ToString();
        }
    }
}
