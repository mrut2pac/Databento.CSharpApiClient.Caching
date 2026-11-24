using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Symbology;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// Which days raw symbols were listed on, from Databento's free symbology lookup (raw symbol to instrument id). A symbol counts as
    /// not listed on a day only when the lookup answered for it: with mappings none of which covers the day, or as not found.
    /// </summary>
    internal sealed class SymbolListing
    {
        private const string DateFormat = "yyyy-MM-dd";

        /// <summary>
        /// Each answered symbol's listed spans, start inclusive and end exclusive; empty for a symbol not found
        /// </summary>
        private readonly Dictionary<string, List<DateSpan>> listedSpans;

        private SymbolListing(Dictionary<string, List<DateSpan>> listedSpans)
        {
            this.listedSpans = listedSpans;
        }

        /// <summary>
        /// Looks the symbols up over a span of days, in chunks of at most <paramref name="maxSymbolsPerRequest"/>
        /// </summary>
        /// <exception cref="Exceptions.DatabentoHttpException">The lookup failed</exception>
        public static async Task<SymbolListing> ResolveAsync(DatabentoJsonClient client, string dataset, IReadOnlyCollection<string> symbols, DateTime firstDay, DateTime lastDay, int maxSymbolsPerRequest, CancellationToken cancellationToken)
        {
            Dictionary<string, List<DateSpan>> listedSpans = new Dictionary<string, List<DateSpan>>(StringComparer.Ordinal);
            foreach(string[] chunk in symbols.Chunk(maxSymbolsPerRequest))
            {
                SymbologyRequest request = new SymbologyRequest
                {
                    Dataset = dataset,
                    Symbols = chunk,
                    StypeIn = SymbolTypes.RawSymbol,
                    StypeOut = SymbolTypes.InstrumentId,
                    StartDate = firstDay.ToString(DateFormat, CultureInfo.InvariantCulture),
                    EndDate = lastDay.AddDays(1).ToString(DateFormat, CultureInfo.InvariantCulture),
                };
                SymbologyResolution resolution = await client.ResolveSymbolsAsync(request, cancellationToken).ConfigureAwait(false);
                foreach(string symbol in chunk)
                {
                    if(resolution?.Result != null && resolution.Result.TryGetValue(symbol, out MappedSymbol[] mappings) && mappings != null && mappings.Any(mapping => !string.IsNullOrEmpty(mapping?.Symbol)))
                    {
                        // a mapping whose dates cannot be read leaves the symbol unanswered, rather than not listed
                        List<DateSpan> spans = mappings.Where(mapping => !string.IsNullOrEmpty(mapping?.Symbol)).Select(ToSpan).ToList();
                        if(spans.All(span => span != null))
                        {
                            listedSpans[symbol] = spans;
                        }
                    }
                    else if(resolution?.NotFound != null && resolution.NotFound.Contains(symbol, StringComparer.Ordinal))
                    {
                        listedSpans[symbol] = new List<DateSpan>();
                    }
                }
            }

            return new SymbolListing(listedSpans);
        }

        /// <summary>
        /// Finds the symbol an instrument id stood for on a day, for records that carry no symbol
        /// </summary>
        /// <returns>The raw symbol, or <c>null</c> when none of the symbols looked up was that instrument on that day</returns>
        public string FindSymbol(uint instrumentId, DateTime day)
        {
            string id = instrumentId.ToString(CultureInfo.InvariantCulture);
            foreach(KeyValuePair<string, List<DateSpan>> symbol in this.listedSpans)
            {
                if(symbol.Value.Any(span => span.InstrumentId == id && span.Start <= day && day < span.End))
                {
                    return symbol.Key;
                }
            }

            return null;
        }

        /// <summary>
        /// Tells whether the lookup lists the symbol on any day
        /// </summary>
        public bool IsListed(string symbol)
        {
            return this.listedSpans.TryGetValue(symbol, out List<DateSpan> spans) && spans.Count > 0;
        }

        /// <summary>
        /// Tells whether the lookup confirms the symbol was listed on no day of the span: not found
        /// </summary>
        public bool IsConfirmedNotListed(string symbol)
        {
            return this.listedSpans.TryGetValue(symbol, out List<DateSpan> spans) && spans.Count == 0;
        }

        /// <summary>
        /// Tells whether the lookup confirms the symbol was not listed on a day
        /// </summary>
        public bool IsConfirmedNotListed(string symbol, DateTime day)
        {
            return this.listedSpans.TryGetValue(symbol, out List<DateSpan> spans) && !spans.Any(span => span.Start <= day && day < span.End);
        }

        private static DateSpan ToSpan(MappedSymbol mapping)
        {
            if(DateTime.TryParseExact(mapping.StartDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime start)
                && DateTime.TryParseExact(mapping.EndDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime end))
            {
                return new DateSpan(start, end, mapping.Symbol);
            }

            return null;
        }

        /// <summary>
        /// Days a mapping holds, start inclusive and end exclusive, and the instrument id the symbol was then
        /// </summary>
        private sealed record DateSpan(DateTime Start, DateTime End, string InstrumentId);
    }
}
