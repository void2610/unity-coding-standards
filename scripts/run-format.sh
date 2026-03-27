#!/usr/bin/env bash

set -euo pipefail

readonly PROJECT_ROOT="$(pwd)"
readonly FORMAT_PROJECT="FormatCheck.csproj"

usage() {
    cat <<'EOF'
Usage:
  scripts/run-format.sh
  scripts/run-format.sh --verify-no-changes

Run this from the Unity project root.

What it does:
  - runs `dotnet format analyzers FormatCheck.csproj --severity warn --verbosity diagnostic`
  - runs `dotnet format whitespace FormatCheck.csproj --verbosity diagnostic`
  - runs `dotnet format style FormatCheck.csproj --severity warn --verbosity diagnostic`

Options:
  --verify-no-changes
      Runs all three checks in verification mode for CI.

This script exists to prevent partial formatting runs.
EOF
}

ensure_project_root() {
    if [[ ! -f "${PROJECT_ROOT}/${FORMAT_PROJECT}" ]]; then
        echo "error: ${FORMAT_PROJECT} が見つかりません。" >&2
        echo "       Unity プロジェクトルートで実行してください。" >&2
        exit 1
    fi
}

main() {
    local verify_flag=""

    if (($# > 0)); then
        case "$1" in
            -h|--help)
                usage
                exit 0
                ;;
            --verify-no-changes)
                verify_flag="--verify-no-changes"
                ;;
            *)
                echo "error: 不明なオプションです: $1" >&2
                usage >&2
                exit 1
                ;;
        esac
    fi

    ensure_project_root

    echo "run: dotnet format analyzers ${FORMAT_PROJECT} ${verify_flag} --severity warn --verbosity diagnostic"
    dotnet format analyzers "${FORMAT_PROJECT}" ${verify_flag:+"${verify_flag}"} --severity warn --verbosity diagnostic

    echo "run: dotnet format whitespace ${FORMAT_PROJECT} ${verify_flag} --verbosity diagnostic"
    dotnet format whitespace "${FORMAT_PROJECT}" ${verify_flag:+"${verify_flag}"} --verbosity diagnostic

    echo "run: dotnet format style ${FORMAT_PROJECT} ${verify_flag} --severity warn --verbosity diagnostic"
    dotnet format style "${FORMAT_PROJECT}" ${verify_flag:+"${verify_flag}"} --severity warn --verbosity diagnostic
}

main "$@"
