// LA AYUDA DE LA ESTRELLA (en XP, AsistenteRocola.cs y AsistenteGuiaRocola.cs; peticiones del 10, 11 y 15 de octubre de 2026). ROJO +
// ABAJO la abre y la cierra (una vez por pulsacion). La estrella NO TIENE ROSTRO: su personalidad va en el cuerpo, los brazos, las manos y
// los pasos. Primero pregunta «¿Que quieres hacer?» (poner una cancion, buscarla por su nombre, aprender a moverme) y despues TE LLEVA DE
// LA MANO: mira lo que de verdad pasa en la rocola (donde estas, si hay credito, si abriste el buscador, si ya pusiste la cancion), senala
// lo que toca y dibuja los botones; tu los aprietas y hacen lo de siempre. Al lograrlo celebra y vuelve sola a su sitio. Con 45 s sin que
// nadie toque nada se despide y a los 2,2 s se cierra (si alguien toca algo mientras se despide, se queda).
// Las reglas puras (PasoParaCancion, OrdenPaso, NumeroPaso, PasoDespedida) son las de XP tal cual.
using System;

namespace Rocola
{
    internal enum Gesto { Quieta, Saludar, Senalar, Explicar, Confirmar, Buscar, Credito, Despedir }

    internal sealed partial class Controlador
    {
        internal const int MisionCancion = 1, MisionBuscar = 2, MisionAprender = 3;
        internal const int PasoDisco = 1, PasoMoneda = 2, PasoCancion = 3, PasoAbreBuscador = 4, PasoEscribe = 5,
            PasoResultado = 6, PasoMover = 7, PasoPagina = 8, PasoLetra = 9, PasoEntrar = 10, PasoVolver = 11,
            PasoCierraBuscador = 12;
        internal const int GuiaLogroMs = 2600, GuiaBienMs = 1300, AdiosTrasMs = 45000, AdiosMs = 2200, RegresoMs = 720;
        internal const int AyudaOpciones = 3;
        private static readonly int[] PasosAprender = { PasoMover, PasoPagina, PasoLetra, PasoEntrar, PasoVolver };
        internal static readonly string[] OpcionesAyuda = { "Poner una canción", "Buscar una canción por su nombre", "Aprender a moverme" };

        internal bool AyudaAbierta;
        internal int AyudaSeleccion, AyudaMision, AyudaPaso, AyudaAprende;
        internal Gesto AyudaGesto = Gesto.Quieta; internal int AyudaGestoTick, AyudaAbreTick, AyudaCierraTick;
        private int ayudaActividad, ayudaAdios, ayudaLogro, ayudaBien, ayudaMoneda, ayudaPedidos, ayudaPedidosBase;
        private int baseCursor, baseLetra;
        internal bool AyudaDespidiendo { get { return AyudaAbierta && ayudaAdios != 0; } }
        internal bool AyudaLograda { get { return ayudaLogro != 0; } }
        internal bool AyudaBienAhora { get { return ayudaBien != 0 && unchecked(Environment.TickCount - ayudaBien) < GuiaBienMs; } }
        internal bool AyudaRegresando { get { return !AyudaAbierta && AyudaCierraTick != 0 && unchecked(Environment.TickCount - AyudaCierraTick) < RegresoMs; } }

        // Por que no se puede abrir ahora (null si se puede), como MotivoSinAyuda de XP.
        private string MotivoSinAyuda()
        {
            if (video != null) return "suena un video";
            if (AjusteSel >= 0) return "estan abiertos los ajustes";
            if (AsistentePaso >= 0) return "se estan configurando los botones";
            return null;
        }

        private void Anima(Gesto g) { AyudaGesto = g; AyudaGestoTick = Environment.TickCount; }

        internal void AbreAyuda()
        {
            string m = MotivoSinAyuda();
            if (m != null) { Aviso("La ayuda no está disponible ahora: " + m); return; }
            if (AyudaAbierta) return;
            AyudaAbierta = true; AyudaSeleccion = 0; AyudaMision = 0; AyudaPaso = 0; AyudaAprende = 0;
            ayudaLogro = ayudaBien = ayudaAdios = 0;
            AyudaAbreTick = ayudaActividad = Environment.TickCount; AyudaCierraTick = 0;
            Atraccion = false;
            Anima(Gesto.Saludar);
        }
        internal void CierraAyuda()
        {
            if (!AyudaAbierta) return;
            if (ayudaAdios == 0) Anima(Gesto.Despedir);
            AyudaAbierta = false; AyudaMision = 0; ayudaLogro = 0; ayudaAdios = 0;
            AyudaCierraTick = Environment.TickCount; if (AyudaCierraTick == 0) AyudaCierraTick = 1;
            ultimoToque = Environment.TickCount;
        }
        // Un pedido quedo en la cola (lo apunta PideCancion: por ahi pasa todo pedido).
        private void NotaPedidoAyuda() { ayudaPedidos = unchecked(ayudaPedidos + 1); }
        private void MonedaAyuda() { if (AyudaAbierta) { ayudaActividad = Environment.TickCount; Anima(Gesto.Credito); ayudaMoneda = Environment.TickCount | 1; } }

