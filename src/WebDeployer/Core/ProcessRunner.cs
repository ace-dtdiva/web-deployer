using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebDeployer.Core
{
    /// <summary>Esegue programmi esterni (git, npm) inoltrando l'output riga per riga.</summary>
    public static class ProcessRunner
    {
        private static readonly Regex AnsiCodes = new Regex(@"\x1B\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);

        /// <summary>
        /// Avvia il processo e attende che termini, restituendo il codice di uscita.
        /// Annullando il token si chiude l'intero albero di processi (es. cmd → npm → node)
        /// e viene lanciata OperationCanceledException.
        /// </summary>
        public static Task<int> RunAsync(string fileName, string arguments, string workingDir,
            Action<string> onOutput, CancellationToken cancel = default)
        {
            return Task.Run(() =>
            {
                using (var p = new Process { StartInfo = CreateStartInfo(fileName, arguments, workingDir) })
                {
                    DataReceivedEventHandler forward = (s, e) =>
                    {
                        if (e.Data != null) onOutput?.Invoke(AnsiCodes.Replace(e.Data, ""));
                    };
                    p.OutputDataReceived += forward;
                    p.ErrorDataReceived += forward;

                    p.Start();
                    // Nessun input: un programma che chiede qualcosa riceve "fine input" invece di restare bloccato
                    p.StandardInput.Close();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    using (cancel.Register(() => KillTree(p.Id)))
                        p.WaitForExit();
                    cancel.ThrowIfCancellationRequested();
                    return p.ExitCode;
                }
            });
        }

        /// <summary>Come RunAsync, ma raccoglie tutto l'output in una stringa.</summary>
        public static async Task<(int ExitCode, string Output)> CaptureAsync(string fileName, string arguments, string workingDir = null)
        {
            var output = new StringBuilder();
            var code = await RunAsync(fileName, arguments, workingDir, line => { lock (output) output.AppendLine(line); });
            return (code, output.ToString().Trim());
        }

        /// <summary>Chiude il processo e tutti i suoi figli.</summary>
        public static void KillTree(int pid)
        {
            try
            {
                var psi = new ProcessStartInfo("taskkill", $"/PID {pid} /T /F") { UseShellExecute = false, CreateNoWindow = true };
                using (var kill = Process.Start(psi))
                    kill?.WaitForExit(5000);
            }
            catch (Exception)
            {
                // il processo è già terminato
            }
        }

        private static ProcessStartInfo CreateStartInfo(string fileName, string arguments, string workingDir)
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = workingDir ?? "",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            // Output senza colori, niente richieste interattive nel terminale, niente browser aperti da Vite
            psi.EnvironmentVariables["NO_COLOR"] = "1";
            psi.EnvironmentVariables["FORCE_COLOR"] = "0";
            psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            psi.EnvironmentVariables["BROWSER"] = "none";
            psi.EnvironmentVariables["NPM_CONFIG_UPDATE_NOTIFIER"] = "false";
            psi.EnvironmentVariables["NPM_CONFIG_FUND"] = "false";
            return psi;
        }
    }
}
