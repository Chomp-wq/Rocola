namespace Rocola
{
    partial class Form1
    {
        /// <summary>
        /// Variable del diseñador necesaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpiar los recursos que se estén usando.
        /// </summary>
        /// <param name="disposing">true si los recursos administrados se deben desechar; false en caso contrario.</param>
        private void Dispose(bool disposing)   // EN LINUX: protected override void Dispose(bool disposing): no sobrescribe nada; su ventana la llama al desecharse (Dispose() de la rocola, Nativo/Form1.Ventana.cs, o al cerrarse)
        {
            if (disposing) DetieneVigiaFoco();
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            ventana.DesechaLoSuyo(disposing);   // EN LINUX: base.Dispose(disposing): lo de la ventana (se destruye, con sus vistas), despues de lo de la rocola
        }

        #region Código generado por el Diseñador de Windows Forms

        /// <summary>
        /// Método necesario para admitir el Diseñador. No se puede modificar
        /// el contenido de este método con el editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            // EN LINUX: (XP) this.AutoScaleMode = AutoScaleMode.Font; (nadie llama a InitializeComponent; la pantalla de Rocola OS va a 96 puntos por pulgada, como la del A4)
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Text = "Form1";
        }

        #endregion
    }
}