        // Un boton con la ayuda abierta. Devuelve true si la ayuda se lo quedo (el menu); guiando, el boton hace lo suyo de siempre.
        private bool BotonAyuda(Boton b)
        {
            if (!AyudaAbierta) return false;
            ayudaActividad = Environment.TickCount;
            if (AyudaMision != 0) return false;
            if (unchecked(Environment.TickCount - AyudaAbreTick) < 450) return true;   // recien abierta: que no se elija sin querer
            if (b == Boton.Regresar) CierraAyuda();
            else if (b == Boton.Izquierda || b == Boton.Arriba) Selecciona(AyudaSeleccion - 1);
            else if (b == Boton.Derecha || b == Boton.Abajo) Selecciona(AyudaSeleccion + 1);
            else if (b == Boton.Poner) EmpiezaMision(AyudaSeleccion + 1);
            return b != Boton.Moneda && b != Boton.Saltar;
        }
        private void Selecciona(int s)
        {
            s = Math.Max(0, Math.Min(AyudaOpciones - 1, s));
            if (s == AyudaSeleccion) return;
            AyudaSeleccion = s; Anima(Gesto.Senalar);
        }
        private void EmpiezaMision(int mision)
        {
            AyudaMision = mision; AyudaPaso = 0; ayudaLogro = 0; ayudaBien = 0; AyudaAprende = 0; ayudaPedidosBase = ayudaPedidos;
            Bases(); Anima(Gesto.Explicar);
            EvaluaGuia();
        }
        private void Bases() { baseCursor = cursorDisco; baseLetra = letra; }

        // Cada vuelta del bucle (TickAsistenteGpu de XP).
        private void RevisaAyuda()
        {
            if (!AyudaAbierta) return;
            if (MotivoSinAyuda() != null) { CierraAyuda(); return; }
            int ahora = Environment.TickCount;
            switch (PasoDespedida(ahora, ayudaActividad, ayudaAdios))
            {
                case 1: ayudaAdios = ahora | 1; Anima(Gesto.Despedir); break;   // 45 s quieta: se despide
                case 2: ayudaAdios = 0; CierraAyuda(); return;                  // y se va
                case 3: ayudaAdios = 0; Anima(Gesto.Saludar); break;            // alguien volvio: se queda
            }
            if (AyudaMision != 0) EvaluaGuia();
            if (AyudaAbierta) ultimoToque = Environment.TickCount;   // con la ayuda abierta no hay reposo ni atraccion
        }

        internal static int PasoDespedida(int ahora, int actividad, int adios)
        {
            if (adios != 0)
            {
                if (unchecked(actividad - adios) > 0) return 3;
                return unchecked(ahora - adios) >= AdiosMs ? 2 : 0;
            }
            return unchecked(ahora - actividad) >= AdiosTrasMs ? 1 : 0;
        }
        internal static int PasoParaCancion(int mision, bool buscadorAbierto, bool enResultados, bool enDisco, int creditos)
        {
            if (buscadorAbierto) return !enResultados ? PasoEscribe : creditos <= 0 ? PasoMoneda : PasoResultado;
            if (enDisco) return creditos <= 0 ? PasoMoneda : PasoCancion;
            return mision == MisionBuscar ? PasoAbreBuscador : PasoDisco;
        }
        internal static int OrdenPaso(int mision, int paso)
        {
            if (mision == MisionCancion) return paso == PasoDisco ? 0 : paso == PasoMoneda ? 1 : paso == PasoCancion ? 2 : -1;
            if (mision == MisionBuscar)
                return paso == PasoAbreBuscador ? 0 : paso == PasoEscribe ? 1 : paso == PasoMoneda ? 2 : paso == PasoResultado ? 3 : -1;
            for (int i = 0; i < PasosAprender.Length; i++) if (PasosAprender[i] == paso) return i;
            return -1;
        }
        internal static int NumeroPaso(int mision, int paso, bool conCredito, out int total)
        {
            int orden = OrdenPaso(mision, paso);
            if (mision == MisionAprender) { total = PasosAprender.Length; return orden + 1; }
            int moneda = OrdenPaso(mision, PasoMoneda);
            total = (mision == MisionCancion ? 3 : 4) - (conCredito ? 1 : 0);
            if (orden < 0) return 0;
            if (conCredito && orden == moneda) return 0;
            return orden + 1 - (conCredito && orden > moneda ? 1 : 0);
        }

