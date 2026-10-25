// EL VOLUMEN DE WINDOWS AL MAXIMO AL ARRANCAR (decision del dueno del 24-10-2026).
//
// El que hace el corte de la venta baja el volumen de Windows para concentrarse, y muchas veces se le olvida subirlo como estaba:
// la rocola amanecia sonando bajito toda la noche siguiente. Ahora, cada vez que la rocola arranca (una sesion nueva del programa),
// pone el volumen general de Windows al maximo y le quita el silencio. Mientras la rocola esta abierta no lo vuelve a tocar: si
// alguien lo baja a mano, se queda bajo hasta el siguiente arranque. (Por eso se retiro tambien el modo noche: bajar lo decide el.)
//
// QUE VOLUMEN. El general, el del altavoz de la barra de tareas, no el de la salida de la rocola:
//   - Windows XP: el control maestro del mezclador (la linea de los altavoces, con su "Silencio"), en cada tarjeta de sonido. Y si
//     la linea "Onda" (lo que sale de los programas) esta en silencio, se le quita; su nivel no se toca.
//   - Vista y posteriores: el volumen del aparato de salida predeterminado (AudioEndpointVolume) y, por si alguien lo bajo en el
//     mezclador por aplicacion, el de la propia rocola (su sesion de sonido).
// Nunca el volumen de la salida WaveOut: desde Vista es el de la rocola en el mezclador y se queda guardado (SalidaSonido.cs).
//
// COSTE Y SEGURIDAD. Una vez por arranque, en un hilo de fondo (no frena la pantalla ni la musica), todo con try/catch: si una API
// no esta o la tarjeta no tiene ese control, no pasa nada y se dice en la bitacora. Sin interruptor: es lo que el dueno pidio.
//
// LAS ENTRADAS QUE SE OYEN POR LA SALIDA (25-10-2026, la revision del ruido del amplificador). En XP, el mezclador de los altavoces
// tambien deja pasar las entradas analogicas de la tarjeta (microfono, linea, CD, auxiliar, el altavoz del PC): si estan abiertas, su
// ruido de fondo (el del microfono con su refuerzo, sobre todo) sale por el amplificador aunque no haya nada enchufado. De paso, al subir el
// volumen se MIRA cuales estan abiertas (sin silencio y con volumen) y, si hay alguna, se apunta «[Ruido en la salida] ...» en
// rocola_error.txt con como silenciarlas. No se tocan: AGENTS.md solo permite subir el volumen general (decision del 24-10).
using System;
using System.Collections.Generic;

namespace Rocola
{
    public partial class Form1
    {
        // Lo que hizo la ultima vez (para la bitacora, Ctrl+F3 y las pruebas). null = aun no se intento.
        internal static string volumenWindowsHecho;
        // Las entradas que se oyen por los altavoces sin ser de la rocola ("" = ninguna; null = no se miro: Vista o sin mezclador).
        internal static string entradasAbiertas;

