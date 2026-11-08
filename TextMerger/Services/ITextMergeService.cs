using TextMerger.Models;

namespace TextMerger.Services;

/// <summary>
/// 複数のテキストファイルを結合するサービスのインターフェース
/// </summary>
public interface ITextMergeService
{
    Task<MergeResult> MergeFilesAsync(
        IReadOnlyList<string> inputFiles,
        MergeOptions options,
        IProgress<MergeProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<MergeResult> MergeFilesAsync(
        IReadOnlyList<FileItem> inputItems,
        MergeOptions options,
        IProgress<MergeProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
