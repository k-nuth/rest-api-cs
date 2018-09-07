using System;

namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetBlockByHash response data structure.
    /// </summary>
    public class GetBlockByHashResponse
    {
        /// <summary>
        /// True if and only if this block belongs to the main chain. 
        /// </summary>
        public bool isMainChain { get; set; }

        /// <summary>
        /// Fees earned by the miner who mined this block, in coin units.
        /// </summary>
        public decimal reward { get; set; }

        /// <summary>
        /// A representation of the computing power which was required to mine this block. 
        /// </summary>
        public double difficulty { get; set; }

        /// <summary>
        /// Refers to the pool to which the miner belongs (if applies).
        /// </summary>
        public PoolInfo poolInfo { get; set; }

        /// <summary>
        /// Packed/compressed representation of block difficulty.
        /// </summary>
        public string bits { get; set; }

        /// <summary>
        /// Total amount of hashes expected to be calculated to reach this block height from genesis.
        /// </summary>
        public string chainwork { get; set; }

        /// <summary>
        /// Block hash as a 64-character (32 bytes) hex string.
        /// </summary>
        public string hash { get; set; }

        /// <summary>
        /// Hash of all block transactions (Merkle tree root) as a 64-character (32 bytes) hex string.
        /// </summary>
        public string merkleroot { get; set; }

        /// <summary>
        /// Next block hash as a 64-character (32 bytes) hex string; for the latest block, this field is left empty.
        /// </summary>
        public string nextblockhash { get; set; }

        /// <summary>
        /// Previous block hash as a 64-character (32 bytes) hex string; for the first block (Genesis), this field is left empty.
        /// </summary>
        public string previousblockhash { get; set; }

        /// <summary>
        /// Block transaction ids.
        /// </summary>
        public string[] tx { get; set; }

        /// <summary>
        /// Block transaction length.
        /// </summary>
        public uint txCount { get; set; }

        /// <summary>
        /// Block nonce.
        /// </summary>
        public UInt32 nonce { get; set; }

        /// <summary>
        /// Block mining timestamp in Unix format.
        /// </summary>
        public UInt32 time { get; set; }

        /// <summary>
        /// Format version.
        /// </summary>
        public UInt32 version { get; set; }

        /// <summary>
        /// Every block on top of the current one is considered 1 confirmation.
        /// </summary>
        public UInt64 confirmations { get; set; }

        /// <summary>
        /// Block height (Genesis height is considered zero).
        /// </summary>
        public UInt64 height { get; set; }

        /// <summary>
        /// Serialized block size in bytes
        /// </summary>
        public UInt64 size { get; set; }
    }

}