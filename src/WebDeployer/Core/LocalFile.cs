using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace WebDeployer.Core
{
    /// <summary>Un file della build da caricare sul server.</summary>
    public sealed class LocalFile
    {
        /// <summary>Percorso relativo alla cartella della build, con "/" come separatore (es. "assets/index.js").</summary>
        public string RelativePath { get; private set; }
        public string FullPath { get; private set; }
        public long Length { get; private set; }

        /// <summary>Contenuto generato da WebDeployer (es. il .htaccess): in quel caso non c'è un file su disco.</summary>
        public byte[] Content { get; private set; }

        /// <summary>Impronta SHA-256 del contenuto (calcolata da ComputeHashes).</summary>
        public string Hash { get; private set; }

        public bool IsHtml => RelativePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase);

        /// <summary>File creato da WebDeployer in memoria, da caricare insieme alla build.</summary>
        public static LocalFile Generated(string relativePath, byte[] content) =>
            new LocalFile { RelativePath = relativePath, Content = content, Length = content.Length };

        public static void ComputeHashes(IEnumerable<LocalFile> files, CancellationToken cancel = default)
        {
            var buffer = new byte[1 << 20];
            foreach (var file in files)
            {
                using (var sha = SHA256.Create())
                {
                    if (file.Content != null)
                    {
                        file.Hash = ToHex(sha.ComputeHash(file.Content));
                        continue;
                    }
                    using (var stream = File.OpenRead(file.FullPath))
                    {
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            cancel.ThrowIfCancellationRequested();
                            sha.TransformBlock(buffer, 0, read, null, 0);
                        }
                        sha.TransformFinalBlock(buffer, 0, 0);
                        file.Hash = ToHex(sha.Hash);
                    }
                }
            }
        }

        private static string ToHex(byte[] hash) => BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

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
