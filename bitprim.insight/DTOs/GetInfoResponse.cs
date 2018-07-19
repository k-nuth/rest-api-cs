using System;

namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetInfoResponse data structure.
    /// </summary>
    public class GetInfoResponse
    {
        /// <summary>
        /// Detailed status information.
        /// </summary>
        public GetInfoData info { get; set; }
    }

    /// <summary>
    /// Specific status information.
    /// </summary>
    public class GetInfoData
    {
        /// <summary>
        ///  True if and only if node is connected to testnet.
        /// </summary>
        public bool testnet { get; set; }

        /// <summary>
        /// Last block difficulty.
        /// </summary>
        public double difficulty { get; set; }

        /// <summary>
        /// Current amount of P2P connections established by the Bitprim node.
        /// </summary>
        public int connections { get; set; }

        /// <summary>
        /// Currency acronym; BTC for Bitcoin, TBTC for Bitcoin Testnet, BCH for Bitcoin Cash, and so on.
        /// </summary>
        public string coin { get; set; }

        /// <summary>
        /// Latest node error messages.
        /// </summary>
        public string errors { get; set; }

        /// <summary>
        /// "livenet" for mainnet, given network name for other cases.
        /// </summary>
        public string network { get; set; }

        /// <summary>
        /// Blockchain protocol version.
        /// </summary>
        public string protocolversion { get; set; }

        /// <summary>
        /// Node proxy URL.
        /// </summary>
        public string proxy { get; set; }

        /// <summary>
        /// Current node relay fee.
        /// </summary>
        public string relayfee { get; set; }

        /// <summary>
        /// Offset to apply to UTC times.
        /// </summary>
        public string timeoffset { get; set; }

        /// <summary>
        /// Underlying Bitprim node version.
        /// </summary>
        public string version { get; set; }

        /// <summary>
        /// Current blockchain height
        /// </summary>
        public UInt64 blocks { get; set; } 

    }
}