// IA1 «QUE LA ROCOLA ENTRENE SOLA» (Rocola OS, 3-11-2026; decision del dueno sobre IA_DATOS_EN_CRUDO_MODEST_2026-11-03.md).
// El entrenador del DSEE (ia/entrenar_ia.py, en una PC con Python) pasa al programa de la rocola: con lo que la propia rocola cosecha
// (rocola_ia_datos.bin y su indice .idx) entrena una red nueva y la compara con la que esta puesta, con LAS MISMAS REGLAS:
//  - el indice dice de que cancion, carpeta y artista viene cada lote; las entradas con su CRC malo, de otro extractor o cuyos datos no
//    cuadran con su CRC no se usan, y lo que no tiene origen tampoco (no se sabe si se mezclaria con la prueba);
//  - se reparte por GRUPO (artista, si no carpeta, si no cancion; un grupo que se coma mas de la mitad baja un nivel) en entrenamiento,
//    validacion y prueba (80/10/10 de los ejemplos, al menos 1/3/3 grupos y 500/200/200 ejemplos), con el mismo orden mezclado y la
//    misma semilla que origen_dsee.py: ningun grupo esta en dos partes;
//  - los pesos igualan grupos y canciones (pesos_equilibrados, acotados a [1/4, 4] con media 1); la normalizacion sale solo del
//    entrenamiento; red de la misma forma que la puesta (si no hay, 28-48-48-12), tanh, Adam (2e-3), L2 1e-5, lotes de 256;
//  - la VALIDACION elige el mejor punto (se mira en cada epoca); la PRUEBA solo decide: contra la curva fija, 5 % mejor en total y
//    ninguna banda 10 % peor; contra la red puesta, 2 % y 10 %; en bruto y como suena (la red de la rocola, float32 y sus topes).
// Lo que se aparta del de Python (se dice en el informe): una muestra de hasta MaxFilas ejemplos, la semilla del azar de .NET (no la de
// numpy: los pesos no salen iguales), y no hace el informe por clases de senal (nivel, brillo...), que alli es solo un aviso o un bloqueo
// sobre la envolvente; el de aqui se queda en las dos reglas de arriba.
// NUNCA se pone sola: si pasa, queda como candidata (IAS/rocola_ia.candidata.bin, con su informe .txt) y el dueno la prueba a ciegas
// en Ctrl+F3 y decide si se queda (IaNueva.cs). Si no pasa, no se guarda la red: solo el informe.
// La cuenta va en coma flotante de 32 bits con Vector<float> (en el A4, AVX: 8 a la vez); un hilo, con la prioridad mas baja.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
        internal sealed class EntrenoIa
        {
            internal int MaxFilas = 400000, Epocas = 40, Lote = 256, Semilla = 3;
            internal float Lr = 2e-3f, L2 = 1e-5f;
            internal Func<bool> Parar = delegate { return false; };
            internal Action<string> Avance = delegate { };

            // Resultado
            internal bool Aceptada;              // paso las dos reglas: queda como candidata
            internal byte[] Modelo;              // RIA1 (solo si se entreno)
            internal string Informe = "";        // el texto del informe
            internal string Resumen = "";        // una linea para Ctrl+F3 y la bitacora
            internal bool Cortado;               // se paro (la rocola se uso): no cuenta como intento

            private sealed class Entrada { public int Fuente; public long Primero; public int Cuenta; public uint Crc; public ulong Cancion, Carpeta, Artista; public int Parte = -1; public ulong Grupo; }

            internal const string Cabecera = "RIAD";
            private readonly StringBuilder inf = new StringBuilder();
            private void Linea(string s) { inf.AppendLine(s); }

            // ---------------- LO QUE SE ENTRENA ----------------
            private int[] dims;          // capas: 28 ... 12
            private float[][] W, B;      // fila-mayor: W[L][o * in + i]

            internal void Corre(string datos, string indice, string instalada) { Corre(new List<string[]> { new[] { datos, indice } }, instalada); }
            // Varios materiales (el de esta rocola y el de la rocola de XP, MaterialIa.cs): las canciones se reconocen por su huella, asi que
            // una misma cancion cosechada en los dos queda en el mismo grupo y nunca a la vez en entrenamiento y en prueba.
            internal void Corre(List<string[]> materiales, string instalada)
            {
                try { CorreDentro(materiales, instalada); }
                catch (OperationCanceledException) { Cortado = true; Resumen = "se paro: la rocola se uso (se vuelve a intentar en otra pausa)"; }
                catch (Exception e) { Resumen = "no se pudo entrenar: " + e.Message; Linea(Resumen); }
                Informe = inf.ToString();
            }
            private void Mira() { if (Parar()) throw new OperationCanceledException(); }

            private string[] rutasDatos;
            private void CorreDentro(List<string[]> materiales, string instalada)
            {
                Linea("IA del DSEE entrenada en la rocola (EntrenaIa.cs, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ")");
                // 1) los indices de cada material
                List<Entrada> ent = new List<Entrada>(); rutasDatos = new string[materiales.Count];
                long filasDatos = 0, conOrigen = 0;
                for (int m = 0; m < materiales.Count; m++)
                {
                    string datos = materiales[m][0]; rutasDatos[m] = datos;
                    long filas;
                    try
                    {
                        using (FileStream fs = new FileStream(datos, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        {
                            byte[] h = new byte[16];
                            if (fs.Read(h, 0, 16) != 16 || h[0] != 'R' || h[1] != 'I' || h[2] != 'A' || h[3] != 'D' || BitConverter.ToInt32(h, 4) != 1
                                || BitConverter.ToInt32(h, 8) != 28 || BitConverter.ToInt32(h, 12) != 12) { Linea(datos + ": no es del formato de la rocola, fuera."); continue; }
                            filas = (fs.Length - 16) / 160;
                        }
                    }
                    catch (Exception ex) { Linea(datos + ": no se pudo leer (" + ex.Message + "), fuera."); continue; }
                    List<Entrada> e1; try { e1 = LeeIndice(materiales[m][1]); } catch (Exception ex) { Linea(materiales[m][1] + ": " + ex.Message + ", fuera."); continue; }
                    e1.RemoveAll(e => e.Primero + e.Cuenta > filas);
                    long co = 0; foreach (Entrada e in e1) { e.Fuente = m; co += e.Cuenta; }
                    Linea("Material " + (m + 1) + " (" + datos + "): " + filas.ToString("#,0", CultureInfo.InvariantCulture) + " ejemplos; con origen " + co.ToString("#,0", CultureInfo.InvariantCulture) + " en " + e1.Count + " lotes.");
                    ent.AddRange(e1); filasDatos += filas; conOrigen += co;
                }
                if (conOrigen < 900) throw new InvalidDataException("muy poco material con origen (" + conOrigen + " ejemplos)");
                // 2) grupos y reparto (antes de mirar un solo ejemplo)
                string nivel = Grupos(ent);
                string motivo = Reparte(ent);
                if (motivo != null) throw new InvalidDataException("sin reparto independiente: " + motivo);
                int[] gr = new int[3]; HashSet<ulong>[] gs = { new HashSet<ulong>(), new HashSet<ulong>(), new HashSet<ulong>() };
                foreach (Entrada e in ent) gs[e.Parte].Add(e.Grupo);
                Linea("Reparto por " + nivel + ": " + gs[0].Count + " / " + gs[1].Count + " / " + gs[2].Count + " grupos (entrenamiento / validacion / prueba).");
                Mira();
                // 3) la muestra, leyendo de corrido y comprobando el CRC de cada lote
                float[] X, Y; int[] parte; ulong[] cancion, grupo; int n;
                Muestra(ent, conOrigen, out X, out Y, out parte, out cancion, out grupo, out n);
                int[] cuantos = new int[3]; for (int i = 0; i < n; i++) cuantos[parte[i]]++;
                Linea("Muestra: " + n.ToString("#,0", CultureInfo.InvariantCulture) + " ejemplos (tope " + MaxFilas.ToString("#,0", CultureInfo.InvariantCulture) + "): " + cuantos[0] + " / " + cuantos[1] + " / " + cuantos[2] + ".");
                if (cuantos[0] < 500 || cuantos[1] < 200 || cuantos[2] < 200) throw new InvalidDataException("la muestra no llega a 500/200/200 ejemplos");
                int[] tr = Indices(parte, n, 0), va = Indices(parte, n, 1), pr = Indices(parte, n, 2);
                float[] pesos = PesosEquilibrados(tr, cancion, grupo);
                // 4) la red puesta (para la forma y para comparar)
                RedChica puesta = null; int[] dimsPuesta = null;
                if (instalada != null && File.Exists(instalada)) { puesta = new RedChica(28, 12); puesta.TryLoad(instalada); dimsPuesta = DimsDe(instalada); if (!puesta.Loaded) { puesta = null; dimsPuesta = null; } }
                dims = dimsPuesta ?? new[] { 28, 48, 48, 12 };
                Linea("Red: " + string.Join("-", dims) + (puesta != null ? " (la forma de la puesta)" : " (no hay red puesta: se compara solo con la curva fija)") + ".");
                // 5) normalizar con el entrenamiento y entrenar
                double[] mu, sd; Normaliza(X, tr, out mu, out sd);
                Entrena(X, Y, tr, va, pesos, mu, sd);
                Pliega(mu, sd);
                Modelo = Ria1();
                // 6) la prueba decide
                RedChica nueva = new RedChica(28, 12); string tmp = Path.GetTempFileName();
                try { File.WriteAllBytes(tmp, Modelo); nueva.TryLoad(tmp); } finally { try { File.Delete(tmp); } catch { } }
                if (!nueva.Loaded) throw new InvalidDataException("la red entrenada no la cargaria la rocola");
                double[] eBruto, eSuena, eRef, ePuestaB = null, ePuestaS = null;
                Evalua(X, Y, pr, nueva, true, out eBruto, out eSuena, out eRef);
                if (puesta != null) { double[] d; Evalua(X, Y, pr, puesta, false, out ePuestaB, out ePuestaS, out d); }
                List<string> motivos = new List<string>(); bool ok = true;
                ok &= Cumple("en bruto contra la curva fija", eBruto, eRef, 0.05, motivos);
                ok &= Cumple("como suena contra la curva fija", eSuena, eRef, 0.05, motivos);
                if (puesta != null)
                {
                    ok &= Cumple("en bruto contra la red puesta", eBruto, ePuestaB, 0.02, motivos);
                    ok &= Cumple("como suena contra la red puesta", eSuena, ePuestaS, 0.02, motivos);
                }
                Linea("Error en la PRUEBA (MSE, log10^2): nueva " + Media(eSuena).ToString("0.0000", CultureInfo.InvariantCulture) + " | curva fija " + Media(eRef).ToString("0.0000", CultureInfo.InvariantCulture)
                    + (puesta != null ? " | puesta " + Media(ePuestaS).ToString("0.0000", CultureInfo.InvariantCulture) : "") + " (como suena).");
                foreach (string m in motivos) Linea("  " + m);
                Aceptada = ok;
                double mejora = puesta != null ? 1 - Media(eSuena) / Math.Max(1e-12, Media(ePuestaS)) : 1 - Media(eSuena) / Math.Max(1e-12, Media(eRef));
                Resumen = ok ? "nueva IA lista para probar: " + (100 * mejora).ToString("0.0", CultureInfo.InvariantCulture) + " % menos error que " + (puesta != null ? "la puesta" : "la curva fija")
                             : "la red nueva no mejoro lo bastante (" + (100 * mejora).ToString("0.0", CultureInfo.InvariantCulture) + " %): se queda la de ahora";
                Linea(Aceptada ? "CANDIDATA: paso las reglas. No se pone sola: pruebala a ciegas en Ctrl+F3 -> Mejoras con IA." : "No paso las reglas: no se guarda la red.");
                Linea("Se aparta de ia/entrenar_ia.py en: muestra de hasta " + MaxFilas + " ejemplos, azar de .NET y sin informe por clases de senal.");
            }

            // ---------------- EL INDICE (RIAX v1, entradas de 64 bytes con su CRC) ----------------
            private List<Entrada> LeeIndice(string ruta)
            {
                List<Entrada> r = new List<Entrada>();
                if (!File.Exists(ruta)) throw new InvalidDataException("falta rocola_ia_datos.idx: no se sabe de que cancion es cada ejemplo");
                byte[] d = File.ReadAllBytes(ruta);
                if (d.Length < 16 || d[0] != 'R' || d[1] != 'I' || d[2] != 'A' || d[3] != 'X' || BitConverter.ToInt32(d, 4) != 1 || BitConverter.ToInt32(d, 8) != 64)
                    throw new InvalidDataException("rocola_ia_datos.idx no es un indice de la rocola");
                int malas = 0, otras = 0;
                for (int p = 16; p + 64 <= d.Length; p += 64)
                {
                    if (IaCrc32(d, p, 60) != BitConverter.ToUInt32(d, p + 60)) { malas++; continue; }
                    if (BitConverter.ToInt32(d, p + 12) != IaExtractorVersion || BitConverter.ToInt32(d, p + 20) != 2048) { otras++; continue; }
                    Entrada e = new Entrada
                    {
                        Primero = BitConverter.ToInt64(d, p), Cuenta = BitConverter.ToInt32(d, p + 8), Crc = BitConverter.ToUInt32(d, p + 28),
                        Cancion = BitConverter.ToUInt64(d, p + 32), Carpeta = BitConverter.ToUInt64(d, p + 40), Artista = BitConverter.ToUInt64(d, p + 48)
                    };
                    if (e.Primero < 0 || e.Cuenta <= 0 || e.Cancion == 0) { malas++; continue; }
                    r.Add(e);
                }
                r.Sort((a, b) => a.Primero.CompareTo(b.Primero));
                List<Entrada> limpia = new List<Entrada>(); long fin = 0;
                foreach (Entrada e in r) { if (e.Primero < fin) { malas++; continue; } limpia.Add(e); fin = e.Primero + e.Cuenta; }
                if (malas + otras > 0) Linea("Indice: " + malas + " entradas danadas o solapadas y " + otras + " de otro extractor, fuera.");
                return limpia;
            }

            // ---------------- GRUPOS (origen_dsee.grupos) y REPARTO (origen_dsee.reparte) ----------------
            private static string Grupos(List<Entrada> ent)
            {
                string[] niveles = { "artista", "carpeta", "cancion" };
                long total = 0; foreach (Entrada e in ent) total += e.Cuenta;
                foreach (string nv in niveles)
                {
                    Dictionary<ulong, ulong> padre = new Dictionary<ulong, ulong>();
                    Func<ulong, ulong> raiz = null;
                    raiz = delegate (ulong x) { while (padre[x] != x) { padre[x] = padre[padre[x]]; x = padre[x]; } return x; };
                    List<ulong[]> claves = new List<ulong[]>();
                    foreach (Entrada e in ent)
                    {
                        List<ulong> k = new List<ulong> { e.Cancion };
                        if (nv != "cancion") k.Add(e.Carpeta != 0 ? e.Carpeta ^ 0x9E3779B97F4A7C15UL : 0);
                        if (nv == "artista") k.Add(e.Artista != 0 ? e.Artista ^ 0x7F4A7C159E3779B9UL : 0);
                        k.RemoveAll(c => c == 0);
                        claves.Add(k.ToArray());
                        foreach (ulong c in k) if (!padre.ContainsKey(c)) padre[c] = c;
                        for (int i = 1; i < k.Count; i++) { ulong a = raiz(k[0]), b = raiz(k[i]); if (a != b) { if (a < b) padre[b] = a; else padre[a] = b; } }
                    }
                    Dictionary<ulong, long> peso = new Dictionary<ulong, long>(); long mayor = 0;
                    for (int i = 0; i < ent.Count; i++)
                    {
                        ulong g = raiz(claves[i][0]); ent[i].Grupo = g;
                        long p; peso.TryGetValue(g, out p); p += ent[i].Cuenta; peso[g] = p; if (p > mayor) mayor = p;
                    }
                    if ((double)mayor / Math.Max(1, total) <= 0.5 || nv == "cancion") return nv;
                }
                return "cancion";
            }
            private static ulong Mezcla64(ulong x, ulong semilla)
            {
                x ^= unchecked(semilla * 0x9E3779B97F4A7C15UL);
                x ^= x >> 33; x = unchecked(x * 0xFF51AFD7ED558CCDUL);
                x ^= x >> 33; x = unchecked(x * 0xC4CEB9FE1A85EC53UL);
                x ^= x >> 33; return x;
            }
            private static string Reparte(List<Entrada> ent)
            {
                int[] minG = { 1, 3, 3 }; long[] minE = { 500, 200, 200 }; double[] fr = { 0.8, 0.1, 0.1 };
                Dictionary<ulong, long> peso = new Dictionary<ulong, long>();
                foreach (Entrada e in ent) { long p; peso.TryGetValue(e.Grupo, out p); peso[e.Grupo] = p + e.Cuenta; }
                if (peso.Count < 7) return "solo hay " + peso.Count + " grupos independientes; hacen falta al menos 7";
                List<ulong> orden = new List<ulong>(peso.Keys);
                orden.Sort((a, b) => { int c = Mezcla64(a, 20260922).CompareTo(Mezcla64(b, 20260922)); return c != 0 ? c : a.CompareTo(b); });
                double total = 0; foreach (long p in peso.Values) total += p;
                Dictionary<ulong, int> parteG = new Dictionary<ulong, int>(); foreach (ulong g in orden) parteG[g] = 0;
                int cursor = 0;
                foreach (int p in new[] { 2, 1 })
                {
                    double acum = 0; int n = 0;
                    while (cursor < orden.Count && (acum < fr[p] * total || n < minG[p])) { ulong g = orden[cursor++]; parteG[g] = p; acum += peso[g]; n++; }
                }
                long[] ej = new long[3]; int[] ng = new int[3];
                foreach (KeyValuePair<ulong, int> kv in parteG) { ng[kv.Value]++; ej[kv.Value] += peso[kv.Key]; }
                foreach (Entrada e in ent) e.Parte = parteG[e.Grupo];
                string[] nombres = { "entrenamiento", "validacion", "prueba" };
                for (int p = 0; p < 3; p++)
                    if (ng[p] < minG[p] || ej[p] < minE[p]) return "la particion de " + nombres[p] + " se queda con " + ng[p] + " grupos y " + ej[p] + " ejemplos";
                return null;
            }

            // ---------------- LA MUESTRA ----------------
            // Una posicion al azar en cada estrato (indices_estratificados, semilla 20260929) sobre los ejemplos CON origen, leyendo el archivo
            // de corrido: cada lote entero pasa por su CRC (como lee_indice) y solo si cuadra entran sus ejemplos elegidos.
            private void Muestra(List<Entrada> ent, long total, out float[] X, out float[] Y, out int[] parte, out ulong[] cancion, out ulong[] grupo, out int n)
            {
                int quiere = (int)Math.Min(total, MaxFilas);
                long[] pos = new long[quiere];
                if (quiere == total) for (int i = 0; i < quiere; i++) pos[i] = i;
                else
                {
                    Random r = new Random(20260929);
                    for (int i = 0; i < quiere; i++)
                    {
                        long a = (long)((double)total * i / quiere), b = (long)((double)total * (i + 1) / quiere);
                        pos[i] = a + (long)(r.NextDouble() * (b - a));
                    }
                }
                X = new float[quiere * 28]; Y = new float[quiere * 12]; parte = new int[quiere]; cancion = new ulong[quiere]; grupo = new ulong[quiere];
                n = 0; int malos = 0, sigue = 0; long rango = 0;
                byte[] buf = new byte[160 * 8192]; float[] fila = new float[40];
                FileStream[] abiertos = new FileStream[rutasDatos.Length];
                try
                {
                long leido = 0, aLeer = total; int ultimoAviso = Environment.TickCount;
                    foreach (Entrada e in ent)
                    {
                        Mira();
                        int n0 = n; uint crc = 0xFFFFFFFFu; bool roto = false;
                        FileStream fs = abiertos[e.Fuente] ?? (abiertos[e.Fuente] = new FileStream(rutasDatos[e.Fuente], FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 20));
                        fs.Position = 16 + e.Primero * 160;
                        for (int hecho = 0; hecho < e.Cuenta;)
                        {
                            int filas = Math.Min(8192, e.Cuenta - hecho), bytes = filas * 160, got = 0;
                            while (got < bytes) { int k = fs.Read(buf, got, bytes - got); if (k <= 0) break; got += k; }
                            if (got < bytes) { roto = true; break; }
                            crc = IaCrc32Acumula(crc, buf, 0, bytes);
                            for (int f = 0; f < filas; f++)
                            {
                                long rk = rango + hecho + f;
                                if (sigue >= quiere || pos[sigue] != rk) continue;
                                sigue++;
                                Buffer.BlockCopy(buf, f * 160, fila, 0, 160);
                                bool finito = true; for (int z = 0; z < 40; z++) if (!float.IsFinite(fila[z])) { finito = false; break; }
                                if (!finito) continue;
                                Array.Copy(fila, 0, X, n * 28, 28); Array.Copy(fila, 28, Y, n * 12, 12);
                                parte[n] = e.Parte; cancion[n] = e.Cancion; grupo[n] = e.Grupo; n++;
                            }
                            hecho += filas;
                        }
                        if (roto || (crc ^ 0xFFFFFFFFu) != e.Crc) { malos++; n = n0; }   // un lote que no cuadra con su CRC no se usa
                        while (sigue < quiere && pos[sigue] < rango + e.Cuenta) sigue++;
                        rango += e.Cuenta; leido += e.Cuenta;
                        if (unchecked(Environment.TickCount - ultimoAviso) > 5000) { ultimoAviso = Environment.TickCount; Avance("leyendo el material: " + (100 * leido / Math.Max(1, aLeer)) + " %"); }
                    }
                }
                finally { foreach (FileStream f in abiertos) if (f != null) f.Dispose(); }
                if (malos > 0) Linea("Lotes cuyo CRC no cuadra (no se usan): " + malos + ".");
            }
            private static int[] Indices(int[] parte, int n, int p) { List<int> r = new List<int>(); for (int i = 0; i < n; i++) if (parte[i] == p) r.Add(i); return r.ToArray(); }

            // pesos_equilibrados: igual masa por grupo y por cancion dentro del grupo, acotado a [1/4, 4] con media 1.
            private static float[] PesosEquilibrados(int[] tr, ulong[] cancion, ulong[] grupo)
            {
                const double limite = 4.0;
                Dictionary<ulong, int> porCancion = new Dictionary<ulong, int>();
                Dictionary<ulong, HashSet<ulong>> cancionesDe = new Dictionary<ulong, HashSet<ulong>>();
                foreach (int i in tr)
                {
                    int c; porCancion.TryGetValue(cancion[i], out c); porCancion[cancion[i]] = c + 1;
                    HashSet<ulong> s; if (!cancionesDe.TryGetValue(grupo[i], out s)) cancionesDe[grupo[i]] = s = new HashSet<ulong>(); s.Add(cancion[i]);
                }
                double[] bruto = new double[tr.Length]; double min = double.MaxValue;
                for (int k = 0; k < tr.Length; k++)
                {
                    int i = tr[k];
                    bruto[k] = (double)tr.Length / ((double)cancionesDe.Count * cancionesDe[grupo[i]].Count * porCancion[cancion[i]]);
                    if (bruto[k] < min) min = bruto[k];
                }
                double abajo = 0, arriba = limite / min;
                for (int it = 0; it < 48; it++)
                {
                    double esc = (abajo + arriba) / 2, s = 0;
                    for (int k = 0; k < bruto.Length; k++) s += Math.Min(limite, Math.Max(1 / limite, bruto[k] * esc));
                    if (s / bruto.Length < 1) abajo = esc; else arriba = esc;
                }
                double e2 = (abajo + arriba) / 2; float[] w = new float[tr.Length];
                for (int k = 0; k < w.Length; k++) w[k] = (float)Math.Min(limite, Math.Max(1 / limite, bruto[k] * e2));
                return w;
            }

            private static void Normaliza(float[] X, int[] tr, out double[] mu, out double[] sd)
            {
                mu = new double[28]; sd = new double[28]; double[] m2 = new double[28]; long n = 0;
                foreach (int i in tr)
                {
                    n++;
                    for (int j = 0; j < 28; j++) { double v = X[i * 28 + j], d = v - mu[j]; mu[j] += d / n; m2[j] += d * (v - mu[j]); }
                }
                for (int j = 0; j < 28; j++) { double s = Math.Sqrt(Math.Max(0, m2[j] / Math.Max(1, n))); sd[j] = s < 1e-6 ? 1.0 : s; }
            }

            private static int[] DimsDe(string ruta)
            {
                try
                {
                    byte[] d = File.ReadAllBytes(ruta); int nl = BitConverter.ToInt32(d, 4), p = 8; int[] r = new int[nl + 1];
                    for (int L = 0; L < nl; L++) { int ro = BitConverter.ToInt32(d, p), ci = BitConverter.ToInt32(d, p + 4); if (L == 0) r[0] = ci; r[L + 1] = ro; p += 8 + (ro * ci + ro) * 4; }
                    return r[0] == 28 && r[nl] == 12 ? r : null;
                }
                catch { return null; }
            }

            // ---------------- ENTRENAR (train de entrenar_ia.py: Adam, L2, MSE con pesos, el mejor por validacion) ----------------
            private void Entrena(float[] X, float[] Y, int[] tr, int[] va, float[] pesos, double[] mu, double[] sd)
            {
                int capas = dims.Length - 1;
                Random rng = new Random(Semilla);
                W = new float[capas][]; B = new float[capas][];
                for (int L = 0; L < capas; L++)
                {
                    W[L] = new float[dims[L + 1] * dims[L]]; B[L] = new float[dims[L + 1]];
                    double s = Math.Sqrt(2.0 / dims[L]);
                    for (int i = 0; i < W[L].Length; i++) W[L][i] = (float)(s * Normal(rng));
                }
                float[][] mW = Ceros(W), vW = Ceros(W), mB = Ceros(B), vB = Ceros(B), gW = Ceros(W), gB = Ceros(B);
                float[] fmu = new float[28], fsd = new float[28]; for (int j = 0; j < 28; j++) { fmu[j] = (float)mu[j]; fsd[j] = (float)(1.0 / sd[j]); }
                int maxDim = 0; foreach (int d in dims) maxDim = Math.Max(maxDim, d);
                float[][] act = new float[dims.Length][]; for (int L = 0; L < dims.Length; L++) act[L] = new float[Lote * dims[L]];
                float[] g = new float[Lote * maxDim], g2 = new float[Lote * maxDim];
                const float b1 = 0.9f, b2 = 0.999f, eps = 1e-8f; long paso = 0;
                double mejor = double.MaxValue; int mejorEpoca = 0; float[][] mejorW = null, mejorB = null;
                int[] idx = new int[tr.Length]; for (int k = 0; k < idx.Length; k++) idx[k] = k;   // posiciones en tr (su peso: pesos[k])
                for (int ep = 0; ep < Epocas; ep++)
                {
                    for (int i = idx.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); int t = idx[i]; idx[i] = idx[j]; idx[j] = t; }
                    for (int s = 0; s < idx.Length; s += Lote)
                    {
                        if ((s & 0xFFFF) == 0) Mira();
                        int nb = Math.Min(Lote, idx.Length - s);
                        for (int b = 0; b < nb; b++) { int i = tr[idx[s + b]]; for (int j = 0; j < 28; j++) act[0][b * 28 + j] = (X[i * 28 + j] - fmu[j]) * fsd[j]; }
                        Adelante(act, nb);
                        // g = 2 (y' - y) w / nb, sobre la ultima capa
                        int no = dims[capas];
                        for (int b = 0; b < nb; b++) { int k = idx[s + b], i = tr[k]; float w = 2f * pesos[k] / nb; for (int o = 0; o < no; o++) g[b * no + o] = (act[capas][b * no + o] - Y[i * 12 + o]) * w; }
                        paso++;
                        float c1 = 1f - (float)Math.Pow(b1, paso), c2 = 1f - (float)Math.Pow(b2, paso);
                        for (int L = capas - 1; L >= 0; L--)
                        {
                            int nin = dims[L], nout = dims[L + 1];
                            Array.Clear(gW[L]); Array.Clear(gB[L]);
                            for (int b = 0; b < nb; b++)
                            {
                                ReadOnlySpan<float> a = act[L].AsSpan(b * nin, nin);
                                for (int o = 0; o < nout; o++) { float go = g[b * nout + o]; if (go == 0f) continue; Axpy(go, a, gW[L].AsSpan(o * nin, nin)); gB[L][o] += go; }
                            }
                            if (L > 0)
                            {
                                Array.Clear(g2, 0, nb * nin);
                                for (int b = 0; b < nb; b++)
                                {
                                    Span<float> d = g2.AsSpan(b * nin, nin);
                                    for (int o = 0; o < nout; o++) { float go = g[b * nout + o]; if (go != 0f) Axpy(go, W[L].AsSpan(o * nin, nin), d); }
                                    for (int k = 0; k < nin; k++) { float h = act[L][b * nin + k]; d[k] *= 1f - h * h; }
                                }
                            }
                            Adam(W[L], gW[L], mW[L], vW[L], L2, c1, c2, b1, b2, eps);
                            Adam(B[L], gB[L], mB[L], vB[L], 0f, c1, c2, b1, b2, eps);
                            if (L > 0) { float[] t = g; g = g2; g2 = t; }
                        }
                    }
                    double vl = MseValidacion(X, Y, va, fmu, fsd, act);
                    if (double.IsFinite(vl) && vl < mejor && Finitos())
                    { mejor = vl; mejorEpoca = ep + 1; mejorW = Copia(W); mejorB = Copia(B); }
                    Avance("entrenando: epoca " + (ep + 1) + " de " + Epocas + " (validacion " + vl.ToString("0.0000", CultureInfo.InvariantCulture) + ")");
                }
                if (mejorW == null) throw new InvalidDataException("ningun punto del entrenamiento salio valido");
                W = mejorW; B = mejorB;
                Linea("Entrenamiento: " + Epocas + " epocas; la validacion eligio la " + mejorEpoca + " (MSE " + mejor.ToString("0.0000", CultureInfo.InvariantCulture) + ").");
            }
            private void Adelante(float[][] act, int nb)
            {
                int capas = dims.Length - 1;
                for (int L = 0; L < capas; L++)
                {
                    int nin = dims[L], nout = dims[L + 1]; float[] w = W[L], bb = B[L];
                    for (int b = 0; b < nb; b++)
                    {
                        ReadOnlySpan<float> a = act[L].AsSpan(b * nin, nin);
                        for (int o = 0; o < nout; o++)
                        {
                            float z = bb[o] + Dot(w.AsSpan(o * nin, nin), a);
                            act[L + 1][b * nout + o] = L < capas - 1 ? MathF.Tanh(z) : z;
                        }
                    }
                }
            }
            private double MseValidacion(float[] X, float[] Y, int[] va, float[] fmu, float[] fsd, float[][] act)
            {
                double suma = 0; long cuenta = 0; int capas = dims.Length - 1;
                for (int s = 0; s < va.Length; s += Lote)
                {
                    int nb = Math.Min(Lote, va.Length - s);
                    for (int b = 0; b < nb; b++) { int i = va[s + b]; for (int j = 0; j < 28; j++) act[0][b * 28 + j] = (X[i * 28 + j] - fmu[j]) * fsd[j]; }
                    Adelante(act, nb);
                    for (int b = 0; b < nb; b++) { int i = va[s + b]; for (int o = 0; o < 12; o++) { double d = act[capas][b * 12 + o] - Y[i * 12 + o]; suma += d * d; } }
                    cuenta += nb * 12;
                }
                return suma / Math.Max(1, cuenta);
            }
            private bool Finitos() { foreach (float[] a in W) foreach (float v in a) if (!float.IsFinite(v)) return false; foreach (float[] a in B) foreach (float v in a) if (!float.IsFinite(v)) return false; return true; }
            // La normalizacion, dentro de la primera capa (como entrenar_ia.py: W0 /= sd; B0 -= W0 . mu)
            private void Pliega(double[] mu, double[] sd)
            {
                int nin = dims[0], nout = dims[1];
                for (int o = 0; o < nout; o++)
                {
                    double bias = B[0][o];
                    for (int j = 0; j < nin; j++) { double w = W[0][o * nin + j] / sd[j]; bias -= w * mu[j]; W[0][o * nin + j] = (float)w; }
                    B[0][o] = (float)bias;
                }
            }
            private byte[] Ria1()
            {
                using (MemoryStream ms = new MemoryStream())
                using (BinaryWriter w = new BinaryWriter(ms))
                {
                    w.Write((byte)'R'); w.Write((byte)'I'); w.Write((byte)'A'); w.Write((byte)'1');
                    w.Write(dims.Length - 1);
                    for (int L = 0; L < dims.Length - 1; L++) { w.Write(dims[L + 1]); w.Write(dims[L]); foreach (float v in W[L]) w.Write(v); foreach (float v in B[L]) w.Write(v); }
                    w.Flush(); return ms.ToArray();
                }
            }

            // ---------------- LA PRUEBA ----------------
            // bruto: la red tal cual; suena: con los topes que pone la reproduccion (-4 y +0,1). Con la red de la rocola (RedChica): lo mismo
            // que calculara al sonar.
            private static void Evalua(float[] X, float[] Y, int[] pr, RedChica red, bool conRef, out double[] bruto, out double[] suena, out double[] refe)
            {
                bruto = new double[12]; suena = new double[12]; refe = new double[12];
                float[] x = new float[28], y = new float[12]; float[] cf = new float[12];
                foreach (int i in pr)
                {
                    Array.Copy(X, i * 28, x, 0, 28);
                    if (!red.Predict(x, y)) throw new InvalidDataException("la red no da un numero en un ejemplo de prueba");
                    if (conRef) CurvaFija(x, cf);
                    for (int o = 0; o < 12; o++)
                    {
                        double t = Y[i * 12 + o], p = y[o], c = Math.Max(-4.0, Math.Min(0.1, p));
                        bruto[o] += (p - t) * (p - t); suena[o] += (c - t) * (c - t);
                        if (conRef) refe[o] += (cf[o] - t) * (cf[o] - t);
                    }
                }
                for (int o = 0; o < 12; o++) { bruto[o] /= pr.Length; suena[o] /= pr.Length; refe[o] /= pr.Length; }
            }
            // calidad_dsee.curva_fija: la linea base historica (cae con la octava sobre el techo de 44,1 kHz y cuadro de 2048)
            internal static void CurvaFija(float[] x, float[] y)
            {
                double fc = x[25] * 20000.0, bhz = 44100.0 / 2048.0;
                double cut = Math.Floor(fc / bhz + 0.5), top = Math.Min(952, 2 * cut - 2) * bhz;
                top = Math.Max(top, fc * (1 + 1e-6));
                for (int j = 0; j < 12; j++) y[j] = (float)Math.Max(-4.0, Math.Min(0.1, -1.8 * Math.Log2(top / fc) * ((j + 0.5) / 12)));
            }
            private static double Media(double[] a) { double s = 0; foreach (double v in a) s += v; return s / a.Length; }
            private static bool Cumple(string que, double[] err, double[] contra, double mejoraMin, List<string> motivos)
            {
                const double tol = 1e-12;
                double me = Media(err), mc = Media(contra);
                if (mc <= tol) { motivos.Add(que + ": la referencia ya es casi perfecta"); return false; }
                if (me > mc * (1 - mejoraMin)) { motivos.Add(que + ": mejora " + (100 * (1 - me / mc)).ToString("0.0", CultureInfo.InvariantCulture) + " % (hace falta " + (100 * mejoraMin).ToString("0") + " %)"); return false; }
                List<int> malas = new List<int>(); for (int j = 0; j < 12; j++) if (err[j] > contra[j] * 1.10 + tol) malas.Add(j);
                if (malas.Count > 0) { motivos.Add(que + ": banda(s) " + string.Join(",", malas) + " mas de 10 % peor"); return false; }
                motivos.Add(que + ": mejora " + (100 * (1 - me / mc)).ToString("0.0", CultureInfo.InvariantCulture) + " %"); return true;
            }

            // ---------------- cuentas ----------------
            private static double Normal(Random r) { double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble(); return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2); }
            private static float[][] Ceros(float[][] a) { float[][] r = new float[a.Length][]; for (int i = 0; i < a.Length; i++) r[i] = new float[a[i].Length]; return r; }
            private static float[][] Copia(float[][] a) { float[][] r = new float[a.Length][]; for (int i = 0; i < a.Length; i++) r[i] = (float[])a[i].Clone(); return r; }
            private static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
            {
                int n = a.Length, i = 0; Vector<float> acc = Vector<float>.Zero; int vc = Vector<float>.Count;
                for (; i <= n - vc; i += vc) acc += new Vector<float>(a.Slice(i)) * new Vector<float>(b.Slice(i));
                float s = Vector.Dot(acc, Vector<float>.One);
                for (; i < n; i++) s += a[i] * b[i];
                return s;
            }
            private static void Axpy(float k, ReadOnlySpan<float> x, Span<float> y)
            {
                int n = x.Length, i = 0, vc = Vector<float>.Count; Vector<float> kv = new Vector<float>(k);
                for (; i <= n - vc; i += vc) (new Vector<float>(y.Slice(i)) + kv * new Vector<float>(x.Slice(i))).CopyTo(y.Slice(i));
                for (; i < n; i++) y[i] += k * x[i];
            }
            private void Adam(float[] p, float[] g, float[] m, float[] v, float l2, float c1, float c2, float b1, float b2, float eps)
            {
                float lr = Lr;
                for (int i = 0; i < p.Length; i++)
                {
                    float gi = g[i] + l2 * p[i];
                    m[i] = b1 * m[i] + (1 - b1) * gi; v[i] = b2 * v[i] + (1 - b2) * gi * gi;
                    p[i] -= lr * (m[i] / c1) / (MathF.Sqrt(v[i] / c2) + eps);
                }
            }
        }
    }
}
