using TextMerger.Models;

namespace TextMerger.Services;

/// <summary>
/// 複数のテキストファイルを結合するサービスのインターフェース
/// </summary>
public interface ITextMergeService
{
    /// <summary>
    /// 指定されたファイルパス一覧を順番に結合し、1つのファイルへ出力します。
    /// </summary>
    Task<MergeResult> MergeFilesAsync(
        IReadOnlyList<string> inputFiles,
        MergeOptions options,
        IProgress<MergeProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// スキャン済みの <see cref="FileItem"/> 一覧（判別済み文字コード情報を含む）を順番に結合し、1つのファイルへ出力します。
    /// </summary>
    Task<MergeResult> MergeFilesAsync(
        IReadOnlyList<FileItem> inputItems,
        MergeOptions options,
        IProgress<MergeProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
