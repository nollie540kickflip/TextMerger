# TextMerger

C# (`.NET 10`) と **Terminal.Gui v2 (`2.5.0`)** で作成した、テキストファイル（CSV / TSV / TXT 等）結合 TUI ツールです。

## 主な機能
- **オリジナル文字コードの自動判別・維持（デフォルト）**
  - 各ファイルの先頭バイト列および BOM から `UTF-8 (BOMなし/あり)`, `Shift_JIS (CP932)`, `EUC-JP`, `UTF-16 LE`, `UTF-16 BE` を自動判別します。
  - デフォルトでは入力文字コードを自動判別し、1つ目のファイルの文字コードで結合ファイルを出力します。
  - 入出力それぞれの文字コードを明示的に指定して変換（例：`Shift_JIS` → `UTF-8 (BOMあり)`）することも可能です。
- **2ファイル目以降のヘッダ行スキップ**
  - CSV や TSV などを結合する際、1ファイル目のヘッダ行だけを残し、2ファイル目以降の先頭 $N$ 行（デフォルト 1 行）をスキップして結合できます。
- **大容量ファイル（1GB超）対応の定数メモリ・ストリーミング結合**
  - `ArrayPool<char>` の固定長（64KB）バッファを用いたチャンクストリーミングにより、行単位の `string` アロケーションを行わずに結合するため、1GBを超える巨大ファイルや長大行データでもメモリ枯渇（OOM）を起こさず高速に動作します。
- **ファイル検索・個別選択・順序変更**
  - 複数ワイルドカード（例: `*.txt;*.csv;*.tsv`）およびサブフォルダ検索に対応。
  - ファイル一覧で `Space` キーまたはクリックによる個別選択、および `[↑ 上へ]` / `[↓ 下へ]` ボタンによる結合順序の入れ替えが可能です。
- **UIテーマ切替・丸角枠線デザイン**
  - 初期テーマとして落ち着いた `Dark` テーマと丸角枠線（`LineStyle.Rounded`）を適用。
  - 画面上部から `Dark` / `Light` / `Default` / `TurboPascal 5` / `Anders` / `Green Phosphor` の6種類の組み込みテーマをリアルタイムに切り替えられます。

## ビルド方法

### 前提条件
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) がインストールされていること

### 1. 開発用ビルド（通常ビルド）
ローカルの `.NET 10` ランタイムを利用してビルドします。

```bash
# Debug ビルド（開発・デバッグ時）
dotnet build

# Release ビルド（最適化あり）
dotnet build -c Release
```

### 2. OS・環境別の配布用ビルド（単一実行バイナリ出力）
配布先のマシンに `.NET 10` ランタイムがインストールされていなくても単体で動作する、OS・CPUアーキテクチャ別の単一実行ファイル（Self-Contained Single-File）を生成できます。

#### macOS
```bash
# Apple Silicon (M1 / M2 / M3 / M4 - ARM64)
dotnet publish TextMerger -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true

# Intel Mac (x64)
dotnet publish TextMerger -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true
```
- 出力先: `TextMerger/bin/Release/net10.0/<RID>/publish/TextMerger`

#### Windows
```bash
# Windows (64-bit / x64)
dotnet publish TextMerger -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Windows (ARM64)
dotnet publish TextMerger -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=true
```
- 出力先: `TextMerger/bin/Release/net10.0/<RID>/publish/TextMerger.exe`

#### Linux
```bash
# Linux (x64 / Ubuntu, Debian, RHEL 等)
dotnet publish TextMerger -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true

# Linux (ARM64 / AWS Graviton, Raspberry Pi 64-bit 等)
dotnet publish TextMerger -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true
```
- 出力先: `TextMerger/bin/Release/net10.0/<RID>/publish/TextMerger`

> [!TIP]
> 配布先に `.NET 10` ランタイムがインストール済みで、バイナリサイズを小さく抑えたい場合は `--self-contained false` に変更してください。

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

## 詳細ドキュメント
- [機能仕様・エッジケース仕様書 (`docs/SPECIFICATION.md`)](./docs/SPECIFICATION.md)
  - 入出力文字コード決定マトリクス、ヘッダ行スキップ・末尾改行補完のエッジケース仕様、TUIキー操作・CLI引数仕様
- [アーキテクチャ・内部設計書 (`docs/ARCHITECTURE.md`)](./docs/ARCHITECTURE.md)
  - ディレクトリ構造・レイヤー責務、デザインパターン（Mediator + Passive View）、1GB超対応の `ArrayPool<char>` 定数メモリ結合アルゴリズム、文字コード自動判別ロジック

