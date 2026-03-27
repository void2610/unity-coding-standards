# unity-coding-standards

Unity プロジェクト向けの共有コーディング規約リポジトリです。
カスタム Roslyn アナライザー、`.editorconfig`、`Directory.Build.props`、`FormatCheck.csproj` をまとめて配布します。

## ルール一覧

| ID | カテゴリ | 重大度 | 説明 |
|---|---|---|---|
| VUA1001 | Design | Warning | `[SerializeField]` フィールドに対する防御的 null チェックを禁止 |
| VUA1002 | Design | Warning | C# 標準イベント/デリゲート禁止（R3 の `Subject<T>` を使用） |
| VUA1003 | Design | Warning | `if(IsActive()) Cancel()` ではなく `TryCancel()` を使用 |
| VUA1004 | Design | Warning | `StartCoroutine` の使用を禁止（UniTask などの代替を使用） |
| VUA2001 | Naming | Warning | `[SerializeField]` フィールドに `_` プレフィックスを付けない |
| VUA2002 | Naming | Warning | private フィールドに `_` プレフィックス必須 |
| VUA3001 | Style | Warning | 単一文の public メソッドには式本体 (`=>`) を使用 |
| VUA3002 | Style | Warning | クラスメンバーの宣言順序を強制 |
| VUA4001 | Documentation | Warning | トップレベル enum メンバーに `/// <summary>` コメント必須 |

## 使用方法

### ビルド

```bash
dotnet build -c Release
```

ビルド成果物は `src/Void2610.Unity.Analyzers/bin/Release/netstandard2.0/Void2610.Unity.Analyzers.dll` に出力されます。

### Unity プロジェクトへの導入

1. このリポジトリを Git サブモジュールとして追加します:

```bash
git submodule add <repository-url> unity-coding-standards
```

2. プロジェクトルートで初期化スクリプトを実行します:

```bash
./unity-coding-standards/scripts/init-unity-project.sh
```

既存ファイルがあるプロジェクトの移行はこのスクリプトの対象外です。新規セットアップ専用です。

3. 必要なら個別にアナライザー DLL をビルドします:

```bash
dotnet build -c Release
```

4. 共有規約を適用した状態で `dotnet format` を実行します:

```bash
./unity-coding-standards/scripts/run-format.sh
```

個別コマンドの実行漏れを防ぐため、LLM や自動化からは `run-format.sh` の利用を推奨します。

## 共有ファイル

- `config/.editorconfig`: 命名規則、C# style、フォーマット設定
- `config/Directory.Build.props`: Analyzer DLL の参照設定
- `config/FormatCheck.csproj`: `dotnet format` 用の共有プロジェクト
- `scripts/init-unity-project.sh`: 新規 Unity プロジェクト向け初期化スクリプト
- `scripts/run-format.sh`: analyzers / whitespace / style をまとめて実行するスクリプト

## ルールの抑制

特定の箇所でルールを無効化したい場合は `#pragma warning disable` を使用します:

```csharp
#pragma warning disable VUA1001
// ここでは警告が出ない
#pragma warning restore VUA1001
```

## テスト

```bash
dotnet test -c Release
```
