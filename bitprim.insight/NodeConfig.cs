namespace bitprim.insight
{
    public class NodeConfig
    {
        public bool AcceptStaleRequests { get; set; }
        public bool InitializeNode { get; set; }
        
        public int Connections { get; set; }
        public int LongResponseCacheDurationInSeconds { get; set; }
        public int MaxBlockSummarySize { get; set; }

        ///<summary>
        /// This size is adimensional; we arbitrarily assign a block a size of BLOCK_CACHE_ENTRY_SIZE, and the blockchain height
        /// a size of BLOCKCHAIN_HEIGHT_CACHE_ENTRY_SIZE. The added size of cached blocks and the blockchain height will not exceed this value.
        ///</summary>
        public int MaxCacheSize { get; set; }
        public int ShortResponseCacheDurationInSeconds { get; set; }
        public int TransactionsByAddressPageSize { get; set; }
        public int WebSocketTimeoutInSeconds { get; set; }
        
        public string ApiPrefix { get; set; }
        public string DateInputFormat { get; set; }
        public string ForwardUrl { get; set; }
        public string NodeConfigFile { get; set; }
        public string NodeType { get; set; }
        public string PoolsFile { get; set; }
        public string ProtocolVersion { get; set; }
        public string Proxy { get; set; }
        public string RelayFee { get; set; }
        public string TimeOffset { get; set; }
        public string Version { get; set; }
    }
}