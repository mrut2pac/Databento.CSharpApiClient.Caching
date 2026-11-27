namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// One pass of symbol-day reads: its order, readers and latencies
    /// </summary>
    internal sealed class QueryPassResult
    {
        /// <summary>
        /// Gets or sets the order the symbol-days were read in: <c>random</c>, or <c>sequential</c> for runs of consecutive strikes of a day.
        /// </summary>
        public string Order { get; set; }

        /// <summary>
        /// Gets or sets the number of concurrent readers.
        /// </summary>
        public int Readers { get; set; }

        /// <summary>
        /// Gets or sets whether this was the first pass on a new client, or the same reads repeated on it.
        /// </summary>
        public bool Repeat { get; set; }

        /// <summary>
        /// Gets or sets the number of symbol-days read.
        /// </summary>
        public int Queries { get; set; }

        /// <summary>
        /// Gets or sets the records read.
        /// </summary>
        public long Records { get; set; }

        /// <summary>
        /// Gets or sets the mean time of one read, milliseconds.
        /// </summary>
        public double MeanMs { get; set; }

        /// <summary>
        /// Gets or sets the median time of one read, milliseconds.
        /// </summary>
        public double P50Ms { get; set; }

        /// <summary>
        /// Gets or sets the 95th percentile time of one read, milliseconds.
        /// </summary>
        public double P95Ms { get; set; }

        /// <summary>
        /// Gets or sets the 99th percentile time of one read, milliseconds.
        /// </summary>
        public double P99Ms { get; set; }

        /// <summary>
        /// Gets or sets the reads finished per second of wall time, all readers together.
        /// </summary>
        public double QueriesPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the records read per second of wall time, all readers together.
        /// </summary>
        public double RecordsPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the bytes allocated per read, the garbage the collector has to clean up for each symbol-day.
        /// </summary>
        public double AllocatedBytesPerRead { get; set; }

        /// <summary>
        /// Gets or sets the managed heap after the pass and a full collection, with the client still open, MB: what the client keeps.
        /// </summary>
        public double RetainedHeapMB { get; set; }

        /// <summary>
        /// Gets or sets the highest private memory committed during the pass, MB.
        /// </summary>
        public double PeakPrivateMemoryMB { get; set; }

        /// <summary>
        /// Gets or sets the highest working set during the pass, MB.
        /// </summary>
        public double PeakWorkingSetMB { get; set; }
    }
}
