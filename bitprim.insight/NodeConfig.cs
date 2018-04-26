using System;

namespace bitprim.insight
{
    public class NodeConfig
    {
        public bool AcceptStaleRequests { get; set; }
        public int MaxBlockSummarySize { get; set; }
        public uint TransactionsByAddressPageSize { get; set; }
        public int WebSocketTimeoutInSeconds { get; set; }
        public string DateInputFormat { get; set; }
        public string NodeConfigFile { get; set; }
        public string NodeType { get; set; }
        public bool InitializeNode { get; set; }
        public string ForwardUrl { get; set; }
    }
}