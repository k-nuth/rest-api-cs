using System;

namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetSyncStatusResponse data structure.
    /// </summary>
    public class GetSyncStatusResponse
    {
        /// <summary>
        /// Current height of the blockchain (not the local node's copy, but the global value);
        /// "unknown" if this value cannot be obtained at the moment.
        /// </summary>
        public string blockChainHeight { get; set; }

        /// <summary>
        /// Error message.
        /// </summary>
        public string error { get; set; }

        /// <summary>
        /// (finished | synchronizing | unknown)
        /// </summary>
        public string status { get; set; }

        /// <summary>
        /// height / blockchainHeight * 100
        /// </summary>
        public string syncPercentage { get; set; }

        /// <summary>
        /// Node type.
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// Current height of the node's local copy of the blockchain.
        /// </summary>
        public UInt64 height { get; set; }
    }
}