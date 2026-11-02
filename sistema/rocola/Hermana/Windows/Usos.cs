// En .NET 10 hay un System.Reflection.MethodInvoker que en .NET 3.5 no existia; el codigo de XP usa el de WinForms.
global using MethodInvoker = System.Windows.Forms.MethodInvoker;
