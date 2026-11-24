namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// When a final day Databento answers with no records is saved as empty, so it is not asked for again
    /// </summary>
    public enum EmptyDayPolicy
    {
        /// <summary>
        /// Only when the free symbology lookup confirms the symbol was not listed that day; otherwise the empty answer is served and the
        /// day is asked for again next time.
        /// </summary>
        CacheWhenNotListed = 0,

        /// <summary>
        /// Every final empty day.
        /// </summary>
        CacheAlways = 1,

        /// <summary>
        /// Empty answers are never saved.
        /// </summary>
        Never = 2,
    }
}
