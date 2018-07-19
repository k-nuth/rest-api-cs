using bitprim.insight.DTOs;
using System;

namespace bitprim.tutorials
{
    public interface IBitprimInsightAPI
    {
        GetTransactionsResponse GetBlockTransactions(string blockHash, int pageNum);

        string GetBlockHash(UInt64 blockHeight);

        TransactionSummary GetTransactionByHash(string hash);

        UInt64 GetCurrentBlockchainHeight();
    }
}