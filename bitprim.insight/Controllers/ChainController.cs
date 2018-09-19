using bitprim.insight.DTOs;
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
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Globalization;

namespace bitprim.insight.Controllers
{
    /// <summary>
    /// Blockchain related operations.
    /// </summary>
    [Route("[controller]")]
    public class ChainController : Controller
    {
        private readonly Chain chain_;
        private readonly Executor nodeExecutor_;
        private static readonly HttpClient httpClient_ = new HttpClient();
        private readonly NodeConfig config_;
        private readonly ILogger<ChainController> logger_;
        private readonly IMemoryCache memoryCache_;
        private readonly Policy breakerPolicy_ = Policy.Handle<Exception>().CircuitBreakerAsync(2, TimeSpan.FromMinutes(1));
        private readonly Policy retryPolicy_ = Policy.Handle<Exception>()
            .WaitAndRetryAsync(RetryUtils.DecorrelatedJitter
                (Constants.MAX_RETRIES, TimeSpan.FromMilliseconds(Constants.SEED_DELAY), TimeSpan.FromSeconds(Constants.MAX_DELAY)));
        private readonly Policy execPolicy_;

        /// <summary>
        /// Build this controller.
        /// </summary>
        /// <param name="config"> Higher level API configuration. </param>
        /// <param name="executor"> Node executor from bitprim-cs library. </param>
        /// <param name="logger"> Abstract logger. </param>
        /// <param name="memoryCache"> Abstract memory cache. </param>
        public ChainController(IOptions<NodeConfig> config, Executor executor, ILogger<ChainController> logger, IMemoryCache memoryCache)
        {
            config_ = config.Value;
            nodeExecutor_ = executor;
            chain_ = executor.Chain;
            memoryCache_ = memoryCache;
            execPolicy_ = Policy.WrapAsync(retryPolicy_, breakerPolicy_);
            logger_ = logger;
        }

        /// <summary>
        /// Get an estimate value for current block fee.
        /// </summary>
        /// <param name="nbBlocks"> Comma-separed list of block numbers to use for each estimation; a higher number
        /// implies higher precision, but will take longer to calculate.
        /// </param>
        /// <returns> Current estimations for block fee, for each block count requested. </returns>
        [HttpGet("utils/estimatefee")]
        [SwaggerOperation("GetEstimateFee")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(IDictionary<string, string>))]
        public ActionResult GetEstimateFee([FromQuery] string nbBlocks = "2")
        {
            if( !ModelState.IsValid || string.IsNullOrWhiteSpace(nbBlocks) )
            {
                return BadRequest("nbBlocks must be a string of comma-separated integers");
            }
            var nbBlocksStr = nbBlocks.Split(",");
            foreach(string s in nbBlocksStr)
            {
                int a;
                if( !int.TryParse(s, out a) )
                {
                    return BadRequest(s + " is not an integer");
                }
            }
            var estimateFee = new ExpandoObject() as IDictionary<string, Object>;
            //TODO Check which algorithm to use (see bitcoin-abc's median, at src/policy/fees.cpp for an example)
            foreach(string s in nbBlocksStr)
            {
                estimateFee.Add(s, config_.EstimateFeeDefault.ToString("N8"));
            }
            return Json(estimateFee);
        }

