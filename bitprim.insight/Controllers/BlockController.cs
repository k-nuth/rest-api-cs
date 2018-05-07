using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace bitprim.insight.Controllers
{
    [Route("[controller]")]
    public class BlockController : Controller
    {
        private readonly Chain chain_;
        private readonly IMemoryCache memoryCache_;
        private readonly PoolsInfo poolsInfo_;
        private readonly WebSocketHandler webSocketHandler_;
        private readonly NodeConfig config_;

        public BlockController(IOptions<NodeConfig> config, Chain chain, WebSocketHandler webSocketHandler, IMemoryCache memoryCache, PoolsInfo poolsInfo)
        {
            config_ = config.Value;
            chain_ = chain;
            webSocketHandler_ = webSocketHandler;
            memoryCache_ = memoryCache;
            poolsInfo_ = poolsInfo;
        }

      
        // GET: block/simulate
        [HttpGet("block/simulate")]
        public ActionResult Simulate()
        {
            var newBlocksNotification = new
            {
                eventname = "block"
            };

            var task = webSocketHandler_.PublishBlock(JsonConvert.SerializeObject(newBlocksNotification));
            task.Wait();

            return Ok();
        }
        

        // GET: block/{hash}
        [ResponseCache(CacheProfileName = Constants.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("block/{hash}")]
        public async Task<ActionResult> GetBlockByHash(string hash)
        {
            if(!Validations.IsValidHash(hash))
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, hash + " is not a valid block hash");
            }

            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);

            if(memoryCache_.TryGetValue("block" + hash, out JsonResult cachedBlockJson))
            {
                return cachedBlockJson;
            };
            
            var binaryHash = Binary.HexStringToByteArray(hash);
            
            using(var getBlockResult = await chain_.FetchBlockHeaderByHashTxSizesAsync(binaryHash))
            {
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockHeaderByHashTxSizesAsync(" + hash + ") failed, check error log");
                
                var getLastHeightResult = await chain_.FetchLastHeightAsync();
                Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "FetchLastHeightAsync() failed, check error log");
                
                var blockHeight = getBlockResult.Result.Block.BlockHeight;

                ApiCallResult<GetBlockHashTimestampResult> getNextBlockResult = null;
                if(blockHeight != getLastHeightResult.Result)
                {
                    getNextBlockResult = await chain_.FetchBlockByHeightHashTimestampAsync(blockHeight + 1);
                    Utils.CheckBitprimApiErrorCode(getNextBlockResult.ErrorCode, "FetchBlockByHeightHashTimestampAsync(" + blockHeight + 1 + ") failed, check error log");
                }
                
                double blockReward;
                PoolsInfo.PoolInfo poolInfo;
                using(DisposableApiCallResult<GetTxDataResult> coinbase = await chain_.FetchTransactionAsync(getBlockResult.Result.TransactionHashes[0], true))
                {
                    Utils.CheckBitprimApiErrorCode(coinbase.ErrorCode, "FetchTransactionAsync(" + getBlockResult.Result.TransactionHashes[0] + ") failed, check error log");
                    blockReward = Math.Round(Utils.SatoshisToCoinUnits(coinbase.Result.Tx.TotalOutputValue),2);
                    poolInfo = poolsInfo_.GetPoolInfo(coinbase.Result.Tx);
                }

                JsonResult blockJson = Json(BlockToJSON
                (
                    getBlockResult.Result.Block.BlockData, blockHeight, getBlockResult.Result.TransactionHashes,
                    blockReward, getLastHeightResult.Result, getNextBlockResult?.Result.BlockHash,
                    getBlockResult.Result.SerializedBlockSize,poolInfo)
                );

                memoryCache_.Set("block" + hash, blockJson, new MemoryCacheEntryOptions{Size = Constants.BLOCK_CACHE_ENTRY_SIZE});
                return blockJson;
            }
        }

        // GET: block-index/{height}
        [ResponseCache(CacheProfileName = Constants.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("block-index/{height}")]
        public async Task<ActionResult> GetBlockByHeight(UInt64 height)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            
            var result = await chain_.FetchBlockByHeightHashTimestampAsync(height);
            Utils.CheckBitprimApiErrorCode(result.ErrorCode, "FetchBlockByHeightHashTimestampAsync(" + height + ") failed, error log");
            
            return Json
            (
                new
                {
                    blockHash = Binary.ByteArrayToHexString(result.Result.BlockHash)
                }
            );               
        }

        // GET: rawblock/{hash}
        [ResponseCache(CacheProfileName = Constants.LONG_CACHE_PROFILE_NAME)]
        [HttpGet("rawblock/{hash}")]
        public async Task<ActionResult> GetRawBlockByHash(string hash)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            var binaryHash = Binary.HexStringToByteArray(hash);
            
            using(var getBlockResult = await chain_.FetchBlockByHashAsync(binaryHash))
            {
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockByHashAsync(" + hash + ") failed, check error log");
                
                var block = getBlockResult.Result.BlockData;
                
                return Json
                (
                    new
                    {
                        rawblock = Binary.ByteArrayToHexString(block.ToData(false).Reverse().ToArray())
                    }
                );
            }   
        }

        // GET: rawblock-index/{height}
        [ResponseCache(CacheProfileName = Constants.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("rawblock-index/{height}")]
        public async Task<ActionResult> GetRawBlockByHeight(UInt64 height)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            
            using(var getBlockResult = await chain_.FetchBlockByHeightAsync(height))
            {
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockByHeightAsync(" + height + ") failed, check error log");
                
                var block = getBlockResult.Result.BlockData;
                return Json
                (
                    new
                    {
                        rawblock = Binary.ByteArrayToHexString(block.ToData(false).Reverse().ToArray())
                    }
                );
            }
        }

        // GET: blocks/?limit={limit}&blockDate={blockDate}
        [ResponseCache(CacheProfileName = Constants.SHORT_CACHE_PROFILE_NAME)]
        [HttpGet("blocks/")]
        public async Task<ActionResult> GetBlocksByDate(int limit = 200, string blockDate = "")
        {
            //Validate input
            var validateInputResult = ValidateGetBlocksByDateInput(limit, blockDate);
            if(!validateInputResult.Item1)
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, validateInputResult.Item2);
            }
            
            var blockDateToSearch = validateInputResult.Item3;
            //These define the search interval (lte, gte)
            var gte = new DateTimeOffset(blockDateToSearch).ToUnixTimeSeconds();
            var lte =  gte + 86400;

            //Find blocks starting point
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            
            var getLastHeightResult = await chain_.FetchLastHeightAsync();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "FetchLastHeightAsync failed, check error log");
            
            var topHeight = getLastHeightResult.Result;
            var low = await FindFirstBlockFromNextDay(blockDateToSearch, topHeight);
            if(low == 0) //No blocks
            {
                return Json(BlocksByDateToJSON(new List<object>(), blockDateToSearch, false, -1,lte));
            }
            
            //Grab the specified amount of blocks (limit)
            var startingHeight = low - 1;
            
            var blocks = await GetPreviousBlocks(startingHeight, (UInt64)limit, blockDateToSearch,topHeight);

            //Check if there are more blocks: grab one more earlier block
            var moreBlocks = await CheckIfMoreBlocks(startingHeight, (UInt64)limit, blockDateToSearch,lte);
            return Json(BlocksByDateToJSON(blocks, blockDateToSearch, moreBlocks.Item1, moreBlocks.Item2,lte));   
        }

        private Tuple<bool, string, DateTime> ValidateGetBlocksByDateInput(int limit, string blockDate)
        {
            if(limit <= 0)
            {
                return new Tuple<bool, string, DateTime>(false, "Invalid limit; must be greater than zero", DateTime.MinValue);
            }

            if(limit > config_.MaxBlockSummarySize)
            {
                return new Tuple<bool, string, DateTime>(false, "Invalid limit; must be lower than " + config_.MaxBlockSummarySize, DateTime.MinValue);
            }

            if(string.IsNullOrWhiteSpace(blockDate))
            {
                blockDate = DateTime.Today.ToString(config_.DateInputFormat);
            }

            if(!DateTime.TryParseExact(blockDate, config_.DateInputFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var blockDateToSearch))
            {
                return new Tuple<bool, string, DateTime>(false, "Invalid date format; expected " + config_.DateInputFormat, DateTime.MinValue);
            }

            return new Tuple<bool, string, DateTime>(true, "", blockDateToSearch);
        }

        private async Task<UInt64> FindFirstBlockFromNextDay(DateTime blockDateToSearch, UInt64 topHeight)
        {
            //Adapted binary search
            UInt64 low = 0;
            var high = topHeight;
            
            while(low < high)
            {
                var mid = (UInt64) ((double)low + (double) high)/2;
                
                var getBlockResult = await chain_.FetchBlockByHeightHashTimestampAsync(mid);
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockByHeightHashTimestampAsync(" + mid + ") failed, check error log");
                
                if(getBlockResult.Result.BlockTimestamp.Date <= blockDateToSearch.Date)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }
            //If the last block belongs to the sought date, the first block from the next day is "1 block off the chain"
            //We do this to avoid missing the latest block
            if(low == topHeight)
            {
                var getBlockResult = await chain_.FetchBlockByHeightHashTimestampAsync(topHeight);
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockByHeightHashTimestampAsync(" + topHeight + ") failed, check error log");
                if(getBlockResult.Result.BlockTimestamp.Date == blockDateToSearch.Date)
                {
                    low = topHeight + 1;
                }
            }
            return low;
        }

        private async Task<object> GetBlockSummary(Block block, UInt64 height, UInt64 topHeight)
        {
            var hashStr = Binary.ByteArrayToHexString(block.Hash);
            var key = "blockSummary" + hashStr;

            if (memoryCache_.TryGetValue(key,out object ret))
            {
                return ret;
            }

            using (var blockHeaderResult = await chain_.FetchBlockHeaderByHashTxSizesAsync(block.Hash))
            {
                Utils.CheckBitprimApiErrorCode(blockHeaderResult.ErrorCode,
                    "FetchBlockHeaderByHashTxSizesAsync(" + block.Hash + ") failed, check error log");

                PoolsInfo.PoolInfo poolInfo;
                using(DisposableApiCallResult<GetTxDataResult> coinbase = await chain_.FetchTransactionAsync(blockHeaderResult.Result.TransactionHashes[0], true))
                {
                    Utils.CheckBitprimApiErrorCode(coinbase.ErrorCode, "FetchTransactionAsync(" + blockHeaderResult.Result.TransactionHashes[0] + ") failed, check error log");
                    poolInfo = poolsInfo_.GetPoolInfo(coinbase.Result.Tx);
                }
                
                var obj = new
                {
                    height = height,
                    size = block.GetSerializedSize(block.Header.Version),
                    hash = Binary.ByteArrayToHexString(block.Hash),
                    time = block.Header.Timestamp,
                    txlength = block.TransactionCount,
                    poolInfo = new{ poolName = poolInfo.Name, url = poolInfo.Url}
                };

                var confirmations = topHeight - height + 1;
                if (confirmations >= Constants.BLOCK_CACHE_CONFIRMATIONS)
                {
                    memoryCache_.Set(key, obj, new MemoryCacheEntryOptions{Size = Constants.BLOCK_CACHE_SUMMARY_SIZE});
                }

                return obj;
            }
        }

        private async Task<List<object>> GetPreviousBlocks(UInt64 startingHeight, UInt64 blockCount, DateTime blockDateToSearch, UInt64 topHeight)
        {
            var blocks = new List<object>();
            var blockWithinDate = true;
            
            for(UInt64 i=0; i<blockCount && startingHeight>=i && blockWithinDate; i++)
            {
                using(var getBlockResult = await chain_.FetchBlockByHeightAsync(startingHeight - i))
                {
                    Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockByHeightAsync(" + (startingHeight - i) + ") failed, check error log");
                    
                    var block = getBlockResult.Result.BlockData;

                    blockWithinDate = DateTimeOffset.FromUnixTimeSeconds(block.Header.Timestamp).UtcDateTime.Date == blockDateToSearch.Date;
                    
                    if(blockWithinDate)
                    {
                        blocks.Add(await GetBlockSummary(block,getBlockResult.Result.BlockHeight, topHeight)); 
                    }
                }
            }
            return blocks;
        }

        private async Task<Tuple<bool, long>> CheckIfMoreBlocks(UInt64 startingHeight, UInt64 limit, DateTime blockDateToSearch, long lte)
        {
            bool moreBlocks;
            long moreBlocksTs = lte;
            
            if(startingHeight <= limit)
            {
                moreBlocks = false;
            }
            else
            {
                var getBlockResult = await chain_.FetchBlockByHeightHashTimestampAsync(startingHeight - limit);
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockByHeightHashTimestampAsync(" + (startingHeight - limit) + ") failed, check error log");
                
                var blockDateUtc = getBlockResult.Result.BlockTimestamp;
                
                moreBlocks = blockDateUtc.Date == blockDateToSearch.Date;
            }
            return new Tuple<bool, long>(moreBlocks, moreBlocksTs);
        }

        private object BlocksByDateToJSON(List<dynamic> blocks, DateTime blockDateToSearch, bool moreBlocks, long moreBlocksTs, long lte)
        {
            return new
            {
                blocks = blocks.ToArray(),
                length = blocks.Count,
                pagination = new
                {
                    next = blockDateToSearch.Date.AddDays(+1).ToString(config_.DateInputFormat),
                    prev = blockDateToSearch.Date.AddDays(-1).ToString(config_.DateInputFormat),
                    currentTs = lte - 1,
                    current = blockDateToSearch.Date.ToString(config_.DateInputFormat),
                    isToday = blockDateToSearch.Date == DateTime.UtcNow.Date,
                    more = moreBlocks,
                    moreTs = moreBlocks ? (object)moreBlocksTs : null
                }
            };
        }

        private static object BlockToJSON(Header blockHeader, UInt64 blockHeight, HashList txHashes,
                                          double blockReward, UInt64 currentHeight, byte[] nextBlockHash,
                                          UInt64 serializedBlockSize, PoolsInfo.PoolInfo poolInfo)
        {
            BigInteger.TryParse(blockHeader.ProofString, out var proof);
            dynamic blockJson = new ExpandoObject();
            blockJson.hash = Binary.ByteArrayToHexString(blockHeader.Hash);
            blockJson.size = serializedBlockSize;
            blockJson.height = blockHeight;
            blockJson.version = blockHeader.Version;
            blockJson.merkleroot = Binary.ByteArrayToHexString(blockHeader.Merkle);
            blockJson.tx = BlockTxsToJSON(txHashes);
            blockJson.time = blockHeader.Timestamp;
            blockJson.nonce = blockHeader.Nonce;
            blockJson.bits = Utils.EncodeInBase16(blockHeader.Bits);
            blockJson.difficulty = Utils.BitsToDifficulty(blockHeader.Bits); //TODO Use bitprim API when implemented
            blockJson.chainwork = (proof * 2).ToString("X64"); //TODO Does not match Blockdozer value; check how bitpay calculates it
            blockJson.confirmations = currentHeight - blockHeight + 1;
            blockJson.previousblockhash = Binary.ByteArrayToHexString(blockHeader.PreviousBlockHash);
            if(nextBlockHash != null)
            {
                blockJson.nextblockhash = Binary.ByteArrayToHexString(nextBlockHash);
            }
            blockJson.reward = blockReward;
            blockJson.isMainChain = true; //TODO Check value
            blockJson.poolInfo = new{ poolName = poolInfo.Name, url = poolInfo.Url};
            return blockJson;
        }

        private static object[] BlockTxsToJSON(HashList txHashes)
        {
            var txs = new List<object>();
            for(uint i = 0; i<txHashes.Count; i++)
            {
                txs.Add(Binary.ByteArrayToHexString(txHashes[i]));
            }
            return txs.ToArray();
        }
    }
}
