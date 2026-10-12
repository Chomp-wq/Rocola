using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

// LOS AVISOS NO SE ESCONDEN DETRAS DEL VIDEO (barrido del 12-10-2026).
// Un video en ventana es una ventana de Windows ENCIMA del panel de la tarjeta: lo que la tarjeta dibuja detras no se ve.
// El aviso de siempre salia centrado (medio tapado), el "aviso que se entiende" a la derecha (tapado entero) y la
// invitacion a meter una moneda centrada (medio tapada). Ahora, con video en ventana, van a la izquierda de el.
// Pruebas de cuentas y de cableado: no abre la rocola, no hay video de verdad ni se miden FPS.
class AvisosVideo
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static Type form;
    static object St(string n, params object[] a)
    {
        foreach (MethodInfo m in form.GetMethods(B)) if (m.Name == n && m.IsStatic && m.GetParameters().Length == a.Length)
            try { return m.Invoke(null, a); } catch (TargetInvocationException e) { throw e.InnerException; }
        throw new Exception("Falta " + n);
    }
    // Lo que de verdad pinta la pastilla: de la primera a la ultima columna con algo.
    static void Extension(Bitmap b, out int x0, out int x1)
    {
        x0 = b.Width; x1 = -1;
        for (int x = 0; x < b.Width; x++) for (int y = 0; y < b.Height; y++)
            if (b.GetPixel(x, y).A > 8) { if (x < x0) x0 = x; if (x > x1) x1 = x; break; }
    }
    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args[0]);
        form = asm.GetType("Rocola.Form1", true);
        if (asm.GetType("Rocola.GpuPanel", false) == null) { Console.WriteLine("  OK avisosvideo: sin tarjeta grafica no hay avisos por GPU (CPU congelado)"); return 0; }
        // Hasta donde se ve, por la derecha.
        Check((int)St("BordeLibre", 1024, false, 0) == 1024, "Sin video, toda la pantalla");
        Check((int)St("BordeLibre", 1024, true, 512) == 504, "Con video en ventana (en 512), hasta 8 px antes de el");
        Check((int)St("BordeLibre", 800, true, 400) == 392, "En 800, hasta 392");
        Check((int)St("BordeLibre", 1024, true, 0) == 1024 && (int)St("BordeLibre", 1024, true, 2000) == 1024, "Un video fuera de sitio no encoge nada");
        // El aviso de siempre: la pastilla no pasa del ancho que se le da, aunque el texto sea largo.
        int toastW = (int)form.GetField("ToastW", B).GetRawConstantValue(), toastH = (int)form.GetField("ToastH", B).GetRawConstantValue();
        using (Font f = new Font(FontFamily.GenericSansSerif, 13f, FontStyle.Bold))
        {
            string largo = "Pusiste «Una canción con un título larguísimo que no cabe» · va la 3.ª en la cola · en ~9 min";
            foreach (int libre in new int[] { 504, 392, 1024 })
                using (Bitmap b = new Bitmap(toastW, toastH, PixelFormat.Format32bppArgb))
                {
                    St("PintaToastHasta", b, largo, Color.FromArgb(40, 165, 90), f, libre - 24);
                    int x0, x1; Extension(b, out x0, out x1);
                    int ancho = x1 - x0 + 1;
                    // (+4: el filo de 1,5 px y su suavizado salen un poco por fuera de la pastilla)
                    Check(x1 > x0 && ancho <= Math.Max(260, Math.Min(toastW, libre - 24)) + 4, "Pastilla de " + ancho + " px con " + libre + " libres");
                    // Centrada en lo que se ve (asi la coloca BuildToastState): no se mete debajo del video.
                    float x = (libre - toastW) * .5f;
                    Check(x + x0 >= 0 && x + x1 <= libre, "La pastilla queda entera en lo que se ve (" + (x + x0) + ".." + (x + x1) + " de " + libre + ")");
                }
            using (Bitmap a = new Bitmap(toastW, toastH, PixelFormat.Format32bppArgb))
            using (Bitmap c = new Bitmap(toastW, toastH, PixelFormat.Format32bppArgb))
            {
                St("PintaToast", a, "Inserta una moneda", Color.FromArgb(200, 130, 35), f);
                St("PintaToastHasta", c, "Inserta una moneda", Color.FromArgb(200, 130, 35), f, toastW);
                bool igual = true;
                for (int x = 0; x < toastW && igual; x++) for (int y = 0; y < toastH; y++) if (a.GetPixel(x, y) != c.GetPixel(x, y)) { igual = false; break; }
                Check(igual, "Sin video, el aviso se pinta exactamente como antes");
            }
            // El que se entiende: con video, cabe a la izquierda de el.
            foreach (int libre in new int[] { 504, 392 })
            {
                Size s = (Size)St("TamanoAviso", libre, "Pusiste «El Rey»", "va la 3.ª en la cola · en ~9 min", f, f);
                float x = Math.Max(8f, libre - 18f - s.Width);
                Check(x >= 8f && x + s.Width <= libre, "El aviso que se entiende cabe en " + libre + " px (" + s.Width + " de ancho)");
            }
            foreach (int libre in new int[] { 504, 392 })
            {
                Size s = (Size)St("TamanoInvita", libre);
                float x = Math.Max(0f, (libre - s.Width) * .5f);
                Check(x + s.Width <= libre + 1, "La invitacion cabe en " + libre + " px (" + s.Width + " de ancho)");
            }
        }
        // Cableado: los tres avisos miran lo que tapa el video.
        Check(Codigo.Llama(form.GetMethod("HorneaToast", B), "BordeLibreAvisos") && Codigo.Llama(form.GetMethod("HorneaToast", B), "PintaToastHasta"), "El aviso de siempre se acota al hornearlo");
        Check(Codigo.Llama(form.GetMethod("HorneaAviso", B), "BordeLibreAvisos"), "El aviso que se entiende va a la izquierda del video");
        Check(Codigo.Llama(form.GetMethod("InvitaAPoner", B), "BordeLibreAvisos"), "La invitacion se centra en lo que se ve");
        int lee, esc; Codigo.UsosDeCampo(form.GetMethod("BuildToastState", B), "toastBorde", out lee, out esc);
        Check(lee > 0, "El aviso se coloca con el borde con que se horneo");
        Console.WriteLine(mal == 0 ? "  OK avisosvideo: " + ok + " comprobaciones (cuentas y cableado; sin video ni HAL)" : "  FALLO avisosvideo: " + mal + " de " + (ok + mal));
        return mal;
    }
}
