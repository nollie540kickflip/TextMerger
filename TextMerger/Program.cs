using System.Text;
using Terminal.Gui.App;
using TextMerger.Models;
using TextMerger.Services;
using TextMerger.UI;

namespace TextMerger;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // CLI引数 (--cli) が指定された場合はヘッドレス実行にも対応
        if (args.Length > 0 && args[0] == "--cli")
        {
            return await RunCliAsync(args[1..]);
        }

        using IApplication app = Application.Create().Init();
        using var mainWindow = new MainWindow(app);
        app.Run(mainWindow);
        return 0;
    }

    private static async Task<int> RunCliAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("使用法: TextMerger --cli <入力フォルダ> <出力ファイル> [検索パターン] [ヘッダスキップ行数]");
            return 1;
        }

        string folder = args[0];
        string output = args[1];
        string pattern = args.Length >= 3 ? args[2] : "*.txt;*.csv";
        int headerLines = args.Length >= 4 && int.TryParse(args[3], out int h) ? h : 1;

        var scanner = new FileScanner();
        IReadOnlyList<FileItem> files = scanner.ScanFiles(folder, pattern, includeSubdirectories: false, excludeFilePath: output);

        if (files.Count == 0)
        {
            Console.Error.WriteLine("対象ファイルが見つかりませんでした。");
            return 1;
        }

        var mergeService = new TextMergeService();
        MergeResult result = await mergeService.MergeFilesAsync(
            files,
            new MergeOptions
            {
                OutputFilePath = output,
                SkipHeaderOnSubsequentFiles = headerLines > 0,
                HeaderLineCount = headerLines
            });

        Console.WriteLine(
            $"結合完了: {result.MergedFileCount} ファイル, {result.TotalLinesWritten} 行 " +
            $"(出力文字コード: {EncodingOption.GetShortName(result.EffectiveOutputEncoding)}) -> {result.OutputFilePath}");
        return 0;
    }
}
