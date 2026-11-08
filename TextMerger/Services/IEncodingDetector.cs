using TextMerger.Models;

namespace TextMerger.Services;

/// <summary>
/// ファイルの文字コードを自動判別するサービスのインターフェース
/// </summary>
public interface IEncodingDetector
{
    EncodingType DetectFileEncoding(string filePath);
}