        private void EvaluaGuia()
        {
            if (ayudaLogro != 0)
            {
                if (unchecked(Environment.TickCount - ayudaLogro) >= GuiaLogroMs) CierraAyuda();   // se guarda sola
                return;
            }
            bool enDisco = dentro != null, enResultados = Buscando && EnResultados && Resultados.Count > 0;
            if (AyudaMision == MisionCancion || AyudaMision == MisionBuscar)
            {
                if (ayudaPedidos != ayudaPedidosBase) { Logro(); return; }
                PonPaso(PasoParaCancion(AyudaMision, Buscando, enResultados, enDisco, motor.Creditos));
                return;
            }
            int meta = PasosAprender[Math.Max(0, Math.Min(PasosAprender.Length - 1, AyudaAprende))];
            bool hecho = false;
            if (!Buscando)
            {
                int pag = cursorDisco / DiscosPorPagina, pagBase = baseCursor / DiscosPorPagina;
                if (meta == PasoMover) hecho = !enDisco && (cursorDisco != baseCursor || letra != baseLetra);
                else if (meta == PasoPagina) hecho = !enDisco && (pag != pagBase || letra != baseLetra);
                else if (meta == PasoLetra) hecho = !enDisco && letra != baseLetra;
                else if (meta == PasoEntrar) hecho = enDisco;
                else if (meta == PasoVolver) hecho = !enDisco;
            }
            if (hecho)
            {
                if (AyudaAprende >= PasosAprender.Length - 1) { Logro(); return; }
                AyudaAprende++; Bases();
                meta = PasosAprender[AyudaAprende];
            }
            int muestra = meta;
            if (Buscando) muestra = PasoCierraBuscador;
            else if (enDisco && meta != PasoVolver) muestra = PasoVolver;
            PonPaso(muestra);
        }
        private void PonPaso(int paso)
        {
            if (paso == AyudaPaso) return;
            int antes = OrdenPaso(AyudaMision, AyudaPaso), ahora = OrdenPaso(AyudaMision, paso);
            bool avanza = AyudaPaso != 0 && antes >= 0 && ahora > antes;
            AyudaPaso = paso;
            if (avanza)
            {
                ayudaBien = Environment.TickCount | 1;
                if (ayudaMoneda == 0 || unchecked(Environment.TickCount - ayudaMoneda) > 1200) Anima(Gesto.Confirmar);
            }
            else { ayudaBien = 0; Anima(paso == PasoMoneda ? Gesto.Buscar : Gesto.Senalar); }
            ayudaActividad = Environment.TickCount;
        }
        private void Logro() { ayudaLogro = Environment.TickCount | 1; ayudaBien = 0; Anima(Gesto.Credito); }

        // Lo que dice el globo para el paso de ahora (los textos de XP). teclas: B rojo, V verde, M moneda, U/D/L/R flechas, + y / (o).
        internal static void TextoPaso(int paso, out string titulo, out string texto, out string teclas)
        {
            titulo = texto = teclas = "";
            switch (paso)
            {
                case PasoDisco: titulo = "Elige un disco"; texto = "Muévete con las flechas. Cuando veas uno que te guste, pulsa VERDE."; teclas = "L/R U/D V"; break;
                case PasoMoneda: titulo = "Mete una moneda"; texto = "Cada canción cuesta 1 crédito. Inserta una moneda y seguimos."; teclas = "M"; break;
                case PasoCancion: titulo = "Elige tu canción"; texto = "Sube o baja con las flechas y pulsa VERDE para ponerla."; teclas = "U/D V"; break;
                case PasoAbreBuscador: titulo = "Abre el buscador"; texto = "Mantén el botón ROJO y pulsa la flecha de ARRIBA."; teclas = "B+U"; break;
                case PasoEscribe: titulo = "Escribe el nombre"; texto = "Elige cada letra con las flechas y VERDE (o escribe con el teclado). Luego ve con las flechas hasta tu canción."; teclas = "L/R U/D V"; break;
                case PasoResultado: titulo = "Elige tu canción"; texto = "Muévete hasta ella con las flechas y pulsa VERDE para ponerla."; teclas = "L/R U/D V"; break;
                case PasoMover: titulo = "Muévete entre los discos"; texto = "Pulsa la flecha IZQUIERDA o la DERECHA."; teclas = "L/R"; break;
                case PasoPagina: titulo = "Cambia de página"; texto = "Pulsa la flecha de ARRIBA o la de ABAJO para ver más discos."; teclas = "U/D"; break;
                case PasoLetra: titulo = "Salta a otra letra"; texto = "Mantén ROJO y pulsa IZQUIERDA o DERECHA: cambia la letra de arriba."; teclas = "B+L/R"; break;
                case PasoEntrar: titulo = "Entra en un disco"; texto = "Pulsa VERDE para ver sus canciones."; teclas = "V"; break;
                case PasoVolver: titulo = "Vuelve a los discos"; texto = "Pulsa el botón ROJO para volver a la pared."; teclas = "B"; break;
                case PasoCierraBuscador: titulo = "Cierra el buscador"; texto = "Mantén ROJO y pulsa la flecha de ARRIBA."; teclas = "B+U"; break;
            }
        }
    }
}
