#!/usr/bin/env bash
# Cross-build a self-contained Windows x64 release of MarkeDitor.
# Produces Publish/MarkeDitor-win-x64/bin/MarkeDitor.exe ready to zip
# or to feed into Inno Setup on a Windows machine.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$ROOT/MarkeDitor/MarkeDitor.csproj"
OUT="$ROOT/Publish/MarkeDitor-win-x64"
RID="win-x64"
VERSION="${VERSION:-1.0.0}"

rm -rf "$OUT"
mkdir -p "$OUT"

echo "==> dotnet publish -c Release -r $RID --self-contained"
dotnet publish "$PROJECT" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:DebugType=embedded \
    -p:Version="$VERSION" \
    -o "$OUT/bin"

find "$OUT/bin" \( -name "*.pdb" -o -name "*.xml" \) -delete || true

echo
echo "==> Done."
echo "Output: $OUT/bin/MarkeDitor.exe"
echo "On a Windows machine, run Inno Setup against installer.iss to make an installer."
