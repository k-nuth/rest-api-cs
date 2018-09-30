using System;

namespace bitprim.insight.DTOs
{
    /// <summary>
    /// Represents an input from a coinbase transaction.
    /// </summary>
    public class CoinbaseTransactionInputSummary : TransactionInputSummary
    {
        /// <summary>
        /// For a coinbase transaction, hex representation of the coinbase script.
        /// For a non-coinbase transaction, this field will be empty.
        /// </summary>
        public string coinbase { get; set; }

        /// <summary>
        /// Zero-based index for the input in the transaction's input set.
        /// </summary>
        public UInt32 sequence { get; set; }
        
        /// <summary>
        /// Zero-based index for the input in the transaction's input set.
        /// </summary>
        public uint n { get; set; }
    }
}