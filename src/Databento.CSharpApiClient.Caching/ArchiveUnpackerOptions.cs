using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// Settings of a <see cref="DatabentoArchiveUnpacker"/> run: the cache, which group-months of its archive to unpack, and how
    /// </summary>
    public sealed class ArchiveUnpackerOptions
    {
        /// <summary>
        /// Gets or sets the cache's root folder. Required.
        /// </summary>
        public string CacheRoot { get; set; }

        /// <summary>
        /// Gets or sets the only dataset unpacked, as its folder is named, e.g. <c>OPRA.PILLAR</c>; <c>null</c> for every dataset.
        /// </summary>
        public string Dataset { get; set; }

        /// <summary>
        /// Gets or sets the only schema unpacked, as Databento names it, e.g. <c>cbbo-1m</c>; <c>null</c> for every schema.
        /// </summary>
        public string Schema { get; set; }

        /// <summary>
        /// Gets or sets the only archive group unpacked, as archive file names carry it, e.g. <c>SPXW.OPT</c> for SPXW options or
        /// <c>MNQ.FUT</c> for MNQ futures; <c>null</c> for every group.
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Gets or sets the first month unpacked, as any day of it; <c>null</c> for no lower bound. A month is unpacked whole or not at all.
        /// </summary>
        public DateOnly? FirstMonth { get; set; }

        /// <summary>
        /// Gets or sets the last month unpacked, as any day of it; <c>null</c> for no upper bound.
        /// </summary>
        public DateOnly? LastMonth { get; set; }

        /// <summary>
        /// Gets or sets whether the run only counts the day files it would write and their size, changing nothing in the cache, so the disk
        /// needed is known first: day files take more room than the archive.
        /// </summary>
        public bool DryRun { get; set; }

        /// <summary>
        /// Gets or sets how many group-months are unpacked at once; 1, the default, unpacks them one after another.
        /// </summary>
        public int MaxParallelGroups { get; set; } = 1;

        /// <summary>
        /// Gets or sets the logger of the run's steps and failures; <see cref="NullLogger.Instance"/> by default. A logger that throws loses
        /// its line and changes nothing.
        /// </summary>
        public ILogger Logger { get; set; } = NullLogger.Instance;
    }
}
