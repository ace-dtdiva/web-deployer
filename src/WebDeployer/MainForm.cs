using System;
using System.Drawing;
using System.Windows.Forms;

namespace WebDeployer
{
    public class MainForm : Form
    {
        private const int MaxLogChars = 400_000;

        private readonly TextBox _log;
        private readonly ToolStripStatusLabel _status;

        public MainForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = "WebDeployer";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 640);
            MinimumSize = new Size(720, 520);

            _log = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F),
                BackColor = Color.FromArgb(24, 24, 24),
                ForeColor = Color.Gainsboro,
            };

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildProjectTab());
            tabs.TabPages.Add(BuildFtpTab());

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            split.Panel1.Controls.Add(tabs);
            split.Panel2.Controls.Add(BuildLogPanel());

            _status = new ToolStripStatusLabel("Pronto") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            var statusStrip = new StatusStrip();
            statusStrip.Items.Add(_status);

            Controls.Add(split);
            Controls.Add(statusStrip);
            Load += (s, e) => split.SplitterDistance = split.Height / 2;

            ResumeLayout(false);
            PerformLayout();

            Log("WebDeployer avviato.");
        }

        private TabPage BuildProjectTab()
        {
            var page = new TabPage("Progetto") { Padding = new Padding(10) };
            page.Controls.Add(new Label { Text = "Configurazione progetto: in arrivo.", AutoSize = true });
            return page;
        }

        private TabPage BuildFtpTab()
        {
            var page = new TabPage("FTP") { Padding = new Padding(10) };
            page.Controls.Add(new Label { Text = "Impostazioni FTP: in arrivo.", AutoSize = true });
            return page;
        }

        private Control BuildLogPanel()
        {
            var header = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
            header.Controls.Add(new Label
            {
                Text = "Log",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 7, 12, 0),
            });
            var clear = new Button { Text = "Pulisci", AutoSize = true };
            clear.Click += (s, e) => _log.Clear();
            header.Controls.Add(clear);

            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 0, 6, 6) };
            panel.Controls.Add(_log);
            panel.Controls.Add(header);
            return panel;
        }

        /// <summary>Aggiunge una riga al log. Si può chiamare da qualsiasi thread.</summary>
        public void Log(string message)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(Log), message); }
                catch (InvalidOperationException) { } // finestra in chiusura
                return;
            }

            if (_log.TextLength > MaxLogChars)
                _log.Text = _log.Text.Substring(_log.TextLength - MaxLogChars / 2);
            _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        private void SetStatus(string text)
        {
            _status.Text = text;
        }
    }
}
