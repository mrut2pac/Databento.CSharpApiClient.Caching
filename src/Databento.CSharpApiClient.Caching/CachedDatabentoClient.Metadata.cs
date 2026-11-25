using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel.Metadata;
using Databento.CSharpApiClient.DataModel.Symbology;

namespace Databento.CSharpApiClient.Caching
{
    public sealed partial class CachedDatabentoClient
    {
        /// <summary>
        /// Lists the datasets the API key can reach: Databento's own answer, not cached
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The dataset codes</returns>
        public Task<string[]> ListDatasetsAsync(CancellationToken ct = default)
        {
            return this.jsonClient.ListDatasetsAsync(ct);
        }

        /// <summary>
        /// Lists the schemas of a dataset: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The schema names</returns>
        public Task<string[]> ListSchemasAsync(string dataset, CancellationToken ct = default)
        {
            return this.jsonClient.ListSchemasAsync(dataset, ct);
        }

        /// <summary>
        /// Lists Databento's publishers: Databento's own answer, not cached
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Each publisher: its id, dataset and venue</returns>
        public Task<PublisherInfo[]> ListPublishersAsync(CancellationToken ct = default)
        {
            return this.jsonClient.ListPublishersAsync(ct);
        }

        /// <summary>
        /// Lists the fields of a schema, with their types: Databento's own answer, not cached
        /// </summary>
        /// <param name="schema">Databento schema, e.g. <c>trades</c></param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Each field's name and type</returns>
        public Task<FieldInfo[]> ListFieldsAsync(string schema, CancellationToken ct = default)
        {
            return this.jsonClient.ListFieldsAsync(schema, ct);
        }

        /// <summary>
        /// Lists a dataset's unit prices, per schema: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The unit prices of each feed mode</returns>
        public Task<UnitPriceInfo[]> ListUnitPricesAsync(string dataset, CancellationToken ct = default)
        {
            return this.jsonClient.ListUnitPricesAsync(dataset, ct);
        }

        /// <summary>
        /// Gets a dataset's data-quality condition on one day, or <c>null</c> when the dataset has no entry for it. Without a date it is the latest day's, which downloads the dataset's whole daily history: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="dateStr">ISO-8601 date, e.g. <c>2026-01-15</c>; <c>null</c> for the latest day</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The day's condition, or <c>null</c></returns>
        public Task<DatasetCondition> GetDatasetConditionAsync(string dataset, string dateStr = null, CancellationToken ct = default)
        {
            return this.jsonClient.GetDatasetConditionAsync(dataset, dateStr, ct);
        }

        /// <summary>
        /// Gets the range a dataset holds data for, and each schema's: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The dataset's range and each schema's</returns>
        public Task<DateRange> GetDatasetRangeAsync(string dataset, CancellationToken ct = default)
        {
            return this.jsonClient.GetDatasetRangeAsync(dataset, ct);
        }

        /// <summary>
        /// Lists a dataset's daily data-quality conditions, oldest first: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="startDateStr">ISO-8601 first date, inclusive, or <c>null</c></param>
        /// <param name="endDateStr">ISO-8601 last date, inclusive, or <c>null</c></param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Each day's condition</returns>
        public Task<DatasetCondition[]> ListConditionsAsync(string dataset, string startDateStr = null, string endDateStr = null, CancellationToken ct = default)
        {
            return this.jsonClient.ListConditionsAsync(dataset, startDateStr, endDateStr, ct);
        }

        /// <summary>
        /// Gets the number of records a query would return, without downloading it: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Instrument symbols</param>
        /// <param name="schema">Databento schema, e.g. <c>cbbo-1m</c></param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The record count</returns>
        public Task<long> GetRecordCountAsync(string dataset, IReadOnlyList<string> symbols, string schema, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct = default)
        {
            return this.jsonClient.GetRecordCountAsync(dataset, symbols, schema, startUtc, endUtc, ct);
        }

        /// <summary>
        /// Gets the billable uncompressed size of a query in bytes, without downloading it: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Instrument symbols</param>
        /// <param name="schema">Databento schema, e.g. <c>cbbo-1m</c></param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The size in bytes</returns>
        public Task<long> GetBillableSizeAsync(string dataset, IReadOnlyList<string> symbols, string schema, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct = default)
        {
            return this.jsonClient.GetBillableSizeAsync(dataset, symbols, schema, startUtc, endUtc, ct);
        }

        /// <summary>
        /// Gets what a query would cost in USD, without downloading it: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Instrument symbols</param>
        /// <param name="schema">Databento schema, e.g. <c>cbbo-1m</c></param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The cost in USD</returns>
        public Task<double> GetCostAsync(string dataset, IReadOnlyList<string> symbols, string schema, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct = default)
        {
            return this.jsonClient.GetCostAsync(dataset, symbols, schema, startUtc, endUtc, ct);
        }

        /// <summary>
        /// Resolves symbols from one symbology type to another over a date range: Databento's own answer, not cached
        /// </summary>
        /// <param name="request">What to resolve: dataset, symbols, symbol types and dates</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Each symbol's mappings, and the symbols not found</returns>
        public Task<SymbologyResolution> ResolveSymbolsAsync(SymbologyRequest request, CancellationToken ct = default)
        {
            return this.jsonClient.ResolveSymbolsAsync(request, ct);
        }
    }
}
