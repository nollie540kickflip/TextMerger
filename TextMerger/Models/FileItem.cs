namespace TextMerger.Models;

/// <summary>
/// 結合対象候補のファイル情報
/// </summary>
public sealed class FileItem
{
    public required string FullPath { get; init; }
    public required string RelativePath { get; init; }
    public required long Length { get; init; }
    public EncodingType DetectedEncoding { get; init; } = EncodingType.Utf8NoBom;
    public bool IsSelected { get; set; } = true;

    public string FormattedSize => FormatBytes(Length);

    public override string ToString() =>
        $"{RelativePath}  ({FormattedSize}, {EncodingOption.GetShortName(DetectedEncoding)})";

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }
        double kb = bytes / 1024.0;
        if (kb < 1024)
        {
            return $"{kb:F1} KB";
        }
        double mb = kb / 1024.0;
        if (mb < 1024)
        {
            return $"{mb:F2} MB";
        }
        double gb = mb / 1024.0;
        return $"{gb:F2} GB";
    }
}
