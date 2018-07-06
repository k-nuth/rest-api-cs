using Bitprim;
using System;

namespace bitprim.insight.DTOs
{
    public class Utxo
    {
        public Utxo() {}

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

        public decimal amount { get; set; }
        public Int64 height { get; set; }
        public Int64 satoshis { get; set; }
        public string address { get; set; }
        public string scriptPubKey { get; set; }
        public string txid { get; set; }
        public UInt64 confirmations { get; set; }
        public UInt64 vout { get; set; }

        private static string GetOutputScript(Output output)
        {
            var script = output.Script;
            var scriptData = script.ToData(false);
            Array.Reverse(scriptData, 0, scriptData.Length);
            return Binary.ByteArrayToHexString(scriptData);
        }
    }
}