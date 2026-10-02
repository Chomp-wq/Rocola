// CTRL+F3 -> Fondo: presentación nueva opcional; el render por CPU queda intacto.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        // Legacy conserva exactamente la presentación anterior y es el valor de fábrica.
        private int estiloFondo = 0;

        internal static int NormalizaEstiloFondo(int style) { return style == 1 ? 1 : 0; }
        internal static int LeeEstiloFondo(string text)
        { return string.Equals((text ?? "").Trim(), "natural", StringComparison.OrdinalIgnoreCase) ? 1 : 0; }
        internal static string TextoEstiloFondo(int style)
        { return NormalizaEstiloFondo(style) == 1 ? "natural" : "legacy"; }

        // Reutiliza el encabezado izquierdo de Fondo, sin encoger mandos ni añadir
        // desplazamiento. El botón de fábrica y la nota del pie se comparten.
        private void MontaEstiloFondo(Panel page, Tarjeta adornments, Tarjeta details,
                                     Font normal, Font small, Font button,
                                     Color text, Color muted, out Action paintStyle)
        {
            page.Name = "Fondo";
            adornments.Name = "FondoAjustesAdornos"; adornments.Titulo = "";
            details.Name = "FondoAjustesDetalle";
            adornments.TabIndex = 2; details.TabIndex = 3;
            Tarjeta styleCard = new Tarjeta(); styleCard.SetBounds(16, 4, 928, 586);
            styleCard.Name = "FondoEstilo"; styleCard.Titulo = ""; styleCard.Acento = Color.FromArgb(150, 190, 255);
            styleCard.TabIndex = 2;
            BotonPlano[] tabs = new BotonPlano[2];
            int selectedSheet = 0;
            Action paintSheets = delegate {
                adornments.Visible = details.Visible = selectedSheet == 0;
                styleCard.Visible = selectedSheet == 1;
                for (int i = 0; i < tabs.Length; i++) {
                    bool active = i == selectedSheet;
                    tabs[i].Relleno = active ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                    tabs[i].Borde = active ? Color.FromArgb(150, 205, 255) : Color.FromArgb(60, 72, 118);
                    tabs[i].ForeColor = active ? text : muted;
                    tabs[i].AccessibleDescription = active ? "Hoja seleccionada" : "Abrir esta hoja de Fondo";
                    tabs[i].Invalidate();
                }
            };
            Label heading = new Label(); heading.SetBounds(16, 50, 896, 26);
            heading.Font = normal; heading.ForeColor = text; heading.BackColor = Color.Transparent;
            heading.Text = "ESTILO DEL FONDO (GPU)";
            Label introduction = new Label(); introduction.SetBounds(16, 90, 896, 44);
            introduction.Font = small; introduction.ForeColor = muted; introduction.BackColor = Color.Transparent;
            introduction.Text = "Cambia la presentación del cielo, las estrellas y la aurora.\n"
                + "Los colores de la música siguen concentrados en el espectro; la parte alta permanece oscura.";
            styleCard.Controls.Add(heading); styleCard.Controls.Add(introduction);

            string[] names = { "Legacy", "Espacio natural" };
            BotonPlano[] choices = new BotonPlano[names.Length];
            paintStyle = delegate {
                int current = NormalizaEstiloFondo(estiloFondo);
                for (int i = 0; i < choices.Length; i++) {
                    bool active = current == i;
                    choices[i].Relleno = active ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                    choices[i].Borde = active ? styleCard.Acento : Color.FromArgb(60, 72, 118);
                    choices[i].ForeColor = active ? text : muted;
                    choices[i].AccessibleDescription = active ? "Seleccionado" : "Seleccionar este estilo de fondo";
                    choices[i].Invalidate();
                }
            };
            Action repaint = paintStyle;
            for (int i = 0; i < choices.Length; i++) {
                int selected = i;
                BotonPlano choice = new BotonPlano(); choice.SetBounds(16 + i * 464, 154, 432, 44);
                choice.Font = button; choice.Text = names[i]; choice.TabIndex = i;
                choice.AccessibleName = "Estilo del fondo: " + names[i];
                choice.Click += delegate {
                    estiloFondo = selected; repaint(); AplicaEfectosGpu(); f3CambioGraf = true;
                };
                choices[i] = choice; styleCard.Controls.Add(choice);
                Label title = new Label(); title.SetBounds(16 + i * 464, 218, 432, 24);
                title.Font = normal; title.ForeColor = text; title.BackColor = Color.Transparent;
                title.Text = i == 0 ? "La presentación actual" : "Luz más natural y detalle delicado";
                Label description = new Label(); description.SetBounds(16 + i * 464, 252, 432, 92);
                description.Font = small; description.ForeColor = muted; description.BackColor = Color.Transparent;
                description.Text = i == 0
                    ? "Conserva el diseño que ya conoces.\n"
                        + "Es el estilo predeterminado y el que usan los\n"
                        + "archivos de configuración anteriores."
                    : "Cielo profundo, nebulosas orgánicas y brillos más\n"
                        + "delicados. Aurora y estrella con luz equilibrada.\n"
                        + "Solo cambia la presentación del renderizador GPU.";
                styleCard.Controls.Add(title); styleCard.Controls.Add(description);
            }
            Label preserved = new Label(); preserved.SetBounds(16, 364, 896, 26);
            preserved.Font = normal; preserved.ForeColor = text; preserved.BackColor = Color.Transparent;
            preserved.Text = "Tus preferencias se conservan al cambiar de estilo";
            Label preservation = new Label(); preservation.SetBounds(16, 404, 896, 72);
            preservation.Font = small; preservation.ForeColor = muted; preservation.BackColor = Color.Transparent;
            preservation.Text = "No modifica los interruptores individuales, la paleta musical ni la fluidez de ondas y cubos.\n"
                + "Puedes alternar entre los dos diseños y volver a Ajustes para ajustar cada efecto.\n"
                + "El renderizado por CPU no cambia de aspecto.";
            Label saving = new Label(); saving.SetBounds(16, 510, 896, 52);
            saving.Font = small; saving.ForeColor = muted; saving.BackColor = Color.Transparent;
            saving.Text = "Se aplica al instante y se guarda al cerrar Ctrl+F3.\n"
                + "\"Fondo como de fábrica\" restablece Legacy y las opciones originales del fondo.";
            styleCard.Controls.Add(preserved); styleCard.Controls.Add(preservation); styleCard.Controls.Add(saving);
            page.Controls.Add(styleCard);
            for (int i = 0; i < tabs.Length; i++) {
                int next = i;
                BotonPlano tab = new BotonPlano(); tab.SetBounds(32 + i * 218, 8, 206, 26);
                tab.Font = button; tab.Text = i == 0 ? "Ajustes" : "Estilo"; tab.TabIndex = i;
                tab.AccessibleName = "Fondo: " + tab.Text;
                tab.Click += delegate { selectedSheet = next; paintSheets(); };
                tabs[i] = tab; page.Controls.Add(tab);
            }
            repaint(); paintSheets();
        }
    }
}
