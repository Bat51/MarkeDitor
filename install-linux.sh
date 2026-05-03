#!/usr/bin/env bash
# Install MarkeDitor for the current user (no sudo required).
# Run this AFTER ./publish-linux.sh has produced the build folder.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
SRC_BIN="$ROOT/Publish/MarkeDitor-linux-x64/bin"

if [ ! -x "$SRC_BIN/MarkeDitor" ]; then
    echo "Error: $SRC_BIN/MarkeDitor not found." >&2
    echo "Run ./publish-linux.sh first." >&2
    exit 1
fi

DEST_BIN="$HOME/.local/share/markeditor"
DEST_DESKTOP="$HOME/.local/share/applications/markeditor.desktop"
DEST_ICON="$HOME/.local/share/icons/hicolor/256x256/apps/markeditor.png"

echo "==> Copying app -> $DEST_BIN"
mkdir -p "$DEST_BIN"
cp -r "$SRC_BIN/." "$DEST_BIN/"

echo "==> Installing icon -> $DEST_ICON"
install -Dm644 "$SRC_BIN/Assets/app.png" "$DEST_ICON"

echo "==> Installing desktop entry -> $DEST_DESKTOP"
mkdir -p "$(dirname "$DEST_DESKTOP")"
cat > "$DEST_DESKTOP" <<EOF
[Desktop Entry]
Type=Application
Name=MarkeDitor
GenericName=Markdown Editor
Comment=Markdown editor with live preview
Exec=$DEST_BIN/MarkeDitor %F
Icon=markeditor
Terminal=false
Categories=Office;TextEditor;
MimeType=text/markdown;text/x-markdown;
StartupWMClass=MarkeDitor
EOF

# Refresh desktop database / mime cache so the launcher and "Open with" menu
# both see MarkeDitor immediately.
update-desktop-database "$HOME/.local/share/applications" >/dev/null 2>&1 || true
gtk-update-icon-cache "$HOME/.local/share/icons/hicolor" >/dev/null 2>&1 || true

echo
echo "Installed."
echo "Launch via GNOME Activities, or: $DEST_BIN/MarkeDitor"
echo
echo "To set MarkeDitor as the default for *.md files:"
echo "  xdg-mime default markeditor.desktop text/markdown"
echo
echo "To uninstall:"
echo "  ./uninstall-linux.sh    # if you keep this script around"
echo "  or manually:"
echo "  rm -rf $DEST_BIN"
echo "  rm -f $DEST_DESKTOP $DEST_ICON"
