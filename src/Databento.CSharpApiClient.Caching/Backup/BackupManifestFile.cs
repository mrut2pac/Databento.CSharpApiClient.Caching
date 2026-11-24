using System.Text.Json.Serialization;

namespace Databento.CSharpApiClient.Caching.Backup
{
    /// <summary>
    /// A file a backup holds, as its manifest lists it
    /// </summary>
    internal sealed class BackupManifestFile
    {
        /// <summary>
        /// Gets or sets the file's path relative to the cache root, <c>/</c>-separated, as its tar entry is named.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the file's size in bytes.
        /// </summary>
        [JsonPropertyName("size")]
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the file's SHA-256, lowercase hex.
        /// </summary>
        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; }
    }
}
