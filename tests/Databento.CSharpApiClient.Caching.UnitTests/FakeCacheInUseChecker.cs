using System;

namespace Databento.CSharpApiClient.Caching.UnitTests
{
    /// <summary>
    /// An in-use checker that answers as the test says and counts how often it was asked
    /// </summary>
    internal sealed class FakeCacheInUseChecker : ICacheInUseChecker
    {
        private readonly Func<int, bool> answer;

        /// <summary>
        /// Creates a checker
        /// </summary>
        /// <param name="answer">Answers each check, given its number from 1</param>
        public FakeCacheInUseChecker(Func<int, bool> answer)
        {
            this.answer = answer;
        }

        /// <summary>
        /// Gets the number of checks so far.
        /// </summary>
        public int Checks { get; private set; }

        public bool IsInUse()
        {
            return this.answer(++this.Checks);
        }
    }
}
