using System;

namespace bitprim.insight.DTOs
{
    /// <summary>
    /// TransactionOutputSummary data structure.
    /// </summary>
    public class TransactionOutputSummary
    {
        /// <summary>
        /// Output value in coin units.
        /// </summary>
        public string value { get; set; }

        /// <summary>
        /// Output index in transaction.
        /// </summary>
        public uint n { get; set; }


        /// <summary>
        /// Output script.
        /// </summary>
        public OutputScriptSummary scriptPubKey { get; set; }

        /// <summary>
        /// Spent transaction hash as 32-character hex string.
        /// </summary>
        public string spentTxId { get; set; }

        
        /// <summary>
        /// Previous output index inside its transaction.
        /// </summary>
        public UInt32? spentIndex { get; set; }

        /// <summary>
        /// Previous output's transaction's block height.
        /// </summary>
        public UInt64? spentHeight { get; set; }
    }

}