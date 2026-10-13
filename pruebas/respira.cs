using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

// LA QUIETUD Y SU FRENO (la pared ya no respira).
//
// La pared de discos respiraba: subia y bajaba de brillo cada medio minuto con nadie tocando. Se retiro el
// 13-10-2026 a peticion del usuario: con musica la pared se oculta a los pocos segundos de reposo, asi que
// esa respiracion no se llegaba a ver ("es inutil").
//
// Lo que queda es la cuenta que comparte la AURORA DEL HORIZONTE, que sigue respirando cuando no suena nada,
// y su FRENO: mientras alguien usa la rocola no respira, y al frenar no se para en seco -se quedaria clavada
// mas oscura o mas clara-, se le quita el aire y acaba en su brillo de siempre.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE LA PARED YA NO RESPIRE: sin ajuste, sin claves, y el cielo con su color de siempre.
//   2) QUE LA CUENTA SIGA SIENDO PURA y que con alguien tocando valga 1 en todo el ciclo.
//   3) QUE FRENE DEPRISA Y VUELVA DESPACIO, y que al frenar acabe en su brillo normal.
//   4) QUE LA AURORA FRENE POR LA MISMA CUENTA (solo existe con tarjeta grafica).
class Respira
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF;

    static MethodBase Met(string n)
    {
        if (TF == null) return null;
        foreach (MethodInfo m in TF.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }

    static MethodInfo mResp, mPaso;
    static float Resp(float fuerza, float quietud, float fase)
    { return Convert.ToSingle(mResp.Invoke(null, new object[] { fuerza, quietud, fase })); }
    static float Paso(float quietud, bool tocando)
    { return Convert.ToSingle(mPaso.Invoke(null, new object[] { quietud, tocando })); }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes()) if (t.Name == "Form1") TF = t;
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }

        Console.WriteLine("\n  1) LA PARED YA NO RESPIRA\n");
        foreach (string ido in new string[] { "respiraOn", "respiraFuerza", "respiraFase", "respiraDemoHasta", "RespiraDef", "RespiraTope", "RespiraPaso" })
            Check(TF.GetField(ido, TODO) == null, "ya no existe " + ido);
        Check(Met("FuerzaRespira") == null && Met("MuestraRespiracion") == null, "ni la fuerza que se leia del archivo ni la vista previa");
        // El brillo del cielo lo lee el dibujo por CPU, que esta congelado: se queda como campo, pero fijo en 1.
        FieldInfo fResp = TF.GetField("bgRespiro", TODO);
        Check(fResp == null || fResp.IsInitOnly, "el brillo del cielo ya no cambia (bgRespiro, si queda, es de solo lectura)");
        if (fResp != null)
        {
            // Solo lo pone el constructor (a 1): nadie lo cambia despues.
            bool otro = false;
            foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            {
                if (m.Name == ".ctor") continue;
                int l, e; Codigo.UsosDeCampo(m, "bgRespiro", out l, out e);
                if (e > 0) otro = true;
            }
            Check(!otro, "y nadie lo cambia: el cielo con su color de siempre");
        }
        // Con tarjeta grafica los colores del cielo se le pasan al panel desde el reloj: ya no pasan por EscalaBrillo.
        if (TF.GetField("gpuPanel", TODO) != null)
            Check(!Codigo.Llama(Codigo.ConAyudantes(TF, "AnimTimer_Tick"), "EscalaBrillo"),
                  "con tarjeta grafica, el reloj le pasa al panel el cielo tal cual");
        bool escribe = false, lee = false;
        MethodBase txt = Met("TextoConfig"), carga = Met("LoadConfigPrefs");
        if (txt != null) foreach (string t in Codigo.Textos(txt)) if (t.StartsWith("respira")) escribe = true;
        if (carga != null) foreach (string t in Codigo.Textos(carga)) if (t.StartsWith("respira")) lee = true;
        Check(txt != null && !escribe, "el archivo de configuracion ya no lleva respira= ni respira_fuerza=");
        Check(carga != null && !lee, "y si una configuracion vieja los trae, se ignoran");

        Console.WriteLine("\n  2) LA CUENTA, PURA, Y QUIETA CON ALGUIEN TOCANDO\n");
        MethodBase r = Met("Respiracion"), p = Met("PasoQuietud");
        Check(r != null && r.IsStatic, "la respiracion (la de la aurora) es una cuenta aparte (Respiracion)");
        Check(p != null && p.IsStatic, "y el freno, otra (PasoQuietud)");
        if (r == null || p == null) { Console.WriteLine("\n  " + fallos + " FALLO(S)"); return fallos; }
        mResp = (MethodInfo)r; mPaso = (MethodInfo)p;
        bool quieta = true;
        for (int i = 0; i < 64; i++)
            if (Resp(0.20f, 0f, (float)(i * 6.2831853 / 64)) != 1f) quieta = false;
        Check(quieta, "con quietud 0 vale su brillo normal EN TODO EL CICLO, no solo en un punto");
        Check(Resp(0f, 1f, 1.2f) == 1f, "y sin fuerza, tampoco se mueve");
        float alto = Resp(0.20f, 1f, (float)(Math.PI / 2)), bajo = Resp(0.20f, 1f, (float)(-Math.PI / 2));
        Check(Math.Abs(alto - 1.20f) < 1e-4f && Math.Abs(bajo - 0.80f) < 1e-4f, "y sola, respira con la fuerza que se le da (+-20%)");

        Console.WriteLine("\n  3) FRENA DEPRISA, VUELVE DESPACIO\n");
        float q = 1f; int nBaja = 0;
        while (q > 0f && nBaja < 400) { q = Paso(q, true); nBaja++; }
        float q2 = 0f; int nSube = 0;
        while (q2 < 1f && nSube < 400) { q2 = Paso(q2, false); nSube++; }
        Console.WriteLine("     frenar del todo: " + nBaja + " vueltas de reloj; volver: " + nSube);
        Check(q == 0f, "frenando se llega a CERO exacto, no a un pelin de movimiento que no acaba nunca");
        Check(nBaja <= 24, "y se frena en menos de un segundo");
        Check(nSube > nBaja * 2, "volver tarda bastante mas que parar: arrancar no se nota");
        bool acaba = true;
        for (int i = 0; i < 64; i++)
        {
            float qq = 1f;
            for (int k = 0; k < nBaja; k++) qq = Paso(qq, true);
            if (Resp(0.20f, qq, (float)(i * 6.2831853 / 64)) != 1f) acaba = false;
        }
        Check(acaba, "y al frenar acaba en su brillo de siempre, no clavada a media respiracion");
        Check(Codigo.Llama(Codigo.ConAyudantes(TF, "AnimTimer_Tick"), "PasoQuietud"),
              "el reloj de la pantalla sigue llevando la cuenta de si hay alguien tocando");

        Console.WriteLine("\n  4) LA AURORA FRENA POR LA MISMA CUENTA\n");
        if (TF.GetField("auroraFase", TODO) == null)
            Console.WriteLine("     (esta version no lleva aurora: no hay nada que frenar ahi)");
        else
        {
            bool aurora = false;
            foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            {
                int l, e;
                Codigo.UsosDeCampo(m, "auroraFase", out l, out e);
                if (l > 0 && Codigo.Llama(m, "Respiracion")) aurora = true;
            }
            Check(aurora, "la aurora del horizonte respira y frena por la misma cuenta");
        }

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
