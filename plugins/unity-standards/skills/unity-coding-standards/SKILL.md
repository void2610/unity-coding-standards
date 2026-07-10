---
name: unity-coding-standards
description: void2610 の Unity プロジェクト共通コーディング規約。Unity の C# コード（MonoBehaviour / ScriptableObject / ゲームロジック）を新規作成・編集・レビューするときに参照する。SerializeField の扱い、命名規則（private フィールドの `_` プレフィックス）、R3 の Subject<T> によるイベント、UniTask、式本体メソッド、メンバー宣言順序、enum の summary コメントなど、カスタム Roslyn アナライザー（VUA1001〜VUA4001）で強制しているルールを説明する。
---

# Unity 共通コーディング規約（void2610）

[`unity-coding-standards`](https://github.com/void2610/unity-coding-standards) リポジトリで配布しているカスタム Roslyn アナライザー（`Void2610.Unity.Analyzers`）が強制する規約。Unity プロジェクトの C# を書く／直す／レビューするときは、以下に沿うこと。CI では `run-format.sh --verify-no-changes` でこれらが検証される。

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

## 各ルールの意図と書き方

### Design

- **VUA1001 — SerializeField に防御的 null チェックを書かない**: `[SerializeField]` は Inspector で必ずワイヤリングされる前提。`if (_target == null) return;` のような防御コードは、本来 Inspector 設定漏れとして気づくべきバグを握り潰す。null なら早期に例外で気づかせる。
- **VUA1002 — 標準 event / delegate ではなく R3 の `Subject<T>`**: C# の `event`・`Action`・`Func` を公開通知に使わない。R3 の `Subject<T>` を持ち、外へは `AsObservable()` で公開する。※このルールは報告のみで自動修正されないため、購読側（`+=` / `-=`）も含めて手動で置き換える。
- **VUA1003 — `if (IsActive()) Cancel()` ではなく `TryCancel()`**: 「状態を確認してから操作」の 2 段構えは競合しやすい。`TryXxx()` として 1 メソッドに畳み、内部で状態確認と操作を完結させる。
- **VUA1004 — `StartCoroutine` 禁止**: コルーチンではなく UniTask（`async UniTask` / `CancellationToken`）で非同期を書く。キャンセル・例外・戻り値の扱いが素直になる。

### Naming

- **VUA2001 — SerializeField に `_` を付けない**: `[SerializeField] private int health;`（`_health` にしない）。Inspector に表示される名前を素直に保つ。
- **VUA2002 — private フィールドは `_` 必須**: SerializeField でない純粋な private フィールドは `_camelCase`（例: `private int _counter;`）。VUA2001 と対になり、「`_` の有無」で SerializeField かどうかが一目で分かる。

### Style

- **VUA3001 — 単一文の public メソッドは式本体**: 本体が 1 文だけの public メソッドは `public int Double(int x) => x * 2;` の式本体形式にする。
- **VUA3002 — メンバー宣言順序**: クラス内のメンバーは規定の順序（アクセシビリティ・種別ごと）で並べる。順序は `.editorconfig` / アナライザーの定義に従う。

### Documentation

- **VUA4001 — トップレベル enum メンバーに summary**: トップレベル（ネストしていない）enum の各メンバーに `/// <summary>` を付ける。

## ルールの抑制

局所的に無効化したいときだけ `#pragma warning disable` を使う（濫用しない）:

```csharp
#pragma warning disable VUA1001
// ここでは警告が出ない
#pragma warning restore VUA1001
```

## 導入・フォーマット

リポジトリを submodule として追加し、`scripts/init-unity-project.sh` で `.editorconfig` / `Directory.Build.props` / `Directory.Build.targets` / `FormatCheck.csproj` を symlink する。整形は個別コマンドではなく `scripts/run-format.sh`（analyzers / whitespace / style をまとめて実行）を使う。CI では `run-format.sh --verify-no-changes`。詳細は [リポジトリの README](https://github.com/void2610/unity-coding-standards) を参照。
