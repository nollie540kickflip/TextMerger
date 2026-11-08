using TextMerger.Models;

namespace TextMerger.Services;

/// <summary>
/// 指定フォルダ内の対象ファイルを検索するサービスのインターフェース
/// </summary>
public interface IFileScanner
{
    /// <summary>
    /// 指定されたディレクトリからパターンに一致するファイル一覧を取得します。
    /// パターンはセミコロン (;) またはカンマ (,) 区切りで複数指定可能です（例: "*.txt;*.csv"）。
    /// </summary>
    IReadOnlyList<FileItem> ScanFiles(
        string directoryPath,
        string searchPatterns,
        bool includeSubdirectories,
        string? excludeFilePath = null);

    /// <summary>
    /// 指定されたディレクトリからファイル一覧を取得し、アクセス権限エラーや不正パターンの警告メッセージもあわせて返します。
    /// </summary>
    FileScanResult ScanFilesWithWarnings(
        string directoryPath,
        string searchPatterns,
        bool includeSubdirectories,
        string? excludeFilePath = null);
}
