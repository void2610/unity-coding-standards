# unity-coding-standards

Unity プロジェクト向けの共有コーディング規約リポジトリです。
カスタム Roslyn アナライザー、`.editorconfig`、`Directory.Build.props`、`Directory.Build.targets`、`FormatCheck.csproj` をまとめて配布します。

## ルール一覧

| ID | カテゴリ | 重大度 | 説明 |
|---|---|---|---|
| VUA1001 | Design | Warning | `[SerializeField]` フィールドに対する防御的 null チェックを禁止 |
| VUA1002 | Design | Warning | C# 標準イベント/デリゲート禁止（R3 の `Subject<T>` を使用。報告のみ・自動修正なし＝呼び出し側を含む手動置換が必要） |
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
このスクリプトは `.editorconfig`、`Directory.Build.props`、`Directory.Build.targets`、`FormatCheck.csproj` の symlink に加えて、共有 reusable workflow を呼ぶ `.github/workflows/format-check.yml` も作成します。

3. 必要なら個別にアナライザー DLL をビルドします:

```bash
dotnet build -c Release
```

4. 共有規約を適用した状態で `dotnet format` を実行します:

```bash
./unity-coding-standards/scripts/run-format.sh
```

個別コマンドの実行漏れを防ぐため、LLM や自動化からは `run-format.sh` の利用を推奨します。
CI では次のように検証モードで実行できます。

```bash
./unity-coding-standards/scripts/run-format.sh --verify-no-changes
```

## 共有ファイル

- `config/.editorconfig`: 命名規則、C# style、フォーマット設定
- `config/Directory.Build.props`: Analyzer DLL の参照設定
- `config/Directory.Build.targets`: Rider / MSBuild 向けの C# 言語バージョン最終上書き
- `config/FormatCheck.csproj`: `dotnet format` 用の共有プロジェクト
- `.github/workflows/`: Unity プロジェクトから呼ばれる reusable workflow 群（フォーマット検証 / Unity テスト / WebGL ビルド & デプロイ / Steam デプロイ）。一覧と使い方は [`.github/workflows/README.md`](.github/workflows/README.md) を参照
- `.github/actions/`: 上記 workflow 内で使う composite action 群（Unity セットアップ / WebGL ビルド / Discord 通知 / sops 復号）。詳細は [`.github/actions/README.md`](.github/actions/README.md)
- `scripts/init-unity-project.sh`: 新規 Unity プロジェクト向け初期化スクリプト
- `scripts/run-format.sh`: analyzers / whitespace / style をまとめて実行するスクリプト（CI 用の `--verify-no-changes` 対応）
- `.claude-plugin/marketplace.json`: このリポを Claude Code プラグイン・マーケットプレイスとして宣言（[Claude Code プラグイン](#claude-code-プラグイン-unity-共通-skill)参照）
- `plugins/unity-standards/`: Unity 共通 Claude Code skill を配布するプラグイン本体
- `docs/`: skill 運用ガイドの索引（本体は `plugins/unity-standards/skills/` の各 `SKILL.md`）

## Claude Code プラグイン: Unity 共通 skill

このリポジトリは Claude Code の **プラグイン・マーケットプレイス** も兼ねています。Unity 共通の Claude Code skill（コーディング規約 / LiminalPalette 運用ガイド / uloop 運用ガイド）を、各プロジェクトへ個別配置せずプロジェクト横断で使い回せます。

### 導入手順

利用側の各プロジェクトで一度だけ実行します（Claude Code 上のスラッシュコマンド）:

```
/plugin marketplace add void2610/unity-coding-standards
/plugin install unity-standards@void2610-unity
```

インストールスコープは user（全プロジェクト）/ project / local から選択できます。skill を更新したいときはこのリポジトリを直すだけで、`/plugin marketplace update` 後に全プロジェクトへ反映されます。

### 含まれる skill

| skill | 内容 |
|---|---|
| `unity-standards:unity-coding-standards` | VUA ルール（アナライザー）に沿った Unity C# コーディング規約 |
| `unity-standards:liminal-palette-guide` | LiminalPalette（`liminal-*`）の運用方針。ランタイム検証をシナリオ資産化する運用ルール |
| `unity-standards:uloop-guide` | Unity Editor 操作（`uloop-*`）の運用方針 |
| `unity-standards:prefab-view` | Unity の UI View を Prefab + SerializeField で構築する標準手順 |
| `unity-standards:unity-automation-unblock` | uloop 自動操作中の Unity ダイアログ・モーダル・中断をコードで解消して自律進行する方法 |

skill の実体は [`plugins/unity-standards/skills/`](plugins/unity-standards/skills/) にあります。

> **注意 / 補足**
> - `liminal-*` / `uloop-*` **skill そのもの**（`liminal-execute` 等）は `liminal-palette` パッケージ側に同梱・配布されるもので、このプラグインには **含めません**。このプラグインが配布するのは「それらをいつ・どう使うか」の運用ガイド skill です。パッケージ同梱 skill との二重配布・上書き衝突を避けるための切り分けです。
> - Claude Code on the web（使い捨てコンテナ）には個人設定・プラグインが自動同期されない可能性があります。web でも確実に効かせたいプロジェクトは、リポジトリの `.claude/skills/` にコミットする方式との併用も検討してください。

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
