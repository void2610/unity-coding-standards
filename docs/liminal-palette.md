# LiminalPalette（汎用デバッグコンソール・またはそのコマンド群を呼び出す手段 / `liminal-*`）

LiminalPaletteはUnityに汎用デバッグコンソールを追加し、特定の属性をつけたゲーム内APIをコマンドという単位で簡単に実行することができるOSSライブラリである。
エディタウィンドウ・ランタイムUI・liminal-cli(HTTP API経由)でコマンドを実行したりその結果、ログ等を確認することができる。
ゲーム内の `[LiminalCommand]` / `[LiminalScenario]` / `[LiminalObservableField]` を HTTP API 経由で直接叩くための `liminal-*` skill 群が提供されている。
**`uloop-execute-dynamic-code` でゴリ押しする前に、まず該当する LiminalCommand があるかを必ず確認する**。

> **LiminalPalette はこのリポジトリのオーナー (void2610) 自身が開発しているライブラリ。**
> 使い勝手が悪い・API が足りない・バグっぽい挙動がある等の問題に当たった場合は、クライアント側（このゲーム側）で workaround を組む前に、**ライブラリ側の修正・API 追加を提案 / 実施する選択肢も常に検討する**こと。属性が無いから叩けない、引数の型変換が回りくどい、scenario の表現力が足りない、といった話は基本的にライブラリ側で直すほうが筋がよい。

## 基本方針：検証はすべてシナリオ化して資産にする

LiminalPalette を使う最大の目的は、**個別の動作確認をその場で消費して終わらせず、再実行可能な `[LiminalScenario]` として残し続けること**。これにより、

- バグ修正・新機能追加・調整、どんな作業でも「今回の検証手順」をシナリオに足すだけで、
- **回帰テスト・統合テストのコーパスが副作用的に勝手に増えていく**。
- 「検証作業」と「テスト資産の追加」を別タスクにしない。前者を後者の形式で書く、というだけ。

運用ルール：

- **その場限りの `liminal-execute` 連打で確認を済ませない**。同じ手順を 2 回踏みそうな気配があれば、迷わず `[LiminalScenario]` に切り出す。
- **バグを直したら、その再現手順をシナリオにする**。同じ退行が二度と通らないようにする最小コスト。
- **新機能を実装したら、その happy path と境界値をシナリオにする**。実装と同じ PR で増やす。
- **シナリオは細かく多く** で良い。1 シナリオ 1 確認の粒度で、`liminal-run-scenario "Battle/*"` のような glob で束ねて回す。
- **シナリオが書けない = 観測点／コマンドが足りない**サイン。`[LiminalObservableField]` / `[LiminalCommand]` を生やすか、LiminalPalette 側に汎用 API を足す。

## 積極的に使うユースケース

- **疎通確認**: 何か動かない時はまず `liminal-find-port`（health / doctor）で LP サーバーの生存と `mode` (`editor` / `runtime`) を確認。`connection refused` の時はまずここから。
- **コマンド・状態の探索**: `liminal-list-commands` で呼べるコマンドと引数スキーマを確認 → `liminal-get-state` で `[LiminalObservableField]` の現在値（HP, 位置, シーン名など）を読む。手で Inspector を覗く前にこれ。
- **単発アクション**: `liminal-execute` で「HP を 1 にする」「特定座標に敵を spawn」「シーン遷移」「フラグ操作」等を 1 行で実行。**ただし同じ手順を再実行する見込みがあれば必ずシナリオ化する**（上記「基本方針」参照）。
- **シナリオ実行 / 回帰テスト**: 複数ステップの統合テスト・回帰テストは `liminal-list-scenarios` → `liminal-run-scenario` で `[LiminalScenario]` を叩く。`spawn → wait → assert` のような流れを skill 1 回でまとめられ、fail-fast で結果が返る。glob (`"Battle/*"`) で複数を一括実行可能。CI 用途では `--report PATH` で JUnit XML を出す。**uloop の input 録画再生 (`record-input`/`replay-input`) や simulate 系は使わず、テスト内容はシナリオとしてコードで表現する**こと（再現性とレビュー容易性のため）。
- **検証 → シナリオ化のループ**: 動作確認を `liminal-execute` 単発で済ませた場合でも、検証が終わった時点でその手順を `[LiminalScenario]` に昇格させる。これを習慣化することで、機能追加と引き換えにテスト本数が単調増加していく。
- **before/after 検証**: `liminal-get-state` → `liminal-execute` → `liminal-get-state` で副作用を観測する。「変更後にゲームを起動して目視で確認してください」と頼まない。
- **実行履歴の確認**: 失敗したコマンドの引数を再現したい時は `liminal-get-logs` で InvocationStore を漁る。Unity Console とは別物（Console は `uloop-get-logs`）。
- **新機能を実装した直後**: 該当機能に `[LiminalCommand]` / `[LiminalObservableField]` を生やしておくと、以後の自動テストで叩けるようになる。新規ゲームプレイ系のクラスを足したら属性を付けることを検討する。

## 判断フロー（迷ったとき）

1. ゲーム内ロジックを呼びたい / 状態を観測したい → **`liminal-*` を最優先**。
2. 該当する `[LiminalCommand]` / `[LiminalObservableField]` が無い → **属性を生やすことをまず検討**。ゲーム側に追加するか、LiminalPalette 側に汎用 API を生やすかも合わせて考える。
3. Editor 自体の操作（コンパイル・シーン編集・Inspector ワイヤリング）→ **`uloop-*`**（[`uloop.md`](./uloop.md) 参照）。
4. それでも無理な場合のみ `uloop-execute-dynamic-code` で C# を直接流す。
5. ファイル編集・コード生成は通常通り Read / Edit / Write。

## 参考

- skill 内蔵 references（例: `references/type-conversion.md`）
- `liminal-overview` skill — 全 `liminal-*` skill のエントリーポイント
