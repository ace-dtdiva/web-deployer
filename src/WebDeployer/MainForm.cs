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
using WebDeployer.Ui;

namespace WebDeployer
{
    // Logica della finestra principale; l'interfaccia è costruita in MainForm.Layout.cs
    public partial class MainForm : Form
    {
        private const int MaxLogChars = 400_000;

        private readonly Settings _settings;
        private readonly string _settingsError;

        // Un'operazione alla volta: i pulsanti si disattivano e "Annulla operazione" la interrompe
        private readonly List<Control> _actionButtons = new List<Control>();
        private bool _busy;
        private CancellationTokenSource _actionCancel;

        // Server di sviluppo (npm run dev): attivo finché _previewCancel non è null
        private CancellationTokenSource _previewCancel;
        private Task _previewWatch;
        private PreviewState _preview = PreviewState.Stopped;

        // Ultimo messaggio dell'app e ultima riga dei programmi esterni: in Anita friendly spiegano cosa è andato storto
        private string _lastMessage = "";
        private string _lastToolLine = "";

        private static readonly string[] EncryptionValues = { FtpTarget.EncryptionAuto, FtpTarget.EncryptionRequired, FtpTarget.EncryptionNone };
        private static readonly string[] EncryptionLabels = { "FTPS se disponibile (consigliato)", "Solo FTPS (TLS esplicito)", "FTP non cifrato" };

        private enum StatusKind { Ready, Busy, Success, Error }
        private enum PreviewState { Stopped, Starting, Running }

        public MainForm()
        {
            _settings = Settings.Load(AppPaths.SettingsFile, out _settingsError);
            BuildUi();
            ApplyMode();

            Load += async (s, e) =>
            {
                FitToScreen();
                await CheckToolsAsync();
            };
            FormClosing += (s, e) =>
            {
                // non lasciare node.exe, git o trasferimenti FTP attivi dopo la chiusura
                _previewCancel?.Cancel();
                _actionCancel?.Cancel();
                SaveSettings();
            };

            if (_settingsError != null)
                Log($"settings.json illeggibile ({_settingsError}): copiato in settings.json.bak, uso le impostazioni di default.");
        }

