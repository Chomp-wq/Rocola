// LAS TECLAS DE LA ROCOLA (port nativo de las ventanas de XP, ventanas2, paso T): lo que en XP le llega al codigo de la rocola con cada
// tecla. En XP eran tipos de WinForms: Keys (el codigo de la tecla con los bits de Mayus, Ctrl y Alt), Message (el WM_KEYDOWN, del que
// XP solo mira el bit 30 de lParam: la repeticion de la tecla mantenida), KeyEventArgs y KeyPressEventArgs. Aqui son de la rocola, con
// solo lo que XP lee de ellos (el inventario: KeyEventArgs.KeyCode, Handled y SuppressKeyPress; KeyPressEventArgs.KeyChar y Handled;
// Message.LParam, solo su bit 30) y sus mismas reglas. Fuente con que se reviso la referencia: dotnet/winforms release/3.0 (portada de
// .NET Framework 4.8): Keys.cs, KeyEventArgs.cs, KeyPressEventArgs.cs; y la documentacion de Win32 de WM_KEYDOWN.
//
//   Tecla           los MISMOS numeros que Keys: el codigo de tecla de Windows (VK_*) en los 16 bits de abajo y Mayus 0x10000, Ctrl
//                   0x20000 y Alt 0x40000 (Keys.cs:23-28 y 977-987), porque rocola_teclas.txt guarda los botones como numeros
//                   (LoadKeys y SaveKeys, Form1.Ajustes.cs, sin cambiar de formato) y Botones.cs traduce evdev a ellos. Y los mismos
//                   NOMBRES que tenia Keys en la referencia, en su mismo orden (los de Keys.cs menos 22 de 18 codigos: las teclas de
//                   Asia y del IME, 0x15, 0x17-0x19 y 0x1C-0x1F, las del navegador menos BrowserBack, 0xA7-0xAC, y las de lanzar
//                   programas, 0xB4-0xB7; Botones.cs no da ninguna): XP escribe cada tecla con su nombre en la marca de la pantalla
//                   (ProcessCmdKey, Form1.cs: VigiaPantalla.Hace("una tecla (" + keyData + ")") -> «una tecla (F3, Control)», lo que
//                   la bitacora dice de un tiron); con otros nombres, u otro orden, saldria otro texto (un numero, otra combinacion de
//                   [Flags] u otro de los alias).
//   PulsoTecla      una tecla apretada o soltada: lo que XP leia del Message de ProcessCmdKey (Repetida) y de KeyEventArgs (KeyCode,
//                   Handled, SuppressKeyPress). La pantalla hace uno por tecla.
//   PulsoCaracter   el caracter de una tecla (WM_CHAR): lo que XP leia de KeyPressEventArgs.
using System;

namespace Rocola
{
    [Flags]
    public enum Tecla
    {
        KeyCode = 0xFFFF, Modifiers = unchecked((int)0xFFFF0000), None = 0,
        LButton = 1, RButton = 2, Cancel = 3, MButton = 4, XButton1 = 5, XButton2 = 6, Back = 8, Tab = 9, LineFeed = 10, Clear = 12,
        Return = 13, Enter = 13, ShiftKey = 16, ControlKey = 17, Menu = 18, Pause = 19, Capital = 20, CapsLock = 20, Escape = 27,
        Space = 32, Prior = 33, PageUp = 33, Next = 34, PageDown = 34, End = 35, Home = 36, Left = 37, Up = 38, Right = 39, Down = 40,
        Select = 41, Print = 42, Execute = 43, Snapshot = 44, PrintScreen = 44, Insert = 45, Delete = 46, Help = 47,
        D0 = 48, D1, D2, D3, D4, D5, D6, D7, D8, D9,
        A = 65, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        LWin = 91, RWin = 92, Apps = 93, Sleep = 95,
        NumPad0 = 96, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
        Multiply = 106, Add = 107, Separator = 108, Subtract = 109, Decimal = 110, Divide = 111,
        F1 = 112, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, F13, F14, F15, F16, F17, F18, F19, F20, F21, F22, F23, F24,
        NumLock = 144, Scroll = 145, LShiftKey = 160, RShiftKey = 161, LControlKey = 162, RControlKey = 163, LMenu = 164, RMenu = 165,
        BrowserBack = 166, VolumeMute = 173, VolumeDown = 174, VolumeUp = 175, MediaNextTrack = 176, MediaPreviousTrack = 177, MediaStop = 178, MediaPlayPause = 179,
        OemSemicolon = 186, Oem1 = 186, Oemplus = 187, Oemcomma = 188, OemMinus = 189, OemPeriod = 190, OemQuestion = 191, Oem2 = 191, Oemtilde = 192, Oem3 = 192,
        OemOpenBrackets = 219, Oem4 = 219, OemPipe = 220, Oem5 = 220, OemCloseBrackets = 221, Oem6 = 221, OemQuotes = 222, Oem7 = 222, Oem8 = 223, OemBackslash = 226, Oem102 = 226,
        ProcessKey = 229, Packet = 231, Attn = 246, Crsel = 247, Exsel = 248, EraseEof = 249, Play = 250, Zoom = 251, NoName = 252, Pa1 = 253, OemClear = 254,
        Shift = 0x10000, Control = 0x20000, Alt = 0x40000,
    }

