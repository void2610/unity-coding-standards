#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(pwd)"
readonly FORMAT_PROJECT="FormatCheck.csproj"
# analyzer はスクリプト位置基準で解決する。Unity プロジェクト側の submodule 配置によらず常に同じ場所を指せる。
readonly ANALYZER_PROJECT="${SCRIPT_DIR}/../src/Void2610.Unity.Analyzers/Void2610.Unity.Analyzers.csproj"

usage() {
    cat <<'EOF'
Usage:
  scripts/run-format.sh
  scripts/run-format.sh --verify-no-changes

Run this from the Unity project root.

What it does:
  - builds Void2610.Unity.Analyzers in Release so `dotnet format` uses the latest rules
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

build_analyzer() {
    # analyzer DLL のバージョンが古いと CI と検出ルールが食い違うため、必要時に Release ビルドで揃える。
    # 既に DLL が最新ソースより新しい場合はスキップして、毎回の起動コストを抑える。
    if [[ ! -f "${ANALYZER_PROJECT}" ]]; then
        echo "error: analyzer プロジェクトが見つかりません: ${ANALYZER_PROJECT}" >&2
        echo "       unity-coding-standards サブモジュールが正しくチェックアウトされているか確認してください。" >&2
        exit 1
    fi

    local analyzer_dir
    analyzer_dir="$(dirname "${ANALYZER_PROJECT}")"
    local dll_path="${analyzer_dir}/bin/Release/netstandard2.0/Void2610.Unity.Analyzers.dll"

    # DLL が存在し、配下に DLL より新しいソースが 1 件も無ければビルドをスキップする。
    if [[ -f "${dll_path}" ]]; then
        local newer_src
        newer_src="$(find "${analyzer_dir}" \( -name '*.cs' -o -name '*.csproj' \) -newer "${dll_path}" -print -quit 2>/dev/null || true)"
        if [[ -z "${newer_src}" ]]; then
            echo "skip: analyzer は最新ビルド済み (${dll_path})"
            return
        fi
    fi

    echo "run: dotnet build ${ANALYZER_PROJECT} -c Release"
    dotnet build "${ANALYZER_PROJECT}" -c Release
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
    build_analyzer

    echo "run: dotnet format analyzers ${FORMAT_PROJECT} ${verify_flag} --severity warn --verbosity diagnostic"
    dotnet format analyzers "${FORMAT_PROJECT}" ${verify_flag:+"${verify_flag}"} --severity warn --verbosity diagnostic

    echo "run: dotnet format whitespace ${FORMAT_PROJECT} ${verify_flag} --verbosity diagnostic"
    dotnet format whitespace "${FORMAT_PROJECT}" ${verify_flag:+"${verify_flag}"} --verbosity diagnostic

    echo "run: dotnet format style ${FORMAT_PROJECT} ${verify_flag} --severity warn --verbosity diagnostic"
    dotnet format style "${FORMAT_PROJECT}" ${verify_flag:+"${verify_flag}"} --severity warn --verbosity diagnostic
}

main "$@"
