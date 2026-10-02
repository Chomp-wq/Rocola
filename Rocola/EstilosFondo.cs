// CTRL+F3 -> Fondo: presentación nueva opcional; el render por CPU queda intacto.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        private int estiloFondo = 0;
        private const int FondoPaginaY = 58, FondoPaginaH = 636, FondoPieY = 594;
        internal static int NormalizaEstiloFondo(int style) { return style == 1 ? 1 : 0; }
        internal static int LeeEstiloFondo(string text)
        { return string.Equals((text ?? "").Trim(), "natural", StringComparison.OrdinalIgnoreCase) ? 1 : 0; }
        internal static string TextoEstiloFondo(int style)
        { return NormalizaEstiloFondo(style) == 1 ? "natural" : "legacy"; }

        // El selector no es una hoja oculta: siempre ocupa su propia fila.
        // Las tres hojas reutilizan los mandos y sus eventos, sin duplicarlos.
        private void MontaEstiloFondo(Panel page, Tarjeta adornments, Tarjeta details, Control[][] groups,
                                     Font normal, Font small, Font button,
                                     Color text, Color muted, out Action paintStyle)
        {
            page.Name = "Fondo"; page.AccessibleName = "Ajustes del fondo"; page.Top = FondoPaginaY;
            Tarjeta styleCard = new Tarjeta();
            styleCard.SetBounds(16, 4, 928, 112); styleCard.Font = normal;
            styleCard.Name = "FondoEstilo"; styleCard.Titulo = "ESTILO DEL FONDO (GPU)";
            styleCard.AccessibleName = "Estilo del fondo"; styleCard.AccessibleRole = AccessibleRole.Grouping;
            styleCard.Acento = Color.FromArgb(150, 190, 255); styleCard.TabIndex = 0;
            string[] names = { "Legacy", "Espacio natural" };
            BotonPlano[] choices = new BotonPlano[names.Length];
            paintStyle = delegate {
                int current = NormalizaEstiloFondo(estiloFondo);
                for (int i = 0; i < choices.Length; i++) {
                    bool active = current == i;
                    choices[i].Relleno = active ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                    choices[i].Borde = active ? styleCard.Acento : Color.FromArgb(60, 72, 118);
                    choices[i].ForeColor = text; // ambos estilos son opciones activables, no mandos deshabilitados
                    choices[i].AccessibleDescription = active ? "Seleccionado" : "Seleccionar este estilo de fondo";
                    choices[i].Invalidate();
                }
            };
            Action repaint = paintStyle;
            for (int i = 0; i < choices.Length; i++) {
                int selected = i;
                BotonPlano choice = new BotonPlano(); choice.SetBounds(16 + i * 226, 42, 210, 42);
                choice.Font = button; choice.Text = names[i]; choice.TabIndex = i;
                choice.AccessibleName = "Estilo del fondo: " + names[i];
                choice.AccessibleRole = AccessibleRole.PushButton;
                choice.Click += delegate {
                    estiloFondo = selected; repaint(); AplicaEfectosGpu(); f3CambioGraf = true;
                };
                choices[i] = choice; styleCard.Controls.Add(choice);
            }
            Label explanation = new Label(); explanation.SetBounds(486, 40, 426, 62);
            explanation.Font = small; explanation.ForeColor = muted; explanation.BackColor = Color.Transparent;
            explanation.Text = "Legacy: el diseño original. Natural: luz y detalle delicados.\n"
                + "Se aplica al instante y se guarda al cerrar Ctrl+F3.\n"
                + "Conserva tus ajustes; no modifica el renderizado CPU.";
            styleCard.Controls.Add(explanation);

            Tarjeta[] cards = { adornments, details, new Tarjeta(), new Tarjeta(), new Tarjeta(), new Tarjeta() };
            string[] cardNames = { "FondoAjustesAdornos", "FondoAjustesDetalle", "FondoInterfazBarra",
                "FondoInterfazInterior", "FondoEstrellaAspecto", "FondoEstrellaReposo" };
            string[] titles = { "CIELO Y AURORA", "DETALLE NATURAL", "BARRA Y COLA",
                "INTERIOR Y TRANSICIONES", "SUPERFICIE Y ESTELA", "MOVIMIENTO Y REPOSO" };
            for (int i = 0; i < cards.Length; i++) {
                Tarjeta card = cards[i]; card.Name = cardNames[i]; card.Titulo = titles[i];
                card.AccessibleName = titles[i]; card.AccessibleRole = AccessibleRole.Grouping;
                card.SetBounds(i % 2 == 0 ? 16 : 488, 172, 456, 410);
                card.Font = normal; card.Acento = styleCard.Acento; card.TabIndex = 4 + i % 2;
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
            FondoNota(cards[0], "La paleta musical fluye por la aurora.\nEl cielo alto conserva su aspecto nocturno.", 358, 42, small, muted);
            FondoNota(cards[1], "El estilo cambia el acabado, no tus preferencias.\n"
                + "La fuerza controla el detalle de las nebulosas;\n"
                + "puedes ajustar cada efecto por separado.", 172, 76, small, muted);
            FondoNota(cards[1], "Fluidez de ondas y cubos: se ajusta en Imagen.\n"
                + "Colores de carátula y género: se ajustan en Colores.", 284, 64, small, muted);
            FondoNota(cards[5], "Al entrar en reposo la estrella gana protagonismo.\n"
                + "Al tocar los controles el movimiento se calma.\n\n"
                + "El estado ampliado se activa en Imagen; ocultar\n"
                + "los controles no impide volver a usarlos.", 212, 126, small, muted);

            string[] sheets = { "Cielo y aurora", "Interfaz", "Estrella" };
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
            page.Controls.Add(styleCard); styleCard.BringToFront();
            for (int i = 0; i < tabs.Length; i++) {
                int next = i;
                BotonPlano tab = new BotonPlano(); tab.SetBounds(16 + i * 310, 128, 308, 32);
                tab.Font = button; tab.Text = sheets[i]; tab.TabIndex = i + 1;
                tab.AccessibleName = "Fondo: " + tab.Text;
                tab.AccessibleRole = AccessibleRole.PushButton;
                tab.Click += delegate { selectedSheet = next; paintSheets(); };
                tabs[i] = tab; page.Controls.Add(tab); tab.BringToFront();
            }
            repaint(); paintSheets();
#if ROCOLA_GPU
            if (gpuEnabled && gpuPanel != null) gpuPanel.PreparaFondoNatural();
#endif
        }

        private static void FondoNota(Tarjeta card, string text, int top, int height, Font font, Color color)
        {
            Label note = new Label(); note.SetBounds(16, top, 424, height);
            note.Text = text; note.Font = font; note.ForeColor = color; note.BackColor = Color.Transparent;
            card.Controls.Add(note);
        }

        // HD tiene una hoja de Sonido mas alta. Fondo no debe heredarla:
        // su marco y Listo caben en 1024x768, sin reducir letra o mandos.
        private static void AjustaMarcoFondo(Form dialog, Control ready, Control version, bool background,
                                              Size normalSize, int normalReadyTop)
        {
            Size size = background ? new Size(normalSize.Width, FondoPaginaY + FondoPaginaH) : normalSize;
            if (dialog.ClientSize != size) dialog.ClientSize = size;
            ready.Top = background ? FondoPaginaY + FondoPieY : normalReadyTop;
            version.Visible = !background;
            if (background) {
                Rectangle area = Screen.FromControl(dialog).WorkingArea;
                int x = Math.Max(area.Left, Math.Min(dialog.Left, area.Right - dialog.Width));
                int y = Math.Max(area.Top, Math.Min(dialog.Top, area.Bottom - dialog.Height));
                if (dialog.Left != x || dialog.Top != y) dialog.Location = new Point(x, y);
            }
        }
    }
}
