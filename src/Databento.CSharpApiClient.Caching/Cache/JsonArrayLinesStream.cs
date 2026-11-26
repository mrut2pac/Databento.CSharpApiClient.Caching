using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Databento.CSharpApiClient.Caching.Cache
{
    /// <summary>
    /// Reads a JSON array of objects as JSON lines, one object a line, as it is read: the array's brackets and the whitespace between
    /// its objects go, each comma between them becomes a newline, and a line break inside an object becomes a space. A record reader
    /// then holds one record as text at a time instead of the whole day. Anything but one JSON array of objects is refused with
    /// <see cref="InvalidDataException"/>; the objects themselves are left to the reader.
    /// </summary>
    internal sealed class JsonArrayLinesStream : Stream
    {
        private readonly Stream source;

        private State state = State.BeforeArray;

        /// <summary>
        /// Brackets and braces open, the array's own included
        /// </summary>
        private int depth;

        private bool inString;

        private bool escaped;

        /// <summary>
        /// Whether the array's current element has begun, so a comma or the array's end is in its place
        /// </summary>
        private bool elementStarted;

        /// <summary>
        /// Whether a comma came after the last element, so another element must follow
        /// </summary>
        private bool commaSeen;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonArrayLinesStream"/> class.
        /// </summary>
        /// <param name="source">The JSON array, as UTF-8; left open</param>
        public JsonArrayLinesStream(Stream source)
        {
            this.source = source;
        }

        private enum State
        {
            BeforeArray,
            InArray,
            AfterArray,
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
                int written = this.Convert(buffer.Slice(0, read));
                if(written > 0 || read == 0)
                {
                    return written;
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
                int written = this.Convert(buffer.Span.Slice(0, read));
                if(written > 0 || read == 0)
                {
                    return written;
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

        private static bool IsWhitespace(byte character)
        {
            return character == ' ' || character == '\t' || character == '\r' || character == '\n';
        }

        /// <summary>
        /// Converts bytes read in place: the output is never longer than the input, as bytes are only dropped or replaced
        /// </summary>
        /// <param name="bytes">Bytes just read; empty at the end of the source</param>
        /// <returns>How many bytes of the output are at the start of <paramref name="bytes"/></returns>
        private int Convert(Span<byte> bytes)
        {
            if(bytes.IsEmpty)
            {
                if(this.state == State.BeforeArray)
                {
                    throw new InvalidDataException("The day file holds no JSON array of records.");
                }

                if(this.state == State.InArray)
                {
                    throw new InvalidDataException("The day file's JSON array is not closed.");
                }

                return 0;
            }

            int written = 0;
            for(int i = 0; i < bytes.Length; ++i)
            {
                byte character = bytes[i];
                if(this.state == State.BeforeArray)
                {
                    if(character == '[')
                    {
                        this.state = State.InArray;
                        this.depth = 1;
                    }
                    else if(!IsWhitespace(character))
                    {
                        throw new InvalidDataException("The day file holds no JSON array of records.");
                    }

                    continue;
                }

                if(this.state == State.AfterArray)
                {
                    if(!IsWhitespace(character))
                    {
                        throw new InvalidDataException("The day file holds more than its JSON array.");
                    }

                    continue;
                }

                if(this.inString)
                {
                    if(this.escaped)
                    {
                        this.escaped = false;
                    }
                    else if(character == '\\')
                    {
                        this.escaped = true;
                    }
                    else if(character == '"')
                    {
                        this.inString = false;
                    }

                    bytes[written++] = character;
                    continue;
                }

                if(this.depth == 1)
                {
                    // between the array's elements
                    if(IsWhitespace(character))
                    {
                        continue;
                    }

                    if(character == ',')
                    {
                        if(!this.elementStarted)
                        {
                            throw new InvalidDataException("The day file's JSON array has a comma without an element before it.");
                        }

                        this.elementStarted = false;
                        this.commaSeen = true;
                        bytes[written++] = (byte)'\n';
                        continue;
                    }

                    if(character == ']')
                    {
                        if(this.commaSeen && !this.elementStarted)
                        {
                            throw new InvalidDataException("The day file's JSON array ends with a comma.");
                        }

                        this.state = State.AfterArray;
                        continue;
                    }

                    if(this.elementStarted)
                    {
                        throw new InvalidDataException("The day file's JSON array has two elements without a comma between them.");
                    }

                    if(character != '{')
                    {
                        // an array element would be read as an array document of its own; a null, number, string or boolean is no record
                        throw new InvalidDataException("The day file's JSON array holds something other than records.");
                    }

                    this.elementStarted = true;
                    this.commaSeen = false;
                }

                if(character == '"')
                {
                    this.inString = true;
                }
                else if(character == '{' || character == '[')
                {
                    ++this.depth;
                }
                else if(character == '}' || character == ']')
                {
                    --this.depth;
                }
                else if(character == '\r' || character == '\n')
                {
                    character = (byte)' ';
                }

                bytes[written++] = character;
            }

            return written;
        }
    }
}
