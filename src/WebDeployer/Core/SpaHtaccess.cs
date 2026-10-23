using System.Linq;
using System.Text;

namespace WebDeployer.Core
{
    /// <summary>
    /// .htaccess per i siti con React Router su hosting Apache/LiteSpeed: gli indirizzi interni
    /// (es. /contatti) aperti direttamente o ricaricati rispondono con index.html invece che con 404.
    /// </summary>
    public static class SpaHtaccess
    {
        public const string FileName = ".htaccess";

        public static readonly string Content = string.Join("\n",
            "# Aggiunto da WebDeployer: gli indirizzi interni del sito (es. /contatti) mostrano index.html,",
            "# poi React Router visualizza la pagina giusta. I file che esistono davvero restano serviti normalmente.",
            "<IfModule mod_rewrite.c>",
            "  RewriteEngine On",
            "  RewriteBase /",
            "  RewriteRule ^index\\.html$ - [L]",
            "  RewriteCond %{REQUEST_FILENAME} !-f",
            "  RewriteCond %{REQUEST_FILENAME} !-d",
            "  RewriteRule . /index.html [L]",
            "</IfModule>",
            "");

        public static byte[] Bytes => new UTF8Encoding(false).GetBytes(Content);

        /// <summary>True se il testo è questo stesso .htaccess (ignorando spazi e fine riga).</summary>
        public static bool IsOurs(string text) => Normalize(text) == Normalize(Content);

        private static string Normalize(string text) =>
            string.Join("\n", (text ?? "").Replace("\r\n", "\n").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
    }
}
