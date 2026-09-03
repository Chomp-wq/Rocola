using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

// TODAS LAS FUGACES ERAN LA MISMA FUGAZ.
//
// En las DOS paredes -la de tarjeta grafica y la horneada- una estrella fugaz salia siempre del mismo
// sitio y siempre hacia el mismo lado:
//
//     X  = azar * ancho * 0.6      <- siempre el lado izquierdo
//     Y  = azar * alto  * 0.45     <- siempre la parte de arriba
//     VX = +velocidad              <- siempre hacia la derecha
//     VY = +velocidad * 0.30..0.70 <- siempre hacia abajo
//
// O sea que TODAS caian hacia abajo-derecha con un angulo de entre 19 y 33 grados. Y cae una cada 7-16
// segundos: quien mira la rocola un minuto ve cinco identicas, y deja de mirar.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE SALGAN DE UN SOLO SITIO. Las dos paredes tienen que tirar las MISMAS fugaces: con dos copias de
//      la cuenta, la maquina del bar y la de repuesto tendrian cielos distintos.
//   2) QUE LAS DIRECCIONES SE REPARTAN DE VERDAD, contadas en cientos de tiradas y repartidas por sectores.
//   3) QUE NAZCAN FUERA DE LA PANTALLA, por el borde por donde entran. Si nacieran dentro, la mitad se
//      saldria en dos palmos sin que diera tiempo a verla.
//   4) QUE ALGUNA SEA DE LAS GORDAS: lenta, gruesa y con estela larga. Es lo que hace que alguien levante
//      la vista.
//   5) Y LA GORDA DE VERDAD: QUE SE BORREN POR LOS CUATRO LADOS. Antes solo se apagaban al salirse por la
//      derecha o por abajo, porque no podian irse por ningun otro lado. Con la direccion suelta, una que se
//      vaya por la izquierda seguiria viva, invisible, ocupando una de las dos ranuras hasta que se le
//      acabara la vida, y mientras tanto NO SALDRIA NINGUNA OTRA. Es el fallo silencioso de este cambio.
class Fugaces
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static void Pon(Type t, object o, string campo, object v)
    { FieldInfo f = t.GetField(campo); if (f != null) f.SetValue(o, v); }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        Type TS = TF.GetNestedType("Starfield", BindingFlags.NonPublic | BindingFlags.Public);

        Console.WriteLine("\n  1) LAS DOS PAREDES TIRAN LAS MISMAS FUGACES\n");
        MethodInfo mTira = TF.GetMethod("TiraFugaz", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Check(mTira != null, "de donde sale una fugaz y hacia donde va se decide en UN solo sitio (TiraFugaz)");

        List<MethodBase> pared = Codigo.MetodosConAyudantes(TF, "DrawShoots");
        bool laPide = false;
        foreach (MethodBase m in pared) if (Codigo.Llama(m, "TiraFugaz")) laPide = true;
        Check(laPide, "y la pared sin tarjeta grafica se la pide ahi, no se hace su propia cuenta");
        Type TG = null;
        foreach (Type t in asm.GetTypes()) if (t.Name == "GpuStarfield") TG = t;
        if (TG != null)
        {
            bool gpuPide = false;
            foreach (MethodBase m in Codigo.MetodosConAyudantes(TG, "UpdateAndDrawShoots"))
                if (Codigo.Llama(m, "TiraFugaz")) gpuPide = true;
            Check(gpuPide, "y la de tarjeta grafica tambien");
        }

        // =========================================================================================
        Console.WriteLine("\n  2) Y VAN A DONDE LES DA LA GANA, NO TODAS AL MISMO SITIO\n");
        const float W = 1920f, H = 1080f;
        const int N = 2000;
        if (mTira != null)
        {
        Random r = new Random(11);
        int[] sector = new int[8];        // el angulo de bajada, en ocho trozos de izquierda a derecha
        int porIzq = 0, porDer = 0, porArriba = 0, dentro = 0, gordas = 0;
        double colaMax = 0, colaMin = 9;
        for (int i = 0; i < N; i++)
        {
            object[] arg = { r, W, H, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false };
            mTira.Invoke(null, arg);
            float x = (float)arg[3], y = (float)arg[4], vx = (float)arg[5], vy = (float)arg[6];
            float cola = (float)arg[9]; bool gorda = (bool)arg[10];
            double ang = Math.Atan2(vy, vx);                       // 0 = a la derecha, pi/2 = abajo
            int s = (int)((ang / Math.PI) * 8); if (s < 0) s = 0; if (s > 7) s = 7;
            sector[s]++;
            if (vy <= 0f) Console.WriteLine("     OJO: una fugaz que sube");
            if (x < 0f) porIzq++; else if (x > W) porDer++; else if (y < 0f) porArriba++;
            if (x >= 0f && x <= W && y >= 0f && y <= H) dentro++;
            if (gorda) { gordas++; if (cola > colaMax) colaMax = cola; }
            else if (cola < colaMin) colaMin = cola;
        }
        Console.Write("     de " + N + " tiradas, por sectores de angulo: ");
        for (int i = 0; i < 8; i++) Console.Write(sector[i] + (i < 7 ? " " : "\n"));
        // Los dos sectores de los extremos se dejan vacios A PROPOSITO: una fugaz a cinco grados de la
        // horizontal no cae, se desliza, y no parece una fugaz. El barrido va de 32 a 148 grados.
        int usados = 0, mayor = 0;
        for (int i = 0; i < 8; i++) { if (sector[i] > N / 100) usados++; if (sector[i] > mayor) mayor = sector[i]; }
        Check(usados >= 6, "las fugaces caen repartidas por seis sectores o mas, no en uno solo");
        Check(mayor < N / 3, "y ninguno se lleva mas de un tercio: estan repartidas de verdad");
        Check(sector[0] + sector[1] > N / 12 && sector[6] + sector[7] > N / 12,
              "las hay que caen casi de lado, hacia los dos lados");
        Check(sector[3] + sector[4] > N / 12, "y las hay que caen casi rectas");

        Console.WriteLine("     entran por la izquierda " + porIzq + ", por la derecha " + porDer + " y por arriba " + porArriba);
        Check(porIzq > 0 && porDer > 0 && porArriba > 0, "y entran por los tres bordes por los que puede entrar algo que cae");
        Check(dentro == 0, "ninguna nace DENTRO de la pantalla: todas entran desde fuera");

        Console.WriteLine("     de las " + N + ", " + gordas + " son de las gordas (" + (gordas * 100 / N) + "%)");
        Check(gordas > N / 20 && gordas < N / 4, "una de cada ocho o asi es de las gordas: ni ninguna ni todas");
        Check(colaMax > colaMin, "y la gorda trae la estela mas larga que una normal");
        }

        // =========================================================================================
        Console.WriteLine("\n  3) Y SE BORRAN POR LOS CUATRO LADOS\n");
        // Esto es lo que de verdad se puede romper. Se le mete a mano una fugaz que se va POR LA IZQUIERDA
        // -que antes era imposible- y se mira si la pared la apaga. Con el codigo de antes se quedaria
        // encendida para siempre, invisible, y con las dos ranuras ocupadas no saldria ninguna mas.
        if (TS != null)
        {
            object sf = Activator.CreateInstance(TS, true);
            TS.GetField("Density", TODO).SetValue(sf, 1f);
            TS.GetField("SkyFullW", TODO).SetValue(sf, 640); TS.GetField("SkyFullH", TODO).SetValue(sf, 480);
            using (Bitmap bm = new Bitmap(640, 480, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(bm))
            {
                Color c1 = Color.FromArgb(255, 8, 10, 24), c2 = Color.FromArgb(255, 60, 40, 30);
                TS.GetMethod("DrawCached").Invoke(sf, new object[] { g, 640, 480, c1, c2 });   // arma pinceles y capas
                FieldInfo fSh = TS.GetField("shoots", TODO);
                MethodInfo mDraw = TS.GetMethod("DrawShoots", TODO);
                Array sh = (Array)fSh.GetValue(sf);
                Type TSh = sh.GetType().GetElementType();
                string[] lados = { "por la izquierda", "por arriba", "por la derecha", "por abajo" };
                float[] vxs = { -4000f, 0f, 4000f, 0f }, vys = { 0f, -4000f, 0f, 4000f };
                for (int k = 0; k < 4; k++)
                {
                    object one = Activator.CreateInstance(TSh);
                    TSh.GetField("On").SetValue(one, true);
                    TSh.GetField("X").SetValue(one, 320f); TSh.GetField("Y").SetValue(one, 240f);
                    TSh.GetField("VX").SetValue(one, vxs[k]); TSh.GetField("VY").SetValue(one, vys[k]);
                    TSh.GetField("Life").SetValue(one, 999f); TSh.GetField("MaxLife").SetValue(one, 999f);
                    // Grosor y estela solo existen desde este cambio; corrida contra el ejecutable de
                    // antes se las salta en vez de reventar, y asi el fallo que importa -el borrado- se
                    // ve escrito en vez de en una pila de errores.
                    Pon(TSh, one, "Wdt", 1.2f); Pon(TSh, one, "Tail", 0.1f);
                    sh.SetValue(one, 0);
                    // EL RELOJ SE FINGE. DrawShoots avanza la fugaz por el reloj de verdad, y en cuarenta
                    // vueltas de un bucle apretado el reloj de Windows no se mueve ni un milisegundo: sin
                    // esto la fugaz no avanzaria y la prueba "pasaria" sin haber comprobado nada.
                    // Y la CITA de la proxima se manda lejos, para que no salga una nueva por en medio.
                    FieldInfo fUlt = TS.GetField("lastTick", TODO), fCita = TS.GetField("nextShoot", TODO);
                    bool apagada = false;
                    for (int paso = 0; paso < 40 && !apagada; paso++)
                    {
                        fUlt.SetValue(sf, Environment.TickCount - 100);   // 100 ms de paso
                        fCita.SetValue(sf, Environment.TickCount + 999999);
                        mDraw.Invoke(sf, new object[] { g, 640, 480 });
                        apagada = !(bool)TSh.GetField("On").GetValue(sh.GetValue(0));
                    }
                    Check(apagada, "una fugaz que se va " + lados[k] + " se apaga y deja su ranura libre");
                }
            }
        }

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO EN ORDEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
