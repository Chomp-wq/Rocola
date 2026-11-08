// LO QUE EN XP ERA DE WINDOWS, AQUI ES DE LINUX: los nombres que el codigo de XP usa, apuntando a lo nativo con que se traducen.
// Se ponen una vez aqui (global using) para que cada archivo traducido de Codigo/ se quede como el de XP, linea por linea.
// GDI+ (Rocola.Dibujo2D) ya NO es global (port nativo, paso D1 del dibujo, 8-11-2026): cada archivo que todavia lo usa lo pone arriba, en la
// misma linea que "using System.Drawing;", para que un archivo ya traducido a SkiaSharp directo no vea por descuido los tipos que imitan.
global using Rocola.Ventanas;   // WinForms (System.Windows.Forms) -> las ventanas de la rocola (Ventanas.*.cs), sin Windows debajo
// Un nombre que en .NET de Linux choca con otro (System.Reflection.MethodInvoker): el de la rocola manda (el Encoder de GDI+ lo pone
// Form1.Biblioteca.cs, el unico que lo usa)
global using MethodInvoker = Rocola.Ventanas.MethodInvoker;
