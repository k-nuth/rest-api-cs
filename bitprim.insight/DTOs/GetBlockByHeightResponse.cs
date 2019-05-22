namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetBlockByHeightResponse data structure.
    /// </summary>
    public class GetBlockByHeightResponse
    {
        /// <summary>
        /// Block hash as 64-character (32 bytes) hex string.
        /// </summary>
        public string blockHash { get; set; }
    }
}