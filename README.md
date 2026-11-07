# TextMerger

C# (`.NET 10`) と **Terminal.Gui v2 (`2.5.0`)** で作成した、テキストファイル（CSV / TSV / TXT 等）結合 TUI ツールです。

## 主な機能
- **オリジナル文字コードの自動判別・維持（デフォルト）**
  - 各ファイルの先頭バイト列および BOM から `UTF-8 (BOMなし/あり)`, `Shift_JIS (CP932)`, `EUC-JP`, `UTF-16 LE`, `UTF-16 BE` を自動判別します。
  - デフォルトでは入力文字コードを自動判別し、1つ目のファイルの文字コードで結合ファイルを出力します。
  - 入出力それぞれの文字コードを明示的に指定して変換（例：`Shift_JIS` → `UTF-8 (BOMあり)`）することも可能です。
- **2ファイル目以降のヘッダ行スキップ**
  - CSV や TSV などを結合する際、1ファイル目のヘッダ行だけを残し、2ファイル目以降の先頭 $N$ 行（デフォルト 1 行）をスキップして結合できます。
- **ファイル検索・個別選択・順序変更**
  - 複数ワイルドカード（例: `*.txt;*.csv;*.tsv`）およびサブフォルダ検索に対応。
  - ファイル一覧で `Space` キーまたはクリックによる個別選択、および `[↑ 上へ]` / `[↓ 下へ]` ボタンによる結合順序の入れ替えが可能です。
- **UIテーマ切替・丸角枠線デザイン**
  - 初期テーマとして落ち着いた `Dark` テーマと丸角枠線（`LineStyle.Rounded`）を適用。
  - 画面上部から `Dark` / `Light` / `Default` / `TurboPascal 5` / `Anders` / `Green Phosphor` の6種類の組み込みテーマをリアルタイムに切り替えられます。

## 起動方法

### TUI モード（対話画面）
```bash
dotnet run --project TextMerger
```

### CLI モード（ヘッドレス実行）
```bash
dotnet run --project TextMerger -- --cli <入力フォルダ> <出力ファイル> [検索パターン] [ヘッダスキップ行数]
# 例:
dotnet run --project TextMerger -- --cli ./samples ./merged.csv "*.csv" 1
```

## テストの実行
```bash
dotnet test
```
