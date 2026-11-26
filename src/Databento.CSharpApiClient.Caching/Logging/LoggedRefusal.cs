using System;

namespace Databento.CSharpApiClient.Caching.Logging
{
    /// <summary>
    /// The refusal a backup or restore logged where it threw it, so the run's outermost catch tells it from a failure and does not log it
    /// again
    /// </summary>
    internal sealed class LoggedRefusal
    {
        /// <summary>
        /// Gets the refusal thrown, or <c>null</c> while there is none.
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        /// Notes a refusal about to be thrown
        /// </summary>
        /// <returns>The refusal, to throw</returns>
        public Exception Note(Exception exception)
        {
            this.Exception = exception;
            return exception;
        }

        /// <summary>
        /// Tells whether an exception is the refusal noted
        /// </summary>
        public bool Is(Exception exception)
        {
            return ReferenceEquals(exception, this.Exception);
        }
    }
}
