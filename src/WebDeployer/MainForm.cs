using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
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

        // Server di sviluppo (npm run dev): attivo finché _previewCancel non è null
        private CancellationTokenSource _previewCancel;
        private Button _previewButton;
        private Button _stopButton;
        private LinkLabel _previewLink;
        private Task _previewWatch;

        private CheckBox _pullBeforeBuild;

        private TabControl _tabs;
        private TabPage _ftpPage;
        private TextBox _ftpHost;
        private TextBox _ftpPort;
        private TextBox _ftpUser;
        private TextBox _ftpPassword; // mai salvata su disco
        private TextBox _ftpRemoteDir;
        private ComboBox _ftpEncryption;
        private CheckBox _ftpAcceptAnyCertificate;

        private static readonly string[] EncryptionValues = { FtpTarget.EncryptionAuto, FtpTarget.EncryptionRequired, FtpTarget.EncryptionNone };
        private static readonly string[] EncryptionLabels = { "FTPS se disponibile (consigliato)", "Solo FTPS (TLS esplicito)", "FTP non cifrato" };

        public MainForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = "WebDeployer";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(960, 640);
            MinimumSize = new Size(820, 560);

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
            _tabs = tabs;
            tabs.TabPages.Add(BuildProjectTab());
            tabs.TabPages.Add(_ftpPage = BuildFtpTab());

            // La parte alta resta della sua altezza, il log prende lo spazio quando la finestra si ingrandisce
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, FixedPanel = FixedPanel.Panel1 };
            split.Panel1.Controls.Add(tabs);
            split.Panel1.Controls.Add(BuildLogoPanel());
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
                FitTopPanel(split, tabs);
                await CheckToolsAsync();
            };
            FormClosing += (s, e) =>
            {
                _previewCancel?.Cancel(); // non lasciare node.exe acceso dopo la chiusura
                SaveSettings();
            };

            ResumeLayout(false);
            PerformLayout();

            Log("WebDeployer avviato.");
            if (_settingsError != null)
                Log($"settings.json illeggibile ({_settingsError}): copiato in settings.json.bak, uso le impostazioni di default.");
        }

        private TabPage BuildProjectTab()
        {
            var page = new TabPage("Progetto") { Padding = new Padding(10) };

            var grid = NewFormGrid();

            _repoUrl = new TextBox { Dock = DockStyle.Fill, Text = _settings.RepoUrl };
            _repoUrl.TextChanged += (s, e) => _localFolder.Text = AppPaths.LocalFolderFor(_repoUrl.Text);
            _repoUrl.Leave += (s, e) => SaveSettings();
            var clone = ActionButton("Clona", () => RunActionAsync("Clone", CloneAsync));
            AddRow(grid, "Repository Git:", _repoUrl, clone);
            AddHint(grid, "Incolla l'URL del repository GitHub del sito (per esempio quello collegato a Lovable).");

            _localFolder = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Text = AppPaths.LocalFolderFor(_settings.RepoUrl) };
            var openFolder = new Button { Text = "Apri", AutoSize = true };
            openFolder.Click += (s, e) => OpenFolder(_localFolder.Text, "La cartella non esiste ancora: clona prima il repository.");
            AddRow(grid, "Cartella locale:", _localFolder, openFolder);

            var pull = ActionButton("Aggiorna (git pull)", () => RunActionAsync("Aggiornamento", async () => { await PullAsync(); }));
            AddRow(grid, "", Flow(pull), null);

            _previewButton = ActionButton("Preview", () => RunActionAsync("Avvio preview", StartPreviewAsync));
            _stopButton = new Button { Text = "Stop", AutoSize = true, Enabled = false };
            _stopButton.Click += (s, e) => _previewCancel?.Cancel();
            _previewLink = new LinkLabel { AutoSize = true, Margin = new Padding(12, 8, 3, 3) };
            _previewLink.LinkClicked += (s, e) => OpenBrowser(_previewLink.Text);

            var build = ActionButton("Build", () => RunActionAsync("Build", async () => { await BuildAsync(); }));
            _pullBeforeBuild = new CheckBox
            {
                Text = "git pull prima del build",
                AutoSize = true,
                Checked = _settings.PullBeforeBuild,
                Margin = new Padding(12, 7, 3, 3),
            };
            _pullBeforeBuild.CheckedChanged += (s, e) => SaveSettings();
            var openBuild = new Button { Text = "Apri cartella build", AutoSize = true, Margin = new Padding(12, 3, 3, 3) };
            openBuild.Click += (s, e) => OpenFolder(OutputFolder(_localFolder.Text), "La build non esiste ancora: premi Build.");

            var stack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            stack.Controls.Add(grid);
            stack.Controls.Add(Section("Sviluppo (npm run dev)", _previewButton, _stopButton, _previewLink));
            stack.Controls.Add(Section("Produzione (npm run build)", build, _pullBeforeBuild, openBuild));

            // Righe alte quanto il contenuto, più una riga vuota finale che prende lo spazio avanzato
            foreach (Control _ in stack.Controls) stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            stack.RowCount = stack.RowStyles.Count;

            page.Controls.Add(stack);
            return page;
        }

        private static GroupBox Section(string title, params Control[] controls)
        {
            var box = new GroupBox
            {
                Text = title,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8, 4, 8, 6),
                Margin = new Padding(3, 10, 3, 3),
            };
            box.Controls.Add(Flow(controls));
            return box;
        }

        /// <summary>Griglia etichetta | campo | pulsante.</summary>
        private static TableLayoutPanel NewFormGrid()
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            return grid;
        }

        /// <summary>Riga di aiuto in grigio sotto il campo precedente.</summary>
        private static void AddHint(TableLayoutPanel grid, string text)
        {
            grid.Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(3, 0, 3, 8),
            }, 1, grid.RowCount++);
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

            var previewRunning = _previewCancel != null;
            _previewButton.Enabled = !_busy && !previewRunning;
            _stopButton.Enabled = previewRunning;
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

        /// <summary>Controlla che il progetto sia clonato e sia un progetto Node.</summary>
        private bool ProjectReady(string folder)
        {
            if (!Git.IsRepo(folder))
            {
                Log("Il repository non è ancora clonato: premi Clona.");
                return false;
            }
            if (!Npm.HasPackageJson(folder))
            {
                Log("package.json non trovato: la cartella non sembra un progetto Node/Vite.");
                return false;
            }
            return true;
        }

        /// <summary>Installa le dipendenze se mancano o sono cambiate.</summary>
        private async Task<bool> EnsureInstalledAsync(string folder)
        {
            if (!Npm.NeedsInstall(folder)) return true;

            // npm ci installa esattamente le versioni del package-lock.json senza modificare i file del repo;
            // se il lock non è allineato a package.json si ripiega su npm install (sempre senza scrivere il lock)
            var code = -1;
            if (Npm.HasLockFile(folder))
            {
                Log("npm ci (installazione delle dipendenze: la prima volta può richiedere qualche minuto)");
                code = await Npm.RunAsync("ci", folder, LogTool);
                if (code != 0) Log("npm ci non riuscito: riprovo con npm install.");
            }
            if (code != 0)
            {
                Log("npm install (installazione delle dipendenze: la prima volta può richiedere qualche minuto)");
                code = await Npm.RunAsync("install --no-package-lock", folder, LogTool);
            }
            if (code != 0)
            {
                Log($"Installazione delle dipendenze fallita (codice {code}).");
                return false;
            }

            Npm.MarkInstalled(folder);
            Log("Dipendenze installate.");
            return true;
        }

        private async Task StartPreviewAsync()
        {
            var folder = _localFolder.Text;
            if (!ProjectReady(folder) || !await EnsureInstalledAsync(folder)) return;

            var cancel = new CancellationTokenSource();
            _previewCancel = cancel;
            UpdateButtons();

            Log("npm run dev");
            var browserOpened = 0;
            var server = Npm.RunAsync("run dev", folder, line =>
            {
                LogTool(line);
                var url = Npm.FindLocalUrl(line);
                if (url != null && Interlocked.Exchange(ref browserOpened, 1) == 0)
                    RunOnUi(() => OnPreviewReady(url));
            }, cancel.Token);

            _previewWatch = WatchPreviewAsync(server, cancel);
        }

        private async Task StopPreviewAsync()
        {
            _previewCancel?.Cancel();
            if (_previewWatch != null) await _previewWatch;
        }

        /// <summary>git pull (se richiesto), dipendenze, npm run build. Restituisce true se la build è pronta.</summary>
        private async Task<bool> BuildAsync()
        {
            var folder = _localFolder.Text;
            if (!ProjectReady(folder)) return false;

            if (_pullBeforeBuild.Checked && !await PullAsync())
            {
                Log("Build annullata perché git pull non è riuscito (togli la spunta \"git pull prima del build\" per buildare comunque).");
                return false;
            }

            // npm ci svuota node_modules: con il server di sviluppo acceso fallirebbe
            if (Npm.NeedsInstall(folder) && _previewCancel != null)
            {
                Log("Le dipendenze sono cambiate: fermo la preview per reinstallarle.");
                await StopPreviewAsync();
            }
            if (!await EnsureInstalledAsync(folder)) return false;

            var output = OutputFolder(folder);
            Log("npm run build");
            var timer = Stopwatch.StartNew();
            var code = await Npm.RunAsync("run build", folder, LogTool);
            if (code != 0)
            {
                Log($"Build fallita (codice {code}).");
                return false;
            }
            if (!File.Exists(Path.Combine(output, "index.html")))
            {
                Log($"La build è terminata ma {Path.Combine(output, "index.html")} non esiste: controlla la cartella di output.");
                return false;
            }

            var files = Directory.GetFiles(output, "*", SearchOption.AllDirectories);
            var megabytes = files.Sum(file => new FileInfo(file).Length) / 1024.0 / 1024.0;
            Log($"Build completata in {timer.Elapsed.TotalSeconds:0.0} s: {files.Length} file, {megabytes:0.00} MB in {output}");
            return true;
        }

        private void OnPreviewReady(string url)
        {
            if (_previewCancel == null) return; // fermata nel frattempo
            _previewLink.Text = url;
            Log("Preview pronta: " + url);
            OpenBrowser(url);
        }

        /// <summary>Aspetta la chiusura del server di sviluppo e riattiva i pulsanti.</summary>
        private async Task WatchPreviewAsync(Task<int> server, CancellationTokenSource cancel)
        {
            int code;
            try { code = await server; }
            catch (Exception ex) { Log("ERRORE preview: " + ex.Message); code = -1; }

            var stoppedByUser = cancel.IsCancellationRequested;
            if (_previewCancel == cancel) _previewCancel = null;
            cancel.Dispose();
            if (IsDisposed) return;

            Log(stoppedByUser ? "Preview fermata." : $"Il server di preview si è chiuso (codice {code}).");
            _previewLink.Text = "";
            UpdateButtons();
        }

        private void OpenBrowser(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { Log($"Impossibile aprire il browser ({ex.Message}). Apri a mano: {url}"); }
        }

        /// <summary>Esegue un'azione sul thread dell'interfaccia (da usare nei callback dei processi).</summary>
        private void RunOnUi(Action action)
        {
            if (IsDisposed) return;
            try { BeginInvoke(action); }
            catch (InvalidOperationException) { } // finestra in chiusura
        }

        private void OpenFolder(string path, string missingMessage)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                MessageBox.Show(this, missingMessage, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Process.Start("explorer.exe", $"\"{path}\"");
        }

        /// <summary>Cartella prodotta dalla build (di solito "dist").</summary>
        private string OutputFolder(string projectFolder)
        {
            if (string.IsNullOrEmpty(projectFolder)) return "";
            var dir = string.IsNullOrWhiteSpace(_settings.BuildOutputDir) ? "dist" : _settings.BuildOutputDir;
            return Path.Combine(projectFolder, dir);
        }

        private void SaveSettings()
        {
            _settings.RepoUrl = _repoUrl.Text.Trim();
            _settings.PullBeforeBuild = _pullBeforeBuild.Checked;
            _settings.FtpHost = FtpTarget.CleanHost(_ftpHost.Text);
            _settings.FtpPort = int.TryParse(_ftpPort.Text.Trim(), out var port) && port > 0 && port < 65536 ? port : 21;
            _settings.FtpUser = _ftpUser.Text.Trim();
            _settings.FtpRemoteDir = FtpTarget.CleanRemoteDir(_ftpRemoteDir.Text);
            _settings.FtpEncryption = EncryptionValues[Math.Max(0, _ftpEncryption.SelectedIndex)];
            _settings.FtpAcceptAnyCertificate = _ftpAcceptAnyCertificate.Checked;
            try { _settings.Save(AppPaths.SettingsFile); }
            catch (Exception ex) { Log("Impossibile salvare le impostazioni: " + ex.Message); }
        }

        /// <summary>Alza la parte superiore quanto basta a mostrare per intero la scheda più alta.</summary>
        private void FitTopPanel(SplitContainer split, TabControl tabs)
        {
            var page = tabs.TabPages[0];
            var width = tabs.DisplayRectangle.Width - page.Padding.Horizontal;
            var content = tabs.TabPages.Cast<TabPage>()
                .SelectMany(p => p.Controls.Cast<Control>())
                .Max(c => c.GetPreferredSize(new Size(width, 0)).Height);
            var chrome = tabs.Height - tabs.DisplayRectangle.Height + page.Padding.Vertical;
            split.SplitterDistance = Math.Max(split.Panel1MinSize, Math.Min(content + chrome + 4, split.Height - 160));
        }

        private static Control BuildLogoPanel()
        {
            var logo = new PictureBox
            {
                Image = LoadLogo(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Top,
                Height = 120,
            };
            var panel = new Panel { Dock = DockStyle.Right, Width = 212, Padding = new Padding(6, 24, 6, 0) };
            panel.Controls.Add(logo);
            return panel;
        }

        private static Image LoadLogo()
        {
            // Lo stream resta aperto: GDI+ lo usa per leggere i fotogrammi della GIF animata
            var stream = typeof(MainForm).Assembly.GetManifestResourceStream("WebDeployer.logo.gif");
            return stream == null ? null : Image.FromStream(stream);
        }

        private TabPage BuildFtpTab()
        {
            var page = new TabPage("FTP") { Padding = new Padding(10) };
            var grid = NewFormGrid();
            grid.Dock = DockStyle.Top;

            _ftpHost = new TextBox { Dock = DockStyle.Fill, Text = _settings.FtpHost };
            AddRow(grid, "Server:", _ftpHost, null);
            AddHint(grid, "L'indirizzo FTP indicato dal provider, per esempio ftp.miosito.it");

            _ftpPort = new TextBox { Width = 60, Text = _settings.FtpPort.ToString() };
            AddRow(grid, "Porta:", _ftpPort, null);

            _ftpUser = new TextBox { Dock = DockStyle.Fill, Text = _settings.FtpUser };
            AddRow(grid, "Utente:", _ftpUser, null);

            _ftpPassword = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            AddRow(grid, "Password:", _ftpPassword, null);
            AddHint(grid, "La password non viene salvata: resta in memoria finché WebDeployer è aperto.");

            _ftpRemoteDir = new TextBox { Dock = DockStyle.Fill, Text = _settings.FtpRemoteDir };
            AddRow(grid, "Cartella remota:", _ftpRemoteDir, null);
            AddHint(grid, "La cartella del sito sul server, per esempio /public_html, /htdocs o /www.miosito.it");

            _ftpEncryption = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
            _ftpEncryption.Items.AddRange(EncryptionLabels);
            _ftpEncryption.SelectedIndex = Math.Max(0, Array.IndexOf(EncryptionValues, _settings.FtpEncryption));
            AddRow(grid, "Cifratura:", _ftpEncryption, null);

            _ftpAcceptAnyCertificate = new CheckBox
            {
                Text = "Accetta certificati non validi (es. intestati al provider invece che al tuo dominio)",
                AutoSize = true,
                Checked = _settings.FtpAcceptAnyCertificate,
            };
            AddRow(grid, "", _ftpAcceptAnyCertificate, null);

            var test = ActionButton("Test connessione", () => RunActionAsync("Test connessione", TestFtpAsync));
            AddRow(grid, "", Flow(test), null);

            foreach (var box in new Control[] { _ftpHost, _ftpPort, _ftpUser, _ftpRemoteDir, _ftpEncryption, _ftpAcceptAnyCertificate })
                box.Leave += (s, e) => SaveSettings();

            page.Controls.Add(grid);
            return page;
        }

        /// <summary>Legge i dati FTP dalla scheda; chiede la password se manca. Null se i dati non bastano.</summary>
        private FtpTarget ReadFtpTarget()
        {
            SaveSettings();
            var host = FtpTarget.CleanHost(_ftpHost.Text);
            var user = _ftpUser.Text.Trim();
            if (host.Length == 0 || user.Length == 0)
            {
                Log("Compila server e utente nella scheda FTP.");
                _tabs.SelectedTab = _ftpPage;
                return null;
            }

            if (_ftpPassword.Text.Length == 0)
            {
                var password = PasswordPrompt.Ask(this, $"Password FTP per {user}@{host}:");
                if (password == null)
                {
                    Log("Operazione annullata: password FTP non inserita.");
                    return null;
                }
                _ftpPassword.Text = password;
            }

            return new FtpTarget
            {
                Host = host,
                Port = _settings.FtpPort,
                User = user,
                Password = _ftpPassword.Text,
                RemoteDir = FtpTarget.CleanRemoteDir(_ftpRemoteDir.Text),
                Encryption = _settings.FtpEncryption,
                AcceptAnyCertificate = _ftpAcceptAnyCertificate.Checked,
            };
        }

        private async Task TestFtpAsync()
        {
            var target = ReadFtpTarget();
            if (target == null) return;

            Log($"Test connessione FTP: {target}");
            try
            {
                await FtpDeployer.TestAsync(target, Log);
                Log("Connessione FTP riuscita.");
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Log("Connessione FTP non riuscita: " + FtpDeployer.Explain(ex));
            }
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
        private void LogTool(string line)
        {
            if (!string.IsNullOrWhiteSpace(line)) Log("    " + line);
        }

        private void SetStatus(string text)
        {
            _status.Text = text;
        }
    }
}
