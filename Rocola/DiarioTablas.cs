using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Rocola
{
    // ============================== LAS TABLAS GRANDES, SIN REESCRIBIRLAS ENTERAS ==============================
    //
    // Las tres tablas grandes (rocola_volumen.txt, rocola_eq.txt y rocola_sonoridad.txt: una linea por cancion) se
    // guardaban ENTERAS cada vez: cada 25 canciones medidas durante el repaso de la biblioteca. La regla de "cuanto
    // mas grande la biblioteca, menos a menudo" miraba lo YA medido, que en la primera medicion empieza en cero, asi
    // que ahi no servia. Con 20.000 canciones eso eran unos 3 GB escritos para dejar 8 MB de tablas, y con 100.000,
    // unos 80 GB (calculado a partir del codigo, con rutas de unos 80 caracteres). En un SSD es desgaste; en un disco
    // duro, varios MB forzados al disco cada minuto mientras suena la musica.
    //
    // Ahora se ANADE AL FINAL solo lo nuevo o lo que cambio, con el MISMO formato de siempre. Se puede porque la
    // rocola -esta y las de antes- ya lee estas tablas quedandose con la ULTIMA linea de cada cancion: una cancion
    // medida otra vez sale repetida y manda la de abajo. Nada cambia para quien lee el archivo.
    //
    // Se sigue escribiendo ENTERO (con el guardado seguro de siempre: archivo aparte, bajado al disco y cambiado de un
    // tiron, con la copia .bak) cuando hace falta que el archivo quede limpio:
    //   - la primera vez en cada sesion: asi se parte siempre de un archivo que dice exactamente lo que hay en memoria;
    //   - si alguien toco el archivo desde fuera (su largo no es el que se dejo: un respaldo restaurado, borrado...);
    //   - si se quito alguna cancion de la tabla (eso no se puede decir anadiendo);
    //   - si ya hay demasiadas repetidas (mas de la mitad de la tabla y mil mas): se compacta.
    //
    // Que el archivo ya dice lo que hay se sabe por una HUELLA de cada medida (sus numeros tal cual, bit a bit), no
    // volviendo a escribir el texto: comparar veinte mil numeros cuesta un suspiro; formatearlos, no.
    //
    // Y si se va la luz justo al anadir, lo de antes no se toca (solo se escribe DETRAS del final). Lo que puede
    // quedar es una ultima linea a medias o ceros al final: al leer se descarta la ultima linea si el archivo no
    // acaba en salto de linea, y cualquier linea con ceros (LineasSanas, abajo). Esa medida se pierde y se vuelve a
    // medir sola.
    public partial class Form1
    {
        // Pantalla: solo pendientes. Escritor serial: espejo completo, sin recorrer la
        // biblioteca en cada tanda. Cancelar/fallar no consume cambios sin confirmar.
        private sealed class CambiosTabla<T> where T : class
        {
            internal readonly object Origen;
            internal readonly Dictionary<string, T> Trabajo;
            private readonly object cerrojo = new object();
            private readonly Dictionary<string, T> pendientes = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            internal CambiosTabla(object origen, Dictionary<string, T> trabajo) { Origen = origen; Trabajo = trabajo; }
            internal void Marca(string ruta, T registro) { lock (cerrojo) pendientes[ruta] = registro; }
            internal List<KeyValuePair<string, T>> Copia() { lock (cerrojo) return new List<KeyValuePair<string, T>>(pendientes); }
            internal void Aplica(List<KeyValuePair<string, T>> cambios)
            {
                for (int i = 0; i < cambios.Count; i++)
                    if (cambios[i].Value == null) Trabajo.Remove(cambios[i].Key);
                    else Trabajo[cambios[i].Key] = cambios[i].Value;
            }
            internal void Confirma(List<KeyValuePair<string, T>> cambios)
            {
                lock (cerrojo) for (int i = 0; i < cambios.Count; i++)
                {
                    T actual;
                    if (pendientes.TryGetValue(cambios[i].Key, out actual) && ReferenceEquals(actual, cambios[i].Value))
                        pendientes.Remove(cambios[i].Key);
                }
            }
        }
        private sealed class RegistroVolumen
        {
            internal readonly float Rms, Duracion;
            internal RegistroVolumen(float rms, float duracion) { Rms = rms; Duracion = duracion; }
        }
        private sealed class RegistroEq
        {
            internal readonly float[] Curva;
            // Comparte el arreglo de eqCache en lugar de copiarlo (una copia de 12 floats por cancion: con una biblioteca grande, megas de objetos
            // chicos que viven toda la sesion). Es seguro porque esos arreglos NO se modifican una vez puestos en la tabla: cada medida nueva
            // arma uno nuevo y lo pone en su lugar (CierraHuella, PublicaMedida, la lectura de la tabla); la bateria diario lo vigila.
            internal RegistroEq(float[] curva) { Curva = curva; }
        }
        private sealed class RegistroIntegrada
        {
            internal readonly SonoridadIntegrada Valor;
            internal RegistroIntegrada(SonoridadIntegrada valor) { Valor = valor; }
        }
        // El fallback de diagnostico (formulario sin constructor) conserva el guardado
        // completo; todas las instancias normales usan deltas, sin opcion de desactivarlas.
        private bool medidasDeltasActivas = true;
        private CambiosTabla<RegistroVolumen> cambiosVolumen;
        private CambiosTabla<RegistroEq> cambiosEq;
        private CambiosTabla<RegistroIntegrada> cambiosIntegrada;

        private static CambiosTabla<RegistroVolumen> PreparaVolumen(Dictionary<string, float> rms, Dictionary<string, float> dur)
        {
            Dictionary<string, RegistroVolumen> tabla = new Dictionary<string, RegistroVolumen>(rms.Count, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, float> kv in rms)
            { float d; if (!dur.TryGetValue(kv.Key, out d)) d = 0f; tabla[kv.Key] = new RegistroVolumen(kv.Value, d); }
            return new CambiosTabla<RegistroVolumen>(rms, tabla);
        }
        private static CambiosTabla<RegistroEq> PreparaEq(Dictionary<string, float[]> eq)
        {
            Dictionary<string, RegistroEq> tabla = new Dictionary<string, RegistroEq>(eq.Count, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, float[]> kv in eq) tabla[kv.Key] = new RegistroEq(kv.Value);
            return new CambiosTabla<RegistroEq>(eq, tabla);
        }
        private static CambiosTabla<RegistroIntegrada> PreparaIntegrada(Dictionary<string, SonoridadIntegrada> integ)
        {
            Dictionary<string, RegistroIntegrada> tabla = new Dictionary<string, RegistroIntegrada>(integ.Count, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, SonoridadIntegrada> kv in integ) tabla[kv.Key] = new RegistroIntegrada(kv.Value);
            return new CambiosTabla<RegistroIntegrada>(integ, tabla);
        }
        private void MarcarVolumen(string ruta)
        {
            if (!medidasDeltasActivas || ruta == null) return;
            if (cambiosVolumen == null || !ReferenceEquals(cambiosVolumen.Origen, loudCache)) cambiosVolumen = PreparaVolumen(loudCache, durCache);
            float r, d; if (!durCache.TryGetValue(ruta, out d)) d = 0f;
            cambiosVolumen.Marca(ruta, loudCache.TryGetValue(ruta, out r) ? new RegistroVolumen(r, d) : null);
        }
        private void MarcarEq(string ruta)
        {
            if (!medidasDeltasActivas || ruta == null) return;
            if (cambiosEq == null || !ReferenceEquals(cambiosEq.Origen, eqCache)) cambiosEq = PreparaEq(eqCache);
            float[] c; cambiosEq.Marca(ruta, eqCache.TryGetValue(ruta, out c) ? new RegistroEq(c) : null);
        }
        private void MarcarIntegrada(string ruta)
        {
            if (!medidasDeltasActivas || ruta == null) return;
            if (cambiosIntegrada == null || !ReferenceEquals(cambiosIntegrada.Origen, integCache)) cambiosIntegrada = PreparaIntegrada(integCache);
            SonoridadIntegrada s; cambiosIntegrada.Marca(ruta, integCache.TryGetValue(ruta, out s) ? new RegistroIntegrada(s) : null);
        }

        private void GuardaTablaCambios<T>(CambiosTabla<T> tabla, GuardadoReciente cola, object cerrojo,
            DiarioTabla diario, string ruta, List<string> ajenas, Func<bool> vigente,
            HuellaDe<T> huella, FilaDe<T> fila, string cabecera, Action confirmado) where T : class
        {
            List<KeyValuePair<string, T>> cambios = tabla.Copia();   // O(tanda), no O(biblioteca)
            GuardaUltimo(cola, delegate
            {
                try
                {
                    if (!vigente()) return;
                    lock (cerrojo)
                    {
                        if (!vigente()) return;
                        tabla.Aplica(cambios);
                        if (!diario.GuardaCambiosConAjenas(ruta, tabla.Trabajo, cambios, huella, fila, cabecera, ajenas, vigente)) return;
                        tabla.Confirma(cambios);
                        if (confirmado != null) confirmado();
                    }
                }
                catch { }   // los pendientes se conservan para reintentar, incluido al cerrar
            });
        }

        internal delegate long HuellaDe<T>(string ruta, T valor);
        internal delegate void FilaDe<T>(TextWriter w, string ruta, T valor);

        private sealed class DiarioTabla
        {
            private Dictionary<string, long> enArchivo;   // lo que ya dice el archivo: ruta -> huella de su medida
            private int filasArchivo;                      // lineas de registros que tiene (con las repetidas)
            private long largoArchivo = -1;                // el largo en que se dejo: si es otro, alguien lo toco
            private long escritaArchivo, creadaArchivo;
            internal int Enteros, Anadidos, SinCambios;    // cuantas veces se escribio entero, se anadio o no hizo falta
            internal long Bytes;                           // bytes escritos en total (pruebas y diagnostico)

            // Guarda la tabla (filas) en ruta. cabecera: la primera linea, si la tabla la lleva (null si no). cola:
            // lineas que se conservan tal cual al final del archivo entero (registros de una version posterior).
            // Devuelve true si el archivo quedo diciendo lo mismo que filas.
            internal bool Guarda<T>(string ruta, List<KeyValuePair<string, T>> filas, HuellaDe<T> huella, FilaDe<T> fila,
                                    string cabecera, List<string> cola, Func<bool> vigente)
            {
                lock (CandadoDe(ruta))
                {
                    if (vigente != null && !vigente()) return false;
                    try
                    {
                        bool entero = enArchivo == null || CambioArchivo(ruta);
                        List<int> cambian = null;
                        if (!entero)
                        {
                            cambian = new List<int>();
                            int conocidas = 0;
                            for (int i = 0; i < filas.Count; i++)
                            {
                                if ((i & 1023) == 0 && vigente != null && !vigente()) return false;
                                long antes;
                                if (enArchivo.TryGetValue(filas[i].Key, out antes))
                                {
                                    conocidas++;
                                    if (antes != huella(filas[i].Key, filas[i].Value)) cambian.Add(i);
                                }
                                else cambian.Add(i);
                            }
                            if (conocidas < enArchivo.Count) entero = true;   // se quito alguna: solo lo dice el archivo entero
                            else if (filasArchivo + cambian.Count > filas.Count + filas.Count / 2 + 1000 + (cola == null ? 0 : cola.Count)) entero = true;   // compactar
                        }
                        if (!entero && cambian.Count == 0) { SinCambios++; return true; }
                        if (!entero)
                        {
                            int r = Anade(ruta, filas, cambian, huella, fila, vigente);
                            if (r != 2) return r == 0;
                            // (2: el archivo cambio entre medias; se escribe entero)
                        }
                        return GuardaEntera(ruta, filas, huella, fila, cabecera, cola, vigente);
                    }
                    catch
                    {
                        enArchivo = null;   // no se sabe como quedo: la proxima, entera
                        throw;
                    }
                }
            }

            // Solo para un diccionario PROPIO del escritor serial, con todos sus cambios pendientes.
            // null es una baja. Cancelar/fallar NO consume pendientes: el llamador los conserva.
            internal bool GuardaCambios<T>(string ruta, Dictionary<string, T> actual, List<KeyValuePair<string, T>> cambios,
                HuellaDe<T> huella, FilaDe<T> fila, string cabecera, Func<bool> vigente) where T : class
            { return GuardaCambiosConAjenas(ruta, actual, cambios, huella, fila, cabecera, null, vigente); }

            internal bool GuardaCambiosConAjenas<T>(string ruta, Dictionary<string, T> actual, List<KeyValuePair<string, T>> cambios,
                HuellaDe<T> huella, FilaDe<T> fila, string cabecera, List<string> cola, Func<bool> vigente) where T : class
            {
                lock (CandadoDe(ruta))
                {
                    if (vigente != null && !vigente()) return false;
                    try
                    {
                        bool entero = enArchivo == null || CambioArchivo(ruta);
                        List<KeyValuePair<string, T>> nuevas = new List<KeyValuePair<string, T>>(cambios.Count);
                        if (!entero)
                        {
                            for (int i = 0; i < cambios.Count; i++)
                            {
                                if ((i & 127) == 0 && vigente != null && !vigente()) return false;
                                KeyValuePair<string, T> c = cambios[i]; long antes;
                                bool existe = enArchivo.TryGetValue(c.Key, out antes);
                                if (c.Value == null) { if (existe) entero = true; }
                                else if (!existe || antes != huella(c.Key, c.Value)) nuevas.Add(c);
                            }
                            if (actual.Count < enArchivo.Count || filasArchivo + nuevas.Count > actual.Count + actual.Count / 2 + 1000 + (cola == null ? 0 : cola.Count)) entero = true;
                        }
                        if (!entero && nuevas.Count == 0) { SinCambios++; return true; }
                        if (!entero)
                        {
                            List<int> indices = new List<int>(nuevas.Count);
                            for (int i = 0; i < nuevas.Count; i++) indices.Add(i);
                            int r = Anade(ruta, nuevas, indices, huella, fila, vigente);
                            if (r != 2) return r == 0;
                        }
                        // Primera vez, bajas, restauracion externa y compactacion conservan el camino seguro.
                        return GuardaEntera(ruta, new List<KeyValuePair<string, T>>(actual), huella, fila, cabecera, cola, vigente);
                    }
                    catch { enArchivo = null; throw; }
                }
            }

            private bool GuardaEntera<T>(string ruta, List<KeyValuePair<string, T>> filas, HuellaDe<T> huella, FilaDe<T> fila,
                                         string cabecera, List<string> cola, Func<bool> vigente)
            {
                bool hecho = EscribeFlujoSeguro(ruta, delegate(TextWriter w)
                {
                    if (cabecera != null) { w.Write(cabecera); w.Write("\r\n"); }
                    for (int i = 0; i < filas.Count; i++)
                    {
                        if ((i & 127) == 0 && vigente != null && !vigente()) return false;
                        fila(w, filas[i].Key, filas[i].Value);
                    }
                    if (cola != null) for (int i = 0; i < cola.Count; i++) { w.Write(cola[i]); w.Write("\r\n"); }
                    return true;
                }, vigente);
                if (!hecho) return false;   // no se toco el archivo: lo que se sabia de el sigue valiendo
                Dictionary<string, long> nuevo = new Dictionary<string, long>(filas.Count, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < filas.Count; i++) nuevo[filas[i].Key] = huella(filas[i].Key, filas[i].Value);
                enArchivo = nuevo;
                filasArchivo = filas.Count + (cola != null ? cola.Count : 0);
                ObservaArchivo(ruta);
                Enteros++;
                if (largoArchivo > 0) Bytes += largoArchivo;
                return true;
            }

            // 0 = anadido; 1 = ya no hacia falta (hay una instantanea mas nueva); 2 = el archivo no es el que se dejo.
            private int Anade<T>(string ruta, List<KeyValuePair<string, T>> filas, List<int> cambian, HuellaDe<T> huella,
                                  FilaDe<T> fila, Func<bool> vigente)
            {
                byte[] datos;
                using (MemoryStream ms = new MemoryStream(cambian.Count * 128 + 64))
                {
                    using (StreamWriter w = new StreamWriter(ms, new UTF8Encoding(false), 4096))
                    {
                        for (int i = 0; i < cambian.Count; i++)
                        {
                            KeyValuePair<string, T> f = filas[cambian[i]];
                            fila(w, f.Key, f.Value);
                        }
                    }
                    datos = ms.ToArray();
                }
                if (vigente != null && !vigente()) return 1;
                using (ProteccionRespaldo cambio = ProtegeRespaldo(ruta))
                using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Write, FileShare.Read))
                {
                    if (fs.Length != largoArchivo || CambioArchivo(ruta)) { enArchivo = null; return 2; }   // incluso mismo largo
                    fs.Position = fs.Length;
                    fs.Write(datos, 0, datos.Length);
                    fs.Flush();
                    // Igual que el guardado entero: bajado al disco de verdad, no solo a la memoria de Windows.
                    try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { }
                    largoArchivo = fs.Length;
                }
                ObservaArchivo(ruta);
                for (int i = 0; i < cambian.Count; i++)
                {
                    KeyValuePair<string, T> f = filas[cambian[i]];
                    enArchivo[f.Key] = huella(f.Key, f.Value);
                }
                filasArchivo += cambian.Count;
                Anadidos++;
                Bytes += datos.Length;
                return 0;
            }

            private bool CambioArchivo(string ruta)
            {
                try { FileInfo f = new FileInfo(ruta); return !f.Exists || f.Length != largoArchivo || f.LastWriteTimeUtc.Ticks != escritaArchivo || f.CreationTimeUtc.Ticks != creadaArchivo; }
                catch { return true; }
            }
            private void ObservaArchivo(string ruta)
            {
                FileInfo f = new FileInfo(ruta);
                largoArchivo = f.Length; escritaArchivo = f.LastWriteTimeUtc.Ticks; creadaArchivo = f.CreationTimeUtc.Ticks;
            }
        }

        private DiarioTabla diarioVolumen, diarioEq, diarioInteg;   // se crean al primer guardado (en el hilo de la pantalla)

        // Las huellas: los numeros de cada medida, bit a bit. Dos medidas con la misma huella se escriben igual.
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct BitsFloat
        {
            [System.Runtime.InteropServices.FieldOffset(0)] internal float F;
            [System.Runtime.InteropServices.FieldOffset(0)] internal int I;
        }
        private static int Bits(float v) { BitsFloat b = new BitsFloat(); b.F = v; return b.I; }   // sin crear basura por numero
        private static long HuellaVolumen(float rms, float duracion)
        {
            return ((long)Bits(rms) << 32) | (uint)Bits(duracion);
        }
        private static long Mezcla(long h, float v) { return Mezcla(h, Bits(v)); }
        private static long Mezcla(long h, int v)
        {
            unchecked
            {
                for (int b = 0; b < 4; b++) { h ^= (v >> (8 * b)) & 0xFF; h *= 1099511628211L; }   // FNV-1a de 64 bits
                return h;
            }
        }
        private const long HuellaVacia = unchecked((long)14695981039346656037UL);
        private static long HuellaEq(float[] c)
        {
            if (c == null) return 0;
            long h = Mezcla(HuellaVacia, c.Length);
            for (int i = 0; i < c.Length; i++) h = Mezcla(h, c[i]);
            return h;
        }
        private static long HuellaInteg(SonoridadIntegrada s)
        {
            long h = Mezcla(HuellaVacia, s.Algoritmo);
            h = Mezcla(h, s.Lkfs); h = Mezcla(h, s.Pesos); h = Mezcla(h, s.Bloques);
            h = Mezcla(h, s.PicoDb); h = Mezcla(h, s.P99Db);
            MapaSilencios m = s.Silencios;
            h = Mezcla(h, m.Desde); h = Mezcla(h, m.Hasta); h = Mezcla(h, m.HuecoDesde); h = Mezcla(h, m.HuecoHasta); h = Mezcla(h, m.Dura);
            return h;
        }

        // AL LEER, SOLO LINEAS ENTERAS. Si el archivo no acaba en salto de linea, su ultima linea quedo a medias (un
        // apagon justo mientras se anadia) y no se lee; y una linea con ceros tampoco (Windows puede dejar ceros al
        // final si se corto la luz entre alargar el archivo y escribirlo). Lo demas se lee exactamente igual que antes.
        private sealed class LineasSanas : TextReader
        {
            private readonly TextReader r;
            private readonly bool cortada;
            private string siguiente;
            private string trozo; private int pos;   // lo que Read/Peek aun no entregaron: una linea con su salto
            internal LineasSanas(TextReader r, bool cortada) { this.r = r; this.cortada = cortada; }
            private string LineaSana()
            {
                while (true)
                {
                    string l = siguiente ?? r.ReadLine();
                    siguiente = null;
                    if (l == null) return null;
                    if (cortada)
                    {
                        siguiente = r.ReadLine();
                        if (siguiente == null) return null;   // era la ultima, y esta a medias
                    }
                    if (l.IndexOf('\0') >= 0) continue;
                    return l;
                }
            }
            public override string ReadLine()
            {
                if (trozo != null && pos < trozo.Length)   // (si antes se leyo a letras: el resto de esa linea)
                {
                    string resto = trozo.Substring(pos, trozo.Length - 1 - pos);
                    trozo = null;
                    return resto;
                }
                return LineaSana();
            }
            // Leer a letras (y ReadToEnd, que va por aqui) entrega lo mismo que ReadLine, linea a linea.
            private bool Llena()
            {
                if (trozo != null && pos < trozo.Length) return true;
                string l = LineaSana();
                if (l == null) { trozo = null; return false; }
                trozo = l + "\n"; pos = 0;
                return true;
            }
            public override int Peek() { return Llena() ? trozo[pos] : -1; }
            public override int Read() { return Llena() ? trozo[pos++] : -1; }
            protected override void Dispose(bool disposing)
            {
                try { if (disposing) r.Dispose(); }
                finally { base.Dispose(disposing); }
            }
        }
        // Mira si el archivo acaba a medias y lo deja listo para leer desde el principio.
        private static TextReader LeeLineasSanas(FileStream fs)
        {
            bool cortada = false;
            if (fs.Length > 0)
            {
                fs.Position = fs.Length - 1;
                cortada = fs.ReadByte() != (byte)'\n';
                fs.Position = 0;
            }
            return new LineasSanas(new StreamReader(fs), cortada);
        }
    }
}
