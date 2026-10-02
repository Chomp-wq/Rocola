using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

class EfectosLayout
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Control Effects(Control root)
    {
        foreach (Control c in root.Controls) {
            if (c.Name == "Efectos") return c;
            Control result = Effects(c); if (result != null) return result;
        }
        return null;
    }
    [STAThread] static int Main(string[] args)
    {
        Type t = Assembly.LoadFrom((args.Length > 0 ? args[0] : "rocola.exe")).GetType("Rocola.Form1", true);
        object form = FormatterServices.GetUninitializedObject(t);
        foreach (FieldInfo f in t.GetFields(F))
            if (!f.IsStatic && f.FieldType.Namespace != null && f.FieldType.Namespace.StartsWith("System.Collections") &&
                f.FieldType.GetConstructor(Type.EmptyTypes) != null)
                f.SetValue(form, Activator.CreateInstance(f.FieldType));
        using (Font normal = new Font("Segoe UI", 9, FontStyle.Bold))
        using (Font small = new Font("Segoe UI", 8))
        using (Panel page = (Panel)t.GetMethod("PaginaImagen", F).Invoke(form,
            new object[] {normal, normal, small, normal, Color.White, Color.Gray, Color.Gray}))
        {
            Control effects = Effects(page);
            if (effects == null) throw new Exception("No hay tarjeta EFECTOS");
            if (effects.Bottom > effects.Parent.Height || effects.Right > effects.Parent.Width
                || effects.Parent.Bottom > page.Height || effects.Parent.Right > page.Width) throw new Exception("Tarjeta recortada");
            int switches = 0, buttons = 0;
            foreach (Control c in effects.Controls)
            {
                if (c.GetType().Name == "Interruptor") switches++;
                if (c.GetType().Name == "BotonPlano") buttons++;
                bool tab = c.AccessibleName != null && c.AccessibleName.StartsWith("Imagen: ");
                if (c.Bottom > effects.ClientSize.Height || c.Right > effects.ClientSize.Width || c.Top < (tab ? 0 : 32))
                    throw new Exception("Control recortado: " + c.Text);
                foreach (Control other in effects.Controls)
                    if (other != c && c.Bounds.IntersectsWith(other.Bounds))
                        throw new Exception("Solapamiento: " + c.Text + " / " + other.Text);
            }
            if (switches != 5 || buttons != 23) throw new Exception("Faltan mandos");
            using (Bitmap bmp = new Bitmap(page.Width, page.Height))
            {
                page.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size));
                bmp.Save((args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "efectoslayout.png")));
            }
            Console.WriteLine("  OK    6 filas, 5 interruptores visuales, 21 mandos y 2 hojas; sin solapamientos ni controles recortados.");
        }
        return 0;
    }
}
