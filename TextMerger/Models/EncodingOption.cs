using System.Text;

namespace TextMerger.Models;

/// <summary>
/// 入出力で選択可能な文字コード種別
/// </summary>
public enum EncodingType
{
    Auto,
    Utf8NoBom,
    Utf8WithBom,
    ShiftJis,
    EucJp,
    Utf16Le,
    Utf16Be
}

/// <summary>
/// 文字コード選択肢の表示名と Encoding インスタンス生成を提供するヘルパー
/// </summary>
public sealed record EncodingOption(EncodingType Type, string DisplayName)
{
    static EncodingOption()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// 入力用の文字コード選択肢一覧（デフォルトは各ファイルの文字コードを自動判別）
    /// </summary>
    public static IReadOnlyList<EncodingOption> InputOptions { get; } =
    [
        new(EncodingType.Auto, "自動判別 (オリジナル)"),
        new(EncodingType.Utf8NoBom, "UTF-8 (BOM自動判別)"),
        new(EncodingType.ShiftJis, "Shift_JIS (CP932)"),
        new(EncodingType.EucJp, "EUC-JP"),
        new(EncodingType.Utf16Le, "UTF-16 LE"),
        new(EncodingType.Utf16Be, "UTF-16 BE")
    ];

    /// <summary>
    /// 出力用の文字コード選択肢一覧（デフォルトは入力ファイルのオリジナル文字コードを維持）
    /// </summary>
    public static IReadOnlyList<EncodingOption> OutputOptions { get; } =
    [
        new(EncodingType.Auto, "入力と同じ (オリジナル)"),
        new(EncodingType.Utf8NoBom, "UTF-8 (BOMなし)"),
        new(EncodingType.Utf8WithBom, "UTF-8 (BOMあり)"),
        new(EncodingType.ShiftJis, "Shift_JIS (CP932)"),
        new(EncodingType.EucJp, "EUC-JP"),
        new(EncodingType.Utf16Le, "UTF-16 LE"),
        new(EncodingType.Utf16Be, "UTF-16 BE")
    ];

    /// <summary>
    /// EncodingType に対応する System.Text.Encoding インスタンスを取得します。
    /// </summary>
    public static Encoding GetEncoding(EncodingType type)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return type switch
        {
            EncodingType.Utf8NoBom => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            EncodingType.Utf8WithBom => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            EncodingType.ShiftJis => Encoding.GetEncoding(932),
            EncodingType.EucJp => Encoding.GetEncoding(51932),
            EncodingType.Utf16Le => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
            EncodingType.Utf16Be => new UnicodeEncoding(bigEndian: true, byteOrderMark: true),
            _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };
    }

    /// <summary>
    /// 一覧表示やダイアログ表示用の簡潔な文字コード名を返します。
    /// </summary>
    public static string GetShortName(EncodingType type) => type switch
    {
        EncodingType.Auto => "自動判別",
        EncodingType.Utf8NoBom => "UTF-8",
        EncodingType.Utf8WithBom => "UTF-8(BOM)",
        EncodingType.ShiftJis => "Shift_JIS",
        EncodingType.EucJp => "EUC-JP",
        EncodingType.Utf16Le => "UTF-16LE",
        EncodingType.Utf16Be => "UTF-16BE",
        _ => "UTF-8"
    };

    public Encoding ToEncoding() => GetEncoding(Type);

    public override string ToString() => DisplayName;
}
