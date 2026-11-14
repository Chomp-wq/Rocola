// EL ANFITRION DE ROCOLA OS (port nativo): lo que en XP hace Windows con la pantalla. Abre la pantalla de verdad (KMS, con OpenGL ES 2 en
// el hilo de la rocola) o una fuera de pantalla (pruebas y capturas: ROCOLA_PANTALLA=fuera:1024x768, o si no hay tarjeta), y en cada
// cuadro le da al compositor nativo (Nativo/CompositorPantalla.cs) las capas, de abajo arriba: la ventana de la rocola (la lista nativa
// de PantallaRocola.Vistas.cs: lo pintado por procesador en tramos, y lo que pintan la tarjeta y el video) y las hojas nativas
// (Nativo/Hoja.cs: Ctrl+F3, Ctrl+F2, los avisos, F12), cada una en su superficie. (Las capturas, ROCOLA_CAPTURA..., las hace el compositor.)
// (N7, borrar-capas: estaba en Plataforma/ y unia la pantalla con las ventanas de la capa de la transicion -VentanasDeLaCapa, los huecos de
// las vistas y del video-; la capa ya no existe y aqui no queda nada que imite a Windows.)
using System;
using System.Collections.Generic;
using System.Globalization;
using Rocola.Pantalla;
using SkiaSharp;

namespace Rocola
{
    internal sealed class Anfitrion
    {
        internal static Anfitrion Actual;
        private readonly CompositorPantalla compositor;
        internal int Composiciones { get { return compositor.Composiciones; } }

        // Al empezar (antes del Main de XP, en el mismo hilo): la pantalla y el contexto de OpenGL.
        internal static void Arranca()
        {
            if (Actual != null) return;
            string p = Environment.GetEnvironmentVariable("ROCOLA_PANTALLA") ?? "";
            ISuperficie s = null;
            if (!p.StartsWith("fuera"))
            {
                try { s = new Kms(); }
                catch (Exception ex) { Console.Error.WriteLine("Pantalla: sin KMS (" + ex.Message + "): fuera de pantalla"); }
            }
            if (s == null)
            {
                int w = 1024, h = 768;
                int i = p.IndexOf(':');
                if (i > 0) { string[] t = p.Substring(i + 1).Split('x'); if (t.Length == 2) { int.TryParse(t[0], out w); int.TryParse(t[1], out h); } }
                s = new FueraDePantalla(w, h);
            }
            Actual = new Anfitrion(s);
            PantallaRocola.Componer = Actual.Compone;
            // el pintado (WM_PAINT) y la composicion pendiente los lleva la pantalla; una hoja que cambia pide componer. (Lo vuelve a poner la
            // ventana de la rocola al abrirse, con lo demas suyo: PantallaRocola.Ventana.cs, AbreNativa. Aqui, para lo que corre sin la rocola
            // -una hoja sola en las pruebas-: la de delante es la hoja activa.)
            PantallaRocola.PonElPintado();
            Hoja.HayQueComponer = PantallaRocola.HayQueComponer;
            PantallaRocola.LaDeDelante = delegate { return Hoja.Activa; };
            ReproductorVideo.Pantalla = new VideoEnPantalla();   // la capa del video (Nativo/ReproductorVideo.cs) en la lista de la ventana
            // las teclas: con la pantalla de verdad, los teclados y botones de /dev/input (TeclasLinux.cs); y las de prueba
            if (s is Kms || Environment.GetEnvironmentVariable("ROCOLA_TECLAS") == "1") TeclasLinux.Arranca("/dev/input");
            TeclasLinux.Programadas(Environment.GetEnvironmentVariable("ROCOLA_PULSA"));
            // (banco, port nativo ventanas2 C) ROCOLA_MIDE_HOJA=1: cuanto tarda cada pintura entera de una hoja nativa (Nativo/Hoja.cs), en la salida de errores
            if (Environment.GetEnvironmentVariable("ROCOLA_MIDE_HOJA") == "1")
                Hoja.MidePintura = delegate (Hoja h, double ms) { Console.Error.WriteLine("hoja-pinta " + h.Width + "x" + h.Height + " " + ms.ToString("0.00", CultureInfo.InvariantCulture) + " ms"); };
        }

