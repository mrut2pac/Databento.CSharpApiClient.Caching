namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// What a step cost the process: time, peak memory, CPU and garbage collection
    /// </summary>
    internal sealed class ResourceUse
    {
        /// <summary>
        /// Gets or sets the wall time, seconds.
        /// </summary>
        public double ElapsedSeconds { get; set; }

        /// <summary>
        /// Gets or sets the highest private memory committed, sampled, MB: what the process reserved, including heap the garbage collector
        /// set aside but is not using.
        /// </summary>
        public double PeakPrivateMemoryMB { get; set; }

        /// <summary>
        /// Gets or sets the highest working set sampled, MB: the memory held in RAM, shared pages such as the runtime's included.
        /// </summary>
        public double PeakWorkingSetMB { get; set; }

        /// <summary>
        /// Gets or sets the CPU time over the wall time: how many cores were busy on average.
        /// </summary>
        public double AverageCoresBusy { get; set; }

        /// <summary>
        /// Gets or sets the share of the wall time the garbage collector paused the process, percent.
        /// </summary>
        public double GcPausePercent { get; set; }
    }
}
