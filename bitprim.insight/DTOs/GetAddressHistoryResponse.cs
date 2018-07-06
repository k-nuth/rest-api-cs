using System;

namespace bitprim.insight.DTOs
{
    public class GetAddressHistoryResponse
    {
        public decimal balance { get; set; }
        public decimal totalReceived { get; set; }
        public decimal totalSent { get; set; }
        public decimal unconfirmedBalance { get; set; }
        public int txApperances { get; set; }
        public Int64 unconfirmedBalanceSat { get; set; }
        public string addrStr { get; set; }
        public string[] transactions { get; set; }
        public UInt64 balanceSat { get; set; }
        public UInt64 totalReceivedSat { get; set; }
        public UInt64 totalSentSat { get; set; }
        public UInt64 unconfirmedTxAppearances { get; set; }
    }
}