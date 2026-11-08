using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TextMerger.Models;
using TextMerger.Services;
using TextMerger.UI.Views;

namespace TextMerger.UI;

/// <summary>
/// Terminal.Gui v2 を用いたテキストファイル結合ツールのメインウィンドウ
/// </summary>
public sealed class MainWindow : Window
{
    public const string DefaultThemeName = UiThemeService.DefaultThemeName;

    public static IReadOnlyList<string> AvailableThemes => UiThemeService.AvailableThemes;

    private readonly IApplication _app;
    private readonly IFileScanner _fileScanner;
    private readonly ITextMergeService _mergeService;

    private readonly SourceFilterView _sourceView;
    private readonly FileListPanelView _fileListView;
    private readonly MergeOptionsView _optionsView;
    private readonly ExecutionPanelView _executionView;

    private CancellationTokenSource? _mergeCts;
    private bool _isMerging;

    public MainWindow(
        IApplication app,
        IFileScanner? fileScanner = null,
        ITextMergeService? mergeService = null)
    {
        _app = app;
        _fileScanner = fileScanner ?? new FileScanner();
        _mergeService = mergeService ?? new TextMergeService();

        Key quitKey = Application.GetDefaultKey(Command.Quit);
        Title = $"テキストファイル結合ツール (TextMerger) - 終了: {quitKey}";
        Width = Dim.Fill();
        Height = Dim.Fill();
        BorderStyle = LineStyle.Rounded;
        SchemeName = SchemeManager.SchemesToSchemeName(Schemes.Base);

        string initialFolder = Environment.CurrentDirectory;

        _sourceView = new SourceFilterView(initialFolder, AvailableThemes);
        _fileListView = new FileListPanelView(_sourceView);
        _optionsView = new MergeOptionsView(_fileListView, _sourceView);
        _executionView = new ExecutionPanelView(initialFolder);

        _sourceView.BrowseFolderRequested += (_, _) => OnBrowseFolder();
        _sourceView.RefreshRequested += (_, _) => RefreshFileList();
        _sourceView.ThemeChanged += (_, themeName) => ApplyTheme(themeName);

        _executionView.BrowseOutputRequested += (_, _) => OnBrowseOutput();
        _executionView.MergeOrCancelRequested += (_, _) => OnMergeOrCancel();
        _executionView.QuitRequested += (_, _) =>
        {
            _mergeCts?.Cancel();
            _app.RequestStop();
        };

        Add(_sourceView, _fileListView, _optionsView, _executionView);

        ApplyTheme(AvailableThemes[0]);
        RefreshFileList();
    }

    /// <summary>
    /// 指定した Terminal.Gui 組み込みテーマを適用し、丸角枠線スタイルを維持して再描画します。
    /// </summary>
    public void ApplyTheme(string themeName)
    {
        UiThemeService.ApplyTheme(
            themeName,
            this,
            _sourceView,
            _fileListView,
            _optionsView,
            _executionView);

        int themeIdx = UiThemeService.FindThemeIndex(themeName);
        if (themeIdx >= 0 && _sourceView.SelectedThemeIndex != themeIdx)
        {
            _sourceView.SelectedThemeIndex = themeIdx;
        }

        SetNeedsDraw();
        _app.LayoutAndDraw(true);
    }

    private void OnBrowseFolder()
    {
        using var dialog = new OpenDialog
        {
            Title = "対象フォルダを選択",
            OpenMode = OpenMode.Directory,
            AllowsMultipleSelection = false,
            BorderStyle = LineStyle.Rounded,
            Path = Directory.Exists(_sourceView.FolderPath)
                ? _sourceView.FolderPath
                : Environment.CurrentDirectory
        };

        _app.Run(dialog);

        if (!dialog.Canceled)
        {
            string? selected = dialog.FilePaths.FirstOrDefault() ?? dialog.Path;
            if (!string.IsNullOrWhiteSpace(selected))
            {
                _sourceView.FolderPath = selected;
                if (string.IsNullOrWhiteSpace(_executionView.OutputFilePath) ||
                    Path.GetFileName(_executionView.OutputFilePath) == "merged_output.txt")
                {
                    _executionView.OutputFilePath = Path.Combine(selected, "merged_output.txt");
                }
                RefreshFileList();
            }
        }
    }

    private void OnBrowseOutput()
    {
        using var dialog = new SaveDialog
        {
            Title = "結合ファイルの保存先を選択",
            BorderStyle = LineStyle.Rounded,
            Path = !string.IsNullOrWhiteSpace(_executionView.OutputFilePath)
                ? _executionView.OutputFilePath
                : Path.Combine(Environment.CurrentDirectory, "merged_output.txt")
        };

        _app.Run(dialog);

        if (!dialog.Canceled && !string.IsNullOrWhiteSpace(dialog.FileName))
        {
            _executionView.OutputFilePath = dialog.FileName;
            RefreshFileList();
        }
    }

