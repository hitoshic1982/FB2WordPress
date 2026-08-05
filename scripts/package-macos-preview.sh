#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 4 ]]; then
  echo "Usage: $0 <version> <publish-directory> <artifact-directory> <x64|arm64>" >&2
  exit 64
fi

version="$1"
publish_dir="$(cd "$2" && pwd)"
artifact_dir="$3"
architecture="$4"
app_name="FB2WordPress Preview"
bundle_name="${app_name}.app"
bundle_id="tw.com.flamebladestudio.fb2wordpress.preview"
executable_name="FB2WordPress.Desktop"
numeric_version="${version%%-*}"
bundle_version="${version##*-rc.}"
release_notes="RELEASE_NOTES_v${version}.md"

if [[ ! "$version" =~ ^1\.1\.0-rc\.([1-9][0-9]*)$ ]]; then
  echo "Preview version must match 1.1.0-rc.N with N greater than zero: $version" >&2
  exit 65
fi
case "$architecture" in
  x64)
    expected_binary_arch='x86_64'
    expected_runner_machine='x86_64'
    ;;
  arm64)
    expected_binary_arch='arm64'
    expected_runner_machine='arm64'
    ;;
  *)
    echo "Unsupported macOS architecture: $architecture" >&2
    exit 65
    ;;
esac
if [[ "$(uname -m)" != "$expected_runner_machine" ]]; then
  echo "The $architecture DMG must be built on its matching native runner; found $(uname -m)." >&2
  exit 65
fi
if [[ ! -x "$publish_dir/$executable_name" ]]; then
  echo "Published macOS executable is missing: $publish_dir/$executable_name" >&2
  exit 66
fi
[[ -f "$release_notes" ]] || { echo "Four-language Preview release notes are missing: $release_notes" >&2; exit 68; }
[[ "$(lipo -archs "$publish_dir/$executable_name")" == "$expected_binary_arch" ]] || {
  echo "Published executable architecture does not match $architecture." >&2
  exit 66
}

mkdir -p "$artifact_dir"
artifact_dir="$(cd "$artifact_dir" && pwd)"
work_dir="$(mktemp -d "${TMPDIR:-/tmp}/fb2wordpress-macos.XXXXXX")"
trap 'rm -rf "$work_dir"' EXIT

bundle="$work_dir/$bundle_name"
contents="$bundle/Contents"
mkdir -p "$contents/MacOS" "$contents/Resources"
cp -R "$publish_dir"/. "$contents/MacOS/"
chmod +x "$contents/MacOS/$executable_name"
cp "assets/fb2wordpress-preview.svg" "$contents/Resources/fb2wordpress-preview.svg"
cp "$release_notes" "$contents/Resources/PREVIEW_LIMITATIONS.md"
cp LICENSE "$contents/Resources/LICENSE.txt"

cat > "$contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>CFBundleDisplayName</key><string>${app_name}</string>
  <key>CFBundleExecutable</key><string>${executable_name}</string>
  <key>CFBundleIdentifier</key><string>${bundle_id}</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleName</key><string>${app_name}</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>${numeric_version}</string>
  <key>CFBundleVersion</key><string>${bundle_version}</string>
  <key>FBPreviewVersion</key><string>${version}</string>
  <key>LSApplicationCategoryType</key><string>public.app-category.utilities</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

plutil -lint "$contents/Info.plist"
if [[ -d "$contents/_CodeSignature" ]] || codesign --verify --deep --strict "$bundle" >/dev/null 2>&1; then
  echo "The macOS Preview must remain unsigned until a real Apple signing identity is configured." >&2
  exit 67
fi

staging="$work_dir/dmg-root"
mkdir -p "$staging"
mv "$bundle" "$staging/$bundle_name"
ln -s /Applications "$staging/Applications"
cp "$release_notes" "$staging/README - Preview limitations.md"
cp LICENSE "$staging/LICENSE.txt"

artifact="$artifact_dir/FB2WordPress-v${version}-macOS-${architecture}-Preview.dmg"
hdiutil create \
  -volname "FB2WordPress Preview" \
  -srcfolder "$staging" \
  -format UDZO \
  -imagekey zlib-level=9 \
  -ov \
  "$artifact"
hdiutil imageinfo "$artifact" >/dev/null
printf '%s\n' "$artifact"
