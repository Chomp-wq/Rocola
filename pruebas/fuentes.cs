using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NAudio.Wave;
class Fuentes
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type F; static int malos, buenos;
    static void Check(bool ok, string s) { Console.WriteLine((ok ? "OK " : "FALLO ") + s); if (ok) buenos++; else malos++; }
    static object Get(object o, string n) { return F.GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { F.GetField(n, B).SetValue(o, v); }
    static object Call(object o, string n, params object[] a)
    { try { return F.GetMethod(n, B).Invoke(o, a); } catch (TargetInvocationException ex) { throw ex.InnerException; } }
    static object Nueva()
    {
        object f = Codigo.FormSinEncender(F);
        foreach (FieldInfo field in F.GetFields(B))
            if (!field.IsStatic && field.FieldType.Name == "GuardadoReciente" && field.GetValue(f) == null)
                field.SetValue(f, Activator.CreateInstance(field.FieldType, true));
        Set(f, "fuentesEstado", 1); Set(f, "startupReadPending", true); return f;
    }
    static void Medida(object f, string ruta, object fuente)
    {
        float[] eq = new float[12]; eq[8] = 17000; eq[11] = 2;
        Call(f, "PublicaMedidaDeFuente", ruta, .1234567f, 10.1234f, eq, fuente);
    }
    static bool Tiene(object f, string r) { return ((IDictionary)Get(f, "durCache")).Contains(r); }
    static void Espera(object f) { Codigo.EsperaGuardado(F, f); }
    static void LectorPrecargado(object f, string dir)
    {
        string wav = Path.Combine(dir, "lector-precargado.wav");
        using (WaveFileWriter writer = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)))
        {
            float[] muestras = new float[88200];
            for (int i = 0; i < muestras.Length; i++) muestras[i] = (float)(.1 * Math.Sin(i * .1));
            writer.WriteSamples(muestras, 0, muestras.Length);
        }
        using (IDisposable lector = (IDisposable)Call(f, "OpenAudio", wav))
        {
            object fuente = Call(f, "FuenteDeLector", wav, lector);
            Check(fuente != null && (bool)Call(f, "CoincideFuenteLector", wav, lector), "OpenAudio conserva el sello previo con su lector");
        }
        // El original y el lector son independientes, igual que bytes precargados o
        // Apollo. Windows impide editar el WAV que AudioFileReader mantiene abierto.
        string ruta = Path.Combine(dir, "original-del-lector.bin"); File.WriteAllBytes(ruta, new byte[32]);
        object inicio = Call(null, "CapturaFuente", ruta);
        using (IDisposable lector = (IDisposable)Call(null, "SellaFuenteAudio", ruta, inicio, Call(f, "OpenAudio", wav)))
        {
            object fuente = Call(f, "FuenteDeLector", ruta, lector);
            File.SetLastWriteTimeUtc(ruta, File.GetLastWriteTimeUtc(ruta).AddSeconds(5));
            object track = Activator.CreateInstance(F.GetNestedType("Track", B), true);
            track.GetType().GetField("Path", B).SetValue(track, ruta);
            object cadena = Call(f, "ArmaCadena", track, lector, false, 0.0);
            object selloCadena = cadena.GetType().GetField("Fuente", B).GetValue(cadena);
            Check(object.ReferenceEquals(fuente, selloCadena) && !(bool)Call(f, "CoincideFuenteLector", ruta, lector),
                  "ArmaCadena no certifica el lector viejo con el archivo que cambio esperando a la UI");
            Medida(f, ruta, selloCadena); Check(!Tiene(f, ruta), "las muestras del lector precargado no se publican para el archivo sustituido");
            object invalida = Call(null, "SellaFuenteAudio", ruta, fuente, lector);
            Check(!(bool)Call(f, "AceptaFuenteMedida", ruta, Call(f, "FuenteDeLector", ruta, invalida)),
                  "un cambio durante la apertura queda no certificable, nunca null aceptable");
        }
        FieldInfo mapa = F.GetField("iaOriginal", B);
        if (mapa != null)
        {
            int lee, escribe; Codigo.UsosDeCampo(F.GetMethod("PlayTrack", B), "iaOriginal", out lee, out escribe);
            Check(lee > 0 && Codigo.Llama(F.GetMethod("PlayTrack", B), "set_Item"),
                  "PlayTrack enlaza el original de Apollo tambien sin precarga previa");
            IDictionary originales = (IDictionary)mapa.GetValue(null);
            string original = Path.Combine(dir, "original-apollo.bin"); File.WriteAllBytes(original, new byte[32]);
            originales[wav] = original;
            try
            {
                using (IDisposable lector = (IDisposable)Call(f, "OpenAudio", wav))
                {
                    object sello = Call(f, "FuenteDeLector", original, lector);
                    File.SetLastWriteTimeUtc(original, File.GetLastWriteTimeUtc(original).AddSeconds(5));
                    Check(sello != null && !(bool)Call(f, "AceptaFuenteMedida", original, sello),
                          "el WAV de Apollo transporta la identidad del original, no solo la del WAV");
                }
            }
            finally { originales.Remove(wav); }
        }
    }
    static int Main(string[] args)
    {
        F = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1", true);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_fuentes_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir); object antes = Get(null, "carpetaDatos");
        try
        {
            Set(null, "carpetaDatos", dir);
            LectorPrecargado(Nueva(), dir);
            string ruta = Path.Combine(dir, "misma-cancion.bin"); File.WriteAllBytes(ruta, new byte[128]);
            object f = Nueva(), sello = Call(null, "CapturaFuente", ruta); Medida(f, ruta, sello);
            Check(Tiene(f, ruta), "medida nueva con sello anterior a la lectura");
            Call(f, "CompruebaFuenteMedida", ruta); Check(Tiene(f, ruta), "misma fuente conserva medidas");
            Set(f, "startupReadPending", false); Call(f, "SaveFuentesMedidas"); Espera(f);
            Set(f, "startupReadPending", true); // las invalidaciones siguientes no encargan escritores de esta fixture
            string archivo = Path.Combine(dir, "rocola_fuentes.txt"); Check(File.Exists(archivo), "identidad persistida en fondo");
            object g = Nueva();
            ((IDictionary)Get(g, "loudCache"))[ruta] = .123457f; ((IDictionary)Get(g, "durCache"))[ruta] = 10.12f;
            ((IDictionary)Get(g, "eqCache"))[ruta] = ((IDictionary)Get(f, "eqCache"))[ruta];
            Call(g, "LoadFuentesMedidas"); Call(g, "CompruebaFuenteMedida", ruta);
            Check(Tiene(g, ruta), "redondeo de tablas conserva identidad");
            ((IDictionary)Get(g, "durCache"))[ruta] = 11f; Call(g, "CompruebaFuenteMedida", ruta);
            Check(!Tiene(g, ruta), "snapshot parcial no certifica medidas distintas");
            DateTime fecha = File.GetLastWriteTimeUtc(ruta);
            Call(null, "IntegradaPuesta", ruta);
            File.WriteAllBytes(ruta, new byte[128]); File.SetLastWriteTimeUtc(ruta, fecha.AddSeconds(2));
            Call(f, "CompruebaFuenteMedida", ruta);
            Check(!Tiene(f, ruta) && !((IDictionary)Get(f, "eqCache")).Contains(ruta), "otra edicion del mismo largo/duracion invalida volumen y EQ");
            Check(!(bool)Call(null, "YaIntegrada", ruta), "la sustitucion permite repetir el analisis integrado en esta sesion");
            Medida(f, ruta, sello); Check(!Tiene(f, ruta), "resultado tardio no se publica");
            object h = Nueva(); ((IDictionary)Get(h, "durCache"))[ruta] = 10f;
            Call(h, "CompruebaFuenteMedida", ruta); Check(!Tiene(h, ruta), "tabla antigua sin identidad se migra conservadoramente");
            File.WriteAllText(archivo, "ROCOLA-FUENTES v99\r\nregistro-futuro\r\n");
            Call(h, "LoadFuentesMedidas"); Medida(h, ruta, Call(null, "CapturaFuente", ruta));
            Set(h, "startupReadPending", false); Call(h, "SaveFuentesMedidas"); Espera(h);
            Check(File.ReadAllText(archivo).Contains("registro-futuro"), "no sobrescribe formato futuro");
            object tintes = Nueva(); Set(tintes, "lastFreshRootSig", "identidad-A");
            ((IDictionary)Get(tintes, "letterTag"))['A'] = "Rock";
            int firma = (int)Call(tintes, "FirmaBiblioteca"); Call(tintes, "GuardaTintes", firma); Espera(tintes);
            Call(tintes, "LeeTintes", firma); Check(!(bool)Get(tintes, "tintesPend"), "colores aceptan identidad completa");
            Set(tintes, "lastFreshRootSig", "identidad-B"); Call(tintes, "LeeTintes", Call(tintes, "FirmaBiblioteca"));
            Check((bool)Get(tintes, "tintesPend"), "otra biblioteca con igual cantidad recalcula colores");
            object diario = Nueva(); Set(diario, "startupReadPending", false);
            ((IDictionary)Get(diario, "loudCache"))[ruta] = .2f; ((IDictionary)Get(diario, "durCache"))[ruta] = 10f;
            Call(diario, "SaveLoudCache"); Espera(diario);
            string volumen = (string)Call(diario, "LoudFile"), texto = File.ReadAllText(volumen);
            DateTime escrita = File.GetLastWriteTimeUtc(volumen);
            File.WriteAllText(volumen, texto.Replace("0.2|", "0.9|"), new System.Text.UTF8Encoding(false));
            File.SetLastWriteTimeUtc(volumen, escrita.AddSeconds(2));
            Call(diario, "SaveLoudCache"); Espera(diario);
            Check(File.ReadAllText(volumen) == texto, "restauracion externa del mismo largo se detecta");
        }
        catch (Exception ex) { Check(false, ex.ToString()); }
        finally { Set(null, "carpetaDatos", antes); try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine("Fuentes: " + buenos + " OK, " + malos + " fallos"); return malos == 0 ? 0 : 1;
    }
}
