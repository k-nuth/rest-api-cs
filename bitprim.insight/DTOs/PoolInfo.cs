namespace bitprim.insight.DTOs
{
    /// <summary>
    /// Block miner pool information.
    /// </summary>
    public class PoolInfo
    {
        /// <summary>
        /// Designates the block's miner's pool
        /// </summary>
        public string poolName { get; set; }

        /// <summary>
        /// The block's miner's pool url
        /// </summary>
        public string url { get; set; }
    }
}