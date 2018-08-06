using Bitprim;
using System;
using System.Threading.Tasks;

namespace bitprim.insight
{
    /// <summary>
    /// Implementation which communicates with a Bitprim blockchain. 
    /// </summary>
    public class BitprimChain : IChain
    {
        private readonly Chain chain_; 

        /// <summary>
        /// Build this wrapper over native Chain.
        /// </summary>
        public BitprimChain(Chain chain)
        {
            chain_ = chain;
        }

        /// <summary>
        /// Delegate to native chain.
        /// </summary>
        public bool IsStale => chain_.IsStale;

        /// <summary>
        /// Given a block height, retrieve only block hash and timestamp, asynchronously.
        /// </summary>
        /// <param name="height"> Block height </param>
        public async Task<ApiCallResult<GetBlockHashTimestampResult>> FetchBlockByHeightHashTimestampAsync(UInt64 height)
        {
            return await chain_.FetchBlockByHeightHashTimestampAsync(height);
        }

        /// <summary>
        /// Fetch the transaction input which spends the indicated output, asynchronously.
        /// </summary>
        /// <param name="outputPoint"> Tx hash and index pair where the output was spent. </param>
        public async Task<ApiCallResult<Point>> FetchSpendAsync(OutputPoint outputPoint)
        {
            return await chain_.FetchSpendAsync(outputPoint);
        }

        /// <summary>
        /// Gets the height of the highest block in the local copy of the blockchain, asynchronously.
        /// </summary>
        public async Task<ApiCallResult<UInt64>> FetchLastHeightAsync()
        {
            return await chain_.FetchLastHeightAsync();
        }

        /// <summary>
        /// Given a block hash, retrieve the full block it identifies, asynchronously.
        /// </summary>
        /// <param name="blockHash"> 32 bytes of the block hash </param>
        public async Task<DisposableApiCallResult<GetBlockDataResult<Block>>> FetchBlockByHashAsync(byte[] blockHash)
        {
            return await chain_.FetchBlockByHashAsync(blockHash);
        }

        /// <summary>
        /// Given a block height, retrieve the full block it identifies, asynchronously.
        /// </summary>
        /// <param name="height"> Block height </param>
        public async Task<DisposableApiCallResult<GetBlockDataResult<Block>>> FetchBlockByHeightAsync(UInt64 height)
        {
            return await chain_.FetchBlockByHeightAsync(height);
        }

        /// <summary>
        /// Given a block height, get the header from the block it identifies, asynchronously.
        /// </summary>
        /// <param name="height"> Block height </param>
        public async Task<DisposableApiCallResult<GetBlockDataResult<Header>>> FetchBlockHeaderByHeightAsync(UInt64 height)
        {
            return await chain_.FetchBlockHeaderByHeightAsync(height);
        }

        /// <summary>
        /// Given a block hash, retrieve block header, tx hashes and serialized block size, asynchronously.
        /// </summary>
        /// <param name="blockHash"> 32 bytes of the block hash </param>
        /// <returns> Tx hashes and serialized block size. Dispose result. </returns>
        public async Task<DisposableApiCallResult<GetBlockHeaderByHashTxSizeResult>> FetchBlockHeaderByHashTxSizesAsync(byte[] blockHash)
        {
            return await chain_.FetchBlockHeaderByHashTxSizesAsync(blockHash);
        }

        /// <summary>
        /// Get a transaction by its hash, asynchronously.
        /// </summary>
        /// <param name="txHash"> 32 bytes of transaction hash </param>
        /// <param name="requireConfirmed"> True if the transaction must belong to a block </param>
        public async Task<DisposableApiCallResult<GetTxDataResult>> FetchTransactionAsync(byte[] txHash, bool requireConfirmed)
        {
            return await chain_.FetchTransactionAsync(txHash, requireConfirmed);
        }

        /// <summary>
        /// Get a list of tx ids for a given payment address (asynchronously). Duplicates are already filtered out.
        /// </summary>
        /// <param name="address"> Bitcoin payment address to search </param>
        /// <param name="limit"> Maximum amount of results to fetch </param>
        /// <param name="fromHeight"> Starting point to search for transactions </param>
        public async Task<DisposableApiCallResult<HashList>> FetchConfirmedTransactionsAsync(PaymentAddress address, UInt64 limit, UInt64 fromHeight)
        {
            return await chain_.FetchConfirmedTransactionsAsync(address, limit, fromHeight);
        }

        /// <summary>
        /// Get a list of output points, values, and spends for a given payment address (asynchronously)
        /// </summary>
        /// <param name="address"> Bitcoin payment address to search </param>
        /// <param name="limit"> Maximum amount of results to fetch </param>
        /// <param name="fromHeight"> Starting point to search for transactions </param>
        public async Task<DisposableApiCallResult<HistoryCompactList>> FetchHistoryAsync(PaymentAddress address, UInt64 limit, UInt64 fromHeight)
        {
            return await chain_.FetchHistoryAsync(address, limit, fromHeight);
        }

        /// <summary>
        /// Add new transaction to blockchain. It will be validated, so it might get rejected.
        /// Confirmation time might depende on miner fees.
        /// </summary>
        /// <param name="transaction"> Transaction to add. </param>
        /// <returns> ErrorCode with operation result. See ErrorCode enumeration. </returns>
        public async Task<ErrorCode> OrganizeTransactionAsync(Transaction transaction)
        {
            return await chain_.OrganizeTransactionAsync(transaction);
        }

        /// <summary>
        /// Get mempool transactions (unconfirmed) from a specific address.
        /// </summary>
        /// <param name="address"> Address to search. </param>
        /// <param name="useTestnetRules"> Tells whether we are in testnet or not. </param>
        public MempoolTransactionList GetMempoolTransactions(PaymentAddress address, bool useTestnetRules)
        {
            return chain_.GetMempoolTransactions(address, useTestnetRules);
        }
    }
}