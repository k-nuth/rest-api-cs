using bitprim.insight.DTOs;
using Moq;
using Xunit;

namespace bitprim.tutorials.tests
{
    public class MemoServiceTest
    {
        private readonly Mock<IBitprimInsightAPI> bitprimApiMock_;
        private readonly MemoService memoService_;
        private readonly TransactionSummary sampleMemoTx_, sampleMemoTx2_;

        public MemoServiceTest()
        {
            bitprimApiMock_ = new Mock<IBitprimInsightAPI>();
            memoService_ = new MemoService(bitprimApiMock_.Object);
            sampleMemoTx_ = new TransactionSummary
            {
                blocktime = 1531833466,
                txid = "4a69a310ce5cade43a12308101822dd9e2988f4be17c53c20785d7060688157d",
                vin = new TransactionInputSummary[]
                {
                    new TransactionInputSummary
                    {
                        value = 0.00385287M,
                        scriptSig = new InputScriptSummary
                        {
                            asm = "[3044022012b742ca2a0c4a49070eacbb018c6ed11c28eaa6a9e52282f78cecd3612424ca022023bbc42638af9c29f6bdd8eae293781e8c7b14c387914d81cdecc5525ede968041] [02e2053838670dc0383d945319d3d1a285476696a4388ed0d907ae043b64af9813]",
                            hex = "473044022012b742ca2a0c4a49070eacbb018c6ed11c28eaa6a9e52282f78cecd3612424ca022023bbc42638af9c29f6bdd8eae293781e8c7b14c387914d81cdecc5525ede9680412102e2053838670dc0383d945319d3d1a285476696a4388ed0d907ae043b64af9813"
                        },
                        addr = "1KZonibiByMHsXkJi5Vfm9jAySbv6yYVEi",
                        txid = "4a69a310ce5cade43a12308101822dd9e2988f4be17c53c20785d7060688157d",
                        n = 0,
                        vout = 0,
                        sequence = 4294967295,
                        valueSat = 385287
                    }
                },
                locktime = 0,
                version = 1,
                vout = new TransactionOutputSummary[]
                {
                    new TransactionOutputSummary
                    {
                        value = 0.00384891M,
                        scriptPubKey = new OutputScriptSummary
                        {
                            asm = "dup hash160 [cba6a1b91eda58e77ff5f265037326699625dfa0] equalverify checksig",
                            hex = "76a914cba6a1b91eda58e77ff5f265037326699625dfa088ac",
                            type = "pubkeyhash",
                            addresses = new string[]{"1KZonibiByMHsXkJi5Vfm9jAySbv6yYVEi"}
                        },
                        n = 0
                    },
                    new TransactionOutputSummary
                    {
                        value = 0,
                        scriptPubKey = new OutputScriptSummary
                        {
                            asm = "return [6d0c] [6d656d6f] [596573746572646179203135303633322d3134383539303d32303432206d656d6f207472616e73616374696f6e732e204e756d626572206f66207472616e73616374696f6e73f09f93883230252e2043686172747320617661696c61626c652061742068747470733a2f2f6d656d6f2e636173682f636861727473202e20636f6e7369646572696e6720776865746865722073686f756c6420492073746f702074686973207265636f7264696e673f3f3f]",
                            hex = "6a026d0c046d656d6f4cb1596573746572646179203135303633322d3134383539303d32303432206d656d6f207472616e73616374696f6e732e204e756d626572206f66207472616e73616374696f6e73f09f93883230252e2043686172747320617661696c61626c652061742068747470733a2f2f6d656d6f2e636173682f636861727473202e20636f6e7369646572696e6720776865746865722073686f756c6420492073746f702074686973207265636f7264696e673f3f3f",
                            type = "non_standard"
                        },
                        n = 1
                    }
                }
            };
            sampleMemoTx2_ = new TransactionSummary
            {
                blocktime = 1531921144,
                txid = "2a2c1b00ec5ed7beb31244d054f5401dbcae1faf22d4ce5c6bcbf9b3bdfb5e1e",
                vin = new TransactionInputSummary[]
                {
                    new TransactionInputSummary
                    {
                        value = 0.00290733M,
                        scriptSig = new InputScriptSummary
                        {
                            asm = "[304402207e523fd8f96721398d70911b85bedd3014164723a45744d0a7df0af7c6d2e4f502201ae237ae5dd78260eb7bf9c439f36300eb591e8a697654f87281505af4b3fa6a41] [024f61f4044b4a4d7fd4ad118b73d992543cb15aaa163e7ead030731f4c97c714e]",
                            hex = "47304402207e523fd8f96721398d70911b85bedd3014164723a45744d0a7df0af7c6d2e4f502201ae237ae5dd78260eb7bf9c439f36300eb591e8a697654f87281505af4b3fa6a4121024f61f4044b4a4d7fd4ad118b73d992543cb15aaa163e7ead030731f4c97c714e"
                        },
                        addr = "19EaiAHNSuC1H5zyXfCgQP6QCdWRcaYGHK",
                        txid = "5f03b24201d6172537a48ce059da6fbd407d6b3050a7a018e3567c84e3d3d047",
                        n = 0,
                        vout = 0,
                        sequence = 4294967295,
                        valueSat = 290733,
                        }
                },
                locktime = 0,
                version = 1,
                vout = new TransactionOutputSummary[]
                {
                    new TransactionOutputSummary
                    {
                        value = 0.00290449M,
                        scriptPubKey = new OutputScriptSummary
                        {
                            asm = "dup hash160 [5a528d9d036d74279f7f969682b95e2c537d3714] equalverify checksig",
                            hex = "76a9145a528d9d036d74279f7f969682b95e2c537d371488ac",
                            type = "pubkeyhash",
                            addresses = new string[]{ "19EaiAHNSuC1H5zyXfCgQP6QCdWRcaYGHK" }
                        },
                        n = 0,
                    },
                    new TransactionOutputSummary
                    {
                        value = 0M,
                        scriptPubKey = new OutputScriptSummary
                        {
                            asm = "return [6d03] [6c4efe072f542bf4bc58d8e0f2924be90b5d588da4fea58b9d75180e2a04c78d] [4243486a6565706e657920616e642073747265737374657374626974636f696e2e63617368]",
                            hex = "6a026d03206c4efe072f542bf4bc58d8e0f2924be90b5d588da4fea58b9d75180e2a04c78d254243486a6565706e657920616e642073747265737374657374626974636f696e2e63617368",
                            type = "non_standard"
                        },
                        n = 1,
                    }
                }
            };
        }

