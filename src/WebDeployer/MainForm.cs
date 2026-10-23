using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebDeployer.Core;

namespace WebDeployer
{
    public class MainForm : Form
    {
        private const int MaxLogChars = 400_000;

        private readonly TextBox _log;
        private readonly ToolStripStatusLabel _status;
        private readonly ToolStripStatusLabel _tools;
        private readonly Settings _settings;
        private readonly string _settingsError;

        // Pulsanti disattivati mentre è in corso un'operazione
        private readonly List<Control> _actionButtons = new List<Control>();
        private bool _busy;

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
            _tools = new ToolStripStatusLabel("Controllo Git e Node...");
            var statusStrip = new StatusStrip();
            statusStrip.Items.Add(_status);
            statusStrip.Items.Add(_tools);

            Controls.Add(split);
            Controls.Add(statusStrip);
            Load += async (s, e) =>
            {
                split.SplitterDistance = split.Height / 2;
                await CheckToolsAsync();
            };
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
            var clone = ActionButton("Clona", () => RunActionAsync("Clone", CloneAsync));
            AddRow(grid, "Repository Git:", _repoUrl, clone);

            _localFolder = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Text = AppPaths.LocalFolderFor(_settings.RepoUrl) };
            var openFolder = new Button { Text = "Apri", AutoSize = true };
            openFolder.Click += (s, e) => OpenFolder(_localFolder.Text);
            AddRow(grid, "Cartella locale:", _localFolder, openFolder);

            var pull = ActionButton("Aggiorna (git pull)", () => RunActionAsync("Aggiornamento", async () => { await PullAsync(); }));
            AddRow(grid, "", Flow(pull), null);

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

        private static FlowLayoutPanel Flow(params Control[] controls)
        {
            var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            flow.Controls.AddRange(controls);
            return flow;
        }

        /// <summary>Pulsante che lancia un'operazione e viene disattivato mentre un'altra è in corso.</summary>
        private Button ActionButton(string text, Func<Task> onClick)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += async (s, e) => await onClick();
            _actionButtons.Add(button);
            return button;
        }

        /// <summary>Esegue un'operazione alla volta: disattiva i pulsanti e scrive nel log eventuali errori.</summary>
        private async Task RunActionAsync(string name, Func<Task> action)
        {
            if (_busy) return;
            _busy = true;
            UpdateButtons();
            SetStatus(name + " in corso...");
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Log("ERRORE: " + ex.Message);
            }
            finally
            {
                _busy = false;
                UpdateButtons();
                SetStatus("Pronto");
            }
        }

        private void UpdateButtons()
        {
            foreach (var button in _actionButtons)
                button.Enabled = !_busy;
        }

        private async Task CheckToolsAsync()
        {
            var git = Tools.GitVersionAsync();
            var node = Tools.NodeVersionAsync();
            var npm = Tools.NpmVersionAsync();
            await Task.WhenAll(git, node, npm);

            _tools.Text = $"Git {git.Result ?? "MANCANTE"}  ·  Node {node.Result ?? "MANCANTE"}  ·  npm {npm.Result ?? "MANCANTE"}";
            if (git.Result == null)
                Log("Git non trovato. Installalo da https://git-scm.com/download/win e riavvia WebDeployer.");
            if (node.Result == null || npm.Result == null)
                Log("Node.js/npm non trovato. Installalo da https://nodejs.org/ e riavvia WebDeployer.");
            if (git.Result == null || node.Result == null || npm.Result == null)
                _tools.ForeColor = Color.Firebrick;
        }

        private async Task CloneAsync()
        {
            var url = _repoUrl.Text.Trim();
            if (!Git.IsValidUrl(url))
            {
                Log("Inserisci un URL valido del repository, per esempio https://github.com/utente/sito.git");
                return;
            }
            SaveSettings();
            var folder = AppPaths.LocalFolderFor(url);

            if (Git.IsRepo(folder))
            {
                var remote = await Git.RemoteUrlAsync(folder);
                if (!Git.SameRemote(remote, url))
                {
                    Log($"La cartella {folder} contiene un altro repository ({remote}). Rinominala o cancellala e riprova.");
                    return;
                }
                Log("Repository già clonato: lo aggiorno.");
                await PullAsync();
                return;
            }
            if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Log($"La cartella {folder} esiste già e non è un repository Git: rinominala o cancellala e riprova.");
                return;
            }

            Log($"git clone {url}");
            var code = await Git.CloneAsync(url, folder, LogTool);
            Log(code == 0 ? $"Clone completato in {folder}" : $"Clone fallito (codice {code}).");
        }

        /// <summary>Esegue git pull nella cartella del progetto. Restituisce true se è andato a buon fine.</summary>
        private async Task<bool> PullAsync()
        {
            var folder = _localFolder.Text;
            if (!Git.IsRepo(folder))
            {
                Log("Il repository non è ancora clonato: premi Clona.");
                return false;
            }
            Log("git pull");
            var code = await Git.PullAsync(folder, LogTool);
            Log(code == 0 ? "Aggiornamento completato." : $"git pull fallito (codice {code}).");
            return code == 0;
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

        /// <summary>Riga di output di un programma esterno: rientrata per distinguerla dai messaggi dell'app.</summary>
        private void LogTool(string line) => Log("    " + line);

        private void SetStatus(string text)
        {
            _status.Text = text;
        }
    }
}
