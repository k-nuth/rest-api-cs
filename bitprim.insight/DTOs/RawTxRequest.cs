namespace bitprim.insight.DTOs
{
    /// <summary>
    /// RawTxRequest data structure.
    /// </summary>
    public class RawTxRequest
    {
        /// <summary>
        /// Hex string representing the raw transaction to try and broadcast.
        /// </summary>
        public string rawtx { get; set; }
    }
}