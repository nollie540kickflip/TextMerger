using System.Text;
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

/// <summary>
/// 複数のテキストファイルを指定された文字コード・ヘッダスキップ設定で結合するサービス
/// </summary>
public sealed class TextMergeService : ITextMergeService
{
    private readonly IEncodingDetector _encodingDetector;

    public TextMergeService(IEncodingDetector? encodingDetector = null)
    {
        _encodingDetector = encodingDetector ?? EncodingDetector.Default;
    }

    public Task<MergeResult> MergeFilesAsync(
        IReadOnlyList<string> inputFiles,
        MergeOptions options,
        IProgress<MergeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputFiles);

        List<FileItem> items = inputFiles
            .Select(path => new FileItem
            {
                FullPath = path,
                RelativePath = Path.GetFileName(path),
                Length = 0L,
                DetectedEncoding = EncodingType.Auto
            })
            .ToList();

        return MergeFilesAsync(items, options, progress, cancellationToken);
    }

    public async Task<MergeResult> MergeFilesAsync(
        IReadOnlyList<FileItem> inputItems,
        MergeOptions options,
        IProgress<MergeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputItems);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.OutputFilePath))
        {
            throw new ArgumentException("出力ファイルパスが指定されていません。", nameof(options));
        }

        if (options.HeaderLineCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "ヘッダ行数は0以上である必要があります。");
        }

        string fullOutputPath = Path.GetFullPath(options.OutputFilePath);
        string? outputDir = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // 入力ファイル一覧に出力先ファイル自身が含まれないよう除外
        List<FileItem> targetItems = inputItems
            .Where(item => !string.Equals(Path.GetFullPath(item.FullPath), fullOutputPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // 出力文字コードの決定（Auto の場合は先頭ファイルの入力文字コードを使用）
        EncodingType effectiveOutputEncodingType = ResolveOutputEncodingType(options, targetItems);
        Encoding outputEncoding = EncodingOption.GetEncoding(effectiveOutputEncodingType);

        long totalLinesWritten = 0;
        long totalHeaderLinesSkipped = 0;
        int processedFiles = 0;

        // 一時ファイルへ書き出してから最後に配置することで、途中キャンセルや例外時の破損を防ぐ
        string tempOutputPath = $"{fullOutputPath}.tmp.{Guid.NewGuid():N}";

        try
        {
            await using (var outputStream = new FileStream(
                tempOutputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 65536,
                useAsync: true))
            await using (var writer = new StreamWriter(outputStream, outputEncoding, bufferSize: 65536))
            {
                for (int fileIndex = 0; fileIndex < targetItems.Count; fileIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    FileItem item = targetItems[fileIndex];
                    string filePath = item.FullPath;
                    string fileName = Path.GetFileName(filePath);

                    progress?.Report(new MergeProgress(
                        ProcessedFiles: processedFiles,
                        TotalFiles: targetItems.Count,
                        CurrentFileName: fileName,
                        WrittenLines: totalLinesWritten));

                    EncodingType fileInputEncodingType = ResolveFileInputEncodingType(options, item);
                    Encoding fileInputEncoding = EncodingOption.GetEncoding(fileInputEncodingType);

                    await using var inputStream = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite,
                        bufferSize: 65536,
                        useAsync: true);

                    // UTF-8やUTF-16のBOM付きファイルも適切に扱えるよう detectEncodingFromByteOrderMarks: true を指定
                    using var reader = new StreamReader(
                        inputStream,
                        fileInputEncoding,
                        detectEncodingFromByteOrderMarks: true,
                        bufferSize: 65536);

                    int headersToSkip = (fileIndex > 0 && options.SkipHeaderOnSubsequentFiles)
                        ? options.HeaderLineCount
                        : 0;

                    int currentLineIndex = 0;
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        if (line is null)
                        {
                            break;
                        }

                        if (currentLineIndex < headersToSkip)
                        {
                            currentLineIndex++;
                            totalHeaderLinesSkipped++;
                            continue;
                        }

                        await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
                        currentLineIndex++;
                        totalLinesWritten++;
                    }

                    processedFiles++;
                    progress?.Report(new MergeProgress(
                        ProcessedFiles: processedFiles,
                        TotalFiles: targetItems.Count,
                        CurrentFileName: fileName,
                        WrittenLines: totalLinesWritten));
                }

                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempOutputPath, fullOutputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempOutputPath))
            {
                try
                {
                    File.Delete(tempOutputPath);
                }
                catch
                {
                    // 一時ファイル削除失敗は無視
                }
            }
        }

        var outputFileInfo = new FileInfo(fullOutputPath);
        return new MergeResult(
            MergedFileCount: processedFiles,
            TotalLinesWritten: totalLinesWritten,
            TotalHeaderLinesSkipped: totalHeaderLinesSkipped,
            OutputFilePath: fullOutputPath,
            OutputFileSize: outputFileInfo.Exists ? outputFileInfo.Length : 0L,
            EffectiveOutputEncoding: effectiveOutputEncodingType);
    }

    private EncodingType ResolveFileInputEncodingType(MergeOptions options, FileItem item)
    {
        if (options.InputEncoding != EncodingType.Auto)
        {
            return options.InputEncoding;
        }

        // FileScanner で既に判別済みの場合は再読み込みせずに再利用
        if (item.DetectedEncoding != EncodingType.Auto)
        {
            return item.DetectedEncoding;
        }

        return _encodingDetector.DetectFileEncoding(item.FullPath);
    }

    private EncodingType ResolveOutputEncodingType(MergeOptions options, IReadOnlyList<FileItem> targetItems)
    {
        if (options.OutputEncoding != EncodingType.Auto)
        {
            return options.OutputEncoding;
        }

        if (options.InputEncoding != EncodingType.Auto)
        {
            return options.InputEncoding;
        }

        if (targetItems.Count > 0)
        {
            return ResolveFileInputEncodingType(options, targetItems[0]);
        }

        return EncodingType.Utf8NoBom;
    }
}
