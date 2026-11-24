using System;
using System.Collections.Generic;
using System.Linq;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// Thrown by a <see cref="CacheMode.CacheOnly"/> call when the cache lacks some of the symbol-days the range needs. Nothing was
    /// downloaded, so nothing was billed.
    /// </summary>
    public sealed class NotCachedException : Exception
    {
        private const int DaysNamedInMessage = 10;

        /// <summary>
        /// Initializes the exception
        /// </summary>
        /// <param name="dataset">Databento dataset</param>
        /// <param name="schema">Databento schema</param>
        /// <param name="missing">The symbol-days the cache lacks: raw symbol and UTC day</param>
        public NotCachedException(string dataset, string schema, IEnumerable<KeyValuePair<string, DateTime>> missing)
            : this(dataset, schema, Order(missing))
        {
        }

        private NotCachedException(string dataset, string schema, KeyValuePair<string, DateTime>[] missing)
            : base(Describe(dataset, schema, missing))
        {
            this.Dataset = dataset;
            this.Schema = schema;
            this.MissingDays = missing.ToLookup(symbolDay => symbolDay.Key, symbolDay => symbolDay.Value, StringComparer.Ordinal);
            this.MissingDayCount = missing.Length;
        }

        /// <summary>
        /// Gets the Databento dataset asked for.
        /// </summary>
        public string Dataset { get; }

        /// <summary>
        /// Gets the Databento schema asked for.
        /// </summary>
        public string Schema { get; }

        /// <summary>
        /// Gets the UTC days the cache lacks, by raw symbol, each symbol's in order.
        /// </summary>
        public ILookup<string, DateTime> MissingDays { get; }

        /// <summary>
        /// Gets how many symbol-days the cache lacks.
        /// </summary>
        public int MissingDayCount { get; }

        private static KeyValuePair<string, DateTime>[] Order(IEnumerable<KeyValuePair<string, DateTime>> missing)
        {
            ArgumentNullException.ThrowIfNull(missing);
            return missing.OrderBy(symbolDay => symbolDay.Key, StringComparer.Ordinal).ThenBy(symbolDay => symbolDay.Value).ToArray();
        }

        private static string Describe(string dataset, string schema, KeyValuePair<string, DateTime>[] missing)
        {
            string named = string.Join(", ", missing.Take(DaysNamedInMessage).Select(symbolDay => Invariant($"{symbolDay.Key} {symbolDay.Value:yyyy-MM-dd}")));
            string more = missing.Length > DaysNamedInMessage ? Invariant($" and {missing.Length - DaysNamedInMessage} more") : string.Empty;
            return Invariant($"The cache lacks {missing.Length} {dataset} {schema} symbol-day(s), and nothing was downloaded: {named}{more}.");
        }
    }
}
