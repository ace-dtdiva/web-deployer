using System;

namespace WebDeployer.Core
{
    /// <summary>Dati di connessione al server FTP su cui pubblicare il sito.</summary>
    public sealed class FtpTarget
    {
        public const string EncryptionAuto = "auto";     // FTPS se il server lo supporta, altrimenti FTP
        public const string EncryptionRequired = "tls";  // solo FTPS (TLS esplicito)
        public const string EncryptionNone = "none";     // FTP non cifrato

        public string Host { get; set; } = "";
        public int Port { get; set; } = 21;
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string RemoteDir { get; set; } = "/";
        public string Encryption { get; set; } = EncryptionAuto;
        public bool AcceptAnyCertificate { get; set; }

        /// <summary>"ftp://ftp.miosito.it/" → "ftp.miosito.it"</summary>
        public static string CleanHost(string host)
        {
            var s = (host ?? "").Trim();
            var scheme = s.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0) s = s.Substring(scheme + 3);
            var slash = s.IndexOf('/');
            return slash >= 0 ? s.Substring(0, slash) : s;
        }

        /// <summary>Cartella remota con "/" come separatore e senza "/" finale; vuota diventa "/".</summary>
        public static string CleanRemoteDir(string dir)
        {
            var s = (dir ?? "").Trim().Replace('\\', '/');
            while (s.Contains("//")) s = s.Replace("//", "/");
            if (s.Length > 1) s = s.TrimEnd('/');
            return s.Length == 0 ? "/" : s;
        }

        /// <summary>Percorso remoto di un file della build, es. "assets/index.js" → "/public_html/assets/index.js".</summary>
        public string RemotePath(string relativePath) =>
            (RemoteDir == "/" ? "/" : RemoteDir + "/") + relativePath;

        /// <summary>Identifica server e cartella, per sapere se l'elenco dei file caricati vale ancora.</summary>
        public string Key => $"{User}@{Host.ToLowerInvariant()}:{Port}{RemoteDir}";

        public override string ToString() => $"{User}@{Host}:{Port} {RemoteDir}";
    }
}
