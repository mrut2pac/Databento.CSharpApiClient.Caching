using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Json;

namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// Invented market data in Databento's record shapes: contracts listed each weekday and one quote a minute for each, all derived from
    /// the settings' seed, so the same settings always give the same contracts and the same records. Nothing here is real market data.
    /// </summary>
    /// <remarks>
    /// Every weekday is a trading day: there is no holiday calendar. Option contracts are listed for the next <see cref="InstrumentSettings.Expirations"/>
    /// trading days, with strikes around the underlying's price on the expiration day, so one contract is quoted on several days; futures
    /// are listed for the next quarterly months. Thread-safe once built.
    /// </remarks>
    internal sealed class SyntheticMarket
    {
        private const string FutureMonthCodes = "HMUZ";

        private const ushort OpraPublisherId = 30;

        private const ushort GlobexPublisherId = 1;

        private readonly BenchmarkSettings settings;

        private readonly List<DateTime> tradingDays;

        /// <summary>
        /// The trading days from the start date, with enough after the end date for the last day's expirations
        /// </summary>
        private readonly List<DateTime> calendar;

        /// <summary>
        /// The underlying's closing price each calendar day, by root
        /// </summary>
        private readonly Dictionary<string, Dictionary<DateTime, double>> closes = new Dictionary<string, Dictionary<DateTime, double>>(StringComparer.Ordinal);

        public SyntheticMarket(BenchmarkSettings settings)
        {
            this.settings = settings;
            this.calendar = new List<DateTime>();
            int extraDays = settings.Instruments.Max(instrument => instrument.Expirations) * 100;
            for(DateTime day = settings.StartDate; day <= settings.EndDate.AddDays(extraDays); day = day.AddDays(1))
            {
                if(day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday)
                {
                    this.calendar.Add(day);
                }
            }

            this.tradingDays = this.calendar.Where(day => day <= settings.EndDate).ToList();
            foreach(InstrumentSettings instrument in settings.Instruments)
            {
                Random random = new Random(Hash(settings.Seed, instrument.Root));
                Dictionary<DateTime, double> rootCloses = new Dictionary<DateTime, double>();
                double price = instrument.Price;
                foreach(DateTime day in this.calendar)
                {
                    price *= Math.Exp(0.01 * NextGaussian(random));
                    rootCloses[day] = price;
                }

                this.closes[instrument.Root] = rootCloses;
            }
        }

        /// <summary>
        /// Gets the trading days from the start date to the end date.
        /// </summary>
        public IReadOnlyList<DateTime> TradingDays => this.tradingDays;

        /// <summary>
        /// Gets the Databento dataset an instrument's records are in
        /// </summary>
        public static string GetDataset(InstrumentSettings instrument)
        {
            return instrument.Kind == InstrumentKind.Option ? Datasets.OpraPillar : Datasets.GlbxMdp3;
        }

        /// <summary>
        /// Gets the Databento schema an instrument's records are in
        /// </summary>
        public static string GetSchema(InstrumentSettings instrument)
        {
            return instrument.Kind == InstrumentKind.Option ? Schema.ConsolidatedBBO1Min : Schema.Bbo1Min;
        }

        /// <summary>
        /// Lists the contracts of an instrument quoted on a day, in expiration then strike order, calls before puts
        /// </summary>
        public IReadOnlyList<SyntheticContract> ListContracts(InstrumentSettings instrument, DateTime day)
        {
            return instrument.Kind == InstrumentKind.Option ? this.ListOptions(instrument, day) : ListFutures(instrument, day);
        }

        /// <summary>
        /// Generates a contract's records of a day: <see cref="CbboRecordJson"/> for an option, <see cref="BboRecordJson"/> for a future
        /// </summary>
        public object[] CreateRecords(SyntheticContract contract, DateTime day)
        {
            InstrumentSettings instrument = contract.Instrument;
            Random random = new Random(Hash(this.settings.Seed, contract.Symbol + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)));
            double underlying = this.GetOpen(instrument.Root, day);
            uint instrumentId = (uint)Hash(0, contract.Symbol);
            double daysToExpiration = Math.Max(0.0, (contract.Expiration - day).TotalDays);
            object[] records = new object[instrument.RecordsPerDay];
            for(int index = 0; index < records.Length; ++index)
            {
                underlying *= Math.Exp(0.0005 * NextGaussian(random));
                DateTime minute = day.AddMinutes(instrument.FirstMinuteUtc + index);
                records[index] = instrument.Kind == InstrumentKind.Option
                    ? CreateOptionRecord(contract, minute, instrumentId, OptionMid(contract, underlying, daysToExpiration + ((records.Length - index) / 1440.0)), random)
                    : CreateFutureRecord(minute, instrumentId, underlying, random);
            }

            return records;
        }

        private static CbboRecordJson CreateOptionRecord(SyntheticContract contract, DateTime minute, uint instrumentId, double mid, Random random)
        {
            double halfSpread = Math.Max(0.025, Math.Round(mid * 0.01, 2));
            bool traded = random.NextDouble() < 0.1;
            return new CbboRecordJson
            {
                Header = new RecordHeaderJson
                {
                    RecordType = RType.Cbbo1M,
                    PublisherId = OpraPublisherId,
                    InstrumentId = instrumentId,
                    TsEventUtc = minute,
                },
                TsReceivedUtc = minute,
                Side = TradeAggressor.None,
                Price = traded ? Math.Round(mid, 2) : null,
                Size = traded ? random.Next(1, 50) : 0,
                Flags = MessageInfoBits.Last,
                Levels = new[]
                {
                    new CbboLevel1RecordJson
                    {
                        BidPrice = Math.Max(0.0, Math.Round(mid - halfSpread, 2)),
                        AskPrice = Math.Round(mid + halfSpread, 2),
                        BidSize = random.Next(1, 500),
                        AskSize = random.Next(1, 500),
                        BidPublisherId = (ushort)random.Next(20, 40),
                        AskPublisherId = (ushort)random.Next(20, 40),
                    },
                },
            };
        }

        private static BboRecordJson CreateFutureRecord(DateTime minute, uint instrumentId, double underlying, Random random)
        {
            double bid = Math.Round(underlying * 4) / 4;
            return new BboRecordJson
            {
                Header = new RecordHeaderJson
                {
                    RecordType = RType.Bbo1M,
                    PublisherId = GlobexPublisherId,
                    InstrumentId = instrumentId,
                    TsEventUtc = minute,
                },
                TsReceivedUtc = minute,
                Side = TradeAggressor.None,
                Price = bid,
                Size = (uint)random.Next(1, 20),
                Flags = MessageInfoBits.Last,
                Sequence = (uint)random.Next(),
                Levels = new[]
                {
                    new Mbp1LevelJson
                    {
                        BidPrice = bid,
                        AskPrice = bid + 0.25,
                        BidSize = (uint)random.Next(1, 100),
                        AskSize = (uint)random.Next(1, 100),
                        BidCount = (uint)random.Next(1, 30),
                        AskCount = (uint)random.Next(1, 30),
                    },
                },
            };
        }

        private static double OptionMid(SyntheticContract contract, double underlying, double daysToExpiration)
        {
            double intrinsic = Math.Max(0.0, contract.IsCall ? underlying - contract.Strike : contract.Strike - underlying);
            double deviation = underlying * 0.01 * Math.Sqrt(daysToExpiration + 0.05);
            double distance = Math.Abs(underlying - contract.Strike);
            double timeValue = 0.4 * deviation * Math.Exp(-distance * distance / (2 * deviation * deviation));
            return Math.Max(0.05, intrinsic + timeValue);
        }

        private static IReadOnlyList<SyntheticContract> ListFutures(InstrumentSettings instrument, DateTime day)
        {
            List<SyntheticContract> contracts = new List<SyntheticContract>();
            DateTime month = new DateTime(day.Year, day.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            while(contracts.Count < instrument.Expirations)
            {
                if(month.Month % 3 == 0 && ThirdFriday(month) >= day)
                {
                    string symbol = Invariant($"{instrument.Root}{FutureMonthCodes[(month.Month / 3) - 1]}{month.Year % 10}");
                    contracts.Add(new SyntheticContract(instrument, symbol, ThirdFriday(month), 0, false));
                }

                month = month.AddMonths(1);
            }

            return contracts;
        }

        private static DateTime ThirdFriday(DateTime month)
        {
            DateTime first = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            int toFriday = ((int)DayOfWeek.Friday - (int)first.DayOfWeek + 7) % 7;
            return first.AddDays(toFriday + 14);
        }

        /// <summary>
        /// A stable FNV-1a hash, the same in every process, unlike <see cref="string.GetHashCode()"/>
        /// </summary>
        private static int Hash(int seed, string text)
        {
            unchecked
            {
                uint hash = 2166136261u ^ (uint)seed;
                foreach(char character in text)
                {
                    hash = (hash ^ character) * 16777619u;
                }

                return (int)hash;
            }
        }

        private static double NextGaussian(Random random)
        {
            double first = 1.0 - random.NextDouble();
            double second = random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(first)) * Math.Cos(2.0 * Math.PI * second);
        }

        private IReadOnlyList<SyntheticContract> ListOptions(InstrumentSettings instrument, DateTime day)
        {
            List<SyntheticContract> contracts = new List<SyntheticContract>(instrument.ContractsPerDay);
            int first = this.calendar.BinarySearch(day);
            for(int expiration = 0; expiration < instrument.Expirations; ++expiration)
            {
                DateTime expirationDay = this.calendar[first + expiration];
                double atTheMoney = Math.Round(this.closes[instrument.Root][expirationDay] / instrument.StrikeStep) * instrument.StrikeStep;
                for(int step = -instrument.StrikesPerSide; step < instrument.StrikesPerSide; ++step)
                {
                    double strike = atTheMoney + (step * instrument.StrikeStep);
                    if(strike <= 0)
                    {
                        continue;
                    }

                    contracts.Add(CreateOption(instrument, expirationDay, strike, true));
                    contracts.Add(CreateOption(instrument, expirationDay, strike, false));
                }
            }

            return contracts;
        }

        private static SyntheticContract CreateOption(InstrumentSettings instrument, DateTime expiration, double strike, bool isCall)
        {
            long thousandths = (long)Math.Round(strike * 1000);
            string symbol = Invariant($"{instrument.Root,-6}{expiration:yyMMdd}{(isCall ? 'C' : 'P')}{thousandths:D8}");
            return new SyntheticContract(instrument, symbol, expiration, strike, isCall);
        }

        private double GetOpen(string root, DateTime day)
        {
            int index = this.calendar.BinarySearch(day);
            return index > 0 ? this.closes[root][this.calendar[index - 1]] : this.settings.Instruments.First(instrument => instrument.Root == root).Price;
        }
    }
}
