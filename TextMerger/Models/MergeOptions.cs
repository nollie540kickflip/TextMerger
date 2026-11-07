namespace TextMerger.Models;

/// <summary>
/// ファイル結合処理のオプション設定
/// </summary>
public sealed record MergeOptions
{
    public required string OutputFilePath { get; init; }
    public EncodingType InputEncoding { get; init; } = EncodingType.Auto;
    public EncodingType OutputEncoding { get; init; } = EncodingType.Auto;
    public bool SkipHeaderOnSubsequentFiles { get; init; } = true;
    public int HeaderLineCount { get; init; } = 1;
}

/// <summary>
/// 結合処理の進捗情報
/// </summary>
public sealed record MergeProgress(
    int ProcessedFiles,
    int TotalFiles,
    string CurrentFileName,
    long WrittenLines);

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
