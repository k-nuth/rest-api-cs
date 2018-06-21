namespace bitprim.insight
{
    public class NodeConfig
    {
        public bool AcceptStaleRequests { get; set; } = true;
        public bool InitializeNode { get; set; } = true;

        public int Connections { get; set; } = 8;
        public int LongResponseCacheDurationInSeconds { get; set; } = 86400;
        public int MaxBlockSummarySize { get; set; } = 500;

        ///<summary>
        /// This size is adimensional; we arbitrarily assign a block a size of BLOCK_CACHE_ENTRY_SIZE, and the blockchain height
        /// a size of BLOCKCHAIN_HEIGHT_CACHE_ENTRY_SIZE. The added size of cached blocks and the blockchain height will not exceed this value.
        ///</summary>
        public int MaxCacheSize { get; set; } = 50000;
        public int MaxSocketPublishRetries { get; set; } = 3;
        public int ShortResponseCacheDurationInSeconds { get; set; } = 30;
        public int SocketPublishRetryIntervalInSeconds { get; set; } = 1;
        public int TransactionsByAddressPageSize { get; set; } = 10;
        public int HttpClientTimeoutInSeconds { get; set; } = 5;
        public int WebsocketForwarderClientRetryDelay { get; set; } = 10;

        public double EstimateFeeDefault { get; set; } = 0.00001000;

        public string AllowedOrigins { get; set; } = "*";
        public string ApiPrefix { get; set; } = "api";
        public string DateInputFormat { get; set; } = "yyyy-MM-dd";
        public string ForwardUrl { get; set; } = "";
        public string NodeConfigFile { get; set; } = "";
        public string NodeType { get; set; } = "bitprim node";
        public string PoolsFile { get; set; } = "pools.json";
        public string ProtocolVersion { get; set; } = "70015";
        public string Proxy { get; set; } = "";
        public string RelayFee { get; set; } = "0.00001";
        public string TimeOffset { get; set; } = "0";
        public string Version { get; set; } = "";
    }
}
