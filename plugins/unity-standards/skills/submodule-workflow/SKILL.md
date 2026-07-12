---
name: submodule-workflow
description: 自作ライブラリ submodule (my-unity-utils / my-unity-settings / unity-coding-standards / novel-kit 等) を修正するときの Git 運用方針。submodule 側には PR を作らず main へ直接マージし、PR は利用側プロジェクトの submodule ポインタ更新として出す。ブランチの切り方、コミット・マージ・ポインタ更新の順序、巻き込み禁止ルール、reusable workflow (@main 参照) の反映タイミングを定める。ライブラリと本体の 2 リポジトリにまたがる変更を扱うときに必ず参照する。
---

# submodule 運用方針

自作ライブラリは利用側プロジェクトに git submodule として組み込まれている (例: color-recollection の `my-unity-utils` は `Assets/Scripts/Utils` に symlink)。ライブラリを修正するときの Git フローを定める。

## 原則: PR は利用側プロジェクトに 1 本だけ

- **submodule リポジトリには PR を作らない**。修正は submodule 側 main へ直接コミット (またはブランチを切って即 squash マージ) する。
- **レビュー対象の PR は利用側プロジェクトに出す**。内容は「submodule ポインタの更新 + (あれば) 利用側の追随変更」。修正の Why・検証手順はこの PR の本文に書く。
- 理由: submodule 単体ではコンパイル・テスト・実機確認ができず、レビューの実効性がない。検証可能な単位 = 利用側プロジェクトのポインタ更新 PR。

## 手順

1. **submodule 側**: `git fetch origin main` → `git switch --no-track -c <branch> origin/main` で最新 main 起点のブランチを切る (`origin/main` を upstream にしない。誤 push 事故防止)。
2. 修正 → 利用側プロジェクトでコンパイル・フォーマット・テストを通す (submodule 単体ではビルドできない)。
3. submodule 側 main へ反映 (直コミット push または即マージ)。ポインタは **main 上のコミット SHA** を指すこと。ブランチ上の SHA を指すと、ブランチ削除でポインタが宙に浮く。
4. **利用側**: 最新 main からブランチを切り、submodule ポインタ更新をコミットして PR を作る。
5. 利用側 PR の CI (テスト / プレビュービルド) が実質的な検証。実機確認が必要な修正はプレビュー URL で確認する。

## 巻き込み禁止

- ポインタ更新コミットに **利用側の無関係な作業中ファイルを含めない**。`git add <明示パス>` のみ使う (`git add .` / `-A` 禁止)。
- submodule 側にも利用側の生成物 (`outputs/` 等) を置かない。symlink 経由で Unity が `.meta` を生成してアセット汚染するため、置いてしまったら即削除する。

## unity-coding-standards (reusable workflow) 固有の注意

- 利用側の GitHub Actions は workflow / action を **`@main` 参照**している。main へ push した瞬間に全利用プロジェクトへ反映されるため、submodule ポインタの更新を待たずに CI 挙動が変わる。
- 逆に、利用側 workspace にチェックアウトされている submodule の中身は CI 挙動に影響しない (CI は `@main` を fetch する)。ローカルの submodule 位置はポインタ整合のためだけに合わせる。
- 破壊的変更 (input の削除・リネーム) は全利用プロジェクトを同時に壊す。optional input の追加で後方互換を保つ。

## 複数 submodule にまたがる修正

- 依存の根 (ライブラリ) から葉 (利用側) の順に main へ反映し、利用側 PR で全ポインタをまとめて更新する。
- 例: unity-coding-standards の workflow input 追加 → my-unity-utils のビルドスクリプト対応 → 利用側 PR (ポインタ更新 + workflow 設定変更)。
