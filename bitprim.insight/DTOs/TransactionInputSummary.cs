using Newtonsoft.Json;
using System;

namespace bitprim.insight.DTOs
{
    /// <summary>
    /// TransactionInputSummary data structure.
    /// </summary>
    public class TransactionInputSummary
    {
        /// <summary>
        /// For a coinbase transaction, hex representation of the coinbase script.
        /// For a non-coinbase transaction, this field will be empty.
        /// </summary>
        public string coinbase { get; set; }

        /// <summary>
        /// References the input's previous output.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string txid { get; set; }

        /// <summary>
        /// References the input's previous output.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
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
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]
        public string doubleSpentTxID { get; set; }
    }

}