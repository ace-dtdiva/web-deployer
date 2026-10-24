using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebDeployer.Core
{
    /// <summary>
    /// I template nuovi di Lovable (TanStack Start) non producono un sito di pagine già pronte:
    /// la build fa un programma che costruisce le pagine mentre il visitatore le chiede, e serve
    /// un server acceso (Cloudflare) che un hosting normale non ha.
    /// Qui costruiamo con il preset "node", avviamo quel programma sul PC, ci chiediamo tutte le
    /// pagine del sito una per una e le salviamo come file .html: il risultato è un sito statico,
    /// caricabile via FTP ovunque.
    /// </summary>
    public static class Prerender
    {
        /// <summary>Cartella con i file pubblici della build (l'equivalente di "dist" per questi progetti).</summary>
        public const string PublicDir = @".output\public";
        private const string ServerEntry = @".output\server\index.mjs";

        private const int MaxPages = 200;
        private const int ServerStartSeconds = 90;

        private static readonly Regex Links = new Regex("href=\"(/[^\"#?]*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>True se il progetto è un sito TanStack Start (i template nuovi di Lovable).</summary>
        public static bool IsTanStackStart(string projectFolder)
        {
            var file = Path.Combine(projectFolder ?? "", "package.json");
            if (!File.Exists(file)) return false;
            try { return File.ReadAllText(file).Contains("\"@tanstack/react-start\""); }
            catch { return false; }
        }

        /// <summary>
        /// Variabili d'ambiente per "npm run build": senza di queste il progetto verrebbe
        /// costruito per Cloudflare e non ci sarebbe nessun programma da avviare qui.
        /// </summary>
        public static IDictionary<string, string> BuildEnvironment(string projectFolder) =>
            IsTanStackStart(projectFolder)
                ? new Dictionary<string, string> { { "NITRO_PRESET", "node" } }
                : null;

        /// <summary>
        /// Avvia la build appena fatta, salva una pagina .html per ogni indirizzo del sito
        /// dentro la cartella pubblica e richiude tutto. Restituisce false se non ci riesce.
        /// </summary>
        public static async Task<bool> RunAsync(string projectFolder, Action<string> log, Action<string> logTool,
            CancellationToken cancel = default)
        {
            var entry = Path.Combine(projectFolder, ServerEntry);
            var output = Path.Combine(projectFolder, PublicDir);
            if (!File.Exists(entry))
            {
                log($"Non trovo {ServerEntry}: non posso generare le pagine del sito.");
                return false;
            }

            var port = FreePort();
            var address = "http://127.0.0.1:" + port;
            log("Genero le pagine del sito da pubblicare.");

            using (var stop = CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {
                var server = ProcessRunner.RunAsync("node", "\"" + entry + "\"", projectFolder, logTool, stop.Token,
                    killWithApp: true,
                    env: new Dictionary<string, string> { { "PORT", port.ToString() }, { "HOST", "127.0.0.1" }, { "NODE_ENV", "production" } });

                try
                {
                    if (!await WaitUntilReadyAsync(address, server, cancel))
                    {
                        log("Il sito non si è avviato sul PC: non posso generare le pagine.");
                        return false;
                    }

                    // su un thread a parte: le richieste HTTP sono sincrone e bloccherebbero la finestra
                    var pages = await Task.Run(() => SavePages(address, output, cancel), cancel);
                    if (pages == 0)
                    {
                        log("Nessuna pagina generata: controlla il log.");
                        return false;
                    }
                    log($"Pagine generate: {pages}.");
                    return true;
                }
                finally
                {
                    stop.Cancel();
                    try { await server; } catch (OperationCanceledException) { }
                }
            }
        }

        /// <summary>Scarica le pagine partendo dalla home e seguendo i collegamenti interni.</summary>
        private static int SavePages(string address, string output, CancellationToken cancel)
        {
            var queue = new Queue<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            queue.Enqueue("/");
            seen.Add("/");
            var saved = 0;

            while (queue.Count > 0 && seen.Count <= MaxPages)
            {
                cancel.ThrowIfCancellationRequested();
                var route = queue.Dequeue();
                string html;
                if (!TryGetHtml(address + route, 60000, out html)) continue;

                File.WriteAllText(PageFile(output, route), html, new UTF8Encoding(false));
                saved++;

                foreach (Match match in Links.Matches(html))
                {
                    var link = Normalize(match.Groups[1].Value);
                    if (link == null || seen.Contains(link) || seen.Count > MaxPages) continue;
                    seen.Add(link);
                    queue.Enqueue(link);
                }
            }
            return saved;
        }

        /// <summary>"/" diventa index.html, "/contatti" diventa contatti\index.html (così Apache lo serve da solo).</summary>
        private static string PageFile(string output, string route)
        {
            var relative = route.Trim('/');
            var file = relative.Length == 0
                ? Path.Combine(output, "index.html")
                : Path.Combine(output, relative.Replace('/', Path.DirectorySeparatorChar), "index.html");
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            return file;
        }

        /// <summary>Tiene solo gli indirizzi interni che sono pagine: niente file, niente immagini.</summary>
        private static string Normalize(string link)
        {
            if (string.IsNullOrEmpty(link) || link.StartsWith("//")) return null;
            var route = "/" + link.Trim('/');
            if (route == "/") return "/";

            var last = route.Substring(route.LastIndexOf('/') + 1);
            if (last.Contains(".")) return null;          // è un file (.js, .jpg, .ico...)
            if (route.StartsWith("/_")) return null;      // percorsi interni del framework
            return route;
        }

        private static async Task<bool> WaitUntilReadyAsync(string address, Task<int> server, CancellationToken cancel)
        {
            for (var i = 0; i < ServerStartSeconds; i++)
            {
                cancel.ThrowIfCancellationRequested();
                if (server.IsCompleted) return false;     // il programma si è chiuso da solo: c'è un errore
                if (await Task.Run(() => { string ignored; return TryGetHtml(address + "/", 5000, out ignored); }, cancel)) return true;
                await Task.Delay(1000, cancel);
            }
            return false;
        }

        private static bool TryGetHtml(string url, int timeoutMs, out string html)
        {
            html = null;
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs;
                request.UserAgent = "WebDeployer";
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK) return false;
                    if ((response.ContentType ?? "").IndexOf("text/html", StringComparison.OrdinalIgnoreCase) < 0) return false;
                    using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                        html = reader.ReadToEnd();
                    return true;
                }
            }
            catch (WebException)
            {
                return false;
            }
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
