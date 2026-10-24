using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebDeployer.Core;
using WebDeployer.Ui;

namespace WebDeployer
{
    // Interfaccia della finestra principale, secondo il design "WebDeployer – redesign".
    // Ogni testo ha due versioni: Professional e Anita friendly ("for dummies").
    public partial class MainForm
    {
        private const int FriendlyMaxWidth = 720; // in Anita friendly le sezioni restano centrate, larghe al massimo così

        private readonly ToolTip _tips = new ToolTip { AutoPopDelay = 15000 };

        // Testi e suggerimenti nelle due modalità, e controlli visibili solo in Professional
        private readonly List<(Control Control, string Pro, string Friendly)> _texts = new List<(Control, string, string)>();
        private readonly List<(Control Control, string Pro, string Friendly)> _tipTexts = new List<(Control, string, string)>();
        private readonly List<Control> _proOnly = new List<Control>();

        // Intestazione, pagine, log e barra di stato
        private Segmented _modeSwitch;
        private Segmented _nav;
        private Pill _gitBadge;
        private Pill _nodeBadge;
        private Pill _npmBadge;
        private Panel _pages;
        private Control _projectPage;
        private Control _ftpPage;
        private TableLayoutPanel _main;
        private Card _logCard;
        private TextBox _log;
        private Label _logEmpty;
        private FlatButton _cancelButton;
        private FlatButton _footerCancel;
        private StatusDot _footerStatus;
        private ThinProgress _progress;

        // Pagina Progetto
        private TextBox _repoUrl;
        private TextBox _localFolder;
        private FlatButton _previewButton;
        private FlatButton _stopButton;
        private LinkLabel _previewLink;
        private StatusDot _previewState;
        private CheckBox _pullBeforeBuild;
        private ComboBox _projectKind;

        // Pagina FTP
        private TextBox _ftpHost;
        private TextBox _ftpPort;
        private TextBox _ftpUser;
        private TextBox _ftpPassword; // mai salvata su disco
        private TextBox _ftpRemoteDir;
        private ComboBox _ftpEncryption;
        private CheckBox _ftpAcceptAnyCertificate;
        private CheckBox _spaHtaccess;
        private CheckBox _deleteObsolete;

        /// <summary>Messaggi di un'operazione per la modalità Anita friendly.</summary>
        private sealed class FriendlyText
        {
            public FriendlyText(string busy, string done, string failed)
            {
                Busy = busy;
                Done = done;
                Failed = failed;
            }

            public string Busy { get; }
            public string Done { get; }
            public string Failed { get; }
        }

        private void BuildUi()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = Theme.Regular(14);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Say(this, "WebDeployer", "Anita's Website Deployer");
            Icon = LoadAppIcon();
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1100, 800);
            MinimumSize = new Size(900, 640);

