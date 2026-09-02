using System;
using System.Reflection;

// CUANDO NO HAY NADIE, QUE TRABAJE.
//
// El pre-analisis -descomprimir cada cancion entera para saber su volumen, su duracion y su huella
// tonal- usaba UN SOLO CARRIL siempre, salvo en el "repaso a fondo" que se enciende a mano. En una
// maquina de ocho nucleos eso es un nucleo de ocho, tambien a las cuatro de la manana con el bar
// cerrado y la pantalla en modo atraccion.
//
// A los 40 segundos sin que nadie toque un boton la rocola entra en atraccion: el cubo gigante con las
// portadas. Ahi no hay nadie eligiendo, no hay pared de discos que dibujar -solo un cubo- y muchas
// veces tampoco suena nada. Ese es el hueco: mientras esta sola, se le sueltan los carriles.
//
// LO QUE MAS IMPORTA DE TODO ESTO NO ES QUE CORRA MAS. Es QUE FRENE. Si el freno no volviera a poner al
// primer boton, la rocola se pondria lenta justo cuando hay gente delante, que es el peor fallo que
// podria salir de aqui: seria peor que no haber tocado nada. Por eso esta bateria mira los dos lados, y
// el del freno con mas cuidado que el otro.
//
// LO QUE VIGILA:
//
//   1) QUE LA DECISION SE PUEDA COMPROBAR: una cuenta pura a la que se le dan los cuatro datos.
//   2) QUE CON ALGUIEN USANDOLA SEA UN CARRIL. Siempre. En cualquier maquina.
//   3) QUE SOLA Y CON NUCLEOS DE SOBRA use mas de uno.
//   4) QUE NUNCA SE COMA LA MAQUINA: siempre deja al menos un nucleo libre para la pantalla y la
//      musica, y no pasa de cuatro por mucha maquina que haya.
//   5) QUE CON DISCO LENTO NO SUELTE NADA: ahi el cuello es el cabezal, y varios carriles solo lo
//      hacen ir y venir.
//   6) QUE LA ROCOLA SE ENTERE al entrar y al salir de la atraccion, y que no se reinicie sola por
//      gusto cuando no ha cambiado nada.
class Sola
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF;
    static MethodInfo mCarriles;

    static MethodBase Met(string n)
    {
        if (TF == null) return null;
        foreach (MethodInfo m in TF.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }
    // El quinto dato -si quedan miniaturas de portada por hacer- llego con la bateria 'duerme': mientras
    // queden, mandan ellas y el pre-analisis se queda en un carril. Aqui se le dice que NO quedan, que es
    // el caso que esta bateria mira; el otro lo comprueba 'duerme'.
    static int Carriles(bool turbo, bool sola, bool discoLento, int nucleos)
    {
        return Convert.ToInt32(mCarriles.Invoke(null, new object[] { turbo, sola, discoLento, nucleos, false }));
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes()) if (t.Name == "Form1") TF = t;
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }

        Console.WriteLine("\n  1) LA DECISION, Y QUE SE PUEDA COMPROBAR\n");
        MethodBase c = Met("CarrilesPara"), sola = Met("RocolaSola"), revisa = Met("RevisaCarriles");
        Check(c != null, "cuantos carriles usar es una cuenta aparte (CarrilesPara)");
        Check(sola != null, "y hay un solo sitio que dice si la rocola esta sola (RocolaSola)");
        Check(revisa != null, "y otro que mira si hay que cambiar (RevisaCarriles)");
        if (c == null) { Console.WriteLine("\n  " + fallos + " FALLO(S)"); return Math.Max(fallos, 1); }
        Check(c.IsStatic, "es pura: se le dan los cuatro datos y contesta, sin encender nada");
        mCarriles = (MethodInfo)c;

        Console.WriteLine("\n  2) CON ALGUIEN USANDOLA, UN CARRIL. SIEMPRE.\n");
        // Esta es LA comprobacion. Si esto se rompe, la rocola se pone lenta con gente delante.
        bool siempreUno = true;
        for (int n = 1; n <= 64; n++)
            for (int d = 0; d < 2; d++)
                if (Carriles(false, false, d == 1, n) != 1) siempreUno = false;
        Check(siempreUno, "con alguien usandola, un carril: en cualquier maquina, de 1 a 64 nucleos");

        Console.WriteLine("\n  3) SOLA Y CON MAQUINA, MAS DE UNO\n");
        Console.WriteLine("     sola, 8 nucleos, disco bueno   ->  " + Carriles(false, true, false, 8) + " carriles");
        Console.WriteLine("     sola, 4 nucleos, disco bueno   ->  " + Carriles(false, true, false, 4) + " carriles");
        Console.WriteLine("     sola, 2 nucleos, disco bueno   ->  " + Carriles(false, true, false, 2) + " carriles");
        Check(Carriles(false, true, false, 8) > 1, "sola en una maquina de ocho nucleos, mas de un carril");
        Check(Carriles(false, true, false, 2) == 1, "y en la de dos nucleos del bar se queda en uno: no hay que repartir");
        // El repaso a fondo sigue como estaba: se enciende a mano para que la maquina se dedique a esto.
        Check(Carriles(true, false, false, 8) > 1, "el repaso a fondo sigue usando varios, como siempre");

        Console.WriteLine("\n  4) PERO NUNCA SE COME LA MAQUINA\n");
        bool dejaSitio = true, tope = true;
        for (int n = 1; n <= 64; n++)
        {
            int k = Carriles(false, true, false, n);
            if (k > n - 1 && n > 1) dejaSitio = false;
            if (k > 4) tope = false;
            if (k < 1) dejaSitio = false;
        }
        Console.WriteLine("     con 64 nucleos pide " + Carriles(false, true, false, 64) + " carriles");
        Check(dejaSitio, "siempre deja al menos un nucleo libre para la pantalla y la musica");
        Check(tope, "y no pasa de cuatro: mas no gana nada y calienta la maquina del bar para nada");

        Console.WriteLine("\n  5) Y CON DISCO LENTO, NI UNO MAS\n");
        Console.WriteLine("     sola, 8 nucleos, disco lento   ->  " + Carriles(false, true, true, 8) + " carriles");
        Check(Carriles(false, true, true, 8) == 1, "con disco lento el cuello es el cabezal: varios carriles solo lo hacen ir y venir");
        Check(Carriles(true, true, true, 16) == 1, "ni siquiera en repaso a fondo");

        Console.WriteLine("\n  6) LA ROCOLA SE ENTERA\n");
        MethodBase arranca = Met("StartPreScan"), entra = Met("EnterAttract"), sale = Met("ExitAttract");
        Check(arranca != null && Codigo.Llama(Codigo.ConAyudantes(TF, "StartPreScan"), "CarrilesPara"),
              "el pre-analisis pregunta cuantos carriles le tocan");
        Check(entra != null && Codigo.Llama(entra, "RevisaCarriles"), "al entrar en atraccion se mira si tocan otros");
        Check(sale != null && Codigo.Llama(sale, "RevisaCarriles"), "y al salir tambien: ahi es donde se frena");
        if (revisa != null)
        {
            Check(Codigo.Llama(revisa, "CarrilesPara"), "y la mirada usa la misma cuenta, no una copia");
            // Sin esto, cualquiera que pase por delante cada 40 segundos reiniciaria el pre-analisis dos
            // veces por paseo, y no avanzaria nunca.
            int lee, esc;
            Codigo.UsosDeCampo(revisa, "preScanCarriles", out lee, out esc);
            Check(lee > 0, "y solo se reinicia si de verdad tocan OTROS: si no, no se toca nada");
        }
        if (sola != null)
        {
            int lee, esc;
            Codigo.UsosDeCampo(sola, "attractOn", out lee, out esc);
            Check(lee > 0, "\"sola\" quiere decir en modo atraccion...");
            Codigo.UsosDeCampo(sola, "nowPlaying", out lee, out esc);
            Check(lee > 0, "...sin nada sonando...");
            Codigo.UsosDeCampo(sola, "videoOn", out lee, out esc);
            Check(lee > 0, "...y sin ningun video puesto");
        }
        // Y el descanso entre canciones: sola no hace falta descansar de nadie.
        Check(Codigo.Llama(Codigo.ConAyudantes(TF, "StartPreScan"), "RocolaSola"),
              "y estando sola tampoco descansa entre cancion y cancion");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
