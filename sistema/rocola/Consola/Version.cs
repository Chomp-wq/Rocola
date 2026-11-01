// LA VERSION DEL PROGRAMA. La pone GitHub al compilarlo (.github/workflows/rocola-programa.yml: -p:RocolaVersion=N y el commit); compilado
// en casa o por Buildroot es la 0 ("local"), y la primera version de GitHub la sustituye.
using System.Reflection;

namespace Rocola
{
    internal static class Version
    {
        private static string Info { get { AssemblyInformationalVersionAttribute a = typeof(Version).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>(); return a != null ? a.InformationalVersion : "0+local"; } }
        internal static string Numero { get { string i = Info; int p = i.IndexOf('+'); return p > 0 ? i.Substring(0, p) : i; } }
        internal static string Commit { get { string i = Info; int p = i.IndexOf('+'); return p > 0 ? i.Substring(p + 1) : "local"; } }
    }
}
