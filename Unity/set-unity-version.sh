#!/usr/bin/env bash
#
# Sets the Unity version the project asks for.
#
#   Unity/set-unity-version.sh 6000.0.32f1
#
# ProjectSettings/ProjectVersion.txt is what Unity Hub reads to decide which
# editor a project belongs to, and a folder without it is not recognised as a
# Unity project at all. The version committed here is a placeholder: set it to
# the LTS you actually have installed, and the choice stops being a guess.
#
# Opening the project with a different editor also works — Unity offers to
# upgrade and rewrites this file itself. That is safe here, because there is no
# scene, no prefab and no serialized asset to migrate yet.

set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "usage: $(basename "$0") <unity-version>    e.g. 6000.0.32f1" >&2
  exit 2
fi

version="$1"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version_file="$repo_root/Unity/FiveDSudoku/ProjectSettings/ProjectVersion.txt"

mkdir -p "$(dirname "$version_file")"
printf 'm_EditorVersion: %s\n' "$version" > "$version_file"

echo "Set to $version:"
cat "$version_file"
