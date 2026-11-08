using TextMerger.Models;

namespace TextMerger.Services;

/// <summary>
/// 指定フォルダ内の対象ファイルをワイルドカードパターンで検索するサービス
/// </summary>
public sealed class FileScanner : IFileScanner
{
    private readonly IEncodingDetector _encodingDetector;

    public FileScanner(IEncodingDetector? encodingDetector = null)
    {
        _encodingDetector = encodingDetector ?? EncodingDetector.Default;
    }

    /// <inheritdoc />
    public IReadOnlyList<FileItem> ScanFiles(
        string directoryPath,
        string searchPatterns,
        bool includeSubdirectories,
        string? excludeFilePath = null) =>
        ScanFilesWithWarnings(directoryPath, searchPatterns, includeSubdirectories, excludeFilePath).Files;

    /// <inheritdoc />
    public FileScanResult ScanFilesWithWarnings(
        string directoryPath,
        string searchPatterns,
        bool includeSubdirectories,
        string? excludeFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return new FileScanResult([], ["指定されたフォルダが存在しません。"]);
        }

        string fullDirectoryPath = Path.GetFullPath(directoryPath);
        string? fullExcludePath = !string.IsNullOrWhiteSpace(excludeFilePath)
            ? Path.GetFullPath(excludeFilePath)
            : null;

        string[] patterns = ParsePatterns(searchPatterns);
        SearchOption searchOption = includeSubdirectories
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var matchedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        foreach (string pattern in patterns)
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(fullDirectoryPath, pattern, searchOption))
                {
                    string fullFilePath = Path.GetFullPath(file);
                    if (fullExcludePath is not null &&
                        string.Equals(fullFilePath, fullExcludePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    matchedPaths.Add(fullFilePath);
                }
            }
            catch (UnauthorizedAccessException)
            {
                warnings.Add($"アクセス権限がないフォルダをスキップしました (パターン: {pattern})");
            }
            catch (DirectoryNotFoundException)
            {
                warnings.Add($"一部のディレクトリが見つかりませんでした (パターン: {pattern})");
            }
            catch (ArgumentException)
            {
                warnings.Add($"検索パターンが不正です: '{pattern}'");
            }
        }

        List<FileItem> files = matchedPaths
            .Select(fullPath =>
            {
                var info = new FileInfo(fullPath);
                string relativePath = Path.GetRelativePath(fullDirectoryPath, fullPath);
                return new FileItem
                {
                    FullPath = fullPath,
                    RelativePath = relativePath,
                    Length = info.Exists ? info.Length : 0L,
                    DetectedEncoding = _encodingDetector.DetectFileEncoding(fullPath),
                    IsSelected = true
                };
            })
            .OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new FileScanResult(files, warnings);
    }

    /// <summary>
    /// セミコロン (;) またはカンマ (,) 区切りの検索パターン文字列を配列に分割します。
    /// 空欄または空白のみの場合は全ファイル対象 ("*.*") を返します。
    /// </summary>
    public static string[] ParsePatterns(string? rawPattern)
    {
        if (string.IsNullOrWhiteSpace(rawPattern))
        {
            return ["*.*"];
        }

        string[] tokens = rawPattern
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return tokens.Length > 0 ? tokens : ["*.*"];
    }
}
