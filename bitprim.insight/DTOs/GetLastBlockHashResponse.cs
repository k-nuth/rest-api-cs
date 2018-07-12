namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetLastBlockHashResponse data structure.
    /// </summary>
    public class GetLastBlockHashResponse
    {
        /// <summary>
        /// Hash of the last block (tip) in the longest block chain, as a hex string.
        /// </summary>
        public string syncTipHash { get; set; }

        /// <summary>
        /// Hash of the last mined block, as a hex string.
        /// </summary>
        public string lastblockhash { get; set; }
    }
}