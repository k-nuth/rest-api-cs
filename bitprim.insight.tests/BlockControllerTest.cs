using Bitprim;
using bitprim.insight.Controllers;
using bitprim.insight.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace bitprim.insight.tests
{
    public class BlockControllerTest
    {
        [Fact]
        public async Task GetBlocksByDateShouldReturn5Blocks()
        {
            var configMock = new Mock<IOptions<NodeConfig>>();
            var chainMock = new Mock<IChain>();
            var memoryCacheMock = new Mock<IMemoryCache>();
            var poolsInfoMock = new Mock<IPoolsInfo>();
            configMock.Setup(x => x.Value).Returns(new NodeConfig());
            var controller = new BlockController(configMock.Object, chainMock.Object, memoryCacheMock.Object, poolsInfoMock.Object);
            const int BLOCK_COUNT = 5;
            chainMock.Setup(x => x.IsStale).Returns(false);
            chainMock.Setup(x => x.FetchLastHeightAsync()).Returns(Task.FromResult(new ApiCallResult<UInt64> { ErrorCode = ErrorCode.Success, Result = 10 }));
            var result = await controller.GetBlocksByDate(BLOCK_COUNT, "");
            GetBlocksByDateResponse blocks = Assert.IsType<GetBlocksByDateResponse>(result) as GetBlocksByDateResponse;
            Assert.Equal(BLOCK_COUNT, blocks.length);
        }
    }
}