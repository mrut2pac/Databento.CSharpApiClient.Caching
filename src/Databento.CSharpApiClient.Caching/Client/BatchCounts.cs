using System;
using System.Collections.Generic;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// What saving batch files came to, added up as the files and jobs are taken
    /// </summary>
    internal sealed class BatchCounts
    {
        /// <summary>
        /// Gets or sets the symbol-days saved with records.
        /// </summary>
        public int Saved { get; set; }

        /// <summary>
        /// Gets or sets the symbol-days saved as empty.
        /// </summary>
        public int SavedEmpty { get; set; }

        /// <summary>
        /// Gets or sets the symbol-days the cache already held, which were left as they were.
        /// </summary>
        public int AlreadyCached { get; set; }

        /// <summary>
        /// Gets the UTC days left out: not final, or not whole in the range.
        /// </summary>
        public SortedSet<DateTime> DaysLeftOut { get; } = new SortedSet<DateTime>();

        public void Add(BatchCounts other)
        {
            this.Saved += other.Saved;
            this.SavedEmpty += other.SavedEmpty;
            this.AlreadyCached += other.AlreadyCached;
            this.DaysLeftOut.UnionWith(other.DaysLeftOut);
        }
    }
}
