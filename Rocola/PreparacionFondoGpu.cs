#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Threading;

namespace Rocola
{
    // Un unico trabajador de baja prioridad; no toca controles ni Direct3D.
    // La peticion nueva reemplaza la pendiente y cancela el calculo anterior por filas.
    internal sealed class PreparacionFondoNatural : IDisposable
    {
        internal sealed class Pedido
        {
            internal readonly int Ancho, Alto;
            internal readonly bool Detalle;
            internal readonly bool ConVia;
            internal readonly float Fuerza;
            internal Pedido(int w, int h, bool detail, float force)
                : this(w, h, detail, force, true) { }
            internal Pedido(int w, int h, bool detail, float force, bool via)
            {
                if (w < 1 || h < 1) throw new ArgumentOutOfRangeException("w");
                Ancho = w; Alto = h; Detalle = detail; ConVia = via;
                Fuerza = float.IsNaN(force) || float.IsInfinity(force) ? Form1.NebNaturalDef : Math.Max(0f, Math.Min(1f, force));
            }
            internal bool Igual(Pedido other)
            { return other != null && Ancho == other.Ancho && Alto == other.Alto && Detalle == other.Detalle && Fuerza == other.Fuerza && ConVia == other.ConVia; }
        }
        internal sealed class Mapas : IDisposable
        {
            internal readonly Pedido Clave;
            internal Bitmap Nebulosa, Via;
            internal Mapas(Pedido key, Bitmap neb, Bitmap via) { Clave = key; Nebulosa = neb; Via = via; }
            public void Dispose()
            {
                if (Nebulosa != null) { Nebulosa.Dispose(); Nebulosa = null; }
                if (Via != null) { Via.Dispose(); Via = null; }
            }
        }
        private readonly object puerta = new object();
        private Pedido ultima, pendiente;
        private Mapas listos;
        private Thread trabajador;
        private volatile int generacion;
        private volatile bool cerrado;
        private bool fallo;
        private int falloMs;
        // Inyeccion exclusivamente para pruebas; en produccion no se asigna.
        internal Func<Pedido, Func<bool>, Mapas> PreparadorPrueba = null;

        internal void Solicita(Pedido key)
        {
            lock (puerta) {
                if (cerrado) return;
                if (key.Igual(ultima) && (!fallo || unchecked(Environment.TickCount - falloMs) < 1000)) return;
                ultima = pendiente = key; generacion++; fallo = false;
                if (listos != null) { listos.Dispose(); listos = null; }
                if (trabajador == null) {
                    try {
                        trabajador = new Thread(Trabaja); trabajador.IsBackground = true;
                        trabajador.Priority = ThreadPriority.Lowest; trabajador.Start();
                    }
                    catch { trabajador = null; pendiente = null; fallo = true; falloMs = Environment.TickCount; }
                }
            }
        }
        internal Mapas Recoge(Pedido key)
        {
            lock (puerta) {
                if (cerrado || listos == null || !key.Igual(listos.Clave)) return null;
                Mapas result = listos; listos = null; return result;
            }
        }
        private void Trabaja()
        {
            for (;;) {
                Pedido key; int version;
                lock (puerta) {
                    if (cerrado || pendiente == null) { trabajador = null; return; }
                    key = pendiente; pendiente = null; version = generacion;
                }
                Mapas result = null;
                Func<bool> cancelled = delegate { return cerrado || version != generacion; };
                try {
                    if (PreparadorPrueba != null) result = PreparadorPrueba(key, cancelled);
                    else {
                        bool slow = CargaTrabajo.Procesadores <= 2;
                        Bitmap neb = GpuPanel.BakeNebulosaPreparada(key.Ancho, key.Alto, key.Detalle, key.Fuerza, cancelled, slow);
                        if (neb != null) {
                            try {
                                Bitmap via = key.ConVia ? GpuPanel.BakeViaPreparada(key.Ancho, key.Alto, cancelled, slow) : null;
                                if (!key.ConVia || via != null) { result = new Mapas(key, neb, via); neb = null; }
                            } finally { if (neb != null) neb.Dispose(); }
                        }
                    }
                } catch { /* El reintento se limita a una vez por segundo. */ }
                lock (puerta) {
                    if (!cerrado && version == generacion) {
                        if (result != null) { listos = result; result = null; }
                        else { fallo = true; falloMs = Environment.TickCount; }
                    }
                }
                if (result != null) result.Dispose();
            }
        }
        public void Dispose()
        {
            lock (puerta) {
                cerrado = true; generacion++; pendiente = null; ultima = null;
                if (listos != null) { listos.Dispose(); listos = null; }
            }
            // Sin Join: cerrar la ventana nunca espera al calculo; el trabajador descarta su resultado.
        }
    }

    internal sealed partial class GpuPanel
    {
        private PreparacionFondoNatural preparacionNatural;
        private int naturalClaveW, naturalClaveH;
        private bool naturalClaveDetalle;
        private float naturalClaveFuerza;
        private bool naturalMapaRetry, naturalRuidoRetry;
        private int naturalMapaRetryMs, naturalRuidoRetryMs;
        private PreparacionFondoNatural.Pedido pedidoNatural;

