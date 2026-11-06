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
            if (unchecked(ahora - ultimaRevisionActualizacion) < 15000) return;   // (y cada 15 s, lo que pasa ahora para el buzon)
            ultimaRevisionActualizacion = ahora;
            EscribeVivo();
            if (File.Exists("/run/rocola-reinicia-programa-ya"))
            {   // pedido desde casa «ya»: aunque este sonando (lo que suena y la cola se guardan y vuelven)
                try { File.Delete("/run/rocola-reinicia-programa-ya"); } catch { }
                Console.WriteLine("Buzon: reiniciar el programa ya (pedido desde casa)");
                motor.GuardaEstado(); CodigoSalida = SalidaParaActualizar; salir = true; return;
            }
            // EL BUZON (7-11-2026, rocola-buzon): desde casa se puede pedir reiniciar el programa o el equipo, o el diagnostico completo;
            // como las actualizaciones, solo cuando la rocola esta libre.
            bool reinicia = File.Exists("/run/rocola-reinicia-programa"), equipo = File.Exists("/run/rocola-reinicia-equipo"), diag = File.Exists("/run/rocola-pide-diagnostico");
            bool programa = File.Exists("/run/rocola-hay-nueva") || reinicia, sistema = File.Exists("/run/rocola-reinicio-sistema") || equipo;
            if (!programa && !sistema && !diag) return;
            bool libre = motor.Sonando == null && !motor.SonandoVideo && motor.EnCola == 0 && video == null && salida == null
                && AjusteSel < 0 && AsistentePaso < 0 && !Buscando && !AyudaAbierta && DiagEscena < 0
                && unchecked(ahora - ultimoToque) > 120000;
            if (!libre) return;
            if (diag && !programa && !sistema)
            {
                try { File.Delete("/run/rocola-pide-diagnostico"); } catch { }
                Console.WriteLine("Buzon: el diagnostico completo, pedido desde casa");
                motor.PideDiagnostico = true;   // (lo corre RevisaDiagnostico y lo deja en rocola_diagnostico.txt; el buzon lo sube)
                return;
            }
            try { File.Delete("/run/rocola-reinicia-programa"); File.Delete("/run/rocola-reinicia-equipo"); } catch { }
            Console.WriteLine("Actualizacion: hay " + (sistema ? "un sistema nuevo" : "una version nueva") + " y la rocola esta libre: se reinicia para ponerlo");
            motor.GuardaEstado();
            CodigoSalida = sistema ? SalidaParaSistema : SalidaParaActualizar;
            salir = true;
        }

        // LO QUE PASA AHORA, para el buzon (/run/rocola-vivo.txt; rocola-buzon lo sube con el estado): cada 15 s.
        private void EscribeVivo()
        {
            try
            {
                int ahora = Environment.TickCount;
                string t = "programa " + Version.Numero + " (" + Version.Commit + ")\n"
                    + "suena: " + (motor.SonandoTitulo ?? "nada") + "\n"
                    + "en cola: " + motor.EnCola + "   creditos: " + motor.Creditos + "\n"
                    + "ultimo boton: hace " + (unchecked(ahora - ultimoToque) / 60000) + " min\n"
                    + "modo noche: " + (Form1.nocheOn ? "encendido" : "apagado") + ", volumen ahora al " + Math.Round(100 * motor.FactorNoche()) + " % de la senal\n"
                    + "spotify: " + (!motor.SpotifyOn ? "apagado" : motor.SpotifyListo ? "conectado" : "encendido, sin conexion") + "\n"
                    + "pantalla: " + (AjusteSel >= 0 ? "Ctrl+F3 abierto" : Buscando ? "buscador" : dentro != null ? "dentro de un disco" : "pared") + "\n";
                File.WriteAllText("/run/rocola-vivo.txt.n", t);
                File.Move("/run/rocola-vivo.txt.n", "/run/rocola-vivo.txt", true);
            }
            catch { }
        }
    }
}
