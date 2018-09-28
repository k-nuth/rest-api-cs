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
        /// </summary>
        public bool isCoinBase { get; set; }

        /// <summary>
        /// Transaction inputs.
        /// </summary>
        public TransactionInputSummary[] vin { get; set; }

        /// <summary>
        /// Transaction outputs.
        /// </summary>
        public TransactionOutputSummary[] vout { get; set; }


        /// <summary>
        /// Identifies the transaction's block (only for confirmed transactions).
        /// </summary>
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
        /// Sum of all inputs, in coin units.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public decimal valueIn { get; set; }

        /// <summary>
        /// Transaction fees, in coin units.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public decimal fees { get; set; }

    }

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

    /// <summary>
    /// InputScriptSummary data structure.
    /// </summary>
    public class InputScriptSummary
    {
        /// <summary>
        /// Script representation as raw hex data.
        /// </summary>
        public string hex { get; set; }

        /// <summary>
        /// Script representation in Script language.
        /// </summary>
        public string asm { get; set; }

    }

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

    /// <summary>
    /// OutputScriptSummary data structure.
    /// </summary>
    public class OutputScriptSummary
    {
        /// <summary>
        /// Script representation as raw hex data.
        /// </summary>
        public string hex { get; set; }

        /// <summary>
        /// Script representation in Script language.
        /// </summary>
        public string asm { get; set; }

        /// <summary>
        /// Output destination addresses.
        /// </summary>
        public string[] addresses { get; set; }

        /// <summary>
        /// Script type (pub2keyhash, multisig, etc)
        /// </summary>
        public string type { get; set; }
    }
}