            // Colonna sinistra (5 parti): le pagine, scorrevoli se la finestra è bassa; destra (6 parti): il log
            _main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(24, 18, 24, 18) };
            _main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45.45f));
            _main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54.55f));
            _main.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _pages = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(0, 0, 10, 0) };
            _ftpPage = BuildFtpPage();
            _ftpPage.Visible = false;
            _projectPage = BuildProjectPage();
            _pages.Controls.Add(_ftpPage);
            _pages.Controls.Add(_projectPage);
            _main.Controls.Add(_pages, 0, 0);
            _logCard = BuildLogCard();
            _main.Controls.Add(_logCard, 1, 0);

            Controls.Add(_main);
            Controls.Add(BuildHeader());
            Controls.Add(BuildFooter());
            Resize += (s, e) => CenterFriendlyContent();
            ResumeLayout(false);
            PerformLayout();
        }

        private Control BuildHeader()
        {
            var header = new EdgePanel(DockStyle.Bottom, Theme.Border)
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Theme.Surface,
                Padding = new Padding(24, 10, 24, 10),
            };
            var row = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, RowCount = 1 };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var logo = new PictureBox
            {
                Image = LoadLogo(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(219, 132),
                Margin = new Padding(0, 0, 28, 0),
                Anchor = AnchorStyles.Left,
                AccessibleName = "Anita's Website Deployer",
            };
            _nav = new Segmented("Progetto", "FTP") { Anchor = AnchorStyles.Left, AccessibleName = "Sezioni" };
            _nav.SelectedIndexChanged += (s, e) => ShowPage(ftp: _nav.SelectedIndex == 1);
            _proOnly.Add(_nav);

            // A destra: la modalità e, sotto, le versioni degli strumenti
            _modeSwitch = new Segmented("Anita friendly", "Professional")
            {
                Anchor = AnchorStyles.Right,
                AccessibleName = "Modalità",
                SelectedIndex = _settings.UiMode == Settings.ModeFriendly ? 0 : 1,
            };
            _modeSwitch.SelectedIndexChanged += (s, e) =>
            {
                _settings.UiMode = _modeSwitch.SelectedIndex == 0 ? Settings.ModeFriendly : Settings.ModeProfessional;
                SaveSettings();
                ApplyMode();
            };
            _gitBadge = new Pill { Text = "Git …" };
            _nodeBadge = new Pill { Text = "Node …" };
            _npmBadge = new Pill { Text = "npm …", Margin = Padding.Empty };
            var badges = Flow(_gitBadge, _nodeBadge, _npmBadge);
            badges.WrapContents = false;
            badges.Dock = DockStyle.None;
            badges.Anchor = AnchorStyles.Right;
            badges.Margin = new Padding(0, 10, 0, 0);
            _proOnly.Add(badges);
            var right = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                Margin = Padding.Empty,
            };
            right.Controls.Add(_modeSwitch);
            right.Controls.Add(badges);

            row.Controls.Add(logo, 0, 0);
            row.Controls.Add(_nav, 1, 0);
            row.Controls.Add(right, 3, 0);
            header.Controls.Add(row);
            return header;
        }

        private Control BuildFooter()
        {
            var footer = new EdgePanel(DockStyle.Top, Theme.Border)
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                BackColor = Theme.Surface,
                Padding = new Padding(24, 1, 24, 0),
            };
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var version = typeof(MainForm).Assembly.GetName().Version;
            var versionLabel = new Label
            {
                Text = $"v{version.Major}.{version.Minor}.{version.Build}",
                AutoSize = true,
                Font = Theme.Regular(12),
                ForeColor = Theme.TextDisabled,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(16, 0, 0, 0),
            };
            row.Controls.Add(versionLabel, 3, 0);

            _footerStatus = new StatusDot { Text = "Pronto", DotColor = Theme.Success, Anchor = AnchorStyles.Left, Font = Theme.Regular(13) };
            _progress = new ThinProgress { Anchor = AnchorStyles.Right, Visible = false, Margin = new Padding(12, 0, 0, 0) };
            // In Anita friendly non c'è il log: "Annulla" sta qui
            _footerCancel = new FlatButton("Annulla", ButtonKind.Secondary, IconKind.None, 26)
            {
                Anchor = AnchorStyles.Right,
                Visible = false,
                Margin = new Padding(12, 0, 0, 0),
            };
            _footerCancel.Click += (s, e) => _actionCancel?.Cancel();
            row.Controls.Add(_footerStatus, 0, 0);
            row.Controls.Add(_progress, 1, 0);
            row.Controls.Add(_footerCancel, 2, 0);
            footer.Controls.Add(row);
            return footer;
        }

        private Control BuildProjectPage()
        {
            // 1. Sorgente
            var source = NewCard();
            AddHeader(source, 1, "Sorgente", "Il tuo sito");
            var repo = new InputBox { Dock = DockStyle.Fill, Placeholder = "https://github.com/utente/sito.git" };
            _repoUrl = repo.Box;
            _repoUrl.Text = _settings.RepoUrl;
            _repoUrl.TextChanged += (s, e) => _localFolder.Text = AppPaths.LocalFolderFor(_repoUrl.Text);
            _repoUrl.Leave += (s, e) => SaveSettings();
            var clone = ActionButton("Clona", "Scarica il sito", ButtonKind.Secondary, IconKind.None, "Clone",
                new FriendlyText("Sto scaricando il sito da GitHub…", "Il sito è stato scaricato sul PC.", "Non è stato possibile scaricare il sito."),
                CloneAsync);
            clone.Margin = new Padding(10, 3, 0, 3);
            AddRow(source, "Repository Git", "Indirizzo GitHub", repo, clone);
            AddHint(source, "Incolla l'URL GitHub del sito (es. quello collegato a Lovable).",
                            "Incolla qui l'indirizzo del sito su GitHub (quello collegato a Lovable).");

            var folder = new InputBox(readOnly: true) { Dock = DockStyle.Fill, Placeholder = "Clona prima il repository" };
            _localFolder = folder.Box;
            _localFolder.Text = AppPaths.LocalFolderFor(_settings.RepoUrl);
            var open = Say(new FlatButton("Apri", ButtonKind.Secondary, IconKind.Folder), "Apri", "Apri cartella");
            open.Click += (s, e) => OpenFolder(_localFolder.Text, NotClonedMessage);
            var pull = ActionButton("", "", ButtonKind.Secondary, IconKind.Refresh, "Aggiornamento",
                new FriendlyText("Sto scaricando le ultime modifiche…", "Hai l'ultima versione del sito.", "Non è stato possibile scaricare le modifiche."),
                PullAsync);
            pull.AccessibleName = "Aggiorna (git pull)";
            SayTip(pull, "Aggiorna (git pull)", "Scarica le ultime modifiche da GitHub");
            pull.Margin = Padding.Empty;
            var folderButtons = Flow(open, pull);
            folderButtons.WrapContents = false;
            folderButtons.Margin = new Padding(10, 3, 0, 3);
            AddRow(source, "Cartella locale", "Sul tuo PC", folder, folderButtons);

            // 2. Anteprima
            var preview = NewCard();
            _previewState = new StatusDot { Anchor = AnchorStyles.Right };
            AddHeader(preview, 2, "Anteprima", "Guarda il sito", "npm run dev", _previewState);
            _previewButton = ActionButton("Avvia anteprima", "Guarda il sito sul PC", ButtonKind.Secondary, IconKind.Play, "Avvio anteprima",
                new FriendlyText("Preparo l'anteprima… la prima volta ci vuole qualche minuto.", "L'anteprima si apre nel browser.", "Non è stato possibile aprire l'anteprima."),
                StartPreviewAsync, 40);
            _stopButton = Say(new FlatButton("Stop", ButtonKind.Secondary, IconKind.Stop, 40) { Enabled = false }, "Stop", "Chiudi l'anteprima");
            _stopButton.Click += (s, e) => _previewCancel?.Cancel();
            _previewLink = new LinkLabel
            {
                AutoSize = true,
                Font = Theme.Regular(13),
                LinkColor = Theme.Accent,
                ActiveLinkColor = Theme.AccentPressed,
                Margin = new Padding(4, 11, 0, 0),
            };
            _previewLink.LinkClicked += (s, e) => OpenBrowser(_previewLink.Text);
            preview.Controls.Add(ButtonRow(_previewButton, _stopButton, _previewLink));

            // 3. Pubblica
            var publish = NewCard();
            AddHeader(publish, 3, "Pubblica", "Metti online", "npm run build");
            var online = new FriendlyText("Costruisco il sito e lo metto online…", "Fatto! Il sito è online.", "Non è stato possibile mettere il sito online.");
            var buildAndDeploy = ActionButton("Build e carica", "Costruisci e metti online", ButtonKind.Primary, IconKind.Upload, "Build e caricamento",
                online, BuildAndDeployAsync, 44);
            var buildOnly = ActionButton("Solo build", "Costruisci il sito", ButtonKind.Secondary, IconKind.None, "Build",
                new FriendlyText("Costruisco il sito…", "Il sito è pronto (non è ancora online).", "Non è stato possibile costruire il sito."),
                BuildAsync, 44);
            var uploadOnly = ActionButton("Solo FTP", "Metti il sito online", ButtonKind.Secondary, IconKind.None, "Caricamento FTP",
                new FriendlyText("Metto il sito online…", online.Done, online.Failed),
                () => DeployAsync(), 44);
            publish.Controls.Add(ButtonRow(buildAndDeploy, buildOnly, uploadOnly));
            publish.Controls.Add(new Panel { Height = 1, Dock = DockStyle.Fill, BackColor = Theme.Divider, Margin = new Padding(0, 14, 0, 10) });
            _pullBeforeBuild = Say(NewCheckBox("", _settings.PullBeforeBuild),
                "Esegui git pull prima del build", "Prima scarica le ultime modifiche da GitHub");
            _pullBeforeBuild.CheckedChanged += (s, e) => SaveSettings();
            publish.Controls.Add(_pullBeforeBuild);

            _projectKind = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 300,
                Font = Theme.Regular(14),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 10, 0, 2),
            };
            _projectKind.Items.AddRange(ProjectKindLabels);
            _projectKind.SelectedIndex = Math.Max(0, Array.IndexOf(ProjectKindValues, _settings.ProjectKind));
            _projectKind.SelectedIndexChanged += (s, e) => SaveSettings();
            _tips.SetToolTip(_projectKind,
                "I siti Lovable tengono le foto sui propri server (in src\\assets ci sono solo dei puntatori)\n" +
                "e costruiscono un sito che avrebbe bisogno di un server acceso. Per questi WebDeployer scarica\n" +
                "le foto e si fa dare le pagine già pronte, così si possono caricare via FTP.\n" +
                "Su «Automatico» decide da solo guardando la cartella del progetto.");
            _proOnly.Add(AddRow(publish, "Tipo di progetto", "Tipo di progetto", _projectKind, null));
            _proOnly.Add(AddHint(publish,
                "Serve solo per i siti Lovable: in automatico li riconosce da sé.",
                "Serve solo per i siti Lovable: in automatico li riconosce da sé."));
            var openBuild = Say(new FlatButton("Apri cartella build", ButtonKind.Link, IconKind.Folder) { Margin = new Padding(0, 8, 0, 0) },
                "Apri cartella build", "Apri il sito costruito");
            openBuild.Click += (s, e) => OpenFolder(OutputFolder(_localFolder.Text), NoBuildMessage);
            publish.Controls.Add(openBuild);

            return Stack(source, preview, publish);
        }

        private Control BuildFtpPage()
        {
            // Solo in Professional: in Anita friendly la pagina FTP non si vede
            var server = NewCard();
            AddHeader(server, 0, "Server FTP", "Server FTP");

            var host = new InputBox { Dock = DockStyle.Fill, Placeholder = "ftp.miosito.it" };
            _ftpHost = host.Box;
            _ftpHost.Text = _settings.FtpHost;
            _tips.SetToolTip(_ftpHost, "L'indirizzo FTP indicato dal provider, per esempio ftp.miosito.it");
            var port = new InputBox { Width = 70, Dock = DockStyle.None };
            _ftpPort = port.Box;
            _ftpPort.Text = _settings.FtpPort.ToString();
            var portLabel = FieldLabel("Porta");
            portLabel.Margin = new Padding(14, 0, 8, 0);
            var portGroup = Flow(portLabel, port);
            portGroup.WrapContents = false;
            AddRow(server, "Server", "Server", host, portGroup);

            var user = new InputBox { Dock = DockStyle.Fill };
            _ftpUser = user.Box;
            _ftpUser.Text = _settings.FtpUser;
            AddRow(server, "Utente", "Utente", user, null);

            var password = new InputBox(password: true) { Dock = DockStyle.Fill, Placeholder = "Non viene salvata su disco" };
            _ftpPassword = password.Box;
            _tips.SetToolTip(_ftpPassword, "La password resta in memoria finché WebDeployer è aperto; se manca te la chiede.");
            AddRow(server, "Password", "Password", password, null);

            var remote = new InputBox { Dock = DockStyle.Fill, Placeholder = "/public_html" };
            _ftpRemoteDir = remote.Box;
            _ftpRemoteDir.Text = _settings.FtpRemoteDir;
            _tips.SetToolTip(_ftpRemoteDir, "La cartella del sito sul server, come indicato dal provider.");
            AddRow(server, "Cartella remota", "Cartella remota", remote, null);
            AddHint(server, "Es. /public_html, /htdocs o /www.miosito.it", "Es. /public_html, /htdocs o /www.miosito.it");

            _ftpEncryption = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 260,
                Font = Theme.Regular(14),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 4, 0, 4),
            };
            _ftpEncryption.Items.AddRange(EncryptionLabels);
            _ftpEncryption.SelectedIndex = Math.Max(0, Array.IndexOf(EncryptionValues, _settings.FtpEncryption));
            AddRow(server, "Cifratura", "Cifratura", _ftpEncryption, null);

            _ftpAcceptAnyCertificate = NewCheckBox("Accetta certificati non validi", _settings.FtpAcceptAnyCertificate);
            _tips.SetToolTip(_ftpAcceptAnyCertificate, "Serve se il certificato FTPS del server è intestato al provider invece che al tuo dominio.");
            AddRow(server, "", "", _ftpAcceptAnyCertificate, null);

            var upload = NewCard();
            AddHeader(upload, 0, "Caricamento", "Caricamento");
            _spaHtaccess = NewCheckBox("Aggiungi .htaccess per React Router (hosting Apache)", _settings.AddSpaHtaccess);
            _spaHtaccess.CheckedChanged += (s, e) => SaveSettings();
            _tips.SetToolTip(_spaHtaccess,
                "Fa funzionare le pagine interne del sito (es. /contatti) anche se aperte da un link o ricaricate.\n" +
                "Si aggiunge solo se la build non ha già un suo .htaccess, e non sovrascrive mai\n" +
                "un .htaccess già presente sul server: in quel caso il log dice cosa aggiungere a mano.");
            upload.Controls.Add(_spaHtaccess);
            _deleteObsolete = NewCheckBox("Elimina dal server i file delle build precedenti", _settings.DeleteObsoleteFiles);
            _deleteObsolete.CheckedChanged += (s, e) => SaveSettings();
            upload.Controls.Add(_deleteObsolete);
            var deleteHint = Hint("Solo i file caricati da WebDeployer: gli altri non vengono mai toccati.");
            deleteHint.Margin = new Padding(20, 0, 0, 12);
            upload.Controls.Add(deleteHint);
            var test = ActionButton("Test connessione", "Test connessione", ButtonKind.Secondary, IconKind.None, "Test connessione",
                new FriendlyText("Provo a collegarmi al server…", "Collegamento riuscito.", "Non è stato possibile collegarsi al server."),
                TestFtpAsync);
            var uploadAll = ActionButton("Ricarica tutti i file", "Ricarica tutti i file", ButtonKind.Secondary, IconKind.Upload, "Caricamento FTP",
                new FriendlyText("Metto il sito online…", "Fatto! Il sito è online.", "Non è stato possibile mettere il sito online."),
                () => DeployAsync(uploadAll: true));
            _tips.SetToolTip(uploadAll, "\"Solo FTP\" e \"Build e carica\" inviano solo i file cambiati dall'ultimo caricamento.\n" +
                                        "Questo pulsante li ricarica tutti: utile se il server è stato modificato a mano.");
            upload.Controls.Add(ButtonRow(test, uploadAll));

            foreach (var box in new Control[] { _ftpHost, _ftpPort, _ftpUser, _ftpRemoteDir, _ftpEncryption, _ftpAcceptAnyCertificate })
                box.Leave += (s, e) => SaveSettings();

            return Stack(server, upload);
        }

        private Card BuildLogCard()
        {
            // Padding 4: i pannelli interni restano dentro gli angoli arrotondati
            var card = new Card
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.LogBackground,
                BorderColor = Color.Empty,
                Padding = new Padding(4),
                Margin = new Padding(10, 0, 0, 0),
                RowCount = 2,
            };
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var header = new EdgePanel(DockStyle.Bottom, Theme.LogBorder)
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.LogBackground,
                Padding = new Padding(14, 0, 6, 1),
                Margin = Padding.Empty,
            };
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            var title = new Label { Text = "Log", AutoSize = true, Font = Theme.Semibold(14), ForeColor = Theme.LogTitle, Anchor = AnchorStyles.Left };
            _cancelButton = new FlatButton("Annulla operazione", ButtonKind.Dark) { Enabled = false, Anchor = AnchorStyles.Right, Margin = new Padding(0, 0, 6, 0) };
            _cancelButton.Click += (s, e) =>
            {
                Log("Annullamento in corso…");
                _actionCancel?.Cancel();
            };
            var clear = new FlatButton("Pulisci", ButtonKind.Dark) { Anchor = AnchorStyles.Right, Margin = Padding.Empty };
            clear.Click += (s, e) => ClearLog();
            row.Controls.Add(title, 0, 0);
            row.Controls.Add(_cancelButton, 1, 0);
            row.Controls.Add(clear, 2, 0);
            header.Controls.Add(row);

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.LogBackground, Padding = new Padding(14, 12, 6, 8), Margin = Padding.Empty };
            _log = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.LogBackground,
                ForeColor = Theme.LogText,
                Font = Theme.Mono(12.5f),
            };
            _logEmpty = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Theme.LogBackground,
                ForeColor = Theme.LogMuted,
                Font = Theme.Mono(12.5f),
                Text = "Nessuna operazione in corso.\nL'output di git, npm e FTP comparirà qui.",
            };
            body.Controls.Add(_logEmpty); // davanti al log finché è vuoto
            body.Controls.Add(_log);

            card.Controls.Add(header, 0, 0);
            card.Controls.Add(body, 0, 1);
            return card;
        }

        // ---- Modalità Anita friendly / Professional ----

        private bool Friendly => _settings.UiMode == Settings.ModeFriendly;

        /// <summary>Applica testi e visibilità della modalità scelta.</summary>
        private void ApplyMode()
        {
            var friendly = Friendly;
            SuspendLayout();
            foreach (var (control, pro, fr) in _texts)
                control.Text = friendly ? fr : pro;
            foreach (var (control, pro, fr) in _tipTexts)
                _tips.SetToolTip(control, friendly ? fr : pro);
            foreach (var control in _proOnly)
                control.Visible = !friendly;

            if (friendly) ShowPage(ftp: false);
            _logCard.Visible = !friendly;
            _main.ColumnStyles[0].Width = friendly ? 100f : 45.45f;
            _main.ColumnStyles[1].Width = friendly ? 0f : 54.55f;
            _pages.Margin = friendly ? Padding.Empty : new Padding(0, 0, 10, 0);
            CenterFriendlyContent();
            ShowPreviewState();
            if (!_busy) SetStatus(friendly ? "Pronto. Cosa vuoi fare?" : "Pronto");
            UpdateButtons();
            ResumeLayout(true);
        }

        /// <summary>In Anita friendly le sezioni restano centrate e non troppo larghe.</summary>
        private void CenterFriendlyContent()
        {
            if (_main == null) return;
            var side = (int)(24 * Theme.Scale(this));
            if (Friendly)
                side = Math.Max(side, (_main.ClientSize.Width - (int)(FriendlyMaxWidth * Theme.Scale(this))) / 2);
            if (_main.Padding.Left != side)
                _main.Padding = new Padding(side, _main.Padding.Top, side, _main.Padding.Bottom);
        }

        private void ShowPage(bool ftp)
        {
            _projectPage.Visible = !ftp;
            _ftpPage.Visible = ftp;
            _nav.SelectedIndex = ftp ? 1 : 0;
        }

        // ---- Mattoncini dell'interfaccia ----

        /// <summary>Imposta il testo del controllo e lo ricorda nelle due versioni.</summary>
        private T Say<T>(T control, string pro, string friendly) where T : Control
        {
            control.Text = pro;
            _texts.Add((control, pro, friendly));
            return control;
        }

        private void SayTip(Control control, string pro, string friendly)
        {
            _tips.SetToolTip(control, pro);
            _tipTexts.Add((control, pro, friendly));
        }

        /// <summary>Pulsante che lancia un'operazione e viene disattivato mentre un'altra è in corso.</summary>
        private FlatButton ActionButton(string text, string friendlyText, ButtonKind kind, IconKind icon, string jobName,
            FriendlyText friendly, Func<Task<bool>> work, int minHeight = 38)
        {
            var button = Say(new FlatButton(text, kind, icon, minHeight), text, friendlyText);
            button.Click += async (s, e) => await RunActionAsync(jobName, friendly, work);
            _actionButtons.Add(button);
            return button;
        }

        /// <summary>Card bianca: una colonna di righe, alta quanto il contenuto.</summary>
        private static Card NewCard() => new Card
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 12),
        };

        /// <summary>Colonna di card; la pagina che la contiene scorre se la finestra è troppo bassa.</summary>
        private static Control Stack(params Control[] cards)
        {
            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            stack.Controls.AddRange(cards);
            return stack;
        }

        /// <summary>
        /// Intestazione della sezione: numero, titolo, comando (es. "npm run dev", solo in Professional)
        /// e un elemento a destra.
        /// </summary>
        private void AddHeader(Card card, int number, string title, string friendlyTitle, string command = null, Control right = null)
        {
            var row = Grid(4);
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.Margin = new Padding(0, 0, 0, 12);
            if (number > 0) row.Controls.Add(new NumberBadge(number) { Anchor = AnchorStyles.Left }, 0, 0);
            row.Controls.Add(Say(new Label
            {
                AutoSize = true,
                Font = Theme.Semibold(15),
                ForeColor = Theme.Text,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 10, 0),
            }, title, friendlyTitle), 1, 0);
            if (command != null)
            {
                var commandLabel = new Label { Text = command, AutoSize = true, Font = Theme.Mono(12), ForeColor = Theme.TextMuted, Anchor = AnchorStyles.Left };
                _proOnly.Add(commandLabel);
                row.Controls.Add(commandLabel, 2, 0);
            }
            if (right != null) row.Controls.Add(right, 3, 0);
            card.Controls.Add(row);
        }

        /// <summary>Riga etichetta (110 px) | campo | pulsanti. Ogni riga è una griglia a sé.</summary>
        private Control AddRow(Card card, string label, string friendlyLabel, Control field, Control buttons)
        {
            var row = Grid(3);
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.Controls.Add(Say(FieldLabel(label), label, friendlyLabel), 0, 0);
            row.Controls.Add(field, 1, 0);
            if (buttons != null) row.Controls.Add(buttons, 2, 0);
            card.Controls.Add(row);
            return row;
        }

        /// <summary>Riga di aiuto in grigio sotto il campo precedente, allineata ai campi.</summary>
        private Control AddHint(Card card, string text, string friendlyText)
        {
            var row = Grid(2);
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.Controls.Add(Say(Hint(text), text, friendlyText), 1, 0);
            card.Controls.Add(row);
            return row;
        }

        private static TableLayoutPanel Grid(int columns) => new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = columns,
            RowCount = 1,
            Margin = Padding.Empty,
        };

        private static Label Hint(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            Font = Theme.Regular(12),
            ForeColor = Theme.TextMuted,
            Margin = new Padding(0, 0, 0, 10),
        };

        private static Label FieldLabel(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            Font = Theme.Regular(13),
            ForeColor = Theme.TextLabel,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 10, 0),
        };

        private static CheckBox NewCheckBox(string text, bool isChecked) => new CheckBox
        {
            Text = text,
            Checked = isChecked,
            AutoSize = true,
            Font = Theme.Regular(13),
            ForeColor = Theme.TextLabel,
            Margin = new Padding(0, 4, 0, 4),
            Cursor = Cursors.Hand,
        };

        /// <summary>Pulsanti affiancati su una riga (senza andare a capo, così l'altezza della card è giusta).</summary>
        private static FlowLayoutPanel ButtonRow(params Control[] controls)
        {
            var row = Flow(controls);
            row.WrapContents = false;
            return row;
        }

        private static FlowLayoutPanel Flow(params Control[] controls)
        {
            var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            flow.Controls.AddRange(controls);
            return flow;
        }

        /// <summary>L'icona dell'app (la stessa dell'exe), per barra del titolo e barra delle applicazioni.</summary>
        internal static Icon LoadAppIcon()
        {
            using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("WebDeployer.app.ico"))
                return stream == null ? null : new Icon(stream);
        }

        private static Image LoadLogo()
        {
            // Lo stream resta aperto: GDI+ lo usa per leggere i fotogrammi della GIF animata
            var stream = typeof(MainForm).Assembly.GetManifestResourceStream("WebDeployer.logo.gif");
            return stream == null ? null : Image.FromStream(stream);
        }
    }
}
