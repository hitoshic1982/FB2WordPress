#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 4 ]]; then
  echo "Usage: $0 <version> <publish-directory> <artifact-directory> <appimagetool>" >&2
  exit 64
fi

version="$1"
publish_dir="$(cd "$2" && pwd)"
artifact_dir="$3"
appimagetool="$(cd "$(dirname "$4")" && pwd)/$(basename "$4")"
executable_name="FB2WordPress.Desktop"
release_notes="RELEASE_NOTES_v${version}.md"

if [[ ! "$version" =~ ^1\.1\.0-rc\.([1-9][0-9]*)$ ]]; then
  echo "Preview version must match 1.1.0-rc.N with N greater than zero: $version" >&2
  exit 65
fi
if [[ ! -x "$publish_dir/$executable_name" ]]; then
  echo "Published Linux executable is missing: $publish_dir/$executable_name" >&2
  exit 66
fi
[[ -f "$release_notes" ]] || { echo "Four-language Preview release notes are missing: $release_notes" >&2; exit 68; }
file "$publish_dir/$executable_name" | grep -Eq 'ELF 64-bit.*x86-64'
[[ -x "$appimagetool" ]] || { echo "appimagetool is not executable: $appimagetool" >&2; exit 67; }

mkdir -p "$artifact_dir"
artifact_dir="$(cd "$artifact_dir" && pwd)"
work_dir="$(mktemp -d "${TMPDIR:-/tmp}/fb2wordpress-linux.XXXXXX")"
trap 'rm -rf "$work_dir"' EXIT
appdir="$work_dir/FB2WordPress.AppDir"
mkdir -p "$appdir/usr/bin" "$appdir/usr/share/applications" "$appdir/usr/share/icons/hicolor/scalable/apps" "$appdir/usr/share/doc/fb2wordpress-preview"
cp -R "$publish_dir"/. "$appdir/usr/bin/"
chmod +x "$appdir/usr/bin/$executable_name"
cp assets/fb2wordpress-preview.svg "$appdir/fb2wordpress-preview.svg"
cp assets/fb2wordpress-preview.svg "$appdir/usr/share/icons/hicolor/scalable/apps/fb2wordpress-preview.svg"
ln -s fb2wordpress-preview.svg "$appdir/.DirIcon"
cp "$release_notes" "$appdir/usr/share/doc/fb2wordpress-preview/PREVIEW_LIMITATIONS.md"
cp LICENSE "$appdir/usr/share/doc/fb2wordpress-preview/LICENSE.txt"

cat > "$appdir/FB2WordPress.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=FB2WordPress Preview
Comment=Safe migration foundation preview; not the complete Windows workflow
Exec=FB2WordPress.Desktop
Icon=fb2wordpress-preview
Terminal=false
Categories=Utility;
X-AppImage-Version=${version}
DESKTOP
cp "$appdir/FB2WordPress.desktop" "$appdir/usr/share/applications/FB2WordPress.desktop"

cat > "$appdir/AppRun" <<'APPRUN'
#!/bin/sh
set -eu
here="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
exec "$here/usr/bin/FB2WordPress.Desktop" "$@"
APPRUN
chmod +x "$appdir/AppRun"

artifact="$artifact_dir/FB2WordPress-v${version}-Linux-x86_64-Preview.AppImage"
ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "$appimagetool" "$appdir" "$artifact"
chmod +x "$artifact"
file "$artifact" | grep -Eq 'ELF 64-bit.*x86-64'
printf '%s\n' "$artifact"
