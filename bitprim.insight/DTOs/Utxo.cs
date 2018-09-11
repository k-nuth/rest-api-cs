using Bitprim;
using System;

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
        /// Constructor for all fields.
        /// </summary>
        /// <param name="paymentAddress"> Utxo destination address. </param>
        /// <param name="outputPoint"> Utxo reference (tx hash + output index). </param>
        /// <param name="getTxEc"> Containing transaction retrieval result. </param>
        /// <param name="tx"> Transaction containing this output. </param>
        /// <param name="compact"> History entry referencing this utxo. </param>
        /// <param name="topHeight"> Current blockchain height. </param>
        public Utxo(PaymentAddress paymentAddress, Point outputPoint, ErrorCode getTxEc,
                    Transaction tx, HistoryCompact compact, UInt64 topHeight)
        {
            address = paymentAddress.Encoded;
            txid = Binary.ByteArrayToHexString(outputPoint.Hash);
            vout = outputPoint.Index;
            scriptPubKey = getTxEc == ErrorCode.Success ? GetOutputScript(tx.Outputs[outputPoint.Index]) : null;
            amount = Utils.SatoshisToCoinUnits(compact.ValueOrChecksum);
            satoshis = (Int64) compact.ValueOrChecksum;
            height = compact.Height;
            confirmations = topHeight - compact.Height + 1;
        }

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
        public Int64 height { get; set; }

        /// <summary>
        /// For the block which contains this output.
        /// </summary>
        public UInt64 confirmations { get; set; }
        
        private static string GetOutputScript(Output output)
        {
            var script = output.Script;
            var scriptData = script.ToData(false);
            Array.Reverse(scriptData, 0, scriptData.Length);
            return Binary.ByteArrayToHexString(scriptData);
        }
    }
}