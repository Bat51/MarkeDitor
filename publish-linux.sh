#!/usr/bin/env bash
# Build a self-contained Linux x64 release of MarkeDitor and package
# it into Publish/MarkeDitor-linux-x64/ together with a .desktop file
# and the icon, ready to install or zip.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$ROOT/MarkeDitor/MarkeDitor.csproj"
OUT="$ROOT/Publish/MarkeDitor-linux-x64"
RID="linux-x64"
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

# Strip pdbs/xml docs we don't want shipped
find "$OUT/bin" \( -name "*.pdb" -o -name "*.xml" \) -delete || true

# .desktop file (so the icon appears in GNOME / KDE launchers)
cat > "$OUT/markeditor.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=MarkeDitor
GenericName=Markdown Editor
Comment=Markdown editor with live preview
Exec=$OUT/bin/MarkeDitor %F
Icon=$OUT/bin/Assets/app.png
Terminal=false
Categories=Office;TextEditor;
MimeType=text/markdown;text/x-markdown;
StartupWMClass=MarkeDitor
EOF
chmod +x "$OUT/markeditor.desktop"

# Quick install hint
cat > "$OUT/INSTALL.md" <<'EOF'
# Install MarkeDitor (Linux)

## Run without installing
    ./bin/MarkeDitor

## Install per-user (recommended)
    cp -r bin "$HOME/.local/share/markeditor"
    sed -i "s|$(pwd)/bin|$HOME/.local/share/markeditor|g" markeditor.desktop
    install -Dm644 markeditor.desktop "$HOME/.local/share/applications/markeditor.desktop"
    install -Dm644 bin/Assets/app.png "$HOME/.local/share/icons/hicolor/256x256/apps/markeditor.png"
    update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true

After this, "MarkeDitor" appears in the GNOME Activities / KDE menu with the
correct icon, and `*.md` files can be opened with it from the file manager.

## Uninstall
    rm -rf "$HOME/.local/share/markeditor"
    rm -f "$HOME/.local/share/applications/markeditor.desktop"
    rm -f "$HOME/.local/share/icons/hicolor/256x256/apps/markeditor.png"
EOF

echo
echo "==> Done."
echo "Output: $OUT"
echo "Run:    $OUT/bin/MarkeDitor"
echo "Install: see $OUT/INSTALL.md"
