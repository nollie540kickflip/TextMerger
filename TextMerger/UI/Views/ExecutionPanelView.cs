using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace TextMerger.UI.Views;

/// <summary>
/// 4. 出力ファイル指定・進捗バー・結合実行パネル
/// </summary>
public sealed class ExecutionPanelView : FrameView
{
    private readonly TextField _outputPathField;
    private readonly ProgressBar _progressBar;
    private readonly Label _statusLabel;
    private readonly Button _mergeButton;

    public event EventHandler? BrowseOutputRequested;
    public event EventHandler? MergeOrCancelRequested;
    public event EventHandler? QuitRequested;

    public ExecutionPanelView(string initialFolder)
    {
        Title = "4. 出力先と実行";
        X = 0;
        Y = Pos.AnchorEnd(7);
        Width = Dim.Fill();
        Height = 7;
        BorderStyle = LineStyle.Rounded;

        var outputLabel = new Label
        {
            Text = "出力ファイル:",
            X = 1,
            Y = 0
        };

        _outputPathField = new TextField
        {
            Text = Path.Combine(initialFolder, "merged_output.txt"),
            X = 15,
            Y = 0,
            Width = Dim.Fill(13)
        };

        var browseOutputButton = new Button
        {
            Text = "参照...",
            X = Pos.AnchorEnd(11),
            Y = 0
        };
        browseOutputButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            BrowseOutputRequested?.Invoke(this, EventArgs.Empty);
        };

        _progressBar = new ProgressBar
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(2),
            Height = 1,
            Fraction = 0f,
            SchemeName = SchemeManager.SchemesToSchemeName(Schemes.Accent)
        };

        _statusLabel = new Label
        {
            Text = "準備完了",
            X = 1,
            Y = 3,
            Width = Dim.Fill(28)
        };

        _mergeButton = new Button
        {
            Text = "結合実行",
            IsDefault = true,
            X = Pos.AnchorEnd(24),
            Y = 3
        };
        _mergeButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            MergeOrCancelRequested?.Invoke(this, EventArgs.Empty);
        };

        var quitButton = new Button
        {
            Text = "終了",
            X = Pos.AnchorEnd(10),
            Y = 3
        };
        quitButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            QuitRequested?.Invoke(this, EventArgs.Empty);
        };

        Add(
            outputLabel,
            _outputPathField,
            browseOutputButton,
            _progressBar,
            _statusLabel,
            _mergeButton,
            quitButton);
    }

    public string OutputFilePath
    {
        get => (_outputPathField.Text ?? string.Empty).Trim();
        set => _outputPathField.Text = value;
    }

    public float ProgressFraction
    {
        get => _progressBar.Fraction;
        set => _progressBar.Fraction = Math.Clamp(value, 0f, 1f);
    }

    public string StatusText
    {
        get => _statusLabel.Text ?? string.Empty;
        set => _statusLabel.Text = value;
    }

    public void SetMergingState(bool isMerging)
    {
        _mergeButton.Text = isMerging ? "キャンセル" : "結合実行";
    }
}
