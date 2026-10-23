using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace WebDeployer.Core
{
    /// <summary>
    /// Elenco dei file che WebDeployer ha caricato su un server, con l'impronta (SHA-256) del contenuto.
    /// Serve a ricaricare solo i file cambiati e a eliminare solo file caricati da WebDeployer.
    /// </summary>
    [DataContract]
    public sealed class DeployManifest
    {
        /// <summary>Server e cartella a cui si riferisce l'elenco (vedi FtpTarget.Key).</summary>
        [DataMember(Order = 1)] public string Target { get; set; }
        [DataMember(Order = 2)] public string LastDeploy { get; set; }
        [DataMember(Order = 3)] public List<Entry> Files { get; set; }

        [DataContract]
        public sealed class Entry
        {
            [DataMember(Order = 1)] public string Path { get; set; }
            [DataMember(Order = 2)] public string Hash { get; set; }
        }

        /// <summary>I file noti per questo server (percorso → impronta); vuoto se l'elenco è di un altro server.</summary>
        public Dictionary<string, string> FilesFor(string target)
        {
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            if (Target != target || Files == null) return files;
            foreach (var entry in Files.Where(e => !string.IsNullOrEmpty(e?.Path)))
                files[entry.Path] = entry.Hash;
            return files;
        }

        /// <summary>Legge l'elenco; se manca o è illeggibile restituisce un elenco vuoto (= si ricarica tutto).</summary>
        public static DeployManifest Load(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return Json.Read<DeployManifest>(path) ?? new DeployManifest();
            }
            catch (Exception)
            {
                // elenco danneggiato: si ricarica tutto
            }
            return new DeployManifest();
        }

        public static void Save(string path, string target, Dictionary<string, string> files)
        {
            var manifest = new DeployManifest
            {
                Target = target,
                LastDeploy = DateTime.Now.ToString("s"),
                Files = files.OrderBy(f => f.Key, StringComparer.Ordinal)
                    .Select(f => new Entry { Path = f.Key, Hash = f.Value })
                    .ToList(),
            };
            Json.Write(path, manifest);
        }
    }
}
