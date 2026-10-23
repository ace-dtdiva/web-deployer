using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WebDeployer.Core
{
    /// <summary>Verifica che Git, Node e npm siano installati.</summary>
    public static class Tools
    {
        private static readonly Regex Version = new Regex(@"\d+(\.\d+)+");

        public static Task<string> GitVersionAsync() => VersionAsync("git", "--version");
        public static Task<string> NodeVersionAsync() => VersionAsync("node", "--version");
        public static Task<string> NpmVersionAsync() => VersionAsync("cmd.exe", "/d /c npm --version");

        /// <summary>Restituisce la versione (es. "2.52.0") oppure null se il programma non c'è.</summary>
        private static async Task<string> VersionAsync(string fileName, string arguments)
        {
            try
            {
                var result = await ProcessRunner.CaptureAsync(fileName, arguments);
                if (result.ExitCode != 0) return null;
                var match = Version.Match(result.Output);
                return match.Success ? match.Value : result.Output;
            }
            catch (Exception)
            {
                return null; // programma non trovato nel PATH
            }
        }
    }
}
