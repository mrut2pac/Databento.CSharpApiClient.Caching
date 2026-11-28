// Ignore Spelling: Cbbo

using System;
using System.IO;
using System.Linq;
using System.Threading;

using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Dbn;
using Databento.CSharpApiClient.DataModel.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// A cache root and a fake Databento, for the cached client's tests: creates clients over both, and reads back what a day file holds.
    /// The SPXW options of October 16, 2026, a Friday, are the ones most tests ask for.
    /// </summary>
    internal sealed class CachedClientFixture : IDisposable
    {
        public const string Call = "SPXW  261016C06600000";

        public const string Put = "SPXW  261016P06600000";

        public static readonly DateTime Day = new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

        public CachedClientFixture()
        {
            this.Cache = new CacheFixture();
            this.Databento.List(Call, Put);
        }

        /// <summary>
        /// Gets the cache root and its helpers.
        /// </summary>
        public CacheFixture Cache { get; }

        /// <summary>
        /// Gets the fake Databento the clients download from.
        /// </summary>
        public FakeDatabento Databento { get; } = new FakeDatabento();

        /// <summary>
        /// Gets the clock of the clients.
        /// </summary>
        public FakeTimeProvider Clock { get; } = new FakeTimeProvider(new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));

        /// <summary>
        /// Gets the start of the fixture's day as a range bound.
        /// </summary>
        public static DateTimeOffset DayStart => new DateTimeOffset(Day);

        /// <summary>
        /// Gets the end of the fixture's day as a range bound.
        /// </summary>
        public static DateTimeOffset DayEnd => new DateTimeOffset(Day.AddDays(1));

        /// <summary>
        /// Creates a CBBO record of a symbol, stamped <paramref name="minute"/> minutes after 13:30 UTC of a day, received a second later
        /// </summary>
        public static CbboRecordJson Cbbo(string symbol, DateTime date, int minute, double price = 6600.5)
        {
            CbboRecordJson record = CacheFixture.CbboRecord(date, price, minute);
            record.Symbol = symbol;
            return record;
        }

        /// <summary>
        /// Creates a CBBO record received at a time of day
        /// </summary>
        public static CbboRecordJson CbboAt(string symbol, DateTime receivedUtc)
        {
            CbboRecordJson record = Cbbo(symbol, receivedUtc.Date, 0);
            record.Header.TsEventUtc = receivedUtc.AddSeconds(-1);
            record.TsReceivedUtc = receivedUtc;
            return record;
        }

        public CachedDatabentoClientOptions CreateOptions()
        {
            return new CachedDatabentoClientOptions
            {
                Databento = new DatabentoOptions
                {
                    ApiKey = "db-test",
                    MaxRetries = 0,
                },
                CacheRoot = this.Cache.Root,
                TimeProvider = this.Clock,
            };
        }

        public CachedDatabentoClient CreateClient(Action<CachedDatabentoClientOptions> change = null)
        {
            CachedDatabentoClientOptions options = this.CreateOptions();
            change?.Invoke(options);
            return new CachedDatabentoClient(options, this.Databento);
        }

        /// <summary>
        /// Gets the path of a day file, in its day folder
        /// </summary>
        public string DayFilePath(string schema, string symbol, DateTime date, string dataset = CacheFixture.Opra)
        {
            return DayFileName.GetPath(this.Cache.MonthDirectory(dataset, schema, date.Year, date.Month), symbol, date, CacheMetadata.UtcCutoff);
        }

        /// <summary>
        /// Gets the path of a legacy day file, in its month folder
        /// </summary>
        public string LegacyDayFilePath(string schema, string symbol, DateTime date, string dataset = CacheFixture.Opra)
        {
            return DayFileName.GetLegacyPath(this.Cache.MonthDirectory(dataset, schema, date.Year, date.Month), symbol, date, CacheMetadata.UtcCutoff);
        }

        /// <summary>
        /// Reads a CBBO day file's records
        /// </summary>
        /// <returns>The records, or <c>null</c> when there is no such day file</returns>
        public CbboRecordDbn[] ReadCbboDayFile(string symbol, DateTime date, string schema = CacheFixture.Cbbo)
        {
            string path = this.DayFilePath(schema, symbol, date);
            return File.Exists(path) ? DayFile.ReadAsync(path, schema == CacheFixture.Cbbo ? DayFileFormats.Cbbo1Min : DayFileFormats.Cbbo1Sec, CancellationToken.None).GetAwaiter().GetResult() : null;
        }

        /// <summary>
        /// Gets the received times of records, to compare what was served
        /// </summary>
        public static DateTime[] ReceivedTimes(params ICbboRecord[] records)
        {
            return records.Select(record => record.TsReceivedUtc.Value).ToArray();
        }

        public void Dispose()
        {
            this.Cache.Dispose();
        }
    }
}
