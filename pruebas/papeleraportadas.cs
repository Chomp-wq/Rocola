using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;

// Solo mapas sinteticos en RAM: sin ventana, dispositivo GPU, musica ni datos del usuario.
class PapeleraPortadas
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type form;
    static MethodInfo drain, limited;
    static int checks, failures;
    static readonly List<Bitmap> images = new List<Bitmap>();
    static void Check(bool good, string text)
    { checks++; if (!good) failures++; Console.WriteLine((good ? "OK " : "FALLO ") + text); }
    static Bitmap NewImage()
    { Bitmap image = new Bitmap(8, 8); images.Add(image); return image; }
    static bool Alive(Image image)
    { try { return image.Width == 8; } catch (ArgumentException) { return false; } catch (InvalidOperationException) { return false; } }
    static object NewForm() { return Codigo.FormSinEncender(form); }
    static List<Image> Trash(object instance) { return (List<Image>)form.GetField("coverTrash", B).GetValue(instance); }
    static object CoverLock(object instance) { return form.GetField("coverLock", B).GetValue(instance); }
    static int Drain(object instance, int maximum, long ticks)
    { return (int)limited.Invoke(instance, new object[] { maximum, ticks }); }
    static object Owner(object instance, string name)
    {
        FieldInfo field = form.GetField(name, B);
        object owner = FormatterServices.GetUninitializedObject(field.FieldType);
        field.SetValue(instance, owner); return owner;
    }
    static void Set(object target, string name, object value)
    { target.GetType().GetField(name, B).SetValue(target, value); }

    static void WiringAndNegative()
    {
        Check(drain != null && limited != null, "ruta GPU acotada independiente de la limpieza CPU");
        object instance = NewForm(); List<Image> trash = Trash(instance);
        for (int i = 0; i < 257; i++) trash.Add(NewImage());
        drain.Invoke(instance, null);
        Check(trash.Count >= 225 && trash.Count < 257, "un tick retira entre 1 y 32, no las 257 portadas juntas");
        if (limited == null) return;
        Check(Codigo.Llama(drain, "DrainCoverTrashAcotado"), "temporizador usa la ruta acotada");
        bool copy = false;
        foreach (Codigo.Paso step in Codigo.Pasos(limited))
            if (step.Op == 0x73)
                try
                {
                    MethodBase constructor = limited.Module.ResolveMethod(step.Numero);
                    if (constructor.DeclaringType == typeof(List<Image>)) copy = true;
                }
                catch { }
        Check(!copy && !Codigo.Llama(limited, "ToArray") && !Codigo.Llama(limited, "Clear"),
            "sin copia completa ni vaciado de toda la papelera");
        int reads, writes;
        Codigo.UsosDeCampo(limited, "coverLock", out reads, out writes);
        Check(reads > 0 && Codigo.Llama(limited, "Enter") && Codigo.Llama(limited, "Exit"),
            "extraccion conserva el mismo coverLock");
        Check(Codigo.Llama(limited, "GetTimestamp") && Codigo.Llama(limited, "RemoveAt"),
            "limite temporal y retirada por extremo, sin desplazar el resto");
        Type search = form.GetField("searchOverlay", B).FieldType;
        MethodInfo retained = search.GetMethod("RetienePortada", B);
        Check(retained != null && !Codigo.Llama(retained, "GetPreviewCube") && !Codigo.Llama(retained, "ComputePreview")
            && !Codigo.Llama(retained, "EnsureArtistCovers") && !Codigo.Llama(retained, "Portada"),
            "proteccion del buscador solo consulta referencias retenidas; no carga ni modifica");
    }

    static void BudgetsAndRemaining()
    {
        object instance = NewForm(); List<Image> trash = Trash(instance);
        List<Image> original = trash;
        List<Bitmap> sample = new List<Bitmap>();
        for (int i = 0; i < 257; i++) { Bitmap image = NewImage(); sample.Add(image); trash.Add(image); }
        Check(Drain(instance, 0, long.MaxValue) == 0 && trash.Count == 257, "tope cero no consume trabajo");
        Check(Drain(instance, 7, long.MaxValue) == 7 && trash.Count == 250, "tope por cantidad exacto en lote grande");
        bool exact = true;
        for (int i = 0; i < sample.Count; i++) if (Alive(sample[i]) != (i < 250)) exact = false;
        Check(exact && object.ReferenceEquals(original, Trash(instance)), "solo se liberan las ultimas siete; lista y resto intactos");
        Check(Drain(instance, 32, 0) == 1 && trash.Count == 249, "presupuesto agotado detiene tras una imagen y permite progresar");
        Bitmap inserted = NewImage(); lock (CoverLock(instance)) trash.Add(inserted);
        Check(Drain(instance, 1, long.MaxValue) == 1 && !Alive(inserted) && trash.Count == 249,
            "una insercion posterior se retira sin perder el resto pendiente");
        int ticks = 0; bool bounded = true;
        while (trash.Count > 0 && ticks < 300)
        {
            int before = trash.Count; drain.Invoke(instance, null); int removed = before - trash.Count;
            if (removed < 1 || removed > 32) bounded = false;
            ticks++;
        }
        Check(bounded && trash.Count == 0 && ticks >= 8, "vaciado progresivo en ticks acotados hasta terminar");
        exact = true; foreach (Bitmap image in sample) if (Alive(image)) exact = false;
        Check(exact && !Alive(inserted), "todas las expulsadas no retenidas se liberan realmente");
        Check(Drain(instance, 32, long.MaxValue) == 0, "papelera vacia termina sin trabajo");
        trash.Add(null); trash.Add(NewImage());
        Check(Drain(instance, 32, long.MaxValue) == 2 && trash.Count == 0, "null y lote pequeno no bloquean la limpieza");
    }

    static void PinnedOwners()
    {
        object instance = NewForm(); List<Image> trash = Trash(instance);
        Bitmap[] pinned = new Bitmap[7]; for (int i = 0; i < pinned.Length; i++) pinned[i] = NewImage();
        object mini = Owner(instance, "miniCube"); Set(mini, "Cover", pinned[0]); Set(mini, "relNueva", pinned[1]);
        object disc = Owner(instance, "cubeBox"); Set(disc, "Cover", pinned[2]); Set(disc, "relNueva", pinned[3]);
        Set(instance, "transImg", pinned[4]);
        object queue = Owner(instance, "pastCola"); Set(queue, "Portadas", new Image[] { null, pinned[5] });
        object search = Owner(instance, "searchOverlay");
        List<Image> artists = new List<Image>(); artists.Add(pinned[6]); Set(search, "artistCovers", artists);
        foreach (Bitmap image in pinned) trash.Add(image);
        Bitmap loose = NewImage(); trash.Add(loose);
        Check(Drain(instance, 32, long.MaxValue) == 8 && trash.Count == 0,
            "portadas retenidas salen de papelera sin crear otra lista creciente");
        string[] owners = { "mini-cubo", "relevo mini-cubo", "cubo disco", "relevo disco", "transicion disco", "cola", "artista buscador" };
        for (int i = 0; i < pinned.Length; i++) Check(Alive(pinned[i]), "no Dispose mientras la usa: " + owners[i]);
        Check(!Alive(loose), "una portada no retenida junto a las usadas si se libera");
        Check(artists.Count == 1 && object.ReferenceEquals(artists[0], pinned[6]), "consultar buscador no modifica su lista de portadas");
    }

    static void SameLockAndInsertions()
    {
        object instance = NewForm(); List<Image> trash = Trash(instance); object gate = CoverLock(instance);
        Bitmap old = NewImage(), newest = NewImage(); trash.Add(old);
        using (ManualResetEvent started = new ManualResetEvent(false))
        using (ManualResetEvent ended = new ManualResetEvent(false))
        {
            int retired = -1; Exception error = null;
            Thread worker = new Thread(delegate()
            {
                started.Set(); try { retired = Drain(instance, 32, 0); } catch (Exception caught) { error = caught; }
                finally { ended.Set(); }
            });
            bool waited;
            lock (gate)
            {
                worker.Start(); started.WaitOne(); waited = !ended.WaitOne(30, false);
                trash.Add(newest);
            }
            bool finished = worker.Join(2000);
            Check(waited && finished && error == null, "limpieza respeta contencion del mismo coverLock y luego continua");
            Check(retired == 1 && trash.Count == 1 && !Alive(newest) && Alive(old),
                "insercion bajo lock durante espera queda coherente; no se pierde ni libera el resto");
        }
        Drain(instance, 32, long.MaxValue);

        instance = NewForm(); trash = Trash(instance); gate = CoverLock(instance);
        List<Bitmap> produced = new List<Bitmap>();
        for (int i = 0; i < 150; i++) produced.Add(NewImage());
        Exception producerError = null;
        Thread producer = new Thread(delegate()
        {
            try { foreach (Bitmap image in produced) { lock (gate) trash.Add(image); Thread.Sleep(0); } }
            catch (Exception caught) { producerError = caught; }
        });
        producer.Start(); bool bounded = true;
        while (producer.IsAlive)
        { int count = Drain(instance, 7, long.MaxValue); if (count < 0 || count > 7) bounded = false; Thread.Sleep(0); }
        producer.Join();
        while (trash.Count > 0) { int count = Drain(instance, 7, long.MaxValue); if (count < 1 || count > 7) bounded = false; }
        bool released = true; foreach (Bitmap image in produced) if (Alive(image)) released = false;
        Check(producerError == null && bounded && released && trash.Count == 0,
            "inserciones concurrentes y vaciado progresivo liberan exactamente las portadas producidas");
    }

    static int Main(string[] args)
    {
        if (args.Length != 1) { Console.WriteLine("Uso: papeleraportadas.exe Rocola.exe"); return 2; }
        form = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1", true);
        drain = form.GetMethod("DrainCoverTrash", B); limited = form.GetMethod("DrainCoverTrashAcotado", B);
        try
        {
            WiringAndNegative();
            if (limited != null) { BudgetsAndRemaining(); PinnedOwners(); SameLockAndInsertions(); }
        }
        catch (Exception error) { Check(false, error.ToString()); }
        finally { foreach (Bitmap image in images) image.Dispose(); }
        Console.WriteLine("Papelera portadas: " + checks + " comprobaciones, " + failures + " fallos");
        return failures == 0 ? 0 : 1;
    }
}
