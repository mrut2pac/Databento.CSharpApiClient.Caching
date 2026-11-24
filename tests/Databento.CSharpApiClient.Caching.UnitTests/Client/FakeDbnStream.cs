// Ignore Spelling: Cbbo dbn

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Databento.CSharpApiClient.DataModel;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// Writes a DBN v2 stream of CBBO 1-second records, as Databento answers a DBN request: its metadata maps each requested raw symbol
    /// to an instrument id over the request's days, and each record carries its symbol's instrument id
    /// </summary>
    internal static class FakeDbnStream
    {
        private const int SymbolWidth = 22;

        public static byte[] BuildCbbo1s(FakeRequest request, IReadOnlyList<ICbboRecord> records)
        {
            string[] symbols = request.Symbols;
            Dictionary<string, uint> instrumentIds = symbols.Select((symbol, i) => new KeyValuePair<string, uint>(symbol, (uint)(1001 + i))).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            DateTime firstDay = request.StartUtc.UtcDateTime.Date;
            DateTime endDay = request.EndUtc.UtcDateTime.AddTicks(-1).Date.AddDays(1);

            using(MemoryStream mappings = new MemoryStream())
            using(BinaryWriter mappingWriter = new BinaryWriter(mappings, Encoding.ASCII, leaveOpen: true))
            using(MemoryStream stream = new MemoryStream())
            using(BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                mappingWriter.Write((uint)symbols.Length);
                foreach(string symbol in symbols)
                {
                    WriteFixedString(mappingWriter, symbol);
                    mappingWriter.Write(1u);
                    mappingWriter.Write(ToYyyyMmDd(firstDay));
                    mappingWriter.Write(ToYyyyMmDd(endDay));
                    WriteFixedString(mappingWriter, Invariant($"{instrumentIds[symbol]}"));
                }

                mappingWriter.Flush();
                byte[] mappingBytes = mappings.ToArray();

                // prelude, then a 116-byte metadata body before the mappings
                writer.Write(Encoding.ASCII.GetBytes("DBN"));
                writer.Write((byte)2);
                writer.Write((uint)(116 + mappingBytes.Length));
                WriteFixedString(writer, request.Fields["dataset"], 16);
                writer.Write((ushort)0xC0);
                writer.Write(ToNanoseconds(request.StartUtc.UtcDateTime));
                writer.Write(ToNanoseconds(request.EndUtc.UtcDateTime));
                writer.Write(0UL);
                writer.Write((byte)0x01);
                writer.Write((byte)0x00);
                writer.Write((byte)0x00);
                writer.Write((ushort)SymbolWidth);
                writer.Write(new byte[53]);
                writer.Write(0u);
                writer.Write(0u);
                writer.Write(0u);
                writer.Write(0u);
                writer.Write(mappingBytes);

                foreach(ICbboRecord record in records)
                {
                    WriteCbbo(writer, record, instrumentIds[record.Symbol]);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void WriteCbbo(BinaryWriter writer, ICbboRecord record, uint instrumentId)
        {
            writer.Write((byte)20);
            writer.Write((byte)0xC0);
            writer.Write(record.PublisherId);
            writer.Write(instrumentId);
            writer.Write(ToNanoseconds(record.TsEventUtc.Value));
            writer.Write(ToNanoPrice(record.Price ?? double.NaN));
            writer.Write((uint)record.Size);
            writer.Write((byte)0);
            writer.Write((byte)'N');
            writer.Write((byte)record.Flags);
            writer.Write((byte)0);
            writer.Write(ToNanoseconds(record.TsReceivedUtc.Value));
            writer.Write(0UL);
            writer.Write(ToNanoPrice(record.BidPrice ?? double.NaN));
            writer.Write(ToNanoPrice(record.AskPrice ?? double.NaN));
            writer.Write((uint)record.BidSize);
            writer.Write((uint)record.AskSize);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
        }

        private static long ToNanoPrice(double price)
        {
            return double.IsNaN(price) ? long.MaxValue : (long)Math.Round(price * 1e9, MidpointRounding.AwayFromZero);
        }

        private static ulong ToNanoseconds(DateTime utc)
        {
            return (ulong)((utc - DateTime.UnixEpoch).Ticks * 100L);
        }

        private static uint ToYyyyMmDd(DateTime date)
        {
            return (uint)((date.Year * 10000) + (date.Month * 100) + date.Day);
        }

        private static void WriteFixedString(BinaryWriter writer, string value, int width = SymbolWidth)
        {
            byte[] buffer = new byte[width];
            byte[] encoded = Encoding.ASCII.GetBytes(value);
            Array.Copy(encoded, buffer, Math.Min(encoded.Length, width - 1));
            writer.Write(buffer);
        }
    }
}
