#!/usr/bin/env bash
# restart / dialogs / dismiss。詳細は SKILL.md。
set -euo pipefail

CR=/Users/shuya/Documents/GitHub/color-recollection

unity_exe() { ls -d /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity 2>/dev/null | head -1; }
unity_pid() { ps -eo pid,command | grep -i "[U]nity.app/Contents/MacOS/Unity.*color-recollection" | awk '{print $1}' | head -1; }

windows() { # 対象 PID のウィンドウ名一覧
  local pid=$1
  osascript -e "tell application \"System Events\" to get name of every window of (first process whose unix id is $pid)" 2>&1
}

cmd_restart() {
  local pid; pid=$(unity_pid)
  if [ -n "$pid" ]; then echo "kill $pid"; kill "$pid" 2>/dev/null || true; sleep 4; kill -9 "$pid" 2>/dev/null || true; fi
  local exe; exe=$(unity_exe); echo "launch: $exe"
  nohup "$exe" -projectPath "$CR" >/tmp/unity.log 2>&1 &
  # 1 分だけ main window を待つ。超えたらダイアログ疑いで列挙して返す。
  local start=$SECONDS npid=""
  while (( SECONDS - start < 75 )); do
    npid=$(unity_pid); [ -n "$npid" ] && windows "$npid" 2>/dev/null | grep -qi "MainScene" && { echo "ready (pid $npid)"; return 0; }
    sleep 5
  done
  echo "[1min 超] main window 未確立 — ダイアログ疑い:" >&2
  cmd_dialogs >&2
  return 1
}

cmd_dialogs() {
  local pid; pid=$(unity_pid); [ -z "$pid" ] && { echo "Unity 未起動"; return 1; }
  echo "windows (pid $pid): $(windows "$pid")"
  local log; log=$(lsof -p "$pid" 2>/dev/null | awk '{print $NF}' | grep -iE "/Logs/.*Editor.*\.log$" | head -1)
  [ -n "$log" ] && { echo "log: $log"; tail -4 "$log"; }
}

cmd_dismiss() {
  local pid; pid=$(unity_pid); [ -z "$pid" ] && { echo "Unity 未起動"; return 1; }
  local btn=${1:-}
  if [ -n "$btn" ]; then
    osascript -e "tell application \"System Events\" to click button \"$btn\" of window 1 of (first process whose unix id is $pid)"
  else
    osascript -e "tell application \"System Events\" to key code 36" # Return
  fi
  echo "dismissed (${btn:-Return})"
}

case "${1:-}" in
  restart) cmd_restart;;
  dialogs) cmd_dialogs;;
  dismiss) shift; cmd_dismiss "${1:-}";;
  *) echo "usage: $0 restart | dialogs | dismiss [button]" >&2; exit 2;;
esac
