using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.Http;
using System.Threading.Tasks;
using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Polly;
using System.Linq;

namespace bitprim.insight.Controllers
{
    [Route("api/[controller]")]
    public class ChainController : Controller
    {
        private Chain chain_;
        private Executor nodeExecutor_;
        private static readonly HttpClient httpClient_ = new HttpClient();
        private readonly NodeConfig config_;
        private const int MAX_BLOCKCHAIN_HEIGHT_AGE_IN_SECONDS = 60;
        private const int MAX_DELAY = 2;
        private const int MAX_RETRIES = 3;
        private const int SEED_DELAY = 100;
        private const string BLOCKCHAIN_HEIGHT_CACHE_KEY = "blockchain_height";
        private const string BLOCKCHAIR_BCC_URL = "https://api.blockchair.com/bitcoin-cash";
        private const string BLOCKCHAIR_BTC_URL = "https://api.blockchair.com/bitcoin";
        private const string BLOCKTRAIL_TBCC_URL = "https://www.blocktrail.com/tBCC/json/blockchain/homeStats";
        private const string GET_BEST_BLOCK_HASH = "getBestBlockHash";
        private const string GET_LAST_BLOCK_HASH = "getLastBlockHash";
        private const string GET_DIFFICULTY = "getDifficulty";
        private const string SOCHAIN_LTC_URL = "https://chain.so/api/v2/get_info/LTC";
        private const string SOCHAIN_TBTC_URL = "https://chain.so/api/v2/get_info/BTCTEST";
        private const string SOCHAIN_TLTC_URL = "https://chain.so/api/v2/get_info/LTCTEST";
        private ILogger<ChainController> logger_;
        private IMemoryCache memoryCache_;
        private readonly Policy breakerPolicy_ = Policy.Handle<Exception>().CircuitBreakerAsync(2, TimeSpan.FromMinutes(1));
        private readonly Policy retryPolicy_ = Policy.Handle<Exception>()
            .WaitAndRetryAsync(RetryUtils.DecorrelatedJitter(MAX_RETRIES, TimeSpan.FromMilliseconds(SEED_DELAY), TimeSpan.FromSeconds(MAX_DELAY)));
        private readonly Policy execPolicy_;

        public ChainController(IOptions<NodeConfig> config, Executor executor, ILogger<ChainController> logger, IMemoryCache memoryCache)
        {
            config_ = config.Value;
            nodeExecutor_ = executor;
            chain_ = executor.Chain;
            memoryCache_ = memoryCache;
            execPolicy_ = Policy.WrapAsync(retryPolicy_, breakerPolicy_);
            logger_ = logger;
        }

        [ResponseCache(CacheProfileName = Constants.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("/api/sync")]
        public async Task<ActionResult> GetSyncStatus()
        {
            var getLastHeightResult = await chain_.FetchLastHeightAsync();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "GetLastHeight() failed");
            
            var currentHeight = getLastHeightResult.Result;
            UInt64? blockChainHeight = await GetCurrentBlockChainHeight();
            dynamic syncStatus = new ExpandoObject();
            if(blockChainHeight.HasValue)
            {
                var synced = currentHeight >= blockChainHeight;
                syncStatus.status = synced? "finished" : "synchronizing";
                syncStatus.blockChainHeight = blockChainHeight;
                syncStatus.syncPercentage = Math.Min((double)currentHeight / (double)blockChainHeight * 100.0, 100).ToString("N2");
                syncStatus.error = null;
            }
            else
            {
                syncStatus.status = "unknown";
                syncStatus.blockChainHeight = "unknown";
                syncStatus.syncPercentage = "unknown";
                syncStatus.error = "Could not determine max blockchain height; check log";
            }
            syncStatus.height = currentHeight;
            syncStatus.type = config_.NodeType;
            return Json(syncStatus);   
        }

