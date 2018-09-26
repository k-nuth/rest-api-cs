namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetTxsForMultipleAddressesRequest data structure.
    /// </summary>
    public class GetTxsForMultipleAddressesRequest
    {
        /// <summary>
        /// Comma-separated list of addresses; for BCH, cashaddr format is accepted.
        /// The maximum amount of addresses is determined by the MaxAddressesPerQuery configuration key.
        /// </summary>
        public string addrs { get; set; }

        /// <summary>
        /// For selecting a subset of the transaction list; starts in zero for the first one.
        /// </summary>
        public int from { get; set; } = 0;

        /// <summary>
        /// For selecting a subset of the transaction list; max value is txCount - 1.
        /// </summary>
        public int to { get; set; } = 10;

        /// <summary>
        /// Choose whether or not to include asm representation of input and output scripts. (1 = include, 0 = don't include)
        /// </summary>
        public int noAsm { get; set; } = 1;

        /// <summary>
        /// Choose whether or not to include scriptsig for scripts. (1 = include, 0 = don't include)
        /// </summary>
        public int noScriptSig { get; set; } = 1;

        /// <summary>
        /// Choose whether or not to include spend information. (1 = include, 0 = don't include)
        /// </summary>
        public int noSpend { get; set; } = 1;

        /// <summary>
        /// If and only if true, return BCH addresses in legacy format.
        /// </summary>
        public bool legacyAddressFormat { get; set; } = false;
    }
}