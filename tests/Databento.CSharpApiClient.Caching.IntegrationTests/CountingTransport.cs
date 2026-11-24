using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Transport;

namespace Databento.CSharpApiClient.Caching.IntegrationTests
{
    /// <summary>
    /// Sends the cached client's requests to Databento and keeps the path of each one answered, so a test can tell which calls went out
    /// and which were served from the cache. Once downloads are forbidden it refuses any paid request before sending it, so a cache that
    /// fails to serve a day cannot buy it again outside the budget.
    /// </summary>
    internal sealed class CountingTransport : IHttpTransport
    {
        private static readonly string[] PaidPaths = { "timeseries.get_range", "batch.submit_job" };

        private readonly HttpClient httpClient;

        private readonly List<string> answered = new List<string>();

        private volatile bool downloadsForbidden;

        public CountingTransport(DatabentoOptions options)
        {
            // one transport carries the JSON and the DBN client's requests, so it leaves the response bytes as Databento sent them
            this.httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None }, disposeHandler: true)
            {
                BaseAddress = options.BaseUri,
                Timeout = options.Timeout,
            };
            this.httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Databento.CSharpApiClient.Caching.IntegrationTests/1.0");
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(options.ApiKey + ":")));
        }

        /// <summary>
        /// Gets the number of requests answered so far
        /// </summary>
        public int Total
        {
            get
            {
                lock(this.answered)
                {
                    return this.answered.Count;
                }
            }
        }

        /// <summary>
        /// Gets the number of answered requests whose path contains the given text, e.g. <c>timeseries.get_range</c>; a retried request
        /// counts once, when it is answered
        /// </summary>
        public int Count(string pathPart)
        {
            lock(this.answered)
            {
                return this.answered.Count(path => path.Contains(pathPart, StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Refuses every paid request from now on, for calls the cache must serve without buying anything
        /// </summary>
        public void ForbidDownloads()
        {
            this.downloadsForbidden = true;
        }

        /// <summary>
        /// Sends a request, unless it is a paid one and downloads are forbidden
        /// </summary>
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.OriginalString ?? string.Empty;
            if(this.downloadsForbidden && PaidPaths.Any(paid => path.Contains(paid, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(Invariant($"The cache was expected to serve this without buying it, but it sent {path}."));
            }

            HttpResponseMessage response = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if(response.IsSuccessStatusCode)
            {
                lock(this.answered)
                {
                    this.answered.Add(path);
                }
            }

            return response;
        }

        /// <summary>
        /// Releases the HTTP client; the cached client's two Databento clients both dispose it, which is harmless
        /// </summary>
        public void Dispose()
        {
            this.httpClient.Dispose();
        }
    }
}
