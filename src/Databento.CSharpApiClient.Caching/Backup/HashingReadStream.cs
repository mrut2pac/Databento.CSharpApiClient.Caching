using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Databento.CSharpApiClient.Caching.Backup
{
    /// <summary>
    /// Reads a stream front to back, counting and hashing (SHA-256) every byte read, so a file is hashed as it is copied rather than read twice
    /// </summary>
    internal sealed class HashingReadStream : Stream
    {
        private readonly Stream source;

        private readonly bool leaveOpen;

        private readonly long? length;

        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        /// <summary>
        /// Wraps a stream to be read once, front to back
        /// </summary>
        /// <param name="source">The stream read</param>
        /// <param name="leaveOpen">Whether disposing of this stream leaves <paramref name="source"/> open</param>
        /// <param name="length">The bytes the source holds when known, e.g. a file's; then the stream reports its length and position, so
        /// a tar writer puts the size in the entry's header up front</param>
        public HashingReadStream(Stream source, bool leaveOpen = false, long? length = null)
        {
            this.source = source;
            this.leaveOpen = leaveOpen;
            this.length = length;
        }

        /// <summary>
        /// Gets the number of bytes read so far.
        /// </summary>
        public long BytesRead { get; private set; }

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => this.length.HasValue;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => this.length ?? throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Position
        {
            get => this.BytesRead;
            set => this.Seek(value, SeekOrigin.Begin);
        }

        /// <summary>
        /// Gets the SHA-256 of the bytes read so far, lowercase hex
        /// </summary>
        public string GetHashHex()
        {
            return Convert.ToHexString(this.hash.GetCurrentHash()).ToLowerInvariant();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.Read(buffer.AsSpan(offset, count));
        }

        /// <inheritdoc/>
        public override int Read(Span<byte> buffer)
        {
            int read = this.source.Read(buffer);
            this.Count(buffer[..read]);
            return read;
        }

        /// <inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await this.source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            this.Count(buffer.Span[..read]);
            return read;
        }

        /// <inheritdoc/>
        public override void Flush()
        {
        }

        /// <summary>
        /// Only "seeks" to where the stream already is, which is all a reader that checks its position asks for; the bytes are read once
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin)
        {
            long target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => this.BytesRead + offset,
                _ => this.Length + offset,
            };

            if(target != this.BytesRead)
            {
                throw new NotSupportedException("The stream is read once, front to back.");
            }

            return target;
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                this.hash.Dispose();
                if(!this.leaveOpen)
                {
                    this.source.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private void Count(ReadOnlySpan<byte> bytes)
        {
            this.hash.AppendData(bytes);
            this.BytesRead += bytes.Length;
        }
    }
}
