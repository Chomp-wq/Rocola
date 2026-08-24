using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

// LA ROCOLA ENCIENDE, SE DIBUJA ENTERA, Y NO QUEDA NI UN HUECO NEGRO.
//
// Todas las demas baterias miran la rocola por dentro: le piden una pieza suelta, la hacen trabajar y le
// cuentan los pixeles. Ninguna la ENCENDIA. Y al encenderla por primera vez en Linux aparecio algo que
// ninguna podia ver: media pantalla en rectangulos negros.
//
// El motivo es que en Windows, a un control se le pone el fondo "transparente" y el sistema deja ver lo que
// hay detras -aqui, el cielo estrellado-. En Linux eso NO pasa: el fondo transparente se pinta de NEGRO. La
// barra de ayuda, el letrero de la seccion, las pastillas de creditos y de cola, la barra de avance y varias
// etiquetas salian como agujeros negros encima del cielo.
//
// Asi que esta bateria hace lo que hace un cliente: enciende la rocola, espera a que se dibuje, y MIRA LA
// PANTALLA. El cielo de la rocola nunca es negro puro -es un degradado azul oscuro con estrellas-, asi que
// cualquier cuadro de negro perfecto delata a un control que no pinto su fondo. Se dice cuantos hay y donde,
// para poder ir a arreglarlos.
class Arranca
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const int BLOQUE = 16;   // un cuadro de 16x16 completamente negro no puede ser cielo

    static string Prepara(string exe)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rocola_arranca");
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        Directory.CreateDirectory(dir);
        File.Copy(exe, Path.Combine(dir, "rocola.exe"));
        foreach (string d in Directory.GetFiles(".", "*.dll")) File.Copy(d, Path.Combine(dir, Path.GetFileName(d)), true);

        // Una biblioteca chiquita pero de verdad: dos discos con canciones, para que haya pared de discos,
        // barra de letras con cuentas, y algo que ensenar en la barra de estado.
        string[] discos = { "Los Tigres del Norte", "Banda El Recodo" };
        string[] wavs = Directory.Exists("wav") ? Directory.GetFiles("wav", "*.wav") : new string[0];
        Array.Sort(wavs);
        for (int i = 0; i < discos.Length; i++)
        {
            string d = Path.Combine(Path.Combine(dir, "musica"), discos[i]);
            Directory.CreateDirectory(d);
            for (int k = 0; k < 2 && k < wavs.Length; k++)
                File.Copy(wavs[k], Path.Combine(d, "Cancion " + (k + 1) + ".wav"), true);
        }
        // Los botones ya configurados: si no, la rocola arranca con el asistente y no se ve la pantalla.
        File.WriteAllText(Path.Combine(dir, "rocola_teclas.txt"),
            "arriba=38\r\nabajo=40\r\nizquierda=37\r\nderecha=39\r\nponer=13\r\nregresar=8\r\nmoneda=32\r\nsaltar=9\r\n");
        return dir;
    }

    static Bitmap Foto(int an, int al)
    {
        Bitmap b = new Bitmap(an, al, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(0, 0, 0, 0, new Size(an, al));
        return b;
    }

    // DONDE ESTA LA VENTANA DE LA ROCOLA. La pantalla de pruebas puede ser mas grande que la ventana, y lo
    // que sobra alrededor es escritorio negro: si se contara, saldria un "agujero" gigante que no es de la
    // rocola. Se busca el recuadro que ocupa lo pintado y todo lo demas se mira SOLO ahi dentro.
    static Rectangle Ventana(Bitmap b)
    {
        int x0 = b.Width, y0 = b.Height, x1 = -1, y1 = -1;
        for (int y = 0; y < b.Height; y += 2)
            for (int x = 0; x < b.Width; x += 2)
            {
                Color c = b.GetPixel(x, y);
                if (c.R + c.G + c.B <= 12) continue;
                if (x < x0) x0 = x; if (x > x1) x1 = x;
                if (y < y0) y0 = y; if (y > y1) y1 = y;
            }
        if (x1 < 0) return Rectangle.Empty;
        return new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    // Que parte de la ventana es cielo de verdad. Sirve para saber si ya termino de dibujarse.
    static double Pintado(Bitmap b, Rectangle v)
    {
        if (v.Width < 8 || v.Height < 8) return 0;
        int hay = 0, tot = 0;
        for (int y = v.Top; y < v.Bottom; y += 8)
            for (int x = v.Left; x < v.Right; x += 8)
            { tot++; Color c = b.GetPixel(x, y); if (c.R + c.G + c.B > 12) hay++; }
        return (tot == 0) ? 0 : (double)hay / tot;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        exe = Path.GetFullPath(exe);
        Rectangle pant = Screen.PrimaryScreen.Bounds;

        Console.WriteLine("\n  1) LA ROCOLA ENCIENDE\n");
        string dir = Prepara(exe);
        Process p = null;
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("mono", "\"" + Path.Combine(dir, "rocola.exe") + "\"");
            psi.WorkingDirectory = dir;
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            p = Process.Start(psi);
        }
        catch (Exception ex) { Console.WriteLine("  FALLO  no se pudo lanzar la rocola: " + ex.Message); return 1; }

        // Se le da tiempo a leer la biblioteca y dibujarse. Se mira la pantalla cada tanto y se sigue en
        // cuanto este pintada, para no esperar de mas.
        Bitmap foto = null;
        double pintado = 0;
        Rectangle ven = Rectangle.Empty;
        for (int i = 0; i < 40; i++)
        {
            System.Threading.Thread.Sleep(700);
            if (p.HasExited) break;
            if (foto != null) foto.Dispose();
            foto = Foto(pant.Width, pant.Height);
            ven = Ventana(foto);
            pintado = Pintado(foto, ven);
            if (pintado > 0.85 && i >= 5) break;
        }

        bool viva = !p.HasExited;
        if (!viva)
        {
            string sal = "";
            try { sal = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); } catch { }
            Console.WriteLine("     la rocola se murio. Lo que dijo:");
            foreach (string l in sal.Replace("\r", "").Split('\n')) if (l.Trim().Length > 0) Console.WriteLine("       " + l);
        }
        Check(viva, "la rocola arranca y sigue en pie");
        Console.WriteLine("     la ventana mide " + ven.Width + "x" + ven.Height
                          + " y esta pintada al " + (pintado * 100).ToString("0") + "%");
        Check(ven.Width >= 640 && ven.Height >= 400, "y se dibuja: la ventana esta ahi y ocupa la pantalla");

        string errores = Path.Combine(dir, "rocola_errores.txt");
        bool hayErrores = File.Exists(errores) && new FileInfo(errores).Length > 0;
        if (hayErrores)
            foreach (string l in File.ReadAllLines(errores)) { Console.WriteLine("       " + l); break; }
        Check(!hayErrores, "y no apunta ni un error al arrancar");

        // =========================================================================================
        Console.WriteLine("\n  2) Y NO QUEDA NI UN HUECO NEGRO\n");
        List<Rectangle> huecos = new List<Rectangle>();
        int negros = 0;
        if (foto != null && viva)
        {
            bool[,] negro = new bool[foto.Width / BLOQUE + 1, foto.Height / BLOQUE + 1];
            for (int by = ven.Top; by + BLOQUE <= ven.Bottom; by += BLOQUE)
                for (int bx = ven.Left; bx + BLOQUE <= ven.Right; bx += BLOQUE)
                {
                    bool todo = true;
                    for (int y = by; y < by + BLOQUE && todo; y++)
                        for (int x = bx; x < bx + BLOQUE && todo; x++)
                        { Color c = foto.GetPixel(x, y); if (c.R != 0 || c.G != 0 || c.B != 0) todo = false; }
                    if (todo) { negro[bx / BLOQUE, by / BLOQUE] = true; negros++; }
                }
            // Se juntan los cuadros pegados en manchas, para decir DONDE esta cada agujero y no cuantos
            // cuadritos hay: asi se puede ir directo al control que lo deja.
            int an = ven.Right / BLOQUE, al = ven.Bottom / BLOQUE;
            bool[,] visto = new bool[an + 1, al + 1];
            for (int j = 0; j < al; j++)
                for (int i = 0; i < an; i++)
                {
                    if (!negro[i, j] || visto[i, j]) continue;
                    int x0 = i, y0 = j, x1 = i, y1 = j, n = 0;
                    Stack<Point> pila = new Stack<Point>();
                    pila.Push(new Point(i, j)); visto[i, j] = true;
                    while (pila.Count > 0)
                    {
                        Point q = pila.Pop(); n++;
                        if (q.X < x0) x0 = q.X; if (q.X > x1) x1 = q.X;
                        if (q.Y < y0) y0 = q.Y; if (q.Y > y1) y1 = q.Y;
                        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = q.X + dx[k], ny = q.Y + dy[k];
                            if (nx < 0 || ny < 0 || nx >= an || ny >= al) continue;
                            if (!negro[nx, ny] || visto[nx, ny]) continue;
                            visto[nx, ny] = true; pila.Push(new Point(nx, ny));
                        }
                    }
                    if (n >= 4) huecos.Add(new Rectangle(x0 * BLOQUE, y0 * BLOQUE, (x1 - x0 + 1) * BLOQUE, (y1 - y0 + 1) * BLOQUE));
                }
            huecos.Sort(delegate(Rectangle a, Rectangle b) { return (b.Width * b.Height).CompareTo(a.Width * a.Height); });
            try { foto.Save(Path.Combine(dir, "pantalla.png"), ImageFormat.Png); } catch { }
        }
        Console.WriteLine("     cuadros de negro puro dentro de la ventana: " + negros + " de "
                          + ((ven.Width / BLOQUE) * (ven.Height / BLOQUE)) + "   (" + huecos.Count + " agujeros)");
        for (int i = 0; i < huecos.Count && i < 8; i++)
        {
            Rectangle r = huecos[i];
            Console.WriteLine("       agujero de " + r.Width + "x" + r.Height + " en (" + r.X + "," + r.Y + ")");
        }
        Check(viva && huecos.Count == 0, "no queda ni un agujero negro: todos los controles pintan su cielo");

        try { if (!p.HasExited) p.Kill(); } catch { }
        if (foto != null) foto.Dispose();
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}