    private void RefreshFileList()
    {
        FileScanResult scanResult = _fileScanner.ScanFilesWithWarnings(
            _sourceView.FolderPath,
            _sourceView.SearchPattern,
            _sourceView.IncludeSubdirectories,
            _executionView.OutputFilePath);

        _fileListView.SetFiles(scanResult.Files);

        _executionView.StatusText = scanResult.Warnings.Count > 0
            ? $"警告: {scanResult.Warnings[0]} ({_fileListView.TotalCount} 件検出)"
            : $"ファイル一覧を更新しました ({_fileListView.TotalCount} 件)";
    }

    private void OnMergeOrCancel()
    {
        if (_isMerging)
        {
            _mergeCts?.Cancel();
            _executionView.StatusText = "結合処理をキャンセルしています...";
            return;
        }

        IReadOnlyList<FileItem> selectedItems = _fileListView.GetSelectedFiles();
        if (selectedItems.Count == 0)
        {
            MessageBox.ErrorQuery(_app, "入力エラー", "結合対象のファイルが選択されていません。", "OK");
            return;
        }

        string outputPath = _executionView.OutputFilePath;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            MessageBox.ErrorQuery(_app, "入力エラー", "出力ファイルパスを指定してください。", "OK");
            return;
        }

        if (!_optionsView.TryGetHeaderLineCount(out int headerLineCount))
        {
            MessageBox.ErrorQuery(_app, "入力エラー", "ヘッダ行数には 0 以上の整数を入力してください。", "OK");
            return;
        }

        if (File.Exists(outputPath))
        {
            int? confirm = MessageBox.Query(
                _app,
                "上書き確認",
                $"出力先ファイルは既に存在します。上書きしますか？\n{outputPath}",
                "はい",
                "いいえ");

            if (confirm != 0)
            {
                return;
            }
        }

        var options = new MergeOptions
        {
            OutputFilePath = outputPath,
            InputEncoding = _optionsView.SelectedInputEncoding,
            OutputEncoding = _optionsView.SelectedOutputEncoding,
            SkipHeaderOnSubsequentFiles = _optionsView.SkipHeaderOnSubsequentFiles,
            HeaderLineCount = headerLineCount
        };

        _ = ExecuteMergeAsync(selectedItems, options);
    }

    private async Task ExecuteMergeAsync(IReadOnlyList<FileItem> selectedItems, MergeOptions options)
    {
        _isMerging = true;
        _mergeCts = new CancellationTokenSource();
        CancellationToken token = _mergeCts.Token;

        _executionView.SetMergingState(isMerging: true);
        _executionView.ProgressFraction = 0f;
        _executionView.StatusText = "結合処理を開始しています...";

        var progress = new Progress<MergeProgress>(p =>
        {
            _app.Invoke(() =>
            {
                float fraction = p.TotalFiles > 0 ? (float)p.ProcessedFiles / p.TotalFiles : 0f;
                _executionView.ProgressFraction = fraction;
                _executionView.StatusText = $"処理中 ({p.ProcessedFiles}/{p.TotalFiles}): {p.CurrentFileName} ({p.WrittenLines:N0} 行出力)";
            });
        });

        try
        {
            MergeResult result = await Task.Run(
                () => _mergeService.MergeFilesAsync(selectedItems, options, progress, token),
                token);

            _app.Invoke(() =>
            {
                _executionView.ProgressFraction = 1f;
                _executionView.StatusText = $"完了: {result.MergedFileCount} ファイル / {result.TotalLinesWritten:N0} 行を出力しました。";

                MessageBox.Query(
                    _app,
                    "結合完了",
                    $"ファイルの結合が完了しました。\n\n" +
                    $"・結合ファイル数: {result.MergedFileCount} 件\n" +
                    $"・総出力行数: {result.TotalLinesWritten:N0} 行\n" +
                    $"・スキップしたヘッダ行数: {result.TotalHeaderLinesSkipped:N0} 行\n" +
                    $"・出力文字コード: {EncodingOption.GetShortName(result.EffectiveOutputEncoding)}\n" +
                    $"・出力サイズ: {FileItem.FormatBytes(result.OutputFileSize)}\n" +
                    $"・出力先: {result.OutputFilePath}",
                    "OK");
            });
        }
        catch (OperationCanceledException)
        {
            _app.Invoke(() =>
            {
                _executionView.ProgressFraction = 0f;
                _executionView.StatusText = "結合処理がキャンセルされました。";
            });
        }
        catch (Exception ex)
        {
            _app.Invoke(() =>
            {
                _executionView.StatusText = $"エラー: {ex.Message}";
                MessageBox.ErrorQuery(_app, "結合エラー", $"結合処理中にエラーが発生しました:\n{ex.Message}", "OK");
            });
        }
        finally
        {
            _mergeCts.Dispose();
            _mergeCts = null;

            _app.Invoke(() =>
            {
                _isMerging = false;
                _executionView.SetMergingState(isMerging: false);
            });
        }
    }
}
