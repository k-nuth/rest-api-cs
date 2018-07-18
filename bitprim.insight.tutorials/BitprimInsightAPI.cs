using bitprim.insight.DTOs;
using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace bitprim.tutorials
{
    public class BitprimInsightAPI : IBitprimInsightAPI
    {
        private const string BASE_URL = "https://blockdozer.com/api";
        private readonly HttpClient httpClient_;

        public BitprimInsightAPI()
        {
            httpClient_ = new HttpClient();
            httpClient_.DefaultRequestHeaders.Accept.Add
            (
                new MediaTypeWithQualityHeaderValue("application/json")
            );
        }

        public GetTransactionsResponse GetBlockTransactions(string blockHash, int pageNum)
        {
            return CallApiMethod<GetTransactionsResponse>(BASE_URL + "/txs?block=" + blockHash + "&pageNum=" + pageNum);
        }

        public string GetBlockHash(UInt64 blockHeight)
        {
            return CallApiMethod<GetBlockByHeightResponse>(BASE_URL + "/block-index/" + blockHeight).blockHash;
        }

        public TransactionSummary GetTransactionByHash(string hash)
        {
            return CallApiMethod<TransactionSummary>(BASE_URL + "/tx/" + hash);
        }

        public UInt64 GetCurrentBlockchainHeight()
        {
            return UInt64.Parse(CallApiMethod<GetSyncStatusResponse>(BASE_URL + "/sync").blockChainHeight);
        }

        private T CallApiMethod<T>(string url)
        {
            HttpResponseMessage response = httpClient_.GetAsync(url).Result;
            if (response.IsSuccessStatusCode)
            {
                return response.Content.ReadAsAsync<T>().Result;
            }
            else
            {
                throw new ApplicationException("API call failed, error: " + response.StatusCode + " " + response.ReasonPhrase);
            }
        }
    }
}