using System.Collections.Generic;

using Databento.CSharpApiClient.Caching.Archive;

namespace Databento.CSharpApiClient.Caching.Compaction
{
    /// <summary>
    /// One archive version to unpack: a group's current full file and delta in one month folder of a dataset's schema
    /// </summary>
    internal sealed class ArchiveUnpackRequest
    {
        public ArchiveUnpackRequest(string directory, string relativeDirectory, string dataset, string schema, string group, int year, int month, ArchiveVersion version, IReadOnlyList<string> otherFiles, IReadOnlyList<string> temporaryFiles)
        {
            this.Directory = directory;
            this.RelativeDirectory = relativeDirectory;
            this.Dataset = dataset;
            this.Schema = schema;
            this.Group = group;
            this.Year = year;
            this.Month = month;
            this.Version = version;
            this.OtherFiles = otherFiles;
            this.TemporaryFiles = temporaryFiles;
        }

        /// <summary>
        /// Gets the month folder, full path.
        /// </summary>
        public string Directory { get; }

        /// <summary>
        /// Gets the month folder relative to the cache root, e.g. <c>OPRA.PILLAR\cbbo-1m\2026\10</c>.
        /// </summary>
        public string RelativeDirectory { get; }

        /// <summary>
        /// Gets the Databento dataset.
        /// </summary>
        public string Dataset { get; }

        /// <summary>
        /// Gets the Databento schema.
        /// </summary>
        public string Schema { get; }

        /// <summary>
        /// Gets the archive group, e.g. <c>SPXW.OPT</c>.
        /// </summary>
        public string Group { get; }

        /// <summary>
        /// Gets the year.
        /// </summary>
        public int Year { get; }

        /// <summary>
        /// Gets the month, 1 to 12.
        /// </summary>
        public int Month { get; }

        /// <summary>
        /// Gets the version to unpack, its full file and any delta.
        /// </summary>
        public ArchiveVersion Version { get; }

        /// <summary>
        /// Gets the group's full files and deltas outside the current version, as the month folder's listing found them: what a stopped
        /// or failed compaction left.
        /// </summary>
        public IReadOnlyList<string> OtherFiles { get; }

        /// <summary>
        /// Gets the temporary files of the group's archive files a stopped run left, never published.
        /// </summary>
        public IReadOnlyList<string> TemporaryFiles { get; }

        /// <summary>
        /// Gets the group-month as reports name it, e.g. <c>OPRA.PILLAR\cbbo-1m\2026\10 SPXW.OPT</c>.
        /// </summary>
        public string DisplayName => Invariant($"{this.RelativeDirectory} {this.Group}");
    }
}
