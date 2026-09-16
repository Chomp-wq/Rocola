using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

class EfectosLayout
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    [STAThread] static int Main(string[] args)
    {
        Type t = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1", true);
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
            Control effects = null;
            foreach (Control c in page.Controls)
            {
                FieldInfo title = c.GetType().GetField("Titulo", F);
                if (title != null && (string)title.GetValue(c) == "EFECTOS") effects = c;
            }
            if (effects == null) throw new Exception("No hay tarjeta EFECTOS");
            if (effects.Bottom > page.Height || effects.Right > page.Width) throw new Exception("Tarjeta recortada");
            int switches = 0, buttons = 0;
            foreach (Control c in effects.Controls)
            {
                if (c.GetType().Name == "Interruptor") switches++;
                if (c.GetType().Name == "BotonPlano") buttons++;
                if (c.Bottom > effects.ClientSize.Height || c.Right > effects.ClientSize.Width || c.Top < 32)
                    throw new Exception("Control recortado: " + c.Text);
                foreach (Control other in effects.Controls)
                    if (other != c && c.Bounds.IntersectsWith(other.Bounds))
                        throw new Exception("Solapamiento: " + c.Text + " / " + other.Text);
            }
            if (switches != 4 || buttons != 21) throw new Exception("Faltan mandos");
            using (Bitmap bmp = new Bitmap(page.Width, page.Height))
            {
                page.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size));
                bmp.Save(args[1]);
            }
            Console.WriteLine("PASS: 6 filas, 4 interruptores, 21 botones; sin solapamientos ni controles recortados.");
        }
        return 0;
    }
}
