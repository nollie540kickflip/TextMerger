using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace TextMerger.UI.Views;

/// <summary>
/// 1. 対象フォルダ・検索条件・UIテーマ設定パネル
/// </summary>
public sealed class SourceFilterView : FrameView
{
    private readonly TextField _folderPathField;
    private readonly TextField _patternField;
    private readonly CheckBox _subfoldersCheckBox;
    private readonly OptionSelector _themeSelector;

    public event EventHandler? BrowseFolderRequested;
    public event EventHandler? RefreshRequested;
    public event EventHandler<string>? ThemeChanged;

    public SourceFilterView(string initialFolder, IReadOnlyList<string> availableThemes)
    {
        Title = "1. 対象フォルダ・検索条件・テーマ";
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = 7;
        BorderStyle = LineStyle.Rounded;

        var folderLabel = new Label
        {
            Text = "対象フォルダ:",
            X = 1,
            Y = 0
        };

        _folderPathField = new TextField
        {
            Text = initialFolder,
            X = 16,
            Y = 0,
            Width = Dim.Fill(24)
        };

        var browseFolderButton = new Button
        {
            Text = "参照...",
            X = Pos.AnchorEnd(22),
            Y = 0
        };
        browseFolderButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            BrowseFolderRequested?.Invoke(this, EventArgs.Empty);
        };

        var scanButton = new Button
        {
            Text = "一覧更新",
            X = Pos.AnchorEnd(12),
            Y = 0
        };
        scanButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        };

        var patternLabel = new Label
        {
            Text = "拡張子フィルタ:",
            X = 1,
            Y = 2
        };

        _patternField = new TextField
        {
            Text = "*.txt;*.csv;*.tsv",
            X = 16,
            Y = 2,
            Width = 28
        };

        _subfoldersCheckBox = new CheckBox
        {
            Text = "サブフォルダを含める",
            Value = CheckState.UnChecked,
            X = Pos.Right(_patternField) + 2,
            Y = 2
        };
        _subfoldersCheckBox.ValueChanged += (_, _) =>
            RefreshRequested?.Invoke(this, EventArgs.Empty);

        var themeLabel = new Label
        {
            Text = "UIテーマ:",
            X = 1,
            Y = 4
        };

        _themeSelector = new OptionSelector
        {
            X = 16,
            Y = 4,
            Orientation = Orientation.Horizontal,
            HorizontalSpace = 2,
            Labels = availableThemes.ToArray(),
            Value = 0
        };
        _themeSelector.ValueChanged += (_, e) =>
        {
            if (e.NewValue.HasValue &&
                e.NewValue.Value >= 0 &&
                e.NewValue.Value < availableThemes.Count)
            {
                ThemeChanged?.Invoke(this, availableThemes[e.NewValue.Value]);
            }
        };

        Add(
            folderLabel,
            _folderPathField,
            browseFolderButton,
            scanButton,
            patternLabel,
            _patternField,
            _subfoldersCheckBox,
            themeLabel,
            _themeSelector);
    }

    public string FolderPath
    {
        get => _folderPathField.Text ?? string.Empty;
        set => _folderPathField.Text = value;
    }

    public string SearchPattern => _patternField.Text ?? "*.*";

    public bool IncludeSubdirectories => _subfoldersCheckBox.Value == CheckState.Checked;

    public int? SelectedThemeIndex
    {
        get => _themeSelector.Value;
        set => _themeSelector.Value = value;
    }
}
