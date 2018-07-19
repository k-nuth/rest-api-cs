using System;

namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetTransactionsForMultipleAddressesResponse data structure.
    /// </summary>
    public class GetTransactionsForMultipleAddressesResponse
    {
        /// <summary>
        /// Selected transactions.
        /// </summary>
        public TransactionSummary[] items { get; set; }

        /// <summary>
        /// Results selection starting point.
        /// </summary>
        public int from { get; set; }

        /// <summary>
        /// Results selection ending point.
        /// </summary>
        public int to { get; set; }

        /// <summary>
        /// Unpaginated results count.
        /// </summary>
        public int totalItems { get; set; }
    }
}