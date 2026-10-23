using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WebDeployer.Core;

namespace WebDeployer
{
    internal static class Program
    {
        private const string ErrorLogName = "WebDeployer-errori.log";

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [STAThread]
        private static void Main()
        {
            EmbeddedAssemblies.Register();

            // Testo nitido sugli schermi ad alta risoluzione
            SetProcessDPIAware();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ShowError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowError(e.ExceptionObject as Exception);

            // Una sola copia aperta per cartella (impostazioni e progetti sono accanto all'exe)
            using (var mutex = new Mutex(true, InstanceName(), out var first))
            {
                if (!first)
                {
                    BringExistingToFront();
                    return;
                }
                Application.Run(new MainForm());
                GC.KeepAlive(mutex);
            }
        }

        private static string InstanceName()
        {
            using (var sha = SHA1.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(AppPaths.ExeDir.ToLowerInvariant()));
                return @"Local\WebDeployer_" + BitConverter.ToString(hash, 0, 8).Replace("-", "");
            }
        }

        /// <summary>Riporta in primo piano la finestra di WebDeployer già aperta da questa cartella.</summary>
        private static void BringExistingToFront()
        {
            var current = Process.GetCurrentProcess();
            foreach (var other in Process.GetProcessesByName(current.ProcessName))
            {
                try
                {
                    if (other.Id == current.Id || other.MainWindowHandle == IntPtr.Zero) continue;
                    if (!string.Equals(other.MainModule?.FileName, current.MainModule?.FileName, StringComparison.OrdinalIgnoreCase)) continue;
                    ShowWindow(other.MainWindowHandle, 9); // SW_RESTORE: se era ridotta a icona
                    SetForegroundWindow(other.MainWindowHandle);
                    return;
                }
                catch (Exception)
                {
                    // processo di un altro utente o già chiuso: si passa al prossimo
                }
            }
            MessageBox.Show("WebDeployer è già aperto.", "WebDeployer", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>Errore imprevisto: i dettagli vanno nel file di log, all'utente un messaggio comprensibile.</summary>
        private static void ShowError(Exception ex)
        {
            var where = "";
            try
            {
                var path = Path.Combine(AppPaths.ExeDir, ErrorLogName);
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
                where = $"\n\nI dettagli sono stati salvati in {ErrorLogName}, accanto a WebDeployer.exe.";
            }
            catch (Exception)
            {
                where = "\n\n" + ex; // cartella non scrivibile: i dettagli si mostrano qui
            }
            MessageBox.Show($"Si è verificato un errore imprevisto:\n{ex?.Message ?? "errore sconosciuto"}{where}",
                "WebDeployer - Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
