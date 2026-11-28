// Ignore Spelling: Cbbo dbn

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.Transport;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// Databento's historical API as the cached client uses it, in memory: time series from the records added, filtered by symbol and by
    /// <c>ts_recv</c> (<c>ts_event</c> for OHLCV) over the range, as JSON lines or as a DBN stream; the dataset range and conditions;
    /// and symbology from the listings given, a symbol without one being not found. A time-series request starting at or ending past the
    /// schema's available end, or naming an unlisted symbol, is refused as Databento refuses it. Every request is recorded.
    /// </summary>
    internal sealed class FakeDatabento : IHttpTransport
    {
        private const string DateFormat = "yyyy-MM-dd";

        private readonly object sync = new object();

        private readonly List<FakeRecord> records = new List<FakeRecord>();

        private readonly List<FakeRequest> requests = new List<FakeRequest>();

        /// <summary>
        /// Gets or sets the end of the available data, UTC.
        /// </summary>
        public DateTime AvailableEndUtc { get; set; } = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Gets the end of the available data of each schema that ends apart from the dataset, UTC.
        /// </summary>
        public Dictionary<string, DateTime> SchemaEnds { get; } = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the dataset condition of each day that is not <c>available</c>, by date.
        /// </summary>
        public Dictionary<DateTime, string> Conditions { get; } = new Dictionary<DateTime, string>();

        /// <summary>
        /// Gets the days the conditions answer leaves out, as Databento leaves out a weekend, or a day it has not reached yet.
        /// </summary>
        public HashSet<DateTime> OmittedConditions { get; } = new HashSet<DateTime>();

        /// <summary>
        /// Gets or sets the latest day the conditions answer lists, as Databento has not reached a later one yet; <c>null</c> for the last
        /// whole day before the available end.
        /// </summary>
        public DateTime? LastConditionDay { get; set; }

        /// <summary>
        /// Gets the days each listed symbol was listed on: start inclusive, end exclusive. A symbol not here is not found.
        /// </summary>
        public Dictionary<string, DateTime[]> Listings { get; } = new Dictionary<string, DateTime[]>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the days and instrument id each symbol's listing maps to, start inclusive and end exclusive, for a symbol whose instrument id
        /// a test needs; a symbol not here but in <see cref="Listings"/> gets a made-up id.
        /// </summary>
        public Dictionary<string, (DateTime Start, DateTime End, uint InstrumentId)[]> Mappings { get; } = new Dictionary<string, (DateTime Start, DateTime End, uint InstrumentId)[]>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the batch jobs the account holds, those submitted included.
        /// </summary>
        public List<FakeBatchJob> Jobs { get; } = new List<FakeBatchJob>();

        /// <summary>
        /// Gets or sets the sample folder a job submitted from now on gets its files from.
        /// </summary>
        public string JobSample { get; set; } = "A_dbn_zstd_day";

        /// <summary>
        /// Gets or sets the states a job submitted from now on goes through.
        /// </summary>
        public string[] JobStates { get; set; } = { "queued", "processing", "done" };

        /// <summary>
        /// Gets or sets what <c>metadata.get_cost</c> answers, USD.
        /// </summary>
        public decimal Cost { get; set; } = 0.000235m;

        /// <summary>
        /// Gets the files whose listed hash is made wrong, by file name.
        /// </summary>
        public HashSet<string> WrongHashes { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the one job whose files <see cref="WrongHashes"/> applies to, or <c>null</c> for every job.
        /// </summary>
        public string WrongHashJob { get; set; }

        /// <summary>
        /// Gets the folder the tests' sample batch folders are in.
        /// </summary>
        public static string SamplesFolder => System.IO.Path.Combine(AppContext.BaseDirectory, "Batch", "Samples");

        /// <summary>
        /// Gets or sets a response a request gets instead of the fake's own, or <c>null</c> to let the fake answer.
        /// </summary>
        public Func<FakeRequest, HttpResponseMessage> Respond { get; set; }

        /// <summary>
        /// Gets or sets what each time-series request waits for before it is answered, or <c>null</c>.
        /// </summary>
        public Func<Task> BeforeTimeseries { get; set; }

        /// <summary>
        /// Gets the time-series requests so far.
        /// </summary>
        public IReadOnlyList<FakeRequest> TimeseriesRequests => this.RequestsTo("timeseries.get_range");

        /// <summary>
        /// Gets the symbology requests so far.
        /// </summary>
        public IReadOnlyList<FakeRequest> SymbologyRequests => this.RequestsTo("symbology.resolve");

        /// <summary>
        /// Lists symbols on every day of October and November 2026
        /// </summary>
        public void List(params string[] symbols)
        {
            foreach(string symbol in symbols)
            {
                this.Listings[symbol] = new[] { new DateTime(2026, 10, 1), new DateTime(2026, 12, 1) };
            }
        }

        /// <summary>
        /// Adds records Databento serves for a schema, each carrying its symbol
        /// </summary>
        public void Add(string schema, params IRecord[] added)
        {
            lock(this.sync)
            {
                this.records.AddRange(added.Select(record => new FakeRecord(schema, record)));
            }
        }

        public IReadOnlyList<FakeRequest> RequestsTo(string path)
        {
            lock(this.sync)
            {
                return this.requests.Where(request => request.Path == path).ToList();
            }
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // as HttpClient does, a canceled token stops the request before it is sent
            cancellationToken.ThrowIfCancellationRequested();
            FakeRequest fake = await FakeRequest.ReadAsync(request).ConfigureAwait(false);
            lock(this.sync)
            {
                this.requests.Add(fake);
            }

            HttpResponseMessage response = this.Respond?.Invoke(fake);
            if(response != null)
            {
                return response;
            }

            switch(fake.Path)
            {
                case "timeseries.get_range":
                    if(this.BeforeTimeseries != null)
                    {
                        await this.BeforeTimeseries().ConfigureAwait(false);
                    }

                    return this.AnswerTimeseries(fake);
                case "metadata.get_dataset_range":
                    string schemas = string.Join(",", this.SchemaEnds.Select(end => Invariant($"\"{end.Key}\":{{\"start\":\"2020-01-01T00:00:00Z\",\"end\":\"{end.Value:yyyy-MM-ddTHH:mm:ss}Z\"}}")));
                    return Json(Invariant($"{{\"start\":\"2020-01-01T00:00:00Z\",\"end\":\"{this.AvailableEndUtc:yyyy-MM-ddTHH:mm:ss}Z\",\"schema\":{{{schemas}}}}}"));
                case "metadata.get_dataset_condition":
                    return this.AnswerConditions(fake);
                case "symbology.resolve":
                    return this.AnswerSymbology(fake);
                case "metadata.get_cost":
                    return Json(this.Cost.ToString(CultureInfo.InvariantCulture));
                case "batch.submit_job":
                    return this.SubmitJob(fake);
                case "batch.get_job_details":
                    return Json(this.FindJob(fake.Fields["job_id"]).DetailsJson());
                case "batch.list_jobs":
                    return this.ListJobs(fake);
                case "batch.list_files":
                    return this.ListFiles(this.FindJob(fake.Fields["job_id"]));
                case "download":
                    return this.Download(fake);
                default:
                    return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
            }
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// Builds Databento's error answer of a case
        /// </summary>
        public static HttpResponseMessage Error(HttpStatusCode status, string errorCase)
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(Invariant($"{{\"detail\":{{\"case\":\"{errorCase}\",\"message\":\"refused\",\"status_code\":{(int)status}}}}}"), Encoding.UTF8, "application/json"),
            };
        }

        private static HttpResponseMessage Json(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }

        private static DateTime? GetFilterTime(FakeRecord record)
        {
            if(record.Schema.StartsWith("ohlcv", StringComparison.Ordinal))
            {
                return record.Record.TsEventUtc;
            }

            return record.Record switch
            {
                ICbboRecord cbbo => cbbo.TsReceivedUtc ?? cbbo.TsEventUtc,
                IBboRecord bbo => bbo.TsReceivedUtc ?? bbo.TsEventUtc,
                ITcbboRecord tcbbo => tcbbo.TsReceivedUtc ?? tcbbo.TsEventUtc,
                ITradeRecord trade => trade.TsReceivedUtc ?? trade.TsEventUtc,
                IStatisticsRecord statistics => statistics.TsReceivedUtc ?? statistics.TsEventUtc,
                _ => record.Record.TsEventUtc,
            };
        }

        private HttpResponseMessage AnswerTimeseries(FakeRequest request)
        {
            // as Databento does, a request starting at or past the schema's available end is refused for its start, and one ending past
            // it for its end, not cut
            DateTime availableEnd = this.SchemaEnds.TryGetValue(request.Fields["schema"], out DateTime schemaEnd) ? schemaEnd : this.AvailableEndUtc;
            if(request.StartUtc.UtcDateTime >= availableEnd)
            {
                return Error(HttpStatusCode.UnprocessableEntity, "data_start_after_available_end");
            }

            if(request.EndUtc.UtcDateTime > availableEnd)
            {
                return Error(HttpStatusCode.UnprocessableEntity, "data_end_after_available_end");
            }

            string[] unlisted = request.Symbols.Where(symbol => !this.Listings.ContainsKey(symbol)).ToArray();
            if(unlisted.Length > 0)
            {
                return Error(HttpStatusCode.BadRequest, "symbology_invalid_request");
            }

            List<IRecord> answer;
            lock(this.sync)
            {
                answer = this.records
                    .Where(record => record.Schema == request.Fields["schema"] && request.Symbols.Contains(record.Record.Symbol, StringComparer.Ordinal))
                    .Where(record => GetFilterTime(record) >= request.StartUtc.UtcDateTime && GetFilterTime(record) < request.EndUtc.UtcDateTime)
                    .OrderBy(record => GetFilterTime(record))
                    .Select(record => record.Record)
                    .ToList();
            }

            if(request.Fields["encoding"] == "dbn")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(FakeDbnStream.Build(request, request.Fields["schema"], answer)) };
            }

            // a one-symbol answer is not mapped: its records carry no symbol
            bool mapped = request.Fields.TryGetValue("map_symbols", out string mapSymbols) && mapSymbols == "true";
            StringBuilder lines = new StringBuilder();
            foreach(IRecord record in answer)
            {
                string json = JsonSerializer.Serialize(record, record.GetType());
                if(!mapped)
                {
                    object copy = JsonSerializer.Deserialize(json, record.GetType());
                    copy.GetType().GetProperty("Symbol").SetValue(copy, null);
                    json = JsonSerializer.Serialize(copy, record.GetType());
                }

                lines.Append(json).Append('\n');
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(lines.ToString(), Encoding.UTF8, "application/json") };
        }

        private HttpResponseMessage AnswerConditions(FakeRequest request)
        {
            DateTime first = DateTime.ParseExact(request.Fields["start_date"], DateFormat, CultureInfo.InvariantCulture);

            // with no end date, Databento answers up to its latest listed day
            DateTime last = this.LastConditionDay ?? this.AvailableEndUtc.AddDays(-1).Date;
            if(request.Fields.TryGetValue("end_date", out string endDate))
            {
                DateTime asked = DateTime.ParseExact(endDate, DateFormat, CultureInfo.InvariantCulture);
                if(asked.AddDays(1) > this.AvailableEndUtc)
                {
                    return Error(HttpStatusCode.UnprocessableEntity, "data_end_after_available_end");
                }

                last = asked < last ? asked : last;
            }

            List<string> days = new List<string>();
            for(DateTime day = first; day <= last; day = day.AddDays(1))
            {
                if(this.OmittedConditions.Contains(day))
                {
                    continue;
                }

                string condition = this.Conditions.TryGetValue(day, out string set) ? set : "available";
                days.Add(Invariant($"{{\"date\":\"{day:yyyy-MM-dd}\",\"condition\":\"{condition}\",\"last_modified_date\":\"{day:yyyy-MM-dd}\"}}"));
            }

            return Json("[" + string.Join(",", days) + "]");
        }

        private HttpResponseMessage AnswerSymbology(FakeRequest request)
        {
            DateTime start = DateTime.ParseExact(request.Fields["start_date"], DateFormat, CultureInfo.InvariantCulture);
            DateTime end = DateTime.ParseExact(request.Fields["end_date"], DateFormat, CultureInfo.InvariantCulture);
            List<string> result = new List<string>();
            List<string> notFound = new List<string>();
            int instrumentId = 1000;
            foreach(string symbol in request.Symbols)
            {
                ++instrumentId;
                if(this.Mappings.TryGetValue(symbol, out (DateTime Start, DateTime End, uint InstrumentId)[] mappings))
                {
                    string spans = string.Join(",", mappings.Where(mapping => mapping.Start < end && start < mapping.End).Select(mapping => Invariant($"{{\"d0\":\"{mapping.Start:yyyy-MM-dd}\",\"d1\":\"{mapping.End:yyyy-MM-dd}\",\"s\":\"{mapping.InstrumentId}\"}}")));
                    result.Add(Invariant($"{JsonSerializer.Serialize(symbol)}:[{spans}]"));
                }
                else if(this.Listings.TryGetValue(symbol, out DateTime[] listed) && listed[0] < end && start < listed[1])
                {
                    DateTime from = listed[0] > start ? listed[0] : start;
                    DateTime to = listed[1] < end ? listed[1] : end;
                    result.Add(Invariant($"{JsonSerializer.Serialize(symbol)}:[{{\"d0\":\"{from:yyyy-MM-dd}\",\"d1\":\"{to:yyyy-MM-dd}\",\"s\":\"{instrumentId}\"}}]"));
                }
                else
                {
                    notFound.Add(JsonSerializer.Serialize(symbol));
                }
            }

            return Json(Invariant($"{{\"result\":{{{string.Join(",", result)}}},\"symbols\":[],\"stype_in\":\"raw_symbol\",\"stype_out\":\"instrument_id\",\"start_date\":\"{start:yyyy-MM-dd}\",\"end_date\":\"{end:yyyy-MM-dd}\",\"partial\":[],\"not_found\":[{string.Join(",", notFound)}],\"message\":\"OK\",\"status\":0}}"));
        }

        private FakeBatchJob FindJob(string id)
        {
            lock(this.sync)
            {
                return this.Jobs.Single(job => job.Id == id);
            }
        }

        private HttpResponseMessage SubmitJob(FakeRequest request)
        {
            FakeBatchJob job;
            lock(this.sync)
            {
                job = new FakeBatchJob
                {
                    Id = Invariant($"TEST-{this.Jobs.Count + 1}"),
                    Fields = new Dictionary<string, string>(request.Fields, StringComparer.Ordinal),
                    States = this.JobStates,
                    Sample = this.JobSample,
                };
                this.Jobs.Add(job);
            }

            return Json(Invariant($"{{\"id\":\"{job.Id}\",\"state\":\"received\",\"cost_usd\":null}}"));
        }

        private HttpResponseMessage ListJobs(FakeRequest request)
        {
            string[] states = request.Fields.TryGetValue("states", out string asked) ? asked.Split(',') : new[] { "queued", "processing", "done" };
            List<string> listed;
            lock(this.sync)
            {
                listed = this.Jobs.Where(job => states.Contains(job.ListedState)).Select(job => Invariant($"{{\"id\":\"{job.Id}\",\"state\":\"{job.ListedState}\",\"ts_received\":\"2026-11-02T12:00:00.000000000Z\"}}")).ToList();
            }

            return Json("[" + string.Join(",", listed) + "]");
        }

        private HttpResponseMessage ListFiles(FakeBatchJob job)
        {
            List<string> files = new List<string>();
            foreach(string path in System.IO.Directory.GetFiles(System.IO.Path.Combine(SamplesFolder, job.Sample)).OrderBy(path => path, StringComparer.Ordinal))
            {
                string name = System.IO.Path.GetFileName(path);
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
                if(this.WrongHashes.Contains(name) && (this.WrongHashJob == null || this.WrongHashJob == job.Id))
                {
                    hash = new string('0', 64);
                }

                files.Add(Invariant($"{{\"filename\":\"{name}\",\"size\":{bytes.Length},\"hash\":\"sha256:{hash}\",\"urls\":{{\"https\":\"https://download.fake/{job.Id}/{Uri.EscapeDataString(name)}\",\"ftp\":\"\"}}}}"));
            }

            return Json("[" + string.Join(",", files) + "]");
        }

        private HttpResponseMessage Download(FakeRequest request)
        {
            string[] parts = request.Uri.AbsolutePath.Trim('/').Split('/');
            FakeBatchJob job = this.FindJob(parts[0]);
            byte[] bytes = System.IO.File.ReadAllBytes(System.IO.Path.Combine(SamplesFolder, job.Sample, Uri.UnescapeDataString(parts[1])));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }

        /// <summary>
        /// A record Databento serves, and its schema
        /// </summary>
        private sealed record FakeRecord(string Schema, IRecord Record);
    }
}
