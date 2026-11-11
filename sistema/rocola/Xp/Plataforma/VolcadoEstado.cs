// PRUEBAS DEL PORT NATIVO, LAS VENTANAS (no son de XP): el estado de XP en puntos fijos, para compararlo con la copia congelada de la
// referencia (el mismo archivo va en una copia de trabajo de ella, solo en su Plataforma). Lo lee el bucle de la pantalla entre un
// mensaje y otro, en su hilo, sin reflexion (es un trozo de Form1: ve sus campos tal cual).
//   ROCOLA_ESTADO_EN=S:ruta[,S2:ruta2...]   a los S segundos de arrancar escribe en «ruta»: creditos, la cola, lo que suena, la letra,
//                                           la pagina, el disco y la cancion elegidos, el buscador y lo escrito, el asistente de los
//                                           botones, la atraccion, el mantenimiento (F10), la pestana de Ctrl+F3, quien tiene el foco y,
//                                           de cada hoja abierta (Ctrl+F3, Ctrl+F2, un aviso), cada pieza: clase, texto, sitio, si se
//                                           ve, si esta activa, su orden del tabulador, Checked o Value, y la del foco.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rocola
{
    internal static class VolcadoEstado
    {
        private static readonly List<KeyValuePair<double, string>> pendientes = new List<KeyValuePair<double, string>>();
        static VolcadoEstado()
        {
            string c = Environment.GetEnvironmentVariable("ROCOLA_ESTADO_EN");
            if (string.IsNullOrEmpty(c)) return;
            foreach (string e in c.Split(','))
            {
                int k = e.IndexOf(':'); double s;
                if (k > 0 && double.TryParse(e.Substring(0, k), NumberStyles.Float, CultureInfo.InvariantCulture, out s)) pendientes.Add(new KeyValuePair<double, string>(s * 1000.0, e.Substring(k + 1)));
            }
            pendientes.Sort((x, y) => x.Key.CompareTo(y.Key));
        }
        // (el bucle de la pantalla, entre un mensaje y otro)
        internal static void Mira(object principal)
        {
            if (pendientes.Count == 0) return;
            double t = PantallaRocola.AhoraMs;
            while (pendientes.Count > 0 && t >= pendientes[0].Key)
            {
                string ruta = pendientes[0].Value;
                pendientes.RemoveAt(0);
                try
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("t=").Append(((long)t).ToString(CultureInfo.InvariantCulture)).Append('\n');
                    Form1 f = principal as Form1;
                    if (f != null) f.EscribeEstadoPrueba(sb); else sb.Append("sin rocola\n");
                    File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(false));
                }
                catch (Exception ex) { Console.Error.WriteLine("[estado de prueba] " + ex.Message); }
                TrazaVentanas.Apunta("estado " + ruta);
            }
        }
        internal static string B(bool b) { return b ? "1" : "0"; }
        internal static string N(int n) { return n.ToString(CultureInfo.InvariantCulture); }
    }

    public partial class Form1
    {
        internal void EscribeEstadoPrueba(StringBuilder sb)
        {
            sb.Append("creditos=").Append(VolcadoEstado.N(credits)).Append('\n');
            sb.Append("cola=").Append(VolcadoEstado.N(queue.Count));
            foreach (PlayItem p in queue) sb.Append(" | ").Append(TrazaVentanas.Limpio(p.Display()));
            sb.Append('\n');
            sb.Append("suena=").Append(nowPlaying != null ? TrazaVentanas.Limpio(nowPlaying.Display()) : "").Append('\n');
            sb.Append("letra=").Append(currentLetter).Append('\n');
            sb.Append("pagina=").Append(VolcadoEstado.N(page)).Append('\n');
            sb.Append("disco=").Append(VolcadoEstado.N(selAlbumIndex)).Append('\n');
            sb.Append("cancion=").Append(VolcadoEstado.N(songSel)).Append('\n');
            sb.Append("buscador=").Append(VolcadoEstado.B(searchOpen)).Append(" escrito=").Append(searchOverlay != null ? TrazaVentanas.Limpio(searchOverlay.Query) : "").Append('\n');
            sb.Append("asistente_botones=").Append(VolcadoEstado.B(configMode)).Append('\n');
            sb.Append("atraccion=").Append(VolcadoEstado.B(attractOn)).Append('\n');
            sb.Append("mantenimiento=").Append(VolcadoEstado.B(maintMode)).Append('\n');
            sb.Append("f3pagina=").Append(VolcadoEstado.N(f3Pagina)).Append('\n');
            // (port nativo, ventanas2 H) el foco y la activa pueden ser de una hoja nativa (Nativo/Hoja.cs)
            sb.Append("foco=").Append(TrazaVentanas.Pieza(Hoja.Foco ?? (object)Escritorio.Foco)).Append('\n');
            sb.Append("activa=").Append(TrazaVentanas.Pieza(Hoja.Activa ?? (object)Escritorio.Activa)).Append('\n');
            foreach (Form h in Escritorio.Formas)
            {
                if (h == this || h.IsDisposed || !h.Visible) continue;
                sb.Append("hoja ").Append(TrazaVentanas.Pieza(h)).Append(" @").Append(CajaPrueba(h.Bounds)).Append('\n');
                PiezasPrueba(h, sb, "  ");
            }
            foreach (Hoja h in Hoja.Abiertas)   // (las hojas nativas van encima de las de la capa)
            {
                if (h.IsDisposed || !h.Visible) continue;
                sb.Append("hoja ").Append(TrazaVentanas.Pieza(h)).Append(" @").Append(CajaPrueba(h.Bounds)).Append('\n');
                PiezasPrueba(h.Controls, sb, "  ");
            }
        }
        // las piezas de una hoja nativa, con lo mismo que las de la capa
        private static void PiezasPrueba(Piezas ps, StringBuilder sb, string sangria)
        {
            foreach (IPieza p in ps)
            {
                CajaPieza c = p.Caja;
                sb.Append(sangria).Append(TrazaVentanas.Pieza(p)).Append(" @").Append(CajaPrueba(c.Bounds))
                  .Append(c.Visible ? " v" : " -").Append(c.Enabled ? "e" : "-").Append(" t").Append(VolcadoEstado.N(c.TabIndex));
                // (port nativo, ventanas2 C) las piezas propias de Ctrl+F3, con lo mismo que cuando eran de la capa
                Interruptor i = p as Interruptor; if (i != null) sb.Append(" checked=").Append(VolcadoEstado.B(i.Checked));
                Deslizador d = p as Deslizador; if (d != null) sb.Append(" value=").Append(VolcadoEstado.N(d.Value));
                if (Hoja.Foco == (object)p) sb.Append(" FOCO");
                sb.Append('\n');
                if (c.Hijas != null && c.Hijas.Count > 0) PiezasPrueba(c.Hijas, sb, sangria + "  ");
            }
        }
        private static string CajaPrueba(System.Drawing.Rectangle r) { return VolcadoEstado.N(r.X) + "," + VolcadoEstado.N(r.Y) + "," + VolcadoEstado.N(r.Width) + "x" + VolcadoEstado.N(r.Height); }
        private static void PiezasPrueba(Control padre, StringBuilder sb, string sangria)
        {
            foreach (Control c in padre.Controls)
            {
                sb.Append(sangria).Append(TrazaVentanas.Pieza(c)).Append(" @").Append(CajaPrueba(c.Bounds))
                  .Append(c.Visible ? " v" : " -").Append(c.Enabled ? "e" : "-").Append(" t").Append(VolcadoEstado.N(c.TabIndex));
                if (Escritorio.Foco == c) sb.Append(" FOCO");
                sb.Append('\n');
                if (c.Controls.Count > 0) PiezasPrueba(c, sb, sangria + "  ");
            }
        }
    }
}
