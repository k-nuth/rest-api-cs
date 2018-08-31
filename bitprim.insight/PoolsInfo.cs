using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Bitprim;
using Newtonsoft.Json;
using static System.String;

namespace bitprim.insight
{
    /// <summary>
    /// Miner pools info; this is used to recognize any block mined by a pool.
    /// </summary>
    public class PoolsInfo : IPoolsInfo
    {
        private class RootObject
        {
            public string poolName { get; set; }
            public string url { get; set; }
            public List<string> searchStrings { get; set; }
        }

        private readonly string poolsFile_;
        private readonly Dictionary<Regex, PoolInfo> data_ = new Dictionary<Regex, PoolInfo>();

        /// <summary>
        /// Only constructor.
        /// </summary>
        /// <param name="poolsFile"> Path to the pools .json file. See pools.json file for format. </param>
        public PoolsInfo(string poolsFile)
        {
            poolsFile_ = poolsFile;
        }

        /// <summary>
        /// Read pools file and load pools information.
        /// </summary>
        public void Load()
        {
            var serializer = new JsonSerializer();
            using (StreamReader file = File.OpenText(poolsFile_))
            {
                var json = (List<RootObject>)serializer.Deserialize(file,typeof (List<RootObject>));

                foreach (RootObject rootObject in json)
                {
                    foreach (string searchString in rootObject.searchStrings)
                    {
                        data_.Add(new Regex(searchString, RegexOptions.Compiled), new PoolInfo
                        {
                            Name = rootObject.poolName,
                            Url = rootObject.url
                        });
                    }
                }
                    
            }
        }

        /// <summary>
        /// Given a transaction, get information about the pool which created it.
        /// </summary>
        /// <param name="tx"> Transaction of interest. </param>
        /// <returns> If tx contains pool info and it matches a pool defined in the pools file, return its info;
        /// otherwise, return the Empty PollInfo instance
        /// </returns>
        public PoolInfo GetPoolInfo(ITransaction tx)
        {
            if (tx == null)
            {
                return PoolInfo.Empty;
            }

            var scriptData = tx.Inputs[0].Script.ToData(false);         
            var script = Encoding.UTF8.GetString(scriptData);
            
            if (IsNullOrWhiteSpace(script))
            {
                return PoolInfo.Empty;
            }

            foreach (KeyValuePair<Regex, PoolInfo> pair in data_)
            {
                if (pair.Key.IsMatch(script))
                {
                    return pair.Value;
                }
            }

            return PoolInfo.Empty;
        }

    }
}