        public void PreparaFondoNatural()
        {
            if (IsDisposed || Disposing || Width < 1 || Height < 1 || Nieblas == GpuStarfield.Bajo) return;
            int w = Math.Max(8, Width / 4), h = Math.Max(8, Height / 4);
            float force = NaturalNebDetail(Form1.nebNaturalFuerza);
            if (pedidoNatural == null || naturalClaveW != w || naturalClaveH != h ||
                naturalClaveDetalle != CieloProfundo || naturalClaveFuerza != force) {
                naturalClaveW = w; naturalClaveH = h; naturalClaveDetalle = CieloProfundo; naturalClaveFuerza = force;
                bool needVia = mapasViaEstilo[1] == null || mapaViaAncho[1] != w || mapaViaAlto[1] != h;
                pedidoNatural = new PreparacionFondoNatural.Pedido(w, h, CieloProfundo, force, needVia);
            }
            if (MapasNaturalesListos()) return;
            if (preparacionNatural == null) preparacionNatural = new PreparacionFondoNatural();
            preparacionNatural.Solicita(pedidoNatural);
            PreparacionFondoNatural.Mapas maps = preparacionNatural.Recoge(pedidoNatural);
            if (maps == null) return;
            using (maps) {
                if (mapasNebEstilo[1] != null) mapasNebEstilo[1].Dispose();
                mapasNebEstilo[1] = maps.Nebulosa;
                if (maps.Via != null) {
                    if (mapasViaEstilo[1] != null) mapasViaEstilo[1].Dispose();
                    mapasViaEstilo[1] = maps.Via;
                }
                maps.Nebulosa = maps.Via = null;
                mapaNebAncho[1] = mapaViaAncho[1] = w; mapaNebAlto[1] = mapaViaAlto[1] = h;
                mapaNebDetalle[1] = CieloProfundo; mapaNebFuerza[1] = force;
            }
        }
        private bool MapasNaturalesListos()
        {
            return mapasNebEstilo[1] != null && mapasViaEstilo[1] != null &&
                mapaNebAncho[1] == naturalClaveW && mapaNebAlto[1] == naturalClaveH &&
                mapaViaAncho[1] == naturalClaveW && mapaViaAlto[1] == naturalClaveH &&
                mapaNebDetalle[1] == naturalClaveDetalle && mapaNebFuerza[1] == naturalClaveFuerza;
        }
        private void CancelaPreparacionNatural()
        {
            if (preparacionNatural != null) { preparacionNatural.Dispose(); preparacionNatural = null; }
            pedidoNatural = null;
            naturalMapaRetry = naturalRuidoRetry = false;
        }
        private void EnsureNieblasPreparadas()
        {
            // El grano tiene su ajuste propio, incluso si las nebulosas estan apagadas.
            if (ruidoTex == IntPtr.Zero && (!naturalRuidoRetry || unchecked(Environment.TickCount - naturalRuidoRetryMs) >= 250)) {
                using (Bitmap noise = Form1.BakeRuidoGrande(RuidoTeja))
                    ruidoTex = Upload(ruidoTex, ref ruTW, ref ruTH, noise, out ruU, out ruV);
                naturalRuidoRetry = ruidoTex == IntPtr.Zero; naturalRuidoRetryMs = Environment.TickCount;
            }
            if (Nieblas == GpuStarfield.Bajo) {
                if (preparacionNatural != null) { preparacionNatural.Dispose(); preparacionNatural = null; pedidoNatural = null; }
                return;
            }
            int now = Environment.TickCount;
            bool forceChange = nebFuerzaDe != NaturalNebDetail(Form1.nebNaturalFuerza);
            if (!TocaRehornear(nebNaturalDe != CieloProfundo || !nebEstiloDe, forceChange, now, nebFuerzaMs)) {
                if (nebTex != IntPtr.Zero && hazTex != IntPtr.Zero && nieblaW == Width && nieblaH == Height) return;
            }
            PreparaFondoNatural();
            if (!MapasNaturalesListos()) return; // Conserva la textura anterior mientras se prepara.
            if (naturalMapaRetry && unchecked(now - naturalMapaRetryMs) < 250) return;
            if (hazTex == IntPtr.Zero || !hazEstiloDe || hazTW != naturalClaveW || hazTH != naturalClaveH) {
                if (!SubeMapaNatural(ref hazTex, ref hazTW, ref hazTH, mapasViaEstilo[1], ref hazU, ref hazV)) {
                    naturalMapaRetry = true; naturalMapaRetryMs = now; return;
                }
                hazTeselas = TeselasVisibles.De(mapasViaEstilo[1], TeselasVisibles.LadoTesela, true); hazEstiloDe = true;
            }
            if (nebTex == IntPtr.Zero || !nebEstiloDe || nebTW != naturalClaveW || nebTH != naturalClaveH ||
                nebNaturalDe != CieloProfundo || forceChange) {
                if (!SubeMapaNatural(ref nebTex, ref nebTW, ref nebTH, mapasNebEstilo[1], ref nebU, ref nebV)) {
                    naturalMapaRetry = true; naturalMapaRetryMs = now; return;
                }
                nebEstiloDe = true; nebNaturalDe = CieloProfundo;
                nebFuerzaDe = naturalClaveFuerza; nebFuerzaMs = now;
            }
            nieblaW = Width; nieblaH = Height;
            naturalMapaRetry = false;
        }
        private bool SubeMapaNatural(ref IntPtr texture, ref int w, ref int h, Bitmap map, ref float u, ref float v)
        {
            // Solo el hilo de pantalla toca Direct3D. No destruye la anterior si Create falla.
            float newU, newV;
            if (texture != IntPtr.Zero && w == map.Width && h == map.Height && stars.UpdateOverlay(texture, map, out newU, out newV))
                { u = newU; v = newV; return true; }
            IntPtr replacement = stars.CreateOverlay(map, out newU, out newV);
            if (replacement == IntPtr.Zero) return false;
            if (texture != IntPtr.Zero) D3D9.Release(texture);
            texture = replacement; w = map.Width; h = map.Height; u = newU; v = newV; return true;
        }
    }
}
#endif
