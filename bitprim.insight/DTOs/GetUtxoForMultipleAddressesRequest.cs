namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetUtxosForMultipleAddressesRequest data structure.
    /// </summary>
    public class GetUtxosForMultipleAddressesRequest
    {
        /// <summary>
        /// Comma-separated list of addresses; for BCH, cashaddr format is accepted.
        /// </summary>
        public string addrs { get; set; }
    }
}