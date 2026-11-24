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
        /// The cached day is replaced with an empty one once the day is final, whatever <see cref="EmptyDayPolicy"/> says, and nothing is
        /// served for it.
        /// </summary>
        Replace = 1,
    }
}
