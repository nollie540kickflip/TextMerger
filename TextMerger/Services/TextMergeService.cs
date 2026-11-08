using System.Buffers;
using System.Text;
using TextMerger.Models;

namespace TextMerger.Services;

/// <summary>
/// 複数のテキストファイルを指定された文字コード・ヘッダスキップ設定で結合するサービス。
/// ArrayPool&lt;char&gt; の固定長バッファによるチャンクストリーミングを行い、1GB超の大容量データでも定数メモリ O(1) で動作します。
/// </summary>
public sealed class TextMergeService : ITextMergeService
{
    private const int ChunkBufferSize = 65536;
    private const long ProgressReportIntervalMs = 100;

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
        char[] rentedBuffer = ArrayPool<char>.Shared.Rent(ChunkBufferSize);

        try
        {
            await using (var outputStream = new FileStream(
                tempOutputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: ChunkBufferSize,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var writer = new StreamWriter(outputStream, outputEncoding, bufferSize: ChunkBufferSize))
            {
                long lastProgressTick = Environment.TickCount64;

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
                        bufferSize: ChunkBufferSize,
                        options: FileOptions.Asynchronous | FileOptions.SequentialScan);

                    using var reader = new StreamReader(
                        inputStream,
                        fileInputEncoding,
                        detectEncodingFromByteOrderMarks: true,
                        bufferSize: ChunkBufferSize);

                    int headersToSkip = (fileIndex > 0 && options.SkipHeaderOnSubsequentFiles)
                        ? options.HeaderLineCount
                        : 0;

                    int skippedLines = 0;
                    bool pendingCarriageReturn = false;
                    bool hasWrittenAnyBodyChar = false;
                    bool lastCharWasLineBreak = false;

                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int charsRead = await reader
                            .ReadAsync(rentedBuffer.AsMemory(0, ChunkBufferSize), cancellationToken)
                            .ConfigureAwait(false);

                        if (charsRead == 0)
                        {
                            break;
                        }

                        int offset = 0;

                        // 直前チャンク末尾が '\r' で、今回チャンク先頭が '\n' の場合は CRLF の後半として消費
                        if (pendingCarriageReturn)
                        {
                            pendingCarriageReturn = false;
                            if (rentedBuffer[0] == '\n')
                            {
                                offset = 1;
                            }
                        }

                        // 1. ヘッダ行の読み飛ばし（改行コード \r\n, \n, \r に対応）
                        while (skippedLines < headersToSkip && offset < charsRead)
                        {
                            char c = rentedBuffer[offset++];
                            if (c == '\r')
                            {
                                skippedLines++;
                                totalHeaderLinesSkipped++;
                                if (offset < charsRead)
                                {
                                    if (rentedBuffer[offset] == '\n')
                                    {
                                        offset++;
                                    }
                                }
                                else
                                {
                                    pendingCarriageReturn = true;
                                }
                            }
                            else if (c == '\n')
                            {
                                skippedLines++;
                                totalHeaderLinesSkipped++;
                            }
                        }

                        // 2. 本体データの固定長チャンク書き出し（string アロケーションなし）
                        int bodyLength = charsRead - offset;
                        if (bodyLength > 0)
                        {
                            hasWrittenAnyBodyChar = true;
                            ReadOnlySpan<char> bodySpan = rentedBuffer.AsSpan(offset, bodyLength);
                            totalLinesWritten += CountLineBreaks(bodySpan, ref pendingCarriageReturn, out lastCharWasLineBreak);

                            await writer
                                .WriteAsync(rentedBuffer.AsMemory(offset, bodyLength), cancellationToken)
                                .ConfigureAwait(false);
                        }

                        long nowTick = Environment.TickCount64;
                        if (progress is not null && nowTick - lastProgressTick >= ProgressReportIntervalMs)
                        {
                            lastProgressTick = nowTick;
                            progress.Report(new MergeProgress(
                                ProcessedFiles: processedFiles,
                                TotalFiles: targetItems.Count,
                                CurrentFileName: fileName,
                                WrittenLines: totalLinesWritten));
                        }
                    }

                    // ファイル末尾に改行コードがない場合は改行を補完し、次ファイルの先頭行と連結されるのを防ぐ
                    if (hasWrittenAnyBodyChar && !lastCharWasLineBreak)
                    {
                        await writer.WriteLineAsync().ConfigureAwait(false);
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
            ArrayPool<char>.Shared.Return(rentedBuffer);

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

    /// <summary>
    /// チャンク内の改行数（\r\n, \n, \r）をカウントし、チャンク境界を跨ぐ \r\n と末尾改行状態を追跡します。
    /// </summary>
    private static long CountLineBreaks(
        ReadOnlySpan<char> span,
        ref bool pendingCarriageReturn,
        out bool endedWithLineBreak)
    {
        long lines = 0;
        int i = 0;

        while (i < span.Length)
        {
            char c = span[i++];
            if (c == '\r')
            {
                lines++;
                if (i < span.Length)
                {
                    if (span[i] == '\n')
                    {
                        i++;
                    }
                }
                else
                {
                    pendingCarriageReturn = true;
                }
            }
            else if (c == '\n')
            {
                lines++;
            }
        }

        char last = span[^1];
        endedWithLineBreak = last is '\r' or '\n';
        return lines;
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
