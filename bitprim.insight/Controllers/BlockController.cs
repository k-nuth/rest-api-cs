using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
        public ActionResult GetBlockByHash(string hash)
        {
            if(!Validations.IsValidHash(hash))
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, hash + " is not a valid block hash");
            }
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            byte[] binaryHash = Binary.HexStringToByteArray(hash);
            using(DisposableApiCallResult<GetBlockByHashTxSizeResult> getBlockResult = chain_.GetBlockByHashTxSizes(binaryHash))
            {
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "GetBlockByHashTxSizes(" + hash + ") failed, check error log");
                ApiCallResult<UInt64> getLastHeightResult = chain_.GetLastHeight();
                Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "GetLastHeight() failed, check error log");
                UInt64 topHeight = getLastHeightResult.Result;
                UInt64 blockHeight = getBlockResult.Result.Block.BlockHeight;
                ApiCallResult<byte[]> getNextBlockResult = chain_.GetBlockHash(blockHeight + 1);
                Utils.CheckBitprimApiErrorCode
                (
                    getNextBlockResult.ErrorCode, "GetBlockByHeight(" + blockHeight + 1 + ") failed, check error log"
                );
                return Json(BlockToJSON
                (
                    getBlockResult.Result.Block.BlockData, blockHeight, getBlockResult.Result.TransactionHashes, getNextBlockResult.Result,
                    getBlockResult.Result.SerializedBlockSize)
                );
            }
        }

        // GET: api/block-index/{height}
        [HttpGet("/api/block-index/{height}")]
        public ActionResult GetBlockByHeight(UInt64 height)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            ApiCallResult<byte[]> result = chain_.GetBlockHash(height);
            Utils.CheckBitprimApiErrorCode(result.ErrorCode, "GetBlockByHeight(" + height + ") failed, error log");
            return Json
            (
                new
                {
                    blockHash = Binary.ByteArrayToHexString(result.Result)
                }
            );               
        }

        // GET: api/rawblock/{hash}
        [HttpGet("/api/rawblock/{hash}")]
        public ActionResult GetRawBlockByHash(string hash)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            byte[] binaryHash = Binary.HexStringToByteArray(hash);
            using(DisposableApiCallResult<GetBlockDataResult<Block>> getBlockResult = chain_.GetBlockByHash(binaryHash))
            {
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "GetBlockByHash(" + hash + ") failed, check error log");
                Block block = getBlockResult.Result.BlockData;
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
        public ActionResult GetRawBlockByHeight(UInt64 height)
        {
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            using(DisposableApiCallResult<GetBlockDataResult<Block>> getBlockResult = chain_.GetBlockByHeight(height))
            {
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "GetBlockByHeight(" + height + ") failed, check error log");
                Block block = getBlockResult.Result.BlockData;
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
        public ActionResult GetBlocksByDate(int? limit = 200, string blockDate = "")
        {
            //Validate input
            Tuple<bool, string, DateTime?> validateInputResult = ValidateGetBlocksByDateInput(limit.Value, blockDate);
            if(!validateInputResult.Item1)
            {
                return StatusCode((int)System.Net.HttpStatusCode.BadRequest, validateInputResult.Item2);
            }
            DateTime blockDateToSearch = validateInputResult.Item3.Value;
            //Find blocks starting point
            Utils.CheckIfChainIsFresh(chain_, config_.AcceptStaleRequests);
            ApiCallResult<UInt64> getLastHeightResult = chain_.GetLastHeight();
            Utils.CheckBitprimApiErrorCode(getLastHeightResult.ErrorCode, "GetLastHeight failed, check error log");
            UInt64 topHeight = getLastHeightResult.Result;
            UInt64 low = FindFirstBlockFromNextDay(blockDateToSearch, topHeight);
            if(low == 0) //No blocks
            {
                return Json(BlocksByDateToJSON(new List<object>(), blockDateToSearch, false, -1));
            }
            //Grab the specified amount of blocks (limit)
            UInt64 startingHeight = low - 1;
            List<object> blocks = GetPreviousBlocks(startingHeight, (UInt64)limit, blockDateToSearch);
            //Check if there are more blocks: grab one more earlier block
            Tuple<bool, int> moreBlocks = CheckIfMoreBlocks(startingHeight, (UInt64)limit, blockDateToSearch);
            return Json(BlocksByDateToJSON(blocks, blockDateToSearch, moreBlocks.Item1, moreBlocks.Item2));   
        }

        private Tuple<bool, string, DateTime?> ValidateGetBlocksByDateInput(int limit, string blockDate)
        {
            if(limit <= 0)
            {
                return new Tuple<bool, string, DateTime?>(false, "Invalid limit; must be greater than zero", null);
            }
            if(limit > config_.MaxBlockSummarySize)
            {
                return new Tuple<bool, string, DateTime?>(false, "Invalid limit; must be lower than " + config_.MaxBlockSummarySize, null);
            }
            if(string.IsNullOrWhiteSpace(blockDate))
            {
                blockDate = DateTime.Now.Date.ToString(config_.DateInputFormat);
            }
            DateTime blockDateToSearch;
            if(!DateTime.TryParseExact(blockDate, config_.DateInputFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out blockDateToSearch))
            {
                return new Tuple<bool, string, DateTime?>(false, "Invalid date format; expected " + config_.DateInputFormat, null);
            }
            return new Tuple<bool, string, DateTime?>(true, "", blockDateToSearch);
        }

        private UInt64 FindFirstBlockFromNextDay(DateTime blockDateToSearch, UInt64 topHeight)
        {
            //Adapted binary search
            UInt64 low = 0;
            UInt64 high = topHeight;
            UInt64 mid = 0;
            while(low < high)
            {
                mid = (UInt64) ((double)low + (double) high)/2; //Adds as doubles to prevent overflow
                ApiCallResult<GetBlockHashTimestampResult> getBlockResult = chain_.GetBlockByHeightHashTimestamp(mid);
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "GetBlockByHeightHashTimestamp(" + mid + ") failed, check error log");
                if(getBlockResult.Result.BlockTimestamp.Date <= blockDateToSearch.Date)
                {
                    low = mid + 1;
                }else
                {
                    high = mid;
                }
            }
            return low;
        } 

        private List<object> GetPreviousBlocks(UInt64 startingHeight, UInt64 blockCount, DateTime blockDateToSearch)
        {
            var blocks = new List<object>();
            bool blockWithinDate = true;
            for(UInt64 i=0; i<blockCount && startingHeight-i>=0 && blockWithinDate; i++)
            {
                using(DisposableApiCallResult<GetBlockDataResult<Block>> getBlockResult = chain_.GetBlockByHeight(startingHeight - i))
                {
                    Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "GetBlockByHeight(" + (startingHeight - i) + ") failed, check error log");
                    Block block = getBlockResult.Result.BlockData;
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

        private Tuple<bool, int> CheckIfMoreBlocks(UInt64 startingHeight, UInt64 limit, DateTime blockDateToSearch)
        {
            bool moreBlocks = false;
            int moreBlocksTs = -1;
            if(startingHeight - limit <= 0)
            {
                moreBlocks = false;
            }
            else
            {
                ApiCallResult<GetBlockHashTimestampResult> getBlockResult = chain_.GetBlockByHeightHashTimestamp(startingHeight - limit);
                Utils.CheckBitprimApiErrorCode(getBlockResult.ErrorCode, "GetBlockByHeightHashTimestamp(" + (startingHeight - limit) + ") failed, check error log");
                DateTime blockDate = getBlockResult.Result.BlockTimestamp;
                moreBlocks = blockDate.Date == blockDateToSearch.Date;
                moreBlocksTs = moreBlocks? (int) ((DateTimeOffset)blockDate).ToUnixTimeSeconds() : -1;
            }
            return new Tuple<bool, int>(moreBlocks, moreBlocksTs);
        }

        private static object BlocksByDateToJSON(List<dynamic> blocks, DateTime blockDate, bool moreBlocks, int moreBlocksTs)
        {
            const string dateFormat = "yyyy-MM-dd";
            return new
            {
                blocks = blocks.ToArray(),
                length = blocks.Count,
                pagination = new
                {
                    next = blockDate.Date.AddDays(+1).ToString(dateFormat),
                    prev = blockDate.Date.AddDays(-1).ToString(dateFormat),
                    currentTs = blocks.Count > 0? blocks[0].time : new DateTimeOffset(blockDate).ToUnixTimeSeconds(),
                    current = blockDate.Date.ToString(dateFormat),
                    isToday = (blockDate.Date == DateTime.Now.Date),
                    more = moreBlocks,
                    moreTs = moreBlocks? (object) moreBlocksTs : null
                }
            };
        }

        private static object BlockToJSON(Block block, UInt64 blockHeight, HashList txHashes, byte[] nextBlockHash, UInt64 serializedBlockSize)
        {
            Header blockHeader = block.Header;
            BigInteger proof;
            BigInteger.TryParse(block.Proof, out proof);
            return new
            {
                hash = Binary.ByteArrayToHexString(block.Hash),
                size = serializedBlockSize,
                height = blockHeight,
                version = blockHeader.Version,
                merkleroot = Binary.ByteArrayToHexString(block.MerkleRoot),
                tx = BlockTxsToJSON(txHashes),
                time = blockHeader.Timestamp,
                nonce = blockHeader.Nonce,
                bits = Utils.EncodeInBase16(blockHeader.Bits),
                difficulty = Utils.BitsToDifficulty(blockHeader.Bits), //TODO Use bitprim API when implemented
                chainwork = (proof * 2).ToString("X64"), //TODO Does not match Blockdozer value; check how bitpay calculates it
                previousblockhash = Binary.ByteArrayToHexString(blockHeader.PreviousBlockHash),
                nextblockhash = Binary.ByteArrayToHexString(nextBlockHash),
                reward = block.GetBlockReward(blockHeight) / 100000000,
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