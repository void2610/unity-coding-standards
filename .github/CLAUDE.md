# CI ガイダンス (Claude 向け)

このディレクトリ配下の workflow / action を変更するときに守る方針。`.github/` 外の Analyzer 本体に関するルールは親 `CLAUDE.md` / `README.md` を参照。

## 構成

- `workflows/*.yml` — 呼び出し側リポから `uses:` される reusable workflow 群
- `actions/<name>/action.yml` — workflow 内で共通化された composite action 群
- `workflows/README.md` / `actions/README.md` — 人間向けの使い方
- このファイル `.github/CLAUDE.md` — Claude 向けの編集ルール

## 新しい workflow / action を足すとき

1. **命名**: `-self-hosted` suffix は self-hosted ランナー専用であることを示す。GitHub-hosted 版と両方作る場合は suffix 有無で揃える（例: `unity-test.yml` / `unity-test-self-hosted.yml`）
2. **input の description は日本語**で書く。既存ファイルのトーンに合わせる
3. **secrets は呼び出し側 secret で受ける**のが原則。共有認証情報（Cloudflare トークン等）のみ `secrets/ci.yaml` に sops で格納する
4. 共通化できそうな shell ブロックを見つけたら **composite action に切り出す**。70 行程度のインラインロジックが 2〜3 箇所に重複したら共通化の目安
5. **呼び出し側リポの `.github/workflows/build.yml` は薄いラッパー**に収まるよう設計する。アプリ固有のロジック（URL 生成など）が膨らむ場合は reusable workflow の input に追加する

## 変更時のチェック

- Reusable workflow の input を変える場合、呼び出し側の破壊的変更になる。削除/リネームは避け、可能なら optional input として追加する
- `setup-unity-project` の output を変更すると既存 workflow が壊れる。`unity-path` は既に参照されているため削除不可
- secrets のキー名変更は sops 再暗号化が必要。手動で `sops` コマンドで `secrets/ci.yaml` を更新する
- Discord 通知の payload フォーマットは `actions/notify-discord/action.yml` 一箇所に集約されている。ビルド/デプロイ workflow 側にインライン展開しない
- YAML 内で `${{ ... }}` の中に `#` を含む文字列（例: `PR #{0}`）を書く場合は、値全体を `"..."` でダブルクォートする（YAML コメント解釈を避けるため）

## Unity 起動の慣習

- `UNITY_PATH` は `setup-unity-project` の output で取得し、再構築しない
- `-batchmode -quit -nographics -projectPath "$GITHUB_WORKSPACE"` は最低限のセット
- WebGL ビルドは `unity-webgl-build` action を使う。直接 `"$UNITY_PATH"` を叩かない
- Unity の終了コードは set +e で捕捉してから tail を出す。エラーログが丸ごと流れないようにする
- self-hosted ランナーでは `rm -f Temp/UnityLockfile` を必ず実行してからビルドを開始する（キャンセルされたジョブのロック残り対策）

## 呼び出し側リポでの使い方

呼び出し側 (`void-red`, 他 Unity プロジェクト) の `.github/workflows/build.yml` は `jobs.<name>.uses:` で reusable workflow を参照するだけに保つ。アプリ固有の処理を増やしたくなったら、まず reusable workflow 側に input として足すことを検討する。
