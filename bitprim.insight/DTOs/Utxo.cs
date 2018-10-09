using Bitprim;
using System;
using System.Numerics;

namespace bitprim.insight.DTOs
{
    /// <summary>
    /// Unspent transaction output data structure.
    /// </summary>
    public class Utxo
    {
        /// <summary>
        /// Empty default constructor.
        /// </summary>
        public Utxo() {}

        /// <summary>
        /// Destination address for this output.
        /// </summary>
        public string address { get; set; }

        /// <summary>
        /// Transaction hash as 64-character (32 bytes) hex string.
        /// </summary>
        public string txid { get; set; }

        /// <summary>
        /// Total unspent money for this output, in Satoshis.
        /// </summary>
        public UInt64 vout { get; set; }

        /// <summary>
        /// Output script.
        /// </summary>
        public string scriptPubKey { get; set; }

        /// <summary>
        /// Total unspent money for this output, in coin units.
        /// </summary>
        public decimal amount { get; set; }

        /// <summary>
        /// Total unspent money for this output, in Satoshis.
        /// </summary>
        public Int64 satoshis { get; set; }

        /// <summary>
        /// Height of the block which contains the transaction which contains this utxo.
        /// </summary>
        public BigInteger height { get; set; }

        /// <summary>
        /// For the block which contains this output.
        /// </summary>
        public UInt64 confirmations { get; set; }

        /// <summary>
        /// Returns true if and only if blockheight should be serialized.
        /// Naming convention is intentionally violated because Newtonsoft.Json relies
        /// on the "ShouldSerialize" prefix before the exact property name.
        /// </summary>
        public bool ShouldSerializeheight()
        {
            return height >= 0;
        }
    }
}