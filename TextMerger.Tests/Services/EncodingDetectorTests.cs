using System.Text;
using TextMerger.Models;
using TextMerger.Services;

namespace TextMerger.Tests.Services;

public sealed class EncodingDetectorTests : IDisposable
{
    private readonly string _tempDir;

    public EncodingDetectorTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), $"EncodingDetectorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // テスト後の一時フォルダ削除失敗は無視
            }
        }
    }

    [Fact]
    public void DetectFileEncoding_WhenFileDoesNotExistOrEmpty_ReturnsUtf8NoBom()
    {
        Assert.Equal(EncodingType.Utf8NoBom, EncodingDetector.DetectFileEncoding(""));
        Assert.Equal(EncodingType.Utf8NoBom, EncodingDetector.DetectFileEncoding(Path.Combine(_tempDir, "missing.txt")));

        string emptyFile = Path.Combine(_tempDir, "empty.txt");
        File.WriteAllBytes(emptyFile, []);
        Assert.Equal(EncodingType.Utf8NoBom, EncodingDetector.DetectFileEncoding(emptyFile));
        Assert.Equal(EncodingType.Utf8NoBom, EncodingDetector.DetectFromBytes([]));
    }

    [Fact]
    public void DetectFromBytes_DetectsBomsAndBomlessUtf16Correctly()
    {
        // UTF-8 BOM
        Assert.Equal(EncodingType.Utf8WithBom, EncodingDetector.DetectFromBytes([0xEF, 0xBB, 0xBF, 0x41]));

        // UTF-16 LE BOM
        Assert.Equal(EncodingType.Utf16Le, EncodingDetector.DetectFromBytes([0xFF, 0xFE, 0x41, 0x00]));

        // UTF-16 BE BOM
        Assert.Equal(EncodingType.Utf16Be, EncodingDetector.DetectFromBytes([0xFE, 0xFF, 0x00, 0x41]));

        // BOMなし UTF-16 LE ("ABCD" -> 41 00 42 00 43 00 44 00)
        Assert.Equal(EncodingType.Utf16Le, EncodingDetector.DetectFromBytes([0x41, 0x00, 0x42, 0x00, 0x43, 0x00, 0x44, 0x00]));

        // BOMなし UTF-16 BE ("ABCD" -> 00 41 00 42 00 43 00 44)
        Assert.Equal(EncodingType.Utf16Be, EncodingDetector.DetectFromBytes([0x00, 0x41, 0x00, 0x42, 0x00, 0x43, 0x00, 0x44]));
    }

    [Fact]
    public void DetectFromBytes_DetectsJapaneseEncodingsAndSpecialSequences()
    {
        // ASCIIのみ
        Assert.Equal(EncodingType.Utf8NoBom, EncodingDetector.DetectFromBytes("Hello, World! 12345"u8));

        // UTF-8 日本語 + 絵文字 (4バイトUTF-8)
        byte[] utf8WithEmoji = Encoding.UTF8.GetBytes("こんにちは😀テスト");
        Assert.Equal(EncodingType.Utf8NoBom, EncodingDetector.DetectFromBytes(utf8WithEmoji));

        // Shift_JIS 半角カナ + 漢字
        Encoding sjis = EncodingOption.GetEncoding(EncodingType.ShiftJis);
        byte[] sjisBytes = sjis.GetBytes("ｶﾀｶﾅと漢字のテスト");
        Assert.Equal(EncodingType.ShiftJis, EncodingDetector.DetectFromBytes(sjisBytes));

        // EUC-JP 半角カナ (0x8E + 0xA1..0xDF) + ひらがな
        byte[] eucHalfKana = [0x8E, 0xB1, 0x8E, 0xB2, 0xA4, 0xA2, 0xA4, 0xA4];
        Assert.Equal(EncodingType.EucJp, EncodingDetector.DetectFromBytes(eucHalfKana));

        // EUC-JP 補助漢字 (0x8F + 2バイト) + ひらがな
        byte[] euc3Byte = [0x8F, 0xB0, 0xA1, 0xA4, 0xA2, 0xA4, 0xA4];
        Assert.Equal(EncodingType.EucJp, EncodingDetector.DetectFromBytes(euc3Byte));
    }

    [Fact]
    public void EncodingOption_GetEncodingAndGetShortName_CoversAllTypes()
    {
        foreach (EncodingType type in Enum.GetValues<EncodingType>())
        {
            Encoding enc = EncodingOption.GetEncoding(type);
            Assert.NotNull(enc);

            string shortName = EncodingOption.GetShortName(type);
            Assert.False(string.IsNullOrWhiteSpace(shortName));
        }

        foreach (EncodingOption option in EncodingOption.InputOptions.Concat(EncodingOption.OutputOptions))
        {
            Assert.NotNull(option.ToEncoding());
            Assert.Equal(option.DisplayName, option.ToString());
        }
    }
}