        /// <summary>
        /// Get best block hash.
        /// </summary>
        /// <returns> Best block hash. </returns>
        [HttpGet("status/bestblockhash")]
        [SwaggerOperation("GetBestBlockHash")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetBestBlockHashResponse))]
        public async Task<ActionResult> GetBestBlockHash()
        {
            var getLastBlockResult = await GetLastBlock();
            
            return Json
            (
                new GetBestBlockHashResponse
                {
                    bestblockhash = Binary.ByteArrayToHexString(getLastBlockResult.Hash)
                }
            );
            
        }

        /// <summary>
        /// Get current coin price in US dollars.
        /// </summary>
        /// <returns> Current coin price in USD. </returns>
        [HttpGet("currency")]
        [SwaggerOperation("GetCurrency")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetCurrencyResponse))]
        public async Task<ActionResult> GetCurrency()
        {
            if( !memoryCache_.TryGetValue(Constants.Cache.CURRENT_PRICE_CACHE_KEY, out float usdPrice))
            {
                try
                {
                    usdPrice = await execPolicy_.ExecuteAsync(GetCurrentCoinPriceInUsd);
                    memoryCache_.Set
                    (
                        Constants.Cache.CURRENT_PRICE_CACHE_KEY, usdPrice,
                        new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(config_.MaxCoinPriceAgeInSeconds),
                            Size = Constants.Cache.CURRENT_PRICE_CACHE_ENTRY_SIZE
                        }
                    );
                }
                catch (Exception ex)
                {
                    logger_.LogWarning(ex, "Failed to get latest currency price from cache or external service; returning default value");
                }
            }
            return Json(new GetCurrencyResponse
            {
                status = 200,
                data = new CurrencyData
                {
                    bitstamp = usdPrice
                }
            });
        }

        /// <summary>
        /// Get latest block difficulty.
        /// </summary>
        /// <returns> Latest block difficulty. </returns>
        [HttpGet("status/difficulty")]
        [SwaggerOperation("GetDifficulty")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetDifficultyResponse))]
        public async Task<ActionResult> GetDifficulty()
        {
            var getLastBlockResult = await GetLastBlock();
            
            return Json
            (
                new GetDifficultyResponse
                {
                    difficulty = Utils.BitsToDifficulty(getLastBlockResult.Bits)
                }
            );
            
        }

        /// <summary>
        /// Check if the underlying bitprim node is running correctly.
        /// </summary>
        /// <param name="minimumSync"> Minimum required sync percentage (from 0 to 100) to consider node healthy. </param>
        /// <returns> "OK" if node healty, "NOK otherwise". </returns>
        [HttpGet("healthcheck")]
        [SwaggerOperation("GetHealthCheck")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(string))]
        public async Task<ActionResult> GetHealthCheck([FromQuery] float minimumSync)
        {
            if( !ModelState.IsValid )
            {
                return BadRequest("minimumSync must be a floating point number");
            }
            dynamic syncStatus = await DoGetSyncStatus();
            bool isNumeric = Double.TryParse(syncStatus.syncPercentage, out double syncPercentage);
            bool isHealthy = isNumeric && syncPercentage > minimumSync;
            return isHealthy?
                StatusCode((int)System.Net.HttpStatusCode.OK, "OK"):
                StatusCode((int)System.Net.HttpStatusCode.PreconditionFailed, "NOK");
        }

        /// <summary>
        /// Get underlying node information.
        /// </summary>
        /// <returns> See GetInfoResponse DTO. </returns>
        [HttpGet("status/info")]
        [SwaggerOperation("GetInfo")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetInfoResponse))]
        public async Task<ActionResult> GetInfo()
        {
            var getLastBlockResult = await GetLastBlock();
            
            return Json
            (
                new GetInfoResponse
                {
                    info = new GetInfoData
                    {
                        //TODO Some of these values should be retrieved from node-cint
                        version = config_.Version,
                        protocolversion = config_.ProtocolVersion,
                        blocks = getLastBlockResult.BlockHeight,
                        timeoffset = config_.TimeOffset,
                        connections = config_.Connections,
                        proxy = config_.Proxy,
                        difficulty = Utils.BitsToDifficulty(getLastBlockResult.Bits),
                        testnet = nodeExecutor_.UseTestnetRules,
                        relayfee = config_.RelayFee,
                        errors = "",
                        network = GetNetworkType(nodeExecutor_.NetworkType),
                        coin = GetCoin()
                    }
                }
            );
            
        }

        /// <summary>
        /// Get latest block hash.
        /// </summary>
        /// <returns> Latest block hash. </returns>
        [HttpGet("status/lastblockhash")]
        [SwaggerOperation("GetLastBlockHash")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetLastBlockHashResponse))]
        public async Task<ActionResult> GetLastBlockHash()
        {
            var getLastBlockResult = await GetLastBlock();
            var hashHexString = Binary.ByteArrayToHexString(getLastBlockResult.Hash);
            return Json
            (
                new GetLastBlockHashResponse
                {
                    syncTipHash = hashHexString,
                    lastblockhash = hashHexString
                }
            );
        }

        /// <summary>
        /// Get various node status information.
        /// (getInfo: see GetInfo method | getDifficulty: see GetDifficulty method | getBestBlockHash: see GetBestBlockHash method |
        ///  getLastBlockHash: see GetLastBlockHash method)
        /// </summary>
        /// <param name="method"> (getInfo | getDifficulty | getBestBlockHash | getLastBlockHash). Default: getInfo.
        /// Use the name 'q' for this query parameter (it will be mapped to the 'method' parameter).
        /// </param>
        /// <returns> Depends on method; see the referenced API method for each case. </returns>
        [HttpGet("status")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetStatus")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(object))]
        public async Task<ActionResult> GetStatus([Bind(Prefix = "q")] string method)
        {
            switch (method)
            {
                case Constants.GET_DIFFICULTY:
                    return await GetDifficulty();
                case Constants.GET_BEST_BLOCK_HASH:
                    return await GetBestBlockHash();
                case Constants.GET_LAST_BLOCK_HASH:
                    return await GetLastBlockHash();
            }
            return await GetInfo();
        }

        /// <summary>
        /// Get node synchronization status, as in how up to date it is with the blockchain.
        /// </summary>
        /// <returns> See GetSyncStatusResponse DTO. </returns>
        [HttpGet("sync")]
        [ResponseCache(CacheProfileName = Constants.Cache.SHORT_CACHE_PROFILE_NAME)]
        [SwaggerOperation("GetSyncStatus")]
        [SwaggerResponse((int)System.Net.HttpStatusCode.OK, typeof(GetSyncStatusResponse))]
        public async Task<ActionResult> GetSyncStatus()
        {
            return Json(await DoGetSyncStatus());
        }

        private async Task<GetLastBlock> GetLastBlock()
        {
            if (!memoryCache_.TryGetValue(Constants.Cache.LAST_BLOCK_HEIGHT_CACHE_KEY,out ulong currentHeight))
            {
                var getLastHeightResult = await chain_.FetchLastHeightAsync();
                Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "FetchLastHeightAsync() failed");
                currentHeight = getLastHeightResult.Result;

                memoryCache_.Set(Constants.Cache.LAST_BLOCK_HEIGHT_CACHE_KEY, currentHeight,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = Constants.Cache.LAST_BLOCK_HEIGHT_MAX_AGE,
                        Size = Constants.Cache.LAST_BLOCK_HEIGHT_ENTRY_SIZE
                    });
            }

            if (!memoryCache_.TryGetValue(Constants.Cache.LAST_BLOCK_CACHE_KEY,out GetLastBlock ret))
            {
                using (var getBlockDataResult = await chain_.FetchBlockHeaderByHeightAsync(currentHeight))
                {
                    Utils.CheckBitprimApiErrorCode(getBlockDataResult.ErrorCode, "FetchBlockHeaderByHeightAsync(" + currentHeight + ") failed");

                    ret = new GetLastBlock
                    {
                        BlockHeight = getBlockDataResult.Result.BlockHeight
                        , Bits = getBlockDataResult.Result.BlockData.Bits
                        , Hash = getBlockDataResult.Result.BlockData.Hash
                    };

                    memoryCache_.Set(Constants.Cache.LAST_BLOCK_CACHE_KEY, ret,
                        new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = Constants.Cache.LAST_BLOCK_MAX_AGE,
                            Size = Constants.Cache.LAST_BLOCK_ENTRY_SIZE
                        });
                }
            }

            return ret;
        }

        //TODO Consider moving this down to node-cint for other APIs to reuse
        private async Task<float> GetCurrentCoinPriceInUsd()
        {
            string currencyPair;
            switch (NodeSettings.CurrencyType)
            {
                case CurrencyType.Bitcoin: currencyPair = Constants.BITSTAMP_BTCUSD; break;
                case CurrencyType.BitcoinCash: currencyPair = Constants.BITSTAMP_BCCUSD; break;
                case CurrencyType.Litecoin: currencyPair = Constants.BITSTAMP_LTCUSD; break;
                default: throw new InvalidOperationException("Unsupported currency: " + NodeSettings.CurrencyType);
            }
            string bitstampUrl = Constants.BITSTAMP_URL.Replace(Constants.BITSTAMP_CURRENCY_PAIR_PLACEHOLDER, currencyPair);
            var priceDataString = await httpClient_.GetStringAsync(bitstampUrl);
            dynamic priceData = JsonConvert.DeserializeObject<dynamic>(priceDataString);
            if (!float.TryParse(priceData.last.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float price))
            {
                throw new FormatException("Invalid price value: " + priceData.last.Value);
            }
            return price;
        }

        private async Task<object> DoGetSyncStatus()
        {
            var getLastHeightResult = await chain_.FetchLastHeightAsync();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "GetLastHeight() failed");
            var currentHeight = getLastHeightResult.Result;

            if( !memoryCache_.TryGetValue(Constants.Cache.LAST_BLOCK_TIMESTAMP, out long lastBlockTimestamp) )
            {
                var getLastBlockResult = await chain_.FetchBlockByHeightHashTimestampAsync(currentHeight);
                Utils.CheckBitprimApiErrorCode(getLastBlockResult.ErrorCode, "FetchBlockByHeightAsync(" + currentHeight + ") failed, check error log");
                lastBlockTimestamp = new DateTimeOffset(getLastBlockResult.Result.BlockTimestamp).ToUnixTimeSeconds();
                memoryCache_.Set(Constants.Cache.LAST_BLOCK_TIMESTAMP, lastBlockTimestamp,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = Constants.Cache.BLOCK_TIMESTAMP_MAX_AGE,
                        Size = Constants.Cache.TIMESTAMP_ENTRY_SIZE
                    });
            }

            if( !memoryCache_.TryGetValue(Constants.Cache.FIRST_BLOCK_TIMESTAMP, out long firstBlockTimestamp) )
            {
                var getFirstBlockResult = await chain_.FetchBlockByHeightHashTimestampAsync(0);
                Utils.CheckBitprimApiErrorCode(getFirstBlockResult.ErrorCode, "FetchBlockByHeightHashTimestampAsync(0) failed, check error log");
                firstBlockTimestamp = new DateTimeOffset(getFirstBlockResult.Result.BlockTimestamp).ToUnixTimeSeconds();
                memoryCache_.Set(Constants.Cache.FIRST_BLOCK_TIMESTAMP, firstBlockTimestamp,
                    new MemoryCacheEntryOptions
                    {
                        Size = Constants.Cache.TIMESTAMP_ENTRY_SIZE
                    });
            }
            var nowTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var lastBlockAge = nowTimestamp - lastBlockTimestamp;
            bool synced = lastBlockAge < config_.BlockchainStalenessThreshold;
            dynamic syncStatus = new ExpandoObject();
            syncStatus.status = synced ? "finished" : "synchronizing";
            syncStatus.blockChainHeight = currentHeight;
            syncStatus.syncPercentage = synced?
                "100" :
                 Math.Min((double)(lastBlockTimestamp - firstBlockTimestamp) / (double)(nowTimestamp - firstBlockTimestamp) * 100.0, 100).ToString("N2");
            syncStatus.error = null;
            syncStatus.height = currentHeight;
            syncStatus.type = config_.NodeType;
            return syncStatus;
        }

        private static string GetNetworkType(NetworkType networkType)
        {
            switch (networkType)
            {
                case NetworkType.Mainnet:
                    return "livenet";
                default:
                    return networkType.ToString().ToLower();
            }
        }

        private string GetCoin()
        {
            switch( NodeSettings.CurrencyType )
            {
                case CurrencyType.Bitcoin: return nodeExecutor_.UseTestnetRules? "tbtc" : "btc";
                case CurrencyType.BitcoinCash: return nodeExecutor_.UseTestnetRules? "tbch" : "bch";
                case CurrencyType.Litecoin: return nodeExecutor_.UseTestnetRules? "tltc" : "ltc";
                default: throw new InvalidOperationException("Invalid coin: " + NodeSettings.CurrencyType);
            }
        }

    }
}