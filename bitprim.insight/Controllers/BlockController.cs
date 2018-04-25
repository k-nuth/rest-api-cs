using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace bitprim.insight.Controllers
{
    [Route("api/[controller]")]
    public class BlockController : Controller
    {
        private Chain chain_;
        private readonly WebSocketHandler webSocketHandler_;
        private readonly NodeConfig config_;

        public BlockController(IOptions<NodeConfig> config, Chain chain, WebSocketHandler webSocketHandler)
        {
            config_ = config.Value;
            chain_ = chain;
            webSocketHandler_ = webSocketHandler;
        }

      
        // GET: api/block/simulate
        [HttpGet("/api/block/simulate")]
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
        

        // GET: api/block/{hash}
        [HttpGet("/api/block/{hash}")]
        public async Task<ActionResult> GetBlockByHash(string hash)
        {
            if(!Validations.IsValidHash(hash))
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, hash + " is not a valid block hash");
            }

            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            
            var binaryHash = Binary.HexStringToByteArray(hash);
            
            using(var getBlockResult = await chain_.FetchBlockHeaderByHashTxSizesAsync(binaryHash))
            {
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockHeaderByHashTxSizesAsync(" + hash + ") failed, check error log");
                
                var getLastHeightResult = await chain_.FetchLastHeightAsync();
                Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "FetchLastHeightAsync() failed, check error log");
                
                var blockHeight = getBlockResult.Result.Block.BlockHeight;

                var getNextBlockResult = await chain_.FetchBlockByHeightHashTimestampAsync(blockHeight + 1);
                Utils.CheckBitprimApiErrorCode(getNextBlockResult.ErrorCode, "FetchBlockByHeightHashTimestampAsync(" + blockHeight + 1 + ") failed, check error log");
                
                double blockReward;
                using(var coinbase = await chain_.FetchTransactionAsync(getBlockResult.Result.TransactionHashes[0], true))
                {
                    blockReward = Utils.SatoshisToCoinUnits(coinbase.Result.Tx.TotalOutputValue);
                }
                
                return Json(BlockToJSON
                (
                    getBlockResult.Result.Block.BlockData, blockHeight, getBlockResult.Result.TransactionHashes,
                    blockReward, getLastHeightResult.Result, getNextBlockResult.Result.BlockHash,
                    getBlockResult.Result.SerializedBlockSize)
                );
            }
        }

        // GET: api/block-index/{height}
        [HttpGet("/api/block-index/{height}")]
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

        // GET: api/rawblock/{hash}
        [HttpGet("/api/rawblock/{hash}")]
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

        // GET: api/rawblock-index/{height}
        [HttpGet("/api/rawblock-index/{height}")]
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

        // GET: api/blocks/?limit={limit}&blockDate={blockDate}
        [HttpGet("/api/blocks/")]
        public async Task<ActionResult> GetBlocksByDate(int limit = 200, string blockDate = "")
        {
            //Validate input
            var validateInputResult = ValidateGetBlocksByDateInput(limit, blockDate);
            if(!validateInputResult.Item1)
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, validateInputResult.Item2);
            }
            
            var blockDateToSearch = validateInputResult.Item3;
            
            //Find blocks starting point
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            
            var getLastHeightResult = await chain_.FetchLastHeightAsync();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "FetchLastHeightAsync failed, check error log");
            
            var topHeight = getLastHeightResult.Result;
            var low = await FindFirstBlockFromNextDay(blockDateToSearch, topHeight);
            if(low == 0) //No blocks
            {
                return Json(BlocksByDateToJSON(new List<object>(), blockDateToSearch, false, -1));
            }
            
            //Grab the specified amount of blocks (limit)
            var startingHeight = low - 1;
            
            var blocks = await GetPreviousBlocks(startingHeight, (UInt64)limit, blockDateToSearch);
            
            //Check if there are more blocks: grab one more earlier block
            var moreBlocks = await CheckIfMoreBlocks(startingHeight, (UInt64)limit, blockDateToSearch);
            return Json(BlocksByDateToJSON(blocks, blockDateToSearch, moreBlocks.Item1, moreBlocks.Item2));   
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

            if(!DateTime.TryParseExact(blockDate, config_.DateInputFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var blockDateToSearch))
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
            return low;
        } 

        private async Task<List<object>> GetPreviousBlocks(UInt64 startingHeight, UInt64 blockCount, DateTime blockDateToSearch)
        {
            var blocks = new List<object>();
            var blockWithinDate = true;
            
            for(UInt64 i=0; i<blockCount && startingHeight>=i && blockWithinDate; i++)
            {
                using(var getBlockResult = await chain_.FetchBlockByHeightAsync(startingHeight - i))
                {
                    Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockByHeightAsync(" + (startingHeight - i) + ") failed, check error log");
                    
                    var block = getBlockResult.Result.BlockData;
                    blockWithinDate = DateTimeOffset.FromUnixTimeSeconds(block.Header.Timestamp).Date == blockDateToSearch.Date;
                    if(blockWithinDate)
                    {
                        blocks.Add(new
                        {
                            height = getBlockResult.Result.BlockHeight,
                            size = block.GetSerializedSize(block.Header.Version),
                            hash = Binary.ByteArrayToHexString(block.Hash),
                            time = block.Header.Timestamp,
                            txlength = block.TransactionCount
                            //TODO Add pool info
                        });
                    }
                }
            }
            return blocks;
        }

        private async Task<Tuple<bool, int>> CheckIfMoreBlocks(UInt64 startingHeight, UInt64 limit, DateTime blockDateToSearch)
        {
            bool moreBlocks;
            int moreBlocksTs = -1;
            if(startingHeight <= limit)
            {
                moreBlocks = false;
            }
            else
            {
                var getBlockResult = await chain_.FetchBlockByHeightHashTimestampAsync(startingHeight - limit);
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "FetchBlockByHeightHashTimestampAsync(" + (startingHeight - limit) + ") failed, check error log");
                
                var blockDate = getBlockResult.Result.BlockTimestamp;
                moreBlocks = blockDate.Date == blockDateToSearch.Date;
                moreBlocksTs = moreBlocks? (int) ((DateTimeOffset)blockDate).ToUnixTimeSeconds() : -1;
            }
            return new Tuple<bool, int>(moreBlocks, moreBlocksTs);
        }

        private static object BlocksByDateToJSON(List<dynamic> blocks, DateTime blockDate, bool moreBlocks, int moreBlocksTs)
        {
            const string DATE_FORMAT = "yyyy-MM-dd";
            return new
            {
                blocks = blocks.ToArray(),
                length = blocks.Count,
                pagination = new
                {
                    next = blockDate.Date.AddDays(+1).ToString(DATE_FORMAT),
                    prev = blockDate.Date.AddDays(-1).ToString(DATE_FORMAT),
                    currentTs = blocks.Count > 0? blocks[0].time : new DateTimeOffset(blockDate).ToUnixTimeSeconds(),
                    current = blockDate.Date.ToString(DATE_FORMAT),
                    isToday = blockDate.Date == DateTime.Today,
                    more = moreBlocks,
                    moreTs = moreBlocks? (object) moreBlocksTs : null
                }
            };
        }

        private static object BlockToJSON(Header blockHeader, UInt64 blockHeight, HashList txHashes,
                                          double blockReward, UInt64 currentHeight, byte[] nextBlockHash,
                                          UInt64 serializedBlockSize)
        {
            BigInteger.TryParse(blockHeader.ProofString, out var proof);
            return new
            {
                hash = Binary.ByteArrayToHexString(blockHeader.Hash),
                size = serializedBlockSize,
                height = blockHeight,
                version = blockHeader.Version,
                merkleroot = Binary.ByteArrayToHexString(blockHeader.Merkle),
                tx = BlockTxsToJSON(txHashes),
                time = blockHeader.Timestamp,
                nonce = blockHeader.Nonce,
                bits = Utils.EncodeInBase16(blockHeader.Bits),
                difficulty = Utils.BitsToDifficulty(blockHeader.Bits), //TODO Use bitprim API when implemented
                chainwork = (proof * 2).ToString("X64"), //TODO Does not match Blockdozer value; check how bitpay calculates it
                confirmations = currentHeight - blockHeight,
                previousblockhash = Binary.ByteArrayToHexString(blockHeader.PreviousBlockHash),
                nextblockhash = Binary.ByteArrayToHexString(nextBlockHash),
                reward = blockReward,
                isMainChain = true, //TODO Check value
                poolInfo = new{} //TODO Check value
            };
        }

        private static object[] BlockTxsToJSON(HashList txHashes)
        {
            var txs = new List<object>();
            for(int i = 0; i<txHashes.Count; i++)
            {
                txs.Add(Binary.ByteArrayToHexString(txHashes[i]));
            }
            return txs.ToArray();
        }

    }
}