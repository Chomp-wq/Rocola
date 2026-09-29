using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Rocola
{
    // ============================== LO QUE TARDA, EN LA BITACORA ==============================
    //
    // Tres esperas que se notan en el bar y que no se pueden medir fuera de su equipo (su disco, su procesador, su
    // Windows): cuanto espera la ventana a las tablas al arrancar, cuanto tarda la biblioteca en estar lista (y dentro,
    // recorrer las carpetas para saber si la guardada sigue valiendo) y cuanto tarda cada cancion en empezar a sonar.
    // Con "diagnostico=1" en la configuracion quedan en rocola_diagnostico.txt; sin eso no se escribe nada. Tomar los
    // tiempos no cuesta nada (dos lecturas del reloj por cosa) y no cambia lo que hace la rocola.
    public partial class Form1
    {
        private int bitacoraArranque = Environment.TickCount;   // cuando nacio la ventana
        private int bitacoraBiblioteca, bitacoraFirmaMs = -1, bitacoraCancion;
        private bool bitacoraBibliotecaDicha;

        private static int Desde(int tick) { return unchecked(Environment.TickCount - tick); }

        // Al publicar las tablas del arranque.
        private void ApuntaArranque(int leerMs, int esperaMs)
        {
            if (!diagOn) return;
            Diag("arranque: tablas leidas en " + leerMs + " ms (" + loudCache.Count + " canciones con volumen); la ventana espero "
                 + esperaMs + " ms; " + Desde(bitacoraArranque) + " ms desde que nacio la ventana");
        }
        // Al terminar de armar la biblioteca.
        private void ApuntaBiblioteca(bool deCache)
        {
            if (!diagOn) return;
            Diag("biblioteca: lista en " + Desde(bitacoraBiblioteca) + " ms (" + (deCache ? "la guardada" : "recorriendo el disco") + "); "
                 + (bitacoraFirmaMs >= 0 ? "comprobar la guardada (recorrer las carpetas) " + bitacoraFirmaMs + " ms; " : "")
                 + tracks.Count + " canciones, " + albums.Count + " discos"
                 + (bitacoraBibliotecaDicha ? "" : "; " + Desde(bitacoraArranque) + " ms desde que nacio la ventana"));
            bitacoraBibliotecaDicha = true;
        }
        // Cuando la cancion ya suena.
        private void ApuntaCancion(int abiertaMs)
        {
            if (!diagOn) return;
            Diag("cancion: abierta en " + abiertaMs + " ms, sonando en " + Desde(bitacoraCancion) + " ms (en memoria hasta ahora: "
                 + EnMemoriaDelAdelanto + " de la lectura por adelantado, " + EnMemoriaLeidas + " leidas al abrir)");
        }
    }

    // ============================== LA BITACORA ESCRIBE DETRAS DE LA PANTALLA ==============================
    //
    // rocola_diagnostico.txt (con "diagnostico=1"), rocola_error.txt y rocola_incompatibles.txt se escribian en el hilo que
    // apuntaba, muchas veces el de la pantalla: abrir el archivo, mirar cuanto mide y pegarle la linea. Con el diagnostico
    // encendido son varias lineas por cancion, y en un disco mecanico ocupado cada una puede tardar decenas de milisegundos:
    // la bitacora que sirve para cazar los tirones los provocaba. Ahora quien apunta deja la linea -con su hora de ahora-
    // en una cola y sigue; un hilo de fondo las escribe en el mismo orden, de a tandas. Lo que dice cada archivo, su tope de
    // tamano y el sello de version no cambian (el sello lo saca el hilo que escribe: mirar la fecha del programa tambien es
    // ir al disco). Si el archivo esta ocupado un momento -un antivirus- se reintenta en vez de perder la linea. Al cerrar y
    // ante un fallo que puede terminar el proceso se escribe todo ahi mismo (Vacia).
    internal static class Bitacora
    {
        private sealed class Apunte { public string Ruta, Cabeza, Cuerpo; public long Tope; public bool ConSello; }
        private static readonly object cerrojoCola = new object();    // la cola
        private static readonly object cerrojoDisco = new object();   // quien escribe: uno a la vez
        private static readonly Queue<Apunte> cola = new Queue<Apunte>();
        private static bool trabajando;          // hay un hilo de fondo escribiendo o por empezar
        private static int descartados;          // lo que no cupo en la cola
        private const int MaximoEnCola = 4000;   // si el disco no da abasto, la cola no crece sin limite
        private static string sello;             // "  (rocola del ...)", sacado una vez en el hilo que escribe

        // Una linea tal cual (el diagnostico). Tope: por encima de ese tamano el archivo empieza de nuevo (0: sin tope).
        internal static void Apunta(string ruta, long tope, string texto) { Mete(ruta, tope, texto, null, false); }
        // Un apunte de error: la cabeza, el sello de version y el cuerpo, como siempre.
        internal static void ApuntaConSello(string ruta, long tope, string cabeza, string cuerpo) { Mete(ruta, tope, cabeza, cuerpo, true); }

        private static void Mete(string ruta, long tope, string cabeza, string cuerpo, bool conSello)
        {
            if (string.IsNullOrEmpty(ruta)) return;
            Apunte a = new Apunte(); a.Ruta = ruta; a.Tope = tope; a.Cabeza = cabeza ?? ""; a.Cuerpo = cuerpo; a.ConSello = conSello;
            lock (cerrojoCola)
            {
                if (cola.Count >= MaximoEnCola) { descartados++; return; }
                cola.Enqueue(a);
                if (trabajando) return;
                trabajando = true;
            }
            bool encolado = false;
            try { encolado = ThreadPool.QueueUserWorkItem(Trabaja); } catch { }
            if (!encolado) lock (cerrojoCola) trabajando = false;   // sin hilos (al cerrar): queda en la cola para Vacia
        }

        private static void Trabaja(object nada)
        {
            while (true)
            {
                try { lock (cerrojoDisco) EscribeLoPendiente(); } catch { }
                lock (cerrojoCola) { if (cola.Count == 0) { trabajando = false; return; } }
            }
        }

        // Escribe ahora, en este hilo, todo lo pendiente: al cerrar, ante un fallo grave y en las pruebas. Espera como mucho
        // 'ms' a que acabe lo que estuviera escribiendo el hilo de fondo.
        internal static bool Vacia(int ms)
        {
            if (!Monitor.TryEnter(cerrojoDisco, ms)) return false;
            try { EscribeLoPendiente(); }
            catch { }
            finally { Monitor.Exit(cerrojoDisco); }
            return true;
        }

        // Con cerrojoDisco tomado. Lo seguido que va al mismo archivo sale de una sola vez.
        private static void EscribeLoPendiente()
        {
            Apunte[] tanda; int perdidos;
            lock (cerrojoCola)
            {
                if (cola.Count == 0) return;
                tanda = cola.ToArray(); cola.Clear();
                perdidos = descartados; descartados = 0;
            }
            if (sello == null)
            {
                string s = "";
                try { s = Form1.DeCuandoEs(); } catch { }
                sello = s.Length > 0 ? "  (rocola del " + s + ")" : "";
            }
            StringBuilder sb = new StringBuilder();
            int i = 0;
            while (i < tanda.Length)
            {
                string ruta = tanda[i].Ruta; long tope = tanda[i].Tope;
                sb.Length = 0;
                if (perdidos > 0) { sb.Append("(" + perdidos + " apuntes sin escribir: el disco no daba abasto)\r\n"); perdidos = 0; }
                for (; i < tanda.Length && string.Equals(tanda[i].Ruta, ruta, StringComparison.OrdinalIgnoreCase); i++)
                {
                    Apunte a = tanda[i];
                    sb.Append(a.Cabeza);
                    if (a.ConSello) sb.Append(sello);
                    if (a.Cuerpo != null) sb.Append(a.Cuerpo);
                }
                Pega(ruta, tope, sb.ToString());
            }
        }

        // El tope y el pegado de siempre (UTF-8 sin marca). Si el archivo esta ocupado, se reintenta durante un segundo.
        private static void Pega(string ruta, long tope, string texto)
        {
            if (tope > 0) { try { FileInfo fi = new FileInfo(ruta); if (fi.Exists && fi.Length > tope) fi.Delete(); } catch { } }
            for (int intento = 0; ; intento++)
            {
                try { File.AppendAllText(ruta, texto); return; }
                catch (DirectoryNotFoundException) { return; }
                catch (PathTooLongException) { return; }
                catch (IOException) { if (intento >= 20) return; Thread.Sleep(50); }
                catch { return; }
            }
        }
    }
}
