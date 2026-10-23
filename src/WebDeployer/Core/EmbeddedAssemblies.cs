using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace WebDeployer.Core
{
    /// <summary>Carica le DLL incorporate nell'exe come risorse (es. FluentFTP.dll).</summary>
    public static class EmbeddedAssemblies
    {
        private static readonly Dictionary<string, Assembly> Loaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        private static bool _registered;

        /// <summary>Da chiamare all'avvio, prima di usare qualsiasi tipo delle DLL incorporate.</summary>
        public static void Register()
        {
            if (_registered) return;
            _registered = true;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            lock (Loaded)
            {
                if (Loaded.TryGetValue(name, out var cached)) return cached;

                using (var stream = typeof(EmbeddedAssemblies).Assembly.GetManifestResourceStream(name + ".dll"))
                {
                    if (stream == null) return null;
                    var buffer = new MemoryStream();
                    stream.CopyTo(buffer);
                    var assembly = Assembly.Load(buffer.ToArray());
                    Loaded[name] = assembly;
                    return assembly;
                }
            }
        }
    }
}