        [ResponseCache(CacheProfileName = Constants.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("/api/status")]
        public async Task<ActionResult> GetStatus([Bind(Prefix="q")] string method)
        {
            switch (method)
            {
                case GET_DIFFICULTY:
                    return await GetDifficulty();
                case GET_BEST_BLOCK_HASH:
                    return await GetBestBlockHash();
                case GET_LAST_BLOCK_HASH:
                    return await GetLastBlockHash();
            }

            return await GetInfo();
        }

        [HttpGet("/api/utils/estimatefee")]
        public ActionResult GetEstimateFee([FromQuery] int nbBlocks = 2)
        {
            var estimateFee = new ExpandoObject() as IDictionary<string, Object>;
            //TODO Check which algorithm to use (see bitcoin-abc's median, at src/policy/fees.cpp for an example)
            estimateFee.Add(nbBlocks.ToString(), 1.0);
            return Json(estimateFee);   
        }

        [HttpGet("/api/currency")]
        public ActionResult GetCurrency()
        {
            //TODO Implement in node-cint? Or here? Ask
            return Json(new{
                status = 200,
                data = new
                {
                    bistamp = 8025.3f
                }
            });
        }

        private async Task<ActionResult> GetDifficulty()
        {
            using(var getLastBlockResult = await GetLastBlock())
            {
                return Json
                (
                    new
                    {
                        difficulty = Utils.BitsToDifficulty(getLastBlockResult.Result.BlockData.Header.Bits)
                    }
                );
            }
        }

        private async Task<ActionResult> GetBestBlockHash()
        {
            using(var getLastBlockResult = await GetLastBlock())
            {
                return Json
                (
                    new
                    {
                        bestblockhash = Binary.ByteArrayToHexString(getLastBlockResult.Result.BlockData.Hash)
                    }
                );
            }
        }

        private async Task<ActionResult> GetLastBlockHash()
        {
            using(var getLastBlockResult = await GetLastBlock())
            {
                var hashHexString = Binary.ByteArrayToHexString(getLastBlockResult.Result.BlockData.Hash); 
                return Json
                (
                    new
                    {
                        syncTipHash = hashHexString,
                        lastblockhash = hashHexString
                    }
                );
            }
        }

        private async Task<ActionResult> GetInfo()
        {
            using(var getLastBlockResult = await GetLastBlock())
            {
                return Json
                (
                    new
                    {
                        info = new 
                        {
                            //TODO Some of these values should be retrieved from node-cint
                            version = config_.Version,
                            protocolversion = config_.ProtocolVersion,
                            blocks = getLastBlockResult.Result.BlockHeight,
                            timeoffset = config_.TimeOffset,
                            connections = config_.Connections,
                            proxy = config_.Proxy,
                            difficulty = Utils.BitsToDifficulty(getLastBlockResult.Result.BlockData.Header.Bits),
                            testnet = nodeExecutor_.UseTestnetRules,
                            relayfee = config_.RelayFee,
                            errors = "",
                            network = nodeExecutor_.NetworkType.ToString()
                        }
                    }
                );
            }
        }

        private async Task<DisposableApiCallResult<GetBlockDataResult<Block>>> GetLastBlock()
        {
            var getLastHeightResult = await chain_.FetchLastHeightAsync();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "FetchLastHeightAsync() failed");
            
            var currentHeight = getLastHeightResult.Result;
            var getBlockResult = await chain_.FetchBlockByHeightAsync(currentHeight);
            Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockByHeightAsync(" + currentHeight + ") failed");

            return getBlockResult;
        }

        //TODO Avoid consulting external sources; get this information from bitprim network
        private async Task<UInt64?> GetCurrentBlockChainHeight()
        {
            try
            {
                UInt64 blockChainHeight = 0;
                if(memoryCache_.TryGetValue(BLOCKCHAIN_HEIGHT_CACHE_KEY, out blockChainHeight))
                {
                    return blockChainHeight;
                };
                switch(NodeSettings.CurrencyType)
                {
                    case CurrencyType.BitcoinCash:
                        blockChainHeight = await execPolicy_.ExecuteAsync<UInt64>( () => GetBCCBlockchainHeight() );
                        break;
                    case CurrencyType.Bitcoin:
                        blockChainHeight = await execPolicy_.ExecuteAsync<UInt64>( () => GetBTCBlockchainHeight() );
                        break;
                    case CurrencyType.Litecoin:
                        blockChainHeight = await execPolicy_.ExecuteAsync<UInt64>( () => GetLTCBlockchainHeight() );
                        break;
                    default:
                        throw new InvalidOperationException("Only BCH, BTC and LTC support this operation");
                }
                memoryCache_.Set
                (
                    BLOCKCHAIN_HEIGHT_CACHE_KEY, blockChainHeight, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(MAX_BLOCKCHAIN_HEIGHT_AGE_IN_SECONDS),
                        Size = Constants.BLOCKCHAIN_HEIGHT_CACHE_ENTRY_SIZE
                    }
                );
                return blockChainHeight;
            }
            catch(Exception ex)
            {
                logger_.LogWarning(ex, "Failed to retrieve blockchain height from external service");
                return null;
            }
        }

        private async Task<UInt64> GetBCCBlockchainHeight()
        {
            if(nodeExecutor_.UseTestnetRules)
            {
                var syncDataString = await httpClient_.GetStringAsync(BLOCKTRAIL_TBCC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return syncData.last_blocks[0].height;
            }
            else
            {
                var syncDataString = await httpClient_.GetStringAsync(BLOCKCHAIR_BCC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return ((IEnumerable<dynamic>)syncData.data).Where( r => r.e == "blocks" ).First().c;
            }
        }

        private async Task<UInt64> GetBTCBlockchainHeight()
        {
            if(nodeExecutor_.UseTestnetRules)
            {
                var syncDataString = await httpClient_.GetStringAsync(SOCHAIN_TBTC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return syncData.data.blocks;
            }
            else
            {
                var syncDataString = await httpClient_.GetStringAsync(BLOCKCHAIR_BTC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return ((IEnumerable<dynamic>)syncData.data).Where( r => r.e == "blocks" ).First().c;
            }
        }

        private async Task<UInt64> GetLTCBlockchainHeight()
        {
            if(nodeExecutor_.UseTestnetRules)
            {
                var syncDataString = await httpClient_.GetStringAsync(SOCHAIN_TLTC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return syncData.data.blocks;
            }
            else
            {
                var syncDataString = await httpClient_.GetStringAsync(SOCHAIN_LTC_URL);
                dynamic syncData = JsonConvert.DeserializeObject<dynamic>(syncDataString);
                return syncData.data.blocks;
            }
        }

    }
}