using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests
{
    /// <summary>
    /// A logger that keeps every line written to it, formatted, and is enabled from a minimum level up, so a test can tell what was logged
    /// and that nothing below the level was formatted
    /// </summary>
    internal sealed class FakeLogger : ILogger
    {
        private readonly object entriesLock = new object();

        private readonly List<FakeLogEntry> entries = new List<FakeLogEntry>();

        /// <summary>
        /// Gets or sets the lowest level <see cref="IsEnabled"/> reports enabled. Default <see cref="LogLevel.Trace"/>: every level.
        /// </summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

        /// <summary>
        /// Gets or sets what every <see cref="Log"/> call throws, as a failing sink would, after noting the line; <c>null</c>, the default,
        /// for nothing.
        /// </summary>
        public Exception Failure { get; set; }

        /// <summary>
        /// Gets or sets what every <see cref="IsEnabled"/> call throws; <c>null</c>, the default, for nothing.
        /// </summary>
        public Exception EnabledFailure { get; set; }

        /// <summary>
        /// Gets the lines written so far, in the order they were written.
        /// </summary>
        public IReadOnlyList<FakeLogEntry> Entries
        {
            get
            {
                lock(this.entriesLock)
                {
                    return this.entries.ToList();
                }
            }
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        /// <summary>
        /// Creates a logger that fails as a broken sink would: <paramref name="mode"/> 0 throws an <see cref="IOException"/> from
        /// <see cref="Log"/>, 1 an <see cref="InvalidOperationException"/> from it, 2 an <see cref="InvalidOperationException"/> from
        /// <see cref="IsEnabled"/>
        /// </summary>
        public static FakeLogger Failing(int mode)
        {
            return mode switch
            {
                0 => new FakeLogger { Failure = new IOException("There is not enough space on the disk.") },
                1 => new FakeLogger { Failure = new InvalidOperationException("The sink is closed.") },
                _ => new FakeLogger { EnabledFailure = new InvalidOperationException("The sink is closed.") },
            };
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            if(this.EnabledFailure != null)
            {
                throw this.EnabledFailure;
            }

            return logLevel != LogLevel.None && logLevel >= this.MinimumLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            FakeLogEntry entry = new FakeLogEntry(logLevel, eventId, formatter(state, exception), exception);
            lock(this.entriesLock)
            {
                this.entries.Add(entry);
            }

            if(this.Failure != null)
            {
                throw this.Failure;
            }
        }

        /// <summary>
        /// Forgets the lines written so far
        /// </summary>
        public void Clear()
        {
            lock(this.entriesLock)
            {
                this.entries.Clear();
            }
        }

        /// <summary>
        /// Gets the lines of a level whose message holds every fragment given
        /// </summary>
        public FakeLogEntry[] Find(LogLevel level, params string[] fragments)
        {
            return this.Entries.Where(entry => entry.Level == level && fragments.All(fragment => entry.Message.Contains(fragment, StringComparison.Ordinal))).ToArray();
        }

        /// <summary>
        /// Asserts a line of a level was written whose message holds every fragment given
        /// </summary>
        /// <returns>The first such line</returns>
        public FakeLogEntry AssertLogged(LogLevel level, params string[] fragments)
        {
            FakeLogEntry[] found = this.Find(level, fragments);
            Assert.AreNotEqual(0, found.Length, Invariant($"No {level} line holds \"{string.Join("\", \"", fragments)}\". Logged:\n{this.Describe()}"));
            return found[0];
        }

        /// <summary>
        /// Asserts no line was written whose message holds the fragment, at any level
        /// </summary>
        public void AssertNotLogged(string fragment)
        {
            Assert.IsFalse(this.Entries.Any(entry => entry.Message.Contains(fragment, StringComparison.Ordinal)), Invariant($"A line holds \"{fragment}\". Logged:\n{this.Describe()}"));
        }

        private string Describe()
        {
            return string.Join("\n", this.Entries.Select(entry => Invariant($"{entry.Level} {entry.EventId.Id}: {entry.Message}")));
        }
    }

    /// <summary>
    /// A line a <see cref="FakeLogger"/> was given: its level, event id, formatted message and exception
    /// </summary>
    internal sealed record FakeLogEntry(LogLevel Level, EventId EventId, string Message, Exception Exception);
}