        [Fact]
        public void PostShouldBeDecoded()
        {
            bitprimApiMock_.Setup(x => x.GetTransactionByHash(sampleMemoTx_.txid))
                .Returns(sampleMemoTx_);
            Assert.Equal("Yesterday 150632-148590=2042 memo transactions. Number of transactions📈20%. Charts available at https://memo.cash/charts . considering whether should I stop this recording???",
                         memoService_.GetPost(sampleMemoTx_.txid));
        }

        [Fact]
        public void Post2ShouldBeDecoded()
        {
            bitprimApiMock_.Setup(x => x.GetTransactionByHash(sampleMemoTx2_.txid))
                .Returns(sampleMemoTx2_);
            Assert.Equal("BCHjeepney and stresstestbitcoin.cash",
                         memoService_.GetPost(sampleMemoTx2_.txid));
        }

        [Fact]
        public void TransactionShouldBeMemo()
        {
            bitprimApiMock_.Setup(x => x.GetTransactionByHash(sampleMemoTx_.txid))
                .Returns(sampleMemoTx_);
            Assert.True(memoService_.TransactionIsMemo(sampleMemoTx_.txid));
        }

        [Fact]
        public void Transaction2ShouldBeMemo()
        {
            bitprimApiMock_.Setup(x => x.GetTransactionByHash(sampleMemoTx2_.txid))
                .Returns(sampleMemoTx2_);
            Assert.True(memoService_.TransactionIsMemo(sampleMemoTx2_.txid));
        }

        [Fact]
        public void TransactionShouldNotBeMemo()
        {
            const string txHash = "46067c17b044c47ad3d3405ca0b0d126e8e5d4d4fadbeac8f9cd6726e4e5f9ef";
            bitprimApiMock_.Setup(x => x.GetTransactionByHash(txHash))
                .Returns(new TransactionSummary
                {
                    txid = "46067c17b044c47ad3d3405ca0b0d126e8e5d4d4fadbeac8f9cd6726e4e5f9ef",
                    version = 1,
                    locktime = 0,
                    vin = new TransactionInputSummary[]
                    {
                        new TransactionInputSummary
                        {
                            coinbase = "03e83a08152f5669614254432f636f696e6765656b2e636f6d2f2cfabe6d6dad844921def544424cb2ed351c5654924c882f630e9a4f4cac8c007b4b86c0fe040000000000000010ba9c1e0c951780acdefefd6a91050000",
                            sequence = 4294967295,
                            n = 0
                        }
                    },
                    vout = new TransactionOutputSummary[]
                    {
                        new TransactionOutputSummary
                        {
                            value = 12.5085271M,
                            n = 0,
                            scriptPubKey = new OutputScriptSummary
                            {
                                asm = "dup hash160 [f1c075a01882ae0972f95d3a4177c86c852b7d91] equalverify checksig",
                                hex = "76a914f1c075a01882ae0972f95d3a4177c86c852b7d9188ac",
                                addresses = new string[]{ "1P3GQYtcWgZHrrJhUa4ctoQ3QoCU2F65nz" },
                                type = "pubkeyhash"
                            }
                        }
                    },
                    blocktime = 1531832488
                });
            Assert.False(memoService_.TransactionIsMemo(txHash));
        }
    }
}
