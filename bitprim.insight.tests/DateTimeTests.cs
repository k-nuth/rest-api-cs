using System;
using Xunit;

namespace bitprim.insight.tests
{
    public class DateTimeTests
    {
        [Fact]
        public void TestUtc()
        {
            var date = new DateTime(2018,09,06,0,0,0,DateTimeKind.Utc);
            var blockDateTimestamp = ((DateTimeOffset) date).ToUnixTimeSeconds();
            Assert.Equal(1536192000, blockDateTimestamp);
        }

        [Fact]
        public void TestStringToDate()
        {
            var stringDate = "2018-09-06";

            DateTime.TryParseExact(stringDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal |
                System.Globalization.DateTimeStyles.AssumeUniversal, out var blockDateToSearch);

            Assert.Equal(blockDateToSearch, new DateTime(2018,09,06,0,0,0,DateTimeKind.Utc));
        }
    }
}