// ================================================================================================
//  MEDIR ESTE EQUIPO, Y AJUSTAR LOS GRAFICOS CON LO MEDIDO
//  (Ctrl+F3 -> pestana "Imagen y rendimiento")
// ------------------------------------------------------------------------------------------------
//  La rocola sabia lo que cuesta cada trozo de su dibujo -eso lo miden las baterias de pruebas/-,
//  pero no sabia decirle a nadie como le va al equipo donde esta puesta. Aqui se enciende, se pulsa
//  un boton y contesta con numeros de ESE equipo: "la atraccion va a 21 cuadros por segundo;
//  apagando las nieblas se pone en 34".
//
//  TRES COSAS QUE MANDAN SOBRE TODO LO DEMAS:
//
//  1) SE MIDE, NO SE ADIVINA. Nada de una tabla de "equipos buenos y malos" escrita a mano. Se pone
//     cada pantalla de verdad, se le piden cuadros de verdad por el camino de verdad, y se cronometra.
//     Lo que sale es lo que hay.
//
//  2) SE PROPONE, NO SE APLICA. Medir no toca ni un ajuste. La propuesta sale escrita, con sus
//     numeros, y no se mueve nada hasta que se pulsa "Aplicar lo recomendado".
//
//  3) CUANDO NO LLEGA, SE DICE. Hay equipos que no dan 30 cuadros por segundo ni con todo apagado.
//     Ahi lo suyo es decirlo, no fingir que se arreglo.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        // LA META. Se pidio que el peor caso pasara de 30 cuadros por segundo. Se apunta a 33 y no a 30
        // porque una medida oscila: dejarlo clavado en la raya haria que la misma maquina saliera "bien"
        // una vez y "mal" la siguiente sin haber cambiado nada.
        private const int MetaFps = 33;
        // POR DEBAJO DE ESTO, UN EFECTO NO CUESTA NADA. Dos medidas del MISMO dibujo en la misma maquina
        // no dan el mismo numero: entre pasadas se mueven unas decimas. Un efecto cuyo "ahorro" cabe en
        // ese temblor no ha demostrado costar nada, y apagarlo seria afear la rocola a cambio de ruido.
        // (Es la regla de siempre; en pruebas/buscacosto.cs esta escrita como "Se midio, y NO PAGA".)
        private const double RuidoMs = 0.20;

        // ---------------------------------------------------------------------------------------------
        //  QUE APAGAR, Y CUANDO PARAR
        // ---------------------------------------------------------------------------------------------
        //  Esta funcion es la que decide, y esta aparte del resto A PROPOSITO: no toca pantalla, no toca
        //  ajustes y no sabe que es una estrella. Recibe numeros -lo que cuesta un cuadro del peor
        //  escenario y lo que se ahorra apagando cada efecto- y devuelve a cuales apagar. Asi se puede
        //  comprobar de verdad (pruebas/banco.cs le da costes de mentira y mira si decide bien), que con
        //  algo que solo se pudiera probar delante de una tarjeta grafica no habria manera.
        //
        //  El orden lo manda el NUMERO: primero el que mas ahorra. Y para en cuanto la cuenta pasa de la
        //  meta, porque apagar de mas es tan malo como no apagar: deja la rocola fea sin motivo.
        // ---------------------------------------------------------------------------------------------
        private static int[] ProponeApagados(double[] msAhorro, double msPeor, int meta, out double msFinal)
        {
            msFinal = msPeor;
            List<int> apaga = new List<int>();
            if (msAhorro == null || msAhorro.Length == 0 || meta <= 0) return apaga.ToArray();

            // Los que cuestan algo de verdad, del que mas ahorra al que menos.
            List<int> orden = new List<int>();
            for (int i = 0; i < msAhorro.Length; i++) if (msAhorro[i] > RuidoMs) orden.Add(i);
            double[] ah = msAhorro;
            orden.Sort(delegate (int a, int b) { return ah[b].CompareTo(ah[a]); });

            double objetivo = 1000.0 / meta;   // los ms por cuadro que hacen falta para llegar a la meta
            for (int k = 0; k < orden.Count && msFinal > objetivo; k++)
            {
                apaga.Add(orden[k]);
                msFinal -= ah[orden[k]];
            }
            if (msFinal < 0.05) msFinal = 0.05;   // un cuadro no cuesta cero: no dividir por nada
            return apaga.ToArray();
        }

        // Los cuadros por segundo que dan esos ms de trabajo, sin pasarse del techo de la pantalla.
        // Sin el techo el numero mentiria al reves: un cuadro de 2 ms no son 500 por segundo si el
        // monitor solo ensena 60.
        private static int FpsDe(double ms, int tope)
        {
            if (ms <= 0.05) ms = 0.05;
            int f = (int)(1000.0 / ms);
            if (tope > 0 && f > tope) f = tope;
            return f;
        }

        // ---------------------------------------------------------------------------------------------
        //  EL CATALOGO DE EFECTOS
        // ---------------------------------------------------------------------------------------------
        //  Lo que se puede bajar, con su nombre en cristiano, lo que se pierde al quitarlo, y -cuando ya
        //  se midio- lo que cuesta EN ESTE EQUIPO. Antes de medir el coste sale "sin medir": nunca un
        //  numero inventado.
        // ---------------------------------------------------------------------------------------------
        private sealed class EfectoGrafico
        {
            public string Clave;      // como se llama en el archivo de configuracion
            public string Nombre;     // como se llama en la pantalla
            public string Pierde;     // que se deja de ver al apagarlo
            public double MsAhorro;   // lo que ahorra apagarlo, MEDIDO en este equipo
            public bool Medido;
            public bool Puesto;       // como esta ahora
        }

        private EfectoGrafico[] efectos;
        private EfectoGrafico[] Efectos()
        {
            if (efectos != null) return efectos;
            efectos = new EfectoGrafico[4];
            efectos[0] = NuevoEfecto("nieblas", "Nieblas y nebulosas",
                                     "El fondo queda de cielo liso, sin las manchas de color del genero.");
            efectos[1] = NuevoEfecto("patron", "Patron nuevo cada minuto",
                                     "El cielo se queda con las mismas estrellas en vez de renovarlas.");
            efectos[2] = NuevoEfecto("fugaces", "Estrellas fugaces",
                                     "Deja de cruzar una estrella fugaz cada 7-16 segundos.");
            efectos[3] = NuevoEfecto("espectro", "Aurora del horizonte",
                                     "Se apaga el color que sube del suelo siguiendo a la musica.");
            return efectos;
        }
        private static EfectoGrafico NuevoEfecto(string clave, string nombre, string pierde)
        {
            EfectoGrafico e = new EfectoGrafico();
            e.Clave = clave; e.Nombre = nombre; e.Pierde = pierde; e.Puesto = true; e.Medido = false;
            return e;
        }

        // Enciende o apaga un efecto por su clave (lo usan el medidor y la pantalla).
        private void PonEfecto(string clave, bool on)
        {
            switch (clave)
            {
                case "nieblas": nieblasOn = on; break;
                case "patron": relevoOn = on; break;
                case "fugaces": fugacesOn = on; break;
                case "espectro": auroraAmount = on ? 0.55f : 0f; break;
            }
            AplicaEfectosGpu();
        }
        private bool LeeEfecto(string clave)
        {
            switch (clave)
            {
                case "nieblas": return nieblasOn;
                case "patron": return relevoOn;
                case "fugaces": return fugacesOn;
                case "espectro": return auroraAmount > 0.004f;
            }
            return true;
        }

        // Lleva las perillas al panel de la tarjeta grafica. Se llama al montarlo y cada vez que cambian.
        private void AplicaEfectosGpu()
        {
#if ROCOLA_GPU
            if (gpuPanel == null) return;
            gpuPanel.Nieblas = nieblasOn;
            gpuPanel.Fugaces = fugacesOn;
            gpuPanel.RelevoPatron = relevoOn;
            gpuPanel.Estrellas = estrellasDens;
            gpuPanel.TopeFps = fpsTope;
            gpuPanel.OlvidaTope();   // el tope se resuelve una vez y se guarda: al cambiarlo hay que re-resolverlo
#endif
        }

        // ---------------------------------------------------------------------------------------------
        //  LO MEDIDO
        // ---------------------------------------------------------------------------------------------
        private sealed class EscenaMedida
        {
            public string Nombre;
            public double MsTrabajo;   // lo que cuesta ARMAR un cuadro (sin la espera del monitor)
            public int Fps;            // los cuadros por segundo que salen de ahi, con el techo de la pantalla
        }
        private List<EscenaMedida> bancoEscenas;   // null = aun sin medir
        private string bancoPeor = "";             // el escenario que peor salio
        private double bancoMsPeor;
        private int bancoFpsPeor;
        private int bancoTope;                     // el techo de la pantalla en este equipo
        private string[] bancoPropuesta;           // claves a apagar (vacio = no hace falta tocar nada)
        private int bancoFpsPropuesta;             // donde quedaria
        private bool bancoConGpu;                  // si al medir la rocola dibujaba con tarjeta grafica

