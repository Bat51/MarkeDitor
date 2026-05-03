#!/usr/bin/env bash
# Remove a per-user MarkeDitor install made by install-linux.sh.
set -euo pipefail

DEST_BIN="$HOME/.local/share/markeditor"
DEST_DESKTOP="$HOME/.local/share/applications/markeditor.desktop"
DEST_ICON="$HOME/.local/share/icons/hicolor/256x256/apps/markeditor.png"

rm -rf "$DEST_BIN"
rm -f "$DEST_DESKTOP" "$DEST_ICON"

update-desktop-database "$HOME/.local/share/applications" >/dev/null 2>&1 || true
gtk-update-icon-cache "$HOME/.local/share/icons/hicolor" >/dev/null 2>&1 || true

echo "Uninstalled."
