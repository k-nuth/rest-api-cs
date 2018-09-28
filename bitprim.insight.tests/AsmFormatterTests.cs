using Xunit;

namespace bitprim.insight.tests
{
    public class AsmFormatterTests
    {
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