// UN RELOJ DE LA PANTALLA (port nativo de las ventanas de XP): lo que en XP es el temporizador de WinForms (System.Windows.Forms.Timer,
// que por debajo es SetTimer y WM_TIMER en el hilo de la ventana). Vence en el hilo de la pantalla (PantallaRocola), despues de los
// encargos, la entrada y el pintado, como WM_TIMER en GetMessage. Solo lo que la rocola de XP usa de el, con sus reglas (fuente de
// WinForms con que se reviso la referencia: dotnet/winforms release/3.0, Timer.cs):
//   - Interval de fabrica 100 ms (Timer.cs: _interval = 100); menos de 1 lanza ArgumentOutOfRangeException (Timer.cs:146-151).
//   - Enabled = true lo pone en marcha (SetTimer, Timer.cs:95-131); Start y Stop son Enabled = true / false.
//   - Cambiar Interval con el reloj en marcha lo vuelve a empezar desde ese momento (RestartTimer: KillTimer y SetTimer, Timer.cs:153-163
//     y 255-259); poner el mismo valor no hace nada (Timer.cs:153: if (_interval != value)).
//   - Vence cada Interval desde el vencimiento anterior (el plazo no se corre si la rocola tarda en atenderlo); los vencimientos que se
//     juntan mientras la pantalla esta ocupada salen como UNO solo (Windows no acumula WM_TIMER), y el siguiente sigue en su sitio.
//     Menos de 10 ms es 10 (USER_TIMER_MINIMUM de Windows XP SP2). Con el reloj fino que pide Form1 (timeBeginPeriod(1)), vence a su hora.
//   - Tick lleva como remitente lo que le diga quien lo crea (en XP, el Timer: Timer.cs:172, OnTick => _onTimer?.Invoke(this, e)).
//   - Lo que lance Tick se apunta (Application.ThreadException) y la rocola sigue: el WM_TIMER pasa por la ventana del temporizador y
//     NativeWindow.Callback lo atrapa (NativeWindow.cs:484-504). Lo hace PantallaRocola.
using System;
using System.Runtime.CompilerServices;

namespace Rocola
{
    internal sealed class Reloj : IDisposable
    {
        internal const int Minimo = 10;   // USER_TIMER_MINIMUM
        private int intervalo = 100;
        private bool activo;
        internal double Proximo;          // cuando vence (ms del reloj de la pantalla); lo mueve PantallaRocola
        internal readonly string Nombre;  // (pruebas: TrazaVentanas) donde se creo: archivo:linea
        internal object Remitente;        // lo que llega como «sender» a Tick (null: el propio reloj)

        public Reloj([CallerFilePath] string archivo = null, [CallerLineNumber] int linea = 0) { Nombre = TrazaVentanas.Sitio(archivo, linea); }
        internal Reloj(string nombre, object remitente) { Nombre = nombre; Remitente = remitente; }

        public int Interval
        {
            get { return intervalo; }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException("value", value, "Value '" + value + "' is not a valid value for Interval. Interval must be greater than 0.");
                if (intervalo == value) return;
                intervalo = value;
                if (activo) PantallaRocola.PonReloj(this, true);   // RestartTimer
            }
        }
        public bool Enabled
        {
            get { return activo; }
            set
            {
                if (activo == value) return;
                activo = value;
                if (value) PantallaRocola.PonReloj(this, true); else PantallaRocola.QuitaReloj(this);
            }
        }
        public void Start() { Enabled = true; }
        public void Stop() { Enabled = false; }
        public event EventHandler Tick;
        // el plazo de cada vez: el intervalo, o el minimo de Windows
        internal double Plazo { get { return Math.Max(Minimo, intervalo); } }
        // (lo llama PantallaRocola en su hilo, con el reloj en marcha)
        internal void Vence() { EventHandler h = Tick; if (h != null) h(Remitente ?? this, EventArgs.Empty); }
        public void Dispose() { Enabled = false; }
    }
}
