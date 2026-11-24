using System;
using System.Collections.Generic;
using System.Threading;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// One download: a dataset's symbols over a UTC range, its end exclusive as Databento's is
    /// </summary>
    internal sealed record RangeRequest(string Dataset, IReadOnlyList<string> Symbols, DateTimeOffset StartUtc, DateTimeOffset EndUtc, CancellationToken CancellationToken);
}
