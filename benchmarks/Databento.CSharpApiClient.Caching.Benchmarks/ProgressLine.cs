using System;

namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// Prints a step's progress line: done of planned, rate, time left and memory
    /// </summary>
    internal static class ProgressLine
    {
        public static void Report(string step, long done, long planned, ResourceSampler sampler)
        {
            double seconds = sampler.Elapsed.TotalSeconds;
            double rate = done / Math.Max(0.001, seconds);
            TimeSpan left = rate > 0 ? TimeSpan.FromSeconds((planned - done) / rate) : TimeSpan.Zero;
            Console.WriteLine(Invariant($"{DateTime.Now:HH:mm:ss} {step}: {done:N0} of {planned:N0} ({100.0 * done / Math.Max(1, planned):N1}%), {rate:N0}/s, about {left:hh\\:mm\\:ss} left, {sampler.CurrentWorkingSetBytes() / 1024.0 / 1024.0:N0} MB working set, {sampler.CurrentPrivateBytes() / 1024.0 / 1024.0:N0} MB committed"));
        }
    }
}