#if ROCOLA_GPU
        // CUANTOS CUADROS SE PIDEN POR MEDIDA. Cuarenta cuadros son dos tercios de segundo en una pantalla
        // de 60: suficiente para que el promedio se asiente y poco para que la rocola no parezca colgada.
        private const int CuadrosPorMedida = 40;
        private const int CuadrosCalentando = 8;   // los primeros no cuentan: el primer cuadro de una pantalla nueva sube texturas

        // UN PULSO DE MUSICA DE MENTIRA, y bien fuerte. Asi el medidor da lo mismo con la rocola callada
        // que en plena noche de sabado -se puede repetir-, y ademas mide el caso PEOR: el latido a tope,
        // que es cuando mas cosas se dibujan.
        private void PulsoDeMentira()
        {
            audioLevel = 0.85f;
            levLo = 0.90f; levMid = 0.72f; levHi = 0.80f;
            rmsLo = 0.30f; rmsMid = 0.22f; rmsHi = 0.18f;
            for (int b = 0; b < SpecBands; b++)
                specVis[b] = 0.55f + 0.40f * (float)Math.Sin(b * 0.7 + phase);
            auroraPres = 1f;
        }

        // MIDE UNA PANTALLA. Los cuadros los pide ESTA funcion, no el bucle de siempre: medir ocurre dentro
        // de una pulsacion, o sea con el bucle de mensajes parado, asi que esperar a que dibuje solo seria
        // esperar sentado. Cada llamada a DibujaUnCuadro es un cuadro del camino de verdad, el mismo que
        // dibuja la rocola encendida.
        //
        // Y el reloj de las animaciones se adelanta A SU RITMO (uno cada 42 ms de reloj de pared, los de
        // siempre), no una vez por cuadro: si se llamara en cada cuadro, la rocola creeria que va al doble
        // de velocidad y rehariamos la textura de la interfaz el doble de veces que en la vida real. El
        // numero saldria peor de lo que es.
        private EscenaMedida MideEscena(string nombre)
        {
            EscenaMedida m = new EscenaMedida();
            m.Nombre = nombre; m.MsTrabajo = 0.0; m.Fps = 0;
            if (gpuPanel == null || !gpuPanel.Visible) return m;

            int ultTick = Environment.TickCount - 42;
            for (int i = 0; i < CuadrosCalentando + CuadrosPorMedida; i++)
            {
                int ahora = Environment.TickCount;
                if (ahora - ultTick >= 42) { ultTick = ahora; try { AnimTimer_Tick(null, EventArgs.Empty); } catch { } }
                PulsoDeMentira();   // DESPUES del tick: el tick lo pone a cero cuando no suena nada
                if (i == CuadrosCalentando) gpuPanel.ReiniciaMedida();   // los de calentar no cuentan
                gpuPanel.DibujaUnCuadro();
            }
            m.MsTrabajo = gpuPanel.MsTrabajo;
            m.Fps = FpsDe(m.MsTrabajo, bancoTope);
            return m;
        }
