using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// A batch job <see cref="FakeDatabento"/> holds: its query, the states its details go through one call at a time, and the sample
    /// folder its files come from
    /// </summary>
    internal sealed class FakeBatchJob
    {
        private int detailCalls;

        /// <summary>
        /// Gets or sets the job's id.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the job's query and file options, by Databento's field name, as submitted.
        /// </summary>
        public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the states the job's details report, one per call, the last one kept.
        /// </summary>
        public string[] States { get; set; } = { "done" };

        /// <summary>
        /// Gets or sets what the job cost, USD.
        /// </summary>
        public decimal CostUsd { get; set; } = 0.000235m;

        /// <summary>
        /// Gets or sets when the job's files stop being downloadable, UTC.
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = new DateTime(2026, 12, 2, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Gets or sets the sample folder the job's files come from, under the tests' <c>Batch\Samples</c>.
        /// </summary>
        public string Sample { get; set; } = "A_dbn_zstd_day";

        /// <summary>
        /// Gets the state a list of jobs shows, which does not move the job on.
        /// </summary>
        public string ListedState => this.States[Math.Min(this.detailCalls, this.States.Length - 1)];

        /// <summary>
        /// Gets the job's details as Databento answers them, moving the job on to its next state
        /// </summary>
        public string DetailsJson()
        {
            string state = this.States[Math.Min(this.detailCalls, this.States.Length - 1)];
            ++this.detailCalls;
            string Field(string name, string fallback) => this.Fields.TryGetValue(name, out string value) ? value : fallback;
            return JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["id"] = this.Id,
                ["cost_usd"] = this.CostUsd,
                ["dataset"] = Field("dataset", null),
                ["symbols"] = Field("symbols", null),
                ["stype_in"] = Field("stype_in", "raw_symbol"),
                ["stype_out"] = "instrument_id",
                ["schema"] = Field("schema", null),
                ["start"] = ToIso(Field("start", null)),
                ["end"] = ToIso(Field("end", null)),
                ["limit"] = null,
                ["encoding"] = Field("encoding", "dbn"),
                ["compression"] = Field("compression", "zstd"),
                ["pretty_px"] = false,
                ["pretty_ts"] = false,
                ["map_symbols"] = Field("map_symbols", "false") == "true",
                ["split_symbols"] = Field("split_symbols", "false") == "true",
                ["split_duration"] = Field("split_duration", "day"),
                ["split_size"] = this.Fields.TryGetValue("split_size", out string size) ? long.Parse(size, CultureInfo.InvariantCulture) : null,
                ["packaging"] = null,
                ["delivery"] = "download",
                ["state"] = state,
                ["ts_received"] = "2026-11-02T12:00:00.000000000Z",
                ["ts_expiration"] = this.ExpiresUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'.000000000Z'", CultureInfo.InvariantCulture),
                ["progress"] = state == "done" ? 100 : 0,
            });
        }

        /// <summary>
        /// Gets the job's symbols, as submitted.
        /// </summary>
        public string[] Symbols => this.Fields.TryGetValue("symbols", out string symbols) ? symbols.Split(',') : Array.Empty<string>();

        private static string ToIso(string value)
        {
            return value == null ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'.000000000Z'", CultureInfo.InvariantCulture);
        }
    }
}