        private void SubeVolumenWindowsAlArrancar()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string r;
                try { r = SubeVolumenWindows(); }
                catch (Exception ex) { r = "no se pudo (" + ex.GetType().Name + ")"; }
                volumenWindowsHecho = r;
                Diag("volumen de Windows al arrancar: " + r);
                if (r.StartsWith("no se pudo")) { try { LogError("Volumen de Windows al arrancar: " + r, null); } catch { } }
                string ruido = TextoRuidoEntradas(entradasAbiertas);
                if (ruido != null) { Diag(ruido); ApuntaCajaNegra(ruido); }
            });
        }

        // Pone el volumen general al maximo y sin silencio. Devuelve lo que hizo, en una linea.
        internal static string SubeVolumenWindows()
        {
            OperatingSystem so = Environment.OSVersion;
            if (so.Platform != PlatformID.Win32NT) return "no es Windows: nada que subir";
            return so.Version.Major >= 6 ? SubeVolumenVista() : SubeVolumenXp();
        }

        private static string SubeVolumenVista()
        {
            List<string> hecho = new List<string>();
            using (NAudio.CoreAudioApi.MMDeviceEnumerator en = new NAudio.CoreAudioApi.MMDeviceEnumerator())
            using (NAudio.CoreAudioApi.MMDevice d = en.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Console))
            {
                NAudio.CoreAudioApi.AudioEndpointVolume v = d.AudioEndpointVolume;
                float antes = v.MasterVolumeLevelScalar; bool mudo = v.Mute;
                if (mudo) v.Mute = false;
                if (antes < 0.999f) v.MasterVolumeLevelScalar = 1f;
                hecho.Add("general " + Pct(antes) + " -> 100 %" + (mudo ? ", sin silencio" : ""));
                try
                {
                    NAudio.CoreAudioApi.SimpleAudioVolume s = d.AudioSessionManager.SimpleAudioVolume;
                    float sa = s.Volume; bool sm = s.Mute;
                    if (sm) s.Mute = false;
                    if (sa < 0.999f) s.Volume = 1f;
                    hecho.Add("la rocola en el mezclador " + Pct(sa) + " -> 100 %" + (sm ? ", sin silencio" : ""));
                }
                catch (Exception ex) { hecho.Add("la rocola en el mezclador: no se pudo (" + ex.GetType().Name + ")"); }
            }
            return string.Join("; ", hecho.ToArray());
        }

        private static string SubeVolumenXp()
        {
            List<string> hecho = new List<string>(), abiertas = new List<string>();
            bool mirado = false;
            int tarjetas = NAudio.Mixer.Mixer.NumberOfDevices;
            for (int i = 0; i < tarjetas; i++)
            {
                try
                {
                    NAudio.Mixer.Mixer m = new NAudio.Mixer.Mixer(i);
                    foreach (NAudio.Mixer.MixerLine dest in m.Destinations)
                    {
                        if (dest.ComponentType != NAudio.Mixer.MixerLineComponentType.DestinationSpeakers) continue;
                        string h = SubeLinea(dest, true);
                        foreach (NAudio.Mixer.MixerLine src in dest.Sources)
                        {
                            if (src.ComponentType == NAudio.Mixer.MixerLineComponentType.SourceWaveOut) { string w = SubeLinea(src, false); if (w.Length > 0) h += "; onda: " + w; }
                            else if (EsEntradaAnalogica(src.ComponentType)) { try { string a = EntradaAbierta(src); if (a != null) abiertas.Add((tarjetas > 1 ? "tarjeta " + i + ": " : "") + a); } catch { } }
                        }
                        mirado = true;
                        hecho.Add("tarjeta " + i + ": " + (h.Length > 0 ? h : "sin control de volumen"));
                    }
                }
                catch (Exception ex) { hecho.Add("tarjeta " + i + ": no se pudo (" + ex.GetType().Name + ")"); }
            }
            if (mirado) entradasAbiertas = string.Join(", ", abiertas.ToArray());
            return hecho.Count > 0 ? string.Join("; ", hecho.ToArray()) : "no se pudo (no hay mezclador de altavoces)";
        }

        // Las entradas analogicas de la tarjeta (las que meten ruido de fondo si se oyen por la salida). La onda y el sintetizador no.
        internal static bool EsEntradaAnalogica(NAudio.Mixer.MixerLineComponentType t)
        {
            switch (t)
            {
                case NAudio.Mixer.MixerLineComponentType.SourceMicrophone:
                case NAudio.Mixer.MixerLineComponentType.SourceLine:
                case NAudio.Mixer.MixerLineComponentType.SourceCompactDisc:
                case NAudio.Mixer.MixerLineComponentType.SourceAuxiliary:
                case NAudio.Mixer.MixerLineComponentType.SourceAnalog:
                case NAudio.Mixer.MixerLineComponentType.SourceTelephone:
                case NAudio.Mixer.MixerLineComponentType.SourcePcSpeaker:
                    return true;
            }
            return false;
        }
        // "Microfono (75 %)" si la linea se oye (sin silencio y con volumen), o null. Solo LEE: no cambia nada.
        private static string EntradaAbierta(NAudio.Mixer.MixerLine linea)
        {
            bool muda = false; double pct = -1;
            foreach (NAudio.Mixer.MixerControl c in linea.Controls)
            {
                if (c.ControlType == NAudio.Mixer.MixerControlType.Mute) { NAudio.Mixer.BooleanMixerControl b = c as NAudio.Mixer.BooleanMixerControl; if (b != null) muda = b.Value; }
                else if (c.ControlType == NAudio.Mixer.MixerControlType.Volume) { NAudio.Mixer.UnsignedMixerControl u = c as NAudio.Mixer.UnsignedMixerControl; if (u != null) pct = u.Percent; }
            }
            if (muda || pct == 0) return null;
            return linea.Name + (pct >= 0 ? " (" + pct.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " %)" : "");
        }
        // La linea para rocola_error.txt (null si no hay ninguna abierta, o no se miro).
        internal static string TextoRuidoEntradas(string abiertas)
        {
            if (string.IsNullOrEmpty(abiertas)) return null;
            return "[Ruido en la salida] Se oyen por los altavoces entradas que la rocola no usa: " + abiertas + ". Su ruido de fondo (el del "
                 + "microfono, sobre todo) sale por el amplificador aunque no haya nada enchufado. Para quitarlo: control de volumen de Windows, "
                 + "Reproduccion, marcar Silencio en esas lineas. La rocola no las toca.";
        }

        // Quita el silencio de la linea y, si 'nivel', la sube al maximo. Devuelve lo que hizo ("" si no habia nada).
        private static string SubeLinea(NAudio.Mixer.MixerLine linea, bool nivel)
        {
            List<string> h = new List<string>();
            foreach (NAudio.Mixer.MixerControl c in linea.Controls)
            {
                if (nivel && c.ControlType == NAudio.Mixer.MixerControlType.Volume)
                {
                    NAudio.Mixer.UnsignedMixerControl u = c as NAudio.Mixer.UnsignedMixerControl;
                    if (u == null) continue;
                    double antes = u.Percent;
                    if (u.Value != u.MaxValue) u.Value = u.MaxValue;
                    h.Add(antes.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " % -> 100 %");
                }
                else if (c.ControlType == NAudio.Mixer.MixerControlType.Mute)
                {
                    NAudio.Mixer.BooleanMixerControl b = c as NAudio.Mixer.BooleanMixerControl;
                    if (b != null && b.Value) { b.Value = false; h.Add("sin silencio"); }
                }
            }
            return string.Join(", ", h.ToArray());
        }

        private static string Pct(float f) { return (f * 100f).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " %"; }
    }
}
