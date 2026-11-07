using TextMerger.Models;

namespace TextMerger.Services;

/// <summary>
/// ファイルの文字コードを自動判別するサービスのインターフェース
/// </summary>
public interface IEncodingDetector
{
    EncodingType DetectFileEncoding(string filePath);
}

/// <summary>
/// DI・モック差し替えに対応した文字コード判別サービスの実装
/// </summary>
public sealed class FileEncodingDetector : IEncodingDetector
{
    public static FileEncodingDetector Default { get; } = new();

    public EncodingType DetectFileEncoding(string filePath) =>
        EncodingDetector.DetectFileEncoding(filePath);
}

/// <summary>
/// テキストファイルのバイト列から文字コード（BOM有無・UTF-8・Shift_JIS・EUC-JP・UTF-16）を自動判別するクラス
/// </summary>
public static class EncodingDetector
{
    private const int MaxSampleBytes = 65536;

    /// <summary>
    /// 指定されたファイルパスの文字コードを自動判別します。
    /// </summary>
    public static EncodingType DetectFileEncoding(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return EncodingType.Utf8NoBom;
        }

        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 4096);

            int lengthToRead = (int)Math.Min(stream.Length, MaxSampleBytes);
            if (lengthToRead == 0)
            {
                return EncodingType.Utf8NoBom;
            }

            byte[] buffer = new byte[lengthToRead];
            int bytesRead = stream.ReadAtLeast(buffer, lengthToRead, throwOnEndOfStream: false);
            return DetectFromBytes(buffer.AsSpan(0, bytesRead));
        }
        catch
        {
            return EncodingType.Utf8NoBom;
        }
    }

    /// <summary>
    /// バイト列から文字コードを自動判別します。
    /// </summary>
    public static EncodingType DetectFromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return EncodingType.Utf8NoBom;
        }

        // 1. BOM (Byte Order Mark) の確認
        if (bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF)
        {
            return EncodingType.Utf8WithBom;
        }

        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return EncodingType.Utf16Le;
            }

            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return EncodingType.Utf16Be;
            }
        }

        // 2. BOMなし UTF-16 の簡易検出 (NULLバイトの偏りを確認)
        if (bytes.Length >= 4)
        {
            int evenNulls = 0;
            int oddNulls = 0;
            int samplePairs = bytes.Length / 2;

            for (int i = 0; i < samplePairs * 2; i += 2)
            {
                if (bytes[i] == 0x00)
                {
                    evenNulls++;
                }

                if (bytes[i + 1] == 0x00)
                {
                    oddNulls++;
                }
            }

            if (oddNulls > samplePairs / 2 && evenNulls == 0)
            {
                return EncodingType.Utf16Le;
            }

            if (evenNulls > samplePairs / 2 && oddNulls == 0)
            {
                return EncodingType.Utf16Be;
            }
        }

        // 3. すべて ASCII (0x00..0x7F) の場合は UTF-8 (BOMなし)
        bool allAscii = true;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] >= 0x80)
            {
                allAscii = false;
                break;
            }
        }

        if (allAscii)
        {
            return EncodingType.Utf8NoBom;
        }

        // 4. UTF-8 の妥当性およびマルチバイトスコアを検査
        int utf8Score = ScoreUtf8(bytes);
        int sjisScore = ScoreShiftJis(bytes);
        int eucJpScore = ScoreEucJp(bytes);

        // UTF-8 として完全に妥当でマルチバイト文字が含まれる場合、UTF-8の3バイト構造は非常に厳格なため最優先
        if (utf8Score > 0 && utf8Score >= sjisScore && utf8Score >= eucJpScore)
        {
            return EncodingType.Utf8NoBom;
        }

        if (eucJpScore > sjisScore && eucJpScore > 0)
        {
            return EncodingType.EucJp;
        }

        if (sjisScore > 0)
        {
            return EncodingType.ShiftJis;
        }

        if (utf8Score >= 0)
        {
            return EncodingType.Utf8NoBom;
        }

        return EncodingType.ShiftJis;
    }

    /// <summary>
    /// UTF-8 としての妥当性を検査し、有効なマルチバイト数をスコアとして返します（不正シーケンスがあれば -1）。
    /// </summary>
    private static int ScoreUtf8(ReadOnlySpan<byte> bytes)
    {
        int score = 0;
        int i = 0;

        while (i < bytes.Length)
        {
            byte b = bytes[i];
            if (b <= 0x7F)
            {
                i++;
                continue;
            }

            int expectedContinuation;
            if (b is >= 0xC2 and <= 0xDF)
            {
                expectedContinuation = 1;
            }
            else if (b is >= 0xE0 and <= 0xEF)
            {
                expectedContinuation = 2;
            }
            else if (b is >= 0xF0 and <= 0xF4)
            {
                expectedContinuation = 3;
            }
            else
            {
                return -1;
            }

            // サンプルバッファ末尾でマルチバイト文字が途切れている場合はエラーとしない
            if (i + expectedContinuation >= bytes.Length)
            {
                break;
            }

            for (int j = 1; j <= expectedContinuation; j++)
            {
                byte c = bytes[i + j];
                if ((c & 0xC0) != 0x80)
                {
                    return -1;
                }
            }

            // オーバーロングやサロゲート領域の除外
            if (expectedContinuation == 2)
            {
                byte b1 = bytes[i + 1];
                if (b == 0xE0 && b1 < 0xA0)
                {
                    return -1;
                }
                if (b == 0xED && b1 >= 0xA0)
                {
                    return -1;
                }
            }
            else if (expectedContinuation == 3)
            {
                byte b1 = bytes[i + 1];
                if (b == 0xF0 && b1 < 0x90)
                {
                    return -1;
                }
                if (b == 0xF4 && b1 >= 0x90)
                {
                    return -1;
                }
            }

            // 3バイト日本語文字領域 (U+3000..U+9FFF, U+FF00..U+FFEF) は高スコア
            score += (expectedContinuation + 1) * 2;
            i += expectedContinuation + 1;
        }

        return score;
    }

    /// <summary>
    /// Shift_JIS (CP932) としての妥当性を検査し、有効なマルチバイト数をスコアとして返します（不正があれば -1）。
    /// </summary>
    private static int ScoreShiftJis(ReadOnlySpan<byte> bytes)
    {
        int score = 0;
        int i = 0;

        while (i < bytes.Length)
        {
            byte b1 = bytes[i];
            if (b1 <= 0x7F)
            {
                i++;
                continue;
            }

            // 半角カナ (0xA1..0xDF)
            if (b1 is >= 0xA1 and <= 0xDF)
            {
                score += 1;
                i++;
                continue;
            }

            // Shift_JIS (CP932) の第1バイト範囲: 0x81..0x9F または 0xE0..0xFC
            if (b1 is (>= 0x81 and <= 0x9F) or (>= 0xE0 and <= 0xFC))
            {
                if (i + 1 >= bytes.Length)
                {
                    break;
                }

                byte b2 = bytes[i + 1];
                // 第2バイト範囲: 0x40..0x7E または 0x80..0xFC
                if (b2 is (>= 0x40 and <= 0x7E) or (>= 0x80 and <= 0xFC))
                {
                    // ひらがな・カタカナ・第一水準漢字の頻出領域は重みを高くする
                    if (b1 is >= 0x81 and <= 0x9F)
                    {
                        score += 3;
                    }
                    else
                    {
                        score += 2;
                    }
                    i += 2;
                    continue;
                }

                return -1;
            }

            return -1;
        }

        return score;
    }

    /// <summary>
    /// EUC-JP としての妥当性を検査し、有効なマルチバイト数をスコアとして返します（不正があれば -1）。
    /// </summary>
    private static int ScoreEucJp(ReadOnlySpan<byte> bytes)
    {
        int score = 0;
        int i = 0;

        while (i < bytes.Length)
        {
            byte b1 = bytes[i];
            if (b1 <= 0x7F)
            {
                i++;
                continue;
            }

            // 2バイト漢字・かな領域 (0xA1..0xFE, 0xA1..0xFE)
            if (b1 is >= 0xA1 and <= 0xFE)
            {
                if (i + 1 >= bytes.Length)
                {
                    break;
                }

                byte b2 = bytes[i + 1];
                if (b2 is >= 0xA1 and <= 0xFE)
                {
                    // ひらがな (0xA4)・カタカナ (0xA5) は EUC-JP 特有の頻出バイト
                    if (b1 is 0xA4 or 0xA5 or >= 0xB0 and <= 0xF4)
                    {
                        score += 3;
                    }
                    else
                    {
                        score += 2;
                    }
                    i += 2;
                    continue;
                }

                return -1;
            }

            // 半角カナ (0x8E + 0xA1..0xDF)
            if (b1 == 0x8E)
            {
                if (i + 1 >= bytes.Length)
                {
                    break;
                }

                byte b2 = bytes[i + 1];
                if (b2 is >= 0xA1 and <= 0xDF)
                {
                    score += 2;
                    i += 2;
                    continue;
                }

                return -1;
            }

            // 補助漢字 (0x8F + 0xA1..0xFE + 0xA1..0xFE)
            if (b1 == 0x8F)
            {
                if (i + 2 >= bytes.Length)
                {
                    break;
                }

                byte b2 = bytes[i + 1];
                byte b3 = bytes[i + 2];
                if (b2 is >= 0xA1 and <= 0xFE && b3 is >= 0xA1 and <= 0xFE)
                {
                    score += 3;
                    i += 3;
                    continue;
                }

                return -1;
            }

            return -1;
        }

        return score;
    }
}
