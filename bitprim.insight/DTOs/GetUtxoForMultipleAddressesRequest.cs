namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetUtxosForMultipleAddressesRequest data structure.
    /// </summary>
    public class GetUtxosForMultipleAddressesRequest
    {
        /// <summary>
        /// If and only if true, use legacy addresses in BCH.
        /// </summary>
        public bool legacy_addr { get; set; } = false;

        /// <summary>
        /// Comma-separated list of addresses; for BCH, cashaddr format is accepted.
        /// </summary>
        public string addrs { get; set; }
    }
}