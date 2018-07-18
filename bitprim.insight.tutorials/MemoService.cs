using bitprim.insight.DTOs;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace bitprim.tutorials
{
    public class MemoService
    {
        private readonly IBitprimInsightAPI bitprimApi_;
        private readonly Regex memoRegex_;
    
        public MemoService(IBitprimInsightAPI bitprimApi)
        {
            bitprimApi_ = bitprimApi;
            memoRegex_ = new Regex("^return " + Regex.Escape("[") + "6d[0-1][1-e]" + Regex.Escape("]"));
        }

        public bool TransactionIsMemo(string txHash)
        {
            TransactionSummary tx = bitprimApi_.GetTransactionByHash(txHash);
            foreach(TransactionOutputSummary output in tx.vout)
            {
                if(memoRegex_.Match(output.scriptPubKey.asm).Success)
                {
                    return true;
                }
            }
            return false;
        }

        public string GetPost(string txHash)
        {
            TransactionSummary tx = bitprimApi_.GetTransactionByHash(txHash);
            foreach(TransactionOutputSummary output in tx.vout)
            {
                string outputScript = output.scriptPubKey.asm;
                if(!memoRegex_.Match(outputScript).Success)
                {
                    continue;
                }
                int iStart = outputScript.LastIndexOf("[");
                string toDecode = outputScript.Substring(iStart + 1, outputScript.Length - (iStart + 2));
                byte[] bytesToDecode = HexStringToBytes(toDecode);
                return Encoding.GetEncoding("UTF-8").GetString(bytesToDecode);
            }
            return "";
        }

        public List<string> GetLatestPosts(int nPosts)
        {
            UInt64 blockchainHeight = bitprimApi_.GetCurrentBlockchainHeight();
            int postsFound = 0;
            var posts = new List<string>();
            while(postsFound < nPosts && blockchainHeight > 1)
            {
                Console.WriteLine("Searching block " + blockchainHeight + "...");
                string blockHash = bitprimApi_.GetBlockHash(blockchainHeight);
                GetTransactionsResponse txs = bitprimApi_.GetBlockTransactions(blockHash, 0);
                for(int iPage=0; iPage<(int)txs.pagesTotal; ++iPage)
                {
                    Console.WriteLine("\tSearching tx page " + iPage + "...");
                    txs = bitprimApi_.GetBlockTransactions(blockHash, iPage);
                    foreach(TransactionSummary tx in txs.txs)
                    {
                        if(TransactionIsMemo(tx.txid))
                        {
                            posts.Add(GetPost(tx.txid));
                            ++postsFound;
                            Console.WriteLine("\t\tFound post " + postsFound + " of " + nPosts + " in tx " + tx.txid + "!");
                            if(postsFound == nPosts)
                            {
                                break;
                            }
                        }
                    }
                    if(postsFound == nPosts)
                    {
                        break;
                    }
                }
                blockchainHeight--;
            }
            return posts;
        }

        private static byte[] HexStringToBytes(string hexString)
        {
            if(hexString == null)
            {
                throw new ArgumentNullException("hexString");
            }
            if(hexString.Length % 2 != 0)
            {
                throw new ArgumentException("hexString must have an even length", "hexString");
            }
            var bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                string currentHex = hexString.Substring(i * 2, 2);
                bytes[i] = Convert.ToByte(currentHex, 16);
            }
            return bytes;
        }
    }
}