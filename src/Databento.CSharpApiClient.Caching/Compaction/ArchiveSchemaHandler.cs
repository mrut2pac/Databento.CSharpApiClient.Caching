using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive.Schemas;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;

namespace Databento.CSharpApiClient.Caching.Compaction
{
    /// <summary>
    /// A Databento schema as a compaction run sees it: its archive columns and its day files
    /// </summary>
    /// <typeparam name="TSource">Record interface</typeparam>
    /// <typeparam name="TRecord">Record type day files and archive reads give</typeparam>
    internal sealed class ArchiveSchemaHandler<TSource, TRecord> : IArchiveSchemaHandler
        where TSource : class, IRecord
        where TRecord : class, TSource
    {
        public ArchiveSchemaHandler(DayFileFormat<TSource, TRecord> format)
        {
            this.Format = format;
        }

        public string Schema => this.Format.Schema;

        /// <summary>
        /// Gets the schema's day files.
        /// </summary>
        public DayFileFormat<TSource, TRecord> Format { get; }

        /// <summary>
        /// Gets the archive columns of the schema.
        /// </summary>
        public ArchiveRecordSchema<TSource, TRecord> RecordSchema => this.Format.RecordSchema;

        public Task CompactAsync(ArchiveGroupRequest request, CompactionRun run, CancellationToken cancellationToken)
        {
            return new ArchiveGroupCompaction<TSource, TRecord>(this, request, run).RunAsync(cancellationToken);
        }

        public Task UnpackAsync(ArchiveUnpackRequest request, UnpackRun run, CancellationToken cancellationToken)
        {
            return new ArchiveGroupUnpacking<TSource, TRecord>(this, request, run).RunAsync(cancellationToken);
        }
    }
}
