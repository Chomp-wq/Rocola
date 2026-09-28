using System;

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
}
