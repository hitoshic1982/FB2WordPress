#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <preview.AppImage>" >&2
  exit 64
fi

appimage="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
[[ -x "$appimage" ]] || { echo "AppImage is missing or not executable: $appimage" >&2; exit 65; }
file "$appimage" | grep -Eq 'ELF 64-bit.*x86-64'

log_file="${TMPDIR:-/tmp}/fb2wordpress-linux-smoke.log"
extract_dir="$(mktemp -d "${TMPDIR:-/tmp}/fb2wordpress-appimage.XXXXXX")"
app_pid=""
cleanup() {
  if [[ -n "$app_pid" ]] && kill -0 "$app_pid" 2>/dev/null; then
    kill -TERM "$app_pid" 2>/dev/null || true
    wait "$app_pid" 2>/dev/null || true
  fi
  rm -rf "$extract_dir"
}
trap cleanup EXIT

(
  cd "$extract_dir"
  "$appimage" --appimage-extract >/dev/null
)
license="$extract_dir/squashfs-root/usr/share/doc/fb2wordpress-preview/LICENSE.txt"
[[ -s "$license" ]] || { echo "The final AppImage does not contain its readable MIT LICENSE." >&2; exit 66; }
grep -Fq 'MIT License' "$license"

APPIMAGE_EXTRACT_AND_RUN=1 "$appimage" >"$log_file" 2>&1 &
app_pid=$!
sleep 6
if ! kill -0 "$app_pid" 2>/dev/null; then
  wait "$app_pid" || status=$?
  echo "The AppImage exited before the native launch smoke window elapsed (status ${status:-unknown})." >&2
  cat "$log_file" >&2 || true
  exit 66
fi

echo "PASS: the final AppImage launched under the native Linux desktop session and stayed alive for six seconds."
