#!/usr/bin/env bash

set -euo pipefail

readonly SUBMODULE_DIR_NAME="unity-coding-standards"
readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly SUBMODULE_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly PROJECT_ROOT="$(pwd)"

build_analyzer=true

usage() {
    cat <<'EOF'
Usage:
  scripts/init-unity-project.sh [--skip-build]

Run this from the Unity project root after adding the `unity-coding-standards`
submodule at `./unity-coding-standards`.

What it does:
  - creates symlinks for `.editorconfig`, `Directory.Build.props`, `FormatCheck.csproj`
  - optionally builds the analyzer DLL

Options:
  --skip-build    do not run `dotnet build` for the analyzer project
  -h, --help      show this help
EOF
}

ensure_project_root() {
    if [[ ! -d "${PROJECT_ROOT}/${SUBMODULE_DIR_NAME}" ]]; then
        echo "error: ${SUBMODULE_DIR_NAME}/ が見つかりません。" >&2
        echo "       Unity プロジェクトルートで実行し、submodule を ./unity-coding-standards に配置してください。" >&2
        exit 1
    fi

    if [[ "${SUBMODULE_ROOT}" != "${PROJECT_ROOT}/${SUBMODULE_DIR_NAME}" ]]; then
        echo "error: スクリプトの実体パスと期待する submodule 配置が一致しません。" >&2
        echo "       expected: ${PROJECT_ROOT}/${SUBMODULE_DIR_NAME}" >&2
        echo "       actual:   ${SUBMODULE_ROOT}" >&2
        exit 1
    fi
}

ensure_link_target() {
    local destination="$1"
    local expected_target="$2"

    if [[ -L "${destination}" ]]; then
        local current_target
        current_target="$(readlink "${destination}")"
        if [[ "${current_target}" == "${expected_target}" ]]; then
            echo "skip: ${destination} は既に ${expected_target} を指しています。"
            return
        fi

        echo "error: ${destination} は別の symlink です: ${current_target}" >&2
        exit 1
    fi

    if [[ -e "${destination}" ]]; then
        echo "error: ${destination} が既に存在します。初期化専用スクリプトのため既存ファイルは上書きしません。" >&2
        exit 1
    fi

    ln -s "${expected_target}" "${destination}"
    echo "linked: ${destination} -> ${expected_target}"
}

build_analyzer_dll() {
    local analyzer_project="src/Void2610.Unity.Analyzers/Void2610.Unity.Analyzers.csproj"
    echo "build: ${SUBMODULE_DIR_NAME}/${analyzer_project}"
    (
        cd "${SUBMODULE_ROOT}"
        dotnet build "${analyzer_project}" -c Release
    )
}

main() {
    while (($# > 0)); do
        case "$1" in
            --skip-build)
                build_analyzer=false
                ;;
            -h|--help)
                usage
                exit 0
                ;;
            *)
                echo "error: 不明なオプションです: $1" >&2
                usage >&2
                exit 1
                ;;
        esac
        shift
    done

    ensure_project_root

    ensure_link_target ".editorconfig" "${SUBMODULE_DIR_NAME}/config/.editorconfig"
    ensure_link_target "Directory.Build.props" "${SUBMODULE_DIR_NAME}/config/Directory.Build.props"
    ensure_link_target "FormatCheck.csproj" "${SUBMODULE_DIR_NAME}/config/FormatCheck.csproj"

    if [[ "${build_analyzer}" == true ]]; then
        build_analyzer_dll
    else
        echo "skip: analyzer DLL のビルドは省略しました。"
    fi

    cat <<'EOF'
done:
  - shared config symlinks are ready
  - analyzer build step completed

next:
  dotnet format analyzers FormatCheck.csproj --severity warn
  dotnet format whitespace FormatCheck.csproj
  dotnet format style FormatCheck.csproj --severity warn
EOF
}

main "$@"
