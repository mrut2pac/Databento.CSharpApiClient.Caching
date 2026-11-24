using System;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// A schema's available end and which days are final, as <see cref="DatasetAvailability"/> found them
    /// </summary>
    internal sealed class DayAvailability
    {
        private readonly Func<DateTime, bool> isFinal;

        public DayAvailability(DateTime availableEndUtc, Func<DateTime, bool> isFinal)
        {
            this.AvailableEndUtc = availableEndUtc;
            this.isFinal = isFinal;
        }

        /// <summary>
        /// Gets the end of the schema's available data, UTC, exclusive.
        /// </summary>
        public DateTime AvailableEndUtc { get; }

        /// <summary>
        /// Tells whether a UTC day is final, and so may be saved
        /// </summary>
        public bool IsFinal(DateTime day)
        {
            return this.isFinal(day);
        }
    }
}
