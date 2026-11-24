namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// How <see cref="CachedDatabentoClient"/> downloads records. What is saved is the same either way, Databento's JSON records, so it
    /// can be changed at any time.
    /// </summary>
    public enum DatabentoTransport
    {
        /// <summary>
        /// JSON records, through <see cref="DatabentoJsonClient"/>.
        /// </summary>
        Json = 0,

        /// <summary>
        /// DBN records, through <see cref="DatabentoClient"/>, converted to JSON records before they are saved.
        /// </summary>
        Dbn = 1,
    }
}
