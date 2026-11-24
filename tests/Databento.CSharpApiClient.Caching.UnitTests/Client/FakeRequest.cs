using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// A request <see cref="FakeDatabento"/> received: its endpoint and its fields, from the form or the query string
    /// </summary>
    internal sealed class FakeRequest
    {
        private FakeRequest(string path, Uri uri, Dictionary<string, string> fields)
        {
            this.Path = path;
            this.Uri = uri;
            this.Fields = fields;
        }

        /// <summary>
        /// Gets the endpoint, e.g. <c>timeseries.get_range</c>.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the request's full URI.
        /// </summary>
        public Uri Uri { get; }

        /// <summary>
        /// Gets the request's fields by name.
        /// </summary>
        public Dictionary<string, string> Fields { get; }

        /// <summary>
        /// Gets the symbols the request names, in its order.
        /// </summary>
        public string[] Symbols => this.Fields.TryGetValue("symbols", out string symbols) ? symbols.Split(',') : Array.Empty<string>();

        /// <summary>
        /// Gets the start of a time-series request's range.
        /// </summary>
        public DateTimeOffset StartUtc => DateTimeOffset.Parse(this.Fields["start"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

        /// <summary>
        /// Gets the end of a time-series request's range, exclusive.
        /// </summary>
        public DateTimeOffset EndUtc => DateTimeOffset.Parse(this.Fields["end"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

        public static async Task<FakeRequest> ReadAsync(HttpRequestMessage request)
        {
            Uri uri = new Uri(new Uri("https://hist.databento.com/v0/"), request.RequestUri);
            // a batch file is downloaded from its own URL, which is no API endpoint
            string path = uri.AbsolutePath.StartsWith("/v0/", StringComparison.Ordinal) ? uri.AbsolutePath.Substring("/v0/".Length) : "download";
            string encoded = request.Content != null ? await request.Content.ReadAsStringAsync().ConfigureAwait(false) : uri.Query.TrimStart('?');
            Dictionary<string, string> fields = encoded
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(pair => Decode(pair[0]), pair => pair.Length > 1 ? Decode(pair[1]) : string.Empty, StringComparer.Ordinal);
            return new FakeRequest(path, uri, fields);
        }

        private static string Decode(string value)
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
    }
}
