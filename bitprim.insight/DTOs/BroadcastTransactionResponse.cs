namespace bitprim.insight.DTOs
{
    /// <summary>
    /// BroadcastTransactionResponse data structure.
    /// </summary>
    public class BroadcastTransactionResponse
    {
        /// <summary>
        /// This object contains all the response information.
        /// </summary>
        public BroadCastTransactionResult txid { get; set; }
    }

    /// <summary>
    /// This object contains all the response information.
    /// </summary>
    public class BroadCastTransactionResult
    {
        /// <summary>
        /// Transaction hash as 64-character (32 bytes) hex string. This will identify it univocally inside the network.
        /// </summary>
        public string result { get; set; }

        /// <summary>
        /// Error message, if appropiate.
        /// </summary>
        public string error { get; set; }

        /// <summary>
        /// Internal transaction id.
        /// </summary>
        public int id { get; set; }
    }
}