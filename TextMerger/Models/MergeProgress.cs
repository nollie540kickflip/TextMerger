namespace TextMerger.Models;

/// <summary>
/// 結合処理の進捗情報
/// </summary>
public sealed record MergeProgress(
    int ProcessedFiles,
    int TotalFiles,
    string CurrentFileName,
    long WrittenLines);