#endif

#if !ROCOLA_GPU
        // Sin tarjeta grafica no hay nada que cronometrar: esta pantalla no se dibuja por ese camino. Se
        // devuelven los campos a cero -y se ASIGNAN, para que el compilador no avise de campos que nadie
        // escribe- y la pagina ya dice, en su sitio, por que aqui no hay numeros.
        private EscenaMedida MideEscena(string nombre)
        {
            EscenaMedida m = new EscenaMedida();
            m.Nombre = nombre; m.MsTrabajo = 0.0; m.Fps = 0;
            return m;
        }
#endif

        // ---------------------------------------------------------------------------------------------
        //  EL MEDIDOR
        // ---------------------------------------------------------------------------------------------
        //  Pone las cuatro pantallas donde la rocola pasa su vida, mide cada una, se queda con la peor, y
        //  mide lo que ahorra apagar cada efecto CONTRA ESA. Al acabar deja la rocola donde estaba.
        //
        //  NO GUARDA NADA Y NO APLICA NADA: solo rellena lo medido y la propuesta. (La bateria lo vigila:
        //  si algun dia esta funcion llamara a SaveConfigPrefs, se pondria roja.)
        // ---------------------------------------------------------------------------------------------
        private void MideRendimiento()
        {
            // Todo a cero primero: volver a medir no puede dejar ni un numero de la vez anterior asomando.
            bancoEscenas = new List<EscenaMedida>();
            bancoPropuesta = new string[0];
            bancoConGpu = false;
            bancoPeor = ""; bancoMsPeor = 0.0; bancoFpsPeor = 0; bancoTope = 0; bancoFpsPropuesta = 0;
#if ROCOLA_GPU
            if (gpuPanel == null || !gpuEnabled || !gpuPanel.Visible) return;
            bancoConGpu = true;
            bancoTope = gpuPanel.TopeFpsReal;

            // COMO ESTABA TODO, para dejarlo igual pase lo que pase.
            bool bkBuscador = searchOpen, bkAtraccion = attractOn;
            bool bkDisco = (panelSongs != null && panelSongs.Visible);
            bool bkNie = nieblasOn, bkPat = relevoOn, bkFug = fugacesOn;
            float bkAur = auroraAmount;
            EfectoGrafico[] ef = Efectos();
            try
            {
                // Se mide desde la pared de discos: es el punto del que se puede ir a todas las demas.
                if (bkBuscador) CloseSearch();
                if (bkAtraccion) ExitAttract();
                if (panelSongs != null && panelSongs.Visible) BackToAlbums();

                bancoEscenas.Add(MideEscena("Pared de discos"));

                List<Album> lista = CurrentList();
                if (lista != null && lista.Count > 0)
                {
                    int gi = selAlbumIndex; if (gi < 0 || gi >= lista.Count) gi = 0;
                    EnterAlbum(lista[gi]);
                    bancoEscenas.Add(MideEscena("Dentro de un disco"));
                    BackToAlbums();
                }

                OpenSearch();
                bancoEscenas.Add(MideEscena("Buscador"));
                CloseSearch();

                EnterAttract();
                bancoEscenas.Add(MideEscena("Pantalla de atraccion"));
                ExitAttract();

                // EL PEOR MANDA: de nada sirve que tres pantallas vayan sobradas si la cuarta se arrastra.
                EscenaMedida peor = null;
                foreach (EscenaMedida m in bancoEscenas)
                    if (m.MsTrabajo > 0.0 && (peor == null || m.MsTrabajo > peor.MsTrabajo)) peor = m;
                if (peor == null) return;
                bancoPeor = peor.Nombre; bancoMsPeor = peor.MsTrabajo; bancoFpsPeor = peor.Fps;

                // LO QUE CUESTA CADA EFECTO, contra ESA pantalla y no contra una cualquiera: un efecto puede
                // costar mucho donde se ve y nada donde no. Se apaga, se vuelve a medir, y la resta es su
                // coste. Se mide con TODO lo demas puesto, que es como se usa.
                PonEscena(peor.Nombre);
                for (int i = 0; i < ef.Length; i++)
                {
                    bool antes = LeeEfecto(ef[i].Clave);
                    if (!antes) { ef[i].MsAhorro = 0.0; ef[i].Medido = true; continue; }   // ya estaba apagado: no ahorra nada mas
                    PonEfecto(ef[i].Clave, false);
                    EscenaMedida sin = MideEscena(peor.Nombre);
                    PonEfecto(ef[i].Clave, true);
                    double ahorro = peor.MsTrabajo - sin.MsTrabajo;
                    ef[i].MsAhorro = (ahorro > 0.0) ? ahorro : 0.0;
                    ef[i].Medido = true;
                }
                QuitaEscena(peor.Nombre);

                // Y LA PROPUESTA. Sale de lo medido y de nada mas.
                double[] ah = new double[ef.Length];
                for (int i = 0; i < ef.Length; i++) ah[i] = ef[i].MsAhorro;
                double msFinal;
                int[] cuales = ProponeApagados(ah, bancoMsPeor, MetaFps, out msFinal);
                string[] claves = new string[cuales.Length];
                for (int i = 0; i < cuales.Length; i++) claves[i] = ef[cuales[i]].Clave;
                bancoPropuesta = claves;
                bancoFpsPropuesta = FpsDe(msFinal, bancoTope);
            }
            catch { }
            finally
            {
                // La rocola vuelve donde estaba, truene lo que truene por el camino.
                nieblasOn = bkNie; relevoOn = bkPat; fugacesOn = bkFug; auroraAmount = bkAur;
                AplicaEfectosGpu();
                try
                {
                    if (searchOpen && !bkBuscador) CloseSearch();
                    if (attractOn && !bkAtraccion) ExitAttract();
                    if (panelSongs != null && panelSongs.Visible && !bkDisco) BackToAlbums();
                    if (bkAtraccion && !attractOn) EnterAttract();
                    else if (bkBuscador && !searchOpen) OpenSearch();
                }
                catch { }
            }
#endif
        }

