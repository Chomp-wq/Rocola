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
        public bool Anima { get { return false; } }
        internal VistaTexto() { Console.Write("\x1b[?25l\x1b[2J"); }   // sin cursor
        public void Dispose() { Console.Write("\x1b[?25h\x1b[0m\n"); }
        public void Suelta() { }
        public void Recupera() { pantallaAntes = null; Console.Write("\x1b[2J"); }

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
            if (c.AjusteSel >= 0)
            {
                sb.Append("\x1b[1;37;44m").Append(Corta(" AJUSTES DEL SONIDO (REGRESAR: guardar y salir)", ancho)).Append("\x1b[0m\n");
                IList<Form1.Ajuste> l = c.ListaAjustes; int n = c.AjustesTotal, desde = Math.Max(0, Math.Min(c.AjusteSel - alto / 2, n - alto));
                for (int i = 0; i < alto + 3; i++)
                {
                    int j = desde + i; string t = "";
                    if (j < n - 1) { Form1.Ajuste a = l[j]; t = " " + a.Nombre.PadRight(48) + " " + a.Texto + (a.Motivo != null ? "  (" + a.Motivo + ")" : ""); }
                    else if (j == n - 1) t = " Configurar los botones de la rocola (PONER)";
                    sb.Append(j == c.AjusteSel ? "\x1b[7m" : "").Append(Corta(t, ancho)).Append("\x1b[0m\n");
                }
                sb.Append("\x1b[1;33m").Append(Corta(" " + c.AvisoVigente, ancho)).Append("\x1b[0m");
                string q3 = sb.ToString(); if (q3 != pantallaAntes) { pantallaAntes = q3; Console.Write("\x1b[H" + q3); }
                return;
            }
            if (c.Buscando)
            {
                sb.Append("\x1b[1;37;44m").Append(Corta(" BUSCAR: " + c.Escrito + "_", ancho)).Append("\x1b[0m\n");
                for (int f = 0; f < Controlador.FilasTeclado.Length; f++)
                {
                    StringBuilder fl = new StringBuilder(" ");
                    for (int i = 0; i < Controlador.FilasTeclado[f].Length; i++)
                    {
                        string et = Controlador.Etiqueta(Controlador.FilasTeclado[f][i]);
                        fl.Append(!c.EnResultados && f == c.FilaTecla && i == c.ColTecla ? "[" + et + "]" : " " + et + " ");
                    }
                    sb.Append(Corta(fl.ToString(), ancho)).Append('\n');
                }
                sb.Append(new string('-', ancho)).Append('\n');
                for (int i = 0; i < alto - 3; i++)
                {
                    if (i < c.Resultados.Count) sb.Append(c.EnResultados && i == c.SelResultado ? "\x1b[7m" : "").Append(Corta(" " + (c.Resultados[i].Tipo == 0 ? "ARTISTA " : c.Resultados[i].Tipo == 1 ? "DISCO   " : c.Resultados[i].Tipo == 3 ? "SERVICIO" : "CANCION ") + c.Resultados[i].Texto, ancho)).Append("\x1b[0m");
                    else sb.Append(new string(' ', ancho));
                    sb.Append('\n');
                }
                string q2 = sb.ToString(); if (q2 != pantallaAntes) { pantallaAntes = q2; Console.Write("\x1b[H" + q2); }
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
                sb.Append(Corta(" IZQ/DER: disco  ARRIBA/ABAJO: pagina  REGRESAR+IZQ/DER: letra  REGRESAR+ARRIBA: buscar", ancho)).Append('\n');
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
                sb.Append(Corta(" ARRIBA/ABAJO: cancion  IZQ/DER: pagina  PONER: pedirla (1 credito)  REGRESAR: a los discos", ancho)).Append('\n');
            }
            sb.Append("\x1b[1;33m").Append(Corta(c.AvisoVigente.Length > 0 ? " " + c.AvisoVigente : "", ancho)).Append("\x1b[0m");
            string p = sb.ToString();
            if (p == pantallaAntes) return;
            pantallaAntes = p;
            Console.Write("\x1b[H" + p);
        }
    }
}
