#if ROCOLA_GPU
using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
namespace Rocola
{
    internal sealed partial class GpuPreviewStore
    {
        private sealed class Entry { internal long Offset; internal uint Crc; }
        private readonly Dictionary<ulong, Entry> index = new Dictionary<ulong, Entry>();
        private readonly Queue<ulong> indexOrder = new Queue<ulong>();
        private long indexedLength = 4;
        internal long IndexBytesRead, PixelBytesRead;
        private const int IndexMagic = 0x32495850;
        private void AddIndex(ulong key, long offset, uint crc)
        {
            if (!index.ContainsKey(key)) indexOrder.Enqueue(key);
            index[key] = new Entry { Offset = offset, Crc = crc };
            while (index.Count > capacity) { ulong drop = indexOrder.Dequeue(); index.Remove(drop); data.Remove(drop); }
        }
        private void IndexedLoad()
        {
            if (loaded) return; loaded = true;
            try
            {
                if (!File.Exists(path)) return;
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader r = new BinaryReader(fs))
                {
                    if (fs.Length > 64L * 1024 * 1024 || fs.Length < 4 || r.ReadInt32() != Magic)
                    { writable = false; return; }
                    long begin = ReadIndex(fs);
                    fs.Position = begin;
                    while (fs.Length - fs.Position >= RecordBytes)
                    {
                        long offset = fs.Position; ulong key = r.ReadUInt64(); uint crc = r.ReadUInt32(); IndexBytesRead += 12;
                        AddIndex(key, offset, crc); fs.Position += Bytes;
                    }
                    records = (fs.Length - 4) / RecordBytes; indexedLength = 4 + records * RecordBytes;
                    if (fs.Position != fs.Length) writable = false;
                    if (begin == 4 || indexedLength - begin >= 128L * RecordBytes) WriteIndex(fs);
                }
            }
            catch { writable = false; }
        }
        // Sidecar opcional: solo metadatos. Valida CRC, alineacion y extremos del archivo base.
        // Si falta o no coincide, recorrer cabeceras de 12 bytes sin decodificar previews.
        private long ReadIndex(FileStream pack)
        {
            try
            {
                string file = path + ".idx"; if (!File.Exists(file)) return 4;
                long size = new FileInfo(file).Length;
                if (size < 44 || size > 44L + 8192 * 20) return 4;
                byte[] bytes = File.ReadAllBytes(file); IndexBytesRead += bytes.Length;
                uint stored = BitConverter.ToUInt32(bytes, bytes.Length - 4);
                byte[] payload = new byte[bytes.Length - 4]; Array.Copy(bytes, payload, payload.Length);
                if (Checksum(payload) != stored) return 4;
                using (BinaryReader r = new BinaryReader(new MemoryStream(payload)))
                {
                    if (r.ReadInt32() != IndexMagic) return 4;
                    long prefix = r.ReadInt64(); int count = r.ReadInt32();
                    ulong firstKey = r.ReadUInt64(); uint firstCrc = r.ReadUInt32();
                    ulong lastKey = r.ReadUInt64(); uint lastCrc = r.ReadUInt32();
                    if (prefix < 4 || prefix > pack.Length || (prefix - 4) % RecordBytes != 0
                        || count < 0 || count > 8192 || payload.Length != 40 + count * 20) return 4;
                    List<KeyValuePair<ulong, Entry>> parsed = new List<KeyValuePair<ulong, Entry>>(count);
                    for (int i = 0; i < count; i++) {
                        ulong key = r.ReadUInt64(); long offset = r.ReadInt64(); uint crc = r.ReadUInt32();
                        if (offset < 4 || (offset - 4) % RecordBytes != 0 || offset + RecordBytes > prefix) return 4;
                        parsed.Add(new KeyValuePair<ulong, Entry>(key, new Entry { Offset = offset, Crc = crc }));
                    }
                    if (prefix > 4 && (!HeaderMatches(pack, 4, firstKey, firstCrc)
                        || !HeaderMatches(pack, prefix - RecordBytes, lastKey, lastCrc))) return 4;
                    parsed.Sort(delegate(KeyValuePair<ulong, Entry> a, KeyValuePair<ulong, Entry> b) { return a.Value.Offset.CompareTo(b.Value.Offset); });
                    for (int i = 0; i < parsed.Count; i++) AddIndex(parsed[i].Key, parsed[i].Value.Offset, parsed[i].Value.Crc);
                    return prefix;
                }
            }
            catch { index.Clear(); indexOrder.Clear(); return 4; }
        }
        private static bool HeaderMatches(FileStream fs, long offset, ulong key, uint crc)
        {
            byte[] header = new byte[12]; fs.Position = offset; int n = 0, read;
            while (n < header.Length && (read = fs.Read(header, n, header.Length - n)) > 0) n += read;
            return n == 12 && BitConverter.ToUInt64(header, 0) == key && BitConverter.ToUInt32(header, 8) == crc;
        }
        private void WriteIndex(FileStream pack)
        {
            string temp = path + ".idx.tmp." + Guid.NewGuid().ToString("N");
            try
            {
                List<KeyValuePair<ulong, Entry>> rows = new List<KeyValuePair<ulong, Entry>>(index);
                rows.Sort(delegate(KeyValuePair<ulong, Entry> a, KeyValuePair<ulong, Entry> b) { return a.Value.Offset.CompareTo(b.Value.Offset); });
                byte[] payload;
                using (MemoryStream buffer = new MemoryStream())
                using (BinaryWriter w = new BinaryWriter(buffer)) {
                    w.Write(IndexMagic); w.Write(indexedLength); w.Write(rows.Count);
                    byte[] header = new byte[12];
                    for (int i = 0; i < 2; i++) {
                        if (indexedLength > 4) {
                            pack.Position = i == 0 ? 4 : indexedLength - RecordBytes;
                            int n = 0, read; while (n < 12 && (read = pack.Read(header, n, 12 - n)) > 0) n += read;
                            if (n != 12) return;
                        }
                        w.Write(BitConverter.ToUInt64(header, 0)); w.Write(BitConverter.ToUInt32(header, 8));
                    }
                    foreach (KeyValuePair<ulong, Entry> row in rows) { w.Write(row.Key); w.Write(row.Value.Offset); w.Write(row.Value.Crc); }
                    w.Flush(); payload = buffer.ToArray();
                }
                using (BinaryWriter w = new BinaryWriter(File.Create(temp))) { w.Write(payload); w.Write(Checksum(payload)); }
                if (File.Exists(path + ".idx")) File.Replace(temp, path + ".idx", null); else File.Move(temp, path + ".idx");
            }
            catch { } // perder el indice nunca invalida el archivo ni la caratula final
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        }
        private byte[] ReadPixels(ulong key, FileStream shared)
        {
            byte[] cached; if (data.TryGetValue(key, out cached)) return cached;
            Entry entry; if (!index.TryGetValue(key, out entry)) return null;
            FileStream own = null;
            try {
                FileStream fs = shared ?? (own = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
                if (!HeaderMatches(fs, entry.Offset, key, entry.Crc)) return null;
                byte[] pixels = new byte[Bytes]; int n = 0, read;
                while (n < Bytes && (read = fs.Read(pixels, n, Bytes - n)) > 0) n += read;
                PixelBytesRead += n;
                if (n != Bytes || Checksum(pixels) != entry.Crc) return null;
                Put(key, pixels); return pixels;
            }
            catch { return null; }
            finally { if (own != null) own.Dispose(); }
        }
        internal Bitmap[] GetBatch(string[] identities)
        {
            Bitmap[] result = new Bitmap[identities.Length];
            lock (sync) {
                Load();
                List<int> sorted = new List<int>();
                for (int i = 0; i < identities.Length; i++) if (identities[i] != null && index.ContainsKey(Key(identities[i]))) sorted.Add(i);
                sorted.Sort(delegate(int a, int b) { return index[Key(identities[a])].Offset.CompareTo(index[Key(identities[b])].Offset); });
                FileStream fs = null;
                try {
                    bool needsDisk = false;
                    foreach (int i in sorted) if (!data.ContainsKey(Key(identities[i]))) { needsDisk = true; break; }
                    if (needsDisk) try { fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); } catch { }
                    foreach (int i in sorted) {
                        ulong key = Key(identities[i]);
                        if (needsDisk && fs == null && !data.ContainsKey(key)) continue; // una apertura fallida por lote, no 54 reintentos
                        byte[] pixels = ReadPixels(key, fs); if (pixels != null) result[i] = FromBytes(pixels);
                    }
                }
                catch { } finally { if (fs != null) fs.Dispose(); }
            }
            return result;
        }
        private void IndexedSave(string identity, byte[] bytes)
        {
            if (bytes == null || bytes.Length != Bytes) return;
            lock (sync) {
                Load(); ulong key = Key(identity);
                if (ReadPixels(key, null) != null) return;
                Put(key, bytes);
                try {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    if (!writable || records >= capacity + 128) { AddIndex(key, -1, Checksum(bytes)); Compact(); return; }
                    long offset;
                    using (FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (BinaryWriter w = new BinaryWriter(fs)) {
                        if (fs.Length == 0) w.Write(Magic);
                        offset = fs.Position; w.Write(key); w.Write(Checksum(bytes)); w.Write(bytes);
                    }
                    AddIndex(key, offset, Checksum(bytes)); records++; indexedLength = offset + RecordBytes;
                    if (records % 128 == 0)
                        using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) WriteIndex(fs);
                }
                catch { writable = false; }
            }
        }
        private void IndexedCompact()
        {
            string temp = path + ".tmp." + Guid.NewGuid().ToString("N");
            try {
                List<KeyValuePair<ulong, Entry>> rows = new List<KeyValuePair<ulong, Entry>>(index);
                rows.Sort(delegate(KeyValuePair<ulong, Entry> a, KeyValuePair<ulong, Entry> b) {
                    return (a.Value.Offset < 0 ? long.MaxValue : a.Value.Offset).CompareTo(b.Value.Offset < 0 ? long.MaxValue : b.Value.Offset);
                });
                Dictionary<ulong, Entry> next = new Dictionary<ulong, Entry>(); FileStream input = null;
                try {
                    if (File.Exists(path)) input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using (BinaryWriter w = new BinaryWriter(File.Create(temp))) {
                        w.Write(Magic);
                        foreach (KeyValuePair<ulong, Entry> row in rows) {
                            byte[] pixels = ReadPixels(row.Key, input); if (pixels == null) continue;
                            next[row.Key] = new Entry { Offset = w.BaseStream.Position, Crc = Checksum(pixels) };
                            w.Write(row.Key); w.Write(Checksum(pixels)); w.Write(pixels);
                        }
                    }
                } finally { if (input != null) input.Dispose(); }
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                index.Clear(); indexOrder.Clear();
                foreach (KeyValuePair<ulong, Entry> row in rows) { Entry valid; if (next.TryGetValue(row.Key, out valid)) AddIndex(row.Key, valid.Offset, valid.Crc); }
                records = index.Count; indexedLength = 4 + records * RecordBytes; writable = true;
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) WriteIndex(fs);
            }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        }
    }
}
#endif

