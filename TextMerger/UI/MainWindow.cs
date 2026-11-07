using System.Collections.ObjectModel;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TextMerger.Models;
using TextMerger.Services;

namespace TextMerger.UI;

/// <summary>
/// Terminal.Gui v2 を用いたテキストファイル結合ツールのメインウィンドウ
/// </summary>
public sealed class MainWindow : Window
{
    private readonly IApplication _app;
    private readonly IFileScanner _fileScanner;
    private readonly ITextMergeService _mergeService;

    private readonly TextField _folderPathField;
    private readonly TextField _patternField;
    private readonly CheckBox _subfoldersCheckBox;

    private readonly ListView _fileListView;
    private readonly Label _fileSummaryLabel;

    private readonly CheckBox _skipHeaderCheckBox;
    private readonly TextField _headerCountField;
    private readonly OptionSelector _inputEncodingSelector;
    private readonly OptionSelector _outputEncodingSelector;

    private readonly TextField _outputPathField;
    private readonly ProgressBar _progressBar;
    private readonly Label _statusLabel;
    private readonly Button _mergeButton;

    private readonly List<FileItem> _currentFiles = [];
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

        string initialFolder = Environment.CurrentDirectory;

        // =========================================================
        // 1. 上部：検索・フィルタ設定フレーム
        // =========================================================
        var sourceFrame = new FrameView
        {
            Title = "1. 対象フォルダ・検索条件",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 6
        };

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
        browseFolderButton.Accepting += OnBrowseFolderAccepted;

        var scanButton = new Button
        {
            Text = "一覧更新",
            X = Pos.AnchorEnd(12),
            Y = 0
        };
        scanButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            RefreshFileList();
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
        _subfoldersCheckBox.ValueChanged += (_, _) => RefreshFileList();

        sourceFrame.Add(
            folderLabel,
            _folderPathField,
            browseFolderButton,
            scanButton,
            patternLabel,
            _patternField,
            _subfoldersCheckBox);

        // =========================================================
        // 2. 中部左：ファイル一覧＆個別選択フレーム
        // =========================================================
        var filesFrame = new FrameView
        {
            Title = "2. 結合対象ファイル一覧 (Spaceキーまたはクリックで選択切替)",
            X = 0,
            Y = Pos.Bottom(sourceFrame),
            Width = Dim.Percent(55),
            Height = Dim.Fill(7)
        };

