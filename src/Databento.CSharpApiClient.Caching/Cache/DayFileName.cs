using System;
using System.Globalization;
using System.IO;

namespace Databento.CSharpApiClient.Caching.Cache
{
    /// <summary>
    /// Names the day files of the cache, one per symbol and UTC day: <c>&lt;symbol&gt;_&lt;yyyy-MM-dd&gt;.&lt;cutoff&gt;.dbn.zst</c>, the symbol
    /// escaped (see <see cref="SymbolEscaping"/>), in the folder <c>&lt;root&gt;\&lt;dataset&gt;\&lt;schema&gt;\&lt;yyyy&gt;\&lt;MM&gt;\&lt;dd&gt;</c> of its
    /// day. Legacy day files, gzipped JSON written before format version 2, end in <c>.json.gz</c> instead and sit in the month folder.
    /// </summary>
    internal static class DayFileName
    {
        /// <summary>
        /// Extension of a day file: DBN, zstd-compressed
        /// </summary>
        public const string Extension = ".dbn.zst";

        /// <summary>
        /// Extension of a legacy day file: Databento's JSON records, gzip-compressed
        /// </summary>
        public const string LegacyExtension = ".json.gz";

        private const string DateFormat = "yyyy-MM-dd";

        /// <summary>
        /// Gets a day file's name
        /// </summary>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="date">UTC day</param>
        /// <param name="cutoff">Day cutoff tag of the cache, see <see cref="CacheMetadata.DayCutoff"/></param>
        /// <returns>The file name, e.g. <c>SPXW%20%20261016C06600000_2026-10-16.UTC.dbn.zst</c></returns>
        public static string Format(string symbol, DateTime date, string cutoff)
        {
            return FormatWith(symbol, date, cutoff, Extension);
        }

        /// <summary>
        /// Gets a legacy day file's name
        /// </summary>
        /// <returns>The file name, e.g. <c>SPXW%20%20261016C06600000_2026-10-16.UTC.json.gz</c></returns>
        public static string FormatLegacy(string symbol, DateTime date, string cutoff)
        {
            return FormatWith(symbol, date, cutoff, LegacyExtension);
        }

        /// <summary>
        /// Reads a day file's name. A name of another cutoff or extension, or written in any form <see cref="Format"/> does not write, is not one.
        /// </summary>
        /// <param name="fileName">File name, without its folder</param>
        /// <param name="cutoff">Day cutoff tag of the cache</param>
        /// <param name="symbol">The raw symbol</param>
        /// <param name="date">The UTC day</param>
        /// <returns><c>true</c> when the name is a day file's of this cutoff</returns>
        public static bool TryParse(string fileName, string cutoff, out string symbol, out DateTime date)
        {
            return TryParseWith(fileName, cutoff, Extension, out symbol, out date);
        }

        /// <summary>
        /// Reads a legacy day file's name, as <see cref="TryParse"/> reads a day file's
        /// </summary>
        public static bool TryParseLegacy(string fileName, string cutoff, out string symbol, out DateTime date)
        {
            return TryParseWith(fileName, cutoff, LegacyExtension, out symbol, out date);
        }

        /// <summary>
        /// Tells whether a path ends as a day file's or a legacy day file's
        /// </summary>
        public static bool IsDayFile(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            return path.EndsWith(Extension, StringComparison.Ordinal) || IsLegacy(path);
        }

        /// <summary>
        /// Tells whether a path ends as a legacy day file's
        /// </summary>
        public static bool IsLegacy(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            return path.EndsWith(LegacyExtension, StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets the folder of a schema's files for a month: <c>&lt;root&gt;\&lt;dataset&gt;\&lt;schema&gt;\&lt;yyyy&gt;\&lt;MM&gt;</c>, which holds the
        /// day folders, the archive files and any legacy day files
        /// </summary>
        /// <param name="cacheRoot">Cache root</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="schema">Databento schema, e.g. <c>cbbo-1m</c></param>
        /// <param name="year">Year</param>
        /// <param name="month">Month, 1 to 12</param>
        /// <returns>The folder's path</returns>
        public static string GetMonthDirectory(string cacheRoot, string dataset, string schema, int year, int month)
        {
            return Path.Combine(cacheRoot, dataset, schema, year.ToString("0000", CultureInfo.InvariantCulture), month.ToString("00", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Gets the folder of a day's day files in its month folder: <c>&lt;month folder&gt;\&lt;dd&gt;</c>
        /// </summary>
        /// <param name="monthDirectory">The day's month folder, see <see cref="GetMonthDirectory"/></param>
        /// <param name="day">Day of the month, 1 to 31</param>
        /// <returns>The folder's path</returns>
        public static string GetDayDirectory(string monthDirectory, int day)
        {
            return Path.Combine(monthDirectory, day.ToString("00", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Gets the path of a day file in its day folder
        /// </summary>
        /// <param name="monthDirectory">The day's month folder</param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="date">UTC day</param>
        /// <param name="cutoff">Day cutoff tag of the cache</param>
        public static string GetPath(string monthDirectory, string symbol, DateTime date, string cutoff)
        {
            return Path.Combine(GetDayDirectory(monthDirectory, date.Day), Format(symbol, date, cutoff));
        }

        /// <summary>
        /// Gets the path of a legacy day file in its month folder
        /// </summary>
        public static string GetLegacyPath(string monthDirectory, string symbol, DateTime date, string cutoff)
        {
            return Path.Combine(monthDirectory, FormatLegacy(symbol, date, cutoff));
        }

        private static string FormatWith(string symbol, DateTime date, string cutoff, string extension)
        {
            ArgumentException.ThrowIfNullOrEmpty(cutoff);

            return SymbolEscaping.Escape(symbol) + "_" + date.ToString(DateFormat, CultureInfo.InvariantCulture) + "." + cutoff + extension;
        }

        private static bool TryParseWith(string fileName, string cutoff, string extension, out string symbol, out DateTime date)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentException.ThrowIfNullOrEmpty(cutoff);

            symbol = null;
            date = default;
            string suffix = "." + cutoff + extension;
            if(!fileName.EndsWith(suffix, StringComparison.Ordinal))
            {
                return false;
            }

            string baseName = fileName.Substring(0, fileName.Length - suffix.Length);
            int separator = baseName.LastIndexOf('_');
            if(separator < 1
                || !DateTime.TryParseExact(baseName.AsSpan(separator + 1), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                || !SymbolEscaping.TryUnescape(baseName.Substring(0, separator), out symbol))
            {
                symbol = null;
                date = default;
                return false;
            }

            return true;
        }
    }
}
