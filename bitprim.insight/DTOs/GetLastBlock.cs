namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetLastBlock data structure
    /// </summary>
    public class GetLastBlock
    {
        /// <summary>
        /// Block height
        /// </summary>
        public ulong BlockHeight { get; set; }
        
        /// <summary>
        /// Block bits
        /// </summary>
        public uint Bits { get; set; }
        
        /// <summary>
        /// Block hash
        /// </summary>
        public byte[] Hash {get; set; }
    }
}