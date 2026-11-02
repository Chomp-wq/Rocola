// LA CALIDAD DE LA BIBLIOTECA (CuentaCalidad de XP, Form1.Datos.cs, tal cual): por donde el MP3 corto los agudos, medido en el repaso
// (la tabla del EQ): completas desde 19,3 kHz, aceptables desde 16,5 kHz, pobres por debajo; y cuantas traen los picos recortados.
// Lo ensena la tarjeta «Tu biblioteca» de Ctrl+F3.
using System.Collections.Generic;
using System.Globalization;

namespace Rocola
{
    public partial class Form1
    {
        private const float CalBuenaHz = 19300f, CalMediaHz = 16500f;
        private void CuentaCalidad(out int buenas, out int medias, out int pobres, out int recortadas, out int sinMedir)
        {
            buenas = medias = pobres = recortadas = sinMedir = 0;
            foreach (KeyValuePair<string, float[]> kv in eqCache)
            {
                float[] c = kv.Value;
                if (c == null || c.Length < EqLen || c[EqCut] <= 100f) { sinMedir++; continue; }
                if (c[EqCut] >= CalBuenaHz) buenas++;
                else if (c[EqCut] >= CalMediaHz) medias++;
                else pobres++;
                if (c[EqClip] > 0.001f) recortadas++;
            }
        }
        // Los renglones de la tarjeta (los de XP: la calidad y como va el repaso).
        private List<string> bibTexto; private int bibTick;
        public List<string> TextoBiblioteca()
        {
            // la pantalla la pide en cada cuadro: se rehace una vez por segundo (recorrer la tabla entera cada cuadro no hace falta)
            if (bibTexto != null && unchecked(System.Environment.TickCount - bibTick) < 1000) return bibTexto;
            bibTick = System.Environment.TickCount;
            List<string> r = new List<string>();
            int cb, cm, cp, cr, cs;
            try { CuentaCalidad(out cb, out cm, out cp, out cr, out cs); } catch { cb = cm = cp = cr = 0; }
            CultureInfo ci = CultureInfo.InvariantCulture;
            r.Add(cb + cm + cp > 0
                ? "Calidad:   " + cb.ToString("#,0", ci) + " completas     " + cm.ToString("#,0", ci) + " aceptables     " + cp.ToString("#,0", ci) + " pobres" + (cr > 0 ? "     " + cr.ToString("#,0", ci) + " con los picos recortados" : "")
                : "Calidad: aun sin medir. Deja que el repaso vaya haciendo su trabajo.");
            int falta = porRepasar.Count;
            r.Add(falta > 0 ? "La biblioteca se mide sola mientras la rocola trabaja: faltan " + falta.ToString("#,0", ci) + " canciones."
                            : "La biblioteca se mide sola mientras la rocola trabaja: ya esta al dia.");
            r.Add(TextoMaterialIa);
            bibTexto = r;
            return r;
        }
    }
}
