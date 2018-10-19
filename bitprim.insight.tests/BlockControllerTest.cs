using Bitprim;
using bitprim.insight.Controllers;
using bitprim.insight.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace bitprim.insight.tests
{
    public class BlockControllerTest
    {
        [Fact]
        public async Task GetBlocksByDateShouldReturn5Blocks()
        {
            const int LAST_HEIGHT = 5;

            byte[] blockHash = Binary.HexStringToByteArray("000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f");
            byte[] txHash = Binary.HexStringToByteArray("d4c7289fdc74dc5043372f343863c635799b40355f7cae46c915eb5fc07598c7");

            var transactionList = new Mock<INativeList<byte[]>>();
            transactionList.Setup(x => x[0]).Returns(txHash);

            var transactionMock = new Mock<ITransaction>();

            var configMock = new Mock<IOptions<NodeConfig>>();
            configMock.Setup(x => x.Value).Returns(new NodeConfig());

            var headerMock = new Mock<IHeader>();
            headerMock.Setup(x => x.Timestamp).Returns((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            headerMock.Setup(x => x.Hash).Returns(blockHash);

            var blockMock = new Mock<IBlock>();
            blockMock.Setup(x => x.Header).Returns(headerMock.Object);
            blockMock.Setup(x => x.Hash).Returns(blockHash);

            var chainMock = new Mock<IChain>();
            chainMock.Setup(x => x.IsStale).Returns(false);
            
            chainMock.Setup(x => x.FetchLastHeightAsync()).Returns(Task.FromResult(new ApiCallResult<UInt64>
            {
                ErrorCode = ErrorCode.Success, 
                Result = LAST_HEIGHT
            }));
            
            chainMock.Setup(x => x.FetchBlockByHeightAsync(It.IsAny<ulong>())).Returns(Task.FromResult(new DisposableApiCallResult<GetBlockDataResult<IBlock>>
            {
                ErrorCode = ErrorCode.Success, 
                Result = new GetBlockDataResult<IBlock>
                {
                    BlockData = blockMock.Object
                }
            }));

            chainMock.Setup(x => x.FetchBlockHeaderByHashTxSizesAsync(blockHash)).Returns(Task.FromResult(new DisposableApiCallResult<GetBlockHeaderByHashTxSizeResult>
            {
                ErrorCode = ErrorCode.Success,
                Result = new GetBlockHeaderByHashTxSizeResult
                {
                    Header = new GetBlockDataResult<IHeader>
                    {
                        BlockData = headerMock.Object
                    },
                    TransactionHashes = transactionList.Object
                }
            }));


            chainMock.Setup(x => x.FetchTransactionAsync(txHash, true))
                .Returns(Task.FromResult(new DisposableApiCallResult<GetTxDataResult>
                {
                    ErrorCode = ErrorCode.Success,
                    Result = new GetTxDataResult
                    {
                        Tx = transactionMock.Object
                    }
                }));

            
            chainMock.Setup(x => x.FetchBlockHeaderByHeightAsync(It.IsAny<ulong>()))
                .Returns(Task.FromResult(new DisposableApiCallResult<GetBlockDataResult<IHeader>>
                {
                    ErrorCode = ErrorCode.Success,
                    Result = new GetBlockDataResult<IHeader>()
                    {
                        BlockData = headerMock.Object
                    }
                }));

            
            
            

            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var poolsInfoMock = new Mock<IPoolsInfo>();
            poolsInfoMock.Setup(x => x.GetPoolInfo(transactionMock.Object)).Returns(PoolInfo.Empty);

            var controller = new BlockController(configMock.Object, chainMock.Object, memoryCache, poolsInfoMock.Object);

            const int BLOCK_COUNT = 5;
            var result = await controller.GetBlocksByDate(BLOCK_COUNT, "");

            var json = Assert.IsType<JsonResult>(result);
            var blocks = Assert.IsType<GetBlocksByDateResponse>(json.Value);

            Assert.Equal(BLOCK_COUNT, blocks.length);
        }
    }
}