using System;
using Newtonsoft.Json;

namespace bitprim.insight.DTOs
{
    /// <summary>
    /// TransactionSummary data structure.
    /// </summary>
    public class TransactionSummary
    {
        /// <summary>
        /// 64-character (32 bytes) hex string which univocally identifies the transaction in the network.
        /// </summary>
        public string txid { get; set; }

        /// <summary>
        /// Transaction protocol version.
        /// </summary>
        public UInt32 version { get; set; }

        /// <summary>
        /// Transaction locktime, expressed as the blockchain height at which the transaction will be considered confirmed.
        /// </summary>
        public UInt32 locktime { get; set; }

        /// <summary>
        /// True if and only if this transaction is coinbase (i.e. generates new coins).
        /// Not serialized when false (default value).
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool isCoinBase { get; set; }

        /// <summary>
        /// Transaction inputs.
        /// </summary>
        public TransactionInputSummary[] vin { get; set; }

        /// <summary>
        /// Sum of all inputs, in coin units.
        /// Not serialized when zero (default value).
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public decimal valueIn { get; set; }

        /// <summary>
        /// Transaction fees, in coin units.
        /// Not serialized when inputs == 0 (default value).
        /// </summary>
        public decimal fees { get; set; }

        /// <summary>
        /// Transaction outputs.
        /// </summary>
        public TransactionOutputSummary[] vout { get; set; }


        /// <summary>
        /// Identifies the transaction's block (only for confirmed transactions).
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string blockhash { get; set; }


        /// <summary>
        /// Height of the transaction's block (only for confirmed transactions).
        /// </summary>
        public Int64 blockheight { get; set; }

        /// <summary>
        /// Amount of blocks on top of the transaction's block.
        /// </summary>
        public UInt64 confirmations { get; set; }
        
        /// <summary>
        /// Unix timestamp which marks when the transaction entered the blockchain.
        /// </summary>
        public UInt32 time { get; set; }

        /// <summary>
        /// Block mining timestamp.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public UInt32 blocktime { get; set; }

        /// <summary>
        /// Sum of all outputs, in coin units.
        /// </summary>
        public decimal valueOut { get; set; }

        /// <summary>
        /// Transaction serialized size in bytes.
        /// </summary>
        public UInt64 size { get; set; }

        /// <summary>
        /// Returns true if and only if blockheight should be serialized.
        /// Naming convention is intentionally violated because Newtonsoft.Json relies
        /// on the "ShouldSerialize" prefix before the exact property name.
        /// </summary>
        public bool ShouldSerializeblockheight()
        {
            return blockheight >= 0;
        }

        /// <summary>
        /// Returns true if and only if fees should be serialized.
        /// Naming convention is intentionally violated because Newtonsoft.Json relies
        /// on the "ShouldSerialize" prefix before the exact property name.
        /// </summary>
        public bool ShouldSerializefees()
        {
            return valueIn > 0;
        }
    }

}