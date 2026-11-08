using TextMerger.Models;

namespace TextMerger.Services;

/// <summary>
/// ファイルの文字コードを自動判別するサービスのインターフェース
/// </summary>
public interface IEncodingDetector
{
    /// <summary>
    /// 指定されたファイルパスの先頭バイト列から文字コードを自動判別します。
    /// ファイルが存在しない場合や空ファイルの場合は <see cref="EncodingType.Utf8NoBom"/> を返します。
    /// </summary>
    EncodingType DetectFileEncoding(string filePath);
}
