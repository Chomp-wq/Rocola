// LA VERSION NUEVA SE PONE SOLA, EN CUANTO LA ROCOLA ESTA LIBRE (peticion del 1-11-2026). rocola-actualiza (en el sistema) la baja de
// GitHub, la comprueba y deja /run/rocola-hay-nueva; aqui, cuando no suena nada, no hay nada en la cola ni video, ninguna hoja esta
// abierta y nadie ha tocado un boton en 2 minutos, la rocola guarda su estado (los creditos se conservan) y sale con 42: /usr/bin/rocola
// pone la nueva y la vuelve a abrir. Si la nueva se cae al arrancar, /usr/bin/rocola vuelve a la anterior.
using System;
using System.IO;

namespace Rocola
{
    internal sealed partial class Controlador
    {
        internal const int SalidaParaActualizar = 42, SalidaParaSistema = 43;   // 43: un sistema nuevo listo: /usr/bin/rocola reinicia el equipo
        internal int CodigoSalida;
        private int ultimaRevisionActualizacion;
        private void RevisaActualizacion()
        {
            int ahora = Environment.TickCount;
            if (unchecked(ahora - ultimaRevisionActualizacion) < 15000) return;
            ultimaRevisionActualizacion = ahora;
            bool programa = File.Exists("/run/rocola-hay-nueva"), sistema = File.Exists("/run/rocola-reinicio-sistema");
            if (!programa && !sistema) return;
            bool libre = motor.Sonando == null && !motor.SonandoVideo && motor.EnCola == 0 && video == null && salida == null
                && AjusteSel < 0 && AsistentePaso < 0 && !Buscando && !AyudaAbierta && DiagEscena < 0
                && unchecked(ahora - ultimoToque) > 120000;
            if (!libre) return;
            Console.WriteLine("Actualizacion: hay " + (sistema ? "un sistema nuevo" : "una version nueva") + " y la rocola esta libre: se reinicia para ponerlo");
            motor.GuardaEstado();
            CodigoSalida = sistema ? SalidaParaSistema : SalidaParaActualizar;
            salir = true;
        }
    }
}
