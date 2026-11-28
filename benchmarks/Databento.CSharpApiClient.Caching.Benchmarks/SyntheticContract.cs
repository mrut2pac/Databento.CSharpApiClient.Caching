using System;

namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// One generated contract: an option strike and side of an expiration, or a future's contract month
    /// </summary>
    internal sealed class SyntheticContract
    {
        public SyntheticContract(InstrumentSettings instrument, string symbol, DateTime expiration, double strike, bool isCall)
        {
            this.Instrument = instrument;
            this.Symbol = symbol;
            this.Expiration = expiration;
            this.Strike = strike;
            this.IsCall = isCall;
        }

        /// <summary>
        /// Gets the instrument the contract is of.
        /// </summary>
        public InstrumentSettings Instrument { get; }

        /// <summary>
        /// Gets the Databento raw symbol.
        /// </summary>
        public string Symbol { get; }

        /// <summary>
        /// Gets the expiration day, UTC.
        /// </summary>
        public DateTime Expiration { get; }

        /// <summary>
        /// Gets the strike; 0 for a future.
        /// </summary>
        public double Strike { get; }

        /// <summary>
        /// Gets whether an option is a call; false for a put and for a future.
        /// </summary>
        public bool IsCall { get; }
    }
}
