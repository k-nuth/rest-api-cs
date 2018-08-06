using Bitprim;
using System;
using System.Threading.Tasks;

namespace bitprim.insight
{
    /// <summary>
    /// Blockchain abstract interface.
    /// </summary>
    public interface IChain
    {
        /// <summary>
        /// Returns true if and only if the blockchain is synchronized (i.e. at current network top height)
        /// </summary>
        bool IsStale { get; }

        /// <summary>
        /// Get mempool transactions (unconfirmed) from a specific address.
        /// </summary>
        /// <param name="address"> Address to search. </param>
        /// <param name="useTestnetRules"> Tells whether we are in testnet or not. </param>
        MempoolTransactionList GetMempoolTransactions(PaymentAddress address, bool useTestnetRules);

        /// <summary>
        /// Given a block height, retrieve only block hash and timestamp, asynchronously.
        /// </summary>
        /// <param name="height"> Block height </param>
        Task<ApiCallResult<GetBlockHashTimestampResult>> FetchBlockByHeightHashTimestampAsync(UInt64 height);

        /// <summary>
        /// Fetch the transaction input which spends the indicated output, asynchronously.
        /// </summary>
        /// <param name="outputPoint"> Tx hash and index pair where the output was spent. </param>
        Task<ApiCallResult<Point>> FetchSpendAsync(OutputPoint outputPoint);

        /// <summary>
        /// Gets the height of the highest block in the local copy of the blockchain, asynchronously.
        /// </summary>
        Task<ApiCallResult<UInt64>> FetchLastHeightAsync();

        /// <summary>
        /// Given a block hash, retrieve the full block it identifies, asynchronously.
        /// </summary>
        /// <param name="blockHash"> 32 bytes of the block hash </param>
        Task<DisposableApiCallResult<GetBlockDataResult<Block>>> FetchBlockByHashAsync(byte[] blockHash);

        /// <summary>
        /// Given a block height, retrieve the full block it identifies, asynchronously.
        /// </summary>
        /// <param name="height"> Block height </param>
        Task<DisposableApiCallResult<GetBlockDataResult<Block>>> FetchBlockByHeightAsync(UInt64 height);

        /// <summary>
        /// Given a block height, get the header from the block it identifies, asynchronously.
        /// </summary>
        /// <param name="height"> Block height </param>
        Task<DisposableApiCallResult<GetBlockDataResult<Header>>> FetchBlockHeaderByHeightAsync(UInt64 height);

        /// <summary>
        /// Given a block hash, retrieve block header, tx hashes and serialized block size, asynchronously.
        /// </summary>
        /// <param name="blockHash"> 32 bytes of the block hash </param>
        /// <returns> Tx hashes and serialized block size. Dispose result. </returns>
        Task<DisposableApiCallResult<GetBlockHeaderByHashTxSizeResult>> FetchBlockHeaderByHashTxSizesAsync(byte[] blockHash);

        /// <summary>
        /// Get a transaction by its hash, asynchronously.
        /// </summary>
        /// <param name="txHash"> 32 bytes of transaction hash </param>
        /// <param name="requireConfirmed"> True if the transaction must belong to a block </param>
        Task<DisposableApiCallResult<GetTxDataResult>> FetchTransactionAsync(byte[] txHash, bool requireConfirmed);

        /// <summary>
        /// Get a list of tx ids for a given payment address (asynchronously). Duplicates are already filtered out.
        /// </summary>
        /// <param name="address"> Bitcoin payment address to search </param>
        /// <param name="limit"> Maximum amount of results to fetch </param>
        /// <param name="fromHeight"> Starting point to search for transactions </param>
        Task<DisposableApiCallResult<HashList>> FetchConfirmedTransactionsAsync(PaymentAddress address, UInt64 limit, UInt64 fromHeight);

        /// <summary>
        /// Get a list of output points, values, and spends for a given payment address (asynchronously)
        /// </summary>
        /// <param name="address"> Bitcoin payment address to search </param>
        /// <param name="limit"> Maximum amount of results to fetch </param>
        /// <param name="fromHeight"> Starting point to search for transactions </param>
        Task<DisposableApiCallResult<HistoryCompactList>> FetchHistoryAsync(PaymentAddress address, UInt64 limit, UInt64 fromHeight);

        /// <summary>
        /// Add new transaction to blockchain. It will be validated, so it might get rejected.
        /// Confirmation time might depende on miner fees.
        /// </summary>
        /// <param name="transaction"> Transaction to add. </param>
        /// <returns> ErrorCode with operation result. See ErrorCode enumeration. </returns>
        Task<ErrorCode> OrganizeTransactionAsync(Transaction transaction);
       
    }
}