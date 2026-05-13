# uloop（Unity Editor 操作）

Unity Editor を外から叩くための `uloop-*` skill 群。**開発ワークフロー内のエディタ操作・単発の確認作業** に使う。ランタイムの動作確認やテストには使わない（→ [`liminal-palette.md`](./liminal-palette.md)）。

## 積極的に使うユースケース

- **コード編集後の検証**: `uloop-compile` → `uloop-get-logs` でエラー / 警告を即確認。エラーが出たまま「完了」と報告しない。
- **テスト実行**: `uloop-run-tests` で EditMode / PlayMode テストを走らせ、失敗時は NUnit XML から原因を読む。
- **シーン構造の把握**: 新規アセットを触る前に `uloop-get-hierarchy` / `uloop-find-game-objects` で既存構造を確認。GameObject 名やパスを推測で書かない。
- **参照ワイヤリング・SerializeField 設定**: Inspector の手作業を頼まずに `uloop-execute-dynamic-code` で `SerializedObject` 経由で設定する。Prefab 生成・コンポーネント追加・マテリアル割り当てもここで完結させる。
- **画面の確認**: `uloop-screenshot` で Game View / Scene View を撮って結果確認。先に `uloop-focus-window` で対象ウィンドウを前面にする。
- **メニュー操作**: ビルド・アセット再インポート等は `uloop-execute-menu-item` で済ませる。
- **ノイズ除去**: 重要な検証の直前は `uloop-clear-console` でログを空にしてから走らせる。
- **Play モードの起動 / 停止**: `uloop-control-play-mode` で切り替えのみ行う。Play 中のゲーム操作は LiminalPalette で扱う。

## 使わない skill

ランタイム（Play モード中）のゲーム動作確認・回帰テストは LiminalPalette の責務なので、以下は使用しない。

- **`uloop-simulate-keyboard` / `uloop-simulate-mouse-input` / `uloop-simulate-mouse-ui`**: 入力の直接シミュレーションは使わない。同等のことは `[LiminalCommand]` を生やして `liminal-execute` / `liminal-run-scenario` で表現する。
- **`uloop-record-input` / `uloop-replay-input`**: 回帰テストは録画再生ではなく **LiminalPalette のシナリオ (`[LiminalScenario]`)** として書き、`liminal-run-scenario` で実行する。

## 注意点

- `uloop-execute-dynamic-code` はファイル I/O やスクリプト生成には使わない。それらは Read / Write / Edit を使う。
- スクリーンショットを撮る前に `uloop-focus-window` で対象ウィンドウを前面に出す。

## LiminalPalette との使い分け

ゲーム内ロジックの呼び出しや `[LiminalObservableField]` 状態観測は `liminal-*` を優先する。`uloop-execute-dynamic-code` で C# を直接流すのは、Editor 操作で代替できず、かつ該当する `[LiminalCommand]` も無い場合の最後の手段。詳細は [`liminal-palette.md`](./liminal-palette.md) を参照。
