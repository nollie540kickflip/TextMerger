using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;
using TextMerger.UI;

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
    }
}
