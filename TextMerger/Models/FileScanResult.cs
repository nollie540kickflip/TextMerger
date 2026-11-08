namespace TextMerger.Models;

/// <summary>
/// ファイルスキャン結果（一致ファイル一覧およびスキップ時の警告メッセージ）
/// </summary>
public sealed record FileScanResult(
    IReadOnlyList<FileItem> Files,
    IReadOnlyList<string> Warnings);