        private Anfitrion(ISuperficie s)
        {
            compositor = new CompositorPantalla(s);
            PantallaRocola.Ancho = s.Ancho; PantallaRocola.Alto = s.Alto;
            int hz = 60;
            int a = s.Descripcion.IndexOf(" a "), b = s.Descripcion.IndexOf(" Hz");
            if (a > 0 && b > a) int.TryParse(s.Descripcion.Substring(a + 3, b - a - 3), out hz);
            PantallaRocola.RefrescoHz = hz >= 24 && hz <= 400 ? hz : 60;
            Console.WriteLine("Pantalla: " + s.Descripcion);
        }

        // ---- un cuadro: el compositor nativo con las capas de abajo arriba ----
        public void Compone() { compositor.Compone(Capas); }
        internal void GuardaPng(string ruta) { compositor.GuardaPng(ruta); }
        internal uint PuntoEn(int x, int y) { return compositor.PuntoEn(x, y); }   // (pruebas)
        // LAS CAPAS, de abajo arriba: las hojas que quedaron detras de la ventana de la rocola (Hoja.DetrasDeLaVentana: la ventana de prueba F12
        // tras activarse la rocola), la ventana de la rocola (PantallaRocola.CapasDeLaVentana: sus tramos por procesador partidos por lo de la
        // tarjeta y el video) y las demas hojas encima, en su orden; la tarjeta que pinta en el cliente de una hoja (F12), justo despues de la
        // suya. Cada hoja se pide UNA vez por cuadro (su superficie y la version que se pinto, Hoja.Superficie: con una pieza que lanza al
        // pintarse -la «X» roja- una segunda peticion la volveria a pintar en el mismo cuadro) y antes que la ventana, como siempre. La clave
        // de cada capa dura lo que la hoja se ve (escondida y vuelta a ensenar, su textura se sube de nuevo).
        private sealed class ClavesDeHoja { internal readonly object Mapa = new object(), Tarjeta = new object(); }
        private readonly Dictionary<Hoja, ClavesDeHoja> clavesDeHoja = new Dictionary<Hoja, ClavesDeHoja>();
        private List<CapaPantalla> Capas()
        {
            List<CapaPantalla> r = new List<CapaPantalla>();
            List<CapaPantalla> hojas = null, bajo = null;
            foreach (Hoja h in Hoja.Abiertas)
            {
                if (h.IsDisposed || !h.Visible) continue;
                ClavesDeHoja k;
                if (!clavesDeHoja.TryGetValue(h, out k)) clavesDeHoja[h] = k = new ClavesDeHoja();
                List<CapaPantalla> destino = h.DetrasDeLaVentana ? (bajo ?? (bajo = new List<CapaPantalla>())) : (hojas ?? (hojas = new List<CapaPantalla>()));
                CapaPantalla p = new CapaPantalla();
                int v; p.Clave = k.Mapa; p.Mapa = h.Superficie(out v); p.Version = v; p.Rect = h.Bounds; p.Opacidad = 1f; p.Tipo = "hoja";
                destino.Add(p);
                // LA TARJETA PINTANDO EN EL CLIENTE DE LA HOJA (en XP, un dispositivo de Direct3D 9 creado sobre la ventana: la de prueba F12): su
                // dibujo ocupa el cliente, encima de la superficie de la hoja y debajo de la hoja que venga despues
                if (h.TexturaGl != 0)
                {
                    CapaPantalla t = new CapaPantalla();
                    t.Clave = k.Tarjeta; t.Rect = h.ClienteEnPantalla; t.Opacidad = 1f;
                    t.DeTextura = true; t.Textura = h.TexturaGl; t.AnchoGl = h.AnchoGl; t.AltoGl = h.AltoGl; t.Nombre = h.GetType().Name; t.Tipo = "tarjeta";
                    destino.Add(t);
                }
            }
            if (clavesDeHoja.Count > 0) foreach (Hoja h in new List<Hoja>(clavesDeHoja.Keys)) if (h.IsDisposed || !h.Visible) clavesDeHoja.Remove(h);
            PantallaRocola.CapasDeLaVentana(r);
            if (hojas != null) r.AddRange(hojas);
            if (bajo != null) r.InsertRange(0, bajo);
            return r;
        }
    }
}
