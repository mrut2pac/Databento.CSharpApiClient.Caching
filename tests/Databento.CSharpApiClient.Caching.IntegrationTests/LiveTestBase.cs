using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

using Databento.CSharpApiClient.DataModel;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.IntegrationTests
{
    /// <summary>
    /// Base of the live tests: reads the API key from <c>DATABENTO_API_KEY</c> and leaves a test inconclusive without one, gives each
    /// test its own cache roots under the temp folder and deletes them afterwards, and opens cached clients whose requests are counted.
    /// </summary>
    public abstract class LiveTestBase
    {
        /// <summary>
        /// A weekly SPXW call listed from 2025-08-01; a whole day of each schema costs under $0.04 (cost endpoint, 2025-09-05)
        /// </summary>
        protected const string SpxwCall = "SPXW  250908C06475000";

        private readonly List<string> folders = new List<string>();

        private readonly List<IDisposable> disposables = new List<IDisposable>();

        /// <summary>
        /// Gets a UTC day that every dataset the tests use holds as final
        /// </summary>
        protected static DateTimeOffset Day { get; } = new DateTimeOffset(2025, 9, 5, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// Gets the API key, empty when not set
        /// </summary>
        protected string ApiKey { get; } = Environment.GetEnvironmentVariable("DATABENTO_API_KEY") ?? string.Empty;

        /// <summary>
        /// Gets a JSON client for the free metadata and symbology calls the tests check the cache against
        /// </summary>
        protected DatabentoJsonClient Reference { get; private set; }

        /// <summary>
        /// Leaves the test inconclusive without an API key, else opens the reference client
        /// </summary>
        [TestInitialize]
        public void SkipWithoutApiKey()
        {
            if(string.IsNullOrWhiteSpace(this.ApiKey))
            {
                Assert.Inconclusive("DATABENTO_API_KEY is not set, so the live tests do not run.");
            }

            this.Reference = this.Track(new DatabentoJsonClient(this.Options()));
        }

        /// <summary>
        /// Releases the clients and deletes the test's folders; one that cannot be deleted is left, so the test's result stands
        /// </summary>
        [TestCleanup]
        public void DeleteFolders()
        {
            foreach(IDisposable disposable in Enumerable.Reverse(this.disposables))
            {
                disposable.Dispose();
            }

            foreach(string folder in this.folders.Where(Directory.Exists))
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch(IOException ex)
                {
                    Console.WriteLine(Invariant($"Left {folder}: {ex.Message}"));
                }
                catch(UnauthorizedAccessException ex)
                {
                    Console.WriteLine(Invariant($"Left {folder}: {ex.Message}"));
                }
            }
        }

        /// <summary>
        /// Gets a new empty folder that is deleted after the test
        /// </summary>
        protected string NewFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "DatabentoCachingLive", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            this.folders.Add(folder);
            return folder;
        }

        /// <summary>
        /// Opens a cached client on a root, whose requests the returned transport counts
        /// </summary>
        private protected CachedDatabentoClient OpenClient(string cacheRoot, DatabentoTransport transport, out CountingTransport requests, Action<CachedDatabentoClientOptions> configure = null)
        {
            DatabentoOptions databento = this.Options();
            requests = this.Track(new CountingTransport(databento));
            CachedDatabentoClientOptions options = new CachedDatabentoClientOptions
            {
                Databento = databento,
                CacheRoot = cacheRoot,
                Transport = transport,
            };
            configure?.Invoke(options);
            return this.Track(new CachedDatabentoClient(options, requests));
        }

        /// <summary>
        /// Gets records as text through their record interface, to compare records the cache served from different places: a day file
        /// serves the JSON record classes, the archive the DBN ones, and a missing price is null in one and NaN in the other.
        /// </summary>
        protected static string[] AsText(IEnumerable<IRecord> records)
        {
            return records.Select(AsText).ToArray();
        }

        private static string AsText(IRecord record)
        {
            Type[] recordInterfaces = record.GetType().GetInterfaces().Where(type => typeof(IRecord).IsAssignableFrom(type)).ToArray();
            Type schemaInterface = recordInterfaces.Single(type => !recordInterfaces.Any(other => other != type && type.IsAssignableFrom(other)));
            IEnumerable<PropertyInfo> properties = new[] { schemaInterface }.Concat(schemaInterface.GetInterfaces())
                .SelectMany(type => type.GetProperties())
                .OrderBy(property => property.Name, StringComparer.Ordinal);
            return string.Join("; ", properties.Select(property => property.Name + "=" + Format(property.GetValue(record))));
        }

        private static string Format(object value)
        {
            return value switch
            {
                null => "missing",
                double number when double.IsNaN(number) => "missing",
                DateTime time => time.ToString("O", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString(),
            };
        }

        private DatabentoOptions Options()
        {
            return new DatabentoOptions
            {
                ApiKey = this.ApiKey,
            };
        }

        private T Track<T>(T disposable)
            where T : IDisposable
        {
            this.disposables.Add(disposable);
            return disposable;
        }
    }
}
