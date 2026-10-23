using System;
using System.IO;
using System.Runtime.Serialization;

namespace WebDeployer.Core
{
    /// <summary>Impostazioni salvate in settings.json accanto all'exe.</summary>
    [DataContract]
    public sealed class Settings
    {
        [DataMember(Order = 1)] public string RepoUrl { get; set; }
        [DataMember(Order = 2)] public bool PullBeforeBuild { get; set; }
        [DataMember(Order = 3)] public string BuildOutputDir { get; set; }

        // FTP (la password non viene mai salvata)
        [DataMember(Order = 4)] public string FtpHost { get; set; }
        [DataMember(Order = 5)] public int FtpPort { get; set; }
        [DataMember(Order = 6)] public string FtpUser { get; set; }
        [DataMember(Order = 7)] public string FtpRemoteDir { get; set; }
        [DataMember(Order = 8)] public string FtpEncryption { get; set; }
        [DataMember(Order = 9)] public bool FtpAcceptAnyCertificate { get; set; }
        [DataMember(Order = 11)] public bool DeleteObsoleteFiles { get; set; }

        public Settings() => SetDefaults();

        // Il deserializzatore non chiama il costruttore: i valori di default vanno impostati qui
        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) => SetDefaults();

        private void SetDefaults()
        {
            RepoUrl = "";
            PullBeforeBuild = true;
            BuildOutputDir = "dist";
            FtpHost = "";
            FtpPort = 21;
            FtpUser = "";
            FtpRemoteDir = "/";
            FtpEncryption = FtpTarget.EncryptionAuto;
            FtpAcceptAnyCertificate = false;
            DeleteObsoleteFiles = false;
        }

        /// <summary>Carica le impostazioni; se il file è illeggibile ne fa una copia .bak e riparte dai default.</summary>
        public static Settings Load(string path, out string error)
        {
            error = null;
            if (!File.Exists(path)) return new Settings();
            try
            {
                return Json.Read<Settings>(path) ?? new Settings();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                try { File.Copy(path, path + ".bak", true); } catch { }
                return new Settings();
            }
        }

        public void Save(string path) => Json.Write(path, this);
    }
}
