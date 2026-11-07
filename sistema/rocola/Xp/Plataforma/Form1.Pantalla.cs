// LO QUE LA PANTALLA DE ROCOLA OS (Consola/Pantalla/Kms.cs, enlazado sin cambios) le pregunta al programa. En XP la pantalla va a la
// resolucion de Windows (1024x768 en el A4): la resolucion nativa del monitor es una mejora de Rocola OS (V30) que vuelve en la fase 11.
namespace Rocola
{
    public partial class Form1
    {
        internal static bool ResolucionNativaGuardada() { return false; }
    }
}
