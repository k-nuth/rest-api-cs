using System;

namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetTransactionsResponse data structure.
    /// </summary>
    public class GetTransactionsResponse
    {
        /// <summary>
        /// Selected results page.
        /// </summary>
        public TransactionSummary[] txs { get; set; }

        /// <summary>
        /// pagesTotal = txCount / pageSize. Page size is configurable via appsettings.json
        /// and command line. See "TransactionsByAddressPageSize" key.
        /// </summary>
        public UInt64 pagesTotal { get; set; }
    }
}