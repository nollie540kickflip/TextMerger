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
