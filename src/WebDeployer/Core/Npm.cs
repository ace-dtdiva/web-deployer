using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebDeployer.Core
{
    /// <summary>Comandi npm del progetto (install, run dev, run build).</summary>
    public static class Npm
    {
        // Scritto in node_modules dopo un'installazione riuscita
        private const string InstallMarker = ".webdeployer-install";

        // Indirizzo locale stampato da Vite, es. "Local:   http://localhost:8080/"
        private static readonly Regex LocalUrl = new Regex(@"https?://(localhost|127\.0\.0\.1|\[::1\]):\d+/?", RegexOptions.IgnoreCase);

        public static bool HasPackageJson(string folder) => File.Exists(Path.Combine(folder, "package.json"));
        public static bool HasLockFile(string folder) => File.Exists(Path.Combine(folder, "package-lock.json"));

        /// <summary>True se le dipendenze non sono mai state installate o package.json / package-lock.json sono cambiati da allora.</summary>
        public static bool NeedsInstall(string folder)
        {
            var marker = Path.Combine(folder, "node_modules", InstallMarker);
            if (!File.Exists(marker)) return true;

            var installedAt = File.GetLastWriteTimeUtc(marker);
            return new[] { "package.json", "package-lock.json" }
                .Select(name => Path.Combine(folder, name))
                .Any(file => File.Exists(file) && File.GetLastWriteTimeUtc(file) > installedAt);
        }

        public static void MarkInstalled(string folder) =>
            File.WriteAllText(Path.Combine(folder, "node_modules", InstallMarker), DateTime.Now.ToString("s"));

        /// <summary>Esegue "npm &lt;args&gt;" nella cartella del progetto; npm e node si chiudono se si chiude WebDeployer.</summary>
        public static Task<int> RunAsync(string args, string folder, Action<string> onOutput, CancellationToken cancel = default) =>
            ProcessRunner.RunAsync("cmd.exe", "/d /c npm " + args, folder, onOutput, cancel, killWithApp: true);

        /// <summary>L'indirizzo locale del server di sviluppo, se la riga lo contiene.</summary>
        public static string FindLocalUrl(string line)
        {
            var match = LocalUrl.Match(line ?? "");
            return match.Success ? match.Value : null;
        }
    }
}
