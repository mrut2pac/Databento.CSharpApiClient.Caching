namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// When a final day Databento answers with no records is saved as empty, so it is not asked for again. Under any policy an empty day
    /// is saved only when Databento's condition for it is <c>available</c>; otherwise it is served empty and asked for again next time.
    /// </summary>
    public enum EmptyDayPolicy
    {
        /// <summary>
        /// Only when the free symbology lookup confirms the symbol was not listed that day; otherwise the empty answer is served and the
        /// day is asked for again next time.
        /// </summary>
        CacheWhenNotListed = 0,

        /// <summary>
        /// Every final empty day whose Databento condition is <c>available</c>, with no symbology lookup. An outage's empty day, whose
        /// condition is <c>degraded</c>, <c>pending</c> or <c>missing</c>, is served empty and not saved.
        /// </summary>
        CacheAlways = 1,

        /// <summary>
        /// Empty answers are never saved.
        /// </summary>
        Never = 2,
    }
}
