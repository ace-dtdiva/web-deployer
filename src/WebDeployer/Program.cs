using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WebDeployer.Core;

namespace WebDeployer
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

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

            Application.Run(new MainForm());
        }

        private static void ShowError(Exception ex)
        {
            MessageBox.Show(ex?.ToString() ?? "Errore sconosciuto", "WebDeployer - Errore",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
