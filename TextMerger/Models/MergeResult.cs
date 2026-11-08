namespace TextMerger.Models;

/// <summary>
/// 結合処理の完了結果
/// </summary>
public sealed record MergeResult(
    int MergedFileCount,
    long TotalLinesWritten,
    long TotalHeaderLinesSkipped,
    string OutputFilePath,
    long OutputFileSize,
    EncodingType EffectiveOutputEncoding);
