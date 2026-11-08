using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace TextMerger.UI;

/// <summary>
/// Terminal.Gui v2 のテーマ一覧管理と丸角枠線スタイルの適用を担当するサービス
/// </summary>
public static class UiThemeService
{
    public const string DefaultThemeName = "Dark";

    public static IReadOnlyList<string> AvailableThemes { get; } = BuildThemeList();

    public static int FindThemeIndex(string themeName)
    {
        for (int i = 0; i < AvailableThemes.Count; i++)
        {
            if (string.Equals(AvailableThemes[i], themeName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    public static void ApplyTheme(string themeName, View rootView, params View[] framedViews)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            return;
        }

        ThemeManager.Theme = themeName;

        // テーマ切替時にも丸角枠線スタイル (LineStyle.Rounded) を維持
        WindowSettings.Current = WindowSettings.Current with { DefaultBorderStyle = LineStyle.Rounded };
        FrameViewSettings.Current = FrameViewSettings.Current with { DefaultBorderStyle = LineStyle.Rounded };
        DialogSettings.Current = DialogSettings.Current with { DefaultBorderStyle = LineStyle.Rounded };
        MessageBoxSettings.Current = MessageBoxSettings.Current with { DefaultBorderStyle = LineStyle.Rounded };

        rootView.BorderStyle = LineStyle.Rounded;
        foreach (View view in framedViews)
        {
            view.BorderStyle = LineStyle.Rounded;
        }
    }

    private static IReadOnlyList<string> BuildThemeList()
    {
        IReadOnlyList<string> builtIn = ThemeManager.GetThemeNames();
        string[] preferredOrder =
        [
            DefaultThemeName,
            "Light",
            "Default",
            "TurboPascal 5",
            "Anders",
            "Green Phosphor"
        ];

        var list = new List<string>();
        foreach (string name in preferredOrder)
        {
            if (builtIn.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(name);
            }
        }

        foreach (string name in builtIn)
        {
            if (!list.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(name);
            }
        }

        return list.Count > 0 ? list : [DefaultThemeName];
    }
}
