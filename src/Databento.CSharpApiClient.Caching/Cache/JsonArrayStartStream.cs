using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Databento.CSharpApiClient.Caching.Cache
{
    /// <summary>
    /// Passes a legacy day file's JSON through from its <c>[</c> on, as it is read: the whitespace before it goes, and data that starts with
    /// anything else is refused with <see cref="InvalidDataException"/>. The client's record reader takes only data starting with
    /// <c>[</c> as an array and reads it one record at a time; read as JSON lines, nothing would be an empty day and lines would be records.
    /// </summary>
    internal sealed class JsonArrayStartStream : Stream
    {
        private readonly Stream source;

        private bool arrayFound;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonArrayStartStream"/> class.
        /// </summary>
        /// <param name="source">The legacy day file's JSON, as UTF-8; left open</param>
        public JsonArrayStartStream(Stream source)
        {
            this.source = source;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            return this.Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            if(buffer.IsEmpty)
            {
                return 0;
            }

            while(true)
            {
                int read = this.source.Read(buffer);
                int kept = this.SkipToArray(buffer.Slice(0, read));
                if(kept > 0 || read == 0)
                {
                    return kept;
                }
            }
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if(buffer.IsEmpty)
            {
                return 0;
            }

            while(true)
            {
                int read = await this.source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                int kept = this.SkipToArray(buffer.Span.Slice(0, read));
                if(kept > 0 || read == 0)
                {
                    return kept;
                }
            }
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Drops the whitespace before the array from bytes just read, moving the rest to their start
        /// </summary>
        /// <param name="bytes">Bytes just read; empty at the end of the source</param>
        /// <returns>How many bytes are kept, at the start of <paramref name="bytes"/></returns>
        private int SkipToArray(Span<byte> bytes)
        {
            if(this.arrayFound)
            {
                return bytes.Length;
            }

            for(int i = 0; i < bytes.Length; ++i)
            {
                byte character = bytes[i];
                if(character == '[')
                {
                    this.arrayFound = true;
                    bytes.Slice(i).CopyTo(bytes);
                    return bytes.Length - i;
                }

                if(character != ' ' && character != '\t' && character != '\r' && character != '\n')
                {
                    break;
                }

                if(i == bytes.Length - 1)
                {
                    // whitespace only so far: the array may start in the next read
                    return 0;
                }
            }

            throw new InvalidDataException("The day file holds no JSON array of records.");
        }
    }
}
