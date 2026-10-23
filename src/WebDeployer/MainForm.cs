using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WebDeployer.Core;

namespace WebDeployer
{
    public class MainForm : Form
    {
        private const int MaxLogChars = 400_000;

        private readonly TextBox _log;
        private readonly ToolStripStatusLabel _status;
        private readonly Settings _settings;
        private readonly string _settingsError;

        private TextBox _repoUrl;
        private TextBox _localFolder;

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

            _settings = Settings.Load(AppPaths.SettingsFile, out _settingsError);

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
            FormClosing += (s, e) => SaveSettings();

            ResumeLayout(false);
            PerformLayout();

            Log("WebDeployer avviato.");
            if (_settingsError != null)
                Log($"settings.json illeggibile ({_settingsError}): copiato in settings.json.bak, uso le impostazioni di default.");
        }

        private TabPage BuildProjectTab()
        {
            var page = new TabPage("Progetto") { Padding = new Padding(10) };

            var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _repoUrl = new TextBox { Dock = DockStyle.Fill, Text = _settings.RepoUrl };
            _repoUrl.TextChanged += (s, e) => _localFolder.Text = AppPaths.LocalFolderFor(_repoUrl.Text);
            _repoUrl.Leave += (s, e) => SaveSettings();
            AddRow(grid, "Repository Git:", _repoUrl, null);

            _localFolder = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Text = AppPaths.LocalFolderFor(_settings.RepoUrl) };
            var openFolder = new Button { Text = "Apri", AutoSize = true };
            openFolder.Click += (s, e) => OpenFolder(_localFolder.Text);
            AddRow(grid, "Cartella locale:", _localFolder, openFolder);

            grid.Controls.Add(new Label
            {
                Text = "Incolla l'URL del repository GitHub del sito (per esempio quello collegato a Lovable).",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(3, 2, 3, 8),
            }, 1, grid.RowCount++);

            page.Controls.Add(grid);
            return page;
        }

        private static void AddRow(TableLayoutPanel grid, string label, Control field, Control button)
        {
            var row = grid.RowCount++;
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 6, 3) }, 0, row);
            grid.Controls.Add(field, 1, row);
            if (button != null) grid.Controls.Add(button, 2, row);
        }

        private void OpenFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                MessageBox.Show(this, "La cartella non esiste ancora: clona prima il repository.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Process.Start("explorer.exe", $"\"{path}\"");
        }

        private void SaveSettings()
        {
            _settings.RepoUrl = _repoUrl.Text.Trim();
            try { _settings.Save(AppPaths.SettingsFile); }
            catch (Exception ex) { Log("Impossibile salvare le impostazioni: " + ex.Message); }
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
