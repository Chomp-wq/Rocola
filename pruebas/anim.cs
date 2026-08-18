using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// Transiciones nuevas: el hojeo de paginas y el aviso flotante. Se comprueban las CURVAS (que empiecen
// donde deben, lleguen donde deben, no se pasen y no den saltos) y que las texturas se REUSEN.
class Anim
{
    static Type TF, TAV;
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }
    static MethodInfo HOJEO, TOAST;

    // saca el .Sel.Has de un GpuCubeState (estructura anidada)
    static bool FldHas(object cubeState)
    {
        object sel = cubeState.GetType().GetField("Sel").GetValue(cubeState);
        return (bool)sel.GetType().GetField("Has").GetValue(sel);
    }

    static float Hojeo(int dt, out float a)
    { object[] p = { dt, 0f }; float r = (float)HOJEO.Invoke(null, p); a = (float)p[1]; return r; }
    static void Toast(int dt, out float a, out float dy)
    { object[] p = { dt, 0f, 0f }; TOAST.Invoke(null, p); a = (float)p[1]; dy = (float)p[2]; }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)) if (t.Name == "AlbumView") TAV = t;
        if (TAV == null) { Console.WriteLine("no encontre AlbumView"); return 1; }
        HOJEO = TAV.GetMethod("CurvaHojeo", BindingFlags.Static | BindingFlags.NonPublic);
        TOAST = TF.GetMethod("CurvaToast", BindingFlags.Static | BindingFlags.NonPublic);
        if (HOJEO == null || TOAST == null) { Console.WriteLine("faltan las curvas"); return 1; }
        int slideMs = (int)TAV.GetField("SlideMs", BindingFlags.Static | BindingFlags.Public).GetValue(null);
        int toastMs = (int)TF.GetField("ToastMs", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        // ---------- HOJEO ----------
        float a0, a1, aMid;
        float d0 = Hojeo(0, out a0), dMid = Hojeo(slideMs / 2, out aMid), dEnd = Hojeo(slideMs, out a1);
        Console.WriteLine(string.Format("\n  HOJEO ({0} ms): falta {1:0.00} al empezar -> {2:0.00} a la mitad -> {3:0.00} al final",
                                        slideMs, d0, dMid, dEnd));
        Check(Math.Abs(d0 - 1f) < 0.001f, "arranca desplazada una pantalla entera (entra desde el borde)");
        Check(Math.Abs(dEnd) < 0.001f, "termina EXACTAMENTE en su sitio (no queda torcida)");
        Check(dMid < 0.5f, "frena al llegar en vez de ir a velocidad pareja (se siente natural)");
        Check(a0 <= 0.01f && a1 >= 0.999f, "entra desvaneciendose: empieza invisible y acaba opaca");

        // monotona y sin saltos: ningun cuadro puede dar un brinco
        float prev = 2f, maxSalto = 0f; bool mono = true;
        for (int dt = 0; dt <= slideMs; dt++)
        {
            float aa; float d = Hojeo(dt, out aa);
            if (d > prev + 1e-5f) mono = false;
            if (dt > 0 && Math.Abs(prev - d) > maxSalto) maxSalto = Math.Abs(prev - d);
            prev = d;
        }
        Console.WriteLine(string.Format("  salto maximo entre dos milisegundos seguidos: {0:0.0000} de pantalla", maxSalto));
        Check(mono, "siempre avanza hacia adelante, nunca retrocede");
        Check(maxSalto < 0.02f, "no da tirones: ningun paso brusco");
        float aFuera; Check(Hojeo(slideMs + 500, out aFuera) == 0f, "pasado su tiempo se queda quieta (no sigue moviendose)");
        Check(Hojeo(-10, out aFuera) == 0f, "un reloj que va para atras no la descoloca");

        // ---------- HOJEO: mientras pasa la pagina no hay NADA seleccionado ----------
        // La rejilla entera se desliza, pero el cubo que gira y el resalte se dibujan en su sitio fijo. Si se
        // pintaran durante el deslizamiento se verian clavados y girando mientras todo lo demas pasa de largo.
        {
            object av = Activator.CreateInstance(TAV, true);
            ((Control)av).SetBounds(0, 0, 900, 500);
            Type TAlb = null;
            foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)) if (t.Name == "Album") TAlb = t;
            Type TL = typeof(List<>).MakeGenericType(TAlb);
            object page = Activator.CreateInstance(TL);
            MethodInfo add = TL.GetMethod("Add");
            for (int i = 0; i < 6; i++)
            {
                object a = Activator.CreateInstance(TAlb, true);
                TAlb.GetField("Folder").SetValue(a, "d" + i);
                TAlb.GetField("Name").SetValue(a, "Disco " + i);
                TAlb.GetField("Artist").SetValue(a, "Artista");
                add.Invoke(page, new object[] { a });
            }
            TAV.GetField("Page").SetValue(av, page);
            TAV.GetField("Selected").SetValue(av, 2);
            MethodInfo mCubo = TAV.GetMethod("BuildCubeState", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo mCont = TAV.GetMethod("ContourScreenRect", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo mSlide = TAV.GetMethod("StartSlide", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo mStep = TAV.GetMethod("StepSpin", BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo pSlid = TAV.GetProperty("Sliding", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo fAng = TAV.GetField("SelAngle", BindingFlags.Instance | BindingFlags.Public);
            // hornea el sprite del resalte (si no, ContourScreenRect devuelve vacio por otro motivo)
            object sprite = TAV.GetProperty("ContourSpriteBmp", BindingFlags.Instance | BindingFlags.Public).GetValue(av, null);

            bool selQuieto = ((bool)FldHas(mCubo.Invoke(av, null)));
            Rectangle rQuieto = (Rectangle)mCont.Invoke(av, null);
            Check(selQuieto, "quieto en una pagina: SI hay cubo seleccionado (girando)");
            Check(sprite != null && rQuieto.Width > 0, "y SI se dibuja el resalte de la casilla");

            mSlide.Invoke(av, new object[] { 1, 900 });
            Check((bool)pSlid.GetValue(av, null), "al pasar de pagina arranca el deslizamiento");
            Check(!((bool)FldHas(mCubo.Invoke(av, null))), "MIENTRAS la pagina entra no hay cubo seleccionado (no se ve girar clavado)");
            Check(((Rectangle)mCont.Invoke(av, null)).Width == 0, "ni resalte de casilla: la pagina entra limpia");

            fAng.SetValue(av, 0.0);
            for (int i = 0; i < 5; i++) mStep.Invoke(av, null);
            Check(Math.Abs((double)fAng.GetValue(av)) < 1e-9, "y el giro tampoco avanza: el cubo aparece PLANO, no a medio girar");

            System.Threading.Thread.Sleep(slideMs + 40);
            Check(!((bool)pSlid.GetValue(av, null)), "pasado su tiempo el deslizamiento termina solo");
            Check((bool)FldHas(mCubo.Invoke(av, null)), "y en cuanto termina vuelve a haber cubo seleccionado");
            Check(((Rectangle)mCont.Invoke(av, null)).Width > 0, "y vuelve el resalte de la casilla");
            mStep.Invoke(av, null);
            Check(Math.Abs((double)fAng.GetValue(av)) > 1e-9, "y a partir de ahi si empieza a girar");
        }

        // ---------- AVISO ----------
        float aa0, dy0, aaIn, dyIn, aaMid, dyMid, aaOut, dyOut, aaEnd, dyEnd;
        Toast(0, out aa0, out dy0);
        Toast(190, out aaIn, out dyIn);
        Toast(toastMs / 2, out aaMid, out dyMid);
        Toast(toastMs - 1, out aaOut, out dyOut);
        Toast(toastMs, out aaEnd, out dyEnd);
        Console.WriteLine(string.Format("\n  AVISO ({0} ms): opacidad {1:0.00} -> {2:0.00} (entrada lista) -> {3:0.00} (a la mitad) -> {4:0.00} (al irse)",
                                        toastMs, aa0, aaIn, aaMid, aaOut));
        Console.WriteLine(string.Format("                 altura   {0,5:0.0} -> {1,5:0.0} -> {2,5:0.0} -> {3,5:0.0}  (0 = su sitio)", dy0, dyIn, dyMid, dyOut));
        Check(aa0 <= 0.01f, "empieza invisible");
        Check(dy0 < -20f, "y arriba de su sitio: baja deslizandose, no aparece de golpe");
        Check(aaIn > 0.99f && Math.Abs(dyIn) < 0.5f, "a los ~190 ms ya llego a su sitio y esta opaco del todo");
        Check(aaMid > 0.99f && Math.Abs(dyMid) < 0.001f, "en medio se queda quieto y solido (se puede leer)");
        Check(aaOut < 0.02f && dyOut < -15f, "al final se va desvaneciendo y subiendo");
        Check(aaEnd == 0f, "cumplido su tiempo, desaparece");

        prev = -1f; bool subeBien = true; maxSalto = 0f;
        for (int dt = 0; dt <= 190; dt++)
        {
            float x, y; Toast(dt, out x, out y);
            if (x < prev - 1e-5f) subeBien = false;
            if (dt > 0 && Math.Abs(x - prev) > maxSalto) maxSalto = Math.Abs(x - prev);
            prev = x;
        }
        Console.WriteLine(string.Format("  salto maximo de opacidad entre milisegundos seguidos: {0:0.000}", maxSalto));
        Check(subeBien, "la aparicion nunca parpadea (la opacidad solo sube)");
        Check(maxSalto < 0.03f, "aparece suave, sin brincos");

        // ---------- REUSO DE TEXTURAS ----------
        MethodInfo ensure = null;
        Type TP = null;
        foreach (Type t in asm.GetType("Rocola.GpuPanel", false) != null ? new Type[] { asm.GetType("Rocola.GpuPanel") } : new Type[0]) TP = t;
        if (TP == null) foreach (Type t in asm.GetTypes()) if (t.Name == "GpuPanel") TP = t;
        if (TP != null) ensure = TP.GetMethod("EnsureCover", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(ensure != null && ensure.GetParameters().Length == 7,
              "las animaciones REUSAN su textura en vez de crear una nueva cada vez (era el tiron de la primera vez)");
        MethodInfo pre = (TP != null) ? TP.GetMethod("PrecalientaAnimaciones", BindingFlags.Instance | BindingFlags.Public) : null;
        Check(pre != null, "existe el pre-calentado: las texturas se crean al arrancar, no en la primera animacion");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
