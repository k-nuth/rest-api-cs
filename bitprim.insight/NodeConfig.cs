namespace bitprim.insight
{
    /// <summary>
    /// Higher-level API node configuration (as compared to the underlying node's .cfg file)
    /// </summary>
    public class NodeConfig
    {
        /// <summary>
        /// Iif true, accept API requests when the node is synchronizing (i.e. not at the top of the chain).
        /// </summary>
        public bool AcceptStaleRequests { get; set; } = true;
        /// <summary>
        /// Iif true, initialize the node's database. If this is set to true and the node's database is
        /// already initialized, node startup will fail.
        /// </summary>
        public bool InitializeNode { get; set; } = true;

        /// <summary>
        /// Current amount of P2P connections the node has with other peers.
        /// </summary>
        public int Connections { get; set; } = 8;
        /// <summary>
        /// For http response caching.
        /// </summary>
        public int LongResponseCacheDurationInSeconds { get; set; } = 86400;
        /// <summary>
        /// This size is measured in block count.
        /// </summary>
        public int MaxBlockSummarySize { get; set; } = 500;

        ///<summary>
        /// This size is adimensional; we arbitrarily assign a block a size of BLOCK_CACHE_ENTRY_SIZE, and the blockchain height
        /// a size of BLOCKCHAIN_HEIGHT_CACHE_ENTRY_SIZE. The added size of cached blocks and the blockchain height will not exceed this value.
        ///</summary>
        public int MaxCacheSize { get; set; } = 50000;
        ///<summary>
        /// This value applies to transaction, block and wallet subscriptions; if the client cannot be reached for these after this amount of
        /// tries, the server (i.e. the bitprim-insight API node) will give up and remove the client from its list of subscribers.
        ///</summary>
        public int MaxSocketPublishRetries { get; set; } = 3;
        ///<summary>
        /// For http request caching.
        ///</summary>
        public int ShortResponseCacheDurationInSeconds { get; set; } = 30;
        ///<summary>
        /// This value applies to transaction, block and wallet subscriptions; this is the retry interval for notifying the client.
        ///</summary>
        public int SocketPublishRetryIntervalInSeconds { get; set; } = 1;
        ///<summary>
        /// This applies to the GetTransactions method from TransactionController.
        /// The value is measured in transaction count.
        ///</summary>
        public int TransactionsByAddressPageSize { get; set; } = 10;
        ///<summary>
        /// This applies to http communication with the client.
        ///</summary>
        public int HttpClientTimeoutInSeconds { get; set; } = 5;
        ///<summary>
        /// Used in forwarder mode.
        ///</summary>
        public int WebsocketForwarderClientRetryDelay { get; set; } = 10;

        ///<summary>
        /// In coin units.
        ///</summary>
        public double EstimateFeeDefault { get; set; } = 0.00001000;

        ///<summary>
        /// Allowed CORS origins (semicolon-separared list).
        ///</summary>
        public string AllowedOrigins { get; set; } = "*";
        ///<summary>
        /// Added between domain and api method name + parameters. 
        ///</summary>
        public string ApiPrefix { get; set; } = "api";
        ///<summary>
        /// Used for searching blocks by date.
        ///</summary>
        public string DateInputFormat { get; set; } = "yyyy-MM-dd";
        ///<summary>
        /// Used in forwarder mode to determine where to forward requests.
        ///</summary>
        public string ForwardUrl { get; set; } = "";
        ///<summary>
        /// Underlying node configuration file (lower level config).
        ///</summary>
        public string NodeConfigFile { get; set; } = "";
        ///<summary>
        /// Informative.
        ///</summary>
        public string NodeType { get; set; } = "bitprim node";
        ///<summary>
        /// Path to file detailing pools and their signatures. See pools.json for format.
        ///</summary>
        public string PoolsFile { get; set; } = "pools.json";
        ///<summary>
        /// Current blockchain protocol version used by the underlying node.
        ///</summary>
        public string ProtocolVersion { get; set; } = "70015";
        ///<summary>
        /// Node's proxy url, if applies.
        ///</summary>
        public string Proxy { get; set; } = "";
        ///<summary>
        /// Node relay fee.
        ///</summary>
        public string RelayFee { get; set; } = "0.00001";
        ///<summary>
        /// To apply to UTC times; for example, for -3 GMT, it would be -0300.
        ///</summary>
        public string TimeOffset { get; set; } = "0";
        ///<summary>
        /// bitprim-insight API version.
        ///</summary>
        public string Version { get; set; } = "";
    }
}
