using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        private static char KeyToChar(Keys k)
        {
            int v = (int)k;
            if (v >= 'A' && v <= 'Z')
            {
                return (char)v;
            }
            if (v >= '0' && v <= '9')
            {
                return (char)v;
            }
            if (v >= 96 && v <= 105)
            {
                return (char)('0' + (v - 96));
            }
            return '\0';
        }

        private bool IsConfiguredKey(Keys kc)
        {
            if (kc != keyUp && kc != keyDown && kc != keyLeft && kc != keyRight && kc != keyOK && kc != keyBack && kc != keyCoin)
            {
                return kc == keySkip;
            }
            return true;
        }

        private bool IsUp(Keys kc)
        {
            if (kc != keyUp)
            {
                if (kc == Keys.Up)
                {
                    return !IsConfiguredKey(Keys.Up);
                }
                return false;
            }
            return true;
        }

        private bool IsDown(Keys kc)
        {
            if (kc != keyDown)
            {
                if (kc == Keys.Down)
                {
                    return !IsConfiguredKey(Keys.Down);
                }
                return false;
            }
            return true;
        }

        private bool IsLeft(Keys kc)
        {
            if (kc != keyLeft)
            {
                if (kc == Keys.Left)
                {
                    return !IsConfiguredKey(Keys.Left);
                }
                return false;
            }
            return true;
        }

        private bool IsRight(Keys kc)
        {
            if (kc != keyRight)
            {
                if (kc == Keys.Right)
                {
                    return !IsConfiguredKey(Keys.Right);
                }
                return false;
            }
            return true;
        }

        private bool IsOK(Keys kc)
        {
            if (kc != keyOK)
            {
                if (kc == Keys.Enter)
                {
                    return !IsConfiguredKey(Keys.Enter);
                }
                return false;
            }
            return true;
        }

        private bool IsBack(Keys kc)
        {
            if (kc != keyBack)
            {
                if (kc == Keys.Escape)
                {
                    return !IsConfiguredKey(Keys.Escape);
                }
                return false;
            }
            return true;
        }

        private bool IsCoin(Keys kc)
        {
            if (kc != keyCoin)
            {
                if (kc == Keys.Insert)
                {
                    return !IsConfiguredKey(Keys.Insert);
                }
                return false;
            }
            return true;
        }

        private bool IsSkip(Keys kc)
        {
            if (kc != keySkip)
            {
                if (kc == Keys.ShiftKey)
                {
                    return !IsConfiguredKey(Keys.ShiftKey);
                }
                return false;
            }
            return true;
        }

        private bool BackHeld()
        {
            if (!KeyHeld(keyBack))
            {
                if (KeyHeld(Keys.Escape))
                {
                    return !IsConfiguredKey(Keys.Escape);
                }
                return false;
            }
            return true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            lastInput = DateTime.Now;
            if (configMode)
            {
                CaptureConfigKey(keyData, msg);
                return true;
            }
            bool wasFull = videoFull;
            Keys kc = keyData & Keys.KeyCode;
            if (IsCoin(kc))
            {
                if (wasFull)
                {
                    ExitVideoFull();
                }
                AddCoin();
                return true;
            }
            if (IsSkip(kc) && (msg.LParam.ToInt64() & 0x40000000) == 0L)
            {
                SkipSong();
                return true;
            }
            if (wasFull)
            {
                ExitVideoFull();
                return true;
            }
            if (keyData == Keys.F5)
            {
                LoadMusicFolder();
                return true;
            }
            if (IsOK(kc) && BackHeld())
            {
                ToggleSearch();
                return true;
            }
            if (searchOpen)
            {
                MarkSearchActivity();
                if (IsLeft(kc))
                {
                    searchOverlay.MoveSel(-1, 0);
                    return true;
                }
                if (IsRight(kc))
                {
                    searchOverlay.MoveSel(1, 0);
                    return true;
                }
                if (IsUp(kc))
                {
                    searchOverlay.MoveSel(0, -1);
                    return true;
                }
                if (IsDown(kc))
                {
                    searchOverlay.MoveSel(0, 1);
                    return true;
                }
                if (IsOK(kc))
                {
                    searchOverlay.PressSelected();
                    return true;
                }
                if (IsBack(kc))
                {
                    searchOverlay.Backspace();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }
            if (panelSongs.Visible)
            {
                int rowsPerPage = SongRowsPerPage();
                if (IsUp(kc))
                {
                    MoveSong(-1);
                    return true;
                }
                if (IsDown(kc))
                {
                    MoveSong(1);
                    return true;
                }
                if (IsLeft(kc))
                {
                    MoveSong(-rowsPerPage);
                    return true;
                }
                if (IsRight(kc))
                {
                    MoveSong(rowsPerPage);
                    return true;
                }
                if (IsOK(kc))
                {
                    EnqueueSelectedSong();
                    return true;
                }
                if (IsBack(kc))
                {
                    BackToAlbums();
                    return true;
                }
            }
            else
            {
                if (IsLeft(kc))
                {
                    if (BackHeld())
                    {
                        ChangeLetter(-1);
                    }
                    else
                    {
                        MoveInPage(-1);
                    }
                    return true;
                }
                if (IsRight(kc))
                {
                    if (BackHeld())
                    {
                        ChangeLetter(1);
                    }
                    else
                    {
                        MoveInPage(1);
                    }
                    return true;
                }
                if (IsUp(kc))
                {
                    NextPage();
                    return true;
                }
                if (IsDown(kc))
                {
                    PrevPage();
                    return true;
                }
                if (IsOK(kc))
                {
                    List<Album> list = CurrentList();
                    if (selAlbumIndex >= 0 && selAlbumIndex < list.Count)
                    {
                        EnterAlbum(list[selAlbumIndex]);
                    }
                    return true;
                }
                if (IsBack(kc))
                {
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private string KeysFile()
        {
            return Path.Combine(Application.StartupPath, "rocola_teclas.txt");
        }

        private bool LoadKeys()
        {
            try
            {
                string path = KeysFile();
                if (!File.Exists(path))
                {
                    return false;
                }
                Keys up = Keys.None;
                Keys down = Keys.None;
                Keys left = Keys.None;
                Keys right = Keys.None;
                Keys ok = Keys.None;
                Keys back = Keys.None;
                Keys coin = Keys.None;
                Keys skip = Keys.None;
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    string name = line.Substring(0, eq).Trim().ToLower();
                    int code;
                    if (int.TryParse(line.Substring(eq + 1).Trim(), out code))
                    {
                        Keys key = (Keys)code;
                        switch (name)
                        {
                            case "arriba":
                                up = key;
                                break;
                            case "abajo":
                                down = key;
                                break;
                            case "izquierda":
                                left = key;
                                break;
                            case "derecha":
                                right = key;
                                break;
                            case "poner":
                                ok = key;
                                break;
                            case "regresar":
                                back = key;
                                break;
                            case "moneda":
                                coin = key;
                                break;
                            case "saltar":
                                skip = key;
                                break;
                        }
                    }
                }
                if (up == Keys.None || down == Keys.None || left == Keys.None || right == Keys.None || ok == Keys.None || back == Keys.None || coin == Keys.None || skip == Keys.None)
                {
                    return false;
                }
                keyUp = up;
                keyDown = down;
                keyLeft = left;
                keyRight = right;
                keyOK = ok;
                keyBack = back;
                keyCoin = coin;
                keySkip = skip;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void SaveKeys()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("arriba=").Append((int)keyUp).Append("\r\n");
                sb.Append("abajo=").Append((int)keyDown).Append("\r\n");
                sb.Append("izquierda=").Append((int)keyLeft).Append("\r\n");
                sb.Append("derecha=").Append((int)keyRight).Append("\r\n");
                sb.Append("poner=").Append((int)keyOK).Append("\r\n");
                sb.Append("regresar=").Append((int)keyBack).Append("\r\n");
                sb.Append("moneda=").Append((int)keyCoin).Append("\r\n");
                sb.Append("saltar=").Append((int)keySkip).Append("\r\n");
                File.WriteAllText(KeysFile(), sb.ToString());
            }
            catch
            {
            }
        }

        private void StartConfig()
        {
            if (searchOpen)
            {
                CloseSearch();
            }
            configMode = true;
            configStep = 0;
            for (int i = 0; i < configCap.Length; i++)
            {
                configCap[i] = Keys.None;
            }
            if (lblConfig != null)
            {
                lblConfig.Visible = true;
                lblConfig.BringToFront();
            }
            UpdateConfigPrompt(null);
        }

        private void UpdateConfigPrompt(string warn)
        {
            if (lblConfig != null)
            {
                string text = "CONFIGURACION DE BOTONES DE LA ROCOLA\r\n\r\n";
                text = text + "Paso " + (configStep + 1) + " de " + ConfigSteps.Length + "\r\n\r\n";
                text += "Presiona en la rocola el boton para:\r\n\r\n";
                text = text + ">>>   " + ConfigSteps[configStep] + "   <<<";
                if (warn != null)
                {
                    text = text + "\r\n\r\n" + warn;
                }
                text += "\r\n\r\n\r\n(cada boton debe ser distinto)";
                lblConfig.Text = text;
                lblConfig.BringToFront();
            }
        }

        private void CaptureConfigKey(Keys keyData, Message msg)
        {
            if ((msg.LParam.ToInt64() & 0x40000000) != 0L)
            {
                return;
            }
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.None || key == Keys.Menu || key == Keys.ControlKey)
            {
                return;
            }
            for (int i = 0; i < configStep; i++)
            {
                if (configCap[i] == key)
                {
                    UpdateConfigPrompt("Ese boton ya esta usado. Presiona OTRO.");
                    return;
                }
            }
            configCap[configStep] = key;
            configStep++;
            if (configStep >= ConfigSteps.Length)
            {
                FinishConfig();
            }
            else
            {
                UpdateConfigPrompt(null);
            }
        }

        private void FinishConfig()
        {
            keyUp = configCap[0];
            keyDown = configCap[1];
            keyLeft = configCap[2];
            keyRight = configCap[3];
            keyOK = configCap[4];
            keyBack = configCap[5];
            keyCoin = configCap[6];
            keySkip = configCap[7];
            SaveKeys();
            configMode = false;
            needConfig = false;
            if (lblConfig != null)
            {
                lblConfig.Visible = false;
            }
            Focus();
            ShowToast("¡Botones configurados!");
        }
    }
}
