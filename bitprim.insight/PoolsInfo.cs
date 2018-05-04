using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Bitprim;
using Newtonsoft.Json;
using static System.String;

namespace bitprim.insight
{
    public class PoolsInfo
    {
        public class PoolInfo
        {
            public string Name { get; set; }
            public string Url { get; set; }

            public static PoolInfo Empty = new PoolInfo {Name = "",Url = ""};
        }

        private class RootObject
        {
            public string poolName { get; set; }
            public string url { get; set; }
            public List<string> searchStrings { get; set; }
        }

        private readonly string poolsFile_;

        private readonly Lazy<Dictionary<Regex, PoolInfo>> data_;
        
       
        public PoolsInfo(string poolsFile)
        {
            poolsFile_ = poolsFile;
            data_= new Lazy<Dictionary<Regex, PoolInfo>>(Load, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private Dictionary<Regex,PoolInfo> Load()
        {
            var ret = new Dictionary<Regex, PoolInfo>();

            var serializer = new JsonSerializer();
            using (StreamReader file = File.OpenText(poolsFile_))
            {
                var json = (List<RootObject>)serializer.Deserialize(file,typeof (List<RootObject>));

                foreach (RootObject rootObject in json)
                {
                    foreach (string searchString in rootObject.searchStrings)
                    {
                        ret.Add(new Regex(searchString, RegexOptions.Compiled), new PoolInfo
                        {
                            Name = rootObject.poolName,
                            Url = rootObject.url
                        });
                    }
                }
                    
            }

            return ret;
        }

        public PoolInfo GetPoolInfo(Transaction tx)
        {
            if (tx == null)
            {
                return PoolInfo.Empty;
            }

            string script = tx.Inputs[0].Script.ToString(0);

            if (IsNullOrWhiteSpace(script))
            {
                return PoolInfo.Empty;
            }

            foreach (KeyValuePair<Regex, PoolInfo> pair in data_.Value)
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