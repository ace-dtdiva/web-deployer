using System;
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

    /// <summary>Connessione FTP/FTPS e caricamento della build.</summary>
    public static class FtpDeployer
    {
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
