#if ROCOLA_GPU
using System;
using System.Drawing; using Rocola.Dibujo2D;   // EN LINUX: GDI+ de la transicion (Plataforma/Dibujo2D.cs) hasta que este archivo se traduzca a SkiaSharp directo
namespace Rocola
{
    // Reloj por familia: no limita el render, la entrada, el progreso ni el audio.
    internal sealed class CadenciaVisual
    {
        private double origin, tick = -1;
        private int rate;
        internal double SampleMs;
        internal bool Step(double now, int fps, bool force)
        {
            fps = Form1.NormalizaFluidezAnimacion(fps);
            if (double.IsNaN(now) || double.IsInfinity(now)) return false;
            if (rate != fps || tick < 0 || now < SampleMs || now - SampleMs > 1000) {
                rate = fps; origin = now; tick = 0; SampleMs = now; return true;
            }
            double next = Math.Floor((now - origin) * fps / 1000.0 + 1e-8);
            if (next > tick) { tick = next; SampleMs = origin + next * 1000.0 / fps; return true; }
            return force;
        }
    }
    internal sealed partial class GpuPanel
    {
        public int OndasFps = 60, CubosFps = 60;
        private readonly CadenciaVisual waveCadence = new CadenciaVisual(), cubeCadence = new CadenciaVisual();
        internal double CubeSampleMs { get { return cubeCadence.SampleMs; } }
        private bool cubeStep;
        private GpuAurora sampledWave;
        private readonly float[] sampledBands = new float[8];
        private int waveWidth, waveHeight;
        private bool waveSmooth, waveDefined;
        private readonly GpuCubeInfo[] sampledCubes = new GpuCubeInfo[3];
        private GpuTrans sampledTransit;
        private bool transitWasPresent;
        private void BeginCubeFrame()
        { cubeStep = cubeCadence.Step(AhoraMs(), CubosFps, false); }
        private void SampleCubePose(ref GpuCubeInfo cube, int slot)
        {
            GpuCubeInfo old = sampledCubes[slot];
            object key = cube.Key ?? (object)cube.CoverA;
            object oldKey = old.Key ?? (object)old.CoverA;
            if (cubeStep || !old.Has || !cube.Has || !ReferenceEquals(key, oldKey)) {
                sampledCubes[slot] = cube; return;
            }
            // Posicion, portada/fundido y seleccion se consultan siempre: el primer toque
            // responde incluso entre muestras. Solo giro y bamboleo siguen este setting.
            cube.Angle = old.Angle; cube.Tilt = old.Tilt;
        }
        private void SampleCubeFlight(ref GpuTrans flight)
        {
            if (cubeStep || !transitWasPresent || !flight.Has || flight.Key != sampledTransit.Key || !ReferenceEquals(flight.Bmp, sampledTransit.Bmp)) sampledTransit = flight;
            else {
                bool has = flight.Has; Bitmap cover = flight.Bmp;
                flight = sampledTransit; flight.Has = has; flight.Bmp = cover;
            }
            transitWasPresent = flight.Has;
        }
        private bool SampleWave(ref GpuAurora wave)
        { return SampleWaveAt(ref wave, AhoraMs()); }
        private bool SampleWaveAt(ref GpuAurora wave, double now)
        {
            bool force = sampledWave.Has != wave.Has || waveWidth != Width || waveHeight != Height ||
                waveSmooth != OndasSuaves || waveDefined != AuroraBandas;
            bool update = waveCadence.Step(now, OndasFps, force);
            float baseY = wave.BaseY;
            if (update) {
                sampledWave = wave;
                for (int i = 0; i < sampledBands.Length; i++)
                    sampledBands[i] = wave.B != null && i < wave.B.Length ? SafeMoonLevel(wave.B[i]) : 0;
                sampledWave.B = sampledBands;
                waveWidth = Width; waveHeight = Height; waveSmooth = OndasSuaves; waveDefined = AuroraBandas;
            }
            wave = sampledWave; wave.BaseY = baseY; // union a la barra suave a cualquier FPS
            return update;
        }
    }
    public partial class Form1
    {
        private double gpuCubeMotionMs = -1, gpuCubeMotionPhase;
        private int gpuFlightTick;
        private double gpuFlightStart;
        private double CubeFlightElapsed(int tick)
        {
            double now = GpuPanel.AhoraMs();
            if (gpuFlightTick != tick) {
                gpuFlightTick = tick;
                gpuFlightStart = now - Math.Max(0, unchecked(Environment.TickCount - tick));
            }
            return Math.Max(0, now - gpuFlightStart);
        }
        private GpuCubeState CubosDelCuadro()
        {
            double now = gpuPanel != null ? gpuPanel.CubeSampleMs : GpuPanel.AhoraMs();
            double elapsed = gpuCubeMotionMs < 0 || now - gpuCubeMotionMs > 1000 ? 0 : Math.Max(0, Math.Min(250, now - gpuCubeMotionMs));
            if (gpuCubeMotionMs < 0) gpuCubeMotionPhase = phase;
            gpuCubeMotionMs = now;
            double steps = elapsed / 42.0; gpuCubeMotionPhase += .045 * steps;
            if (attractOn && attractView != null) attractView.Angle += .05 * steps;
            else if (searchOpen && searchOverlay != null) searchOverlay.CubeAngle += .075 * steps;
            else if (panelSongs != null && panelSongs.Visible && cubeBox != null) cubeBox.AvanzaGpu(.06 * steps, gpuCubeMotionPhase, now);
            else if (albumView != null && albumView.Visible) albumView.AvanceGiroGpu(steps);
            if (miniCube != null && miniCube.Visible) miniCube.AvanzaGpu(.06 * steps, gpuCubeMotionPhase, now);
            return BuildGpuCubeState();
        }
        private partial class AlbumView
        {
            private int gpuSpinSelected = -1;
            internal void AvanceGiroGpu(double steps)
            {
                if (gpuSpinSelected != Selected) { gpuSpinSelected = Selected; steps = 0; }
                if (Sliding || !(steps > 0) || double.IsInfinity(steps)) return;
                SelAngle += .06 * spinDir * steps;
                if (prevIdx < 0) return;
                if (Math.Abs(prevSpin) > .02f) {
                    double fade = Math.Pow(.85, steps);
                    prevAngle += prevSpin * (1 - fade) / (1 - .85);
                    prevSpin *= (float)fade;
                } else {
                    double target = Math.Round(prevAngle / (2 * Math.PI)) * (2 * Math.PI);
                    prevAngle += (target - prevAngle) * (1 - Math.Pow(.75, steps));
                    if (Math.Abs(target - prevAngle) < .03) prevIdx = -1;
                }
            }
        }
        private partial class CubeBox
        {
            private int gpuRelayTick;
            private double gpuRelayStart;
            internal void AvanzaGpu(double step, double smoothPhase, double sampleMs)
            {
                Angle += step; Phase = smoothPhase;
                if (relTick == 0) { gpuRelayTick = 0; return; }
                if (gpuRelayTick != relTick) {
                    gpuRelayTick = relTick;
                    gpuRelayStart = GpuPanel.AhoraMs() - Math.Max(0, unchecked(Environment.TickCount - relTick));
                }
                // Portada y pose avanzan juntas: entre ticks no cruzar el cambio de cara.
                double u = Math.Max(0, Math.Min(1, (sampleMs - gpuRelayStart) / RelMs));
                double e = u * u * (3 - 2 * u), target = Math.PI * e;
                Angle += target - relGirado; relGirado = target;
                if (relNueva != null && e >= .5) { Cover = relNueva; relNueva = null; }
                if (u >= 1) relTick = 0;
            }
        }
    }
}
#endif
