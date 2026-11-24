using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Databento.CSharpApiClient.Caching.Backup
{
    /// <summary>
    /// The last entry of a backup tar: when the backup was taken and every file it holds, with its size and SHA-256
    /// </summary>
    internal sealed class BackupManifest
    {
        /// <summary>
        /// Gets or sets the layout version of the manifest.
        /// </summary>
        [JsonPropertyName("format_version")]
        public int FormatVersion { get; set; }

        /// <summary>
        /// Gets or sets when the backup started, UTC, which names it.
        /// </summary>
        [JsonPropertyName("run_start_utc")]
        public DateTime RunStartUtc { get; set; }

        /// <summary>
        /// Gets or sets the number of files the backup holds.
        /// </summary>
        [JsonPropertyName("file_count")]
        public int FileCount { get; set; }

        /// <summary>
        /// Gets or sets the bytes of all the files together.
        /// </summary>
        [JsonPropertyName("total_bytes")]
        public long TotalBytes { get; set; }

        /// <summary>
        /// Gets or sets the files, in the order they are in the tar.
        /// </summary>
        [JsonPropertyName("files")]
        public List<BackupManifestFile> Files { get; set; } = new List<BackupManifestFile>();
    }
}
