namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// What a <see cref="CacheMode.Bypass"/> request does with a cached day Databento now answers with no records
    /// </summary>
    public enum BypassMissAction
    {
        /// <summary>
        /// The cached day is kept and served.
        /// </summary>
        KeepCached = 0,

        /// <summary>
        /// Nothing is served for the day, and the cached day is replaced with an empty one once it is settled, whatever
        /// <see cref="EmptyDayPolicy"/> says, but only when Databento's condition for it is <c>available</c>. So an outage's empty answer
        /// never replaces a cached day.
        /// </summary>
        Replace = 1,
    }
}
