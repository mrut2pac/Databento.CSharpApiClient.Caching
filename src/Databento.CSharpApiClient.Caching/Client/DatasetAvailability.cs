using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel.Metadata;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// Tells which UTC days of a dataset are final, and so may be saved: before the schema's available end, and with Databento's dataset
    /// condition <c>available</c>, or no condition at all (a day the dataset has no data for). Both calls are free; a final day is kept in
    /// memory and never asked about again.
    /// </summary>
    internal sealed class DatasetAvailability
    {
        private const string AvailableCondition = "available";

        private const string DateFormat = "yyyy-MM-dd";

        /// <summary>
        /// The days found final, per dataset
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<DateTime, bool>> finalDays = new ConcurrentDictionary<string, ConcurrentDictionary<DateTime, bool>>(StringComparer.Ordinal);

        /// <summary>
        /// Finds the available end of a schema and which days of a span are final
        /// </summary>
        /// <param name="client">Client the metadata calls go through</param>
        /// <param name="dataset">Databento dataset</param>
        /// <param name="schema">Databento schema</param>
        /// <param name="firstDay">First UTC day of the span</param>
        /// <param name="lastDay">Last UTC day of the span</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The span's availability</returns>
        public async Task<DayAvailability> GetAsync(DatabentoJsonClient client, string dataset, string schema, DateTime firstDay, DateTime lastDay, CancellationToken cancellationToken)
        {
            DateRange range = await client.GetDatasetRangeAsync(dataset, cancellationToken).ConfigureAwait(false);
            DateTime availableEnd = (range.Schemas != null && range.Schemas.TryGetValue(schema, out SchemaRange schemaRange) ? schemaRange.End : range.End).UtcDateTime;
            ConcurrentDictionary<DateTime, bool> final = this.finalDays.GetOrAdd(dataset, _ => new ConcurrentDictionary<DateTime, bool>());

            // a day ending past the available end is not final whatever its condition says, and asking about one past it is refused
            DateTime lastWhole = availableEnd.Date.AddDays(-1);
            DateTime lastAsked = lastDay < lastWhole ? lastDay : lastWhole;
            List<DateTime> unknown = new List<DateTime>();
            for(DateTime day = firstDay; day <= lastAsked; day = day.AddDays(1))
            {
                if(!final.ContainsKey(day))
                {
                    unknown.Add(day);
                }
            }

            if(unknown.Count > 0)
            {
                DatasetCondition[] conditions = await client.ListConditionsAsync(dataset, unknown[0].ToString(DateFormat, CultureInfo.InvariantCulture), unknown[^1].ToString(DateFormat, CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
                HashSet<DateTime> listed = new HashSet<DateTime>();
                foreach(DatasetCondition condition in conditions)
                {
                    if(DateTime.TryParseExact(condition.Date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                    {
                        listed.Add(date);
                        if(string.Equals(condition.Condition, AvailableCondition, StringComparison.Ordinal))
                        {
                            final[date] = true;
                        }
                    }
                }

                foreach(DateTime day in unknown)
                {
                    if(!listed.Contains(day))
                    {
                        final[day] = true;
                    }
                }
            }

            // the days found final are the dataset's, but the end is the schema's: a day another schema found final may not be this one's
            return new DayAvailability(availableEnd, day => day.AddDays(1) <= availableEnd && final.ContainsKey(day));
        }
    }
}
