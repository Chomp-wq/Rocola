// CTRL+F3: cadencia visual independiente del dibujo general y del audio.
// Los dos ajustes existen en ambos proyectos; solo se aplican al renderizador GPU.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        private int animOndasFps = 60;
        private int animCubosFps = 60;

        internal static int NormalizaFluidezAnimacion(int fps)
        { return fps == 24 || fps == 50 || fps == 60 ? fps : 60; }

        internal static int LeeFluidezAnimacion(string text)
        {
            int fps;
            return int.TryParse(text, out fps) ? NormalizaFluidezAnimacion(fps) : 60;
        }

        // No crecer la tarjeta ya llena de Imagen. Las dos hojas comparten su espacio,
        // con botones en el antiguo encabezado, sin tapar los mandos de los efectos.
        // TRES HOJAS (13-10-2026): Efectos, Fluidez y Performance (los ahorros que casi no se notan; Rendimiento.cs la llena).
        private Panel MontaFluidezGrafica(Tarjeta effects, Tarjeta performance, Font normal, Font small, Font button,
                                         Color text, Color muted, out Action paintFluency)
        {
            Panel sheets = new Panel(); sheets.Bounds = effects.Bounds;
            sheets.BackColor = Color.FromArgb(11, 14, 32);
            effects.Location = Point.Empty; effects.Name = "Efectos"; effects.Titulo = "";
            foreach (Control previous in effects.Controls) previous.TabIndex += 3;
            foreach (Control previous in performance.Controls) previous.TabIndex += 3;
            Tarjeta fluency = new Tarjeta(); fluency.Size = effects.Size;
            fluency.Name = "Fluidez"; fluency.Titulo = ""; fluency.Acento = Color.FromArgb(150, 190, 255);
            performance.Location = Point.Empty; performance.Size = effects.Size;
            Tarjeta[] cards = { effects, fluency, performance };
            string[] tabNames = { "Efectos", "Fluidez", "Performance" };
            BotonPlano[][] tabs = new BotonPlano[cards.Length][];
            int selectedSheet = 0;
            Action paintSheets = delegate {
                for (int page = 0; page < cards.Length; page++) {
                    cards[page].Visible = page == selectedSheet;
                    for (int i = 0; i < tabs[page].Length; i++) {
                        bool active = i == selectedSheet;
                        tabs[page][i].Relleno = active ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                        tabs[page][i].Borde = active ? Color.FromArgb(150, 205, 255) : Color.FromArgb(60, 72, 118);
                        tabs[page][i].ForeColor = active ? text : muted;
                        tabs[page][i].Invalidate();
                    }
                }
            };
            for (int page = 0; page < cards.Length; page++) {
                tabs[page] = new BotonPlano[cards.Length];
                for (int i = 0; i < cards.Length; i++) {
                    int next = i;
                    BotonPlano tab = new BotonPlano(); tab.SetBounds(16 + i * 144, 6, 136, 28);
                    tab.Font = button; tab.Text = tabNames[i];
                    tab.TabIndex = i;
                    tab.AccessibleName = "Imagen: " + tab.Text;
                    tab.Click += delegate { selectedSheet = next; paintSheets(); };
                    tabs[page][i] = tab; cards[page].Controls.Add(tab);
                }
                sheets.Controls.Add(cards[page]);
            }

            int[] values = { 24, 50, 60 };
            BotonPlano[][] choices = new BotonPlano[2][];
            string[] names = { "Ondas de sonido", "Todos los cubos" };
            string[] descriptions = {
                "Frecuencia de la aurora y sus ondas reactivas.\nNo cambia la calidad del sonido ni su análisis.",
                "Cadencia del giro, bamboleo y transiciones de cubos.\nNo cambia su calidad, carga ni la respuesta al toque."
            };
            paintFluency = delegate {
                int[] current = { NormalizaFluidezAnimacion(animOndasFps), NormalizaFluidezAnimacion(animCubosFps) };
                for (int group = 0; group < choices.Length; group++)
                    for (int i = 0; i < choices[group].Length; i++) {
                        bool active = values[i] == current[group];
                        choices[group][i].Relleno = active ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                        choices[group][i].Borde = active ? fluency.Acento : Color.FromArgb(60, 72, 118);
                        choices[group][i].ForeColor = active ? text : muted;
                        choices[group][i].AccessibleDescription = active ? "Seleccionado" : "Seleccionar esta cadencia visual";
                        choices[group][i].Invalidate();
                    }
            };
            Action paintOptions = paintFluency;
            for (int group = 0; group < choices.Length; group++) {
                int y = 54 + group * 158;
                Label name = new Label(); name.SetBounds(16, y, 424, 24);
                name.Font = normal; name.ForeColor = text; name.BackColor = Color.Transparent; name.Text = names[group];
                Label description = new Label(); description.SetBounds(16, y + 30, 424, 40);
                description.Font = small; description.ForeColor = muted; description.BackColor = Color.Transparent;
                description.Text = descriptions[group]; fluency.Controls.Add(name); fluency.Controls.Add(description);
                choices[group] = new BotonPlano[values.Length];
                for (int i = 0; i < values.Length; i++) {
                    int setting = group, fps = values[i];
                    BotonPlano option = new BotonPlano(); option.SetBounds(16 + i * 144, y + 78, 136, 36);
                    option.Font = button; option.Text = fps + " FPS";
                    option.TabIndex = 3 + group * 3 + i;
                    option.AccessibleName = names[group] + ": " + option.Text;
                    option.Click += delegate {
                        if (setting == 0) animOndasFps = fps; else animCubosFps = fps;
                        paintOptions(); AplicaEfectosGpu(); f3CambioGraf = true;
                    };
                    choices[group][i] = option; fluency.Controls.Add(option);
                    SoloConTarjeta(option);   // solo se dibujan con la tarjeta grafica (A1)
                }
            }
            Label note = new Label(); note.SetBounds(16, 382, 424, 112);
            note.Font = small; note.ForeColor = muted; note.BackColor = Color.Transparent;
            note.Text = "60 FPS: máximo detalle temporal. 50 FPS: intermedio.\n"
                + "24 FPS: menos actualizaciones de estas animaciones.\n\n"
                + "Son objetivos visuales, no el límite general de FPS.\n"
                + "La cadencia real depende del equipo y de la pantalla.\n"
                + "Se guardan al momento. De fábrica: 60 / 60.";
            fluency.Controls.Add(note);
            Label sinTarjeta = new Label(); sinTarjeta.SetBounds(16, 500, 424, 34);
            sinTarjeta.Font = small; sinTarjeta.ForeColor = Interruptor.ColorNecesita; sinTarjeta.BackColor = Color.Transparent;
            sinTarjeta.Text = TextoSinTarjeta; sinTarjeta.Visible = false;
            fluency.Controls.Add(sinTarjeta); AvisoSinTarjeta(sinTarjeta, null);
            paintOptions(); paintSheets();
            return sheets;
        }
    }
}
