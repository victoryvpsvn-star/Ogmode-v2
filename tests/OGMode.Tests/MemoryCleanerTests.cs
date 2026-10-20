using ogmm.Core;

namespace OGMode.Tests
{
    public class MemoryCleanerTests
    {
        [Fact]
        public void GetInfo_ReturnsSaneNumbers()
        {
            var m = MemoryCleaner.GetInfo();
            Assert.True(m.TotalMiB > 0, "Tổng RAM phải > 0");
            Assert.InRange(m.AvailMiB, 0, m.TotalMiB);
            Assert.InRange(m.UsedPercent, 0, 100);
        }
    }
}
