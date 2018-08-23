namespace bitprim.insight.DTOs
{
    /// <summary>
    /// BroadcastTransactionResponse data structure.
    /// </summary>
    public class BroadcastTransactionResponse
    {
        /// <summary>
        /// Transaction hash as 64-character (32 bytes) hex string. This will identify it univocally inside the network.
        /// </summary>
        public string txid { get; set; }
    }
}