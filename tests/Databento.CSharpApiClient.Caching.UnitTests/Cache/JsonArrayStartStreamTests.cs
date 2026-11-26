using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Cache;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests.Cache
{
    /// <summary>
    /// A day file's JSON must reach the record reader from its array on, as written, however the reads cut it, and nothing but an
    /// array may
    /// </summary>
    [TestClass]
    public class JsonArrayStartStreamTests
    {
        private const string Array = "[{\"a\":\" [x\"},\r\n {\"b\":1}] \n";

        [TestMethod]
        public async Task ReadAsync_WhitespaceBeforeTheArray_OneByteAtATime_StartsAtTheArray()
        {
            using(JsonArrayStartStream stream = new JsonArrayStartStream(new FakeOneByteStream(" \t\r\n " + Array)))
            {
                Assert.AreEqual(Array, await ReadAllAsync(stream));
            }
        }

        [TestMethod]
        public void Read_WhitespaceBeforeTheArray_OneByteAtATime_StartsAtTheArray()
        {
            using(JsonArrayStartStream stream = new JsonArrayStartStream(new FakeOneByteStream(" \t\r\n " + Array)))
            {
                Assert.AreEqual(Array, ReadAll(stream));
            }
        }

        [TestMethod]
        public void Read_WhitespaceBeforeTheArray_AllAtOnce_StartsAtTheArray()
        {
            using(JsonArrayStartStream stream = new JsonArrayStartStream(new MemoryStream(Encoding.UTF8.GetBytes("\n\n" + Array))))
            {
                Assert.AreEqual(Array, ReadAll(stream));
            }
        }

        [TestMethod]
        [DataRow("", DisplayName = "nothing")]
        [DataRow(" \r\n\t", DisplayName = "whitespace only")]
        [DataRow("x[]", DisplayName = "text before the array")]
        [DataRow("{\"a\":1}\n", DisplayName = "JSON lines")]
        [DataRow("\uFEFF[]", DisplayName = "a byte order mark")]
        public void Read_NoArray_Throws(string json)
        {
            using(JsonArrayStartStream stream = new JsonArrayStartStream(new FakeOneByteStream(json)))
            {
                Assert.ThrowsException<InvalidDataException>(() => ReadAll(stream));
            }
        }

        [TestMethod]
        [DataRow("", DisplayName = "nothing")]
        [DataRow(" \r\n\t", DisplayName = "whitespace only")]
        [DataRow("x[]", DisplayName = "text before the array")]
        [DataRow("{\"a\":1}\n", DisplayName = "JSON lines")]
        [DataRow("\uFEFF[]", DisplayName = "a byte order mark")]
        public async Task ReadAsync_NoArray_Throws(string json)
        {
            using(JsonArrayStartStream stream = new JsonArrayStartStream(new FakeOneByteStream(json)))
            {
                await Assert.ThrowsExceptionAsync<InvalidDataException>(() => ReadAllAsync(stream));
            }
        }

        [TestMethod]
        public void Read_NoBuffer_Throws()
        {
            using(JsonArrayStartStream stream = new JsonArrayStartStream(new MemoryStream(Encoding.UTF8.GetBytes(Array))))
            {
                Assert.ThrowsException<ArgumentNullException>(() => stream.Read(null, 0, 0));
            }
        }

        private static async Task<string> ReadAllAsync(Stream stream)
        {
            using(MemoryStream output = new MemoryStream())
            {
                byte[] buffer = new byte[3];
                int read;
                while((read = await stream.ReadAsync(buffer.AsMemory(), CancellationToken.None)) > 0)
                {
                    output.Write(buffer, 0, read);
                }

                return Encoding.UTF8.GetString(output.ToArray());
            }
        }

        private static string ReadAll(Stream stream)
        {
            using(MemoryStream output = new MemoryStream())
            {
                byte[] buffer = new byte[3];
                int read;
                while((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                }

                return Encoding.UTF8.GetString(output.ToArray());
            }
        }

        /// <summary>
        /// A stream that gives one byte a read, so the whitespace and the array's start are cut across reads
        /// </summary>
        private sealed class FakeOneByteStream : Stream
        {
            private readonly byte[] bytes;

            private int position;

            public FakeOneByteStream(string text)
            {
                this.bytes = Encoding.UTF8.GetBytes(text);
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

            public override int Read(Span<byte> buffer)
            {
                if(buffer.IsEmpty || this.position == this.bytes.Length)
                {
                    return 0;
                }

                buffer[0] = this.bytes[this.position++];
                return 1;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return this.Read(buffer.AsSpan(offset, count));
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return ValueTask.FromResult(this.Read(buffer.Span));
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
        }
    }
}
