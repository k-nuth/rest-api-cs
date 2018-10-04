using Xunit;

namespace bitprim.insight.tests
{
    public class AsmFormatterTests
    {
        [Fact]
        public void Coinbase()
        {
            var asmFormatter = new AsmFormatter();
            Assert.Equal
            (
                "304402201465bc1f92884134dc5b660c2655dbdc555d9a6eeea50c89d3b6c25082917d5d0220687d6a7b2442f162e34493b13b56d71" + 
                "6acfe7f0852fee33fe26e0098cef0aa0641 03758d7df1e4f797e0e68303deb91c06eebcaae2b5bab5151f8e2008990c957b0f",
                asmFormatter.Format
                (
                    "[304402201465bc1f92884134dc5b660c2655dbdc555d9a6eeea50c89d3b6c25082917d5d0220687d6a7b2442f162e34493b13b56d71" + 
                    "6acfe7f0852fee33fe26e0098cef0aa0641] [03758d7df1e4f797e0e68303deb91c06eebcaae2b5bab5151f8e2008990c957b0f]"
                )
            );
        }

        // TODO Bitpay encodes opcodes totally differently here; analyze
        // [Fact]
        // public void OpReturn()
        // {
        //     var asmFormatter = new AsmFormatter();
        //     Assert.Equal
        //     (
        //         "OP_RETURN 1885824512 6845541 16777216 307845466641393537313730653738343543334632643438396242304543363842314530303039353264",
        //         asmFormatter.Format("return [00666770] [657468] [00000001] [307845466641393537313730653738343543334632643438396242304543363842314530303039353264]")
        //     );
        // }

        [Fact]
        public void OutputScript()
        {
            var asmFormatter = new AsmFormatter();
            Assert.Equal
            (
                "OP_HASH160 5c253d296fafb232d99dcec34dd709590b71656a OP_EQUAL",
                asmFormatter.Format("hash160 [5c253d296fafb232d99dcec34dd709590b71656a] equal")
            );
        }

        [Fact]
        public void ScriptPubKey()
        {
            var asmFormatter = new AsmFormatter();
            Assert.Equal
            (
                "OP_DUP OP_HASH160 10a4b9226d4d923927ba85d9009ef469e459ecf1 OP_EQUALVERIFY OP_CHECKSIG",
                asmFormatter.Format("dup hash160 [10a4b9226d4d923927ba85d9009ef469e459ecf1] equalverify checksig")
            );
        }

    }
}