using TextMerger.Models;

namespace TextMerger.Tests.Models;

public sealed class FileItemTests
{
    [Fact]
    public void FormatBytes_FormatsBytesKilobytesMegabytesAndGigabytesCorrectly()
    {
        Assert.Equal("512 B", FileItem.FormatBytes(512));
        Assert.Equal("2.0 KB", FileItem.FormatBytes(2048));
        Assert.Equal("5.00 MB", FileItem.FormatBytes(5L * 1024 * 1024));

        long onePointFiveGb = (long)(1.5 * 1024 * 1024 * 1024);
        Assert.Equal("1.50 GB", FileItem.FormatBytes(onePointFiveGb));

        var item = new FileItem
        {
            FullPath = "/tmp/test.csv",
            RelativePath = "test.csv",
            Length = 2048,
            DetectedEncoding = EncodingType.ShiftJis
        };

        Assert.Equal("2.0 KB", item.FormattedSize);
        Assert.Contains("test.csv", item.ToString());
        Assert.Contains("Shift_JIS", item.ToString());
    }
}
