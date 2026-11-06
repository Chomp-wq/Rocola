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
                foreach (string line in File.ReadAllLines(ConfigFile)) AplicaLineaConfig(line.Trim());
            }
            catch { }
            if (!sbrAllowed) sbrOn = false;   // la maquina no cumple los requisitos del NIVEL 2: apagado si o si
        }

        // Una linea de rocola_config.txt (LoadConfigPrefs de XP); la usan la lectura del archivo y «Dejar como de fabrica».
        private void AplicaLineaConfig(string s)
        {
            if (s.StartsWith("ia_datos=")) iaHarvestOn = s.Substring(9).Trim() != "0";   // juntar material para entrenar la IA (de XP)
            else if (s.StartsWith("ia_datos_mb=")) { int v; if (int.TryParse(s.Substring(12).Trim(), out v)) iaMaxBytes = (v <= 0) ? 0L : (long)v * 1024 * 1024; }   // tope del archivo de material (como XP)
            else if (s.StartsWith("diagnostico=")) { diagOn = s.Substring(12).Trim() != "0"; DiagMemoria = diagOn; }   // bitacora en rocola_diagnostico.txt
            else if (s.StartsWith("normalizar=")) normalizeOn = s.Substring(11).Trim() != "0";   // nivelar volumen de las canciones (por defecto si)
            else if (s.StartsWith("respaldo_dir=")) { string d = s.Substring(13).Trim(); if (d.Length > 0) backupDir = d; }   // 2o destino del respaldo de datos aprendidos
            else if (s.StartsWith("nivel_objetivo=")) { float v; if (float.TryParse(s.Substring(15).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) normTarget = Clampf(v, NormTargetMin, NormTargetMax); }
            else if (s.StartsWith("nivel_fuerza=")) { float v; if (float.TryParse(s.Substring(13).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) normStrength = Clampf(v, 0f, 1f); }
            else if (s.StartsWith("realce=")) { float v; if (float.TryParse(s.Substring(7).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) excAmount = Clampf(v, 0f, 1f); }   // realce de agudos (exciter, tipo DSEE)
            else if (s.StartsWith("bajo=")) { float v; if (float.TryParse(s.Substring(5).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) bassAmount = Clampf(v, 0f, 1f); }   // bajo psicoacustico (armonicos del grave)
            // EL MODO NOCHE (volvio el 7-11-2026, ModoNoche.cs); noche_db= y noche_compensa= (los de antes) se ignoran.
            else if (s.StartsWith("noche=")) nocheOn = s.Substring(6).Trim() != "0";
            else if (s.StartsWith("noche_desde=")) { int v; if (int.TryParse(s.Substring(12).Trim(), out v) && v >= 0 && v <= 23) nocheDesde = v; }
            else if (s.StartsWith("noche_hasta=")) { int v; if (int.TryParse(s.Substring(12).Trim(), out v) && v >= 0 && v <= 23) nocheHasta = v; }
            else if (s.StartsWith("noche_pct=")) { int v; if (int.TryParse(s.Substring(10).Trim(), out v)) nochePct = Math.Max(NochePctMin, Math.Min(NochePctMax, v)); }
            else if (s.StartsWith("noche")) { }
            else if (s.StartsWith("saltar_silencios=")) saltaSilencios = s.Substring(17).Trim() != "0";   // saltar los silencios largos (SinSilencios.cs)
            else if (s.StartsWith("grave_mono=")) graveMonoHz = GraveMonoHz(s.Substring(11));   // corte del grave en mono (Hz; 0 = apagado)
            else if (s.StartsWith("ancho_estereo=")) anchoEstereo = AnchoEstereo(s.Substring(14));   // % del estereo (100 = como viene)
            else if (s.StartsWith("subgrave=")) subgraveHz = SubgraveHz(s.Substring(9));   // corte del subgrave (Hz; 0 = sin corte)
            else if (s.StartsWith("canal_mudo=")) canalMudo = s.Substring(11).Trim() != "0";   // S1: un canal mudo, por los dos
            else if (s.StartsWith("quita_zumbido=")) quitaZumbido = s.Substring(14).Trim() != "0";   // T6: el zumbido de la corriente
            else if (s.StartsWith("dsee_billar_cortes=")) dseeBillarCortes = s.Substring(19).Trim() == "1";   // el DSEE en el billar, solo en las muy cortadas (apagado de fabrica)
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
            else if (s.StartsWith("ficha_disco=")) FichaDisco = s.Substring(12).Trim() != "0";             // la pantalla viva (PantallaViva.cs)
            else if (s.StartsWith("letras_lupa=")) LetrasLupa = s.Substring(12).Trim() != "0";
            else if (s.StartsWith("pagina_desliza=")) PaginaDesliza = s.Substring(15).Trim() != "0";
            else if (s.StartsWith("lista_grande=")) ListaGrande = s.Substring(13).Trim() != "0";
            else if (s.StartsWith("eq_sonando=")) EqSonando = s.Substring(11).Trim() != "0";
            else if (s.StartsWith("cola_minutos=")) ColaMinutos = s.Substring(13).Trim() != "0";
            else if (s.StartsWith("vuelo_cola=")) VueloCola = s.Substring(11).Trim() != "0";
            else if (s.StartsWith("moneda_cae=")) MonedaCae = s.Substring(11).Trim() != "0";
            else if (s.StartsWith("sin_creditos_invita=")) SinCreditosInvita = s.Substring(20).Trim() != "0";
            else if (s.StartsWith("avisos_claros=")) AvisosClaros = s.Substring(14).Trim() != "0";
            else if (s.StartsWith("escaparate=")) Escaparate = s.Substring(11).Trim() != "0";
            else if (s.StartsWith("top_noche=")) TopNocheOn = s.Substring(10).Trim() != "0";
            else if (s.StartsWith("color_genero=")) ColorGenero = s.Substring(13).Trim() != "0";
            else if (s.StartsWith("letra_lejos=")) LetraLejos = s.Substring(12).Trim() != "0";
            else if (s.StartsWith("guia_rojo=")) GuiaRojo = s.Substring(10).Trim() != "0";
            else if (s.StartsWith("textos_deslizantes=")) TextosDeslizantes = s.Substring(19).Trim() != "0";
            else if (s.StartsWith("buscador_portadas=")) BuscadorPortadas = s.Substring(18).Trim() != "0";
            else if (s.StartsWith("luna=")) EstrellaCielo = s.Substring(5).Trim() != "0";                 // la estrella del cielo
            else if (s.StartsWith("estrella_3d=")) Estrella3d = s.Substring(12).Trim() != "0";
            else if (s.StartsWith("halo_lunar=")) HaloLunar = s.Substring(11).Trim() != "0";
            else if (s.StartsWith("polvo_lunar=")) PolvoLunar = s.Substring(12).Trim() != "0";
            else if (s.StartsWith("estela_estelar=")) EstelaEstelar = s.Substring(15).Trim() != "0";
            else if (s.StartsWith("actualizar_solo=")) ActualizarSolo = s.Substring(16).Trim() != "0";
            else if (s.StartsWith("spotify=")) SpotifyOn = s.Substring(8).Trim() == "1";
            else if (s.StartsWith("calidad_spotify=")) CalidadSpotify = s.Substring(16).Trim() != "0";
            else if (s.StartsWith("letras_spotify=")) LetrasSpotify = s.Substring(15).Trim() != "0";
            else if (s.StartsWith("portadas_spotify=")) PortadasSpotify = s.Substring(17).Trim() != "0";
            else if (s.StartsWith("completar_discos=")) CompletarDiscos = s.Substring(17).Trim() != "0";
            else if (s.StartsWith("artista_fondo=")) ArtistaFondo = s.Substring(14).Trim() != "0";
            else if (s.StartsWith("muro_reposo=")) MuroReposo = s.Substring(12).Trim() != "0";             // el estado extendido (EstadoExtendido.cs)
            else if (s.StartsWith("estado_expandido=")) EstadoExpandido = s.Substring(17).Trim() != "0";
            else if (s.StartsWith("cola_doce=")) ColaDoce = s.Substring(10).Trim() != "0";
            else if (s.StartsWith("video_vol=")) { int v; if (int.TryParse(s.Substring(10).Trim(), out v)) videoVol = v < 0 ? 0 : (v > 100 ? 100 : v); }   // volumen base de los videos (de XP)
            else if (LineaVisual(s)) { }   // las mejoras visuales de Rocola OS (MejorasVisuales.cs)
            else if (s.StartsWith("marca_cola=")) MarcaCola = s.Substring(11).Trim() != "0";             // la hoja Detalles de XP
            else if (s.StartsWith("cola_portadas=")) ColaPortadas = s.Substring(14).Trim() != "0";
            else if (s.StartsWith("fondo_portada=")) FondoPortada = s.Substring(14).Trim() != "0";
            else if (s.StartsWith("portada_manda=")) PortadaManda = Porciento(s.Substring(14), 50, 0, 100);   // la hoja Colores de XP
            else if (s.StartsWith("cielo_azul=")) CieloAzul = Porciento(s.Substring(11), 100, 0, 100);
            else if (s.StartsWith("cielo_negro=")) CieloNegro = Porciento(s.Substring(12), 42, 25, 66);
            else if (s.StartsWith("relevo=")) Relevo = s.Substring(7).Trim() != "0";
            else if (s.StartsWith("turno=")) turnoOn = s.Substring(6).Trim() != "0";   // el letrero de cuando suena tu cancion (de XP)
            else if (s.StartsWith("dir="))   // carpetas de musica extra (en XP, Ctrl+F2)
            {
                string d = s.Substring(4).Trim();
                if (d.Length > 0 && !extraMusicDirs.Contains(d)) extraMusicDirs.Add(d);
            }
        }
    }
}
