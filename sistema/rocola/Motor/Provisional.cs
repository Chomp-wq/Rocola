// LO PROVISIONAL DEL MOTOR (Rocola OS, primer hito de la etapa 2). Las etapas leen el volumen general del nivelado (LevelTap.Master) para
// no medirlo. El nivelado de verdad (Rocola/Form1.Sonido.cs, Realce.cs y AudioControles.cs) se trae en el siguiente hito; hasta entonces,
// esto: solo el volumen general.
namespace Rocola
{
    public partial class Form1
    {
        internal partial class LevelTap
        {
            internal float Master = 1f;
        }
    }
}
