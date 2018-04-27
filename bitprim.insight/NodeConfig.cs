using System;

namespace bitprim.insight
{
    public class NodeConfig
    {
        public bool AcceptStaleRequests { get; set; }
        public bool InitializeNode { get; set; }
        public int Connections { get; set; }
        public int LongResponseCacheDurationInSeconds { get; set; }
        public int MaxBlockSummarySize { get; set; }
        public int MaxCachedBlocks { get; set; }
        public int ShortResponseCacheDurationInSeconds { get; set; }
        public int TransactionsByAddressPageSize { get; set; }
        public int WebSocketTimeoutInSeconds { get; set; }
        public string DateInputFormat { get; set; }
        public string ForwardUrl { get; set; }
        public string NodeConfigFile { get; set; }
        public string NodeType { get; set; }
        public string ProtocolVersion { get; set; }
        public string Proxy { get; set; }
        public string RelayFee { get; set; }
        public string TimeOffset { get; set; }
        public string Version { get; set; }
    }
}