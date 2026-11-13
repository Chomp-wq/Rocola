// PRUEBAS DEL PORT NATIVO, LAS VENTANAS (no son de XP): lo que le llega al codigo de XP desde las ventanas, en orden, para compararlo con
// la copia congelada de la referencia (la misma traza va en una copia de trabajo de ella, solo en su Plataforma). Se apunta en el sitio
// por donde pasa cada cosa (hoy la capa de ventanas; despues Pantalla y la hoja nativas), asi la traza sobrevive a la traduccion.
//   ROCOLA_TRAZA_VENTANAS=ruta   una linea por suceso: «MS<tab>QUE», MS = milisegundos desde el arranque (el mismo reloj que
//                                ROCOLA_PULSA y ROCOLA_CAPTURA). QUE es uno de:
//     tecla DATOS [rep] a PIEZA       una tecla abajo: keyData (en hexadecimal, con los bits de Mayus/Ctrl/Alt), «rep» si es la
//                                     repeticion de la tecla mantenida (el bit 30 de WM_KEYDOWN) y quien la recibe (ProcessCmdKey)
//     pck 0|1                         lo que devolvio ProcessCmdKey (1 = atendida)
//     keydown DATOS a PIEZA [h|s]     KeyDown (con KeyPreview, primero el de la hoja); h = Handled, s = SuppressKeyPress
//     caracter N a PIEZA              el caracter (WM_CHAR) a KeyPress: en la rocola, Form_KeyPress por KeyPreview
//     keyup DATOS a PIEZA             la tecla arriba
//     tick RELOJ MS                   un reloj vencido (RELOJ = archivo:linea donde se creo; MS = su Interval)
//     encargo QUE / encargo-perdido   un BeginInvoke atendido (QUE = archivo:linea de la llamada) o perdido (su ventana ya no estaba)
//     raton EVENTO PIEZA X,Y B C D    un aviso del raton (baja, arriba, clic, doble, mueve, entra, sale, rueda) con su destino
//     foco PIEZA|nadie                quien tiene el foco del teclado
//     ventana-ve / ventana-esconde    una ventana (la rocola, una hoja) se ensena o se esconde (con su DialogResult)
//     dialogo-fin PIEZA RESULTADO     ShowDialog acabo
//     shown / closing / closed        OnShown, OnFormClosing y OnFormClosed (con la razon del cierre)
//     estado RUTA                     se escribio el estado de XP (ROCOLA_ESTADO_EN, VolcadoEstado.cs)
using System;
using System.IO;
using System.Text;

namespace Rocola
{
    internal static class TrazaVentanas
    {
        internal static readonly bool Activa;
        private static readonly StreamWriter salida;
        private static readonly object candado = new object();

        static TrazaVentanas()
        {
            string r = Environment.GetEnvironmentVariable("ROCOLA_TRAZA_VENTANAS");
            if (string.IsNullOrEmpty(r)) return;
            try { salida = new StreamWriter(r, false, new UTF8Encoding(false)); salida.AutoFlush = true; Activa = true; }
            catch (Exception ex) { Console.Error.WriteLine("[traza de ventanas] " + ex.Message); }
        }

        internal static void Apunta(string que)
        {
            if (!Activa) return;
            string ms = ((long)PantallaRocola.AhoraMs).ToString(System.Globalization.CultureInfo.InvariantCulture);
            lock (candado) { try { salida.WriteLine(ms + "\t" + que); } catch { } }
        }

        // ---- los nombres de lo que se apunta (iguales en los dos programas) ----
        internal static string Datos(int keyData) { return keyData.ToString("X", System.Globalization.CultureInfo.InvariantCulture); }
        // una pieza: su clase y, si tiene, su texto (sin saltos de linea ni tabuladores, para que la linea siga siendo una)
        internal static string Pieza(object o)
        {
            if (o == null) return "nadie";
            object vh = Anfitrion.VistaDeHueco(o); if (vh != null) o = vh;   // (armado-vistas) el hueco de una vista sin herencia en la capa, por su vista
            string n = Anfitrion.NombreDeVentana(o) ?? o.GetType().Name;   // (form1-sin-form) la ventana de la rocola en la capa, por la rocola («Form1»)
            string t = null;
            // (port nativo, ventanas2 H) las hojas y las piezas nativas (Nativo/Hoja.cs, Pieza.cs), con su texto como las de la capa
            IPieza p = o as IPieza; Hoja h = o as Hoja;
            if (p != null) t = p.Caja.Text;
            else if (h != null) t = h.Text;
            else if (o is RotuloVista) t = ((RotuloVista)o).Text;   // (rotulos-y-letras) el rotulo de la pantalla, con su texto como el Label de la capa
            else
            {
                Control c = o as Control;
                if (c == null) return n;
                try { t = c.Text; } catch { }
            }
            if (!string.IsNullOrEmpty(t)) n += "'" + Limpio(t) + "'";
            return n;
        }
        internal static string Limpio(string t)
        {
            if (t == null) return "";
            StringBuilder sb = new StringBuilder(t.Length);
            foreach (char ch in t)
            {
                if (ch == '\r') continue;
                if (ch == '\n') sb.Append("\\n");
                else if (ch == '\t') sb.Append("\\t");
                else sb.Append(ch);
            }
            return sb.ToString();
        }
        // donde se escribio una llamada (archivo:linea), sin la carpeta: la misma en la copia de la referencia y en la traduccion
        internal static string Sitio(string archivo, int linea)
        {
            if (string.IsNullOrEmpty(archivo)) return "?";
            int i = Math.Max(archivo.LastIndexOf('/'), archivo.LastIndexOf('\\'));
            return (i >= 0 ? archivo.Substring(i + 1) : archivo) + ":" + linea.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        internal static readonly string[] EventoRaton = { "baja", "clic", "mueve", "entra", "sale", "doble", "rueda", "arriba" };
    }
}
