using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TextMerger.Models;

namespace TextMerger.UI.Views;

/// <summary>
/// 3. ヘッダ行数・入出力文字コード設定パネル
/// </summary>
public sealed class MergeOptionsView : FrameView
{
    private readonly CheckBox _skipHeaderCheckBox;
    private readonly TextField _headerCountField;
    private readonly OptionSelector _inputEncodingSelector;
    private readonly OptionSelector _outputEncodingSelector;

    public MergeOptionsView(View leftAnchorView, View topAnchorView)
    {
        Title = "3. ヘッダ・文字コード設定";
        X = Pos.Right(leftAnchorView);
        Y = Pos.Bottom(topAnchorView);
        Width = Dim.Fill();
        Height = Dim.Fill(7);
        BorderStyle = LineStyle.Rounded;

        _skipHeaderCheckBox = new CheckBox
        {
            Text = "2ファイル目以降の先頭(ヘッダ)行をスキップ",
            Value = CheckState.Checked,
            X = 1,
            Y = 0
        };

        var headerCountLabel = new Label
        {
            Text = "スキップするヘッダ行数:",
            X = 3,
            Y = 1
        };

        _headerCountField = new TextField
        {
            Text = "1",
            X = Pos.Right(headerCountLabel) + 1,
            Y = 1,
            Width = 6
        };

        _skipHeaderCheckBox.ValueChanged += (_, e) =>
        {
            _headerCountField.Enabled = e.NewValue == CheckState.Checked;
        };

        var inputEncodingLabel = new Label
        {
            Text = "[入力文字コード]",
            X = 1,
            Y = 3
        };

        _inputEncodingSelector = new OptionSelector
        {
            X = 2,
            Y = 4,
            Labels = EncodingOption.InputOptions.Select(o => o.DisplayName).ToArray(),
            Value = 0
        };

        var outputEncodingLabel = new Label
        {
            Text = "[出力文字コード]",
            X = 1,
            Y = Pos.Bottom(_inputEncodingSelector) + 1
        };

        _outputEncodingSelector = new OptionSelector
        {
            X = 2,
            Y = Pos.Bottom(outputEncodingLabel),
            Labels = EncodingOption.OutputOptions.Select(o => o.DisplayName).ToArray(),
            Value = 0
        };

        Add(
            _skipHeaderCheckBox,
            headerCountLabel,
            _headerCountField,
            inputEncodingLabel,
            _inputEncodingSelector,
            outputEncodingLabel,
            _outputEncodingSelector);
    }

    public bool SkipHeaderOnSubsequentFiles => _skipHeaderCheckBox.Value == CheckState.Checked;

    public bool TryGetHeaderLineCount(out int headerLineCount)
    {
        if (!SkipHeaderOnSubsequentFiles)
        {
            headerLineCount = 0;
            return true;
        }

        if (int.TryParse(_headerCountField.Text, out headerLineCount) && headerLineCount >= 0)
        {
            return true;
        }

        headerLineCount = 0;
        return false;
    }

    public EncodingType SelectedInputEncoding
    {
        get
        {
            int idx = Math.Clamp(_inputEncodingSelector.Value ?? 0, 0, EncodingOption.InputOptions.Count - 1);
            return EncodingOption.InputOptions[idx].Type;
        }
    }

    public EncodingType SelectedOutputEncoding
    {
        get
        {
            int idx = Math.Clamp(_outputEncodingSelector.Value ?? 0, 0, EncodingOption.OutputOptions.Count - 1);
            return EncodingOption.OutputOptions[idx].Type;
        }
    }
}
