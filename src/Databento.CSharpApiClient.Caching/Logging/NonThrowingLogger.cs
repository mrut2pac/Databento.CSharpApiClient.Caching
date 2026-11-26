using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Databento.CSharpApiClient.Caching.Logging
{
    /// <summary>
    /// Wraps the caller's logger so that nothing it throws reaches the package: a line it fails to take is lost, and what the cache serves
    /// or writes is unchanged, as with a progress or event handler that throws. Every option class's logger is wrapped where the options
    /// are copied, so every component sees only the wrapper.
    /// </summary>
    internal sealed class NonThrowingLogger : ILogger
    {
        private readonly ILogger inner;

        private NonThrowingLogger(ILogger inner)
        {
            this.inner = inner;
        }

        /// <summary>
        /// Wraps a caller's logger
        /// </summary>
        /// <param name="logger">The caller's logger; <c>null</c> for none</param>
        /// <returns><see cref="NullLogger.Instance"/> for none or a null logger, the logger itself when already wrapped, else the logger
        /// wrapped once</returns>
        public static ILogger Wrap(ILogger logger)
        {
            if(logger == null || logger is NullLogger || (logger.GetType().IsGenericType && logger.GetType().GetGenericTypeDefinition() == typeof(NullLogger<>)))
            {
                return NullLogger.Instance;
            }

            return logger as NonThrowingLogger ?? new NonThrowingLogger(logger);
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            try
            {
                return this.inner.BeginScope(state);
            }
            catch(Exception)
            {
                return null;
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            try
            {
                return this.inner.IsEnabled(logLevel);
            }
            catch(Exception)
            {
                return false;
            }
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            try
            {
                this.inner.Log(logLevel, eventId, state, exception, formatter);
            }
            catch(Exception)
            {
                // logging is only diagnostic: a logger that fails loses its line, and never what was being logged
            }
        }
    }
}
