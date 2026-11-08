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
