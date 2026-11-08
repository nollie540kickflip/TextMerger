using TextMerger.Models;
using TextMerger.Services;

namespace TextMerger.Tests.Services;

public sealed class FileScannerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileScanner _fileScanner = new();

    public FileScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FileScannerTests_{Guid.NewGuid():N}");
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
    public void ScanFiles_FiltersByMultiplePatternsAndSubdirectoryOption()
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
    public void ScanFilesWithWarnings_ReturnsWarningWhenDirectoryDoesNotExist()
    {
        string nonExistent = Path.Combine(_tempDir, "does_not_exist");
        FileScanResult scanResult = _fileScanner.ScanFilesWithWarnings(nonExistent, "*.txt", includeSubdirectories: false);

        Assert.Empty(scanResult.Files);
        Assert.NotEmpty(scanResult.Warnings);
    }
}
