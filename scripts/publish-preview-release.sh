#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: $0 <tag> <version> <release-bundle-directory>" >&2
  exit 64
fi

tag="$1"
version="$2"
bundle_dir="$(cd "$3" && pwd)"
notes="RELEASE_NOTES_v${version}.md"

[[ "$version" =~ ^1\.1\.0-rc\.([1-9][0-9]*)$ ]] || {
  echo "Version must match 1.1.0-rc.N with N greater than zero: $version" >&2
  exit 65
}
[[ "$tag" == "v$version" ]] || { echo "Tag and PreviewVersion differ: $tag / $version" >&2; exit 65; }
tag_push=false
manual_recovery=false
[[ "$GITHUB_EVENT_NAME" == "push" && "$GITHUB_REF_TYPE" == "tag" ]] && tag_push=true
[[ "$GITHUB_EVENT_NAME" == "workflow_dispatch" && "${TRUSTED_RELEASE_RECOVERY:-}" == "true" ]] && manual_recovery=true
[[ "$tag_push" == "true" || "$manual_recovery" == "true" ]] || {
  echo "Publishing requires a tag push or a trusted, validated recovery dispatch." >&2
  exit 65
}
[[ -n "${GITHUB_REPOSITORY:-}" && -n "${GH_TOKEN:-}" ]] || {
  echo "Trusted GitHub release context is unavailable." >&2
  exit 65
}
[[ -s "$notes" ]] || { echo "Four-language release notes are missing: $notes" >&2; exit 66; }
for heading in '## 繁體中文' '## 简体中文' '## English' '## 日本語'; do
  grep -q "^${heading}" "$notes" || { echo "Release notes are missing heading: $heading" >&2; exit 66; }
done

expected=(
  "FB2WordPress-v${version}-Windows-x64.exe"
  "FB2WordPress-v${version}-Windows-x64-Preview.spdx.json"
  "FB2WordPress-v${version}-macOS-x64-Preview.dmg"
  "FB2WordPress-v${version}-macOS-x64-Preview.spdx.json"
  "FB2WordPress-v${version}-macOS-arm64-Preview.dmg"
  "FB2WordPress-v${version}-macOS-arm64-Preview.spdx.json"
  "FB2WordPress-v${version}-Linux-x86_64-Preview.AppImage"
  "FB2WordPress-v${version}-Linux-x86_64-Preview.spdx.json"
  'LICENSE.txt'
  "RELEASE_NOTES_v${version}.md"
  'SHA256SUMS.txt'
)
for name in "${expected[@]}"; do
  [[ -s "$bundle_dir/$name" ]] || { echo "Required release asset is missing: $name" >&2; exit 66; }
done
cmp -s "$notes" "$bundle_dir/$notes" || { echo "Bundled release notes differ from the reviewed source file." >&2; exit 66; }
(cd "$bundle_dir" && sha256sum --check --strict SHA256SUMS.txt)

existing_id="$(gh api --paginate "repos/$GITHUB_REPOSITORY/releases?per_page=100" --jq ".[] | select(.tag_name == \"$tag\") | .id" | head -n 1)"
if [[ -n "$existing_id" ]]; then
  echo "A GitHub Release already exists for $tag; refusing to overwrite it." >&2
  exit 67
fi

release_id=''
published=false
cleanup_failed_draft() {
  status=$?
  if [[ $status -ne 0 && "$published" != true && -n "$release_id" ]]; then
    gh api --method DELETE "repos/$GITHUB_REPOSITORY/releases/$release_id" >/dev/null 2>&1 || true
  fi
  trap - EXIT
  exit "$status"
}
trap cleanup_failed_draft EXIT

# Draft first: a failed upload or verification can never expose a partial
# prerelease. Cleanup deletes only the draft Release record, never the tag.
upload_paths=()
for name in "${expected[@]}"; do upload_paths+=("$bundle_dir/$name"); done
gh release create "$tag" "${upload_paths[@]}" \
  --repo "$GITHUB_REPOSITORY" \
  --verify-tag \
  --draft \
  --prerelease \
  --title "FB2WordPress $version Preview" \
  --notes-file "$notes"
release_id="$(gh api --paginate "repos/$GITHUB_REPOSITORY/releases?per_page=100" --jq ".[] | select(.tag_name == \"$tag\" and .draft == true) | .id" | head -n 1)"
[[ "$release_id" =~ ^[0-9]+$ ]] || { echo "Unable to resolve the draft Release ID." >&2; exit 68; }

mapfile -t actual_assets < <(gh api --paginate "repos/$GITHUB_REPOSITORY/releases/$release_id/assets" --jq '.[].name' | sort)
mapfile -t expected_assets < <(printf '%s\n' "${expected[@]}" | sort)
if [[ "$(printf '%s\n' "${actual_assets[@]}")" != "$(printf '%s\n' "${expected_assets[@]}")" ]]; then
  echo "Draft Release assets differ from the exact expected set." >&2
  printf 'Expected:\n%s\nActual:\n%s\n' "$(printf '%s\n' "${expected_assets[@]}")" "$(printf '%s\n' "${actual_assets[@]}")" >&2
  exit 69
fi

gh api --method PATCH "repos/$GITHUB_REPOSITORY/releases/$release_id" \
  -F draft=false \
  -F prerelease=true >/dev/null
[[ "$(gh api "repos/$GITHUB_REPOSITORY/releases/$release_id" --jq '.draft == false and .prerelease == true')" == true ]] || {
  echo "GitHub did not confirm the final prerelease state." >&2
  exit 70
}

published=true
trap - EXIT
echo "Published verified prerelease $tag with ${#expected[@]} exact assets."
