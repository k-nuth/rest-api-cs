using System;

namespace bitprim.insight
{
    /// <summary>
    /// Higher-level API node configuration (as compared to the underlying node's .cfg file)
    /// </summary>
    public class NodeConfig
    {
        /// <summary>
        /// If and only if true, accept API requests when the node is synchronizing (i.e. not at the top of the chain).
        /// </summary>
        public bool AcceptStaleRequests { get; set; } = true;
        /// <summary>
        /// If and only if true, initialize the node's database. If this is set to true and the node's database is
        /// already initialized, node startup will fail.
        /// </summary>
        public bool InitializeNode { get; set; } = true;

        /// <summary>
        /// If and only if true, the node accepts websockets requests
        /// </summary>
        public bool WebsocketEnabled { get; set; } = true;
        /// <summary>
        /// If and only if true, the node send block messages
        /// </summary>
        public bool WebsocketMsgBlockEnabled { get; set; } = true;
        /// <summary>
        /// If and only if true, the node send tx messages
        /// </summary>
        public bool WebsocketMsgTxEnabled { get; set; } = true;
        /// <summary>
        /// If and only if true, the node send addresstx messages
        /// </summary>
        public bool WebsocketMsgAddressTxEnabled { get; set; } = true;

        /// <summary>
        /// Current amount of P2P connections the node has with other peers.
        /// </summary>
        public int Connections { get; set; } = 8;
        /// <summary>
        /// If first forwarding attempt via Http fails, wait this amount of time before retrying for the first time.
        /// </summary>
        public int ForwarderFirstRetryDelayInMillis { get; set; } = 500;
        /// <summary>
        /// Used in forwarder mode, when trying to send from forwarder to full node via Http.
        /// </summary>
        public int ForwarderMaxRetries { get; set; } = 3;
        /// <summary>
        /// Used in forwarder mode, when trying to send from forwarder to full node via Http.
        /// Retry delay cannot exceed this value.
        /// </summary>
        public int ForwarderMaxRetryDelayInSeconds { get; set; } = 10;
        /// <summary>
        /// For http response caching.
        /// </summary>
        public int LongResponseCacheDurationInSeconds { get; set; } = 86400;
        /// <summary>
        /// When querying multiple addresses, limit them to this value per query.
        /// </summary>
        public int MaxAddressesPerQuery { get; set; } = 10;
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
        /// When last read coin price is older than this value, it will be retrieved again.
        ///</summary>
        public int MaxCoinPriceAgeInSeconds { get; set; } = 300;
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
        /// External service url for consulting blockchain height.
        /// Some examples:
        /// BCH: "https://www.blocktrail.com/BCC/json/blockchain/homeStats"
        /// TBCH: "https://www.blocktrail.com/tBCC/json/blockchain/homeStats"
        /// BTC: "https://chain.so/api/v2/get_info/BTC"
        /// TBTC: "https://chain.so/api/v2/get_info/BTCTEST"
        /// LTC: "https://chain.so/api/v2/get_info/LTC"
        /// TLTC: "https://chain.so/api/v2/get_info/LTCTEST" 
        ///</summary>
        public string BlockchainHeightServiceUrl { get; set; } = "https://www.blocktrail.com/BCC/json/blockchain/homeStats";
        ///<summary>
        /// String representing an array of JSON property names (or indexes) to navigate to get to external
        /// service blockchain height. String elements must be between escaped double quotes.
        /// Some examples: (url ---> expression)
        /// BCH: "https://www.blocktrail.com/BCC/json/blockchain/homeStats" ---> "[\"last_blocks\", 0, \"height\"]"
        /// TBCH: "https://www.blocktrail.com/tBCC/json/blockchain/homeStats" ---> "[\"last_blocks\", 0, \"height\"]"
        /// BTC: "https://chain.so/api/v2/get_info/BTC" ---> "[\"data\", \"blocks\"]"
        /// TBTC: "https://chain.so/api/v2/get_info/BTCTEST" --->  "[\"data\", \"blocks\"]"
        /// LTC: "https://chain.so/api/v2/get_info/LTC" ---> "[\"data\", \"blocks\"]"
        /// TLTC: "https://chain.so/api/v2/get_info/LTCTEST" ---> "[\"data\", \"blocks\"]"
        ///</summary>
        public string BlockchainHeightParsingExpression { get; set; } = "[\"last_blocks\", 0, \"height\"]";
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

        ///<summary>
        /// This applies to the GetTransactions method from TransactionController.
        /// The value is measured in transaction count.
        ///</summary>
        public uint TransactionsByAddressPageSize { get; set; } = 10;

        ///<summary>
        /// If the last block's timestamp is older than this value in seconds,
        /// the blockchain will be considered stale.
        ///</summary>
        public UInt32 BlockchainStalenessThreshold { get; set; } = 43200;
    }
}
