// CTRL+F3 -> Fondo: sus tres hojas (Cielo y aurora, Interfaz, Estrella). Solo GPU; el render por CPU queda intacto.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        // UN SOLO FONDO, EL LEGACY (decision del usuario del 12-10-2026). Espacio natural se retiro: arriba ya no hay
        // selector de estilo y las tres hojas suben a su sitio, dentro del marco de siempre (como las demas paginas).
        // Las tres hojas reutilizan los mandos y sus eventos, sin duplicarlos.
        private void MontaHojasFondo(Panel page, Tarjeta adornments, Tarjeta details, Control[][] groups,
                                     Font normal, Font small, Font button, Color text, Color muted)
        {
            page.Name = "Fondo"; page.AccessibleName = "Ajustes del fondo";
            Color acento = Color.FromArgb(150, 190, 255);
            Tarjeta[] cards = { adornments, details, new Tarjeta(), new Tarjeta(), new Tarjeta(), new Tarjeta(), new Tarjeta(), new Tarjeta(),
                new Tarjeta(), new Tarjeta() };
            string[] cardNames = { "FondoAjustesAdornos", "FondoAjustesDetalle", "FondoInterfazBarra",
                "FondoInterfazInterior", "FondoEstrellaAspecto", "FondoEstrellaReposo", "FondoVivoLuna", "FondoVivoHora",
                "FondoPulidoColores", "FondoPulidoEstrella" };
            string[] titles = { "CIELO Y AURORA", "DETALLE DE LAS NEBULOSAS", "BARRA Y COLA",
                "INTERIOR Y TRANSICIONES", "SUPERFICIE Y ESTELA", "MOVIMIENTO Y REPOSO",
                "LA LUNA Y LAS FUGACES", "PROFUNDIDAD, HORA Y REFLEJO",
                "COLORES Y DEGRADADOS", "LA ESTRELLA Y LA PORTADA" };
            for (int i = 0; i < cards.Length; i++) {
                Tarjeta card = cards[i]; card.Name = cardNames[i]; card.Titulo = titles[i];
                card.AccessibleName = titles[i]; card.AccessibleRole = AccessibleRole.Grouping;
                card.SetBounds(i % 2 == 0 ? 16 : 488, 48, 456, 410);
                card.Font = normal; card.Acento = acento; card.TabIndex = 3 + i % 2;
                int row = 0;
                foreach (Control control in groups[i]) {
                    card.Controls.Add(control);
                    if (control is Interruptor) control.SetBounds(16, 40 + row++ * 40, 424, 28);
                }
                if (i >= 2) page.Controls.Add(card);
            }
            // Las notas originales conservan contenido, pero no ocupan filas de interruptores.
            groups[3][groups[3].Length - 1].SetBounds(16, 270, 424, 80);
            groups[5][groups[5].Length - 1].SetBounds(16, 144, 424, 48);
            if (groups.Length > 7)
            {   // El cielo vivo (CieloVivoGpu.cs): su nota, debajo de sus interruptores.
                groups[6][groups[6].Length - 1].SetBounds(16, 166, 424, 52);
                groups[7][groups[7].Length - 1].SetBounds(16, 166, 424, 52);
            }
            if (groups.Length > 9)
            {   // El pulido (PulidoGpu.cs): su nota, debajo de sus interruptores.
                groups[8][groups[8].Length - 1].SetBounds(16, 206, 424, 52);
                groups[9][groups[9].Length - 1].SetBounds(16, 286, 424, 36);
            }
            FondoNota(cards[0], "La paleta musical fluye por la aurora.\nEl cielo alto conserva su aspecto nocturno.", 358, 42, small, muted);
            FondoNota(cards[1], "La fuerza controla el detalle de las nebulosas;\n"
                + "cada efecto se ajusta por separado.", 140, 48, small, muted);
            FondoNota(cards[1], "Fluidez de ondas y cubos: se ajusta en Imagen.\n"
                + "Colores de carátula y género: se ajustan en Colores.", 196, 64, small, muted);
            FondoNota(cards[5], "Al entrar en reposo la estrella gana protagonismo.\n"
                + "Al tocar los controles el movimiento se calma.\n\n"
                + "El estado ampliado se activa en Imagen; ocultar\n"
                + "los controles no impide volver a usarlos.", 212, 126, small, muted);

            string[] sheets = { "Cielo y aurora", "Interfaz", "Estrella", "Cielo vivo", "Pulido" };
            BotonPlano[] tabs = new BotonPlano[sheets.Length];
            int selectedSheet = 0;
            Action paintSheets = delegate {
                for (int i = 0; i < cards.Length; i++) cards[i].Visible = i / 2 == selectedSheet;
                for (int i = 0; i < tabs.Length; i++) {
                    bool active = i == selectedSheet;
                    tabs[i].Relleno = active ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                    tabs[i].Borde = active ? Color.FromArgb(150, 205, 255) : Color.FromArgb(60, 72, 118);
                    tabs[i].ForeColor = active ? text : muted;
                    tabs[i].AccessibleDescription = active ? "Hoja seleccionada" : "Abrir esta hoja de Fondo";
                    tabs[i].Invalidate();
                }
            };
            for (int i = 0; i < tabs.Length; i++) {
                int next = i;
                BotonPlano tab = new BotonPlano(); tab.SetBounds(16 + i * 186, 4, 180, 32);   // cinco hojas (el cielo vivo y el pulido, las ultimas)
                tab.Font = button; tab.Text = sheets[i]; tab.TabIndex = i;
                tab.AccessibleName = "Fondo: " + tab.Text;
                tab.AccessibleRole = AccessibleRole.PushButton;
                tab.Click += delegate { selectedSheet = next; paintSheets(); };
                tabs[i] = tab; page.Controls.Add(tab); tab.BringToFront();
            }
            paintSheets();
        }

        private static void FondoNota(Tarjeta card, string text, int top, int height, Font font, Color color)
        {
            Label note = new Label(); note.SetBounds(16, top, 424, height);
            note.Text = text; note.Font = font; note.ForeColor = color; note.BackColor = Color.Transparent;
            card.Controls.Add(note);
        }
    }
}
