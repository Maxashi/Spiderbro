#!/usr/bin/env bash

set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity_editor="${UNITY_EDITOR:-unity}"
output_directory="${1:-"$project_root/Builds/WebGL"}"
compression="${SPIDERBRO_WEBGL_COMPRESSION:-Brotli}"
development="${SPIDERBRO_DEVELOPMENT_BUILD:-0}"

if [[ "$output_directory" != /* ]]; then
  output_directory="$project_root/$output_directory"
fi

if [[ "$development" != "0" && "$development" != "1" ]]; then
  echo "SPIDERBRO_DEVELOPMENT_BUILD must be 0 or 1." >&2
  exit 2
fi

case "$compression" in
  Disabled|Gzip|Brotli) ;;
  *)
    echo "SPIDERBRO_WEBGL_COMPRESSION must be Disabled, Gzip, or Brotli." >&2
    exit 2
    ;;
esac

"$unity_editor" \
  -batchmode \
  -nographics \
  -quit \
  -projectPath "$project_root" \
  -executeMethod Spiderbro.Editor.Shipping.WebGLBuild.Build \
  -buildOutput "$output_directory" \
  -buildCompression "$compression" \
  -buildDevelopment "$development" \
  -logFile -
