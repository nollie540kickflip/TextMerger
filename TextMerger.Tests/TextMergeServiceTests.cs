using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;
using TextMerger.Models;
using TextMerger.Services;
using TextMerger.UI;

namespace TextMerger.Tests;

public sealed class TextMergeServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TextMergeService _mergeService = new();
    private readonly FileScanner _fileScanner = new();

    public TextMergeServiceTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), $"TextMergerTests_{Guid.NewGuid():N}");
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
    public async Task MergeFilesAsync_SkipSingleLineHeader_KeepsOnlyFirstFileHeader()
    {
        string file1 = Path.Combine(_tempDir, "part1.csv");
        string file2 = Path.Combine(_tempDir, "part2.csv");
        string file3 = Path.Combine(_tempDir, "part3.csv");
        string output = Path.Combine(_tempDir, "merged.csv");

        await File.WriteAllLinesAsync(file1, ["ID,Name,Score", "1,Alice,85", "2,Bob,90"]);
        await File.WriteAllLinesAsync(file2, ["ID,Name,Score", "3,Charlie,78", "4,Diana,92"]);
        await File.WriteAllLinesAsync(file3, ["ID,Name,Score", "5,Eve,88"]);

        var options = new MergeOptions
        {
            OutputFilePath = output,
            InputEncoding = EncodingType.Utf8NoBom,
            OutputEncoding = EncodingType.Utf8NoBom,
            SkipHeaderOnSubsequentFiles = true,
            HeaderLineCount = 1
        };

        MergeResult result = await _mergeService.MergeFilesAsync([file1, file2, file3], options);

        Assert.Equal(3, result.MergedFileCount);
        Assert.Equal(6, result.TotalLinesWritten);
        Assert.Equal(2, result.TotalHeaderLinesSkipped);

        string[] mergedLines = await File.ReadAllLinesAsync(output, Encoding.UTF8);
        Assert.Equal(
            [
                "ID,Name,Score",
                "1,Alice,85",
                "2,Bob,90",
                "3,Charlie,78",
                "4,Diana,92",
                "5,Eve,88"
            ],
            mergedLines);
    }

    [Fact]
    public async Task MergeFilesAsync_SkipMultiLineHeader_SkipsSpecifiedLinesOnSubsequentFiles()
    {
        string file1 = Path.Combine(_tempDir, "report1.txt");
        string file2 = Path.Combine(_tempDir, "report2.txt");
        string output = Path.Combine(_tempDir, "merged_report.txt");

        await File.WriteAllLinesAsync(file1, ["# Report Header", "ColA\tColB", "A1\tB1"]);
        await File.WriteAllLinesAsync(file2, ["# Report Header", "ColA\tColB", "A2\tB2", "A3\tB3"]);

        var options = new MergeOptions
        {
            OutputFilePath = output,
            SkipHeaderOnSubsequentFiles = true,
            HeaderLineCount = 2
        };

        MergeResult result = await _mergeService.MergeFilesAsync([file1, file2], options);

        Assert.Equal(2, result.MergedFileCount);
        Assert.Equal(5, result.TotalLinesWritten);
        Assert.Equal(2, result.TotalHeaderLinesSkipped);

        string[] mergedLines = await File.ReadAllLinesAsync(output, Encoding.UTF8);
        Assert.Equal(
            [
                "# Report Header",
                "ColA\tColB",
                "A1\tB1",
                "A2\tB2",
                "A3\tB3"
            ],
            mergedLines);
    }

    [Fact]
    public async Task MergeFilesAsync_WhenSkipHeaderDisabled_WritesAllLines()
    {
        string file1 = Path.Combine(_tempDir, "a.txt");
        string file2 = Path.Combine(_tempDir, "b.txt");
        string output = Path.Combine(_tempDir, "out.txt");

        await File.WriteAllLinesAsync(file1, ["Header", "Row1"]);
        await File.WriteAllLinesAsync(file2, ["Header", "Row2"]);

        var options = new MergeOptions
        {
            OutputFilePath = output,
            SkipHeaderOnSubsequentFiles = false,
            HeaderLineCount = 1
        };

        MergeResult result = await _mergeService.MergeFilesAsync([file1, file2], options);

        Assert.Equal(4, result.TotalLinesWritten);
        Assert.Equal(0, result.TotalHeaderLinesSkipped);

        string[] mergedLines = await File.ReadAllLinesAsync(output, Encoding.UTF8);
        Assert.Equal(["Header", "Row1", "Header", "Row2"], mergedLines);
    }

    [Fact]
    public async Task MergeFilesAsync_ShiftJisInputToUtf8WithBomOutput_ConvertsJapaneseCorrectly()
    {
        Encoding sjis = EncodingOption.GetEncoding(EncodingType.ShiftJis);
        string file1 = Path.Combine(_tempDir, "sjis1.csv");
        string file2 = Path.Combine(_tempDir, "sjis2.csv");
        string output = Path.Combine(_tempDir, "utf8bom.csv");

        await File.WriteAllLinesAsync(file1, ["社員番号,氏名,部署", "001,山田太郎,開発部"], sjis);
        await File.WriteAllLinesAsync(file2, ["社員番号,氏名,部署", "002,佐藤花子,営業部"], sjis);

        var options = new MergeOptions
        {
            OutputFilePath = output,
            InputEncoding = EncodingType.ShiftJis,
            OutputEncoding = EncodingType.Utf8WithBom,
            SkipHeaderOnSubsequentFiles = true,
            HeaderLineCount = 1
        };

        await _mergeService.MergeFilesAsync([file1, file2], options);

        byte[] rawBytes = await File.ReadAllBytesAsync(output);
        // UTF-8 BOM (EF BB BF) が先頭に付与されていることを確認
        Assert.True(rawBytes.Length >= 3);
        Assert.Equal(0xEF, rawBytes[0]);
        Assert.Equal(0xBB, rawBytes[1]);
        Assert.Equal(0xBF, rawBytes[2]);

        string[] lines = await File.ReadAllLinesAsync(output, Encoding.UTF8);
        Assert.Equal(
            [
                "社員番号,氏名,部署",
                "001,山田太郎,開発部",
                "002,佐藤花子,営業部"
            ],
            lines);
    }

    [Fact]
    public async Task MergeFilesAsync_EucJpInputToShiftJisOutput_ConvertsJapaneseCorrectly()
    {
        Encoding eucJp = EncodingOption.GetEncoding(EncodingType.EucJp);
        Encoding sjis = EncodingOption.GetEncoding(EncodingType.ShiftJis);

        string file1 = Path.Combine(_tempDir, "euc1.txt");
        string file2 = Path.Combine(_tempDir, "euc2.txt");
        string output = Path.Combine(_tempDir, "sjis_out.txt");

        await File.WriteAllLinesAsync(file1, ["見出し行", "あいうえお"], eucJp);
        await File.WriteAllLinesAsync(file2, ["見出し行", "かきくけこ"], eucJp);

        var options = new MergeOptions
        {
            OutputFilePath = output,
            InputEncoding = EncodingType.EucJp,
            OutputEncoding = EncodingType.ShiftJis,
            SkipHeaderOnSubsequentFiles = true,
            HeaderLineCount = 1
        };

        await _mergeService.MergeFilesAsync([file1, file2], options);

        string[] lines = await File.ReadAllLinesAsync(output, sjis);
        Assert.Equal(["見出し行", "あいうえお", "かきくけこ"], lines);
    }

    [Fact]
    public void FileScanner_ScanFiles_FiltersByMultiplePatternsAndSubdirectoryOption()
    {
        string subDir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subDir);

        string txt1 = Path.Combine(_tempDir, "01.txt");
        string csv1 = Path.Combine(_tempDir, "02.csv");
        string log1 = Path.Combine(_tempDir, "03.log");
        string subTxt = Path.Combine(subDir, "04.txt");
        string output = Path.Combine(_tempDir, "merged.txt");

        File.WriteAllText(txt1, "a");
        File.WriteAllText(csv1, "b");
        File.WriteAllText(log1, "c");
        File.WriteAllText(subTxt, "d");
        File.WriteAllText(output, "existing output");

        // サブフォルダなし・出力ファイル除外
        IReadOnlyList<FileItem> topOnly = _fileScanner.ScanFiles(
            _tempDir,
            "*.txt;*.csv",
            includeSubdirectories: false,
            excludeFilePath: output);

        Assert.Equal(["01.txt", "02.csv"], topOnly.Select(f => f.RelativePath).ToArray());

        // サブフォルダあり
        IReadOnlyList<FileItem> withSub = _fileScanner.ScanFiles(
            _tempDir,
            "*.txt;*.csv",
            includeSubdirectories: true,
            excludeFilePath: output);

        Assert.Equal(3, withSub.Count);
        Assert.Contains(withSub, f => f.RelativePath == Path.Combine("sub", "04.txt"));
    }

    [Fact]
    public void MainWindow_InitializeWithAnsiDriver_ConstructsAndLaysOutWithoutError()
    {
        using IApplication app = Application.Create().Init(DriverRegistry.Names.ANSI);
        using var window = new MainWindow(app);
        window.BeginInit();
        window.EndInit();
        window.Layout();

        Assert.Equal(4, window.SubViews.Count);
        Assert.Equal(MainWindow.DefaultThemeName, ThemeManager.GetCurrentThemeName());
        Assert.Equal(LineStyle.Rounded, window.BorderStyle);

        foreach (string theme in MainWindow.AvailableThemes)
        {
            window.ApplyTheme(theme);
            Assert.Equal(theme, ThemeManager.GetCurrentThemeName());
            Assert.Equal(LineStyle.Rounded, window.BorderStyle);
            Assert.All(window.SubViews.OfType<FrameView>(), frame => Assert.Equal(LineStyle.Rounded, frame.BorderStyle));
        }
    }

    [Fact]
    public async Task MergeFilesAsync_DefaultAutoEncoding_PreservesOriginalShiftJisEncoding()
    {
        Encoding sjis = EncodingOption.GetEncoding(EncodingType.ShiftJis);
        string file1 = Path.Combine(_tempDir, "orig_sjis1.csv");
        string file2 = Path.Combine(_tempDir, "orig_sjis2.csv");
        string output = Path.Combine(_tempDir, "orig_sjis_merged.csv");

        await File.WriteAllLinesAsync(file1, ["社員番号,氏名,部署", "101,鈴木一郎,総務部"], sjis);
        await File.WriteAllLinesAsync(file2, ["社員番号,氏名,部署", "102,高橋花子,人事部"], sjis);

        // InputEncoding / OutputEncoding を指定せずデフォルト (Auto) のまま実行
        var options = new MergeOptions
        {
            OutputFilePath = output,
            SkipHeaderOnSubsequentFiles = true,
            HeaderLineCount = 1
        };

        MergeResult result = await _mergeService.MergeFilesAsync([file1, file2], options);

        Assert.Equal(EncodingType.ShiftJis, result.EffectiveOutputEncoding);
        Assert.Equal(EncodingType.ShiftJis, EncodingDetector.DetectFileEncoding(output));

        string[] mergedLines = await File.ReadAllLinesAsync(output, sjis);
        Assert.Equal(
            [
                "社員番号,氏名,部署",
                "101,鈴木一郎,総務部",
                "102,高橋花子,人事部"
            ],
            mergedLines);
    }

    [Fact]
    public async Task MergeFilesAsync_DefaultAutoEncoding_PreservesUtf8WithBom()
    {
        Encoding utf8Bom = EncodingOption.GetEncoding(EncodingType.Utf8WithBom);
        string file1 = Path.Combine(_tempDir, "bom1.csv");
        string file2 = Path.Combine(_tempDir, "bom2.csv");
        string output = Path.Combine(_tempDir, "bom_merged.csv");

        await File.WriteAllLinesAsync(file1, ["項目,値", "A,テスト1"], utf8Bom);
        await File.WriteAllLinesAsync(file2, ["項目,値", "B,テスト2"], utf8Bom);

        var options = new MergeOptions
        {
            OutputFilePath = output
        };

        MergeResult result = await _mergeService.MergeFilesAsync([file1, file2], options);

        Assert.Equal(EncodingType.Utf8WithBom, result.EffectiveOutputEncoding);
        Assert.Equal(EncodingType.Utf8WithBom, EncodingDetector.DetectFileEncoding(output));
    }

    [Fact]
    public async Task MergeFilesAsync_DefaultAutoEncoding_MixedInputEncodings_DecodesEachAndOutputsFirstFileEncoding()
    {
        Encoding utf8NoBom = EncodingOption.GetEncoding(EncodingType.Utf8NoBom);
        Encoding sjis = EncodingOption.GetEncoding(EncodingType.ShiftJis);
        Encoding eucJp = EncodingOption.GetEncoding(EncodingType.EucJp);

        string file1 = Path.Combine(_tempDir, "01_utf8.csv");
        string file2 = Path.Combine(_tempDir, "02_sjis.csv");
        string file3 = Path.Combine(_tempDir, "03_euc.csv");
        string output = Path.Combine(_tempDir, "mixed_merged.csv");

        await File.WriteAllLinesAsync(file1, ["ID,名称", "1,東京本社(UTF-8)"], utf8NoBom);
        await File.WriteAllLinesAsync(file2, ["ID,名称", "2,大阪支社(Shift_JIS)"], sjis);
        await File.WriteAllLinesAsync(file3, ["ID,名称", "3,名古屋営業所(EUC-JP)"], eucJp);

        var options = new MergeOptions
        {
            OutputFilePath = output,
            SkipHeaderOnSubsequentFiles = true,
            HeaderLineCount = 1
        };

        MergeResult result = await _mergeService.MergeFilesAsync([file1, file2, file3], options);

        Assert.Equal(EncodingType.Utf8NoBom, result.EffectiveOutputEncoding);
        string[] lines = await File.ReadAllLinesAsync(output, utf8NoBom);
        Assert.Equal(
            [
                "ID,名称",
                "1,東京本社(UTF-8)",
                "2,大阪支社(Shift_JIS)",
                "3,名古屋営業所(EUC-JP)"
            ],
            lines);
    }

    [Fact]
    public async Task MergeFilesAsync_WithPreDetectedFileItems_DoesNotReInvokeEncodingDetector()
    {
        string file1 = Path.Combine(_tempDir, "pre1.csv");
        string file2 = Path.Combine(_tempDir, "pre2.csv");
        string output = Path.Combine(_tempDir, "pre_merged.csv");

        await File.WriteAllLinesAsync(file1, ["H1,H2", "1,A"]);
        await File.WriteAllLinesAsync(file2, ["H1,H2", "2,B"]);

        var countingDetector = new CountingEncodingDetector();
        var service = new TextMergeService(countingDetector);

        var items = new List<FileItem>
        {
            new()
            {
                FullPath = file1,
                RelativePath = "pre1.csv",
                Length = 10,
                DetectedEncoding = EncodingType.Utf8NoBom
            },
            new()
            {
                FullPath = file2,
                RelativePath = "pre2.csv",
                Length = 10,
                DetectedEncoding = EncodingType.Utf8NoBom
            }
        };

        MergeResult result = await service.MergeFilesAsync(items, new MergeOptions { OutputFilePath = output });

        Assert.Equal(2, result.MergedFileCount);
        Assert.Equal(0, countingDetector.CallCount);
    }

    [Fact]
    public async Task MergeFilesAsync_WhenCancelled_ThrowsOperationCanceledAndCleansUpTempFiles()
    {
        string file1 = Path.Combine(_tempDir, "cancel1.txt");
        string output = Path.Combine(_tempDir, "cancel_out.txt");
        await File.WriteAllLinesAsync(file1, ["Line1", "Line2"]);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _mergeService.MergeFilesAsync([file1], new MergeOptions { OutputFilePath = output }, cancellationToken: cts.Token));

        Assert.False(File.Exists(output));
        Assert.Empty(Directory.GetFiles(_tempDir, "*.tmp.*"));
    }

    [Fact]
    public void FileScanner_ScanFilesWithWarnings_ReturnsWarningWhenDirectoryDoesNotExist()
    {
        string nonExistent = Path.Combine(_tempDir, "does_not_exist");
        FileScanResult scanResult = _fileScanner.ScanFilesWithWarnings(nonExistent, "*.txt", includeSubdirectories: false);

        Assert.Empty(scanResult.Files);
        Assert.NotEmpty(scanResult.Warnings);
    }

    [Fact]
    public async Task MergeFilesAsync_LargeLinesSpanningMultipleChunks_StreamsWithoutDataLoss()
    {
        // 64KB (65536 chars) のチャンクサイズを大きく超える1行データ (150,000文字)
        string giantHeader = "HEADER_" + new string('H', 150_000);
        string giantRow1 = "ROW1_" + new string('A', 150_000);
        string giantRow2 = "ROW2_" + new string('B', 150_000);

        string file1 = Path.Combine(_tempDir, "giant1.txt");
        string file2 = Path.Combine(_tempDir, "giant2.txt");
        string output = Path.Combine(_tempDir, "giant_merged.txt");

        await File.WriteAllLinesAsync(file1, [giantHeader, giantRow1]);
        await File.WriteAllLinesAsync(file2, [giantHeader, giantRow2]);

        var options = new MergeOptions
        {
            OutputFilePath = output,
            SkipHeaderOnSubsequentFiles = true,
            HeaderLineCount = 1
        };

        MergeResult result = await _mergeService.MergeFilesAsync([file1, file2], options);

        Assert.Equal(2, result.MergedFileCount);
        Assert.Equal(3, result.TotalLinesWritten);
        Assert.Equal(1, result.TotalHeaderLinesSkipped);

        string[] mergedLines = await File.ReadAllLinesAsync(output);
        Assert.Equal(3, mergedLines.Length);
        Assert.Equal(giantHeader, mergedLines[0]);
        Assert.Equal(giantRow1, mergedLines[1]);
        Assert.Equal(giantRow2, mergedLines[2]);
    }

    [Fact]
    public async Task MergeFilesAsync_FilesWithoutTrailingNewline_AppendsNewlineBetweenFiles()
    {
        string file1 = Path.Combine(_tempDir, "no_newline1.csv");
        string file2 = Path.Combine(_tempDir, "no_newline2.csv");
        string output = Path.Combine(_tempDir, "no_newline_merged.csv");

        // 末尾に改行コード (\n) がないファイルを作成
        await File.WriteAllTextAsync(file1, "ID,Name\n1,Alice");
        await File.WriteAllTextAsync(file2, "ID,Name\n2,Bob");

        var options = new MergeOptions
        {
            OutputFilePath = output,
            SkipHeaderOnSubsequentFiles = true,
            HeaderLineCount = 1
        };

        MergeResult result = await _mergeService.MergeFilesAsync([file1, file2], options);

        Assert.Equal(3, result.TotalLinesWritten);
        string[] lines = await File.ReadAllLinesAsync(output);
        Assert.Equal(["ID,Name", "1,Alice", "2,Bob"], lines);
    }

    [Fact]
    public void FileItem_FormatBytes_FormatsGigabytesCorrectly()
    {
        long onePointFiveGb = (long)(1.5 * 1024 * 1024 * 1024);
        Assert.Equal("1.50 GB", FileItem.FormatBytes(onePointFiveGb));
    }

    private sealed class CountingEncodingDetector : IEncodingDetector
    {
        public int CallCount { get; private set; }

        public EncodingType DetectFileEncoding(string filePath)
        {
            CallCount++;
            return EncodingDetector.DetectFileEncoding(filePath);
        }
    }
}
