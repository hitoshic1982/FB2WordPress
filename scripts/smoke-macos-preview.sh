#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <preview.dmg>" >&2
  exit 64
fi

dmg="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
[[ -f "$dmg" ]] || { echo "DMG not found: $dmg" >&2; exit 65; }

mount_dir="$(mktemp -d "${TMPDIR:-/tmp}/fb2wordpress-dmg.XXXXXX")"
app_pid=""
cleanup() {
  if [[ -n "$app_pid" ]] && kill -0 "$app_pid" 2>/dev/null; then
    kill -TERM "$app_pid" 2>/dev/null || true
    wait "$app_pid" 2>/dev/null || true
  fi
  hdiutil detach "$mount_dir" -quiet 2>/dev/null || true
  rmdir "$mount_dir" 2>/dev/null || true
}
trap cleanup EXIT

hdiutil attach "$dmg" -readonly -nobrowse -mountpoint "$mount_dir" -quiet
bundle="$mount_dir/FB2WordPress Preview.app"
executable="$bundle/Contents/MacOS/FB2WordPress.Desktop"
[[ -x "$executable" ]] || { echo "Mounted DMG does not contain the executable app bundle." >&2; exit 66; }
plutil -lint "$bundle/Contents/Info.plist"
if codesign --verify --deep --strict "$bundle" >/dev/null 2>&1; then
  echo "Expected an unsigned Preview app, but signature verification succeeded." >&2
  exit 67
fi

"$executable" >"${TMPDIR:-/tmp}/fb2wordpress-macos-smoke.log" 2>&1 &
app_pid=$!
sleep 6
if ! kill -0 "$app_pid" 2>/dev/null; then
  wait "$app_pid" || status=$?
  echo "The app from the mounted DMG exited before the native launch smoke window elapsed (status ${status:-unknown})." >&2
  cat "${TMPDIR:-/tmp}/fb2wordpress-macos-smoke.log" >&2 || true
  exit 68
fi

echo "PASS: the unsigned app launched directly from the mounted DMG and stayed alive for six seconds."
