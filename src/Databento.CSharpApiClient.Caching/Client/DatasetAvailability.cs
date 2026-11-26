using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        /// Reads the available end of a dataset's schema from Databento's dataset range: the schema's own end where the range has one, else
        /// the dataset's
        /// </summary>
        /// <param name="client">Client the metadata call goes through</param>
        /// <param name="dataset">Databento dataset</param>
        /// <param name="schema">Databento schema</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The available end, UTC</returns>
        public static async Task<DateTime> GetAvailableEndAsync(DatabentoJsonClient client, string dataset, string schema, CancellationToken cancellationToken)
        {
            DateRange range = await client.GetDatasetRangeAsync(dataset, cancellationToken).ConfigureAwait(false);
            return (range.Schemas != null && range.Schemas.TryGetValue(schema, out SchemaRange schemaRange) ? schemaRange.End : range.End).UtcDateTime;
        }

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
            DateTime availableEnd = await GetAvailableEndAsync(client, dataset, schema, cancellationToken).ConfigureAwait(false);
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

            await ReadConditionsAsync(client, dataset, final, unknown, false, cancellationToken).ConfigureAwait(false);

            // the days found final are the dataset's, but the end is the schema's: a day another schema found final may not be this one's
            return new DayAvailability(availableEnd, day => day.AddDays(1) <= availableEnd && final.ContainsKey(day));
        }

        /// <summary>
        /// Finds which of a dataset's UTC days are not final by Databento's condition, with no dataset range read: the days not yet known
        /// final are asked about in one call, from the first of them up to Databento's latest listed day. A day the answer omits is final
        /// only when the answer lists a later day, so a day Databento has not published yet is neither saved nor kept as final.
        /// </summary>
        /// <param name="client">Client the metadata call goes through</param>
        /// <param name="dataset">Databento dataset</param>
        /// <param name="days">UTC days</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The condition of each day that is not final, <c>null</c> for one omitted with no later day listed; a day not in it is
        /// final</returns>
        public async Task<IReadOnlyDictionary<DateTime, string>> GetNotFinalAsync(DatabentoJsonClient client, string dataset, IEnumerable<DateTime> days, CancellationToken cancellationToken)
        {
            ConcurrentDictionary<DateTime, bool> final = this.finalDays.GetOrAdd(dataset, _ => new ConcurrentDictionary<DateTime, bool>());
            List<DateTime> unknown = days.Select(day => day.Date).Distinct().Where(day => !final.ContainsKey(day)).OrderBy(day => day).ToList();
            return await ReadConditionsAsync(client, dataset, final, unknown, true, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads the conditions of days not yet known final, in one call from the first to the last (to the latest listed when
        /// <paramref name="omittedNeedsLaterDay"/>), and keeps the final ones: those <c>available</c>, or with no condition listed (and,
        /// when <paramref name="omittedNeedsLaterDay"/>, a later day listed)
        /// </summary>
        /// <returns>The condition of each of the days that is not final, <c>null</c> for one omitted with no later day listed</returns>
        private static async Task<Dictionary<DateTime, string>> ReadConditionsAsync(DatabentoJsonClient client, string dataset, ConcurrentDictionary<DateTime, bool> final, List<DateTime> unknown, bool omittedNeedsLaterDay, CancellationToken cancellationToken)
        {
            Dictionary<DateTime, string> notFinal = new Dictionary<DateTime, string>();
            if(unknown.Count == 0)
            {
                return notFinal;
            }

            // asked up to Databento's latest listed day when an omitted day needs a later one, so a trailing weekend can count
            string endDate = omittedNeedsLaterDay ? null : unknown[^1].ToString(DateFormat, CultureInfo.InvariantCulture);
            DatasetCondition[] conditions = await client.ListConditionsAsync(dataset, unknown[0].ToString(DateFormat, CultureInfo.InvariantCulture), endDate, cancellationToken).ConfigureAwait(false);
            Dictionary<DateTime, string> listed = new Dictionary<DateTime, string>();
            foreach(DatasetCondition condition in conditions)
            {
                if(DateTime.TryParseExact(condition.Date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                {
                    listed[date] = condition.Condition;
                    if(string.Equals(condition.Condition, AvailableCondition, StringComparison.Ordinal))
                    {
                        final[date] = true;
                    }
                }
            }

            DateTime? lastListed = listed.Count > 0 ? listed.Keys.Max() : null;
            foreach(DateTime day in unknown)
            {
                if(!listed.TryGetValue(day, out string condition))
                {
                    // a weekend or holiday inside the listed days has no condition; one past the last listed may not be published yet
                    if(omittedNeedsLaterDay && !(lastListed > day))
                    {
                        notFinal[day] = null;
                    }
                    else
                    {
                        final[day] = true;
                    }
                }
                else if(!string.Equals(condition, AvailableCondition, StringComparison.Ordinal))
                {
                    notFinal[day] = condition;
                }
            }

            return notFinal;
        }
    }
}
