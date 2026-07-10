---
name: unity-restart
description: color-recollection の Unity Editor を起動/再起動し、起動時にブロックするダイアログ (Safe Mode / API Update / Import 等) を検出して捌くスキル。トリガー: 「Unity 再起動して」「Unity 起動して」「Unity が固まってる/応答しない」「パッケージ差し替え後に反映されない」「ダイアログ出てる?」等。git package 差し替え・コンパイルエラー・凍結からの復帰に使う。
---

# unity-restart — Unity 起動/再起動 + ダイアログ対応

Unity は **manifest / package を変えても focus では再解決/再コンパイルしない**ことが多く、
凍結したら**再起動が一番速くて確実**。ただし起動時にモーダルダイアログが出て止まることが
あるので、必ず「起動 → ダイアログ検出 → 捌く」までを一連で行う。

## 最重要ルール: 1 分の壁

**正常なら Unity は起動〜プロジェクト読み込み〜コンパイルまで遅くても 1 分で main window が
操作可能**になる。1 分経っても先に進まないなら **ハングではなく高確率でダイアログ待ち**。
**ポーリングを続けず、即ダイアログを見て捌く。** (今日はこれを怠って時間を溶かした)

## 使い方

`scripts/unity.sh <cmd>`:

- `restart` — 稼働中の Unity を kill → 同プロジェクトで再起動。
- `dialogs` — 今出ているウィンドウ/ダイアログを列挙 (main 以外があればそれがブロッカー)。
- `dismiss [button]` — 最前面ダイアログの既定ボタン (省略時 Return) を押して閉じる。

手動の要点:

- **PID 特定**: `ps -eo pid,command | grep "[U]nity.app/Contents/MacOS/Unity.*color-recollection"`
- **再起動**:
  ```
  kill <PID>; sleep 4; kill -9 <PID> 2>/dev/null
  nohup /Applications/Unity/Hub/Editor/<ver>/Unity.app/Contents/MacOS/Unity \
        -projectPath /Users/shuya/Documents/GitHub/color-recollection >/tmp/unity.log 2>&1 &
  ```
  Hub 経由で license 引数付きの子プロセス (別 PID) に化けるのは**正常**。
- **ダイアログ検出**:
  ```
  osascript -e 'tell application "System Events" to get name of every window \
    of (first process whose unix id is <PID>)'
  ```
  `MainScene - color-recollection ...` だけなら OK。それ以外の窓 = ブロッカー。

## よく出るダイアログと対応

| ダイアログ | 意味 | 対応 |
|---|---|---|
| **Corrupted Library Detected** | boot 中の強制終了で `Library/` が破損 | **`Rebuild Library` をクリック**。これが licensing 直後で止まる典型の正体。ボタンは AX 経由で取れる (下記) |
| **Enter Safe Mode?** | コンパイルエラーで起動 | 通常は `Ignore` (エラーごと開く)。原因コードを直すのが本筋 |
| **API Update Required** | 旧 API 検出 | 目的次第。基本 `No` (勝手に書き換えさせない) |
| **Importing / Hold on** | アセット取り込み中 (モーダルでない進捗) | **待つ**。ダイアログではないので dismiss 不要 |
| **Opening Project / Upgrade** | プロジェクト変換 | 意図通りなら `Confirm`、不明ならユーザーに確認 |

> **`Access token is unavailable` はライセンス失効ではない** (無害な警告)。その直後に
> `Licensing is initialized` が出ていれば licensing は成功。ここで止まって見えるのは
> たいてい**その先の Corrupted Library / Safe Mode ダイアログがスプラッシュ裏に隠れている**だけ。
> ウィンドウ名が空 (`{, }`) で 1 分進まなければ**スクショを撮って実物を見る** (`screencapture -x`)。

- **`kill -9` で boot 中の Editor を落とすと Library が壊れ、次回起動が Corrupted Library
  ダイアログで止まる。** 再起動は必ず `kill` (SIGTERM) を先に、`-9` は最終手段。
- ダイアログのボタンは AX で列挙・クリックできる:
  ```
  osascript -e 'tell application "System Events" to get name of every button of window 1 of (first process whose unix id is <PID>)'
  osascript -e 'tell application "System Events" to click (first button whose name is "Rebuild Library") of window 1 of (first process whose unix id is <PID>)'
  ```
  既定ボタンは Return、キャンセルは Escape。
- **破壊的な選択 (API Update の Yes / プロジェクト Upgrade 等) は独断で押さない。**ユーザーに確認する。

## ログの取り違え注意

複数 Unity インスタンス (novel-kit / the-garden 等) が同居すると `Editor.log` /
`Editor-prev.log` が別プロジェクトを指す。`lsof -p <PID>` で対象 PID が実際に開いている
ログを確認してから読むこと。
