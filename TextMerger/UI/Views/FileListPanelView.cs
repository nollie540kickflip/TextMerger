using System.Collections.ObjectModel;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TextMerger.Models;

namespace TextMerger.UI.Views;

/// <summary>
/// 2. 結合対象ファイル一覧・選択・順序入れ替えパネル
/// </summary>
public sealed class FileListPanelView : FrameView
{
    private readonly ListView _fileListView;
    private readonly Label _fileSummaryLabel;
    private readonly List<FileItem> _currentFiles = [];

    public FileListPanelView(View topAnchorView)
    {
        Title = "2. 結合対象ファイル一覧 (Spaceキーまたはクリックで選択切替)";
        X = 0;
        Y = Pos.Bottom(topAnchorView);
        Width = Dim.Percent(55);
        Height = Dim.Fill(7);
        BorderStyle = LineStyle.Rounded;

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
            Width = Dim.Fill(1),
            SchemeName = SchemeManager.SchemesToSchemeName(Schemes.Accent)
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

        Add(
            _fileListView,
            _fileSummaryLabel,
            selectAllButton,
            unselectAllButton,
            moveUpButton,
            moveDownButton);
    }

    public int TotalCount => _currentFiles.Count;

    public void SetFiles(IEnumerable<FileItem> files)
    {
        _currentFiles.Clear();
        _currentFiles.AddRange(files);
        RebindListViewSource();
    }

    public IReadOnlyList<FileItem> GetSelectedFiles()
    {
        SyncMarksFromListView();
        UpdateSummaryLabel();
        return _currentFiles.Where(f => f.IsSelected).ToList();
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
}
