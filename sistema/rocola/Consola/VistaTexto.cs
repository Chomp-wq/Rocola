// LA ROCOLA EN MODO TEXTO: la vista en la consola (tty1 en Rocola OS o cualquier terminal), con codigos ANSI. Para probar sin pantalla
// grafica, y de repuesto si la grafica no arranca.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Rocola
{
    internal sealed class VistaTexto : IVista
    {
        private string pantallaAntes;
        public bool LeeTerminal { get { return true; } }
        internal VistaTexto() { Console.Write("\x1b[?25l\x1b[2J"); }   // sin cursor
        public void Dispose() { Console.Write("\x1b[?25h\x1b[0m\n"); }

        private static int Filas() { try { return Math.Max(12, Console.WindowHeight); } catch { return 25; } }
        private static int Columnas() { try { return Math.Max(40, Console.WindowWidth); } catch { return 80; } }
        private static int FilasLista() { return Filas() - 7; }
        private static string Corta(string s, int n) { s = s ?? ""; return s.Length <= n ? s.PadRight(n) : s.Substring(0, n - 1) + "…"; }

        public void Dibuja(Controlador c)
        {
            Form1 motor = c.motor; c.PorPagina = FilasLista();
            int ancho = Columnas(), alto = FilasLista();
            StringBuilder sb = new StringBuilder();
            string sonando = motor.Sonando;
            if (c.AsistentePaso >= 0)
            {
                sb.Append("\x1b[1;37;44m").Append(Corta(" CONFIGURACION DE BOTONES DE LA ROCOLA", ancho)).Append("\x1b[0m\n\n");
                sb.Append(Corta(" Paso " + (c.AsistentePaso + 1) + " de " + Controlador.PasosAsistente.Length, ancho)).Append("\n\n");
                sb.Append(Corta(" Presiona en la rocola el boton para:", ancho)).Append("\n\n");
                sb.Append("\x1b[1;33m").Append(Corta(" >>>   " + Controlador.PasosAsistente[c.AsistentePaso] + "   <<<", ancho)).Append("\x1b[0m\n\n");
                sb.Append(Corta(" " + c.AsistenteAviso, ancho)).Append("\n\n").Append(Corta(" (cada boton debe ser distinto)", ancho)).Append('\n');
                for (int i = 0; i < alto - 6; i++) sb.Append(new string(' ', ancho)).Append('\n');
                string q = sb.ToString(); if (q != pantallaAntes) { pantallaAntes = q; Console.Write("\x1b[H" + q); }
                return;
            }
            sb.Append("\x1b[1;37;44m").Append(Corta(" ROCOLA OS   Creditos: " + motor.Creditos + "   En la cola: " + motor.EnCola, ancho)).Append("\x1b[0m\n");
            sb.Append(Corta(" Suena: " + (sonando != null ? Path.GetFileNameWithoutExtension(sonando) : "(nada)"), ancho)).Append('\n');
            if (c.dentro == null)
            {
                StringBuilder l = new StringBuilder(" Letra: ");
                for (int i = 0; i < c.letras.Count; i++) l.Append(i == c.letra ? "[" + Controlador.NombreLetra(c.letras[i]) + "]" : " " + Controlador.NombreLetra(c.letras[i]) + " ");
                sb.Append(Corta(l.ToString(), ancho)).Append('\n').Append(new string('-', ancho)).Append('\n');
                List<Form1.Disco> ds = c.DiscosDeLetra();
                int desde = Math.Max(0, Math.Min(c.cursorDisco - alto / 2, ds.Count - alto));
                for (int i = 0; i < alto; i++)
                {
                    int k = desde + i;
                    if (k < ds.Count) sb.Append(k == c.cursorDisco ? "\x1b[7m" : "").Append(Corta(" " + ds[k] + "  (" + ds[k].Rutas.Count + ")", ancho)).Append("\x1b[0m");
                    else sb.Append(new string(' ', ancho));
                    sb.Append('\n');
                }
                sb.Append(Corta(" ARRIBA/ABAJO: disco   IZQ/DER: letra   PONER: entrar   MONEDA   SALTAR", ancho)).Append('\n');
            }
            else
            {
                sb.Append(Corta(" Disco: " + c.dentro, ancho)).Append('\n').Append(new string('-', ancho)).Append('\n');
                int n = c.dentro.Rutas.Count, desde = Math.Max(0, Math.Min(c.cursorCancion - alto / 2, n - alto));
                for (int i = 0; i < alto; i++)
                {
                    int k = desde + i;
                    if (k < n) sb.Append(k == c.cursorCancion ? "\x1b[7m" : "").Append(Corta(" " + (k + 1).ToString().PadLeft(3) + ". " + c.dentro.Titulos[k], ancho)).Append("\x1b[0m");
                    else sb.Append(new string(' ', ancho));
                    sb.Append('\n');
                }
                sb.Append(Corta(" ARRIBA/ABAJO: cancion   PONER: pedirla (1 credito)   REGRESAR: a los discos", ancho)).Append('\n');
            }
            sb.Append("\x1b[1;33m").Append(Corta(c.AvisoVigente.Length > 0 ? " " + c.AvisoVigente : "", ancho)).Append("\x1b[0m");
            string p = sb.ToString();
            if (p == pantallaAntes) return;
            pantallaAntes = p;
            Console.Write("\x1b[H" + p);
        }
    }
}
