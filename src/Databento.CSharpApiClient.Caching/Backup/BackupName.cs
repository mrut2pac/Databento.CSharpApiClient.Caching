using System;
using System.Globalization;

using Databento.CSharpApiClient.Caching.Archive;

namespace Databento.CSharpApiClient.Caching.Backup
{
    /// <summary>
    /// Names a backup by when it started (UTC, <c>yyyyMMddTHHmmss</c>), so names sort as the backups' ages do
    /// </summary>
    internal static class BackupName
    {
        /// <summary>
        /// Gets the name of a backup started at a time, to the second
        /// </summary>
        public static string Format(DateTime startUtc)
        {
            return startUtc.ToString(ArchiveFileName.BuildTimeFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads when a backup started from its name
        /// </summary>
        /// <returns>Whether the name is a backup's</returns>
        public static bool TryParse(string name, out DateTime startUtc)
        {
            bool parsed = DateTime.TryParseExact(name, ArchiveFileName.BuildTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out startUtc);
            return parsed && string.Equals(Format(startUtc), name, StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets whether a name is a backup's
        /// </summary>
        public static bool IsValid(string name)
        {
            return TryParse(name, out _);
        }
    }
}
