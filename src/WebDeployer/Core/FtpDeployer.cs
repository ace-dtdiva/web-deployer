using System;
using System.Linq;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
using FluentFTP.Exceptions;

namespace WebDeployer.Core
{
    /// <summary>Errore con un messaggio già comprensibile per l'utente.</summary>
    public sealed class DeployException : Exception
    {
        public DeployException(string message) : base(message) { }
    }

    /// <summary>Riepilogo di un caricamento.</summary>
    public sealed class DeployResult
    {
        public int Uploaded { get; set; }
        public long UploadedBytes { get; set; }
        public int Unchanged { get; set; }
        public int Deleted { get; set; }

        public override string ToString() =>
            $"{Uploaded} file caricati ({UploadedBytes / 1024.0 / 1024.0:0.00} MB)" +
            (Unchanged > 0 ? $", {Unchanged} invariati" : "") +
            (Deleted > 0 ? $", {Deleted} eliminati dal server" : "");
    }

    /// <summary>Connessione FTP/FTPS e caricamento della build.</summary>
    public static class FtpDeployer
    {
        /// <summary>
        /// Carica tutti i file della cartella locale nella cartella remota, sovrascrivendo quelli esistenti.
        /// Le pagine .html vanno per ultime, così il sito online non punta mai a file non ancora caricati.
        /// </summary>
        public static async Task<DeployResult> UploadAsync(FtpTarget target, string localFolder, Action<string> log,
            IProgress<double> progress, CancellationToken cancel = default)
        {
            var queue = LocalFile.Scan(localFolder)
                .OrderBy(f => f.IsHtml ? 1 : 0)
                .ThenBy(f => f.RelativePath, StringComparer.Ordinal)
                .ToList();
            var totalBytes = Math.Max(1, queue.Sum(f => f.Length));
            var result = new DeployResult();

            using (var client = CreateClient(target))
            {
                await ConnectAsync(client, target, log, cancel);
                foreach (var file in queue)
                {
                    cancel.ThrowIfCancellationRequested();
                    var doneBefore = result.UploadedBytes;
                    var fileProgress = new InlineProgress<FtpProgress>(p =>
                        progress?.Report((doneBefore + p.TransferredBytes) / (double)totalBytes));

                    var status = await client.UploadFile(file.FullPath, target.RemotePath(file.RelativePath),
                        FtpRemoteExists.Overwrite, true, FtpVerify.None, fileProgress, cancel);
                    if (status == FtpStatus.Failed)
                        throw new DeployException($"caricamento di {file.RelativePath} non riuscito.");

                    result.Uploaded++;
                    result.UploadedBytes += file.Length;
                    progress?.Report(result.UploadedBytes / (double)totalBytes);
                    log("    ↑ " + file.RelativePath);
                }
                await client.Disconnect(cancel);
            }
            return result;
        }

        /// <summary>IProgress che richiama subito l'azione, senza passare dal thread dell'interfaccia.</summary>
        private sealed class InlineProgress<T> : IProgress<T>
        {
            private readonly Action<T> _report;
            public InlineProgress(Action<T> report) => _report = report;
            public void Report(T value) => _report(value);
        }

        /// <summary>Si collega, verifica la cartella remota e ne conta gli elementi.</summary>
        public static async Task TestAsync(FtpTarget target, Action<string> log, CancellationToken cancel = default)
        {
            using (var client = CreateClient(target))
            {
                await ConnectAsync(client, target, log, cancel);
                var items = await client.GetListing(target.RemoteDir, cancel);
                log($"Cartella remota {target.RemoteDir}: {items.Length} elementi.");
                await client.Disconnect(cancel);
            }
        }

        private static async Task ConnectAsync(AsyncFtpClient client, FtpTarget target, Action<string> log, CancellationToken cancel)
        {
            await client.Connect(cancel);
            log(client.IsEncrypted
                ? $"Connesso a {target.Host} con FTPS (connessione cifrata)."
                : $"Connesso a {target.Host} con FTP NON cifrato.");

            if (!await client.DirectoryExists(target.RemoteDir, cancel))
                throw new DeployException($"la cartella remota \"{target.RemoteDir}\" non esiste sul server: controlla il percorso nella scheda FTP.");
        }

        private static AsyncFtpClient CreateClient(FtpTarget target)
        {
            var client = new AsyncFtpClient(target.Host, target.User, target.Password, target.Port);
            client.Config.EncryptionMode =
                target.Encryption == FtpTarget.EncryptionNone ? FtpEncryptionMode.None :
                target.Encryption == FtpTarget.EncryptionRequired ? FtpEncryptionMode.Explicit :
                FtpEncryptionMode.Auto;
            client.Config.ValidateAnyCertificate = target.AcceptAnyCertificate;
            return client;
        }

        /// <summary>Spiega un errore di connessione/caricamento in modo comprensibile.</summary>
        public static string Explain(Exception error)
        {
            for (var e = error; e != null; e = e.InnerException)
            {
                switch (e)
                {
                    case DeployException deploy:
                        return deploy.Message;
                    case FtpAuthenticationException login:
                        return $"accesso rifiutato dal server ({login.Message.Trim()}). Controlla utente e password.";
                    case FtpInvalidCertificateException _:
                    case AuthenticationException _:
                        return "il certificato FTPS del server non è valido per questo indirizzo. Se ti fidi del server " +
                               "spunta \"Accetta certificati non validi\", oppure usa l'indirizzo FTP indicato dal provider.";
                    case FtpSecurityNotAvailableException _:
                        return "il server non supporta FTPS: in Cifratura scegli \"FTPS se disponibile\" o \"FTP non cifrato\".";
                    case SocketException socket:
                        return $"server non raggiungibile ({socket.Message}). Controlla indirizzo e porta.";
                    case TimeoutException _:
                        return "il server non risponde (timeout). Controlla indirizzo e porta.";
                }
            }
            return error.Message;
        }
    }
}