        _fileListView = new ListView
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Height = Dim.Fill(3),
            ShowMarks = true,
            MarkMultiple = true
        };
        _fileListView.KeyUp += (_, _) => UpdateSummaryLabel();
        _fileListView.MouseEvent += (_, _) => UpdateSummaryLabel();

        _fileSummaryLabel = new Label
        {
            Text = "対象ファイル: 0 件",
            X = 1,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(1)
        };

        var selectAllButton = new Button
        {
            Text = "全選択",
            X = 1,
            Y = Pos.AnchorEnd(1)
        };
        selectAllButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            SetAllMarks(true);
        };

        var unselectAllButton = new Button
        {
            Text = "全解除",
            X = Pos.Right(selectAllButton) + 1,
            Y = Pos.AnchorEnd(1)
        };
        unselectAllButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            SetAllMarks(false);
        };

        var moveUpButton = new Button
        {
            Text = "↑ 上へ",
            X = Pos.Right(unselectAllButton) + 2,
            Y = Pos.AnchorEnd(1)
        };
        moveUpButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            MoveSelectedFile(-1);
        };

        var moveDownButton = new Button
        {
            Text = "↓ 下へ",
            X = Pos.Right(moveUpButton) + 1,
            Y = Pos.AnchorEnd(1)
        };
        moveDownButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            MoveSelectedFile(1);
        };

        filesFrame.Add(
            _fileListView,
            _fileSummaryLabel,
            selectAllButton,
            unselectAllButton,
            moveUpButton,
            moveDownButton);

        // =========================================================
        // 3. 中部右：ヘッダ設定・文字コード設定フレーム
        // =========================================================
        var optionsFrame = new FrameView
        {
            Title = "3. ヘッダ・文字コード設定",
            X = Pos.Right(filesFrame),
            Y = Pos.Bottom(sourceFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill(7)
        };

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

        optionsFrame.Add(
            _skipHeaderCheckBox,
            headerCountLabel,
            _headerCountField,
            inputEncodingLabel,
            _inputEncodingSelector,
            outputEncodingLabel,
            _outputEncodingSelector);

        // =========================================================
        // 4. 下部：出力先設定・進捗・実行フレーム
        // =========================================================
        var bottomFrame = new FrameView
        {
            Title = "4. 出力先と実行",
            X = 0,
            Y = Pos.AnchorEnd(7),
            Width = Dim.Fill(),
            Height = 7
        };

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
        browseOutputButton.Accepting += OnBrowseOutputAccepted;

        _progressBar = new ProgressBar
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(2),
            Height = 1,
            Fraction = 0f
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
        _mergeButton.Accepting += OnMergeButtonAccepted;

        var quitButton = new Button
        {
            Text = "終了",
            X = Pos.AnchorEnd(10),
            Y = 3
        };
        quitButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            _mergeCts?.Cancel();
            _app.RequestStop();
        };

        bottomFrame.Add(
            outputLabel,
            _outputPathField,
            browseOutputButton,
            _progressBar,
            _statusLabel,
            _mergeButton,
            quitButton);

        Add(sourceFrame, filesFrame, optionsFrame, bottomFrame);

        RefreshFileList();
    }

    private void OnBrowseFolderAccepted(object? sender, CommandEventArgs e)
    {
        e.Handled = true;

        using var dialog = new OpenDialog
        {
            Title = "対象フォルダを選択",
            OpenMode = OpenMode.Directory,
            AllowsMultipleSelection = false,
            Path = Directory.Exists(_folderPathField.Text)
                ? _folderPathField.Text
                : Environment.CurrentDirectory
        };

        _app.Run(dialog);

        if (!dialog.Canceled)
        {
            string? selected = dialog.FilePaths.FirstOrDefault() ?? dialog.Path;
            if (!string.IsNullOrWhiteSpace(selected))
            {
                _folderPathField.Text = selected;
                if (string.IsNullOrWhiteSpace(_outputPathField.Text) ||
                    Path.GetFileName(_outputPathField.Text) == "merged_output.txt")
                {
                    _outputPathField.Text = Path.Combine(selected, "merged_output.txt");
                }
                RefreshFileList();
            }
        }
    }

    private void OnBrowseOutputAccepted(object? sender, CommandEventArgs e)
    {
        e.Handled = true;

        using var dialog = new SaveDialog
        {
            Title = "結合ファイルの保存先を選択",
            Path = !string.IsNullOrWhiteSpace(_outputPathField.Text)
                ? _outputPathField.Text
                : Path.Combine(Environment.CurrentDirectory, "merged_output.txt")
        };

        _app.Run(dialog);

        if (!dialog.Canceled && !string.IsNullOrWhiteSpace(dialog.FileName))
        {
            _outputPathField.Text = dialog.FileName;
            RefreshFileList();
        }
    }

    private void RefreshFileList()
    {
        string folder = _folderPathField.Text ?? string.Empty;
        string pattern = _patternField.Text ?? "*.*";
        bool includeSubdirs = _subfoldersCheckBox.Value == CheckState.Checked;
        string? outputPath = _outputPathField.Text;

        _currentFiles.Clear();
        FileScanResult scanResult = _fileScanner.ScanFilesWithWarnings(folder, pattern, includeSubdirs, outputPath);
        _currentFiles.AddRange(scanResult.Files);

        RebindListViewSource();
        if (scanResult.Warnings.Count > 0)
        {
            _statusLabel.Text = $"警告: {scanResult.Warnings[0]} ({_currentFiles.Count} 件検出)";
        }
        else
        {
            _statusLabel.Text = $"ファイル一覧を更新しました ({_currentFiles.Count} 件)";
        }
    }

    private void RebindListViewSource(int? selectedIndex = null)
    {
        var observable = new ObservableCollection<FileItem>(_currentFiles);
        _fileListView.SetSource(observable);

        if (_fileListView.Source is not null)
        {
            for (int i = 0; i < _currentFiles.Count; i++)
            {
                _fileListView.Source.SetMark(i, _currentFiles[i].IsSelected);
            }
        }

        if (selectedIndex.HasValue && selectedIndex.Value >= 0 && selectedIndex.Value < _currentFiles.Count)
        {
            _fileListView.SelectedItem = selectedIndex.Value;
        }

        UpdateSummaryLabel();
    }

    private void SyncMarksFromListView()
    {
        if (_fileListView.Source is null)
        {
            return;
        }

        for (int i = 0; i < _currentFiles.Count && i < _fileListView.Source.Count; i++)
        {
            _currentFiles[i].IsSelected = _fileListView.Source.IsMarked(i);
        }
    }

    private void SetAllMarks(bool mark)
    {
        foreach (FileItem item in _currentFiles)
        {
            item.IsSelected = mark;
        }

        _fileListView.MarkAll(mark);
        _fileListView.SetNeedsDraw();
        UpdateSummaryLabel();
    }

    private void MoveSelectedFile(int offset)
    {
        if (_currentFiles.Count <= 1 || _fileListView.SelectedItem is null)
        {
            return;
        }

        SyncMarksFromListView();

        int currentIndex = _fileListView.SelectedItem.Value;
        int targetIndex = currentIndex + offset;
        if (targetIndex < 0 || targetIndex >= _currentFiles.Count)
        {
            return;
        }

        (_currentFiles[currentIndex], _currentFiles[targetIndex]) =
            (_currentFiles[targetIndex], _currentFiles[currentIndex]);

        RebindListViewSource(targetIndex);
    }

    private void UpdateSummaryLabel()
    {
        SyncMarksFromListView();
        int selectedCount = _currentFiles.Count(f => f.IsSelected);
        long totalBytes = _currentFiles.Where(f => f.IsSelected).Sum(f => f.Length);
        _fileSummaryLabel.Text = $"選択: {selectedCount} / {_currentFiles.Count} 件 (合計: {FileItem.FormatBytes(totalBytes)})";
    }

    private void OnMergeButtonAccepted(object? sender, CommandEventArgs e)
    {
        e.Handled = true;
        if (_isMerging)
        {
            _mergeCts?.Cancel();
            _statusLabel.Text = "結合処理をキャンセルしています...";
            return;
        }

        SyncMarksFromListView();
        UpdateSummaryLabel();

        List<FileItem> selectedItems = _currentFiles
            .Where(f => f.IsSelected)
            .ToList();

        if (selectedItems.Count == 0)
        {
            MessageBox.ErrorQuery(_app, "入力エラー", "結合対象のファイルが選択されていません。", "OK");
            return;
        }

        string outputPath = (_outputPathField.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            MessageBox.ErrorQuery(_app, "入力エラー", "出力ファイルパスを指定してください。", "OK");
            return;
        }

        bool skipHeader = _skipHeaderCheckBox.Value == CheckState.Checked;
        int headerLineCount = 0;
        if (skipHeader)
        {
            if (!int.TryParse(_headerCountField.Text, out headerLineCount) || headerLineCount < 0)
            {
                MessageBox.ErrorQuery(_app, "入力エラー", "ヘッダ行数には 0 以上の整数を入力してください。", "OK");
                return;
            }
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

        int inputIdx = _inputEncodingSelector.Value ?? 0;
        int outputIdx = _outputEncodingSelector.Value ?? 0;

        EncodingOption inputEncodingOption = EncodingOption.InputOptions[
            Math.Clamp(inputIdx, 0, EncodingOption.InputOptions.Count - 1)];
        EncodingOption outputEncodingOption = EncodingOption.OutputOptions[
            Math.Clamp(outputIdx, 0, EncodingOption.OutputOptions.Count - 1)];

        var options = new MergeOptions
        {
            OutputFilePath = outputPath,
            InputEncoding = inputEncodingOption.Type,
            OutputEncoding = outputEncodingOption.Type,
            SkipHeaderOnSubsequentFiles = skipHeader,
            HeaderLineCount = headerLineCount
        };

        _ = ExecuteMergeAsync(selectedItems, options);
    }

    private async Task ExecuteMergeAsync(IReadOnlyList<FileItem> selectedItems, MergeOptions options)
    {
        _isMerging = true;
        _mergeCts = new CancellationTokenSource();
        CancellationToken token = _mergeCts.Token;

        _mergeButton.Text = "キャンセル";
        _progressBar.Fraction = 0f;
        _statusLabel.Text = "結合処理を開始しています...";

        var progress = new Progress<MergeProgress>(p =>
        {
            _app.Invoke(() =>
            {
                float fraction = p.TotalFiles > 0 ? (float)p.ProcessedFiles / p.TotalFiles : 0f;
                _progressBar.Fraction = Math.Clamp(fraction, 0f, 1f);
                _statusLabel.Text = $"処理中 ({p.ProcessedFiles}/{p.TotalFiles}): {p.CurrentFileName} ({p.WrittenLines:N0} 行出力)";
            });
        });

        try
        {
            MergeResult result = await Task.Run(
                () => _mergeService.MergeFilesAsync(selectedItems, options, progress, token),
                token);

            _app.Invoke(() =>
            {
                _progressBar.Fraction = 1f;
                _statusLabel.Text = $"完了: {result.MergedFileCount} ファイル / {result.TotalLinesWritten:N0} 行を出力しました。";

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
                _progressBar.Fraction = 0f;
                _statusLabel.Text = "結合処理がキャンセルされました。";
            });
        }
        catch (Exception ex)
        {
            _app.Invoke(() =>
            {
                _statusLabel.Text = $"エラー: {ex.Message}";
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
                _mergeButton.Text = "結合実行";
            });
        }
    }
}
