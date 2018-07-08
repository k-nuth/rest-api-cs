namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetPeerStatusResponse data structure.
    /// </summary>
    public class GetPeerStatusResponse
    {
        /// <summary>
        /// True iif the node is connected to the blockchain's P2P network.
        /// </summary>
        public bool connected { get; set; }

        /// <summary>
        /// Port used by the node to communicate with his peers. Null equals the well-known default.
        /// </summary>
        public int? port { get; set; }

        /// <summary>
        /// Node's IP address inside the network.
        /// </summary>
        public string host { get; set; }
    }
}