    // UNA TECLA APRETADA O SOLTADA.
    //   - KeyData: la tecla con los bits de Mayus, Ctrl y Alt que estaban apretadas (en XP, keyData de ProcessCmdKey y KeyEventArgs.KeyData).
    //   - KeyCode: la tecla sola, o None si Keys no tiene ese codigo (KeyEventArgs.cs:45-58: «since Keys can be discontiguous, keeping
    //     Enum.IsDefined»: con los valores que define Keys.cs de WinForms, los de DefineKeys). Los codigos que Rocola OS da (Botones.cs)
    //     estan todos.
    //   - Handled y SuppressKeyPress: SuppressKeyPress = true tambien pone Handled (KeyEventArgs.cs:84-92); con el, el caracter de la
    //     tecla no llega a nadie (Control.ProcessKeyEventArgs: RemovePendingMessages de WM_CHAR, WM_SYSCHAR y WM_IME_CHAR,
    //     Control.cs:9680-9685).
    //   - Repetida: el bit 30 de lParam de WM_KEYDOWN («The previous key state. The value is 1 if the key is down before the message is
    //     sent», documentacion de WM_KEYDOWN): la repeticion de la tecla mantenida. XP lo mira para no contar otra moneda, ni saltar otra
    //     vez, ni repetir sus atajos (Form1.cs ProcessCmdKey, CaptureConfigKey, AsistenteEntrada).
    public class PulsoTecla
    {
        private bool suprime;
        public PulsoTecla(Tecla keyData) { KeyData = keyData; }
        public PulsoTecla(Tecla keyData, bool repetida) { KeyData = keyData; Repetida = repetida; }
        public Tecla KeyData { get; }
        public Tecla KeyCode { get { int k = (int)(KeyData & Tecla.KeyCode); return DefineKeys(k) ? (Tecla)k : Tecla.None; } }
        public bool Handled { get; set; }
        public bool SuppressKeyPress { get { return suprime; } set { suprime = value; Handled = value; } }
        public bool Repetida { get; }

        // Los codigos que define Keys.cs de WinForms (todos sus miembros, de 0 a 0xFFFF): 0-6, 8-0xA, 0xC-0xD, 0x10-0x15, 0x17-0x19,
        // 0x1B-0x39, 0x41-0x5D, 0x5F-0x87, 0x90-0x91, 0xA0-0xB7, 0xBA-0xC0, 0xDB-0xDF, 0xE2, 0xE5, 0xE7, 0xF6-0xFE y 0xFFFF (KeyCode).
        internal static bool DefineKeys(int k)
        {
            if (k == 0xFFFF) return true;
            if (k < 0 || k > 0xFF) return false;
            return k <= 0x06 || (k >= 0x08 && k <= 0x0A) || k == 0x0C || k == 0x0D || (k >= 0x10 && k <= 0x15) || (k >= 0x17 && k <= 0x19)
                || (k >= 0x1B && k <= 0x39) || (k >= 0x41 && k <= 0x5D) || (k >= 0x5F && k <= 0x87) || k == 0x90 || k == 0x91
                || (k >= 0xA0 && k <= 0xB7) || (k >= 0xBA && k <= 0xC0) || (k >= 0xDB && k <= 0xDF) || k == 0xE2 || k == 0xE5 || k == 0xE7
                || (k >= 0xF6 && k <= 0xFE);
        }
    }

    // EL CARACTER DE UNA TECLA (WM_CHAR, o WM_SYSCHAR con Alt): KeyChar y Handled, como KeyPressEventArgs.cs:27 y 33. Si alguien lo
    // atiende (Handled), ya no llega a nadie mas: con KeyPreview, el de la ventana va primero (Control.ProcessKeyMessage, Control.cs:9708;
    // Form.ProcessKeyPreview, Form.cs:4985-4993) y lo que devuelve es su Handled (Control.ProcessKeyEventArgs, Control.cs:9675).
    public class PulsoCaracter
    {
        public PulsoCaracter(char keyChar) { KeyChar = keyChar; }
        public char KeyChar { get; set; }
        public bool Handled { get; set; }
    }
}
