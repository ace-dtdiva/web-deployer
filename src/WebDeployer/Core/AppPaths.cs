using System;
using System.IO;

namespace WebDeployer.Core
{
    /// <summary>Percorsi dell'app: tutto sta accanto all'exe, così resta portabile.</summary>
    public static class AppPaths
    {
        public static string ExeDir { get; } = Path.GetDirectoryName(typeof(AppPaths).Assembly.Location);
        public static string SettingsFile { get; } = Path.Combine(ExeDir, "settings.json");
        public static string ProjectsDir { get; } = Path.Combine(ExeDir, "projects");

        /// <summary>Cartella locale in cui clonare il repository: projects\&lt;nome-repo&gt;.</summary>
        public static string LocalFolderFor(string repoUrl)
        {
            var name = RepoNameFromUrl(repoUrl);
            return name.Length == 0 ? "" : Path.Combine(ProjectsDir, name);
        }

        /// <summary>"https://github.com/utente/sito.git" → "sito".</summary>
        public static string RepoNameFromUrl(string repoUrl)
        {
            var s = (repoUrl ?? "").Trim().TrimEnd('/', '\\');
            if (s.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - 4);

            var cut = s.LastIndexOfAny(new[] { '/', '\\', ':' });
            var name = cut >= 0 ? s.Substring(cut + 1) : s;
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim('.', ' ');
        }
    }
}
