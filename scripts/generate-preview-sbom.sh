#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 5 ]]; then
  echo "Usage: $0 <version> <build-drop> <artifact-directory> <sbom-tool> <platform-name>" >&2
  exit 64
fi

version="$1"
build_drop="$(cd "$2" && pwd)"
artifact_dir="$(cd "$3" && pwd)"
sbom_tool="$(cd "$(dirname "$4")" && pwd)/$(basename "$4")"
platform="$5"

[[ -x "$sbom_tool" ]] || { echo "SBOM tool is not executable: $sbom_tool" >&2; exit 65; }
rm -rf "$build_drop/_manifest"
"$sbom_tool" generate \
  -b "$build_drop" \
  -bc "$GITHUB_WORKSPACE" \
  -pn "FB2WordPress ${platform} Preview" \
  -pv "$version" \
  -ps "Flameblade Studio" \
  -nsb "https://github.com/hitoshic1982/FB2WordPress/blob/${GITHUB_SHA}/" \
  -V Information

manifest="$build_drop/_manifest/spdx_2.2/manifest.spdx.json"
[[ -s "$manifest" ]] || { echo "Microsoft SBOM Tool did not produce an SPDX manifest." >&2; exit 66; }
cp "$manifest" "$artifact_dir/FB2WordPress-v${version}-${platform}-Preview.spdx.json"
rm -rf "$build_drop/_manifest"
