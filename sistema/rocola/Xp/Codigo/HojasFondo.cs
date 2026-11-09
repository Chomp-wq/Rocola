// CTRL+F3 -> Fondo: sus tres hojas (Cielo y aurora, Interfaz, Estrella). Solo GPU; el render por CPU queda intacto.
using System;
using System.Drawing; using Rocola.Dibujo2D;   // EN LINUX: GDI+ de la transicion (Plataforma/Dibujo2D.cs) hasta que este archivo se traduzca a SkiaSharp directo

namespace Rocola
{
    public partial class Form1
    {
        // UN SOLO FONDO, EL LEGACY (decision del usuario del 12-10-2026). Espacio natural se retiro: arriba ya no hay
        // selector de estilo y las tres hojas suben a su sitio, dentro del marco de siempre (como las demas paginas).
        // Las tres hojas reutilizan los mandos y sus eventos, sin duplicarlos.
        private void MontaHojasFondo(Grupo page, Tarjeta adornments, Tarjeta details, IPieza[][] groups,   // EN LINUX: Panel -> Grupo (Nativo/Rotulo.cs); Control -> IPieza (Nativo/Pieza.cs: lo que la hoja le pide a cada pieza)
                                     Rocola.Nativo.Letra normal, Rocola.Nativo.Letra small, Rocola.Nativo.Letra button, Color text, Color muted)   // EN LINUX: Font -> Letra (Nativo/Letras.cs: la misma familia, tamano y estilo)
        {
            page.Name = "Fondo";   // EN LINUX: (XP) page.AccessibleName = "Ajustes del fondo"; -- el nombre, el papel o la descripcion para los lectores de pantalla de Windows (MSAA): en Rocola OS no hay, y nada de XP los lee
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
                // EN LINUX: (XP) card.AccessibleName = titles[i]; card.AccessibleRole = AccessibleRole.Grouping; -- el nombre, el papel o la descripcion para los lectores de pantalla de Windows (MSAA): en Rocola OS no hay, y nada de XP los lee
                card.SetBounds(i % 2 == 0 ? 16 : 488, 48, 456, 410);
                card.Font = normal; card.Acento = acento; card.TabIndex = 3 + i % 2;
                int row = 0;
                foreach (IPieza control in groups[i]) {   // EN LINUX: Control -> IPieza (Nativo/Pieza.cs: lo que la hoja le pide a cada pieza)
                    card.Controls.Add(control);
                    if (control is Interruptor) control.Caja.SetBounds(16, 40 + row++ * 40, 424, 28);   // EN LINUX: Control.SetBounds -> SetBounds de la caja de la pieza
                }
                if (i >= 2) page.Controls.Add(card);
            }
            // Las notas originales conservan contenido, pero no ocupan filas de interruptores.
            groups[3][groups[3].Length - 1].Caja.SetBounds(16, 270, 424, 80);   // EN LINUX: Control.SetBounds -> SetBounds de la caja de la pieza
            groups[5][groups[5].Length - 1].Caja.SetBounds(16, 144, 424, 48);   // EN LINUX: Control.SetBounds -> SetBounds de la caja
            if (groups.Length > 7)
            {   // El cielo vivo (CieloVivoGpu.cs): su nota, debajo de sus interruptores.
                groups[6][groups[6].Length - 1].Caja.SetBounds(16, 166, 424, 52);   // EN LINUX: Control.SetBounds -> SetBounds de la caja
                groups[7][groups[7].Length - 1].Caja.SetBounds(16, 166, 424, 52);   // EN LINUX: Control.SetBounds -> SetBounds de la caja
            }
            if (groups.Length > 9)
            {   // El pulido (PulidoGpu.cs): su nota, debajo de sus interruptores.
                groups[8][groups[8].Length - 1].Caja.SetBounds(16, 206, 424, 52);   // EN LINUX: Control.SetBounds -> SetBounds de la caja
                groups[9][groups[9].Length - 1].Caja.SetBounds(16, 286, 424, 36);   // EN LINUX: Control.SetBounds -> SetBounds de la caja
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
                    // EN LINUX: (XP) tabs[i].AccessibleDescription = active ? "Hoja seleccionada" : "Abrir esta hoja de Fondo"; -- el nombre, el papel o la descripcion para los lectores de pantalla de Windows (MSAA): en Rocola OS no hay, y nada de XP los lee
                    tabs[i].Invalidate();
                }
            };
            for (int i = 0; i < tabs.Length; i++) {
                int next = i;
                BotonPlano tab = new BotonPlano(); tab.SetBounds(16 + i * 186, 4, 180, 32);   // cinco hojas (el cielo vivo y el pulido, las ultimas)
                tab.Font = button; tab.Text = sheets[i]; tab.TabIndex = i;
                // EN LINUX: (XP) tab.AccessibleName = "Fondo: " + tab.Text; -- el nombre, el papel o la descripcion para los lectores de pantalla de Windows (MSAA): en Rocola OS no hay, y nada de XP los lee
                // EN LINUX: (XP) tab.AccessibleRole = AccessibleRole.PushButton; -- el nombre, el papel o la descripcion para los lectores de pantalla de Windows (MSAA): en Rocola OS no hay, y nada de XP los lee
                tab.Click += delegate { selectedSheet = next; paintSheets(); };
                tabs[i] = tab; page.Controls.Add(tab); tab.BringToFront();
            }
            paintSheets();
        }

        private static void FondoNota(Tarjeta card, string text, int top, int height, Rocola.Nativo.Letra font, Color color)   // EN LINUX: Font -> Letra (Nativo/Letras.cs: la misma familia, tamano y estilo)
        {
            Rotulo note = new Rotulo(); note.SetBounds(16, top, 424, height);   // EN LINUX: Label -> Rotulo (Nativo/Rotulo.cs)
            note.Text = text; note.Font = font; note.ForeColor = color; note.BackColor = Color.Transparent;
            card.Controls.Add(note);
        }
    }
}
