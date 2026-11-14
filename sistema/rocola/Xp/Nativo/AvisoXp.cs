// EL AVISO (port nativo de las ventanas de XP, ventanas2, paso H): lo que en XP era MessageBox.Show (Program.cs: el aviso de la carpeta de
// datos; Ctrl+F2, Form1.Datos.cs: las carpetas que no se pueden agregar o quitar). Una hoja modal gris con su icono, el texto y su boton,
// medida como la de user32 (SoftModalMessageBox): la letra de los mensajes de XP (Tahoma de 8), margen de 12, 16 entre el icono, el texto y
// los botones, botones de 75x23 separados 6 y centrados, el texto partido a lo mas en 4/5 del ancho de la pantalla, en espanol como el XP
// del bar. Intro pulsa el boton por defecto (el que tiene el foco) y Escape el de cancelar (Aceptar, el unico); la «X» y Alt+F4 tambien
// contestan Aceptar, como user32 con un aviso de solo Aceptar. Solo lo que XP usa: el boton Aceptar (MessageBoxButtons.OK) y el icono de
// advertencia (MessageBoxIcon.Warning) o ninguno. Se porto de la capa (en la referencia, Plataforma/Ventanas.Form.cs: MessageBox y CajaAviso).
using System;
using System.Drawing;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    // los botones de un aviso (MessageBoxButtons) y su icono (MessageBoxIcon): los mismos numeros y nombres, solo los que XP usa
    public enum BotonesAviso { OK = 0 }
    public enum IconoAviso { None = 0, Warning = 48 }

    public sealed class AvisoXp : Hoja
    {
        private const int Margen = 12, Espacio = 16, EntreBotones = 6, BotonAncho = 75, BotonAlto = 23, Icono = 32;
        // MessageBox.Show(text, caption), MessageBox.Show(owner, text, caption) y MessageBox.Show(text, caption, buttons, icon): el aviso se
        // apunta tambien en la salida de errores; pedido desde otro hilo que no es el de la pantalla no se puede ensenar: se da por aceptado
        public static Resultado Muestra(string texto, string titulo) { return Muestra(null, texto, titulo, BotonesAviso.OK, IconoAviso.None); }
        public static Resultado Muestra(Hoja duena, string texto, string titulo) { return Muestra(duena, texto, titulo, BotonesAviso.OK, IconoAviso.None); }
        public static Resultado Muestra(string texto, string titulo, BotonesAviso botones, IconoAviso icono) { return Muestra(null, texto, titulo, botones, icono); }
        private static Resultado Muestra(object duena, string texto, string titulo, BotonesAviso botones, IconoAviso icono)
        {
            Console.Error.WriteLine("[Aviso] " + titulo + ": " + texto);
            if (PantallaRocola.Hilo != null && !PantallaRocola.EnSuHilo) return Resultado.OK;
            using (AvisoXp c = new AvisoXp(texto ?? "", titulo ?? "", icono)) return c.MuestraModal(duena);
        }

        private readonly IconoAviso icono;
        private readonly Rectangle sitioIcono;
        private AvisoXp(string texto, string titulo, IconoAviso icon) : base(MarcoXp.Dialogo)
        {
            icono = icon;
            Text = titulo;
            Font = Letra.EnPuntos(Letras.Resuelve("Tahoma"), 8f);
            bool conIcono = icon != IconoAviso.None;
            Size t = TextoGdi.Mide(texto, Font, new Size(PantallaRocola.Ancho / 5 * 4, int.MaxValue), BanderasGdi.Partir | BanderasGdi.SinPrefijo | BanderasGdi.Tabuladores);
            int fila = BotonAncho;   // (un boton: 1 x (75 + 6) - 6)
            int izq, xBotones, ancho = t.Width + (conIcono ? Icono + Espacio : 0);
            if (fila > ancho) { izq = Margen + (fila - ancho) / 2; xBotones = Margen; }
            else { izq = Margen; xBotones = Margen + ancho / 2 - fila / 2; }
            int yTexto = Math.Max(Margen, Margen + (conIcono ? Icono : 0) / 2 - t.Height / 2);
            sitioIcono = new Rectangle(izq, Margen, Icono, Icono);
            int yBotones = Math.Max(yTexto + t.Height, conIcono ? Margen + Icono : 0) + Espacio;
            ClientSize = new Size(Math.Max(xBotones + fila, izq + ancho) + Margen, yBotones + BotonAlto + Margen);
            Rotulo l = new Rotulo();
            l.SetBounds(izq + (conIcono ? Icono + Espacio : 0), yTexto, t.Width + 1, t.Height);
            l.UseMnemonic = false; l.Text = texto; l.BackColor = Color.Transparent;
            Controls.Add(l);
            BotonXp b = new BotonXp();
            b.SetBounds(xBotones, yBotones, BotonAncho, BotonAlto);
            b.Text = "Aceptar"; b.Resultado = Resultado.OK;   // (Button.DialogResult: al pulsarlo, la hoja se cierra con ese resultado)
            Controls.Add(b);
            BotonCancelar = b;   // (un aviso de solo Aceptar: Escape es Aceptar)
            activo = b;          // (ActiveControl: el boton por defecto, MessageBoxDefaultButton.Button1)
        }
        // la «X» (y Alt+F4: SC_CLOSE; el cierre de un dialogo pone Cancel) es como el boton de cancelar: en un aviso de solo Aceptar devuelve
        // OK, como user32. (No se mira la razon: en WinForms se queda en UserClosing despues de un cierre impedido.)
        internal override bool AlCerrarse(RazonCierre razon)
        {
            if (resultado == Resultado.Cancel) resultado = Resultado.OK;
            return false;
        }
        // EL ICONO (el de advertencia: el triangulo amarillo con la admiracion), como lo pintaba la capa
        internal override void PintaContenido(SKCanvas g)
        {
            if (icono != IconoAviso.Warning) return;
            Rectangle r = sitioIcono;
            PointF[] p = { new PointF(r.X + 16, r.Y + 2), new PointF(r.Right - 1, r.Bottom - 3), new PointF(r.X + 1, r.Bottom - 3) };
            SKPaint b = PinturaXp.Degradado(r, Color.FromArgb(255, 238, 120), Color.FromArgb(240, 190, 20), true);
            try { Pixel.RellenaPoligono(g, p, b, true); } finally { PinturaXp.Suelta(b); }
            SKPaint pn = PinturaXp.Pluma(Color.FromArgb(150, 110, 0), 1f);
            try { Pixel.TrazaPoligono(g, p, pn, true); } finally { PinturaXp.Suelta(pn); }
            SKPaint n = PinturaXp.Pincel(Color.Black);
            try { Pixel.Rellena(g, new RectangleF(r.X + 14.5f, r.Y + 10, 3, 11), n, true); Pixel.RellenaOvalo(g, new RectangleF(r.X + 14.5f, r.Y + 23, 3, 3), n, true); }
            finally { PinturaXp.Suelta(n); }
        }
    }
}
