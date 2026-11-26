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
    /// A JSON array read as JSON lines must keep every element as written, whatever its strings hold and however the reads cut it
    /// </summary>
    [TestClass]
    public class JsonArrayLinesStreamTests
    {
        // strings holding an escaped backslash right before the closing quote, an escaped quote followed by brackets and a comma, and
        // a line break inside an element
        private const string Array = " \r\n[ {\"a\":\"x\\\\\",\"b\":[1,2]},\r\n  {\"c\":\"q\\\"],{\",\r\n\"d\":{\"e\":null}} ] \n";

        private const string Lines = "{\"a\":\"x\\\\\",\"b\":[1,2]}\n{\"c\":\"q\\\"],{\",  \"d\":{\"e\":null}}";

        [TestMethod]
        public async Task ReadAsync_OneByteAtATime_GivesEachElementOnALine()
        {
            using(JsonArrayLinesStream stream = new JsonArrayLinesStream(new FakeOneByteStream(Array)))
            {
                Assert.AreEqual(Lines, await ReadAllAsync(stream));
            }
        }

        [TestMethod]
        public void Read_OneByteAtATime_GivesEachElementOnALine()
        {
            using(JsonArrayLinesStream stream = new JsonArrayLinesStream(new FakeOneByteStream(Array)))
            {
                Assert.AreEqual(Lines, ReadAll(stream));
            }
        }

        [TestMethod]
        public void Read_WholeArrayAtOnce_GivesEachElementOnALine()
        {
            using(JsonArrayLinesStream stream = new JsonArrayLinesStream(new MemoryStream(Encoding.UTF8.GetBytes(Array))))
            {
                Assert.AreEqual(Lines, ReadAll(stream));
            }
        }

        [TestMethod]
        public void Read_NoBuffer_Throws()
        {
            using(JsonArrayLinesStream stream = new JsonArrayLinesStream(new MemoryStream(Encoding.UTF8.GetBytes(Array))))
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
        /// A stream that gives one byte a read, so every token of the array is cut across reads
        /// </summary>
        private sealed class FakeOneByteStream : MemoryStream
        {
            public FakeOneByteStream(string text)
                : base(Encoding.UTF8.GetBytes(text))
            {
            }

            public override int Read(Span<byte> buffer)
            {
                return base.Read(buffer.Slice(0, Math.Min(1, buffer.Length)));
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return base.Read(buffer, offset, Math.Min(1, count));
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return base.ReadAsync(buffer.Slice(0, Math.Min(1, buffer.Length)), cancellationToken);
            }
        }
    }
}
