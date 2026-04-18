# Composite Actions

reusable workflow の内部で共通化されているビルディングブロック。基本的にこれらを呼び出し側リポから直接使うことは少ないが、独自 workflow を組む場合に利用できる。

| Action | 用途 |
|---|---|
| [`setup-unity-project`](setup-unity-project/) | self-hosted ランナーでのチェックアウト・Unity インストール検証・NuGet 復元。`unity-path` を output として提供 |
| [`unity-webgl-build`](unity-webgl-build/) | Unity を batchmode で起動して WebGL ビルドを実行（ロックファイル除去、ログ tail、終了コード判定を含む） |
| [`decrypt-secrets`](decrypt-secrets/) | `unity-coding-standards/secrets/ci.yaml` を sops で復号し、Cloudflare の認証情報を output として返す |
| [`notify-discord`](notify-discord/) | デプロイ完了を Discord Webhook に通知する。PR 番号指定時はプレビュー用フォーマットで送信 |

## 代表的な使い方

`setup-unity-project` → `unity-webgl-build` の流れが基本。`setup-unity-project` の output を後続ステップ/action で再利用することで、`UNITY_PATH` の文字列組み立てが 1 箇所に集約される。

```yaml
- name: Setup Unity project
  id: setup
  uses: void2610/unity-coding-standards/.github/actions/setup-unity-project@main
  with:
    unity-version: 6000.3.10f1

- name: Build WebGL
  uses: void2610/unity-coding-standards/.github/actions/unity-webgl-build@main
  with:
    unity-path: ${{ steps.setup.outputs.unity-path }}
    execute-method: CiBuild.BuildWebGL
    build-output-path: CIBuilds/WebGL/build

- name: Notify Discord
  uses: void2610/unity-coding-standards/.github/actions/notify-discord@main
  with:
    discord-webhook-url: ${{ secrets.DISCORD_WEBHOOK_URL }}
    deploy-url: https://example.com/
    commit-sha: ${{ github.sha }}
```

## 入出力

各 action の詳細な inputs/outputs は個別の `action.yml` を参照。`description` フィールドに用途を記載している。

- [`setup-unity-project/action.yml`](setup-unity-project/action.yml)
- [`unity-webgl-build/action.yml`](unity-webgl-build/action.yml)
- [`decrypt-secrets/action.yml`](decrypt-secrets/action.yml)
- [`notify-discord/action.yml`](notify-discord/action.yml)