        /// <summary>
        /// Esegue un'operazione alla volta: disattiva i pulsanti e scrive nel log eventuali errori.
        /// In Anita friendly mostra messaggi semplici e, se qualcosa va storto, una finestra che lo spiega.
        /// </summary>
        private async Task RunActionAsync(string name, FriendlyText friendly, Func<Task<bool>> action)
        {
            if (_busy) return;
            _busy = true;
            _actionCancel = new CancellationTokenSource();
            _lastMessage = _lastToolLine = "";
            UpdateButtons();
            SetStatus(Friendly ? friendly.Busy : name + " in corso…", StatusKind.Busy);

            var ok = false;
            var cancelled = false;
            try
            {
                ok = await action();
            }
            catch (Exception ex) when (ex is OperationCanceledException || _actionCancel.IsCancellationRequested)
            {
                cancelled = true;
                Log("Operazione annullata.");
            }
            catch (Exception ex)
            {
                Log("ERRORE: " + ex.Message);
            }
            finally
            {
                _actionCancel.Dispose();
                _actionCancel = null;
                _busy = false;
                _progress.Visible = false;
                UpdateButtons();
            }

            if (!Friendly)
                SetStatus("Pronto");
            else if (ok)
                SetStatus(friendly.Done, StatusKind.Success);
            else if (cancelled)
                SetStatus("Operazione annullata.");
            else
            {
                SetStatus(friendly.Failed, StatusKind.Error);
                var technical = _lastToolLine.Length > 0 ? "\n\nDettaglio tecnico (utile a chi ti aiuta): " + _lastToolLine : "";
                MessageBox.Show(this,
                    friendly.Failed + "\n\nCosa è successo: " + _lastMessage + technical +
                    "\n\nSe non è chiaro, passa alla modalità Professional: nel log ci sono tutti i dettagli.",
                    "Anita's Website Deployer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Token dell'operazione in corso (annullato dal pulsante "Annulla operazione").</summary>
        private CancellationToken ActionToken => _actionCancel?.Token ?? CancellationToken.None;

        private void UpdateButtons()
        {
            foreach (var button in _actionButtons)
                button.Enabled = !_busy;
            _cancelButton.Enabled = _busy;
            _footerCancel.Visible = Friendly && _busy;

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

            SetBadge(_gitBadge, "Git", git.Result);
            SetBadge(_nodeBadge, "Node", node.Result);
            SetBadge(_npmBadge, "npm", npm.Result);
            if (git.Result == null)
                Log("Git non trovato. Installalo da https://git-scm.com/download/win e riavvia WebDeployer.");
            if (node.Result == null || npm.Result == null)
                Log("Node.js/npm non trovato. Installalo da https://nodejs.org/ e riavvia WebDeployer.");

            // In Anita friendly il log non si vede: lo diciamo con una finestra
            if (Friendly && (git.Result == null || node.Result == null || npm.Result == null))
            {
                SetStatus("Mancano dei programmi che servono a WebDeployer.", StatusKind.Error);
                var missing = (git.Result == null ? "\n• Git: https://git-scm.com/download/win" : "") +
                              (node.Result == null || npm.Result == null ? "\n• Node.js: https://nodejs.org/" : "");
                MessageBox.Show(this,
                    "Per funzionare, WebDeployer ha bisogno di questi programmi, che su questo PC non ci sono:" + missing +
                    "\n\nDopo averli installati, riapri WebDeployer.",
                    "Anita's Website Deployer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>La frase giusta per la modalità corrente.</summary>
        private string Words(string professional, string friendly) => Friendly ? friendly : professional;

        private async Task<bool> CloneAsync()
        {
            var url = _repoUrl.Text.Trim();
            if (!Git.IsValidUrl(url))
            {
                Log(Words("Inserisci un URL valido del repository, per esempio https://github.com/utente/sito.git",
                          "L'indirizzo non sembra giusto: deve essere come https://github.com/utente/sito"));
                return false;
            }
            SaveSettings();
            var folder = AppPaths.LocalFolderFor(url);

            if (Git.IsRepo(folder))
            {
                var remote = await Git.RemoteUrlAsync(folder);
                if (!Git.SameRemote(remote, url))
                {
                    Log($"La cartella {folder} contiene un altro repository ({remote}). Rinominala o cancellala e riprova.");
                    return false;
                }
                Log("Repository già clonato: lo aggiorno.");
                return await PullAsync();
            }
            if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Log($"La cartella {folder} esiste già e non è un repository Git: rinominala o cancellala e riprova.");
                return false;
            }

            Log($"git clone {url}");
            var code = await Git.CloneAsync(url, folder, LogTool, ActionToken);
            Log(code == 0 ? $"Clone completato in {folder}" : $"Clone fallito (codice {code}).");
            return code == 0;
        }

        /// <summary>Esegue git pull nella cartella del progetto. Restituisce true se è andato a buon fine.</summary>
        private async Task<bool> PullAsync()
        {
            var folder = _localFolder.Text;
            if (!Git.IsRepo(folder))
            {
                Log(NotClonedMessage);
                return false;
            }
            Log("git pull");
            var code = await Git.PullAsync(folder, LogTool, ActionToken);
            Log(code == 0 ? "Aggiornamento completato." : $"git pull fallito (codice {code}).");
            return code == 0;
        }

        private string NotClonedMessage => Words("Il repository non è ancora clonato: premi Clona.",
                                                 "Il sito non è ancora sul PC: premi «Scarica il sito».");

        private string NoBuildMessage => Words("La build non esiste ancora: premi «Solo build» o «Build e carica».",
                                               "Il sito non è ancora stato costruito: premi «Costruisci il sito».");

        /// <summary>Controlla che il progetto sia clonato e sia un progetto Node.</summary>
        private bool ProjectReady(string folder)
        {
            if (!Git.IsRepo(folder))
            {
                Log(NotClonedMessage);
                return false;
            }
            if (!Npm.HasPackageJson(folder))
            {
                Log(Words("package.json non trovato: la cartella non sembra un progetto Node/Vite.",
                          "Questa cartella non sembra contenere un sito da costruire (manca package.json)."));
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
                code = await Npm.RunAsync("ci", folder, LogTool, ActionToken);
                if (code != 0) Log("npm ci non riuscito: riprovo con npm install.");
            }
            if (code != 0)
            {
                Log("npm install (installazione delle dipendenze: la prima volta può richiedere qualche minuto)");
                code = await Npm.RunAsync("install --no-package-lock", folder, LogTool, ActionToken);
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

        private async Task<bool> StartPreviewAsync()
        {
            var folder = _localFolder.Text;
            if (!ProjectReady(folder) || !await EnsureInstalledAsync(folder)) return false;

            var cancel = new CancellationTokenSource();
            _previewCancel = cancel;
            SetPreviewState(PreviewState.Starting);
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
            return true;
        }

        private async Task StopPreviewAsync()
        {
            _previewCancel?.Cancel();
            if (_previewWatch != null) await _previewWatch;
        }

        private void OnPreviewReady(string url)
        {
            if (_previewCancel == null) return; // fermata nel frattempo
            _previewLink.Text = url;
            SetPreviewState(PreviewState.Running);
            Log("Preview pronta: " + url);
            if (Friendly && !_busy) SetStatus("L'anteprima è aperta nel browser.", StatusKind.Success);
            OpenBrowser(url);
        }

        /// <summary>Aspetta la chiusura del server di sviluppo e riattiva i pulsanti.</summary>
        private async Task WatchPreviewAsync(Task<int> server, CancellationTokenSource cancel)
        {
            int code;
            try { code = await server; }
            catch (OperationCanceledException) { code = -1; } // fermata con Stop
            catch (Exception ex) { Log("ERRORE preview: " + ex.Message); code = -1; }

            var stoppedByUser = cancel.IsCancellationRequested;
            if (_previewCancel == cancel) _previewCancel = null;
            cancel.Dispose();
            if (IsDisposed) return;

            Log(stoppedByUser ? "Preview fermata." : $"Il server di preview si è chiuso (codice {code}).");
            _previewLink.Text = "";
            SetPreviewState(PreviewState.Stopped);
            if (Friendly && !_busy)
                SetStatus(stoppedByUser ? "Anteprima chiusa." : "L'anteprima si è chiusa da sola.", stoppedByUser ? StatusKind.Ready : StatusKind.Error);
            UpdateButtons();
        }

        /// <summary>git pull (se richiesto), dipendenze, npm run build. Restituisce true se la build è pronta.</summary>
        private async Task<bool> BuildAsync()
        {
            var folder = _localFolder.Text;
            if (!ProjectReady(folder)) return false;

            if (_pullBeforeBuild.Checked && !await PullAsync())
            {
                Log(Words("Build annullata perché git pull non è riuscito (togli la spunta «Esegui git pull prima del build» per buildare comunque).",
                          "Non è stato possibile scaricare le ultime modifiche, quindi il sito non è stato costruito. " +
                          "Puoi togliere la spunta «Prima scarica le ultime modifiche da GitHub» e riprovare."));
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
            var code = await Npm.RunAsync("run build", folder, LogTool, ActionToken);
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

        /// <summary>Pulsante "Build e carica": build e, se riesce, caricamento via FTP.</summary>
        private async Task<bool> BuildAndDeployAsync() => await BuildAsync() && await DeployAsync();

        /// <summary>Legge i dati FTP dalla scheda; chiede la password se manca. Null se i dati non bastano.</summary>
        private FtpTarget ReadFtpTarget()
        {
            SaveSettings();
            var host = FtpTarget.CleanHost(_ftpHost.Text);
            var user = _ftpUser.Text.Trim();
            if (host.Length == 0 || user.Length == 0)
            {
                if (Friendly)
                    Log("Mancano i dati del server su cui mettere il sito: vanno inseriti in modalità Professional, nella scheda FTP.");
                else
                {
                    Log("Compila server e utente nella scheda FTP.");
                    ShowPage(ftp: true);
                }
                return null;
            }

            if (_ftpPassword.Text.Length == 0)
            {
                var password = PasswordPrompt.Ask(this, Friendly
                    ? "Per mettere il sito online serve la password del server:"
                    : $"Password FTP per {user}@{host}:");
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

        private async Task<bool> TestFtpAsync()
        {
            var target = ReadFtpTarget();
            if (target == null) return false;

            Log($"Test connessione FTP: {target}");
            try
            {
                await FtpDeployer.TestAsync(target, Log, ActionToken);
                Log("Connessione FTP riuscita.");
                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Log("Connessione FTP non riuscita: " + FtpDeployer.Explain(ex));
                return false;
            }
        }

        /// <summary>
        /// Carica la build sul server FTP: solo i file cambiati, oppure tutti con uploadAll.
        /// Restituisce true se il caricamento è completo.
        /// </summary>
        private async Task<bool> DeployAsync(bool uploadAll = false)
        {
            SetStatus(Friendly ? "Metto il sito online…" : "Caricamento FTP in corso…", StatusKind.Busy);
            var output = OutputFolder(_localFolder.Text);
            if (!File.Exists(Path.Combine(output, "index.html")))
            {
                Log(NoBuildMessage);
                return false;
            }
            var target = ReadFtpTarget();
            if (target == null) return false;

            Log($"Caricamento via FTP di {output} su {target}" + (uploadAll ? " (tutti i file)" : ""));
            _progress.Value = 0;
            _progress.Visible = true;
            var progress = new Progress<double>(p => _progress.Value = (int)(p * 100));
            try
            {
                var timer = Stopwatch.StartNew();
                // Elenco dei file caricati, accanto alla cartella del progetto (es. projects\sito.deploy.json)
                var manifest = _localFolder.Text.TrimEnd('\\') + ".deploy.json";
                var result = await FtpDeployer.UploadAsync(target, output, manifest, uploadAll, _deleteObsolete.Checked,
                    Log, progress, ActionToken);
                Log($"Caricamento completato in {timer.Elapsed.TotalSeconds:0.0} s: {result}.");
                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException) && !ActionToken.IsCancellationRequested)
            {
                Log("Caricamento non riuscito: " + FtpDeployer.Explain(ex));
                return false;
            }
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
            _settings.DeleteObsoleteFiles = _deleteObsolete.Checked;
            try { _settings.Save(AppPaths.SettingsFile); }
            catch (Exception ex) { Log("Impossibile salvare le impostazioni: " + ex.Message); }
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

            if (!message.StartsWith("    ")) _lastMessage = message;
            _logEmpty.Visible = false;
            if (_log.TextLength > MaxLogChars)
                _log.Text = _log.Text.Substring(_log.TextLength - MaxLogChars / 2);
            _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        /// <summary>Riga di output di un programma esterno: rientrata per distinguerla dai messaggi dell'app.</summary>
        private void LogTool(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            _lastToolLine = line.Trim(); // dal thread del processo: è solo un riferimento a una stringa
            Log("    " + line);
        }

        private void ClearLog()
        {
            _log.Clear();
            _logEmpty.Visible = true;
        }

        private void SetStatus(string text, StatusKind kind = StatusKind.Ready)
        {
            _footerStatus.Text = text;
            _footerStatus.DotColor = kind == StatusKind.Busy ? Theme.Accent : kind == StatusKind.Error ? Theme.Danger : Theme.Success;
        }

        private void SetPreviewState(PreviewState state)
        {
            _preview = state;
            ShowPreviewState();
        }

        /// <summary>Stato dell'anteprima accanto al titolo, con le parole della modalità corrente.</summary>
        private void ShowPreviewState()
        {
            switch (_preview)
            {
                case PreviewState.Starting:
                    _previewState.Text = Friendly ? "Si sta aprendo…" : "In avvio…";
                    _previewState.DotColor = Theme.Accent;
                    break;
                case PreviewState.Running:
                    _previewState.Text = Friendly ? "Aperta" : "In esecuzione";
                    _previewState.DotColor = Theme.Success;
                    break;
                default:
                    _previewState.Text = Friendly ? "Chiusa" : "Ferma";
                    _previewState.DotColor = Theme.Idle;
                    break;
            }
        }

        private static void SetBadge(Pill badge, string name, string version)
        {
            badge.Text = version == null ? name + " mancante" : $"{name} {version}";
            badge.Fill = version == null ? Theme.DangerSoft : Theme.SuccessSoft;
            badge.ForeColor = version == null ? Theme.DangerText : Theme.SuccessText;
            badge.Invalidate();
        }

        /// <summary>Se la finestra è più grande dello schermo (es. zoom al 150%) la riduce e la centra.</summary>
        private void FitToScreen()
        {
            var area = Screen.FromControl(this).WorkingArea;
            if (Width <= area.Width && Height <= area.Height) return;
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
        }
    }
}
