// LO QUE EN XP ERA DE WINDOWS, AQUI ES DE LINUX: los nombres que el codigo de XP usa, apuntando a lo nativo con que se traducen.
// Se ponen una vez aqui (global using) para que cada archivo traducido de Codigo/ se quede como el de XP, linea por linea.
global using Rocola.Dibujo2D;   // GDI+ (System.Drawing.Drawing2D, Imaging, Text) -> SkiaSharp
global using Rocola.Ventanas;   // WinForms (System.Windows.Forms) -> las ventanas de la rocola (Ventanas.*.cs), sin Windows debajo
// Dos nombres que en .NET de Linux chocan con otros (System.Reflection.MethodInvoker, System.Text.Encoder): los de la rocola mandan
global using MethodInvoker = Rocola.Ventanas.MethodInvoker;
global using Encoder = Rocola.Dibujo2D.Encoder;
