namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetBestBlockHashResponse data structure.
    /// </summary>
    public class GetBestBlockHashResponse
    {
        /// <summary>
        /// Hash of the best block (tip) in the longest block chain, as a hex string.
        /// </summary>
        public string bestblockhash { get; set; }
    }
}