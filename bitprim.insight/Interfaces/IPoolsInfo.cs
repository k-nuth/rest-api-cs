using Bitprim;

namespace bitprim.insight
{
    /// <summary>
    /// Specific pool info.
    /// </summary>
    public class PoolInfo
    {
        /// <summary>
        /// Pool well-known name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Pool url.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Empty pool instance.
        /// </summary>
        public static PoolInfo Empty = new PoolInfo {Name = "",Url = ""};
    }

    /// <summary>
    /// Miner pools info; this is used to recognize any block mined by a pool.
    /// </summary>
    public interface IPoolsInfo
    {
        /// <summary>
        /// Given a transaction, get information about the pool which created it.
        /// </summary>
        /// <param name="tx"> Transaction of interest. </param>
        /// <returns> If tx contains pool info and it matches a pool defined in the pools file, return its info;
        /// otherwise, return the Empty PollInfo instance
        /// </returns>
        PoolInfo GetPoolInfo(ITransaction tx);
    }
}