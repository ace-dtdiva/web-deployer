using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WebDeployer.Core
{
    /// <summary>Operazioni Git, eseguite con il git.exe installato (così funzionano anche i login di GitHub).</summary>
    public static class Git
    {
        public static bool IsRepo(string folder) =>
            !string.IsNullOrEmpty(folder) && Directory.Exists(Path.Combine(folder, ".git"));

        /// <summary>Accetta URL https/ssh oppure il percorso di un repository locale.</summary>
        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url.Any(c => char.IsWhiteSpace(c) || c == '"')) return false;
            if (AppPaths.RepoNameFromUrl(url).Length == 0) return false;
            return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("git@", StringComparison.OrdinalIgnoreCase)
                || Directory.Exists(url);
        }

        public static Task<int> CloneAsync(string url, string folder, Action<string> onOutput)
        {
            var parent = Path.GetDirectoryName(folder);
            Directory.CreateDirectory(parent);
            return ProcessRunner.RunAsync("git", $"clone {Quote(url)} {Quote(folder)}", parent, onOutput);
        }

        /// <summary>git pull solo "fast-forward": non crea mai merge e si ferma se ci sono modifiche locali in conflitto.</summary>
        public static Task<int> PullAsync(string folder, Action<string> onOutput) =>
            ProcessRunner.RunAsync("git", "pull --ff-only", folder, onOutput);

        public static async Task<string> RemoteUrlAsync(string folder)
        {
            var result = await ProcessRunner.CaptureAsync("git", "remote get-url origin", folder);
            return result.ExitCode == 0 ? result.Output : null;
        }

        /// <summary>Confronta due URL ignorando maiuscole, "/" finale e ".git".</summary>
        public static bool SameRemote(string a, string b) => Normalize(a) == Normalize(b);

        private static string Normalize(string url)
        {
            var s = (url ?? "").Trim().TrimEnd('/', '\\').ToLowerInvariant();
            return s.EndsWith(".git") ? s.Substring(0, s.Length - 4) : s;
        }

        private static string Quote(string s) => "\"" + s + "\"";
    }
}
