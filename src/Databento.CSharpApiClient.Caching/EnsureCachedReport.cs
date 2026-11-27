using System;
using System.Collections.Generic;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// What <see cref="ICachedDatabentoClient.EnsureCachedAsync"/> found and saved, by symbol-day. The records themselves are served by the
    /// time-series calls.
    /// </summary>
    public sealed class EnsureCachedReport
    {
        /// <summary>
        /// Initializes the report
        /// </summary>
        /// <param name="alreadyCached">Symbol-days the cache held</param>
        /// <param name="saved">Symbol-days downloaded and saved</param>
        /// <param name="pastAvailableEnd">Symbol-days not downloaded, past the available end</param>
        /// <param name="notSettled">Symbol-days downloaded and not saved, not settled yet</param>
        /// <param name="notCached">Symbol-days the cache still lacks</param>
        public EnsureCachedReport(int alreadyCached, int saved, int pastAvailableEnd, int notSettled, IReadOnlyList<KeyValuePair<string, DateTime>> notCached)
        {
            this.AlreadyCached = alreadyCached;
            this.Saved = saved;
            this.PastAvailableEnd = pastAvailableEnd;
            this.NotSettled = notSettled;
            this.NotCached = notCached;
        }

        /// <summary>
        /// Gets the symbol-days the cache already held, found by day file name or in the archive's index with no record read, so a damaged
        /// day file counts until a read finds it.
        /// </summary>
        public int AlreadyCached { get; }

        /// <summary>
        /// Gets the symbol-days downloaded and saved, with records or as empty; also those another request saved meanwhile.
        /// </summary>
        public int Saved { get; }

        /// <summary>
        /// Gets the symbol-days not saved because they end past the available end a download was cut at, as
        /// <see cref="CachedDatabentoClientOptions.AvailableEndRefusal"/> <see cref="AvailableEndRefusal.ServeEmpty"/> leaves them.
        /// </summary>
        public int PastAvailableEnd { get; }

        /// <summary>
        /// Gets the symbol-days downloaded and not saved because their UTC day ended less than
        /// <see cref="CachedDatabentoClientOptions.SettleDelay"/> ago.
        /// </summary>
        public int NotSettled { get; }

        /// <summary>
        /// Gets the symbol-days the cache still lacks, in the order of the symbols given and then by day: those past the available end, those
        /// not settled, empty days the <see cref="CachedDatabentoClientOptions.EmptyDays"/> policy or Databento's condition kept from being saved,
        /// and with <see cref="CachedDatabentoClientOptions.CheckAvailabilityFirst"/> days not yet <c>available</c>; the Debug log names why.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, DateTime>> NotCached { get; }
    }
}
