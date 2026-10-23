using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace WebDeployer.Core
{
    /// <summary>Lettura e scrittura di file JSON (classi con [DataContract]).</summary>
    public static class Json
    {
        public static T Read<T>(string path) where T : class
        {
            // ReadAllText toglie l'eventuale BOM (es. file salvato dal Blocco note), che il deserializzatore rifiuta
            var json = File.ReadAllText(path);
            using (var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(json)))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }

        public static void Write<T>(string path, T value)
        {
            // Scrive su un file temporaneo e poi lo sostituisce, per non lasciare mai un file a metà
            var tmp = path + ".tmp";
            using (var stream = File.Create(tmp))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, new UTF8Encoding(false), false, true, "  "))
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(writer, value);
            }

            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
    }
}
