using System;
using System.Diagnostics;
using System.Threading;

namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// Samples this process's memory and CPU use while a step runs, for their peaks and average: the private bytes it has committed, and
    /// its working set, the part of its memory held in RAM
    /// </summary>
    internal sealed class ResourceSampler : IDisposable
    {
        private readonly Process process = Process.GetCurrentProcess();

        /// <summary>
        /// Lets one thread at a time refresh <see cref="process"/> and read it: the sampling timer, progress lines and <see cref="Stop"/>
        /// share it, and <see cref="Process"/> is not safe to use from several threads at once
        /// </summary>
        private readonly object gate = new object();

        private readonly Stopwatch clock = Stopwatch.StartNew();

        private readonly TimeSpan startCpu;

        private readonly TimeSpan startPause;

        private readonly Timer timer;

        private long peakPrivateBytes;

        private long peakWorkingSetBytes;

        private bool disposed;

        public ResourceSampler()
        {
            this.process.Refresh();
            this.startCpu = this.process.TotalProcessorTime;
            this.startPause = GC.GetTotalPauseDuration();
            this.timer = new Timer(_ => this.Sample(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        }

        /// <summary>
        /// Gets the time since sampling started.
        /// </summary>
        public TimeSpan Elapsed => this.clock.Elapsed;

        /// <summary>
        /// Gets the highest private memory seen, bytes.
        /// </summary>
        public long PeakPrivateBytes => Interlocked.Read(ref this.peakPrivateBytes);

        /// <summary>
        /// Gets the highest working set seen, bytes.
        /// </summary>
        public long PeakWorkingSetBytes => Interlocked.Read(ref this.peakWorkingSetBytes);

        /// <summary>
        /// Gets the working set now, bytes, for progress lines
        /// </summary>
        public long CurrentWorkingSetBytes()
        {
            lock(this.gate)
            {
                if(this.disposed)
                {
                    return 0;
                }

                this.process.Refresh();
                return this.process.WorkingSet64;
            }
        }

        /// <summary>
        /// Gets the private memory now, bytes, for progress lines
        /// </summary>
        public long CurrentPrivateBytes()
        {
            lock(this.gate)
            {
                if(this.disposed)
                {
                    return 0;
                }

                this.process.Refresh();
                return this.process.PrivateMemorySize64;
            }
        }

        /// <summary>
        /// Stops sampling and gives what was measured
        /// </summary>
        public ResourceUse Stop()
        {
            this.timer.Dispose();
            this.Sample();
            this.clock.Stop();
            TimeSpan cpu;
            lock(this.gate)
            {
                this.process.Refresh();
                cpu = this.process.TotalProcessorTime;
            }

            double cores = (cpu - this.startCpu).TotalSeconds / Math.Max(0.001, this.clock.Elapsed.TotalSeconds);
            return new ResourceUse
            {
                ElapsedSeconds = this.clock.Elapsed.TotalSeconds,
                PeakPrivateMemoryMB = this.PeakPrivateBytes / 1024.0 / 1024.0,
                PeakWorkingSetMB = this.PeakWorkingSetBytes / 1024.0 / 1024.0,
                AverageCoresBusy = cores,
                GcPausePercent = 100.0 * (GC.GetTotalPauseDuration() - this.startPause).TotalSeconds / Math.Max(0.001, this.clock.Elapsed.TotalSeconds),
            };
        }

        public void Dispose()
        {
            this.timer.Dispose();
            lock(this.gate)
            {
                if(!this.disposed)
                {
                    this.disposed = true;
                    this.process.Dispose();
                }
            }
        }

        private static void RaisePeak(ref long peak, long bytes)
        {
            long seen;
            do
            {
                seen = Interlocked.Read(ref peak);
            }
            while(bytes > seen && Interlocked.CompareExchange(ref peak, bytes, seen) != seen);
        }

        private void Sample()
        {
            lock(this.gate)
            {
                if(this.disposed)
                {
                    return;
                }

                this.process.Refresh();
                RaisePeak(ref this.peakPrivateBytes, this.process.PrivateMemorySize64);
                RaisePeak(ref this.peakWorkingSetBytes, this.process.WorkingSet64);
            }
        }
    }
}
