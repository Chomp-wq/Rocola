// LOS AJUSTES DEL DUENO (en XP, Ctrl+F3 -> Sonido): los interruptores del sonido de rocola_config.txt, con los mismos nombres y valores de
// fabrica que XP. Se cambian con los botones (la vista los ensena; Controlador los maneja) y se guardan en rocola_config.txt (las demas
// lineas del archivo se conservan tal cual). Lo que cambia se oye desde la siguiente cancion (la cadena se arma al empezar cada una).
// Con el modo billar encendido, lo que decide el modo va "en gris" con el motivo, como en XP, y conserva lo guardado.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
        public sealed class Ajuste
        {
            public string Nombre, Clave, Grupo;
            internal Func<string> Valor;            // como se ve
            internal Func<string> Guardado;         // como va en rocola_config.txt
            internal Action<int> Cambia;            // +1 / -1
            internal Func<string> Bloqueo;          // null si se puede cambiar; si no, el motivo
            public string Texto { get { return Valor(); } }
            public string Motivo { get { return Bloqueo != null ? Bloqueo() : null; } }
            public void Mueve(int d) { if (Motivo == null) Cambia(d); }
        }
        private List<Ajuste> ajustes;
        private const string PorElBillar = "lo decide el modo billar";

        private Ajuste Interruptor(string grupo, string nombre, string clave, Func<bool> lee, Action<bool> pon, bool delBillar)
        {
            Ajuste a = new Ajuste { Grupo = grupo, Nombre = nombre, Clave = clave };
            a.Valor = delegate { return lee() ? "SÍ" : "NO"; };
            a.Guardado = delegate { return lee() ? "1" : "0"; };
            a.Cambia = delegate (int d) { pon(!lee()); };
            if (delBillar) a.Bloqueo = delegate { return localAuto ? PorElBillar : null; };
            return a;
        }

        public IList<Ajuste> Ajustes()
        {
            if (ajustes != null) return ajustes;
            List<Ajuste> l = new List<Ajuste>();
            l.Add(Interruptor("El billar", "Modo billar (la cadena para las bocinas del billar)", "local_auto", () => localAuto, v => localAuto = v, false));
            l.Add(Interruptor("El local", "Claridad para el local", "claridad_local", () => claridadLocal, v => claridadLocal = v, true));
            l.Add(Interruptor("El local", "Que las partes suaves se oigan", "partes_suaves", () => partesSuaves, v => partesSuaves = v, true));
            l.Add(Interruptor("El local", "Presencia en sala ruidosa", "presencia_sala", () => presenciaSala, v => presenciaSala = v, true));
            l.Add(Interruptor("El local", "Quitar el subgrave", "corta_subgrave", () => cortaSubgrave, v => cortaSubgrave = v, true));
            l.Add(Interruptor("El local", "Estereo mas estrecho", "estereo_estrecho", () => estereoEstrecho, v => estereoEstrecho = v, true));
            l.Add(Interruptor("El local", "Suavizar las eses", "suaviza_eses", () => suavizaEses, v => suavizaEses = v, true));
            l.Add(Interruptor("Voz, golpe y retumbe", "Retumbe dinamico", "retumbe_dinamico", () => retumbeDinamico, v => retumbeDinamico = v, true));
            l.Add(Interruptor("Voz, golpe y retumbe", "Ataque mas claro", "ataque_claro", () => ataqueClaro, v => ataqueClaro = v, true));
            l.Add(Interruptor("Voz, golpe y retumbe", "Agudos asperos dinamicos", "aspereza_dinamica", () => asperezaDinamica, v => asperezaDinamica = v, true));
            l.Add(Interruptor("Voz, golpe y retumbe", "Que el bombo no hunda la voz", "bombo_sin_hundir", () => bomboSinHundir, v => bomboSinHundir = v, true));
            l.Add(Interruptor("Lo que suena ahora", "Saltar los silencios largos", "saltar_silencios", () => saltaSilencios, v => saltaSilencios = v, false));
            l.Add(Interruptor("Lo que suena ahora", "Quitar el zumbido de la corriente", "quita_zumbido", () => quitaZumbido, v => quitaZumbido = v, false));
            l.Add(Interruptor("Lo que suena ahora", "Quitar el pre-eco de los MP3 viejos", "quita_preeco", () => quitaPreEco, v => quitaPreEco = v, false));
            l.Add(Interruptor("Lo que suena ahora", "Un canal mudo, por los dos", "canal_mudo", () => canalMudo, v => canalMudo = v, false));
            l.Add(Interruptor("Lo que suena ahora", "Quitar la corriente continua", "quita_continua", () => quitaContinua, v => quitaContinua = v, false));
            Ajuste dsee = Interruptor("Mejoras con IA", "Reconstruir los agudos (DSEE)", "dsee", () => sbrOn, v => sbrOn = v, true);
            dsee.Bloqueo = delegate { return !sbrAllowed ? "este equipo no llega (" + sbrReason + ")" : localAuto ? PorElBillar : null; };
            l.Add(dsee);
            l.Add(Interruptor("Mejoras con IA", "Bajo y brillo a la medida (I1)", "a_la_medida", () => aLaMedida, v => aLaMedida = v, false));
            l.Add(Interruptor("Mejoras con IA", "Quitar el siseo de las grabaciones viejas (I2)", "quita_siseo", () => quitaSiseo, v => quitaSiseo = v, true));
            float[] fundidos = { 0f, 0.5f, 1f, 1.5f, 2f, 3f };
            Ajuste fu = new Ajuste { Grupo = "Entre canciones", Nombre = "Fundido entre canciones", Clave = "fundido" };
            fu.Valor = delegate { return crossfadeSec <= 0.01f ? "sin fundido" : crossfadeSec.ToString("0.#", CultureInfo.InvariantCulture) + " s"; };
            fu.Guardado = delegate { return crossfadeSec.ToString("0.##", CultureInfo.InvariantCulture); };
            fu.Cambia = delegate (int d)
            {
                int i = 0; for (int j = 0; j < fundidos.Length; j++) if (Math.Abs(fundidos[j] - crossfadeSec) < 0.05f) i = j;
                crossfadeSec = fundidos[Math.Max(0, Math.Min(fundidos.Length - 1, i + d))];
            };
            l.Add(fu);
            Ajuste vv = new Ajuste { Grupo = "Videos", Nombre = "Volumen de los videos", Clave = "video_vol" };
            vv.Valor = delegate { return videoVol + " %"; };
            vv.Guardado = delegate { return videoVol.ToString(CultureInfo.InvariantCulture); };
            vv.Cambia = delegate (int d) { videoVol = Math.Max(0, Math.Min(100, videoVol + 5 * d)); };
            l.Add(vv);
            ajustes = l;
            return l;
        }

        // A rocola_config.txt: cada clave de los ajustes en su linea (cambiada o anadida); lo demas, tal cual.
        public void GuardaAjustes()
        {
            List<string> lineas = new List<string>();
            try { if (File.Exists(ConfigFile)) lineas.AddRange(File.ReadAllLines(ConfigFile)); } catch { }
            foreach (Ajuste a in Ajustes())
            {
                string linea = a.Clave + "=" + a.Guardado();
                int i = lineas.FindIndex(x => x.Trim().StartsWith(a.Clave + "=", StringComparison.Ordinal));
                if (i >= 0) lineas[i] = linea; else lineas.Add(linea);
            }
            StringBuilder sb = new StringBuilder();
            foreach (string x in lineas) sb.Append(x).Append("\r\n");
            EscribeYa(ConfigFile, sb.ToString());
        }
    }
}