#if ROCOLA_GPU
        // Deja puesta la pantalla que toca medir (y la quita despues). Sale aparte porque el coste de cada
        // efecto se mide varias veces sobre la MISMA pantalla.
        private void PonEscena(string nombre)
        {
            if (nombre == "Buscador") OpenSearch();
            else if (nombre == "Pantalla de atraccion") EnterAttract();
            else if (nombre == "Dentro de un disco")
            {
                List<Album> lista = CurrentList();
                if (lista != null && lista.Count > 0)
                {
                    int gi = selAlbumIndex; if (gi < 0 || gi >= lista.Count) gi = 0;
                    EnterAlbum(lista[gi]);
                }
            }
        }
        private void QuitaEscena(string nombre)
        {
            if (nombre == "Buscador") { if (searchOpen) CloseSearch(); }
            else if (nombre == "Pantalla de atraccion") { if (attractOn) ExitAttract(); }
            else if (nombre == "Dentro de un disco") { if (panelSongs != null && panelSongs.Visible) BackToAlbums(); }
        }
#endif

        // =============================================================================================
        //  LA PAGINA "IMAGEN Y RENDIMIENTO"
        // =============================================================================================
        //  Dos tarjetas, una al lado de la otra, como pediste: a la izquierda el boton de medir con lo que
        //  salio, y a la derecha la sub-ventana de los efectos, cada uno con SU coste medido en este equipo.
        //  Debajo, la propuesta y el boton de aplicarla -que hasta que no se pulsa no mueve nada-.
        //
        //  Va toda dentro de un Panel para que cambiar de pestana sea una linea (ensenarlo o esconderlo) y
        //  la pagina de Sonido no se entere: se queda exactamente donde estaba, hasta el ultimo pixel.
        // =============================================================================================
        private bool f3Medir;          // se pulso "Medir este equipo": la ventana se cierra, mide, y vuelve
        private int f3Pagina;          // 0 = Sonido, 1 = Imagen (al volver de medir se abre en Imagen)
        private bool f3CambioGraf;     // se toco algo de graficos (para guardar al cerrar)
        private float auroraPrev = 0.55f;   // lo que valia la aurora antes de apagarla, para devolverla igual

        private Panel PaginaImagen(Font fNom, Font fVal, Font fDes, Font fBot,
                                   Color cTexto, Color cTenue, Color cApagado)
        {
            Color cMide = Color.FromArgb(140, 205, 255);
            Color cEfe = Color.FromArgb(178, 152, 255);
            Color cProp = Color.FromArgb(255, 176, 102);
            Color cBien = Color.FromArgb(120, 230, 165);
            Color cMal = Color.FromArgb(255, 170, 110);

            Panel pg = new Panel();
            pg.SetBounds(0, 96, 960, 636);
            pg.BackColor = Color.FromArgb(11, 14, 32);

            Tarjeta tMide = new Tarjeta(); tMide.SetBounds(16, 4, 456, 398); tMide.Titulo = "ESTE EQUIPO"; tMide.Acento = cMide;
            Tarjeta tEfe = new Tarjeta(); tEfe.SetBounds(488, 4, 456, 398); tEfe.Titulo = "EFECTOS"; tEfe.Acento = cEfe;
            Tarjeta tProp = new Tarjeta(); tProp.SetBounds(16, 412, 928, 160); tProp.Titulo = "LA PROPUESTA"; tProp.Acento = cProp;

            // ---------------- ESTE EQUIPO ----------------
            BotonPlano bMedir = new BotonPlano();
            bMedir.SetBounds(16, 44, 424, 38); bMedir.Font = fBot; bMedir.Text = "Medir este equipo";
            bMedir.Relleno = Color.FromArgb(28, 62, 104); bMedir.Borde = cMide;
            Label lTarda = new Label(); lTarda.SetBounds(16, 88, 424, 32); lTarda.BackColor = Color.Transparent;
            lTarda.ForeColor = cTenue; lTarda.Font = fDes;
            lTarda.Text = "Tarda unos diez segundos y veras pasar las cuatro pantallas:\nes que las esta midiendo de verdad, una por una.";

            Label lCab = new Label(); lCab.SetBounds(16, 126, 424, 18); lCab.BackColor = Color.Transparent;
            lCab.ForeColor = cApagado; lCab.Font = fDes; lCab.Text = "PANTALLA                              TRABAJO      CUADROS/S";
            Label[] lFila = new Label[4];
            for (int i = 0; i < 4; i++)
            {
                lFila[i] = new Label(); lFila[i].SetBounds(16, 148 + i * 24, 424, 20);
                lFila[i].BackColor = Color.Transparent; lFila[i].ForeColor = cTexto; lFila[i].Font = fDes;
                tMide.Controls.Add(lFila[i]);
            }
            Label lVer = new Label(); lVer.SetBounds(16, 246, 424, 148); lVer.BackColor = Color.Transparent;
            lVer.ForeColor = cTenue; lVer.Font = fDes;
            tMide.Controls.Add(bMedir); tMide.Controls.Add(lTarda); tMide.Controls.Add(lCab); tMide.Controls.Add(lVer);

            // ---------------- EFECTOS (la sub-ventana de al lado) ----------------
            EfectoGrafico[] ef = Efectos();
            Interruptor[] sw = new Interruptor[ef.Length + 1];
            Label[] lCost = new Label[ef.Length + 1];
            Label[] lPier = new Label[ef.Length + 1];
            string[] claveFila = new string[ef.Length + 1];
            for (int i = 0; i < ef.Length; i++) claveFila[i] = ef[i].Clave;
            claveFila[ef.Length] = "estrellas";

            for (int i = 0; i < claveFila.Length; i++)
            {
                int y = 40 + i * 68;
                sw[i] = new Interruptor(); sw[i].SetBounds(16, y, 300, 26);
                sw[i].ForeColor = cTexto; sw[i].Font = fNom; sw[i].Acento = cEfe;
                lCost[i] = new Label(); lCost[i].SetBounds(322, y + 2, 118, 20); lCost[i].BackColor = Color.Transparent;
                lCost[i].ForeColor = cEfe; lCost[i].Font = fVal; lCost[i].TextAlign = ContentAlignment.TopRight;
                lPier[i] = new Label(); lPier[i].SetBounds(16, y + 28, 424, 32); lPier[i].BackColor = Color.Transparent;
                lPier[i].ForeColor = cTenue; lPier[i].Font = fDes;
                tEfe.Controls.Add(sw[i]); tEfe.Controls.Add(lCost[i]); tEfe.Controls.Add(lPier[i]);
            }
            sw[ef.Length].Text = "Cielo con todas las estrellas";
            lPier[ef.Length].Text = "Apagado, el cielo se queda con la MITAD de estrellas.\nEs lo que mas se nota de esta lista, y lo que mas ahorra.";
            for (int i = 0; i < ef.Length; i++) { sw[i].Text = ef[i].Nombre; lPier[i].Text = ef[i].Pierde; }

            // ---------------- LA PROPUESTA ----------------
            Label lProp = new Label(); lProp.SetBounds(16, 40, 896, 72); lProp.BackColor = Color.Transparent;
            lProp.ForeColor = cTenue; lProp.Font = fDes;
            BotonPlano bApli = new BotonPlano();
            bApli.SetBounds(16, 116, 300, 34); bApli.Font = fBot; bApli.Text = "Aplicar lo recomendado";
            Label lApliDes = new Label(); lApliDes.SetBounds(332, 118, 580, 36); lApliDes.BackColor = Color.Transparent;
            lApliDes.ForeColor = cTenue; lApliDes.Font = fDes;
            lApliDes.Text = "Nada se mueve hasta que pulses aqui. El orden lo manda lo medido:\n"
                          + "primero lo que mas ahorra, y para en cuanto pasa. Nada de apagar de mas.";
            tProp.Controls.Add(lProp); tProp.Controls.Add(bApli); tProp.Controls.Add(lApliDes);

            BotonPlano bResetG = new BotonPlano();
            bResetG.SetBounds(16, 584, 300, 38); bResetG.Font = fBot; bResetG.Text = "Graficos como de fabrica";

            // ---------------- LO QUE SE ENSENA, SEGUN LO QUE HAYA ----------------
            Action pinta = delegate
            {
                for (int i = 0; i < claveFila.Length; i++)
                {
                    bool puesto = (claveFila[i] == "estrellas") ? (estrellasDens > 0.75f) : LeeEfecto(claveFila[i]);
                    sw[i].Checked = puesto;
                    string coste = "sin medir";
                    Color cc = cApagado;
                    if (i < ef.Length && ef[i].Medido)
                    {
                        if (ef[i].MsAhorro > RuidoMs) { coste = ef[i].MsAhorro.ToString("0.0") + " ms"; cc = cEfe; }
                        else { coste = "no cuesta"; cc = cApagado; }
                    }
                    else if (claveFila[i] == "estrellas" && bancoEscenas != null) { coste = "a mano"; cc = cApagado; }
                    lCost[i].Text = coste; lCost[i].ForeColor = cc;
                }

                // La tabla de lo medido
                for (int i = 0; i < lFila.Length; i++) lFila[i].Text = "";
                if (bancoEscenas == null)
                {
                    lCab.Visible = false;
                    lVer.Text = "Aun sin medir.\n\nPulsa el boton de arriba y la rocola se medira sola en este equipo:"
                              + " pondra sus cuatro pantallas, le pedira cuadros a la tarjeta grafica y cronometrara"
                              + " lo que cuesta armar cada uno.\n\nHasta entonces aqui no hay ningun numero, porque"
                              + " ninguno seria de verdad.";
                    lVer.ForeColor = cTenue;
                    lProp.Text = "Sin medir todavia. La propuesta sale de lo que se mida en ESTE equipo, no de una"
                               + " lista de equipos buenos y malos escrita a mano.";
                    bApli.Enabled = false;
                    bApli.Relleno = Color.FromArgb(28, 34, 62); bApli.Borde = Color.FromArgb(60, 72, 118);
                    bApli.ForeColor = cApagado;
                    return;
                }
                lCab.Visible = true;
                int n = 0;
                foreach (EscenaMedida m in bancoEscenas)
                {
                    if (n >= lFila.Length) break;
                    string nom = m.Nombre;
                    while (nom.Length < 26) nom += " ";
                    lFila[n].Text = nom + "  " + m.MsTrabajo.ToString("0.0") + " ms" + (m.MsTrabajo < 10 ? "  " : " ")
                                  + "       " + m.Fps;
                    lFila[n].ForeColor = (m.Fps >= MetaFps) ? cBien : cMal;
                    if (m.Nombre == bancoPeor) lFila[n].Font = fNom;
                    n++;
                }

                if (!bancoConGpu)
                {
                    lVer.ForeColor = cMal;
                    lVer.Text = "Esta rocola NO esta dibujando con la tarjeta grafica.\n\nPor ese camino el techo son 24"
                              + " cuadros por segundo, y no por falta de equipo: esta puesto asi a proposito para gastar"
                              + " poco. Los 30 no se alcanzan ahi por mucho que se apaguen efectos.\n\nLa perilla que"
                              + " importa en este equipo es encender la tarjeta grafica.";
                    lProp.Text = "Sin tarjeta grafica no hay nada que proponer aqui: apagar efectos no sube un techo que"
                               + " no es de la tarjeta.";
                    bApli.Enabled = false;
                    bApli.Relleno = Color.FromArgb(28, 34, 62); bApli.Borde = Color.FromArgb(60, 72, 118);
                    bApli.ForeColor = cApagado;
                    return;
                }

                string peor = "La que peor va es \"" + bancoPeor + "\": " + bancoFpsPeor + " cuadros por segundo"
                            + " (" + bancoMsPeor.ToString("0.0") + " ms de trabajo por cuadro).";
                string techo = "\n\nEl techo de esta pantalla son " + bancoTope + " cuadros por segundo: por encima de"
                             + " eso no se dibuja, porque no se veria.";
                if (bancoFpsPeor >= MetaFps)
                {
                    lVer.ForeColor = cBien;
                    lVer.Text = peor + "\n\nPasa de los 30 de sobra. No hace falta apagar nada." + techo;
                    lProp.Text = "Nada que cambiar: la pantalla que peor va ya da " + bancoFpsPeor + " cuadros por"
                               + " segundo, por encima de los 30 que se pedian.";
                    bApli.Enabled = false;
                    bApli.Relleno = Color.FromArgb(28, 34, 62); bApli.Borde = Color.FromArgb(60, 72, 118);
                    bApli.ForeColor = cApagado;
                    return;
                }

                lVer.ForeColor = cMal;
                lVer.Text = peor + "\n\nNo llega a los 30. A la derecha esta lo que cuesta cada efecto en ESTE equipo,"
                          + " medido apagandolo y volviendo a medir." + techo;

                if (bancoPropuesta == null || bancoPropuesta.Length == 0)
                {
                    lProp.Text = "No llega a los 30 y NO hay nada que apagar que lo arregle: ninguno de los efectos"
                               + " cuesta lo suficiente como para notarse. El cuello esta en otro sitio, no en los"
                               + " adornos del cielo.";
                    bApli.Enabled = false;
                    bApli.Relleno = Color.FromArgb(28, 34, 62); bApli.Borde = Color.FromArgb(60, 72, 118);
                    bApli.ForeColor = cApagado;
                    return;
                }

                string cuales = "";
                for (int i = 0; i < bancoPropuesta.Length; i++)
                {
                    string nom = bancoPropuesta[i];
                    for (int k = 0; k < ef.Length; k++) if (ef[k].Clave == bancoPropuesta[i]) nom = ef[k].Nombre.ToLower();
                    cuales += (i == 0 ? "" : (i == bancoPropuesta.Length - 1 ? " y " : ", ")) + nom;
                }
                bool llega = bancoFpsPropuesta >= MetaFps;
                lProp.Text = "\"" + bancoPeor + "\" va a " + bancoFpsPeor + " cuadros por segundo. Apagando " + cuales
                           + " se pondria en " + bancoFpsPropuesta + "."
                           + (llega ? "  Con eso pasa de los 30."
                                    : "  AUN ASI NO LLEGA A 30: este equipo no da para mas ni apagandolo todo, y es"
                                    + " mejor decirlo que fingir que se arreglo.")
                           ;
                bApli.Enabled = true;
                bApli.Relleno = Color.FromArgb(30, 96, 70); bApli.Borde = Color.FromArgb(72, 182, 132);
                bApli.ForeColor = Color.White;
            };

            // ---------------- LO QUE HACEN LOS BOTONES ----------------
            bMedir.Click += delegate
            {
                f3Medir = true;
                Form f = pg.FindForm();
                if (f != null) f.Close();   // la ventana se cierra, se mide con la pantalla libre, y vuelve sola
            };
            EventHandler alTocar = delegate (object so, EventArgs ea)
            {
                for (int i = 0; i < claveFila.Length; i++)
                {
                    if (so != sw[i]) continue;
                    if (claveFila[i] == "estrellas") { estrellasDens = sw[i].Checked ? 1f : 0.5f; AplicaEfectosGpu(); }
                    else if (claveFila[i] == "espectro")
                    {
                        if (sw[i].Checked) auroraAmount = (auroraPrev > 0.004f) ? auroraPrev : 0.55f;
                        else { if (auroraAmount > 0.004f) auroraPrev = auroraAmount; auroraAmount = 0f; }
                        AplicaEfectosGpu();
                    }
                    else PonEfecto(claveFila[i], sw[i].Checked);
                }
                f3CambioGraf = true;
                pinta();
            };
            for (int i = 0; i < sw.Length; i++) sw[i].CheckedChanged += alTocar;

            bApli.Click += delegate
            {
                if (bancoPropuesta == null) return;
                foreach (string c in bancoPropuesta)
                {
                    if (c == "estrellas") { estrellasDens = 0.5f; AplicaEfectosGpu(); }
                    else if (c == "espectro") { if (auroraAmount > 0.004f) auroraPrev = auroraAmount; auroraAmount = 0f; AplicaEfectosGpu(); }
                    else PonEfecto(c, false);
                }
                f3CambioGraf = true;
                pinta();
                ShowToast("Graficos ajustados a este equipo", Color.FromArgb(40, 130, 90));
            };
            bResetG.Click += delegate
            {
                nieblasOn = true; relevoOn = true; fugacesOn = true;
                estrellasDens = 1f; auroraAmount = 0.55f; auroraPrev = 0.55f; fpsTope = 0;
                AplicaEfectosGpu();
                f3CambioGraf = true;
                pinta();
            };

            pg.Controls.Add(tMide); pg.Controls.Add(tEfe); pg.Controls.Add(tProp); pg.Controls.Add(bResetG);
            pinta();
            return pg;
        }
    }
}
