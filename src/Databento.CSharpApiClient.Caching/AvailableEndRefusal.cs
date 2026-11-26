namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// What a time-series call does with the days past the available end, once Databento refused a request as past it
    /// (<c>DataEndAfterAvailableEnd</c> or <c>DataStartAfterAvailableEnd</c>) and the cache cut the request at the end. Every other
    /// refusal throws whatever this says.
    /// </summary>
    public enum AvailableEndRefusal
    {
        /// <summary>
        /// The days are served up to the available end, empty past it, and not saved; the call returns with every other day served as
        /// usual.
        /// </summary>
        ServeEmpty = 0,

        /// <summary>
        /// The call throws a <c>DatabentoHttpException</c> once the days before the end are saved: Databento's refusal or, for a request
        /// cut at a remembered end before it was sent, a 422 of the same case naming that end.
        /// </summary>
        Throw = 1,
    }
}
