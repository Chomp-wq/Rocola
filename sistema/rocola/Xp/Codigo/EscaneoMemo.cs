using System;
using System.Collections.Generic;
using System.IO;
namespace Rocola
{
    public partial class Form1
    {
        // Solo durante el worker de biblioteca; nunca persiste entre escaneos.
        private ScanDirectoryMemo scanDirectoryMemo;
        private sealed class ScanDirectoryMemo
        {
            internal const int MaxDirectories = 2048, MaxPaths = 32768;
            internal const long MaxBytes = 8L * 1024 * 1024;
            private sealed class Entry
            {
                internal long Stamp, Bytes;
                internal string[] Files, Subdirs;
            }
            private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            internal long Bytes;
            internal int Paths, Hits, Misses;
            internal bool CanCapture(int files)
            { return entries.Count < MaxDirectories && files <= MaxPaths - Paths && Bytes < MaxBytes; }
            internal void Remember(string dir, long stamp, string[] files, string[] subdirs)
            {
                if (!CanCapture(files.Length + subdirs.Length) || entries.ContainsKey(dir)) return;
                long bytes = 192L + dir.Length * 2L;
                for (int i = 0; i < files.Length; i++) bytes += 40L + files[i].Length * 2L;
                for (int i = 0; i < subdirs.Length; i++) bytes += 40L + subdirs[i].Length * 2L;
                if (bytes > MaxBytes - Bytes) return;
                // Files ya es una copia privada. Subdirs se ordenara para la firma: conservar orden original.
                entries.Add(dir, new Entry { Stamp = stamp, Bytes = bytes, Files = files, Subdirs = (string[])subdirs.Clone() });
                Bytes += bytes; Paths += files.Length + subdirs.Length;
            }
            internal bool Take(string dir, out string[] files, out string[] subdirs)
            {
                files = subdirs = null;
                Entry entry;
                string full;
                try { full = Path.GetFullPath(dir); } catch { Misses++; return false; }
                if (!entries.TryGetValue(full, out entry)) { Misses++; return false; }
                entries.Remove(full); Bytes -= entry.Bytes; Paths -= entry.Files.Length + entry.Subdirs.Length;
                // No reutilizar nombres si hubo altas/bajas/renombres desde la enumeracion.
                try { if (Directory.GetLastWriteTimeUtc(full).Ticks != entry.Stamp) { Misses++; return false; } }
                catch { Misses++; return false; }
                files = entry.Files; subdirs = entry.Subdirs; Hits++; return true;
            }
            internal void Clear() { entries.Clear(); Bytes = 0; Paths = 0; }
        }

        private string RootsSigForScan(List<string> roots)
        {
            System.Text.StringBuilder value = new System.Text.StringBuilder();
            for (int i = 0; i < roots.Count; i++)
            {
                if (i > 0) value.Append('|');
                value.Append(RootSigCaptured(roots[i], scanDirectoryMemo));
            }
            return value.ToString();
        }
    }
}
