// LO QUE SUENA AHORA (en XP, Rocola/LoQueSuena.cs: S4 «Escuchar sin mejoras» y S5 «que le hace a esta cancion»), copiado tal cual; la
// hoja de ajustes de Rocola OS lo ensena (Controlador/VistaGrafica). AplicaSinMejoras ya estaba en DeXP.cs.
using System;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
        internal const int SinMejorasSeg = 15;
        private DateTime sinMejorasHasta;

        // Enciende o apaga «Escuchar sin mejoras» y lo aplica ya en la que suena y en la siguiente.
        public void PonSinMejoras(bool on)
        {
            sinMejoras = on;
            if (on) { CadenaCancion c = cadenaSonando; if (c != null && c.Margen != null) c.Margen.Descarta = true; }   // esa vuelta no se aprende (VolumenParejo.cs)
            sinMejorasHasta = on ? DateTime.Now.AddSeconds(SinMejorasSeg) : DateTime.MinValue;
            ApplyNormNow();   // devuelve lo de siempre y, si toca, lo quita (AplicaSinMejoras, al final)
        }

        // Los segundos que quedan (0 = apagado). Se apaga solo al pasar el tiempo.
        public int SinMejorasQuedan()
        {
            if (!sinMejoras) return 0;
            double s = (sinMejorasHasta - DateTime.Now).TotalSeconds;
            if (s <= 0) { PonSinMejoras(false); return 0; }
            return (int)Math.Ceiling(s);
        }

        // S5: lo que le esta haciendo la cadena a la cancion que suena, en renglones cortos.
        public string TextoLoQueSuena()
        {
            CadenaCancion c = cadenaSonando;
            if (c == null || nowPlaying == null || nowPlaying.Song == null)
                return SonandoVideo ? "Suena un video: no pasa por la cadena de sonido." : "No suena nada ahora.";
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
            // T6 (Zumbido.cs): el de la corriente, si la cancion lo trae
            if (c.Zumbido != null) sb.Append("   Zumbido: ").Append(c.Zumbido.Hz).Append(" Hz ").Append(c.Zumbido.Activa && !c.Zumbido.Pasa ? "quitado" : "(sin quitar)");
            if (c.PreEco != null) sb.Append("   Pre-eco: ").Append(c.PreEco.Pasa ? "(sin quitar)" : c.PreEco.Quitados + " golpes");   // R2 (PreEco.cs)
            // T9 (Eses.cs): lo que mas bajo la banda de las eses desde la ultima mirada (cada medio segundo)
            ActividadEtapas act = c.Actividad;   // lo muestreado cada medio segundo (MuestreaEtapas)
            if (c.Eses != null && c.Eses.Encendido) { double e = act.Ultimo[ActividadEtapas.Eses]; sb.Append("   Eses: ").Append(e >= 0.1 ? "-" + e.ToString("0.0") + " dB" : "nada"); }
            // Bombo, retumbe y ataque (VozGolpeYRetumbe.cs): lo que hicieron desde la ultima mirada, solo si estan encendidos (una linea corta)
            string vg = "";
            if (c.Hundir != null && c.Hundir.Encendido) { double e = c.Hundir.BajaDb; vg += "Bombo " + (e >= 0.1 ? "-" + e.ToString("0.0") + " dB" : "nada"); }
            if (c.Retumbe != null && c.Retumbe.Encendido) { double e = act.Ultimo[ActividadEtapas.Retumbe]; vg += (vg.Length > 0 ? "   " : "") + "Retumbe " + (e >= 0.1 ? "-" + e.ToString("0.0") + " dB" : "nada"); }
            if (c.Ataque != null && c.Ataque.Encendido) { double bajo = -act.Ultimo[ActividadEtapas.AtaqueBaja], alto = act.Ultimo[ActividadEtapas.AtaqueSube]; vg += (vg.Length > 0 ? "   " : "") + "Ataque " + (bajo <= -0.1 || alto >= 0.1 ? bajo.ToString("0.0") + "/+" + alto.ToString("0.0") + " dB" : "nada"); }
            if (c.Aspereza != null && c.Aspereza.Encendido) { double e = act.Ultimo[ActividadEtapas.Asperos]; vg += (vg.Length > 0 ? "   " : "") + "Asperos " + (e >= 0.1 ? "-" + e.ToString("0.0") + " dB" : "nada"); }
            if (vg.Length > 0) sb.Append("\n").Append(vg);
            return sb.ToString();
        }
    }
}
