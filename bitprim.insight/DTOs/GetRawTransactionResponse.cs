namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetRawTransactionResponse data structure.
    /// </summary>
    public class GetRawTransactionResponse
    {
        /// <summary>
        /// Transaction representation as a hex string.
        /// </summary>
        public string rawtx { get; set; }
    }
}