#!/usr/bin/env bash
# ウォームアップ Unity のログにアセンブリ読み込み失敗が残っていたら、テストで意味不明に全滅する前にここで止める。
# 引数: Unity ログのパス
set -euo pipefail
LOG="$1"
if grep -q "will not be loaded due to errors" "$LOG"; then
  echo "::error::Warm-up Unity でアセンブリの読み込みに失敗しています。この状態でテストを走らせると全 fixture が 'No arguments were provided' で落ちます。"
  grep -A1 "will not be loaded due to errors" "$LOG" | head -20
  exit 1
fi
