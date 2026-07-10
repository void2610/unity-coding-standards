---
name: unity-automation-unblock
description: uloop などで Unity Editor を自律操作している最中に、Unity の確認ダイアログ・モーダル・中断メッセージで自動化が無言で固まる／中断するのを、ユーザーにクリックを頼まずコードで解消して進める方法。特に「未保存のシーン変更があるため中断」でテスト実行・Play 入場がブロックされたとき、Domain Reload 後にシーンが dirty になったとき、モーダルで uloop コマンドが返ってこないときに参照する。「Unity 側でボタンを押してください」と頼む前にこの手順で自力解消する。
---

# Unity 自動操作のブロッキング解消（自律進行）

uloop で Unity Editor を外から叩いていると、**Unity 側のダイアログ・モーダル・中断メッセージ**で自動化が止まることがある。原則:

- **ユーザーにクリックを依頼しない**。ブロックの原因を特定し、`uloop-execute-dynamic-code` で Editor API を直接叩いて解消してから続行する。
- モーダルが出ると uloop コマンドが**無言で返ってこない**ことがある。「返事が無い＝ハング」を疑い、原因を潰す。

## 最頻: 「未保存のシーン変更があるため中断」

`uloop-run-tests` / Play 入場が次のように弾かれる:

```
Tests cannot run while the editor has unsaved scene or prefab changes.
Save or discard these changes before running tests. Unsaved changes: Scene: Assets/Scenes/XXX.unity
```

これは **Editor のメモリ上でシーンが dirty** なだけで、ディスク上のシーンファイルには差分が無いことが多い（Domain Reload・アセット再インポート・自動操作の副作用で dirty になる）。

### 解消手順

1. **ディスクに実差分が無いか確認**（あるなら勝手に破棄しない）:
   ```bash
   git status --short "Assets/Scenes/XXX.unity"
   ```
   出力が空 = メモリ上 dirty のみ → 破棄して安全。差分がある = ユーザーの WIP の可能性 → 破棄前に確認する。

2. **シーンを開き直して in-memory の変更を破棄**（`uloop-execute-dynamic-code`）:
   ```csharp
   var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
   UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
       scene.path, UnityEditor.SceneManagement.OpenSceneMode.Single);
   ```
   これで dirty フラグが落ち、テスト実行・Play 入場が通る。

3. **意図した変更を残したい場合**は破棄でなく保存する:
   ```csharp
   var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
   UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
   ```

> Play 入場メニューが「未保存なら中断」する安全設計（例: `PlaySceneMenu` 系の MenuItem）を持つプロジェクトでも、上記の reopen で dirty を落とせば同じく通る。

## その他のブロッキングと対処

- **確認/警告のモーダル（保存する? 破棄する? 上書きする?）が出て uloop が返らない**
  - モーダルを出す API（`EditorUtility.DisplayDialog` 等）を**踏む経路自体を避ける**。目的の状態変更は dynamic code で直接行う（例: ダイアログ経由の保存でなく `SaveScene` / `AssetDatabase.SaveAssets` を直接呼ぶ）。
  - すでに出てしまった場合は `uloop-focus-window` で Editor を前面化し、`uloop-screenshot` で状況を撮って何が待っているか確認してから、原因の操作を dynamic code 版に置き換えて再実行する。

- **Domain Reload 待ちで次コマンドが早すぎて失敗する**
  - `uloop-compile` は `--wait-for-domain-reload true` を付け、リロード完了を待ってから後続（配線・テスト）へ進む。

- **Game View 非表示でランタイム描画・スクショが取れない / `WaitForEndOfFrame` 系がホスト非アクティブで進まない**
  - スクショや描画確認の前に `uloop-focus-window`（対象は Game）で前面化する。
  - CLI 駆動のランタイム検証では `Application.runInBackground = true` を先に入れて PlayerLoop の凍結を防ぐ（非フォーカス時にフレームが進まないとフェード等の tween が進行しない）。

## 鉄則

- **「Unity でボタンを押してください」「Editor を開いて保存してください」と頼まない**。まず原因（dirty / モーダル / 非アクティブ）を特定し、`uloop-execute-dynamic-code` か対応する `uloop-*` skill で自力解消する（→ `uloop-guide` skill）。
- 破棄系の操作（シーン reopen・アセット破棄）は、**ディスクの実差分をコマンドで確認**してから行う。ユーザーの未コミット WIP を巻き込まない。
