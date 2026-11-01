// LO QUE LA PANTALLA VIVA DE XP ENSENA (PantallaVivaGpu.cs, peticion del 24-09-2026), sin el dibujo: la ficha rapida de un disco (sus tres
// canciones mas pedidas -MasPedidas, tal cual-, cuantas suyas van ya en la cola y lo que dura cada una) y lo que dura una cancion
// (DurText). Sus interruptores, con las claves de XP (encendidos de fabrica).
using System;
using System.Collections.Generic;

namespace Rocola
{
    public partial class Form1
    {
        public bool FichaDisco = true;     // "ficha_disco="
        public bool LetrasLupa = true;     // "letras_lupa="
        public bool PaginaDesliza = true;  // "pagina_desliza="
        // tanda 2 (dentro del disco) y tanda 3 (lo que suena y los creditos) de XP: DiscoVivoGpu.cs y SuenaVivoGpu.cs
        public bool ListaGrande = true;    // "lista_grande=": filas mas altas y letra mas grande
        public bool EqSonando = true;      // "eq_sonando=": la que suena lleva cuatro barritas que se mueven con la musica
        public bool ColaMinutos = true;    // "cola_minutos=": "EN COLA 3 · en ~9 min" en las filas
        public bool VueloCola = true;      // "vuelo_cola=": el boleto vuela de la fila a la cola
        public bool MonedaCae = true;      // "moneda_cae=": cae una moneda dorada hasta los creditos
        public bool SinCreditosInvita = true;   // "sin_creditos_invita=": sin creditos, una tarjeta amable en vez del letrero
        public bool AvisosClaros = true;   // "avisos_claros=": los avisos con su color, su icono y dos renglones

        public sealed class Ficha
        {
            public bool Pedidas;                       // false: nadie las ha pedido aun (van las tres primeras)
            public List<string> Titulos = new List<string>(), Duraciones = new List<string>();
            public List<int> Veces = new List<int>();
            public int EnCola;
        }
        public Ficha FichaDe(Disco d)
        {
            Ficha f = new Ficha();
            if (d == null) return f;
            bool pedidas; List<int> top = MasPedidas(d.Rutas, songCounts, out pedidas);
            f.Pedidas = pedidas;
            foreach (int i in top)
            {
                f.Titulos.Add(d.Titulos[i]); f.Duraciones.Add(DurText(d.Rutas[i]));
                int v = 0; if (d.Rutas[i] != null) songCounts.TryGetValue(d.Rutas[i], out v); f.Veces.Add(v);
            }
            HashSet<string> suyas = new HashSet<string>(d.Rutas, StringComparer.OrdinalIgnoreCase);
            foreach (PlayItem q in queue) if (q != null && suyas.Contains(q.PathKey())) f.EnCola++;
            return f;
        }
        public string DurText(string path)
        {
            float s = SegundosDe(path);
            if (s <= 0f) return "";
            int tot = (int)(s + 0.5f);
            return (tot / 60) + ":" + (tot % 60).ToString("00");
        }
        // Las tres mas pedidas (por las veces que entraron en la cola), de mas a menos; a igualdad, en el orden del disco. Si ninguna se ha
        // pedido nunca, 'pedidas' sale false y van las tres primeras. (PantallaVivaGpu.cs de XP, tal cual.)
        internal static List<int> MasPedidas(IList<string> rutas, IDictionary<string, int> cuentas, out bool pedidas)
        {
            List<int> r = new List<int>();
            pedidas = false;
            if (rutas == null) return r;
            int n = rutas.Count;
            int[] veces = new int[n];
            for (int i = 0; i < n; i++)
            {
                int v = 0;
                if (cuentas != null && rutas[i] != null) cuentas.TryGetValue(rutas[i], out v);
                veces[i] = v;
                if (v > 0) pedidas = true;
            }
            if (!pedidas) { for (int i = 0; i < n && i < 3; i++) r.Add(i); return r; }
            for (int k = 0; k < 3; k++)
            {
                int mejor = -1;
                for (int i = 0; i < n; i++)
                    if (veces[i] > 0 && !r.Contains(i) && (mejor < 0 || veces[i] > veces[mejor])) mejor = i;
                if (mejor < 0) break;
                r.Add(mejor);
            }
            return r;
        }
    }
}
