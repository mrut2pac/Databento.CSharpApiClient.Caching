using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Exceptions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.IntegrationTests
{
    /// <summary>
    /// Keeps a run of the live tests under a spending cap. Before a test buys data it asks Databento's free cost endpoint what the
    /// whole UTC days it is about to download cost, and fails without buying them when the run would pass the cap.
    /// </summary>
    internal static class LiveBudget
    {
        /// <summary>
        /// The most one run of the live tests may spend, USD. <c>DATABENTO_LIVE_TEST_CAP_USD</c> lowers or raises it.
        /// </summary>
        public static readonly decimal CapUsd = ReadCap();

        private static readonly object Gate = new object();

        private static decimal spentUsd;

        /// <summary>
        /// Gets the estimated spend of this run so far, USD
        /// </summary>
        public static decimal SpentUsd
        {
            get
            {
                lock(Gate)
                {
                    return spentUsd;
                }
            }
        }

        /// <summary>
        /// Prices a download and charges it to the run, or fails the test when the run would pass the cap
        /// </summary>
        /// <param name="pricing">A client that prices it; pricing is free</param>
        /// <param name="dataset">Databento dataset</param>
        /// <param name="schema">Databento schema</param>
        /// <param name="symbols">Raw symbols</param>
        /// <param name="startUtc">Start of the download, inclusive</param>
        /// <param name="endUtc">End of the download, exclusive</param>
        /// <param name="times">How many times the test downloads it, such as once per transport</param>
        public static async Task ChargeAsync(DatabentoJsonClient pricing, string dataset, string schema, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, int times = 1)
        {
            decimal cost;
            try
            {
                cost = (decimal)await pricing.GetCostAsync(dataset, symbols, schema, startUtc, endUtc).ConfigureAwait(false) * times;
            }
            catch(DatabentoHttpException ex) when(ex.ErrorCase == "symbology_invalid_request" || ex.ErrorCase == "symbology_invalid_symbol")
            {
                // none of the symbols was listed in the range, so a download has nothing to bill
                cost = 0m;
            }

            lock(Gate)
            {
                if(spentUsd + cost > CapUsd)
                {
                    Assert.Fail(Invariant($"{dataset} {schema} {string.Join(",", symbols)} {startUtc:yyyy-MM-dd} to {endUtc:yyyy-MM-dd} costs ${cost:0.######}; with ${spentUsd:0.######} spent the run would pass its ${CapUsd} cap, so it was not bought."));
                }

                spentUsd += cost;
            }

            Console.WriteLine(Invariant($"Charged ${cost:0.######} for {dataset} {schema} {string.Join(",", symbols)}; run total ${SpentUsd:0.######} of ${CapUsd}."));
        }

        private static decimal ReadCap()
        {
            string text = Environment.GetEnvironmentVariable("DATABENTO_LIVE_TEST_CAP_USD");
            return decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal cap) ? cap : 0.25m;
        }
    }
}
