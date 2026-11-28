using System.IO;
using System.Linq;

namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// One root the benchmark generates contracts of: an OPRA option root (<c>cbbo-1m</c>) or a CME future root (<c>bbo-1m</c>)
    /// </summary>
    internal sealed class InstrumentSettings
    {
        /// <summary>
        /// Gets or sets the root, e.g. <c>SPXW</c> or <c>MES</c>.
        /// </summary>
        public string Root { get; set; }

        /// <summary>
        /// Gets or sets whether the root's contracts are options or futures.
        /// </summary>
        public InstrumentKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the price the underlying starts at; it then wanders by a seeded random walk.
        /// </summary>
        public double Price { get; set; } = 100;

        /// <summary>
        /// Gets or sets the distance between two strikes, options only.
        /// </summary>
        public double StrikeStep { get; set; } = 1;

        /// <summary>
        /// Gets or sets how far the strikes reach from the money, options only: this many below it, the strike at it and one fewer above, each
        /// listed as a call and a put, a strike of zero or below left out.
        /// </summary>
        public int StrikesPerSide { get; set; } = 10;

        /// <summary>
        /// Gets or sets the number of expirations quoted each day: the next trading days for options, the next quarterly months for futures.
        /// </summary>
        public int Expirations { get; set; } = 1;

        /// <summary>
        /// Gets or sets the records each contract has each day, one a minute.
        /// </summary>
        public int RecordsPerDay { get; set; } = 390;

        /// <summary>
        /// Gets or sets the minute of the UTC day the first record is at, e.g. 810 for 13:30.
        /// </summary>
        public int FirstMinuteUtc { get; set; } = 810;

        /// <summary>
        /// Gets the number of contracts quoted each day, before strikes of zero or below are left out.
        /// </summary>
        public int ContractsPerDay => this.Kind == InstrumentKind.Option ? this.Expirations * this.StrikesPerSide * 2 * 2 : this.Expirations;

        /// <summary>
        /// Checks the settings
        /// </summary>
        /// <exception cref="InvalidDataException">A setting is missing or out of range</exception>
        public void Check()
        {
            if(string.IsNullOrWhiteSpace(this.Root) || this.Root.Length > 6 || !this.Root.All(character => char.IsAsciiLetterOrDigit(character) || character == '.'))
            {
                throw new InvalidDataException(Invariant($"Root '{this.Root}' must be 1 to 6 ASCII letters, digits or dots, as a symbol's root is."));
            }

            if(this.Price <= 0 || this.StrikeStep <= 0 || this.StrikesPerSide < 1 || this.Expirations < 1)
            {
                throw new InvalidDataException(Invariant($"{this.Root}: Price, StrikeStep, StrikesPerSide and Expirations must be above zero."));
            }

            if(this.RecordsPerDay < 1 || this.FirstMinuteUtc < 0 || this.FirstMinuteUtc + this.RecordsPerDay > 1440)
            {
                throw new InvalidDataException(Invariant($"{this.Root}: FirstMinuteUtc {this.FirstMinuteUtc} plus RecordsPerDay {this.RecordsPerDay} must fit in a day of 1440 minutes."));
            }
        }
    }
}
