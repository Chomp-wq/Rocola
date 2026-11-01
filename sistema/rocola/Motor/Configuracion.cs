// LA CONFIGURACION DEL SONIDO (rocola_config.txt, la misma de XP). Las lineas de cada clave son las de Rocola/Form1.Datos.cs
// (LoadConfigPrefs) tal cual, solo las del sonido; lo de la pantalla se ignora (aun no hay pantalla). Solo lee: no escribe nada.
using System;
using System.IO;

namespace Rocola
{
    public partial class Form1
    {
        private string ConfigFile { get { return RutaDatos("rocola_config.txt"); } }
        private string backupDir;   // "respaldo_dir=" (en XP, el segundo destino del respaldo de lo aprendido; aqui solo se lee)

        // Los ajustes del sonido de rocola_config.txt. Sin archivo, los de fabrica.
        private void LeeConfiguracion()
        {
            try
            {
                if (!File.Exists(ConfigFile)) return;
                foreach (string line in File.ReadAllLines(ConfigFile))
                {
                    string s = line.Trim();
                    if (s.StartsWith("diagnostico=")) { diagOn = s.Substring(12).Trim() != "0"; DiagMemoria = diagOn; }   // bitacora en rocola_diagnostico.txt
                    else if (s.StartsWith("normalizar=")) normalizeOn = s.Substring(11).Trim() != "0";   // nivelar volumen de las canciones (por defecto si)
                    else if (s.StartsWith("respaldo_dir=")) { string d = s.Substring(13).Trim(); if (d.Length > 0) backupDir = d; }   // 2o destino del respaldo de datos aprendidos
                    else if (s.StartsWith("nivel_objetivo=")) { float v; if (float.TryParse(s.Substring(15).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) normTarget = Clampf(v, NormTargetMin, NormTargetMax); }
                    else if (s.StartsWith("nivel_fuerza=")) { float v; if (float.TryParse(s.Substring(13).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) normStrength = Clampf(v, 0f, 1f); }
                    else if (s.StartsWith("realce=")) { float v; if (float.TryParse(s.Substring(7).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) excAmount = Clampf(v, 0f, 1f); }   // realce de agudos (exciter, tipo DSEE)
                    else if (s.StartsWith("bajo=")) { float v; if (float.TryParse(s.Substring(5).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) bassAmount = Clampf(v, 0f, 1f); }   // bajo psicoacustico (armonicos del grave)
                    else if (s.StartsWith("noche")) { }   // noche=, noche_desde=, noche_hasta=, noche_db=, noche_compensa=: el modo noche se retiro (24-10-2026), se ignoran
                    else if (s.StartsWith("saltar_silencios=")) saltaSilencios = s.Substring(17).Trim() != "0";   // saltar los silencios largos (SinSilencios.cs)
                    else if (s.StartsWith("grave_mono=")) graveMonoHz = GraveMonoHz(s.Substring(11));   // corte del grave en mono (Hz; 0 = apagado)
                    else if (s.StartsWith("ancho_estereo=")) anchoEstereo = AnchoEstereo(s.Substring(14));   // % del estereo (100 = como viene)
                    else if (s.StartsWith("subgrave=")) subgraveHz = SubgraveHz(s.Substring(9));   // corte del subgrave (Hz; 0 = sin corte)
                    else if (s.StartsWith("canal_mudo=")) canalMudo = s.Substring(11).Trim() != "0";   // S1: un canal mudo, por los dos
                    else if (s.StartsWith("quita_zumbido=")) quitaZumbido = s.Substring(14).Trim() != "0";   // T6: el zumbido de la corriente
                    else if (s.StartsWith("quita_preeco=")) quitaPreEco = s.Substring(13).Trim() == "1";     // R2: el pre-eco de los MP3 (apagado de fabrica)
                    else if (s.StartsWith("suaviza_eses=")) suavizaEses = s.Substring(13).Trim() == "1";   // T9: suavizar las eses
                    else if (s.StartsWith("bombo_sin_hundir=")) bomboSinHundir = s.Substring(17).Trim() == "1";   // que el bombo no hunda la voz
                    else if (s.StartsWith("retumbe_dinamico=")) retumbeDinamico = s.Substring(17).Trim() == "1";   // el retumbe dinamico
                    else if (s.StartsWith("aspereza_dinamica=")) asperezaDinamica = s.Substring(18).Trim() == "1";   // los agudos asperos dinamicos
                    else if (s.StartsWith("ataque_claro=")) ataqueClaro = s.Substring(13).Trim() == "1";   // el ataque mas claro
                    else if (s.StartsWith("quita_continua=")) quitaContinua = s.Substring(15).Trim() != "0";   // S2: quitar la continua
                    else if (s.StartsWith("estereo_auto=")) estereoAuto = s.Substring(13).Trim() == "1";   // S3: estrecho solo si hace falta
                    else if (s.StartsWith("estereo_estrecho=")) estereoEstrecho = s.Substring(17).Trim() == "1";   // interruptor: estereo al ancho_estereo
                    else if (s.StartsWith("corta_subgrave=")) cortaSubgrave = s.Substring(15).Trim() == "1";       // interruptor: subgrave fuera
                    else if (s.StartsWith("claridad_local=")) claridadLocal = s.Substring(15).Trim() == "1";       // interruptor: claridad para el local
                    else if (s.StartsWith("partes_suaves=")) partesSuaves = s.Substring(14).Trim() == "1";         // interruptor: que las partes suaves se oigan
                    else if (s.StartsWith("balance=")) balanceDb = BalanceDb(s.Substring(8));                       // dB que se baja un canal (-6..6)
                    else if (s.StartsWith("graves_local=")) gravesLocalDb = TonoLocalDb(s.Substring(13), gravesLocalDb);   // graves del local en dB (-6..6; 0 de fabrica)
                    else if (s.StartsWith("agudos_local=")) agudosLocalDb = TonoLocalDb(s.Substring(13), agudosLocalDb);   // agudos del local en dB (-6..6; 0 de fabrica)
                    else if (s.StartsWith("local_auto=")) localAuto = s.Substring(11).Trim() == "1";   // el modo automatico del local (LocalAuto.cs)
                    else if (s.StartsWith("afinado_local=")) LeeAfinado(s.Substring(14));                          // afinar el local con el celular (AfinarLocal.cs)
                    else if (s.StartsWith("afinado_ponderacion=")) afinadoPonderacion = PonderacionDe(s.Substring(20));
                    else if (s.StartsWith("afinado_usar=")) afinadoUsar = s.Substring(13).Trim() == "1";
                    else if (s.StartsWith("tipo_local=")) { }   // retirado el 25-10-2026: el modo automatico es solo para el billar (LocalAuto.cs); se ignora
                    else if (s.StartsWith("presencia_sala=")) presenciaSala = s.Substring(15).Trim() == "1";   // interruptor: presencia en sala ruidosa (compresor suave)
                    else if (s.StartsWith("a_la_medida=")) aLaMedida = s.Substring(12).Trim() == "1";              // I1: bajo y brillo a la medida de cada cancion
                    else if (s.StartsWith("quita_siseo=")) quitaSiseo = s.Substring(12).Trim() == "1";             // I2: quitar el siseo de las grabaciones viejas
                    else if (s.StartsWith("dsee_genero=")) dseePorGenero = s.Substring(12).Trim() == "1";          // I3: el modelo del DSEE del genero de la cancion
                    else if (s.StartsWith("fundido=")) { float v; if (float.TryParse(s.Substring(8).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) crossfadeSec = Clampf(v, 0f, 3f); }   // FUNDIDO entre canciones (segundos)
                    else if (s.StartsWith("declip=")) declipOn = s.Substring(7).Trim() != "0";   // des-recorte de picos aplastados (por defecto si)
                    else if (s.StartsWith("eq_auto=")) { float v; if (float.TryParse(s.Substring(8).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) eqAmount = Clampf(v / 100f, 0f, 1f); }   // EQ automatico (fuerza % a mano)
                    else if (s.StartsWith("eq_automatico=")) eqAuto = s.Substring(14).Trim() != "0";     // la fuerza del EQ la decide el programa
                    else if (s.StartsWith("bajo_automatico=")) bassAuto = s.Substring(16).Trim() != "0"; // y la cantidad de bajo tambien
                    else if (s.StartsWith("dsee=")) sbrOn = s.Substring(5).Trim() == "1";   // reconstruccion espectral (NIVEL 2)
                    else if (s.StartsWith("dsee_forzar=")) { if (s.Substring(12).Trim() == "1") { sbrForce = true; sbrAllowed = true; sbrReason = ""; } }   // salta la puerta de requisitos (pruebas)
                    else if (s.StartsWith("video_vol=")) { int v; if (int.TryParse(s.Substring(10).Trim(), out v)) videoVol = v < 0 ? 0 : (v > 100 ? 100 : v); }   // volumen base de los videos (de XP)
                    else if (s.StartsWith("turno=")) turnoOn = s.Substring(6).Trim() != "0";   // el letrero de cuando suena tu cancion (de XP)
                    else if (s.StartsWith("dir="))   // carpetas de musica extra (en XP, Ctrl+F2)
                    {
                        string d = s.Substring(4).Trim();
                        if (d.Length > 0 && !extraMusicDirs.Contains(d)) extraMusicDirs.Add(d);
                    }
                }
            }
            catch { }
            if (!sbrAllowed) sbrOn = false;   // la maquina no cumple los requisitos del NIVEL 2: apagado si o si
        }
    }
}
