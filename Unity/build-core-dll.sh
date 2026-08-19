#!/usr/bin/env bash
#
# Builds Core and puts it where Unity can see it.
#
# Core is written in C# 10 and Unity's compiler is C# 9, so Unity never sees the
# source: it gets the compiled netstandard2.1 assembly instead, which is exactly
# what Unity consumes. That also keeps the language level a Core-internal
# concern rather than a standing rule someone eventually breaks.
#
# Run this after any change to Core/, and before opening the Unity project for
# the first time.
#
#   Unity/build-core-dll.sh            build and copy
#   Unity/build-core-dll.sh --check    fail if the copy is out of date
#
# --check is the staleness guard. It recomputes the source fingerprint and
# compares it with the committed one, so a Core change that never made it into
# the assembly is caught rather than discovered later as a mystery.

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
core_project="$repo_root/Core/Core.csproj"
plugin_dir="$repo_root/Unity/FiveDSudoku/Assets/Plugins/FiveDSudoku"
fingerprint_file="$plugin_dir/core-source.sha256"

check_only=0
if [[ "${1:-}" == "--check" ]]; then
  check_only=1
elif [[ $# -gt 0 ]]; then
  echo "usage: $(basename "$0") [--check]" >&2
  exit 2
fi

# Fingerprint the sources rather than the assembly. Two builds of identical
# sources are not byte-identical, so comparing assemblies would report a
# difference on every build; comparing what went in reports one only when
# something actually changed.
fingerprint_sources() {
  find "$repo_root/Core" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' \
    | LC_ALL=C sort \
    | while read -r file; do
        printf '%s ' "${file#"$repo_root/"}"
        sha256sum "$file" | cut -d' ' -f1
      done \
    | sha256sum | cut -d' ' -f1
}

current="$(fingerprint_sources)"

if [[ $check_only -eq 1 ]]; then
  if [[ ! -f "$fingerprint_file" ]]; then
    echo "FAIL: no fingerprint at $fingerprint_file — run $(basename "$0")" >&2
    exit 1
  fi

  recorded="$(cat "$fingerprint_file")"

  if [[ "$current" != "$recorded" ]]; then
    echo "FAIL: the Core assembly in Assets/Plugins is out of date." >&2
    echo "  recorded $recorded" >&2
    echo "  sources  $current" >&2
    echo "  run: Unity/build-core-dll.sh" >&2
    exit 1
  fi

  echo "PASS: the Core assembly matches Core/ ($current)"
  exit 0
fi

echo "Building Core..."
dotnet build "$core_project" -c Release --nologo -v quiet

built="$repo_root/Core/bin/Release/netstandard2.1"

mkdir -p "$plugin_dir"
cp "$built/FiveDSudoku.Core.dll" "$plugin_dir/"

# The portable PDB is what makes a Core stack trace from inside Unity readable.
if [[ -f "$built/FiveDSudoku.Core.pdb" ]]; then
  cp "$built/FiveDSudoku.Core.pdb" "$plugin_dir/"
fi

printf '%s' "$current" > "$fingerprint_file"

echo "Copied to Assets/Plugins/FiveDSudoku:"
ls -1 "$plugin_dir"
echo "Source fingerprint: $current"
