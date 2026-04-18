# Reusable Workflows

void2610 の Unity プロジェクトから `uses:` で呼び出すことを想定した reusable workflow を収録する。呼び出し側は薄いラッパー (`jobs.<name>.uses: void2610/unity-coding-standards/.github/workflows/<file>@main`) を書くだけで済むよう設計している。

| Workflow | 用途 | 実行環境 |
|---|---|---|
| [`ci.yml`](ci.yml) | このリポジトリ自身の Analyzer ビルド・テスト | self-hosted |
| [`format-check.yml`](format-check.yml) | `dotnet format` の verify 実行 | self-hosted |
| [`unity-test.yml`](unity-test.yml) / [`unity-test-self-hosted.yml`](unity-test-self-hosted.yml) | Unity Test Runner の実行 | GitHub-hosted / self-hosted |
| [`steam-deploy.yml`](steam-deploy.yml) / [`steam-deploy-self-hosted.yml`](steam-deploy-self-hosted.yml) | Mac/Win ビルド + Steam アップロード | GitHub-hosted / self-hosted |
| [`webgl-build-self-hosted.yml`](webgl-build-self-hosted.yml) | WebGL ビルド → **Cloudflare Pages** デプロイ | self-hosted |
| [`webgl-build-github-pages-self-hosted.yml`](webgl-build-github-pages-self-hosted.yml) | WebGL ビルド → **GitHub Pages (gh-pages ブランチ push)** デプロイ | self-hosted |
| [`webgl-build-netlify-self-hosted.yml`](webgl-build-netlify-self-hosted.yml) | WebGL ビルド → **Netlify** デプロイ（単一ファイル25MB制限がないためアセットが重いプロジェクト向け） | self-hosted |

## 共通の呼び出し規約

### 想定する self-hosted ランナーラベル

`runner-labels` を受け取るものは JSON 配列の文字列で渡す。

```yaml
runner-labels: '["self-hosted","macOS"]'
```

### 共通 secrets

- `DISCORD_WEBHOOK_URL`（optional）: 指定時は Deploy 完了を通知する。[`../actions/notify-discord`](../actions/notify-discord) 参照
- `SOPS_AGE_KEY`（Cloudflare / Netlify 版で required）: `secrets/ci.yaml` の sops 復号用。Cloudflare Account ID/API Token と Netlify Personal Access Token をここから取り出す。[`../actions/decrypt-secrets`](../actions/decrypt-secrets) 参照
- `NETLIFY_SITE_ID`（Netlify 版のみ required）: リポジトリ固有のため呼び出し側 secret に登録する

### 共通 inputs

| input | 用途 |
|---|---|
| `unity-version` | Unity Editor バージョン（self-hosted ランナー上にインストール済みのものと一致させる）|
| `execute-method` | `-executeMethod` に渡すビルドメソッド（例: `CiBuild.BuildWebGL`）|
| `build-output-path` | ビルド成果物出力先。デフォルト `CIBuilds/WebGL/build` |
| `nuget-restore-method` | NuGetForUnity などで復元が必要なら指定。空文字列で skip |
| `pr-number` / `pr-title` / `pr-url` / `pr-head-sha` | PR 通知用のメタ情報 |

## WebGL 版の使い分け

| 配信先 | 単一ファイル制限 | 月帯域 (無料枠) | 向いているケース |
|---|---|---|---|
| GitHub Pages (`webgl-build-github-pages-self-hosted`) | 100 MB (推奨 50 MB) | 100 GB soft | アセットが軽い小規模プロジェクト |
| Cloudflare Pages (`webgl-build-self-hosted`) | 25 MiB | 無制限 (Free) | 細かくチャンク化できるサイト |
| Netlify (`webgl-build-netlify-self-hosted`) | 実質なし | 100 GB | WebGLの `build.data` が数十〜数百 MB になる Unity プロジェクト |

WebGL はビルド自体が大きくなりがちなため、迷ったら **Netlify 版** が安全。

## 呼び出しサンプル

### Netlify 版

```yaml
# .github/workflows/build.yml
jobs:
  webgl-build:
    uses: void2610/unity-coding-standards/.github/workflows/webgl-build-netlify-self-hosted.yml@main
    with:
      unity-version: 6000.3.10f1
      execute-method: CiBuild.BuildWebGL
      runner-labels: '["self-hosted","macOS"]'
      deploy-alias: ${{ github.event_name == 'pull_request' && format('pr-{0}', github.event.pull_request.number) || '' }}
      pr-number: ${{ github.event.pull_request.number }}
      pr-title: ${{ github.event.pull_request.title }}
      pr-url: ${{ github.event.pull_request.html_url }}
      pr-head-sha: ${{ github.event.pull_request.head.sha || github.sha }}
    secrets:
      SOPS_AGE_KEY: ${{ secrets.SOPS_AGE_KEY }}
      NETLIFY_SITE_ID: ${{ secrets.NETLIFY_SITE_ID }}
      DISCORD_WEBHOOK_URL: ${{ secrets.DISCORD_WEBHOOK_URL }}
```

呼び出し側リポには `CiBuild.BuildWebGL` のような `-executeMethod` 対象が `Assets/Scripts/Editor/` 以下に必要。

### Cloudflare Pages 版

```yaml
jobs:
  webgl-build:
    uses: void2610/unity-coding-standards/.github/workflows/webgl-build-self-hosted.yml@main
    with:
      unity-version: 6000.3.10f1
      execute-method: CiBuild.BuildWebGL
      runner-labels: '["self-hosted","macOS"]'
      cloudflare-project-name: my-project
      deploy-branch: ${{ github.event_name == 'pull_request' && github.head_ref || 'main' }}
    secrets:
      SOPS_AGE_KEY: ${{ secrets.SOPS_AGE_KEY }}
      DISCORD_WEBHOOK_URL: ${{ secrets.DISCORD_WEBHOOK_URL }}
```
