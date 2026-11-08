using TextMerger.Models;

namespace TextMerger.Services;

/// <summary>
/// 指定フォルダ内の対象ファイルを検索するサービスのインターフェース
/// </summary>
public interface IFileScanner
{
    IReadOnlyList<FileItem> ScanFiles(
        string directoryPath,
        string searchPatterns,
        bool includeSubdirectories,
        string? excludeFilePath = null);

    FileScanResult ScanFilesWithWarnings(
        string directoryPath,
        string searchPatterns,
        bool includeSubdirectories,
        string? excludeFilePath = null);
}
