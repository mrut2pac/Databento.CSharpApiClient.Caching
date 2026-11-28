namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// What an instrument's contracts are, which decides their dataset, schema and symbols
    /// </summary>
    internal enum InstrumentKind
    {
        /// <summary>
        /// OPRA options, <c>OPRA.PILLAR</c> <c>cbbo-1m</c>, OSI symbols such as <c>SPXW  261016C06600000</c>
        /// </summary>
        Option = 0,

        /// <summary>
        /// CME futures, <c>GLBX.MDP3</c> <c>bbo-1m</c>, raw symbols such as <c>MESZ6</c>
        /// </summary>
        Future = 1,
    }
}
