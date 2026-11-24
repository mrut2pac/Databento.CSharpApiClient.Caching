using System;
using System.Collections.Generic;
using System.Linq;

using Databento.CSharpApiClient.Caching.Backup;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// Which backups a <see cref="DatabentoCacheBackup"/> keeps once it has put a new one into the store: the last few, or one a month.
    /// The backup just written is always kept, and the store's other backups are deleted by this rule.
    /// </summary>
    public sealed class BackupRetention
    {
        private readonly int keepLast;

        private readonly int months;

        private BackupRetention(int keepLast, int months)
        {
            this.keepLast = keepLast;
            this.months = months;
        }

        /// <summary>
        /// Keeps the newest backups, the one just written among them.
        /// </summary>
        /// <param name="count">How many backups are kept; 1 or more</param>
        /// <returns>The rule</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is below 1</exception>
        public static BackupRetention KeepLast(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
            return new BackupRetention(count, 0);
        }

        /// <summary>
        /// Keeps every backup of the UTC month the new one is taken in, then the last backup of each of the months before it.
        /// </summary>
        /// <param name="months">How many months before the current one keep their last backup; 0 or more</param>
        /// <returns>The rule</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="months"/> is negative</exception>
        public static BackupRetention Calendar(int months)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(months);
            return new BackupRetention(0, months);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.keepLast > 0 ? Invariant($"KeepLast({this.keepLast})") : Invariant($"Calendar({this.months})");
        }

        /// <summary>
        /// Gets the backups the rule deletes, oldest first
        /// </summary>
        /// <param name="backups">The store's backups, oldest first, the new one included or not</param>
        /// <param name="newBackup">The backup just written, which is kept</param>
        internal IReadOnlyList<string> SelectExpired(IReadOnlyList<string> backups, string newBackup)
        {
            // a name that is no backup's is not this rule's to delete
            List<string> older = backups.Where(backup => BackupName.IsValid(backup) && !string.Equals(backup, newBackup, StringComparison.Ordinal)).ToList();
            if(this.keepLast > 0)
            {
                return older.Take(Math.Max(0, older.Count + 1 - this.keepLast)).ToList();
            }

            BackupName.TryParse(newBackup, out DateTime newStartUtc);
            DateTime currentMonth = new DateTime(newStartUtc.Year, newStartUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime firstKeptMonth = currentMonth.AddMonths(-this.months);
            HashSet<string> kept = new HashSet<string>(StringComparer.Ordinal);
            foreach(IGrouping<DateTime, string> month in older.GroupBy(backup => MonthOf(backup)))
            {
                if(month.Key >= currentMonth)
                {
                    kept.UnionWith(month);
                }
                else if(month.Key >= firstKeptMonth)
                {
                    kept.Add(month.Max(StringComparer.Ordinal));
                }
            }

            return older.Where(backup => !kept.Contains(backup)).ToList();
        }

        private static DateTime MonthOf(string backup)
        {
            BackupName.TryParse(backup, out DateTime startUtc);
            return new DateTime(startUtc.Year, startUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }
    }
}
