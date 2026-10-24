using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebDeployer.Core
{
    /// <summary>
    /// I siti fatti con Lovable non tengono le foto nel repository: in src\assets ci sono dei
    /// puntatori (*.asset.json) a immagini servite da Lovable all'indirizzo /__l5e/assets-v1/...
    /// La build non le scarica, quindi senza di noi il sito verrebbe pubblicato senza immagini.
    /// Le mettiamo in public\__l5e\..., da dove Vite le copia nella build senza toccarle.
    /// </summary>
    public static class LovableAssets
    {
        private const string PointerSuffix = ".asset.json";
        private const string PublicFolder = "public";

        // Da questi indirizzi Lovable serve le immagini del progetto; si prova il primo e si ripiega sul secondo
        private static readonly Func<string, string>[] Hosts =
        {
            id => "https://id-preview--" + id + ".lovable.app",
            id => "https://" + id + ".lovableproject.com",
        };

        public sealed class Pointer
        {
            public string ProjectId { get; set; }
            public string Url { get; set; }
            public string OriginalFilename { get; set; }
            public long Size { get; set; }
        }

        public static string AssetsFolder(string projectFolder) => Path.Combine(projectFolder, "src", "assets");

        /// <summary>True se il progetto ha immagini di Lovable da scaricare.</summary>
        public static bool HasPointers(string projectFolder) => PointerFiles(projectFolder).Any();

        private static IEnumerable<string> PointerFiles(string projectFolder)
        {
            var folder = AssetsFolder(projectFolder);
            if (!Directory.Exists(folder)) return Enumerable.Empty<string>();
            return Directory.GetFiles(folder, "*" + PointerSuffix, SearchOption.AllDirectories);
        }

        private static IEnumerable<Pointer> Pointers(string projectFolder)
        {
            foreach (var file in PointerFiles(projectFolder))
            {
                Pointer pointer = null;
                try { pointer = Parse(File.ReadAllText(file)); } catch { }
                if (pointer != null && !string.IsNullOrEmpty(pointer.Url) && !string.IsNullOrEmpty(pointer.ProjectId))
                    yield return pointer;
            }
        }

        // Questi file li scrive Lovable e hanno sempre la stessa forma: bastano quattro campi,
        // letti senza deserializzatore (che pretende i campi in ordine alfabetico)
        private static Pointer Parse(string json)
        {
            long size;
            long.TryParse(Field(json, "size"), out size);
            return new Pointer
            {
                ProjectId = Field(json, "project_id"),
                Url = Field(json, "url"),
                OriginalFilename = Field(json, "original_filename"),
                Size = size,
            };
        }

        private static string Field(string json, string name)
        {
            var match = Regex.Match(json, "\"" + name + "\"\\s*:\\s*\"?([^\",}]*)\"?");
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        /// <summary>Il file locale che corrisponde al puntatore, dentro public\ del progetto.</summary>
        private static string LocalFile(string projectFolder, Pointer pointer) =>
            Path.Combine(projectFolder, PublicFolder, pointer.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        /// <summary>
        /// Scarica le immagini che mancano (o che sono arrivate a metà) in public\.
        /// Non è un errore bloccante: se qualcuna non arriva il sito si pubblica lo stesso, senza quella foto.
        /// </summary>
        public static async Task EnsureDownloadedAsync(string projectFolder, Action<string> log, CancellationToken cancel = default(CancellationToken))
        {
            var pointers = Pointers(projectFolder).ToList();
            if (pointers.Count == 0) return;

            var missing = pointers.Where(p => !IsComplete(LocalFile(projectFolder, p), p.Size)).ToList();
            if (missing.Count == 0)
            {
                log($"Foto del sito: {pointers.Count} già scaricate.");
                return;
            }

            var megabytes = missing.Sum(p => p.Size) / 1024.0 / 1024.0;
            log($"Foto del sito: ne mancano {missing.Count} su {pointers.Count} ({megabytes:0.0} MB), le scarico da Lovable.");

            // TLS 1.2: .NET Framework 4.8 non lo attiva da solo su tutte le installazioni di Windows
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var failed = new List<string>();
            var done = 0;
            foreach (var pointer in missing)
            {
                cancel.ThrowIfCancellationRequested();
                if (await DownloadAsync(projectFolder, pointer, cancel))
                {
                    done++;
                    if (done % 10 == 0) log($"    {done} di {missing.Count}...");
                }
                else failed.Add(pointer.OriginalFilename ?? pointer.Url);
            }

            if (failed.Count == 0) log($"Foto scaricate: {done}.");
            else log($"Foto scaricate: {done}. Non sono riuscito a scaricarne {failed.Count} " +
                     $"({string.Join(", ", failed.Take(3))}{(failed.Count > 3 ? ", ..." : "")}): " +
                     "il sito verrà pubblicato senza quelle immagini.");
        }

        private static bool IsComplete(string file, long expectedSize)
        {
            if (!File.Exists(file)) return false;
            // size è la dimensione dichiarata dal puntatore: se non combacia il file è troncato o vecchio
            return expectedSize <= 0 || new FileInfo(file).Length == expectedSize;
        }

        private static async Task<bool> DownloadAsync(string projectFolder, Pointer pointer, CancellationToken cancel)
        {
            var destination = LocalFile(projectFolder, pointer);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var temp = destination + ".parziale";

            foreach (var host in Hosts)
            {
                cancel.ThrowIfCancellationRequested();
                try
                {
                    using (var client = new WebClient())
                    using (cancel.Register(client.CancelAsync))
                    {
                        await client.DownloadFileTaskAsync(host(pointer.ProjectId) + pointer.Url, temp);
                    }

                    if (!IsComplete(temp, pointer.Size)) { Delete(temp); continue; }
                    if (File.Exists(destination)) File.Delete(destination);
                    File.Move(temp, destination);
                    return true;
                }
                catch (Exception)
                {
                    Delete(temp);
                    cancel.ThrowIfCancellationRequested();
                }
            }
            return false;
        }

        private static void Delete(string file)
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
    }
}
