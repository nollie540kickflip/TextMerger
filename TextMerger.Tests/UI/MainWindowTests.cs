using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;
using TextMerger.Models;
using TextMerger.UI;
using TextMerger.UI.Views;

namespace TextMerger.Tests.UI;

public sealed class MainWindowTests
{
    [Fact]
    public void InitializeWithAnsiDriver_ConstructsLaysOutAndSwitchesThemesWithoutError()
    {
        using IApplication app = Application.Create().Init(DriverRegistry.Names.ANSI);
        using var window = new MainWindow(app);
        window.BeginInit();
        window.EndInit();
        window.Layout();

        Assert.Equal(4, window.SubViews.Count);
        Assert.Equal(MainWindow.DefaultThemeName, ThemeManager.GetCurrentThemeName());
        Assert.Equal(LineStyle.Rounded, window.BorderStyle);

        foreach (string theme in MainWindow.AvailableThemes)
        {
            window.ApplyTheme(theme);
            Assert.Equal(theme, ThemeManager.GetCurrentThemeName());
            Assert.Equal(LineStyle.Rounded, window.BorderStyle);
            Assert.All(window.SubViews.OfType<FrameView>(), frame => Assert.Equal(LineStyle.Rounded, frame.BorderStyle));
        }

        // 各サブビューの操作検証
        SourceFilterView sourceView = window.SubViews.OfType<SourceFilterView>().Single();
        FileListPanelView fileListView = window.SubViews.OfType<FileListPanelView>().Single();
        MergeOptionsView optionsView = window.SubViews.OfType<MergeOptionsView>().Single();
        ExecutionPanelView executionView = window.SubViews.OfType<ExecutionPanelView>().Single();

        sourceView.FolderPath = Environment.CurrentDirectory;
        Assert.Equal(Environment.CurrentDirectory, sourceView.FolderPath);
        Assert.False(sourceView.IncludeSubdirectories);

        var sampleFiles = new List<FileItem>
        {
            new() { FullPath = "/tmp/a.csv", RelativePath = "a.csv", Length = 100 },
            new() { FullPath = "/tmp/b.csv", RelativePath = "b.csv", Length = 200 }
        };
        fileListView.SetFiles(sampleFiles);
        Assert.Equal(2, fileListView.TotalCount);
        Assert.Equal(2, fileListView.GetSelectedFiles().Count);

        Assert.True(optionsView.SkipHeaderOnSubsequentFiles);
        Assert.True(optionsView.TryGetHeaderLineCount(out int headerLines));
        Assert.Equal(1, headerLines);
        Assert.Equal(EncodingType.Auto, optionsView.SelectedInputEncoding);
        Assert.Equal(EncodingType.Auto, optionsView.SelectedOutputEncoding);

        executionView.ProgressFraction = 0.5f;
        Assert.Equal(0.5f, executionView.ProgressFraction);
        executionView.SetMergingState(true);
        executionView.SetMergingState(false);
    }
}
