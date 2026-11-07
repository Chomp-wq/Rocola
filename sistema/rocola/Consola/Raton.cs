// EL RATON (peticion del dueno del 7-11-2026, con Rocola OS ya instalado en el A4: «no tengo soporte para mouse»). En XP la rocola se
// manejaba tambien con el raton (la pared, las letras, el disco, el buscador y, sobre todo, la ventana de Ctrl+F3); aqui igual:
//   - lo que llega (Botones.Raton, de /dev/input) se mete en una cola y lo atiende el bucle, como los botones, antes de dibujar;
//   - la pantalla, al dibujar, apunta donde quedo cada cosa que se puede tocar (ZonaClic: solo datos, sin funciones, y solo si hay un
//     raton en uso: sin raton no cuesta nada) y el clic busca, en las del ultimo cuadro, la de encima;
//   - IZQUIERDO: en la pared, una letra la elige y una portada abre el disco (como XP); en un disco, la cancion se elige y un segundo
//     clic en la misma la pide (cuesta un credito: asi no se paga sin querer); en el buscador, teclas, atajos y tarjetas (como XP);
//     en Ctrl+F3, las pestanas, «Listo», los interruptores y botones (un clic), los demas valores (elegir y otro clic: el siguiente) y
//     las barras (donde se toca). En el modo atraccion o en el reposo, el clic solo despierta (como XP).
//   - DERECHO: REGRESAR. RUEDA: ARRIBA/ABAJO (en la pared, la pagina; en una lista, el renglon).
//   - La flechita se ve mientras se usa y se esconde a los 6 s quieta.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Rocola
{
    internal sealed partial class Controlador
    {
        internal const byte ClicLetra = 1, ClicDisco = 2, ClicCancion = 3, ClicAjuste = 4, ClicBarra = 5, ClicPestana = 6, ClicListo = 7,
            ClicTecla = 8, ClicAtajo = 9, ClicResultado = 10;
        internal struct ZonaClic { public float X, Y, W, H; public byte Tipo; public int A, B; }
        private List<ZonaClic> zonasHechas = new List<ZonaClic>(), zonasCuadro = new List<ZonaClic>();
        private readonly ConcurrentQueue<long> ratonCola = new ConcurrentQueue<long>();
        internal float RatonX = -1, RatonY = -1;
        private int ratonMovio;
        internal float PantallaW = 1024, PantallaH = 768;
        // Hay raton: desde que se movio o toco la primera vez (antes, ni se apuntan zonas)
        internal bool ConRaton { get { return RatonX >= 0; } }
        internal bool RatonVisible { get { return RatonX >= 0 && unchecked(Environment.TickCount - ratonMovio) < 6000; } }

        // La pantalla, al empezar cada cuadro: las zonas del cuadro anterior quedan como las buenas.
        internal void EmpiezaZonas(float w, float h)
        {
            PantallaW = w; PantallaH = h;
            List<ZonaClic> t = zonasHechas; zonasHechas = zonasCuadro; zonasCuadro = t; zonasCuadro.Clear();
        }
        internal void Zona(float x, float y, float w, float h, byte tipo, int a, int b = 0)
        {
            if (RatonX < 0 || w <= 0 || h <= 0) return;
            zonasCuadro.Add(new ZonaClic { X = x, Y = y, W = w, H = h, Tipo = tipo, A = a, B = b });
        }

        private void ConectaRaton(Botones b)
        {
            b.Raton += delegate (int tipo, int a, int c)
            {
                ratonCola.Enqueue(((long)tipo << 56) | ((long)(a & 0xFFFFFFF) << 28) | (long)(c & 0xFFFFFFF));
            };
        }
        private static int Signo28(long v) { int x = (int)(v & 0xFFFFFFF); return (x & 0x8000000) != 0 ? x - 0x10000000 : x; }
        internal void RatonDePrueba(int tipo, int a, int b) { ratonCola.Enqueue(((long)tipo << 56) | ((long)(a & 0xFFFFFFF) << 28) | (long)(b & 0xFFFFFFF)); }   // (pruebas)

        internal void AtiendeRatonDePrueba() { AtiendeRaton(); }   // (pruebas)
        // En cada vuelta del bucle, antes de dibujar.
        private void AtiendeRaton()
        {
            long e;
            while (ratonCola.TryDequeue(out e))
            {
                int tipo = (int)(e >> 56), a = Signo28(e >> 28), b = Signo28(e);
                if (RatonX < 0) { RatonX = PantallaW / 2; RatonY = PantallaH / 2; }
                if (tipo == 1)
                {   // la velocidad: a la escala de la pantalla, y algo mas rapido si se mueve deprisa
                    float f = Math.Max(1f, PantallaH / 768f) * (Math.Abs(a) + Math.Abs(b) > 10 ? 1.8f : 1.1f);
                    RatonX = Math.Max(0, Math.Min(PantallaW - 1, RatonX + a * f)); RatonY = Math.Max(0, Math.Min(PantallaH - 1, RatonY + b * f));
                    ratonMovio = Environment.TickCount;
                }
                else if (tipo == 4)
                {
                    if (a >= 0) RatonX = a * (PantallaW - 1) / 65535f;
                    if (b >= 0) RatonY = b * (PantallaH - 1) / 65535f;
                    ratonMovio = Environment.TickCount;
                }
                else if (tipo == 2) { ratonMovio = Environment.TickCount; ComoBoton(a > 0 ? Boton.Arriba : Boton.Abajo); }
                else if (tipo == 3)
                {
                    ratonMovio = Environment.TickCount;
                    if (a == 2) ComoBoton(Boton.Regresar);
                    else if (a == 1) ClicIzquierdo();
                }
            }
        }

        // Lo que hace un boton de verdad al llegar (Tecla): el diagnostico se corta, la atraccion y el reposo solo despiertan.
        private bool DespiertaConRaton()
        {
            motor.HuboBoton();
            if (DiagEscena >= 0) { AcabaDiagnostico("se toco el raton"); ultimoToque = Environment.TickCount; return true; }
            bool enReposo = Reposo;
            ultimoToque = Environment.TickCount;
            if (Atraccion || enReposo) { Atraccion = false; return true; }
            return AsistentePaso >= 0;   // el asistente de los botones espera botones de verdad
        }
        private void ComoBoton(Boton b)
        {
            if (DespiertaConRaton()) return;
            Maneja(b); TrasBoton();
        }

        private void ClicIzquierdo()
        {
            if (DespiertaConRaton()) return;
            if (video != null) return;
            for (int i = zonasHechas.Count - 1; i >= 0; i--)
            {
                ZonaClic z = zonasHechas[i];
                if (RatonX < z.X || RatonX >= z.X + z.W || RatonY < z.Y || RatonY >= z.Y + z.H) continue;
                bool ajuste = z.Tipo >= ClicAjuste && z.Tipo <= ClicListo, buscador = z.Tipo >= ClicTecla && z.Tipo <= ClicResultado;
                // solo lo de la pantalla que esta delante (con Ctrl+F3 abierto, la pared de detras no se toca)
                if (AjusteSel >= 0 ? !ajuste : Buscando ? !buscador : (ajuste || buscador)) continue;
                Clic(z);
                TrasBoton();
                return;
            }
        }

        private void Clic(ZonaClic z)
        {
            switch (z.Tipo)
            {
                case ClicLetra:
                    if (z.A >= 0 && z.A < letras.Count) { dentro = null; deDonde.Clear(); cursorMas = -1; letra = z.A; cursorDisco = 0; }
                    return;
                case ClicDisco:
                    {
                        List<Form1.Disco> l = DiscosDeLetra();
                        if (dentro != null || z.A < 0 || z.A >= l.Count) return;
                        cursorDisco = z.A; Maneja(Boton.Poner);   // como XP: el clic en la portada abre el disco
                        return;
                    }
                case ClicCancion:
                    if (dentro == null || z.A < 0 || z.A >= dentro.Rutas.Count) return;
                    if (cursorCancion == z.A && cursorMas < 0) Maneja(Boton.Poner);   // el segundo clic en la misma: pedirla
                    else { cursorCancion = z.A; cursorMas = -1; }
                    return;
                case ClicPestana:
                    if (z.A >= 0 && z.A < Form1.PaginasAjustes.Length) IrAPaginaAjustes(Form1.PaginasAjustes[z.A]);
                    return;
                case ClicListo: ManejaAjustes(Boton.Regresar); return;
                case ClicAjuste:
                    {
                        int n = AjustesTotal; if (z.A < 0 || z.A >= n) return;
                        bool ya = AjusteSel == z.A;
                        if (!ya) { AjusteSel = z.A; ultimoLoQueSuena = 0; RevisaLoQueSuena(); }
                        if (z.A == n - 1) { ManejaAjustes(Boton.Poner); return; }   // configurar los botones
                        Form1.Ajuste a = ListaAjustes[z.A]; string v = a.Texto ?? "";
                        bool unClic = v == "SÍ" || v == "NO" || a.Herramienta;   // interruptores y botones: al primer clic
                        if (unClic || ya) ManejaAjustes(Boton.Derecha);
                        return;
                    }
                case ClicBarra:
                    {
                        if (z.A < 0 || z.A >= ListaAjustes.Count) return;
                        if (AjusteSel != z.A) { AjusteSel = z.A; ultimoLoQueSuena = 0; RevisaLoQueSuena(); }
                        Form1.Ajuste a = ListaAjustes[z.A];
                        if (a.Motivo != null) { Aviso("No se puede: " + a.Motivo); return; }
                        int meta = (int)Math.Round(Math.Max(0, Math.Min(1, (RatonX - z.X) / z.W)) * 100);
                        // hacia donde se toco, paso a paso (cada ajuste sabe sus pasos); se para si ya no se mueve o se pasa
                        for (int paso = 0; paso < 400; paso++)
                        {
                            int? p0 = a.Posicion; if (!p0.HasValue) break;
                            int dif = meta - p0.Value; if (Math.Abs(dif) <= 1) break;
                            if (!a.Herramienta) motor.CortaCiego("Cambiaste un ajuste: comparacion cortada. ");
                            a.Mueve(dif > 0 ? 1 : -1);
                            int? p1 = a.Posicion; if (!p1.HasValue || p1.Value == p0.Value) break;
                            if (Math.Abs(meta - p1.Value) > Math.Abs(dif)) { a.Mueve(dif > 0 ? -1 : 1); break; }   // se paso: vuelve
                        }
                        ultimoLoQueSuena = 0; RevisaLoQueSuena();
                        return;
                    }
                case ClicTecla:
                    if (z.A < 0 || z.A >= FilasTeclado.Length || z.B < 0 || z.B >= FilasTeclado[z.A].Length) return;
                    EnResultados = false; FilaTecla = z.A; ColTecla = z.B; Escribe(FilasTeclado[z.A][z.B]);
                    return;
                case ClicAtajo:
                    if (z.A < 0 || z.A >= Atajos.Count) return;
                    EnResultados = false; FilaTecla = -1; ColTecla = z.A; UsaAtajo(Atajos[z.A]);
                    return;
                case ClicResultado:
                    if (z.A < 0 || z.A >= Resultados.Count) return;
                    EnResultados = true; SelResultado = z.A; ManejaBuscador(Boton.Poner);   // como XP: el clic en la tarjeta la activa
                    return;
            }
        }
    }
}
