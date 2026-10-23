using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WebDeployer.Core
{
    /// <summary>Un file della build da caricare sul server.</summary>
    public sealed class LocalFile
    {
        /// <summary>Percorso relativo alla cartella della build, con "/" come separatore (es. "assets/index.js").</summary>
        public string RelativePath { get; private set; }
        public string FullPath { get; private set; }
        public long Length { get; private set; }

        public bool IsHtml => RelativePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase);

        public static List<LocalFile> Scan(string folder)
        {
            var root = Path.GetFullPath(folder).TrimEnd('\\') + "\\";
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(full => new LocalFile
                {
                    FullPath = full,
                    RelativePath = full.Substring(root.Length).Replace('\\', '/'),
                    Length = new FileInfo(full).Length,
                })
                .ToList();
        }
    }
}
