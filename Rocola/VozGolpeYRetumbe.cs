// LAS TRES ETAPAS DE «VOZ, GOLPE Y RETUMBE» (27 de octubre de 2026): sus interruptores y como se llevan a la cancion que suena.
//
//  - Que el bombo no hunda la voz (BomboSinHundir.cs): el bajo con su propio margen, delante del limitador. "bombo_sin_hundir=".
//  - Retumbe dinamico (RetumbeDinamico.cs): un ecualizador que solo corta 260 Hz cuando el local retumba. "retumbe_dinamico=".
//  - Ataque mas claro (AtaqueClaro.cs): menos cola detras de cada golpe y, si hay sitio, algo mas de ataque. "ataque_claro=".
//
// Cada una lleva su interruptor persistente, apagado de fabrica; con el interruptor apagado la cadena sale BIT A BIT como antes. Con el
// MODO AUTOMATICO DEL LOCAL encendido (LocalAuto.cs, hecho para el billar) las tres van encendidas, y lo guardado en sus interruptores se
// conserva y vuelve al apagar el modo automatico. «Escuchar sin mejoras» (S4) quita el retumbe y el ataque y deja el bombo, que es de la
// familia del limitador.
//
// ORDEN EN LA CADENA: ... local (SalaLocal) -> RETUMBE -> presencia (compresor) -> ATAQUE -> eses (T9) -> bajo psicoacustico -> BOMBO ->
// limitador de pico real. El retumbe va justo detras del EQ automatico y la claridad (asi no se cuenta dos veces lo que ya se corto) y
// delante del compresor (que no debe apretar por un grave que va a bajar). El ataque va detras del compresor (que aplana los golpes
// que el ataque levanta) y delante de las eses (si levanta una "s", la baja). El bombo va pegado al limitador, tras el unico que anade
// picos graves (el bajo psicoacustico): es el que decide cuanto del techo le toca al bajo.
using System;

namespace Rocola
{
    public partial class Form1
    {
        private static bool bomboSinHundir;   // apagados de fabrica

        // Lleva los interruptores (o lo que decide el modo automatico) a las tres etapas de esa cadena. Lo llaman las bocinas y
        // «Escuchar sin mejoras» (AplicaSinMejoras), que ya se llama tras cada cambio de ajustes, de cancion y de la siguiente preparada.
        private void AjustaVozGolpe(CadenaCancion c)
        {
            if (c == null) return;
            AjusteLocal lo = LocalDe(c);
            if (c.Hundir != null) c.Hundir.Ajusta(lo.Bombo);
        }
    }
}
