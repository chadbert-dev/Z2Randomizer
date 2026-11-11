#!/usr/bin/env bash
# Builds release zips of the desktop app for Linux, Windows and macOS, without .pdb files.
# Usage: scripts/build-release.sh [version] [rid...]
#   version  label used in the zip names (default: "dev")
#   rid      runtime identifiers to build (default: linux-x64 win-x64 osx-arm64 osx-x64)
# Output: dist/Z2Randomizer-<version>-<rid>.zip
set -euo pipefail

cd "$(dirname "$0")/.."

version="${1:-dev}"
shift || true
rids=("$@")
if [ ${#rids[@]} -eq 0 ]; then
    rids=(linux-x64 win-x64 osx-arm64 osx-x64)
fi

if [ ! -f FtRandoLib/README.md ] && [ -z "$(ls -A FtRandoLib 2>/dev/null)" ]; then
    git submodule update --init
fi

project=CrossPlatformUI.Desktop/CrossPlatformUI.Desktop.csproj
out_root=dist
mkdir -p "$out_root"

for rid in "${rids[@]}"; do
    echo "=== Building $rid ==="
    publish_dir="$out_root/publish/$rid"
    rm -rf "$publish_dir"

    # DebugType=none keeps the pdb files from being produced at all
    dotnet publish "$project" -c Release -r "$rid" --self-contained true \
        -p:DebugType=none -p:DebugSymbols=false \
        -o "$publish_dir"

    # catch any that still slip through, such as ones from referenced packages
    find "$publish_dir" -name '*.pdb' -delete

    zip_path="$PWD/$out_root/Z2Randomizer-$version-$rid.zip"
    rm -f "$zip_path"
    (cd "$publish_dir" && zip -q -r -9 "$zip_path" .)
    echo "Created $zip_path"
done
