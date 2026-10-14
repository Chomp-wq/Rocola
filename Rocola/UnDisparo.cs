// UN DISPARO POR PULSACION (14-10-2026). Peticion del usuario: «sobre el buscador y el asistente, haz que solo reciba un
// input a la vez aunque se mantenga presionado el boton, asi si quiere el cliente abrir y cerrar deberia hacer la
// combinacion de botones varias veces, lo cual ya no se puede hacer por accidente».
//
// Antes, ROJO + ARRIBA no miraba nada: con los dos botones apretados, la repeticion del teclado (unas treinta por
// segundo) abria y cerraba el buscador sin parar (abrir tenia el tope de 0,7 s; cerrar, ninguno). ROJO + ABAJO ya
// descartaba la repeticion por el bit de Windows, pero ese bit no llega con todos los teclados, mapeadores de botones
// ni en Linux.
//
// Ahora cada combinacion, en cuanto hace lo suyo, queda TOMADA hasta que se suelta uno de sus dos botones. Mientras
// sigan apretados no vuelve a hacer nada, venga lo que venga del teclado. Para abrir y cerrar hay que soltar y volver
// a apretar. Soltar se sabe por el aviso de tecla soltada (Form1.OnKeyUp), con dos cuidados:
//   - REBOTE: un soltar de menos de 80 ms no cuenta. Un boton de rocola rebota unos milisegundos al apretarlo, y
//     algunos mapeadores de botones mandan apretar-soltar seguidos mientras se mantiene; una persona que suelta y
//     vuelve a apretar a proposito tarda mas que eso.
//   - SOLTAR PERDIDO: si el aviso de soltar se lo quedo otra ventana, y despues de 1,5 s sin ninguna pulsacion ni
//     repeticion de sus botones llega otra, es una pulsacion nueva. Manteniendo apretado, Windows repite como mucho
//     al segundo de empezar y luego cada pocos milisegundos, asi que una pausa asi solo pasa si se solto.
// Va aparte del tope de aperturas (TopeAperturas, Form1.cs), que sigue igual.
namespace Rocola
{
    internal sealed class UnDisparo
    {
        internal const int ReboteMs = 80, PerdidoMs = 1500;
        private bool tomado;      // ya hizo lo suyo y sus botones siguen apretados
        private int sueltoTick;   // cuando se solto uno de sus botones (0 = no se ha soltado)
        private int ultimaTick;   // la ultima pulsacion o repeticion de uno de sus botones (0 = ninguna)

        // Cada pulsacion o repeticion de uno de sus botones, ANTES de mirar si es la combinacion.
        internal void Aprieta(int ahora)
        {
            if (tomado)
            {
                if (sueltoTick != 0)
                {
                    int d = unchecked(ahora - sueltoTick);
                    if (d >= ReboteMs || d < 0) tomado = false;   // se solto de verdad: la siguiente vale
                    sueltoTick = 0;                                // si fue rebote, sigue tomada
                }
                else if (ultimaTick != 0 && unchecked(ahora - ultimaTick) >= PerdidoMs) tomado = false;
            }
            ultimaTick = ahora == 0 ? 1 : ahora;
        }

        // Se solto uno de sus botones.
        internal void Suelta(int ahora)
        {
            if (tomado && sueltoTick == 0) sueltoTick = ahora == 0 ? 1 : ahora;
        }

        // Llego la combinacion. true = es la primera de esta pulsacion: se hace y queda tomada. false = sigue apretada
        // desde la vez anterior: no se hace nada.
        internal bool Pide()
        {
            if (tomado) return false;
            tomado = true; sueltoTick = 0;
            return true;
        }

        internal bool Tomado { get { return tomado; } }
    }
}
