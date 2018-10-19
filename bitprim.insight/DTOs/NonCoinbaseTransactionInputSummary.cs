using Newtonsoft.Json;
using System;

namespace bitprim.insight.DTOs
{
    /// <summary>
    /// Represents an input from a non coinbase transaction.
    /// </summary>
    public class NonCoinbaseTransactionInputSummary : TransactionInputSummary
    {
        /// <summary>
        /// References the input's previous output.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string txid { get; set; }

        /// <summary>
        /// References the input's previous output.
        /// </summary>
        public UInt32 vout { get; set; }

        /// <summary>
        /// Zero-based index for the input in the transaction's input set.
        /// </summary>
        public UInt32 sequence { get; set; }
        
        /// <summary>
        /// Zero-based index for the input in the transaction's input set.
        /// </summary>
        public uint n { get; set; }

        /// <summary>
        /// Input script.
        /// </summary>
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]
        public InputScriptSummary scriptSig { get; set; }

        
        /// <summary>
        /// Previous output destination address.
        /// </summary>
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]
        public string addr { get; set; }

        
        /// <summary>
        /// Output value in Satoshis.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public UInt64 valueSat { get; set; }
        
        /// <summary>
        /// Input value in coin units.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public decimal value { get; set; }

        /// <summary>
        /// If this input is a double spend, this will point to the conflicting transaction.
        /// </summary>
        public string doubleSpentTxID { get; set; }

        /// <summary>
        /// Fixed dummy for matching insight response.
        /// </summary>
        public bool? isConfirmed { get; set; } = null;

        /// <summary>
        /// Fixed dummy for matching insight response.
        /// </summary>
        public UInt64? confirmations { get; set; } = null;

        /// <summary>
        /// Fixed dummy for matching insight response.
        /// </summary>
        public bool? unconfirmedInput { get; set; } = null;
    }
}