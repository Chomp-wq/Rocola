// EL DIAGNOSTICO COMPLETO (en XP, Ctrl+F3 -> Imagen -> «Diagnostico completo», DiagnosticoCompleto.cs; decision del 23-10-2026: es una
// herramienta, sin preferencia guardada: no guarda ni aplica nada). Recorre las escenas de la pantalla, unos segundos cada una, moviendo
// lo que se moveria con los botones, y mide los INTERVALOS ENTRE CUADROS PRESENTADOS (no solo los cuadros por segundo: la mediana, p95, p99
// y el peor), como pide la regla del 9-10-2026. No pide canciones ni toca los creditos; al acabar deja la pantalla como estaba y escribe
// el informe en rocola_diagnostico.txt (carpeta de datos). Cualquier boton lo corta. Lo que se mide aqui no es el A4 si no se corre alli.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rocola
{
    // Los cuadros presentados (la vista apunta cada uno).
    internal static class Fluidez
    {
        private static readonly Stopwatch reloj = Stopwatch.StartNew();
        private static double ultimo = -1;
        private static List<double> intervalos;
        internal static void Cuadro()
        {
            double t = reloj.Elapsed.TotalMilliseconds;
            List<double> l = intervalos;
            if (l != null && ultimo >= 0) l.Add(t - ultimo);
            ultimo = t;
        }
        internal static void Empieza() { intervalos = new List<double>(); ultimo = -1; }
        internal static List<double> Acaba() { List<double> l = intervalos; intervalos = null; return l ?? new List<double>(); }
    }

    internal sealed partial class Controlador
    {
        internal const int DiagEscenaMs = 6000;
        private static readonly string[] DiagEscenas = { "pared quieta", "pared moviendose (un disco cada 0,3 s)", "pasando de pagina (cada 1 s)",
            "dentro de un disco (una cancion cada 0,3 s)", "buscador escribiendo", "estado extendido (si suena algo)", "atraccion", "ayuda de la estrella guiando" };
        internal int DiagEscena = -1;   // -1: no hay diagnostico
        private int diagDesde, diagUltimoPaso;
        private StringBuilder diagInforme;
        private int dLetra, dCursor; private Form1.Disco dDentro;
        internal string DiagTexto { get { return DiagEscena < 0 ? "" : "DIAGNÓSTICO " + (DiagEscena + 1) + " de " + DiagEscenas.Length + ": " + DiagEscenas[DiagEscena] + " (cualquier botón lo corta)"; } }

        internal void EmpiezaDiagnostico()
        {
            if (DiagEscena >= 0) return;
            AjusteSel = -1; Buscando = false; if (AyudaAbierta) CierraAyuda();
            dLetra = letra; dCursor = cursorDisco; dDentro = dentro;
            diagInforme = new StringBuilder();
            diagInforme.Append("Diagnostico completo de Rocola OS, ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append("\r\n");
            diagInforme.Append("Equipo: ").Append(Environment.ProcessorCount).Append(" nucleos; ").Append(Environment.OSVersion).Append("; memoria del programa ")
                .Append((Environment.WorkingSet / (1024 * 1024)).ToString(CultureInfo.InvariantCulture)).Append(" MB; repaso pendiente: ").Append(motor.PorRepasar).Append(" canciones\r\n");
            diagInforme.Append("Escena\tcuadros\tpor segundo\tmediana ms\tp95 ms\tp99 ms\tpeor ms\tGC0/1/2\r\n");
            EmpiezaEscena(0);
        }
        private int gc0, gc1, gc2;
        private void EmpiezaEscena(int i)
        {
            DiagEscena = i; diagDesde = diagUltimoPaso = Environment.TickCount;
            letra = dLetra; cursorDisco = dCursor; dentro = null; Buscando = false; Atraccion = false;
            if (i == 3) { List<Form1.Disco> l = DiscosDeLetra(); if (l.Count > 0) { dentro = l[Math.Min(cursorDisco, l.Count - 1)]; cursorCancion = 0; } }
            if (i == 4) AbreBuscador();
            if (i == 5) ultimoToque = Environment.TickCount - 20000;    // el reposo
            if (i == 6) { AtraccionDesfase = 0; ultimoToque = Environment.TickCount - 60000; }
            if (i == 7) { AbreAyuda(); AyudaAbreTick -= 1000; EmpiezaMision(MisionAprender); }
            if (i < 5) ultimoToque = Environment.TickCount;
            gc0 = GC.CollectionCount(0); gc1 = GC.CollectionCount(1); gc2 = GC.CollectionCount(2);
            Fluidez.Empieza();
        }
        // Cada vuelta del bucle: mueve lo que toca en la escena y pasa a la siguiente a su tiempo.
        private void RevisaDiagnostico()
        {
            if (motor.PideDiagnostico) { motor.PideDiagnostico = false; EmpiezaDiagnostico(); }
            if (DiagEscena < 0) return;
            int ahora = Environment.TickCount;
            if (DiagEscena == 6 && !Atraccion) { if (motor.Sonando == null && motor.EnCola == 0) { Atraccion = true; atraccionCambio = ahora - 8000; RevisaAtraccion(); } }
            if (DiagEscena == 5 || DiagEscena == 6) ultimoToque = ahora - (DiagEscena == 5 ? 20000 : 60000);
            int cada = DiagEscena == 2 ? 1000 : 300;
            if (unchecked(ahora - diagUltimoPaso) >= cada)
            {
                diagUltimoPaso = ahora;
                switch (DiagEscena)
                {
                    case 1: Maneja(Boton.Derecha); break;
                    case 2: Maneja(Boton.Arriba); break;
                    case 3: Maneja(Boton.Abajo); break;
                    case 4: Maneja(Boton.Derecha); if (ColTecla == 0) Escribe('A'); if (Escrito.Length > 6) Escribe('\u0002'); break;
                    case 7: Maneja(Boton.Derecha); break;
                }
            }
            if (unchecked(ahora - diagDesde) < DiagEscenaMs) return;
            CierraEscena();
            if (DiagEscena + 1 < DiagEscenas.Length) EmpiezaEscena(DiagEscena + 1); else AcabaDiagnostico("");
        }
        private void CierraEscena()
        {
            List<double> l = Fluidez.Acaba();
            string nombre = DiagEscenas[DiagEscena];
            if (DiagEscena == 5 && motor.Sonando == null) { diagInforme.Append(nombre).Append("\t(no sonaba nada: sin medir)\r\n"); return; }
            l.Sort();
            double seg = DiagEscenaMs / 1000.0;
            Func<double, string> f = x => x.ToString("0.0", CultureInfo.InvariantCulture);
            Func<double, double> pc = p => l.Count == 0 ? 0 : l[Math.Min(l.Count - 1, (int)Math.Ceiling(p * l.Count) - 1)];
            diagInforme.Append(nombre).Append('\t').Append(l.Count + 1).Append('\t').Append(f((l.Count + 1) / seg)).Append('\t').Append(f(pc(0.5)))
                .Append('\t').Append(f(pc(0.95))).Append('\t').Append(f(pc(0.99))).Append('\t').Append(f(l.Count > 0 ? l[l.Count - 1] : 0))
                .Append('\t').Append(GC.CollectionCount(0) - gc0).Append('/').Append(GC.CollectionCount(1) - gc1).Append('/').Append(GC.CollectionCount(2) - gc2).Append("\r\n");
        }
        private void AcabaDiagnostico(string porQue)
        {
            if (DiagEscena < 0) return;
            if (porQue.Length > 0) { Fluidez.Acaba(); diagInforme.Append("Cortado: ").Append(porQue).Append("\r\n"); }
            if (AyudaAbierta) CierraAyuda();
            DiagEscena = -1; Buscando = false; Atraccion = false;
            letra = dLetra; cursorDisco = dCursor; dentro = dDentro; ultimoToque = Environment.TickCount;   // la pantalla, como estaba
            diagInforme.Append("(Intervalos entre cuadros presentados; la pantalla se redibuja a unos 30 por segundo como mucho. Medido en este equipo.)\r\n\r\n");
            try
            {
                string ruta = Path.Combine(motor.DatosEn, "rocola_diagnostico.txt");
                File.AppendAllText(ruta, diagInforme.ToString());
                Aviso(porQue.Length > 0 ? "Diagnóstico cortado: rocola_diagnostico.txt" : "Diagnóstico hecho: rocola_diagnostico.txt");
            }
            catch (Exception ex) { Aviso("No se pudo escribir el diagnóstico: " + ex.Message); }
            UltimoDiagnostico = diagInforme.ToString();
        }
        internal string UltimoDiagnostico = "";
        // (pruebas) el diagnostico entero sin el bucle de la rocola: las mismas revisiones de cada vuelta y la vista, hasta que acabe.
        internal void DiagnosticoDePrueba(IVista v)
        {
            EmpiezaDiagnostico();
            int tope = Environment.TickCount + 120000;
            while (DiagEscena >= 0 && unchecked(Environment.TickCount - tope) < 0)
            {
                RevisaAtraccion(); RevisaAyuda(); RevisaDiagnostico();
                v.Dibuja(this);
                System.Threading.Thread.Sleep(15);
            }
        }
    }
}
